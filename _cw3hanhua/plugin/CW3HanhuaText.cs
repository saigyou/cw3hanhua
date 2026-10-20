// ============================================================================
//  CW3 汉化 · 文本层 v0.9.0 —— 词表 + UILabel.ProcessText prefix 替换
//
//  与字体层（CW3Hanhua.cs）**独立编译、独立运行**：
//    字体层负责"字形从哪来"，本层负责"英文串怎么变中文"。两层可分别开关，
//    任何一层出问题都能立刻判断是谁的锅。
//
//  词表：_cw3hanhua/_dict/zh_cn.dict（tools/build_dict.py 生成）
//    格式见 build_dict.py 头注释：CWH1 + count + [klen,key,zlen,zh]* + uint32 FNV
//
//  本层的替换路径，只剩一条：
//    ① Harmony prefix + postfix on UILabel.ProcessText —— 全部 1029 个 UILabel 的总收口
//
//  ★ v0.9.0：补丁点从 `set_text` 挪到 `ProcessText`。
//    `set_text` 只有 322 个调用点，**资产里序列化的 mText 一个都不走它**
//    （prefab 直接把值写进字段），所以 v0.8.5 删掉 sweep 之后那 622 处就露英文了。
//    而 `ProcessText` 是**每条要显示出来的 UILabel 的必经之路**
//    （它只有 3 个调用方：get_processedText / get_relativeSize / MakePixelPerfect，
//     全在上屏路径上），在它外面套一层，资产文本与动态文本一次全覆盖、零闪烁。
//
//    具体做法（见 PrefixProcessText / PostfixProcessText 的注释）：
//      prefix  把 mText 临时换成中文 → 原方法体照常跑，mProcessedText 与 mSize
//              都按中文算出来（字宽、换行、shrinkToFit 全都对）
//      postfix 立刻把 mText 与 mLastText 写回英文
//    结果：**屏幕上永远是 mProcessedText（中文），而 `label.text` 对外始终是英文**。
//    这一点是刻意的 —— ACTIVATE / RESUPPLY / 单位类型名等几处游戏逻辑在拿
//    `label.text` 做字符串判定，改 mText 会让它们全部失效（NOTES 3.3）。
//
//  ★ v0.8.5：**整套 sweep 逻辑已删除**（v0.8.1~v0.8.4 的 ②③ 两条路）。
//    原 ② 周期 sweep 管资产里序列化的 UILabel.mText，原 ③ 每帧快扫管 TextMesh。
//    两条都在"事后补"，天生会闪一下英文；已改为**直接改资产**（把中文烘进 prefab，
//    见 tools/bake_textmesh_assets.py 与 NOTES 4.6），对象一出现就是中文，没有闪烁。
//    实测最后剩下的"必须走代码路线"的对象，在本文件里逐个打 Harmony prefix（步骤 6）。
//    删掉的东西：labelCache / meshCache / RebuildCache / ReplaceLabels / ReplaceMeshes
//    / Sweep / LateUpdate / IsSwapped / swMesh / hitMesh / swLabel / hitLabel。
//    **保留** missSeen，但用途变了：F5 从"做一次替换"改成"打印未命中清单"，
//    用来找步骤 6 那批"必须走代码路线"的对象。
//
//  v0.8.3：查表增加 LookupConcat —— 游戏有一批文本是运行时 Concat 出来的
//    （主菜单悬浮框 body + "\n[ff5050]To Access:..."），词表里只有两个半截，
//    精确匹配必然落空。见 LookupConcat 注释与 NOTES 3.3「拼接串」。
//
//  按键：
//    F4  文本层 开/关（开=接词表并替换；关=尽力还原本层改过的文本）
//    F9  重新载入词表（改完 zh_cn.dict 不用重启游戏）
//
//  ★ F4 的边界：只能还原**本层经手**的文本（prefix 记下来的）。改资产烘进 prefab 的
//    中文不归本层管，关不掉 —— 要还原得跑 tools/bake_textmesh_assets.py --restore。
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
    [BepInPlugin("cw3hanhua.text", "CW3 Hanhua Text", "0.9.0")]
    public class TextPlugin : BaseUnityPlugin
    {
        const string Ver = "0.9.0";

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

        // 统计
        static int prefixCalls;
        static int prefixHits;
        static int hitConcat;       // 靠 LookupConcat 拼出来的条数（主菜单悬浮框那类）

        // 已替换记录：用于关闭时还原。
        // ★ 按 InstanceID 索引，**不能**用 List + 切场景时 Clear() ——
        //   踩过：Clear() 之后 F4 关闭时 RestoreAll 变成空操作，留下中英混杂。
        //   切场景只该丢弃**已销毁**的对象，仍然活着的必须留住以便还原。
        class Swap { public UILabel label; public string en; public string zh; }
        static readonly Dictionary<int, Swap> swaps = new Dictionary<int, Swap>();

        // 走过 ProcessText 但词表里查不到的串（去重、上限 400）。
        // v0.9.0 起补丁点在 ProcessText，所以这里收到的是**全部 UILabel** 的未命中
        // （含资产序列化文本），不再是 set_text 那 322 个调用点的子集。
        // 用途：F5 打印清单 → 补词表 → F9 重载即可，不用重启游戏。
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
                fldText = AccessTools.Field(typeof(UILabel), "mText");
                fldLastText = AccessTools.Field(typeof(UILabel), "mLastText");
                if (fldText == null || fldLastText == null)
                    Say("!! 找不到 UILabel.mText / mLastText，补丁无法工作");

                var m = AccessTools.Method(typeof(UILabel), "ProcessText", null, null);
                if (m == null) { Say("!! 找不到 UILabel.ProcessText，补丁未安装"); }
                else
                {
                    Harmony h = new Harmony("cw3hanhua.text");
                    // ★★ 不用 Harmony.Patch 的 6 参数重载 —— 它的槽位次序是反直觉的。
                    //    实测 0Harmony.dll 的 IL（Harmony.Patch token=0x060000CD）：
                    //      arg2 → AddPrefix   arg3 → AddPostfix   arg4 → AddTranspiler
                    //      arg5 → AddFinalizer   arg6 → AddILManipulator   ← finalizer 在倒数第二！
                    //    踩过（v0.9.0 首次实测：整个补丁装不上、界面一个字都不变）：
                    //    把 finalizer 放第 6 位 → 进了 ILManipulator 槽位 → Harmony 按
                    //    `void (ILContext, MethodBase, ILLabel)` 三参签名找方法 → 对不上 →
                    //      TargetParameterCountException: parameters do not match signature
                    //    在 HarmonyManipulator.ApplyManipulators 里炸掉。
                    //    ★ 且 Harmony 只往日志写一行 Error、**不抛异常**，
                    //    外层 try/catch 接不到 → 表现为"什么都没发生"。
                    //    ★ 5 参数重载这个版本标了 [Obsolete]（CS0619），也用不了。
                    //    → 改用 PatchProcessor：AddPrefix/AddPostfix/AddFinalizer 名字就是语义，
                    //      再没有"第几个参数是什么"的问题。
                    PatchProcessor proc = h.CreateProcessor(m);
                    proc.AddPrefix(new HarmonyMethod(typeof(TextPlugin).GetMethod("PrefixProcessText",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
                    proc.AddPostfix(new HarmonyMethod(typeof(TextPlugin).GetMethod("PostfixProcessText",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
                    proc.AddFinalizer(new HarmonyMethod(typeof(TextPlugin).GetMethod("FinalizerProcessText",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
                    proc.Patch();
                    Say("★ 已装 patch: UILabel.ProcessText（prefix 换中文 + postfix/finalizer 还原 mText）");
                    Say("  覆盖面: 全部 1029 个 UILabel（含资产序列化的 mText），.text 对外仍是英文");

                    // ★ 装完立刻自检：Harmony 的 Patch() 在有 ILHook 失败时**可能只打一行
                    //   Error 到日志而不抛异常**（实测 v0.9.0：finalizer 错位导致
                    //   `Failed to patch … TargetParameterCountException`，异常被 Harmony
                    //   内部吞掉，我们的 try/catch 什么也没接到，界面一个字不变还看不出原因）。
                    //   所以这里回读 patch 记录，确认 prefix/postfix/finalizer 三个都在。
                    try
                    {
                        Patches pi = Harmony.GetPatchInfo(m);
                        int np = pi == null ? -1 : pi.Prefixes.Count;
                        int nq = pi == null ? -1 : pi.Postfixes.Count;
                        int nf = pi == null ? -1 : pi.Finalizers.Count;
                        int ni = pi == null ? -1 : pi.ILManipulators.Count;
                        Say("  自检: prefix=" + np + " postfix=" + nq + " finalizer=" + nf
                            + " ilmanipulator=" + ni + "（期望 1/1/1/0）");
                        if (np != 1 || nq != 1 || nf != 1 || ni != 0)
                            Say("!! 自检不合格 —— 补丁没装全，界面上不会出现中文，查上面的 Harmony 错误");
                    }
                    catch (Exception e2) { Say("  自检读取失败（不影响补丁本身）: " + e2.Message); }
                }
            }
            catch (Exception e) { Say("!! 装 patch 失败: " + e); }

            Say("按键: F4 文本层开关 | F5 打印未命中清单 | F9 重载词表");
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
                    Say("词表已载入: " + dictCount + " 条");                }
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
        /// <summary>★ v0.9.0 的核心：在 `UILabel.ProcessText` 上打 prefix+postfix，
        /// **临时**用中文骗过 `ProcessText`，让它把 `mProcessedText` / `mSize` /
        /// （shrinkToFit 时的）localScale 全按中文算对，再把 `mText` 还原成英文。
        ///
        /// 为什么不是直接改 mProcessedText（postfix 版）：ProcessText 的 IL 里
        ///   `0204`–`0232` 用 `mProcessedText` 算 `mSize`（`UIFont.CalculatePrintedSize`），
        ///   `0288`–`02D3` 在 `mShrinkToFit` 时用 `mSize.x` 反推 localScale。
        ///   事后改 `mProcessedText` 这两个值就仍按英文算 —— 中文比英文窄，通常看不出，
        ///   但 `mMaxLineWidth` 换行（`UIFont.WrapText`，见 `014B`/`019F`/`01E9`）是按
        ///   **英文** 的宽度断的行，中文串会沿用英文的断点，长句会断在词中间。
        ///
        /// 为什么不改 `mText` 就收工（那样最省事）：`mText` 不是纯显示字段。
        ///   实测有 6 处游戏逻辑把 `label.text` 当数据读（NOTES 4.3「mText 被读取的风险」）：
        ///   `CommandCenter.DeactivateSelectedUnits` 比 `'ACTIVATE'`、
        ///   `DisarmSelectedUnits` 比 `'ARM'`、`StopResupplySelectedUnits` 比 `'RESUPPLY'`、
        ///   `TerraformCommandCenter.HandleUnitInteraction` 拿 label 文字当单位类型名传给
        ///   `CreateUnit`、`CRPLTowerManager.get_popupText`/`WriteXML` 把 label 当存储并写存档。
        ///   留着英文 = 这些判定一律安全。
        ///
        /// ★ 为什么必须连 `mLastText` 一起还原：ProcessText 开头（IL `000E`–`0015`）会把
        ///   `mLastText = mText`，此时 mText 是中文。若只还原 mText，`get_hasChanged`
        ///   （比 `mLastText` vs `get_text()`）永远不相等 → 每帧都重新 WrapText 一遍，
        ///   白白烧 CPU 且让 `mShrinkToFit` 反复缩。所以 postfix 里两个字段一起写回英文。
        ///
        /// 覆盖面的意义：`UILabel.ProcessText` 只有 3 个调用方（`MakePixelPerfect`、
        ///   `get_processedText`、`get_relativeSize`），**全在上屏路径上**，
        ///   所以任何一个画得出来的 UILabel 都必然经过这里 —— 包括资产里序列化的
        ///   `mText`（它们从不经过 `set_text`，这正是旧 sweep 存在的原因）。
        ///   旧 `set_text` prefix 只能覆盖 322 个动态调用点，对 prefab 里的静态串无能为力。
        /// </summary>
        static FieldInfo fldText, fldLastText;

        // prefix→postfix 之间传递原文。
        // ★ 必须**按 label 实例**分开存，不能用一个全局静态槽位：
        //   ProcessText 本身不递归（只调 UIFont.WrapText / CalculatePrintedSize），
        //   但 `UIFont.WrapText` 内部会不会间接触发另一个 label 的 ProcessText，
        //   是我们控制不了的（例如它读 relativeSize，而 relativeSize 会调 ProcessText）。
        //   用实例 ID 做键，嵌套时各管各的；同一实例连续两次调用则用列表当栈。
        static readonly Dictionary<int, List<string>> pendEn = new Dictionary<int, List<string>>();

        static void PrefixProcessText(UILabel __instance)
        {
            try
            {
                if (!textOn || __instance == null || fldText == null) return;
                string en = (string)fldText.GetValue(__instance);
                if (en == null || en.Length == 0) return;

                prefixCalls++;
                string zh = Lookup(en);
                int id = __instance.GetInstanceID();
                if (zh == null)
                {
                    // 已经是中文（我们自己写进去的）或纯数字/计时器 → 不留记录
                    if (id != 0 && HasLetter(en)) swaps.Remove(id);
                    NoteMiss(en);
                    return;
                }
                prefixHits++;
                if (id != 0) swaps[id] = new Swap { label = __instance, en = en, zh = zh };

                if (id == 0) return;                   // 拿不到 ID 就不改，免得还原不回来
                List<string> st;
                if (!pendEn.TryGetValue(id, out st)) { st = new List<string>(1); pendEn[id] = st; }
                st.Add(en);
                fldText.SetValue(__instance, zh);      // 只在 ProcessText 执行期间有效
            }
            catch (Exception) { }
        }

        /// <summary>兜底：`ProcessText` 万一抛异常，postfix 不会执行，mText 就会永远卡在
        /// 中文（这台 label 之后 `get_text()` 都返回中文 → CommandCenter 那三处状态判定、
        /// Terraform 的单位类型名全坏）。Harmony 的 finalizer 无论正常还是异常返回都会跑，
        /// 在这里做同样的还原。正常路径下 postfix 已经把栈弹空了，这里就是个空操作。</summary>
        static Exception FinalizerProcessText(UILabel __instance, Exception __exception)
        {
            try { RestorePending(__instance); }
            catch (Exception) { }
            return __exception;      // 原样放回，不吞异常
        }

        static void PostfixProcessText(UILabel __instance)
        {
            try { RestorePending(__instance); }
            catch (Exception) { }
        }

        static void RestorePending(UILabel __instance)
        {
            if (__instance == null || fldText == null) return;
            int id = __instance.GetInstanceID();
            List<string> st;
            if (!pendEn.TryGetValue(id, out st) || st.Count == 0) return;   // prefix 没改过
            string en = st[st.Count - 1];
            st.RemoveAt(st.Count - 1);
            if (st.Count == 0) pendEn.Remove(id);

            // 把 mText 写回英文，并同步 mLastText —— 两个都必须写：
            //   ProcessText 开头（IL 000E–0015）已经 `mLastText = mText`（此时是中文），
            //   若只写回 mText，`get_hasChanged`（比 mLastText vs get_text()）永远不等
            //   → 每帧重新 WrapText 一遍，白白烧 CPU，mShrinkToFit 还会反复缩。
            fldText.SetValue(__instance, en);
            if (fldLastText != null) fldLastText.SetValue(__instance, en);
        }

        // ================================================================ 开关
        void Enable()
        {
            textOn = true;
            Say("");
            Say("======== F4: 文本层 = 开 ========");
            if (dictCount == 0) Say("!! 词表为空 —— " + loadError);
            // ★ v0.9.0：打开时把屏上已有的 label 催一遍。ProcessText 补丁只在 label
            //   「变脏」时才被调用，已经画出来的静态文本不会自己重算，所以要主动
            //   MarkAsChanged。这是**手动按键时的一次性全扫**，不是每帧轮询 ——
            //   开销可接受，且只在 F4 按下的那一帧发生。
            RefreshAllLabels();
        }

        void Disable()
        {
            textOn = false;
            Say("");
            Say("======== F4: 文本层 = 关 ========");
            RestoreAll();
        }

        // ================================================================ 未命中记录
        // 作用：玩游戏时把「经过 UILabel 但词表里没有」的英文串记下来，F5 打印。
        // v0.9.0 起覆盖全部 UILabel（含资产序列化文本），所以这个清单就是
        // 「还差哪些译文」的完整答案 → 补词表后 F9 重载即可，不用重启游戏。
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
                if (s.label == null)
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
                    // ★ v0.9.0：mText 一直是英文（中文只在 ProcessText 内部临时存在），
                    //   所以这里**不需要写回英文**，只要逼它重算一次 mProcessedText。
                    //   踩过：写 s.label.text = s.en 是无效的 —— set_text 里
                    //   `if (mText != arg)` 不成立就什么都不做，mProcessedText 还是中文。
                    if (s.label != null) { ForceRefresh(s.label); n++; }
                    else dead++;
                    remove.Add(kv.Key);
                }
                catch (Exception) { remove.Add(kv.Key); }
            }
            for (int i = 0; i < remove.Count; i++) swaps.Remove(remove[i]);
            Say("  已还原 " + n + " 处文本" + (dead > 0 ? "（" + dead + " 处对象已销毁）" : ""));
        }

        /// <summary>逼一个 UILabel 重新跑 ProcessText（不依赖 set_text 的 `mText != arg` 判断）。
        /// `UILabel.set_hasChanged` 是私有的，只能反射调；`MarkAsChanged()` 是 public 的
        /// 等价物（IL：set_hasChanged(true) + UIWidget.MarkAsChanged），优先用它。</summary>
        static void ForceRefresh(UILabel l)
        {
            try { l.MarkAsChanged(); }
            catch (Exception)
            {
                try
                {
                    if (fldHasChanged == null)
                        fldHasChanged = AccessTools.Method(typeof(UILabel), "set_hasChanged", null, null);
                    if (fldHasChanged != null) fldHasChanged.Invoke(l, new object[] { true });
                }
                catch (Exception) { }
            }
        }
        static MethodInfo fldHasChanged;

        /// <summary>把屏上所有 UILabel 催一遍 ProcessText。
        /// 只在**手动按键**（F4 开 / F9 重载词表）时调用，不是每帧轮询 ——
        /// 日常显示完全靠 ProcessText 补丁被动生效，不需要这里出力。
        ///
        /// 为什么要催：补丁挂在 ProcessText 上，而 ProcessText 只在 label 变脏时被调
        /// （`get_processedText` 先查 `hasChanged`）。已经画出来的静态文本不变脏，
        /// 就永远不会再走一遍 —— 按 F4 打开后它会一直保持英文，直到那个界面重进。
        /// `MarkAsChanged()` 把 mShouldBeProcessed 置真，下一次 Fill 就会重算。</summary>
        static void RefreshAllLabels()
        {
            int n = 0;
            try
            {
                UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(UILabel));
                for (int i = 0; i < all.Length; i++)
                {
                    UILabel l = all[i] as UILabel;
                    if (l == null) continue;      // 已销毁的会在 as 后为 null
                    ForceRefresh(l);
                    n++;
                }
            }
            catch (Exception e) { Say("!! 刷新全部 label 失败: " + e.Message); }
            Say("  已催促 " + n + " 个 UILabel 重算（下次 Fill 生效）");
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
                }

                if (Input.GetKeyDown(KeyCode.F4)) { if (textOn) Disable(); else Enable(); }
                if (Input.GetKeyDown(KeyCode.F5)) DumpMisses();
                if (Input.GetKeyDown(KeyCode.F9))
                {
                    Say(""); Say("======== F9: 重载词表 ========"); LoadDict();
                    // 词表变了，屏上已有的 label 不会自己重算 —— 催一遍（同 F4 的理由）
                    if (textOn) RefreshAllLabels();
                }
            }
            catch (Exception e) { Say("!! Update 异常: " + e.Message); }
        }

        /// <summary>F5：把本轮「经过 UILabel 但词表里查不到」的英文串全打出来。
        /// v0.9.0 起补丁点在 ProcessText，覆盖全部 1029 个 UILabel（含资产序列化文本），
        /// 所以这里列出的就是**还缺译文的完整清单**：补进词表后按 F9 重载即时生效。
        /// 反过来说，界面上还是英文、这里又没列出的，才是根本不经 UILabel 的对象
        /// （TextMesh：text 是 extern icall 钩不上，只能改资产，见 NOTES 4.6）。</summary>
        void DumpMisses()
        {
            Say("");
            Say("======== F5: 未命中清单（prefix 走过但无译文）========");
            Say("  累计 prefix 调用 " + prefixCalls + "  命中 " + prefixHits
                + "  拼接命中 " + hitConcat + "  未命中 " + missSeen.Count + " 条");
            int i = 0;
            foreach (string s in missSeen)
            {
                i++;
                Say("  " + i + ". " + Trunc(s, 90));
            }
            if (missSeen.Count == 0) Say("  （空 —— 本场景所有经过 ProcessText 的串都命中词表了）");
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
                    + "  词表=" + dictCount
                    + "  prefix=" + prefixCalls + "/" + prefixHits + "  拼接=" + hitConcat
                    + "  未命中=" + missSeen.Count;
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
