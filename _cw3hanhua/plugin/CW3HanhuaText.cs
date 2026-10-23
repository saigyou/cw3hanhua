// ============================================================================
//  CW3 汉化 · 文本层 v1.1.0 —— 词表 + UILabel.ProcessText + 5 个 TextMesh 写入点
//
//  字体层负责"字形从哪来"，本层负责"英文串怎么变中文"。
//
//  ★ v1.1.0（2026-10-23）：查表加**第四级**「分段重组」LookupSegment。
//    精确 → '\n' 拼接 → 归一化 → 分段重组。前三级都没动。
//    起因：护盾提示 "Planet is shielded. ...:\n" + "[ff7f7f]Tempus" 这类
//    「碎片常量 + 运行时值」的串，前三级一律落空；而往词表里穷举整串
//    （41 条 × 颜色组合）不可扩展。分段器把碎片逐段翻掉、色标与变量原样透传。
//    设计要点与边界规则见 LookupSegment 的注释；幂等性已用全量词表验证。
//
//  ★ v1.0.0（2026-10-23）：两个 DLL 合并成一个 —— 本文件与 CW3Hanhua.cs 一起
//    编进 **CW3Hanhua.dll**，本类不再是 BepInPlugin / MonoBehaviour，而是被
//    HanhuaPlugin 直接驱动的普通类（Init / Tick / SetOn）。两层之间零反射。
//    仍然一文件一层、各自一个类，只是打在一个包里。
//
//    ★ 开关已合并 —— 本层没有自己的按键，跟着 F3 汉化总开关一起开/关
//      （HanhuaPlugin.SetHanhua 直接调本类的 SetOn(bool)）。
//
//  词表：_cw3hanhua/_dict/zh_cn.dict（tools/build_dict.py 生成）
//    格式见 build_dict.py 头注释：CWH1 + count + [klen,key,zlen,zh]* + uint32 FNV
//
//  本层的替换路径：
//    ① Harmony prefix + postfix on UILabel.ProcessText —— 全部 1029 个 UILabel 的总收口
//    ② 五个 TextMesh 写入点（v0.10.0，步骤 6）—— 资产改不到的那几条
//         PreludeManager / EpilogueManager / DemoScreenManager 的 ShowText：prefix 改 __0
//         StarManager.set_starName / PlanetManager.set_planetName：postfix 只改 TextMesh
//       为什么后者不能改入参：见 PatchTextMeshWriters 里的注释（会毁存档路径）
//
//  ★ v0.10.0：加 ②。改资产能覆盖 19 条静态 TextMesh，但有一批文本是运行时才写的
//    （星区名/星球名、三段过场的台词），资产里只是占位值，烘中文也留不住。
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
//    （无）本层（文本层）没有自己的开关按键了。
//    ★ 2026-10-23：F4 已退役 —— 文本层并入**汉化总开关 F3**，与字体层同开同关。
//      F3 在 CW3Hanhua.cs 的 HanhuaPlugin 里，那边直接调本类的 SetOn(bool)。
//    F5  打印未命中清单（缺哪些译文）
//    F9  重新载入词表（改完 zh_cn.dict 不用重启游戏）
//    这三个键都由 HanhuaPlugin.Update 驱动（F5/F9 走本文件的 Tick()）。
//
//  ★ 本层的边界：只能还原**本层经手**的文本（prefix 记下来的）。改资产烘进 prefab 的
//    中文不归本层管，关不掉 —— 要还原得跑 tools/bake_textmesh_assets.py --restore。
//
//  编译（见 NOTES 5.2）：与 CW3Hanhua.cs 一起编进同一个 CW3Hanhua.dll
//    csc /target:library /platform:x86 /codepage:65001
//        /reference:BepInEx.dll /reference:0Harmony.dll
//        /reference:UnityEngine.dll /reference:Assembly-CSharp.dll
//        /out:...\CW3Hanhua.dll  CW3Hanhua.cs CW3HanhuaText.cs
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
    /// <summary>文本层：词表 + UILabel.ProcessText 补丁 + TextMesh 写入点补丁。
    /// ★ v1.0.0 起不是插件、不是 MonoBehaviour —— 由 HanhuaPlugin.Init/Tick/SetOn 驱动，
    ///   与字体层同在一个程序集，不再有任何反射互调。</summary>
    public class TextPlugin
    {
        public const string Ver = "1.1.0";

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        // ---------------------------------------------------------------- 词表
        static Dictionary<string, string> dict = new Dictionary<string, string>();
        // v0.10.2：归一化索引（尾部空白/CRLF/连续空格一律抹平），只作**兜底**用。
        // 起因：剧情台词 579 条在游戏里全部查不到 —— XML 原文与运行时串差在不可见字符上
        // （尾部换行、双空格等），精确匹配天然落空。见 LookupNormalized。
        static Dictionary<string, string> dictNorm = new Dictionary<string, string>();
        static int hitNorm;         // 靠归一化兜底命中的条数
        static string dictPath;
        static string loadError = "";
        static int dictCount;

        // ---------------------------------------------------------------- 状态
        static bool textOn = false;
        // frame 已删：v1.0.0 起场景切换由 HanhuaPlugin.Update 统一检测，
        // 本层不再需要自己的帧计数（原先只用于「场景切换 #N」那行日志）。

        // 统计
        static int prefixCalls;
        static int prefixHits;
        static int hitConcat;       // 靠 LookupConcat 拼出来的条数（主菜单悬浮框那类）

        // v1.1.0：分段重组用的「首字符 → key 列表」索引（列表按长度降序，保证最长匹配优先）。
        // 起因：游戏里大量文本是「常量前缀/碎片 + 运行时值」拼出来的，词表里只有碎片。
        //   典型：护盾提示 "Planet is shielded. ...:\n" + "[ff7f7f]Tempus"
        //        资源条   "  [ffffff]Energy:[-][ff0000] " + "55/100"
        //   精确匹配和 \n 拼接都够不着（碎片不是整串、切点也不在 \n 上）。
        static Dictionary<char, List<string>> byFirst = new Dictionary<char, List<string>>();
        static int hitSeg;          // 靠 LookupSegment 拼出来的条数

        // 已替换记录：用于关闭时还原。
        // ★ 按 InstanceID 索引，**不能**用 List + 切场景时 Clear() ——
        //   踩过：Clear() 之后关总开关时 RestoreAll 变成空操作，留下中英混杂。
        //   切场景只该丢弃**已销毁**的对象，仍然活着的必须留住以便还原。
        class Swap { public UILabel label; public string en; public string zh; }
        static readonly Dictionary<int, Swap> swaps = new Dictionary<int, Swap>();

        // ---------------------------------------------------------------- TextMesh（v0.10.0，步骤 6）
        // TextMesh 的 `set_text` 是 extern icall（UnityEngine.dll 里 RVA=0x0，没有 IL 体），
        // Harmony 打不上 —— 这就是为什么这批对象只剩「改资产」和「改上游 C# 方法」两条路。
        // 改资产已经做完（19 条烘进 prefab，见 tools/bake_textmesh_assets.py）；
        // 这里处理**改资产解决不了**的那几个：它们在资产里是占位值，运行时被代码重写。
        //   写了中文资产也留不住（一读档就被英文盖掉），只能在写入点动手。
        class MeshSwap { public TextMesh mesh; public string en; }
        static readonly Dictionary<int, MeshSwap> meshSwaps = new Dictionary<int, MeshSwap>();

        // 统计（和 UILabel 的分开，HUD 里分别看，判断是哪条路在生效）
        static int meshCalls;
        static int meshHits;

        // 走过 ProcessText 但词表里查不到的串（去重、上限 400）。
        // v0.9.0 起补丁点在 ProcessText，所以这里收到的是**全部 UILabel** 的未命中
        // （含资产序列化文本），不再是 set_text 那 322 个调用点的子集。
        // 用途：F5 打印清单 → 补词表 → F9 重载即可，不用重启游戏。
        static readonly HashSet<string> missSeen = new HashSet<string>();

        // ---------------------------------------------------------------- 日志
        // ★ v1.0.0：直接转发到 HanhuaPlugin.Say —— 同一程序集，日志与 F11 报告合并成一份。
        static void Say(string s)
        {
            HanhuaPlugin.Say(s);
        }

        // ================================================================ 生命周期
        // ★ v1.0.0：不再是 MonoBehaviour，由 HanhuaPlugin.Awake 调用一次。
        //   顺序由字体层保证（它先装完自己的东西再 Init 本层）。
        public static void Init()
        {
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

            PatchTextMeshWriters();

            Say("按键: （本层无开关，跟着 F3 汉化总开关走）| F5 打印未命中清单 | F9 重载词表");
        }

        // ================================================================ TextMesh 写入点（正文路径）
        /// <summary>★ v0.10.0（六步计划的步骤 6）：给「必须走代码路线」的 TextMesh 写入点打补丁。
        ///
        /// 为什么这批不能靠改资产：它们在资产里只是**占位值**，运行时会被代码覆盖。
        ///   `Star/StarTitle` 与 `Wormhole/StarTitle` 里存的是 `'Andro'`，
        ///   一进游戏 `StarManager.set_starName` 就把它换成真正的星名；
        ///   三段过场叙事（Prelude / Epilogue / DemoScreen）同理，
        ///   资产里是模板，每句话都是 `ShowText` 现场造对象写进去的。
        ///   → 中文烘进资产也留不住，只能在写入的瞬间换。
        ///
        /// 为什么改上游方法而不是 TextMesh.set_text：后者是 extern icall
        ///   （UnityEngine.dll 里 RVA=0x0，没有 IL 体），Harmony 根本打不上。
        ///   全游戏只有 8 个 C# 方法写过 TextMesh、共 15 个调用点，逐个来。
        ///
        /// ★ 参数改法分两种，不能一刀切：
        ///   `ShowText` 的 arg1 是**纯文案** → prefix 改入参 `__0` 最干净，
        ///     原方法体照常用它 set_text，连描边/淡入都不受影响。
        ///   `set_starName` / `set_planetName` 的 arg1 是**数据字段**（先 stfld _starName
        ///     再写 TextMesh）→ 绝不能改入参：`get_starName` 有 10 个调用方，
        ///     `SystemManager.LaunchMission` 拿它拼存档文件名
        ///     （`'Alpha Sector : ' + get_starName() + ' : ' + … + '.cw3'`），
        ///     改成中文会让存档路径变成中文。所以这两个走 postfix，只改 TextMesh。</summary>
        static void PatchTextMeshWriters()
        {
            // ---- ① 三段过场叙事：prefix 改文案入参 ----
            // 调用方：PreludeManager.FixedUpdate ×31、EpilogueManager.FixedUpdate ×21、
            //         DemoScreenManager.FixedUpdate ×10（各自唯一的调用方）
            PrefixShowText(typeof(PreludeManager), "PreludeManager.ShowText");
            PrefixShowText(typeof(EpilogueManager), "EpilogueManager.ShowText");
            PrefixShowText(typeof(DemoScreenManager), "DemoScreenManager.ShowText");

            // ---- ② 星名 / 星球名：postfix 只改 TextMesh，不碰数据字段 ----
            PatchNameSetter(typeof(StarManager), "set_starName", "starTitle",
                "StarManager.set_starName", "PostfixStarName");
            PatchNameSetter(typeof(PlanetManager), "set_planetName", "planetTitle",
                "PlanetManager.set_planetName", "PostfixPlanetName");

            // 说明：以下三条**故意不打**，理由都写在这里，免得后来者以为是漏了
            //   StarManager.Refresh —— 只写 starInfo（"3/12" 这种进度串），没词可翻
            //   CRPLTowerManager.set_text —— 玩家 CRPL 脚本产出的文字，翻了会毁脚本
            //   TextLabel.set_text —— ilx callers 命中 0 处（prefab/UnityEvent 驱动），
            //     资产里那 5 个 `'4'` 是 prefab 模板占位，不在任何场景里显示
        }

        static void PrefixShowText(Type t, string label)
        {
            try
            {
                MethodInfo m = AccessTools.Method(t, "ShowText", null, null);
                if (m == null) { Say("  （" + label + " 没找到，跳过）"); return; }
                Harmony h = new Harmony("cw3hanhua.text.tm." + m.Name + "." + t.Name);
                PatchProcessor p = h.CreateProcessor(m);
                p.AddPrefix(new HarmonyMethod(typeof(TextPlugin).GetMethod("PrefixAnyShowText",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
                p.Patch();
                Patches pi = Harmony.GetPatchInfo(m);
                int n = pi == null ? -1 : pi.Prefixes.Count;
                Say("★ TextMesh patch: " + label + "  prefix=" + n + (n == 1 ? "" : "  !! 没装上"));
            }
            catch (Exception e) { Say("!! 装 " + label + " 补丁失败: " + e.Message); }
        }

        /// <summary>三个 ShowText 共用：它们的第 1 个参数都是要显示的那句话。
        /// 用 `ref string __0` 而不是具体签名的原因：三个方法的参数个数不同
        /// （7 / 8 / 9），只声明关心的那个参数，Harmony 按名匹配，其余不碰。</summary>
        static void PrefixAnyShowText(ref string __0)
        {
            if (!textOn || __0 == null) return;
            try
            {
                meshCalls++;
                string zh = Lookup(__0);
                if (zh == null || zh == __0) { NoteMiss(__0); return; }
                meshHits++;
                __0 = zh;
            }
            catch (Exception) { }
        }

        static void PatchNameSetter(Type t, string method, string meshField, string label, string postfixName)
        {
            try
            {
                MethodInfo m = AccessTools.Method(t, method, null, null);
                FieldInfo f = AccessTools.Field(t, meshField);
                if (m == null || f == null) { Say("  （" + label + " / " + meshField + " 没找到，跳过）"); return; }
                if (f.FieldType != typeof(TextMesh))
                {
                    Say("  （" + label + " 的 " + meshField + " 不是 TextMesh（" + f.FieldType.Name + "），跳过）");
                    return;
                }
                if (t == typeof(StarManager)) fldStarTitle = f;
                else if (t == typeof(PlanetManager)) fldPlanetTitle = f;

                Harmony h = new Harmony("cw3hanhua.text.tm." + method);
                PatchProcessor p = h.CreateProcessor(m);
                // 两个 setter 各用一个具名 postfix：不用 `__originalMethod` 分派，
                // 少一层间接、日志里一眼能看出是哪个方法触发的。
                p.AddPostfix(new HarmonyMethod(typeof(TextPlugin).GetMethod(postfixName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
                p.Patch();
                Patches pi = Harmony.GetPatchInfo(m);
                int n = pi == null ? -1 : pi.Postfixes.Count;
                Say("★ TextMesh patch: " + label + "  postfix=" + n + "（只改 " + meshField + "）"
                    + (n == 1 ? "" : "  !! 没装上"));
            }
            catch (Exception e) { Say("!! 装 " + label + " 补丁失败: " + e.Message); }
        }

        static FieldInfo fldStarTitle, fldPlanetTitle;

        /// <summary>星名 setter 跑完之后，只把 starTitle 那个 TextMesh 换成中文。
        /// 数据字段 `_starName` 保持英文 —— 存档文件名、XML、`get_starName` 的 10 个
        /// 调用方全指望它是英文。</summary>
        static void PostfixStarName(object __instance)
        {
            if (!textOn || __instance == null) return;
            try
            {
                TextMesh tm = fldStarTitle == null ? null : fldStarTitle.GetValue(__instance) as TextMesh;
                if (tm != null) TranslateMesh(tm);
            }
            catch (Exception) { }
        }

        /// <summary>同 PostfixStarName，对象是 `_planetName` / `planetTitle`。</summary>
        static void PostfixPlanetName(object __instance)
        {
            if (!textOn || __instance == null) return;
            try
            {
                TextMesh tm = fldPlanetTitle == null ? null : fldPlanetTitle.GetValue(__instance) as TextMesh;
                if (tm != null) TranslateMesh(tm);
            }
            catch (Exception) { }
        }

        /// <summary>把一个 TextMesh 的当前文本换成中文（就地查表）。
        /// 记录到 meshSwaps 仅为了关总开关时能还原 —— TextMesh 没有 `mText/mProcessedText` 之分，
        /// 改了就是改了，没有「临时」这回事。</summary>
        static void TranslateMesh(TextMesh tm)
        {
            string en = tm.text;
            if (en == null || en.Length == 0) return;
            string zh = Lookup(en);
            if (zh == null || zh == en) { NoteMiss(en); return; }
            int id = tm.GetInstanceID();
            if (id != 0 && !meshSwaps.ContainsKey(id)) meshSwaps[id] = new MeshSwap { mesh = tm, en = en };
            tm.text = zh;
        }

        // ================================================================ 词表读取
        static void LoadDict()
        {
            dict.Clear();
            dictCount = 0;
            loadError = "";
            dictNorm.Clear();
            byFirst.Clear();
            hitSeg = 0;
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
                        // v0.10.2：同时建一张「归一化 → 译文」表，兜住运行时串与 XML 原文的
                        // 不可见差异（尾部换行/空格、双空格折叠、CRLF）。见 LookupNormalized。
                        string nk = Normalize(k);
                        if (nk.Length > 0 && !dictNorm.ContainsKey(nk)) dictNorm[nk] = z;
                    }
                    uint expect = br.ReadUInt32();
                    uint got = Fnv1a(body.GetBuffer(), (int)body.Length);
                    if (expect != got)
                        Say("!! 词表校验失败（期望 " + expect + " 实际 " + got + "），仍然使用但请重新生成");
                    dictCount = dict.Count;
                    BuildSegmentIndex();
                    Say("词表已载入: " + dictCount + " 条  分段索引: " + byFirst.Count + " 个首字符");
                }
            }
            catch (Exception e)
            {
                loadError = "词表读取异常: " + e.Message;
                Say("!! " + loadError);
            }
        }

        /// <summary>建「首字符 → key 列表」索引，供 LookupSegment 用。
        /// 列表按长度降序排好，扫描时第一个 startswith 成功的就是最长匹配。
        /// ★ 只收「首字符是字母 / 空格 / '['」的 key：分段器只在这三种地方起头查表
        ///   （'[' 是因为资源条有一批 key 自带 [色标] 前缀），
        ///   其余 key（数字、符号开头）精确匹配那条路已经覆盖。</summary>
        static void BuildSegmentIndex()
        {
            byFirst.Clear();
            foreach (string k in dict.Keys)
            {
                if (k.Length == 0) continue;
                char c = k[0];
                if (!IsLetter(c) && c != ' ' && c != '[') continue;
                List<string> list;
                if (!byFirst.TryGetValue(c, out list))
                {
                    list = new List<string>();
                    byFirst[c] = list;
                }
                list.Add(k);
            }
            foreach (List<string> list in byFirst.Values)
                list.Sort(CompareByLenDesc);
        }

        static int CompareByLenDesc(string a, string b)
        {
            int r = b.Length.CompareTo(a.Length);
            if (r != 0) return r;
            return string.CompareOrdinal(a, b);
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
            zh = LookupConcat(s, 0);
            if (zh != null) return zh;
            zh = LookupNormalized(s);
            if (zh != null) return zh;
            // ★ 已经是中文的串不要再翻：TextMesh 那条路会把自己写进去的中文再喂回来，
            //   后三级匹配（尤其分段）会把「space」这类保留原文的词再吃掉一次。
            //   英文原文不可能含汉字，所以这个闸门对我们自己的译文是精确的。
            if (HasCjk(s)) return null;
            // v1.1.0：最后一级 —— 分段重组（碎片 + 运行时值）。见 LookupSegment。
            zh = LookupSegment(s);
            if (zh != null)
            {
                hitSeg++;
                return zh;
            }
            return null;
        }

        /// <summary>★ v0.10.2 兜底：把「不可见差异」抹平后再查一次。
        /// 起因：剧情台词（resources.assets 里 TextAsset 的 XML）线上全部查不到 ——
        /// 运行时 `MessageDialogManager.set_currentMessage` 拿到的串与 XML 原文差在
        /// 尾部换行 / 双空格 / CRLF 这类看不见的东西上，精确匹配必然落空。
        ///
        /// 抹平规则（两端一致，所以不会误配到别的条目）：
        ///   "\r\n" → "\n"、连续普通空格折叠成一个、去掉尾部空白（含换行）。
        /// 只在精确匹配和 \n 拼接都失败之后才走，所以对既有的 1000+ 条译文零影响。</summary>
        static string LookupNormalized(string s)
        {
            if (dictNorm.Count == 0) return null;
            string nk = Normalize(s);
            if (nk.Length == 0) return null;
            string zh;
            if (dictNorm.TryGetValue(nk, out zh) && zh != null && zh.Length > 0)
            {
                hitNorm++;
                return zh;
            }
            return null;
        }

        /// <summary>归一化：CRLF→LF、连续空格折叠、去尾部空白。
        /// 注意**不动**行首空白（那是排版缩进）也不动中间换行结构。</summary>
        static string Normalize(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\r') { if (i + 1 < s.Length && s[i + 1] == '\n') continue; sb.Append('\n'); continue; }
                if (c == ' ')
                {
                    int j = i;
                    while (j + 1 < s.Length && s[j + 1] == ' ') j++;
                    sb.Append(' ');
                    i = j;
                    continue;
                }
                sb.Append(c);
            }
            int end = sb.Length;
            while (end > 0 && (sb[end - 1] == ' ' || sb[end - 1] == '\n' || sb[end - 1] == '\t')) end--;
            sb.Length = end;
            return sb.ToString();
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

        // ================================================================ 分段重组（v1.1.0）
        /// <summary>把整串拆成「词表碎片 + 原样透传的变量」再逐段翻译拼回去。
        ///
        /// ★ 为什么需要它：游戏大量 UI 文本是「碎片常量 + 运行时值」拼出来的，词表里只有碎片：
        ///     护盾提示  "Planet is shielded. ...:\n" + "[ff7f7f]Tempus"
        ///     资源条    "  [ffffff]Energy:[-][ff0000] " + "55/100"
        ///     计数      "Amt: " + n 、"Collected: " + techName
        ///   精确匹配只能中整串，LookupConcat 只能在 '\n' 处切两段，都够不着。
        ///
        /// 扫描规则（逐字符）：
        ///   1. [rrggbb] / [-] / [tag] 这类标记整段透传 —— 颜色不是语言，不参与匹配；
        ///   2. 非字母字符原样透传（数字、标点、空格、换行都要保住）；
        ///   3. 遇到字母（或它前面的空格）就在 byFirst 里试最长前缀 key；
        ///   4. **左边界**：起头位置前一字符是字母 → 放弃（不允许从词中间命中，否则
        ///      "Windows" 会被 "Window" 吃成「窗口s」）；
        ///   5. **右边界**：key 自身以字母结尾时才检查后一字符（"Collected: " 这种
        ///      自带空格的 key 天然有边界，不该再被卡）；
        ///   6. **全有或全无**：只要有字母消费不掉就整串放弃，宁留英文也不出中英混杂；
        ///   7. 至少命中 1 段才返回。
        ///
        /// 结果幂等：把词表里已有的译文再喂回来跑一遍，不会被二次改写
        /// （否则 "载入存档位「Auto」？" 会变成「自动」）。</summary>
        static string LookupSegment(string s)
        {
            if (byFirst.Count == 0 || s == null || s.Length == 0) return null;
            StringBuilder sb = new StringBuilder(s.Length + 8);
            int i = 0, matched = 0, L = s.Length;
            try
            {
                while (i < L)
                {
                    char c = s[i];
                    // 先试词表 key：有些 key 自己就带 [色标] 前缀（资源条的 Pad 那条），
                    // 若先当标记透传就永远匹配不上。
                    string hit = (IsLetter(c) || c == ' ' || c == '[') ? MatchKey(s, i) : null;
                    if (hit != null)
                    {
                        sb.Append(dict[hit]);
                        i += hit.Length;
                        matched++;
                        continue;
                    }
                    int tagLen = TagLength(s, i);
                    if (tagLen > 0)
                    {
                        sb.Append(s, i, tagLen);
                        i += tagLen;
                        continue;
                    }
                    if (IsLetter(c)) return null;       // 有字母消费不掉 → 全有或全无
                    sb.Append(c);
                    i++;
                }
            }
            catch (Exception) { return null; }
            if (matched == 0) return null;
            return sb.ToString();
        }

        /// <summary>在 s[i] 处找一个能用的词表 key（最长的、且边界合法的）；没有返回 null。
        /// 左边界必须在这里判，不能放到调用处 —— 标记透传那些分支不需要这个约束。</summary>
        static string MatchKey(string s, int i)
        {
            if (i > 0 && IsLetter(s[i - 1])) return null;   // 不许从词中段起头
            List<string> cand;
            if (!byFirst.TryGetValue(s[i], out cand)) return null;
            int L = s.Length;
            for (int k = 0; k < cand.Count; k++)
            {
                string key = cand[k];
                if (!StartsWith(s, i, key)) continue;
                int j = i + key.Length;
                // 右边界：key 以字母结尾时，后面紧接字母**或数字**都算词内
                // （"Link327" 是玩家名、"Custom1_256" 是纹理内部名，都不该拆）
                if (IsLetter(key[key.Length - 1]) && j < L && IsWordChar(s[j])) continue;
                return key;
            }
            return null;
        }

        /// <summary>算不算「词的一部分」：字母或数字。数字也要算 —— 标识符大量是
        /// 字母前缀 + 编号（Link327、Custom1_256、Mesh0），把数字当边界会把它们拆坏。</summary>
        static bool IsWordChar(char c)
        {
            return IsLetter(c) || (c >= '0' && c <= '9');
        }

        /// <summary>若 s[i] 起是一个颜色/标记段，返回它的长度（含方括号），否则 0。
        /// 认三种：[rrggbb]（6 位十六进制）、[-]（恢复默认色）、[tag]（纯字母标签，≤20 字）。</summary>
        static int TagLength(string s, int i)
        {
            if (i >= s.Length || s[i] != '[') return 0;
            int close = -1;
            int limit = i + 24;
            if (limit > s.Length) limit = s.Length;
            for (int k = i + 1; k < limit; k++)
            {
                char c = s[k];
                if (c == ']') { close = k; break; }
                if (c == '[' || c == '\n' || c == ' ') return 0;
            }
            if (close < 0) return 0;
            int n = close - i - 1;
            if (n == 1 && s[i + 1] == '-') return close - i + 1;
            if (n == 6)
            {
                for (int k = i + 1; k < close; k++)
                    if (!IsHex(s[k])) return 0;
                return close - i + 1;
            }
            if (n >= 1 && n <= 20)
            {
                for (int k = i + 1; k < close; k++)
                    if (!IsLetter(s[k])) return 0;
                return close - i + 1;
            }
            return 0;
        }

        static bool IsLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        /// <summary>串里有没有汉字（含全角标点）。用来识别「这串已经是我们的译文了」。
        /// 判定：CJK 统一表意文字（U+4E00–U+9FFF）+ 中文标点/全角符号常用区
        /// （U+3000–U+303F 标点、U+FF00–U+FFEF 全角）。</summary>
        static bool HasCjk(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '\u4e00' && c <= '\u9fff') return true;
                if (c >= '\u3000' && c <= '\u303f') return true;
                if (c >= '\uff00' && c <= '\uffef') return true;
            }
            return false;
        }

        static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        static bool StartsWith(string s, int at, string key)
        {
            if (at + key.Length > s.Length) return false;
            for (int k = 0; k < key.Length; k++)
                if (s[at + k] != key[k]) return false;
            return true;
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
        // ★ 2026-10-23：F4 已退役，文本层不再有自己的按键 —— 它跟着字体层的
        //   **汉化总开关（F3）**一起动。v1.0.0 起两层在同一个程序集，HanhuaPlugin
        //   直接调下面这个 SetOn(bool)（旧版是反射调用，已删除）。
        //   ★ 返回值是 bool（不是 void）：字体层靠它判断"到底切上没有"。
        public static bool SetOn(bool on)
        {
            try
            {
                if (on) Enable(); else Disable();
                return textOn == on;
            }
            catch (Exception e) { Say("!! SetOn 异常: " + e.Message); return false; }
        }

        /// <summary>当前是否开着（字体层可用它核对状态）。</summary>
        public static bool IsOn()
        {
            return textOn;
        }

        static void Enable()
        {
            if (textOn) return;
            textOn = true;
            Say("");
            Say("======== 文本层 = 开（随 F3 汉化总开关）========");
            if (dictCount == 0) Say("!! 词表为空 —— " + loadError);
            // ★ v0.9.0：打开时把屏上已有的 label 催一遍。ProcessText 补丁只在 label
            //   「变脏」时才被调用，已经画出来的静态文本不会自己重算，所以要主动
            //   MarkAsChanged。这是**开总开关时的一次性全扫**，不是每帧轮询 ——
            //   开销可接受，且只在拨开关的那一帧发生。
            RefreshAllLabels();
        }

        static void Disable()
        {
            if (!textOn) return;
            textOn = false;
            Say("");
            Say("======== 文本层 = 关（随 F3 汉化总开关）========");
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
                if (s.Length >= 20) DumpMissDetail(s);
            }
        }

        /// <summary>★ v0.10.2 临时诊断（剧情台词查不到译文时用）：把未命中的长串**全文**写进
        /// `_cw3hanhua\_dict\miss_dump.txt`，并当场试探几种「可能被游戏/XML 改过」的变体
        /// （去尾部空白、补尾换行、双空格折叠、CRLF→LF），命中就记下来 —— 这样一眼能看出
        /// 词表 key 与运行时字符串到底差在哪。诊断完可以整段删掉。</summary>
        static void DumpMissDetail(string s)
        {
            try
            {
                if (dictPath == null) return;
                string file = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dictPath), "miss_dump.txt");
                StringBuilder sb = new StringBuilder();
                sb.Append("=== ").Append(DateTime.Now.ToString("HH:mm:ss")).Append(" len=").Append(s.Length);
                sb.Append(" ===\n[raw] ").Append(Esc(s)).Append('\n');

                // 变体试探：命中哪一条，就说明运行时串与词表差在哪
                string[] names = { "TrimEnd", "Trim", "s+\\n", "collapse2sp", "CRLF->LF", "TrimEnd+\\n" };
                string[] vars =
                {
                    s.TrimEnd(), s.Trim(), s + "\n",
                    CollapseSpaces(s), s.Replace("\r\n", "\n"), s.TrimEnd() + "\n"
                };
                for (int i = 0; i < vars.Length; i++)
                {
                    string zh;
                    if (dict.TryGetValue(vars[i], out zh))
                        sb.Append("[HIT:").Append(names[i]).Append("] ").Append(Esc(vars[i])).Append('\n');
                }

                // 前缀相同的最接近 key：报第一个不同字符的位置
                string pre = s.Length >= 20 ? s.Substring(0, 20) : s;
                foreach (KeyValuePair<string, string> kv in dict)
                {
                    string k = kv.Key;
                    if (k.Length < 20 || !k.StartsWith(pre, StringComparison.Ordinal)) continue;
                    int d = 0, lim = Math.Min(k.Length, s.Length);
                    while (d < lim && k[d] == s[d]) d++;
                    sb.Append("[near] len=").Append(k.Length).Append(" firstdiff=").Append(d);
                    if (d < lim)
                        sb.Append(" got=U+").Append(((int)s[d]).ToString("X4"))
                          .Append(" want=U+").Append(((int)k[d]).ToString("X4"));
                    sb.Append("\n[key] ").Append(Esc(k)).Append('\n');
                    break;
                }
                sb.Append('\n');
                File.AppendAllText(file, sb.ToString(), Utf8);
            }
            catch (Exception) { }
        }

        static string Esc(string s)
        {
            return s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        static string CollapseSpaces(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == ' ' && i + 1 < s.Length && s[i + 1] == ' ') continue;
                sb.Append(s[i]);
            }
            return sb.ToString();
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

            if (meshSwaps.Count == 0) return;
            List<int> deadM = null;
            foreach (KeyValuePair<int, MeshSwap> kv in meshSwaps)
            {
                if (kv.Value.mesh == null)
                {
                    if (deadM == null) deadM = new List<int>();
                    deadM.Add(kv.Key);
                }
            }
            if (deadM != null) for (int i = 0; i < deadM.Count; i++) meshSwaps.Remove(deadM[i]);
        }

        static void RestoreAll()
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

            // TextMesh 那批：直接写回英文即可。TextMesh 没有「processed」中间层，
            // 改了 text 立刻重建网格，不需要 ForceRefresh 那套。
            // 但**只有我们改过的**才还原 —— 资产里烘进去的中文不归本层管（那要 --restore）。
            int nm = 0, dm = 0;
            List<int> removeM = new List<int>();
            foreach (KeyValuePair<int, MeshSwap> kv in meshSwaps)
            {
                MeshSwap s = kv.Value;
                try
                {
                    if (s.mesh != null) { s.mesh.text = s.en; nm++; }
                    else dm++;
                }
                catch (Exception) { }
                removeM.Add(kv.Key);
            }
            for (int i = 0; i < removeM.Count; i++) meshSwaps.Remove(removeM[i]);

            Say("  已还原 " + n + " 处文本" + (dead > 0 ? "（" + dead + " 处对象已销毁）" : "")
                + (nm + dm > 0 ? "；TextMesh " + nm + " 处" + (dm > 0 ? "（" + dm + " 处已销毁）" : "") : ""));
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
        /// 只在**拨总开关**（F3 开 / SetOn(true)）或 F9 重载词表时调用，不是每帧轮询 ——
        /// 日常显示完全靠 ProcessText 补丁被动生效，不需要这里出力。
        ///
        /// 为什么要催：补丁挂在 ProcessText 上，而 ProcessText 只在 label 变脏时被调
        /// （`get_processedText` 先查 `hasChanged`）。已经画出来的静态文本不变脏，
        /// 就永远不会再走一遍 —— 按 F3 打开后它会一直保持英文，直到那个界面重进。
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

        // ================================================================ Tick（原 Update）
        // ★ v1.0.0：不再是 MonoBehaviour，改由 HanhuaPlugin.Update 每帧调用。
        //   F3 不在本层（总开关在字体层），F5 / F9 在这里。
        //   场景切换的检测与打日志由 HanhuaPlugin.Update 统一做（一处一帧一次），
        //   本层只在那边发现切换后收到 OnSceneChanged()。原先两层各有一份检测，
        //   合并后会把同一行日志打两遍（v1.0.0 已修）。
        public static void Tick()
        {
            try
            {
                // F4 已退役：文本层现在跟着字体层的汉化总开关（F3）走。
                if (Input.GetKeyDown(KeyCode.F5)) DumpMisses();
                if (Input.GetKeyDown(KeyCode.F9))
                {
                    Say(""); Say("======== F9: 重载词表 ========"); LoadDict();
                    // 词表变了，屏上已有的 label 不会自己重算 —— 催一遍（同总开关的理由）
                    if (textOn) RefreshAllLabels();
                }
            }
            catch (Exception e) { Say("!! Tick 异常: " + e.Message); }
        }

        /// <summary>场景切换：由 HanhuaPlugin.Update 检测到后调用（一个事件只调一次）。
        /// 原先这段住在文本层自己的 Update 里，合并成一个插件后必须挪出来，
        /// 否则两层各打一份「=== 场景切换 ===」日志。</summary>
        public static void OnSceneChanged()
        {
            try
            {
                PruneSwaps();           // ★ 只清已销毁的，不能 swaps.Clear()
                missSeen.Clear();
            }
            catch (Exception e) { Say("!! OnSceneChanged 异常: " + e.Message); }
        }

        /// <summary>F5：把本轮「经过 UILabel 但词表里查不到」的英文串全打出来。
        /// v0.9.0 起补丁点在 ProcessText，覆盖全部 1029 个 UILabel（含资产序列化文本），
        /// 所以这里列出的就是**还缺译文的完整清单**：补进词表后按 F9 重载即时生效。
        /// 反过来说，界面上还是英文、这里又没列出的，才是根本不经 UILabel 的对象
        /// （TextMesh：text 是 extern icall 钩不上，只能改资产，见 NOTES 4.6）。</summary>
        static void DumpMisses()
        {
            Say("");
            Say("======== F5: 未命中清单（prefix 走过但无译文）========");
            Say("  累计 prefix 调用 " + prefixCalls + "  命中 " + prefixHits
                + "  拼接命中 " + hitConcat + "  分段命中 " + hitSeg
                + "  未命中 " + missSeen.Count + " 条");
            int i = 0;
            foreach (string s in missSeen)
            {
                i++;
                Say("  " + i + ". " + Trunc(s, 90));
            }
            if (missSeen.Count == 0) Say("  （空 —— 本场景所有经过 ProcessText 的串都命中词表了）");
        }

        // ================================================================ HUD
        // ★ 2026-10-21：HUD 只报**开关状态**，内部数字全部移到 F11 的 dump
        //   （TextPlugin.DumpLine）—— 用户明确要求"这些信息不用暴露给用户"。
        //   宿主是 HanhuaPlugin 的 OnGUI，它直接调 HudLine() 拼进第一行。
        //   ★ 返回值只允许是 "文本层:开/关" 这一种形态：字体层直接把它拼到
        //     按键行里，多一个字都会把那一行撑长。
        //   ★ 2026-10-23：原先是 "F4 文本层:开/关"，F4 退役后前缀里的 F4 去掉，
        //     免得用户去找一个已经不存在（且已被 F3 取代）的键。
        //   ★ v1.0.0：本层不再有自己的 OnGUI 兜底条 —— 两层同一个插件，
        //     不再有"对方没装载"这种情况。
        public static string HudLine()
        {
            try { return "文本层:" + (textOn ? "开" : "关"); }
            catch (Exception) { return "文本层:?"; }
        }

        /// <summary>F11 dump 用的一整行：所有数字都在这里（HUD 上不显示）。</summary>
        public static string DumpLine()
        {
            try
            {
                return "文本层 v" + Ver
                    + "  开关=" + (textOn ? "开" : "关")
                    + "  词表=" + dictCount + " 条"
                    + "  prefix=" + prefixCalls + "/" + prefixHits
                    + "  拼接=" + hitConcat
                    + "  分段=" + hitSeg
                    + "  归一化=" + hitNorm
                    + "  mesh=" + meshCalls + "/" + meshHits
                    + "  未命中=" + missSeen.Count
                    + "  已记录 label=" + swaps.Count
                    + "  TextMesh=" + meshSwaps.Count;
            }
            catch (Exception) { return "文本层: 状态读取异常"; }
        }

        /// <summary>出错原因（HUD 只在非空时多画一行）。</summary>
        public static string ErrorLine()
        {
            try
            {
                if (dictCount == 0) return (loadError.Length > 0 ? loadError : "词表为空");
                if (loadError.Length > 0) return loadError;
                return "";
            }
            catch (Exception) { return ""; }
        }
    }
}
