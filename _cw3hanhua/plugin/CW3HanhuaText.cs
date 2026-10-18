// ============================================================================
//  CW3 汉化 · 文本层 v0.8.0 —— 词表 + UILabel/TextMesh 替换
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
    [BepInPlugin("cw3hanhua.text", "CW3 Hanhua Text", "0.8.0")]
    public class TextPlugin : BaseUnityPlugin
    {
        const string Ver = "0.8.0";

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

        // 已替换记录：用于关闭时还原（原文 → 替换后的对象列表）
        class Swap { public UILabel label; public TextMesh mesh; public string en; public string zh; }
        static readonly List<Swap> swaps = new List<Swap>();

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
        /// 因为像 "PACKET SPEED.  " 这种尾部空格是拼接前缀的一部分。</summary>
        static string Lookup(string s)
        {
            if (s == null || s.Length == 0 || dict.Count == 0) return null;
            string zh;
            if (dict.TryGetValue(s, out zh) && zh != null && zh.Length > 0) return zh;
            return null;
        }

        // ================================================================ Patch
        /// <summary>
        /// ★ UILabel.set_text 的 prefix。
        ///
        /// 注意：prefix 里改写 __0 后，原方法会用它做 SetDirty 等，
        /// 所以"改传入值"是最安全的做法（NOTES 5.6 的第一个假象就是 prefix 里改 __result）。
        /// </summary>
        static void PrefixSetText(UILabel __instance, ref string __0)
        {
            try
            {
                if (!textOn || __0 == null) return;
                prefixCalls++;
                string zh = Lookup(__0);
                if (zh == null) return;
                prefixHits++;
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
                    if (zh == null) { NoteMiss(cur); continue; }
                    try
                    {
                        l.text = zh;
                        hitLabel++;
                        swaps.Add(new Swap { label = l, en = cur, zh = zh });
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
                    if (zh == null) { NoteMiss(cur); continue; }
                    try
                    {
                        tm.text = zh;
                        hitMesh++;
                        swaps.Add(new Swap { mesh = tm, en = cur, zh = zh });
                        if (verbose) Say("  [TextMesh] " + Trunc(cur, 40) + " → " + Trunc(zh, 30));
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception e) { Say("!! ReplaceMeshes 异常: " + e.Message); }
        }

        static bool IsSwapped(UILabel l)
        {
            for (int i = 0; i < swaps.Count; i++) if (swaps[i].label != null && ReferenceEquals(swaps[i].label, l)) return true;
            return false;
        }

        static bool IsSwapped(TextMesh tm)
        {
            for (int i = 0; i < swaps.Count; i++) if (swaps[i].mesh != null && ReferenceEquals(swaps[i].mesh, tm)) return true;
            return false;
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

        void RestoreAll()
        {
            int n = 0;
            for (int i = swaps.Count - 1; i >= 0; i--)
            {
                Swap s = swaps[i];
                try
                {
                    if (s.label != null) { s.label.text = s.en; n++; }
                    else if (s.mesh != null) { s.mesh.text = s.en; n++; }
                }
                catch (Exception) { }
            }
            swaps.Clear();
            Say("  已还原 " + n + " 处文本");
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
                    swaps.Clear();          // 换场景后旧引用全死了
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

        void OnGUI()
        {
            try
            {
                Color old = GUI.color;
                GUI.color = Color.white;
                string s = "汉化文本 v" + Ver + "  F4=" + (textOn ? "开" : "关")
                    + "  词表=" + dictCount + "  label命中=" + hitLabel + "  mesh命中=" + hitMesh
                    + "  prefix=" + prefixCalls + "/" + prefixHits;
                GUI.Box(new Rect(8, Screen.height - 30, 720, 22), s);
                GUI.color = old;      // ★ NOTES 5.6：不改回来会污染后续绘制
            }
            catch (Exception) { }
        }
    }
}
