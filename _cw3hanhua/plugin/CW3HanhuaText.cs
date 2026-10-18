// ============================================================================
//  CW3 汉化 · 文本层 v0.8.3 —— 词表 + UILabel/TextMesh 替换
//
//  与字体层（CW3Hanhua.cs）**独立编译、独立运行**：
//    字体层负责"字形从哪来"，本层负责"英文串怎么变中文"。两层可分别开关，
//    任何一层出问题都能立刻判断是谁的锅。
//
//  词表：_cw3hanhua/_dict/zh_cn.dict（tools/build_dict.py 生成）
//    格式见 build_dict.py 头注释：CWH1 + count + [klen,key,zlen,zh]* + uint32 FNV
//
//  三条替换路径，缺一不可：
//    ① Harmony prefix on UILabel.set_text   —— 322 个调用点，运行时动态文本走这里
//    ② 周期 sweep（含未激活 label）        —— 资产里序列化的 mText 不经过 setter（NOTES 3.3）
//    ③ TextMesh 周期 sweep                 —— 主菜单大按钮等 15 个写入点
//
//  v0.8.3：查表增加 LookupConcat —— 游戏有一批文本是运行时 Concat 出来的
//    （主菜单悬浮框 body + "\n[ff5050]To Access:..."），词表里只有两个半截，
//    精确匹配必然落空。见 LookupConcat 注释与 NOTES 3.3「拼接串」。
//
//  按键：
//    F4  文本层 开/关（开=接词表并替换；关=尽力还原）
//    F5  只做一次全量 sweep（不改变开关状态，用来看当前命中率）
//    F9  重新载入词表（改完 zh_cn.dict 不用重启游戏）
//
//  编译（见 NOTES 5.2）：
//    csc /target:library /platform:x86 /codepage:65001
//        /reference:BepInEx.dll /reference:0Harmony.dll
//        /reference:UnityEngine.dll /reference:Assembly-CSharp.dll
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace CW3Hanhua
{
    [BepInPlugin("cw3hanhua.text", "CW3 Hanhua Text", "0.8.2")]
    public class TextPlugin : BaseUnityPlugin
    {
        const string Ver = "0.8.2";

        static TextPlugin inst;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        // ---------------------------------------------------------------- 词表
        static Dictionary<string, string> dict = new Dictionary<string, string>();
        static string dictPath;
        static string loadError = "";
        static int dictCount;

        // ---------------------------------------------------------------- 状态
        static bool textOn = false;
        static int frame;
        static string sceneName = "";

        // 统计：改了多少、命中多少
        static int swMesh;
        static int hitMesh;
        static int swLabel;
        static int hitLabel;
        static int prefixCalls;
        static int prefixHits;
        static int hitConcat;       // 靠 LookupConcat 拼出来的条数（主菜单悬浮框那类）

        // 已替换记录：用于关闭时还原。
        // ★ 按 InstanceID 索引，**不能**用 List + 切场景时 Clear() ——
        //   踩过：Clear() 之后 F4 关闭时 RestoreAll 变成空操作，留下中英混杂。
        //   切场景只该丢弃**已销毁**的对象，仍然活着的必须留住以便还原。
        class Swap { public UILabel label; public TextMesh mesh; public string en; public string zh; }
        static readonly Dictionary<int, Swap> swaps = new Dictionary<int, Swap>();

        // 已知无法替换的（例如查不到词表），每帧不要重复刷日志
        static readonly HashSet<string> missSeen = new HashSet<string>();

        // ---------------------------------------------------------------- 日志
        static void Say(string s)
        {
            try { if (inst != null) inst.Logger.LogInfo(s); } catch (Exception) { }
        }

        // ================================================================ 生命周期
        void Awake()
        {
            inst = this;
            try
            {
                // ★ 游戏程序集里有全局 Path 类，会抢掉 System.IO.Path（NOTES 5.3 #4）
                dictPath = System.IO.Path.Combine(Paths.GameRootPath, "_cw3hanhua");
                dictPath = System.IO.Path.Combine(dictPath, "_dict");
                dictPath = System.IO.Path.Combine(dictPath, "zh_cn.dict");
            }
            catch (Exception) { dictPath = null; }

            Say("==============================================================");
            Say(" CW3 汉化 · 文本层 v" + Ver);
            Say("==============================================================");
            Say("词表: " + dictPath);
            LoadDict();

            try
            {
                var m = AccessTools.Method(typeof(UILabel), "set_text", null, null);
                if (m == null) { Say("!! 找不到 UILabel.set_text，prefix 未安装"); }
                else
                {
                    Harmony h = new Harmony("cw3hanhua.text");
                    // ★ 6 参数重载：不用的槽位传字面 null，不要 new HarmonyMethod(null)（NOTES 5.3 #3 / 5.6）
                    h.Patch(m, new HarmonyMethod(typeof(TextPlugin).GetMethod("PrefixSetText",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)),
                        null, null, null, null);
                    Say("★ 已装 prefix: UILabel.set_text");
                }
            }
            catch (Exception e) { Say("!! 装 prefix 失败: " + e); }

            Say("按键: F4 文本层开关 | F5 单次 sweep | F9 重载词表");
        }

        // ================================================================ 词表读取
        void LoadDict()
        {
            dict.Clear();
            dictCount = 0;
            loadError = "";
            if (dictPath == null || !File.Exists(dictPath))
            {
                loadError = "找不到词表（先跑 tools\\build_dict.py）";
                Say("!! " + loadError + ": " + dictPath);
                return;
            }
            try
            {
                using (BinaryReader br = new BinaryReader(File.OpenRead(dictPath), Utf8))
                {
                    byte[] magic = br.ReadBytes(4);
                    if (magic.Length != 4 || magic[0] != 'C' || magic[1] != 'W' || magic[2] != 'H' || magic[3] != '1')
                    {
                        loadError = "magic 不对，不是 CWH1 格式";
                        Say("!! " + loadError);
                        return;
                    }
                    int n = br.ReadInt32();
                    // ★ 校验区 = magic 之后、计数之后、校验值之前 的全部原始字节，
                    //   必须**逐字节照抄**（含 uint16 长度前缀）。build_dict.py 那边算的是 raw[8:-4]。
                    //   踩过：只累加 key/zh、漏掉长度前缀 → 两端字节流不同 → 恒报"校验失败"。
                    MemoryStream body = new MemoryStream();
                    for (int i = 0; i < n; i++)
                    {
                        int klen = br.ReadUInt16();
                        byte[] kb = br.ReadBytes(klen);
                        byte[] klb = BitConverter.GetBytes((ushort)klen);
                        body.Write(klb, 0, 2);
                        body.Write(kb, 0, kb.Length);
                        int zlen = br.ReadUInt16();
                        byte[] zb = br.ReadBytes(zlen);
                        byte[] zlb = BitConverter.GetBytes((ushort)zlen);
                        body.Write(zlb, 0, 2);
                        body.Write(zb, 0, zb.Length);
                        string k = Utf8.GetString(kb);
                        string z = Utf8.GetString(zb);
                        dict[k] = z;
                    }
                    uint expect = br.ReadUInt32();
                    uint got = Fnv1a(body.GetBuffer(), (int)body.Length);
                    if (expect != got)
                        Say("!! 词表校验失败（期望 " + expect + " 实际 " + got + "），仍然使用但请重新生成");
                    dictCount = dict.Count;
                    Say("词表已载入: " + dictCount + " 条");
                }
            }
            catch (Exception e)
            {
                loadError = "词表读取异常: " + e.Message;
                Say("!! " + loadError);
            }
        }

        static uint Fnv1a(byte[] data, int len)
        {
            uint h = 2166136261u;
            for (int i = 0; i < len; i++) { h ^= data[i]; h *= 16777619u; }
            return h;
        }

        /// <summary>查词表；未命中返回 null。查表用**原样**匹配（首尾空格都算），
        /// 因为像 "PACKET SPEED.  " 这种尾部空格是拼接前缀的一部分。
        ///
        /// ★ 精确匹配失败后走 LookupConcat：游戏有一批文本是**运行时拼出来的**，
        ///   拼完才 set_text，词表里只有两个半截、没有整串。典型就是主菜单的悬浮框：
        ///       body + "\n[ff5050]To Access:\n[ff0000]Complete first 5 Arc Eternal missions."
        ///   见 MainMenu.OnMouseEnterEntryManager / MainMenu.Update 的
        ///   `ldloc.0; ldstr 后缀; call String.Concat(String,String); stloc.0`。
        ///   静态收集器（tools/collect_ui_strings.py）只认 ldstr，收不到 Concat 的结果。</summary>
        static string Lookup(string s)
        {
            if (s == null || s.Length == 0 || dict.Count == 0) return null;
            string zh;
            if (dict.TryGetValue(s, out zh) && zh != null && zh.Length > 0) return zh;
            return LookupConcat(s, 0);
        }

        /// <summary>把整串在 '\n' 处切成两段，两段**各自精确命中**词表才拼接返回。
        /// 只要有一段没命中就返回 null —— 宁可留英文，也不能拼出中英混杂。
        /// 从右往左找切点：动态拼接几乎都是「正文 + 追加段」，切点靠后。
        /// depth 限 2 层，防止病态串把递归撑大。</summary>
        static string LookupConcat(string s, int depth)
        {
            if (depth >= 2 || s.Length < 2) return null;
            for (int i = s.Length - 1; i > 0; i--)
            {
                if (s[i] != '\n') continue;
                string head = s.Substring(0, i);
                string tail = s.Substring(i);          // 保留前导 '\n'，词表里就是这么存的
                string h, t;
                if (!dict.TryGetValue(head, out h) || h == null || h.Length == 0)
                    h = LookupConcat(head, depth + 1);
                if (h == null) continue;
                if (!dict.TryGetValue(tail, out t) || t == null || t.Length == 0)
                    t = LookupConcat(tail, depth + 1);
                if (t == null) continue;
                hitConcat++;
                return h + t;
            }
            return null;
        }

        // ================================================================ Patch
        /// <summary>
        /// ★ UILabel.set_text 的 prefix。
        ///
        /// 注意：prefix 里改写 __0 后，原方法会用它做 SetDirty 等，
        /// 所以"改传入值"是最安全的做法（NOTES 5.6 的第一个假象就是 prefix 里改 __result）。
        ///
        /// ★ 必须登记到 swaps：否则 F4 关闭时这些动态文本无处还原，会留下中英混杂。
        ///   登记的是「本次要设成中文」这件事，原文就是 __0 的当前值。
        /// </summary>
        static void PrefixSetText(UILabel __instance, ref string __0)
        {
            try
            {
                if (!textOn || __0 == null) return;
                prefixCalls++;
                string zh = Lookup(__0);
                int id = (__instance != null) ? __instance.GetInstanceID() : 0;
                if (zh == null)
                {
                    // ★ 游戏把标签改成了新的**英文**串（分数、计时器之类每帧都在变）：
                    //   旧记录已过期，删掉。否则关 F4 时会把它还原成更早的英文。
                    //   ★ 只对「像英文的」串这么做 —— 我们自己写进去的中文也会走 prefix
                    //     （ReplaceLabels 里 l.text = zh），那种情况绝不能删记录。
                    if (id != 0 && HasLetter(__0)) swaps.Remove(id);
                    return;
                }
                prefixHits++;
                if (id != 0)
                    swaps[id] = new Swap { label = __instance, en = __0, zh = zh };
                __0 = zh;
            }
            catch (Exception) { }
        }

        // ================================================================ 开关
        void Enable()
        {
            textOn = true;
            Say("");
            Say("======== F4: 文本层 = 开 ========");
            if (dictCount == 0) Say("!! 词表为空 —— " + loadError);
            Sweep(true);
        }

        void Disable()
        {
            textOn = false;
            Say("");
            Say("======== F4: 文本层 = 关 ========");
            RestoreAll();
        }

        // ================================================================ 替换
        void Sweep(bool verbose)
        {
            int nMesh0 = hitMesh, nLabel0 = hitLabel;
            ReplaceLabels(verbose);
            ReplaceMeshes(verbose);
            if (verbose)
                Say("  sweep 结果: label " + (hitLabel - nLabel0) + " 条 / TextMesh " + (hitMesh - nMesh0) + " 条"
                    + "；累计 label " + hitLabel + "/" + swLabel + "  TextMesh " + hitMesh + "/" + swMesh
                    + "  prefix 调用 " + prefixCalls + " 命中 " + prefixHits);
        }

        void ReplaceLabels(bool verbose)
        {
            try
            {
                UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(UILabel));
                for (int i = 0; i < all.Length; i++)
                {
                    UILabel l = all[i] as UILabel;
                    if (l == null) continue;
                    string cur = null;
                    try { cur = l.text; } catch (Exception) { continue; }
                    if (cur == null || cur.Length == 0) continue;
                    if (IsSwapped(l)) continue;
                    string zh = Lookup(cur);
                    swLabel++;
                    if (zh == null) { swaps.Remove(l.GetInstanceID()); NoteMiss(cur); continue; }
                    try
                    {
                        int id = l.GetInstanceID();
                        l.text = zh;
                        hitLabel++;
                        swaps[id] = new Swap { label = l, en = cur, zh = zh };
                        if (verbose) Say("  [label] " + Trunc(cur, 40) + " → " + Trunc(zh, 30));
                    }
                    catch (Exception e) { NoteMiss("设置失败 " + e.Message); }
                }
            }
            catch (Exception e) { Say("!! ReplaceLabels 异常: " + e.Message); }
        }

        void ReplaceMeshes(bool verbose)
        {
            try
            {
                UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(TextMesh));
                for (int i = 0; i < all.Length; i++)
                {
                    TextMesh tm = all[i] as TextMesh;
                    if (tm == null) continue;
                    string cur = null;
                    try { cur = tm.text; } catch (Exception) { continue; }
                    if (cur == null || cur.Length == 0) continue;
                    if (IsSwapped(tm)) continue;
                    string zh = Lookup(cur);
                    swMesh++;
                    if (zh == null) { swaps.Remove(tm.GetInstanceID()); NoteMiss(cur); continue; }
                    try
                    {
                        int id = tm.GetInstanceID();
                        tm.text = zh;
                        hitMesh++;
                        swaps[id] = new Swap { mesh = tm, en = cur, zh = zh };
                        if (verbose) Say("  [TextMesh] " + Trunc(cur, 40) + " → " + Trunc(zh, 30));
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception e) { Say("!! ReplaceMeshes 异常: " + e.Message); }
        }

        static bool IsSwapped(UILabel l)
        {
            Swap s;
            if (!swaps.TryGetValue(l.GetInstanceID(), out s)) return false;
            // ★ ReferenceEquals：InstanceID 会被回收给新对象。
            // ★ 还要比当前显示值：若游戏把标签改成了别的串（分数/计时器每帧变），
            //   旧记录就过期了，必须重新翻译而不是跳过。
            return ReferenceEquals(s.label, l) && s.zh == l.text;
        }

        static bool IsSwapped(TextMesh tm)
        {
            Swap s;
            if (!swaps.TryGetValue(tm.GetInstanceID(), out s)) return false;
            return ReferenceEquals(s.mesh, tm) && s.zh == tm.text;
        }

        static void NoteMiss(string s)
        {
            if (s == null || s.Length == 0) return;
            if (missSeen.Count > 400) return;
            if (missSeen.Add(s) && missSeen.Count <= 400)
            {
                // 只在"看起来像英文 UI 字符串"时记，避免刷屏
                if (HasLetter(s)) Say("  [未命中] " + Trunc(s, 60));
            }
        }

        static bool HasLetter(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return true;
            }
            return false;
        }

        /// <summary>★ 只丢弃**已销毁**的记录，活着的必须留住 —— 切场景不等于所有文本都消失
        /// （常驻 UI、DontDestroyOnLoad 的对象会跨场景存活）。见 swaps 字段的注释。</summary>
        static void PruneSwaps()
        {
            if (swaps.Count == 0) return;
            List<int> dead = null;
            foreach (KeyValuePair<int, Swap> kv in swaps)
            {
                Swap s = kv.Value;
                // Unity 的 == null 对**已销毁**对象也返回 true，正是这里要的语义
                if (s.label == null && s.mesh == null)
                {
                    if (dead == null) dead = new List<int>();
                    dead.Add(kv.Key);
                }
            }
            if (dead != null) for (int i = 0; i < dead.Count; i++) swaps.Remove(dead[i]);
        }

        void RestoreAll()
        {
            int n = 0, dead = 0;
            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, Swap> kv in swaps)
            {
                Swap s = kv.Value;
                try
                {
                    if (s.label != null) { s.label.text = s.en; n++; }
                    else if (s.mesh != null) { s.mesh.text = s.en; n++; }
                    else dead++;
                    remove.Add(kv.Key);
                }
                catch (Exception) { remove.Add(kv.Key); }
            }
            for (int i = 0; i < remove.Count; i++) swaps.Remove(remove[i]);
            Say("  已还原 " + n + " 处文本" + (dead > 0 ? "（" + dead + " 处对象已销毁）" : ""));
        }

        static string Trunc(string s, int n)
        {
            if (s == null) return "(null)";
            string t = s.Replace("\n", "\\n");
            return t.Length <= n ? t : t.Substring(0, n) + "…";
        }

        // ================================================================ Update
        void Update()
        {
            frame++;
            try
            {
                string sc = "";
                try { sc = Application.loadedLevelName; } catch (Exception) { }
                if (sc != sceneName)
                {
                    Say("");
                    Say("=== 场景切换 #" + frame + ": '" + sceneName + "' -> '" + sc + "' ===");
                    sceneName = sc;
                    PruneSwaps();           // ★ 只清已销毁的，不能 swaps.Clear()
                    missSeen.Clear();
                    swMesh = hitMesh = swLabel = hitLabel = 0;
                }

                // ② ③ 周期 sweep：资产里的文本不经 setter，只能靠扫。
                //    每 30 帧一次：切页签/新单位弹出后能在一秒内跟上。
                if (textOn && (frame % 30) == 0) Sweep(false);

                if (Input.GetKeyDown(KeyCode.F4)) { if (textOn) Disable(); else Enable(); }
                if (Input.GetKeyDown(KeyCode.F5)) { Say(""); Say("======== F5: 单次 sweep ========"); Sweep(true); }
                if (Input.GetKeyDown(KeyCode.F9)) { Say(""); Say("======== F9: 重载词表 ========"); LoadDict(); Sweep(true); }
            }
            catch (Exception e) { Say("!! Update 异常: " + e.Message); }
        }

        // ================================================================ HUD
        // ★ 2026-10-19：HUD 合并。字体层（CW3Hanhua.dll）的 OnGUI 会**反射**调用
        //   本方法取一行状态，和它自己的三行一起画在左上角。
        //   本方法要满足三个约束，别改签名：
        //     1) public static string HudLine() —— 反射按 (Static|Public) 找
        //     2) 不抛异常 —— 对方用 try/catch 包着 Invoke，但空串比异常好排查
        //     3) 自己去查跨 DLL 的静态字段（textOn/dictCount…）—— 同进程同程序集，
        //        这些 static 是共享的，取到的就是实时值
        public static string HudLine()
        {
            try
            {
                return "文本层 v" + Ver + "  F4=" + (textOn ? "开" : "关")
                    + "  词表=" + dictCount + "  label命中=" + hitLabel + "  mesh命中=" + hitMesh
                    + "  prefix=" + prefixCalls + "/" + prefixHits + "  拼接=" + hitConcat;
            }
            catch (Exception) { return "文本层: 状态读取异常"; }
        }

        /// <summary>字体层是否在画合并 HUD（它每次 OnGUI 都会反射调 HudLine）。
        /// 在就不要再画一遍，否则右下角也会多出一条。</summary>
        static int lastPeerProbeFrame = -1000;
        static bool peerAlive;

        static bool PeerHudAlive()
        {
            // 每 60 帧探一次：字体层没装载时本插件必须自己把状态画出来
            if (frame - lastPeerProbeFrame >= 60)
            {
                lastPeerProbeFrame = frame;
                peerAlive = false;
                try
                {
                    Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < asms.Length; i++)
                    {
                        Type t = asms[i].GetType("CW3Hanhua.HanhuaPlugin", false);
                        if (t != null) { peerAlive = true; break; }
                    }
                }
                catch (Exception) { }
            }
            return peerAlive;
        }

        void OnGUI()
        {
            try
            {
                if (PeerHudAlive()) return;      // 合并模式下由字体层统一画（NOTES 4.3）
                Color old = GUI.color;
                GUI.color = Color.white;
                GUI.Box(new Rect(8, Screen.height - 30, 720, 22), HudLine());
                GUI.color = old;      // ★ NOTES 5.6：不改回来会污染后续绘制
            }
            catch (Exception) { }
        }
    }
}
