// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (C) 2026 saigyou
//
// 本文件是 CW3 Hanhua 的一部分，按 GNU LGPL v2.1（或更新版本）授权。
// 全文见仓库根目录的 LICENSE；许可分层与第三方声明见 NOTICE。
// ============================================================================
//  CW3 汉化 · 文本层 v1.5.0 —— 词表 + UILabel.ProcessText + 5 个 TextMesh 写入点 + CRPL 消息 + IMGUI
//
//  字体层负责"字形从哪来"，本层负责"英文串怎么变中文"。
//
//  ★ v1.3.0（2026-10-23）：把 CRPL 教学提示的翻译**提前到源头** ——
//    补丁打在 `CrplCore.ProcessCommands` 的 postfix，直接把 `GameSpace.message`
//    这个字段换成中文。这是用户点名的方案，确实比 v1.2.0 在 GUI.Label 上换更好：
//      · 时机更早：CRPL 刚把英文写进字段，我们就换掉；
//      · 盒子由游戏自己算：`GameSpace.OnGUI` 里 `CalcSize` 量的已经是中文，
//        Rect / 居中 / 780 钳位全部天然正确 —— 我们**一行 Rect 都不用碰**，
//        v1.2.0 那个靠「宽 ≈ 英文宽 ±28px」猜盒子的 FitBox 已随之删除；
//      · 无状态：字段本身就是显示用的，`message` 全游戏只有 `GameSpace.OnGUI` 读
//        （`ilx fields --type GameSpace message`：OnGUI 4×ldfld，另 1×stfld 是
//         `GameSpace.GameUpdate` 每帧开头清空、2×stfld 是 `CrplCore.ProcessCommands`
//         的 SHOWMESSAGE / SHOWMESSAGEDISMISSIBLE），没有任何逻辑判定拿它做比较。
//    字体问题不变：仍要 `Font.CreateDynamicFontFromOSFont` 造中文字体，
//    但改成塞进 `GameSpace.instance.skin` 的 `"message"` 样式 —— 游戏自己那句
//    `skin.GetStyle("message")` 拿到的就是它。
//
//  ★ v1.2.0（2026-10-23，**已于 v1.3.0 整段删除**）：曾经给 `GUI.Label` 打过一层
//    prefix 做 IMGUI 总兜底。它确实能把那句教学提示翻掉（它是全六种重载的唯一收口），
//    但要靠 `|r.width - 英文宽| <= 28px` 猜「这个盒子是不是自动量的」才能重排，
//    且会波及 `SGT_CameraFPS` / `SGT_CameraMessage` / `KeyEntryGUI` 等处的定宽盒子。
//    v1.3.0 改成在源头（`CrplCore.ProcessCommands` postfix）换字段后，
//    `GameSpace.OnGUI` 自己就算对了盒子 —— 那层兜底**没有存在意义**，
//    留着只会让「界面还是英文」这种问题多一条要排查的来源。
//    → 已删。那套 `FitBox` 猜盒子的做法**不要复活**。
//
//  ★ v1.4.0（2026-10-23）：IMGUI 翻译层**重新加了回来，但换了个收口点和做法** ——
//    这次打在 `GUILayout.Label/Box/Button/RepeatButton/Toggle` 的**文字重载**上
//    （`ref string __N` / `ref GUIContent __N`），在 `GUILayoutUtility.GetRect`
//    **之前**把入参换成中文 —— 宽度由游戏自己按中文量，**依然一行 Rect 都不碰**，
//    没有 FitBox、没有「猜盒子」。覆盖的是完全不经 NGUI 的那些界面：
//    结算面板 / 在线得分 / 地图编辑器 / 殖民空间站 / 世界机器 / 统计成就等。
//    `imguiCalls/imguiHits/imguiPatched` 三个统计随之回归（见 F5 与 F11 dump）。
//    详见 4.3a。**不碰 `GUILayout.TextField`**（返回值是玩家输入数据）。
//
//  ★ v1.5.0（2026-10-24）：补上 `GUI.*` 这一族 —— **方案 A2**（用户 m01714 拍板）。
//    v1.4.0 只覆盖 `GUILayout`，而游戏里有一批界面直接调 `GUI.Xxx(Rect, String, GUIStyle)`：
//    `WorldMachineManager.DoWorldMachineWindow` 的 `Return to Origin`、
//    `KeyEntryGUI.ShowKeyEntry` 整个注册界面、`Menu.OnGUI` 的 `Clear` 等。
//    它们不经过 GUILayout，所以 v1.4.0 的补丁点碰不到，日志里也不会留未命中。
//
//    **A2 = 只做精确命中，不做任何拼接/归一/分段**。理由：`GUI.*` 的字面量全部是
//    完整的短串，压根不需要分段能力；而分段正是「猜」的来源 —— 在固定 Rect 里
//    把 `FPS: ` 吃掉再把 `60.0` 透传，会发生什么完全不可控。
//    所以 `LookupGuiExact` 只查 `dict[en]` 精确相等。代价是 `FPS: 60.0` / `Map Size: 1x1`
//    这类拼接串在 GUI.* 面上一律不译（前者本来就不该动，后者走 GUILayout 已覆盖）。
//
//    **为什么打 String 重载不会干扰 v1.4.0 的 GUILayout 补丁**（这条是动手前专门验证的）：
//      `GUILayout.Label(string,…)` 体内 `0001: call GUIContent.Temp(String)` 立刻转成
//      GUIContent，再 `call GUILayout.DoLabel(GUIContent, …)` → `GUI.Label(Rect, GUIContent, GUIStyle)`。
//      DoButton / DoBox / DoRepeatButton 同样只转发到 GUIContent 重载。
//      ⇒ **GUILayout 那条链从不接触 `GUI.Xxx(Rect, String, …)`**，两组入口在类型上就不重叠。
//      反过来 `GUI.Button(Rect, String, GUIStyle)` 是 `call GUIContent.Temp` 后
//      **直接进 `GUI.DoButton(Rect, GUIContent, IntPtr)`**，不绕 GUIContent 重载 ⇒ 不会翻两次。
//      也因此**绝不能**顺手把 GUIContent 重载也打上：那条链的 `GetRect` 在 `GUI.Label`
//      **之前**跑，量宽早就用英文完成了，中文会被塞进英文尺寸的框里 —— 正是 v1.2.0 的形状。
//
//    **矩形风险**：`GUI.*` 没有 layout 阶段可以重算宽度，Rect 全是游戏写死的常量
//    （`KeyEntryGUI` 四个 Label 里一个 `CalcSize` 都没有）。所以这一族**逐处核对过**：
//    Return to Origin 120×22 / Enter Key 100×20 / Invalid Key… 280×20 / Product Key 280×100
//    / Arc Eternal 270 宽(fontSize 26) / 产品密钥提示 600 宽 —— 中文都装得下。
//
//    **不译 `Creeper World III`**（用户 m01714 同批拍板）：它是游戏名本身，且
//    fontSize 36、Rect 只有 270 宽。词表 zh 留空 ⇒ dict 里没有这条 ⇒ `TryGetValue`
//    返回 false ⇒ 自然跳过 —— 不需要特判。
//
//    **不碰 `GUI.TextField / TextArea`**：同 GUILayout，返回值是玩家输入。
//    统计另开 `guiCalls/guiHits/guiPatched`，**不与 imgui 混算**（混了就分不清哪层在干活）。
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
//    ③ Harmony postfix on CrplCore.ProcessCommands（v1.3.0）—— CRPL 消息的源头收口
//         把 GameSpace.message 直接换成中文，盒子由游戏自己按中文算
//         + Font.CreateDynamicFontFromOSFont 造中文字体塞进 GameSpace.skin 的 "message" 样式
//       ★ 这是 CRPL 教学提示的**唯一**替换路径。v1.2.0 那层 GUI.Label prefix 已删除
//         （见顶部 v1.2.0 条目），所以「教学提示还是英文」时只需要查这一条路。
//    ④ Harmony prefix on GUILayout.Label/Box/Button/RepeatButton/Toggle 的文字重载（v1.4.0）
//         在 GUILayoutUtility.GetRect 之前改入参 → 宽度由游戏按中文量，不碰 Rect
//    ⑤ Harmony prefix on GUI.Label/Box/Button/RepeatButton 的 **String 重载**（v1.5.0）
//         只做精确命中，补 ④ 碰不到的直调点（Return to Origin / KeyEntryGUI / Menu.Clear 等）
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
        public const string Ver = "1.5.0";

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

        // CRPL 消息（v1.3.0）：CrplCore.ProcessCommands postfix 那条路 ——
        // 关卡教学提示（Tempus 等 15 关的 ShowMessage）**唯一**的替换路径。
        static int crplCalls;
        static int crplHits;

        // 走过 ProcessText 但词表里查不到的串。**按 key 去重、上限 2000**。
        // v0.9.0 起补丁点在 ProcessText，所以这里收到的是**全部 UILabel** 的未命中
        // （含资产序列化文本），不再是 set_text 那 322 个调用点的子集。
        // 用途：F5 打印清单 → 补词表 → F9 重载即可，不用重启游戏。
        //
        // ★ v1.4.1：改用 Dictionary<去重键, 首次出现的原串>，去重键 = MissKey(s)。
        //   起因（用户 m00859 要求）：原来按**整串**去重，可游戏大量文案是
        //   「碎片 + 运行时值」拼的 —— `Map Size: 162x121`、`Collected: 12`、
        //   `Amt: 7` … 每换一个数字就是一条新记录，400 条上限几秒就爆，
        //   真正缺的模板反而挤不进来（用户报「日志里没看到未命中」就是这个原因）。
        //   现在把数字串归一成 `#`（`Map Size: #x#`），同一模板只记一次；
        //   打印时仍给出**首次出现的原串**，可读性不降。上限同时提到 2000。
        const int MissCap = 2000;
        static readonly Dictionary<string, string> missSeen = new Dictionary<string, string>();

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

            PatchCrplMessage();      // v1.3.0：源头（CrplCore.ProcessCommands postfix）

            PatchIMGUI();            // v1.4.0：第四层，GUILayout 文字重载

            PatchGUI();              // v1.5.0：第五层，GUI.* 的 String 重载（只做精确命中）

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

        // ================================================================ CRPL 消息（v1.3.0）
        /// <summary>★ v1.3.0（2026-10-23）：把 CRPL 教学提示的翻译**提前到源头**。
        ///
        /// 与 v1.2.0 的 IMGUI 补丁相比，这条路好在三点：
        ///   ① **时机更早**：中文在 `GameSpace.message` 被赋值的那一刻就写进去，
        ///      `GameSpace.OnGUI` 后面那句 `skin.GetStyle("message").CalcSize(new GUIContent(message))`
        ///      量到的**就是中文尺寸** —— 盒子由游戏自己算准，我们一行 Rect 都不用碰，
        ///      v1.2.0 那个 `FitBox` 的启发式判定（`|r.width - enSize.x| <= 28f`）已随补丁删除。
        ///   ② **只覆盖 CRPL 提示**，不像 `GUI.Label` 收口那样波及 `SGT_CameraFPS` /
        ///      `SGT_CameraMessage` / `KeyEntryGUI` 等处的自动量宽盒子。
        ///   ③ **无状态**：每帧 `GameSpace.GameUpdate` 开头会把 `message` 置 null，
        ///      CRPL 重新 `stfld` 时再翻一遍，不需要还原表；关总开关立刻恢复英文。
        ///   这是教学提示**唯一**的替换路径，排查时只看它。
        ///
        /// 为什么打在 `CrplCore.ProcessCommands(Boolean,Boolean,Boolean,Boolean)`（token=0x060009E1）：
        ///   它是 SHOWMESSAGE / SHOWMESSAGEDISMISSIBLE 两条分支的唯一出口，
        ///   而调用方只有 `PreGameUpdate` / `GameLoadedGameUpdate` / `GameUpdate` 三个，
        ///   都在 CRPL 解释器自己的节奏里 —— postfix 时机正好落在「已经 `stfld GameSpace.message`、
        ///   还没到 `OnGUI` 绘制」之间。
        ///
        /// 字段可见性（IL 实证）：`GameSpace` 与 `CrplCore` 都是 tdPublic 顶层类；
        ///   `GameSpace.instance`(static) / `skin` / `message`(fdNotSerialized) 全部 fdPublic，
        ///   但这里仍走反射，免得哪天字段变成 internal 就编译不过。
        ///
        /// `message` 的读者只有 `GameSpace.OnGUI`（4 处 ldfld），**没有任何逻辑分支读它**
        /// —— 是个纯显示字段，所以在这里改成中文是安全的，不会影响任何判定。</summary>
        static void PatchCrplMessage()
        {
            try
            {
                MethodInfo m = null;
                MethodInfo[] all = typeof(CrplCore).GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                for (int i = 0; i < all.Length; i++)
                {
                    MethodInfo x = all[i];
                    if (x.Name != "ProcessCommands") continue;
                    ParameterInfo[] ps = x.GetParameters();
                    if (ps.Length != 4) continue;
                    bool okAllBool = true;
                    for (int k = 0; k < 4; k++)
                        if (ps[k].ParameterType != typeof(bool)) { okAllBool = false; break; }
                    if (!okAllBool) continue;
                    m = x; break;
                }
                if (m == null) { Say("  （找不到 CrplCore.ProcessCommands，CRPL 补丁跳过）"); return; }

                Harmony h = new Harmony("cw3hanhua.text.crpl");
                PatchProcessor p = h.CreateProcessor(m);
                p.AddPostfix(new HarmonyMethod(typeof(TextPlugin).GetMethod("PostfixCrplMessage",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
                p.Patch();

                Patches pi = Harmony.GetPatchInfo(m);
                int n = pi == null ? -1 : pi.Postfixes.Count;
                Say("★ CRPL patch: CrplCore.ProcessCommands  postfix=" + n
                    + (n == 1 ? "" : "  !! 没装上"));
                if (n == 1) Say("  覆盖面: CRPL ShowMessage / ShowMessageDismissible 的全部教学提示（源头换字）");
            }
            catch (Exception e) { Say("!! 装 CRPL 补丁失败: " + e.Message); }
        }

        static FieldInfo fldGsInstance;
        static FieldInfo fldGsMessage;
        static FieldInfo fldGsSkin;

        static void PostfixCrplMessage()
        {
            if (!textOn) return;
            try
            {
                if (fldGsInstance == null)
                {
                    fldGsInstance = typeof(GameSpace).GetField("instance",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    fldGsMessage = typeof(GameSpace).GetField("message",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    fldGsSkin = typeof(GameSpace).GetField("skin",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fldGsInstance == null || fldGsMessage == null) return;
                }

                GameSpace gs = fldGsInstance.GetValue(null) as GameSpace;
                if (gs == null) return;
                string en = fldGsMessage.GetValue(gs) as string;
                if (en == null || en.Length == 0) return;
                if (HasCjk(en)) return;      // 已经是中文 → 上一帧翻译过的，别再查一遍

                crplCalls++;
                string zh;
                try { zh = Lookup(en); } catch (Exception) { return; }
                if (zh == null || zh == en) { NoteMiss(en); return; }

                fldGsMessage.SetValue(gs, zh);
                crplHits++;

                // 中文要能显示出来：把系统字体塞进 GameSpace.skin 的 "message" 样式。
                // GUISkin.GetStyle 返回皮肤内部对象引用，改一次持久生效。
                if (fldGsSkin != null)
                {
                    GUISkin sk = fldGsSkin.GetValue(gs) as GUISkin;
                    if (sk != null)
                    {
                        GUIStyle st = sk.GetStyle("message");
                        if (st != null) ApplyCjkFont(st);
                    }
                }
            }
            catch (Exception) { }
        }

        // ================================================================ IMGUI（v1.4.0）
        /// <summary>给 `GUILayout.Label / Box / Button / RepeatButton / Toggle` 的**文字重载**打
        /// prefix，把文案入参换成中文。这是第四条替换路径，补的是「根本不经 UILabel」的那些界面。
        ///
        /// **为什么现在才做**：v1.2.0 曾试过在 `GUI.Label` 上打补丁 + 用
        /// `|r.width - enSize.x| <= 28f` 猜「这个盒子是不是自动量的」，结果误伤
        /// `SGT_CameraFPS` / `SGT_CameraMessage` / `KeyEntryGUI` 的定宽盒子，
        /// v1.3.0 整段删掉（用户要求「出问题时只能有一个来源」）。
        /// 现在换了个更干净的收口点：**在 layout 之前改入参**，不动任何矩形。
        ///
        /// **为什么是 GUILayout 而不是 GUI**：两者最终都走
        /// `GUILayout.DoLabel` → `GUILayoutUtility.GetRect(content, style, options)`
        /// → `GUI.DoLabel(rect, content, style)`。`DoLabel` 是 private（Harmony 打不上）。
        /// 而 `GUILayout.Label` 的 6 个重载都是 public + 有真实 IL 体 —— 可以打。
        /// 更关键的是：`GetRect` 就在 `GUILayout.Label` 体内、**用我们改过的入参**
        /// 去量宽，所以中文的宽度天然算对，不需要猜盒子，也不会溢出。
        /// 全库 `GUILayout.*` 有 689 处、直接 `GUI.Label` 只有 58 处（多在编辑器 demo 里），
        /// 所以覆盖 GUILayout 就是覆盖了 92% 的面。
        ///
        /// ★ 更正（2026-10-24，v1.5.0）：这里原来写「`GUI.Label` 的 6 个重载全是 abstract
        ///   （reference stub，无 IL 体）」—— **这是错的**。`ilx dump GUI Label --dll UnityEngine.dll`
        ///   实测 6 个重载全部有真实 IL 体（il=5~7，RVA 非 0），`GUI.Box/Button/RepeatButton` 同样。
        ///   当年打不上的是 `GUI.DoLabel/DoButton`（private），不是 `GUI.Label`。
        ///   所以 v1.5.0 起 `GUI.*` 的 String 重载由 PatchGUI() 接手（见那里）。
        ///   本方法继续只管 GUILayout —— 两族的入口不重叠，见文件头 v1.5.0 的验证记录。
        ///
        /// **不碰 TextField**：它的返回值是玩家输入的文本，翻了会毁数据
        /// （玩家名字、存档名、CRPL 代码都是从这里出去的）。
        ///
        /// **不设字体**：HUD 本身就是 IMGUI 画的中文、一直正常显示 —— Unity 5 的
        /// 动态字体（内置 Arial）对缺字有 OS 回退，`Font.CreateDynamicFontFromOSFont`
        /// 那条路只留给 `GameSpace.skin` 的 message 框（见上）。保持原字体才能
        /// 让拉丁字母的观感与游戏一致（Aldrich）。</summary>
        static int imguiCalls;
        static int imguiHits;
        static int imguiPatched;

        static void PatchIMGUI()
        {
            // 方法名 → 文字参数是否第 0 个（Toggle 是 (bool, text, ...)，文字在 1）
            string[] names = { "Label", "Box", "Button", "RepeatButton", "Toggle" };
            int[] textIdx = { 0, 0, 0, 0, 1 };
            for (int n = 0; n < names.Length; n++)
            {
                try { PatchIMGUIMethod(names[n], textIdx[n]); }
                catch (Exception e) { Say("!! 装 GUILayout." + names[n] + " 补丁失败: " + e.Message); }
            }
            Say("★ IMGUI patch: GUILayout 文字重载 " + imguiPatched + " 个"
                + (imguiPatched > 0 ? "" : "  !! 一个都没装上"));
            if (imguiPatched > 0)
                Say("  覆盖面: 结算/得分/地图编辑器/殖民空间站/统计成就等全部 IMGUI 界面");
        }

        static void PatchIMGUIMethod(string name, int textIdx)
        {
            MethodInfo[] all = typeof(GUILayout).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            for (int i = 0; i < all.Length; i++)
            {
                MethodInfo m = all[i];
                if (m.Name != name) continue;
                if (m.IsAbstract || m.GetMethodBody() == null) continue;   // 无 IL 体，跳过
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length <= textIdx) continue;
                Type tt = ps[textIdx].ParameterType;
                if (tt != typeof(string) && tt != typeof(GUIContent)) continue;  // Texture 重载跳过

                // 文字参数在第 0 还是第 1 位（Toggle 是 (bool, text, ...)）+ 是 string 还是 GUIContent
                // → 四种 prefix 就够，带不带 GUIStyle 都用同一个（文字下标不变，样式参数用不上）
                string prefixName;
                if (tt == typeof(string))
                    prefixName = textIdx == 0 ? "PrefixGuiStr0" : "PrefixGuiStr1";
                else
                    prefixName = textIdx == 0 ? "PrefixGuiCon0" : "PrefixGuiCon1";

                // ★ Harmony id 必须唯一到「每个重载」：同名同参数数同文字下标的两个重载
                //   （如 Label(GUIContent,…) 与 Label(string,…) 都是 2 参、文字在 0）
                //   若共用一个 id，owner 字符串撞在一起，将来摘补丁会摘错。
                Harmony h = new Harmony("cw3hanhua.text.imgui." + name + "." + ps.Length + "."
                    + textIdx + "." + (tt == typeof(string) ? "s" : "c"));
                PatchProcessor p = h.CreateProcessor(m);
                p.AddPrefix(new HarmonyMethod(typeof(TextPlugin).GetMethod(prefixName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
                p.Patch();

                Patches pi = Harmony.GetPatchInfo(m);
                int cnt = pi == null ? -1 : pi.Prefixes.Count;
                if (cnt == 1) imguiPatched++;
                else Say("  !! GUILayout." + name + "/" + ps.Length + " 参数 没装上（prefix=" + cnt + "）");
            }
        }

        // ================================================================ GUI.*（v1.5.0，方案 A2）
        /// <summary>给 `GUI.Label / Box / Button / RepeatButton` 的 **String 重载**打 prefix。
        /// 这是第五条替换路径，补 v1.4.0（GUILayout）碰不到的那些直调点。
        ///
        /// **只打 String 重载，绝不打 GUIContent 重载** —— 后者是 GUILayout 那条链的终点
        /// （`GUILayout.DoLabel` → `GUI.Label(Rect, GUIContent, GUIStyle)`），
        /// 打了会与 v1.4.0 的 prefix 叠加，而且 GetRect 早已用英文量完宽度，中文必然溢出。
        /// 详见文件头 v1.5.0 条目里的调用链验证。
        ///
        /// **只给 `(Rect, String)` 与 `(Rect, String, GUIStyle)` 两种形态打**：
        /// 文字恒在参数 1（Rect 在 0），所以用 `PrefixGuiExact1`（**不是** `PrefixGuiStr1` ——
        /// 那一个会走完整 Lookup 的四级链，本层只做精确命中，理由见下）。
        /// `(Rect, Texture)` / `(Rect, GUIContent)` / `(Rect, Texture, GUIStyle)` /
        /// `(Rect, GUIContent, GUIStyle)` / `(Rect, GUIStyle)` 五种形态按参数类型自然跳过。
        ///
        /// **不碰 TextField / TextArea**：它们的 String 重载返回值/引用是玩家输入，
        /// 翻了会毁数据（与 GUILayout 那条同一条纪律）。本方法的方法名白名单里没有它们。
        ///
        /// **A2 = 只精确命中**：prefix 用 `PrefixGuiExact1` → `LookupGuiExact`，只查 `dict[en]`。
        /// 不走 v1.4.0 那套 `Lookup` 四级链 —— 这一族的 Rect 是常量、没有 layout 阶段可重算，
        /// 分段拼出来的长度不可控。所以本层「查不到就不动」，宁可漏译也不猜。</summary>
        static int guiCalls;
        static int guiHits;
        static int guiPatched;

        static void PatchGUI()
        {
            string[] names = { "Label", "Box", "Button", "RepeatButton" };
            for (int n = 0; n < names.Length; n++)
            {
                try { PatchGUIMethod(names[n]); }
                catch (Exception e) { Say("!! 装 GUI." + names[n] + " 补丁失败: " + e.Message); }
            }
            Say("★ IMGUI patch: GUI.* 文字重载 " + guiPatched + " 个（方案 A2，只精确命中）"
                + (guiPatched > 0 ? "" : "  !! 一个都没装上"));
            if (guiPatched > 0)
                Say("  覆盖面: Return to Origin / 产品密钥界面 / 菜单 Clear 等直调 GUI.* 的界面");
        }

        static void PatchGUIMethod(string name)
        {
            MethodInfo[] all = typeof(GUI).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            for (int i = 0; i < all.Length; i++)
            {
                MethodInfo m = all[i];
                if (m.Name != name) continue;
                if (m.IsAbstract || m.GetMethodBody() == null) continue;   // 无 IL 体，跳过
                ParameterInfo[] ps = m.GetParameters();
                // 只认 (Rect, String) 与 (Rect, String, GUIStyle)：文字恒在 1，Rect 在 0
                if (ps.Length < 2 || ps.Length > 3) continue;
                if (ps[0].ParameterType != typeof(Rect)) continue;
                if (ps[1].ParameterType != typeof(string)) continue;       // ★ 非 String 一律跳过
                if (ps.Length == 3 && ps[2].ParameterType != typeof(GUIStyle)) continue;

                // id 里带「参数个数」就够区分这两个重载（2 参 / 3 参），文字下标恒为 1
                Harmony h = new Harmony("cw3hanhua.text.gui." + name + "." + ps.Length + ".s1");
                PatchProcessor p = h.CreateProcessor(m);
                p.AddPrefix(new HarmonyMethod(typeof(TextPlugin).GetMethod("PrefixGuiExact1",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
                p.Patch();

                Patches pi = Harmony.GetPatchInfo(m);
                int cnt = pi == null ? -1 : pi.Prefixes.Count;
                if (cnt == 1) guiPatched++;
                else Say("  !! GUI." + name + "/" + ps.Length + " 参数 没装上（prefix=" + cnt + "）");
            }
        }

        /// <summary>把一条文案换成中文；没命中返回 null。IMGUI 的串没有色标解析，
        /// 但游戏偶尔会把 `[ffffff]` 之类塞进 IMGUI 文本 —— 那属于极少数，交给词表原样匹配。</summary>
        static string LookupGuiText(string en)
        {
            if (!textOn || en == null || en.Length == 0) return null;
            if (HasCjk(en)) return null;              // 已经是中文
            string zh;
            try { zh = Lookup(en); } catch (Exception) { return null; }
            if (zh == null || zh == en) { NoteMiss(en); return null; }
            return zh;
        }

        /// <summary>★ v1.5.0（方案 A2）：`GUI.*` 专用 —— **只做精确命中**，直接查 dict。
        /// 不走 Lookup() 的四级链（拼接 / 归一化 / 分段），因为 `GUI.*` 的 Rect 是游戏
        /// 写死的常量、没有 layout 阶段可以重算宽度，分段拼出来的串长度不可控。
        /// 代价：`FPS: 60.0`、`Map Size: 1x1` 这类「碎片+变量」串在这一层不译
        /// （前者是性能显示本来就不该动，后者走 GUILayout 那条已由 v1.4.1 的 x 规则覆盖）。
        ///
        /// 词表里 zh 留空的条目（如 `Creeper World III`）dict 里根本不存在，
        /// 自然查不中 —— 不需要为它写特判。</summary>
        static string LookupGuiExact(string en)
        {
            if (!textOn || en == null || en.Length == 0 || dict.Count == 0) return null;
            if (HasCjk(en)) return null;              // 已经是中文
            string zh;
            if (!dict.TryGetValue(en, out zh)) return null;
            if (zh == null || zh.Length == 0 || zh == en) return null;
            return zh;
        }

        static void PrefixGuiExact1(ref string __1)
        {
            guiCalls++;
            if (__1 == null || __1.Length == 0) return;
            string zh = LookupGuiExact(__1);
            if (zh == null) return;
            guiHits++;
            __1 = zh;
        }

        // GUILayout 的 4 种入参形态（文字在第 0 / 第 1 个 × string / GUIContent）各一个 prefix。
        // 不用 `object[] __args` 改参数：那依赖「改完会回写」，不同 Harmony 版本行为不一；
        // `ref string __N` / `ref GUIContent __N` 是确定能生效的写法。
        static void PrefixGuiStr0(ref string __0) { GuiTake(ref __0); }
        static void PrefixGuiStr1(ref string __1) { GuiTake(ref __1); }
        static void PrefixGuiCon0(ref GUIContent __0) { GuiTakeCon(ref __0); }
        static void PrefixGuiCon1(ref GUIContent __1) { GuiTakeCon(ref __1); }

        static void GuiTake(ref string s)
        {
            imguiCalls++;
            string zh = LookupGuiText(s);
            if (zh == null) return;
            imguiHits++;
            s = zh;
        }

        static void GuiTakeCon(ref GUIContent c)
        {
            imguiCalls++;
            if (c == null) return;
            string zh = LookupGuiText(c.text);
            if (zh == null) return;
            imguiHits++;
            // ★ 绝不能写 c.text —— GUILayout 内部用的是 `GUIContent.Temp(String)`，
            //   那返回**静态共享单例 s_Text**，改它等于改全进程的下一个 GUIContent
            //   （v1.2.0 踩过：改完之后所有 Label 一起变中文/串味）。必须换引用。
            c = new GUIContent(zh, c.image, c.tooltip);
        }

        // ---------------------------------------------------------------- IMGUI 中文字形（CRPL 消息用）
        // 剩下的唯一用武之地是 `GameSpace.skin` 的 `"message"` 样式 ——
        // 也就是 CRPL 教学提示那个框（见上一条路径 ③）。
        // 那个 GUIStyle.font 指向 UnityEngine.Font，而游戏里只有 Aldrich-Regular / arial
        // 两个字体资产，cmap 里没有汉字 —— 光换文案会显示成空白或方框。
        // 这里现场造一个动态字体（UnityEngine.dll 里 RVA 非 0、是托管方法，可以正常调）：
        //   Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16)  token=0x0600190D
        // 候选名按「先简体黑体、后退到通用」排序；GetOSInstalledFontNames() 逐个比对。
        static Font cjkFont;
        static bool cjkTried;
        static string cjkFontName = "";
        static readonly string[] cjkCandidates =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
            "Microsoft JhengHei", "SimHei", "黑体", "SimSun", "宋体",
            "Noto Sans CJK SC", "Source Han Sans SC", "Arial Unicode MS"
        };

        static void ApplyCjkFont(GUIStyle style)
        {
            if (style == null) return;
            if (!cjkTried) InitCjkFont();
            if (cjkFont == null) return;
            try
            {
                if (style.font == cjkFont) return;      // 已经是它了，不重复设
                style.font = cjkFont;
            }
            catch (Exception) { }
        }

        static void InitCjkFont()
        {
            cjkTried = true;
            string[] installed = null;
            try { installed = Font.GetOSInstalledFontNames(); } catch (Exception) { }
            try
            {
                for (int c = 0; c < cjkCandidates.Length; c++)
                {
                    string want = cjkCandidates[c];
                    if (installed != null && installed.Length > 0)
                    {
                        bool found = false;
                        for (int i = 0; i < installed.Length; i++)
                        {
                            if (string.Equals(installed[i], want, StringComparison.OrdinalIgnoreCase))
                            { found = true; break; }
                        }
                        if (!found) continue;
                    }
                    Font f = Font.CreateDynamicFontFromOSFont(want, 16);
                    if (f != null)
                    {
                        cjkFont = f; cjkFontName = want;
                        Say("★ CRPL 提示中文字体: " + want + "（Font.CreateDynamicFontFromOSFont，动态）");
                        return;
                    }
                }
                // 全都没匹配上（少见）：硬试一次第一个候选，Unity 自己会退到默认
                Font ff = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16);
                if (ff != null)
                {
                    cjkFont = ff; cjkFontName = "Microsoft YaHei(硬试)";
                    Say("★ CRPL 提示中文字体: 硬试 Microsoft YaHei 成功（GetOSInstalledFontNames 未匹配）");
                    return;
                }
                Say("!! CRPL 提示找不到可用的系统中文字体 → 会显示成空白/方框");
            }
            catch (Exception e) { Say("!! CRPL 提示造中文字体失败: " + e.Message); }
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
                    if (IsLetter(c))
                    {
                        // ★ v1.3.2：单引号里的「键名」原样透传。
                        //   起因（用户实测 F5 报 `Click or 'P'` 未译）：
                        //   GeneralHandlers.SetPauseButton 拼的是
                        //     "Click or '" + InputManager.GetKeyText(mod, key) + "'"
                        //   词表里 `Click or '` → `点击或 '` 有，但键名 `P` 是**单字母**，
                        //   绝不能进词表（单字母 key 会把所有串拆烂）。这里在引号内把
                        //   `P` / `LeftControl-P` / `Space` 这类键名当作变量透传，
                        //   与「数字、色标原样透传」同一个道理：键名不是语言，不参与翻译。
                        int keyEnd = QuotedKeyEnd(s, i);
                        if (keyEnd > i)
                        {
                            sb.Append(s, i, keyEnd - i);
                            i = keyEnd;
                            matched++;
                            continue;
                        }
                        // ★ v1.4.1：「数字x数字」里的那个 x 也当变量透传。
                        //   起因（用户实测 DMD 界面 `Map Size: 101x101` 未译，日志有未命中）：
                        //   WorldMachineManager.DoWorldMachineWindow 拼的是
                        //     "Map Size: " + (Int16)w + "x" + (Int16)h
                        //   词表里 `Map Size: ` 命中、两侧数字本来就逐字符透传，
                        //   偏偏中间的 `x` 是字母、消费不掉 → 触发下面的「全有或全无」→ 整串丢弃。
                        //   这是尺寸写法（16x9 / 101x101 / 1920x1080），与键名同类：**不是语言**。
                        //   只认「数字 + x/X + 数字」这一种极窄形态，别的字母一概不放过。
                        if (IsSizeSep(s, i))
                        {
                            sb.Append(c);
                            i++;
                            continue;
                        }
                        return null;       // 有字母消费不掉 → 全有或全无
                    }
                    sb.Append(c);
                    i++;
                }
            }
            catch (Exception) { return null; }
            if (matched == 0) return null;
            return sb.ToString();
        }

        /// <summary>在 s[i] 处找一个能用的词表 key（最长的、且边界合法的）；没有返回 null。
        /// 左边界必须在这里判，不能放到调用处 —— 标记透传那些分支不需要这个约束。
        ///
        /// ★ v1.3.2：左边界只在「key 自己也以字母起头」时才判。
        ///   起因（用户实测 F5 报 `Shield Key  [ffffff]Health:[-][00ff00] 100%[-]` 未译）：
        ///   单位标题是 `GetUnitTitleDisplay` 拼的 —— `um.GetUnitGameName()` + "  [ffffff]Health:..."
        ///   而词表里存的碎片是 `"  [ffffff]Health:[-][00ff00] "`（**自带两个前导空格**）。
        ///   旧规则 `if (i > 0 && IsLetter(s[i-1])) return null` 一看前一字符是 `Key` 的 `y`
        ///   就直接拒绝，哪怕 key 自己以空格起头、边界天然成立 → 所有单位标题整体放弃。
        ///   现在只有 key 首个字符是字母时才看左邻（防 "Windows" 被 "Window" 吃成「窗口s」），
        ///   key 以空格或 `[` 起头则天然有边界，不必也不能再卡。</summary>
        static string MatchKey(string s, int i)
        {
            if (i > 0 && IsLetter(s[i - 1]) && IsLetter(s[i])) return null;   // 不许从词中段起头
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

        /// <summary>若 s[i] 起是**单引号包住的键名**（`'P'`、`'LeftControl-P'`、`'Space'`），
        /// 返回键名结束后的下标（指向右引号），否则返回 -1。
        ///
        /// 只认这四种字符：字母、数字、`_`、`-`、`+` —— 覆盖 KeyCode 枚举名与 `Ctrl-Shift-P`
        /// 这类组合键写法。长度 1–24，且引号里不能再出现引号。
        ///
        /// **左引号前不能是字母**：否则 `don't` / `isn't` 这类缩写会被当键名吞掉。
        /// 这个判定让本函数只对「`'` 前面是空格/标点/行首」的真引号生效。</summary>
        static int QuotedKeyEnd(string s, int i)
        {
            if (i < 2 || s[i - 1] != '\'') return -1;
            if (IsLetter(s[i - 2])) return -1;              // don't / isn't 之类
            int limit = i + 24;
            if (limit > s.Length) limit = s.Length;
            for (int k = i; k < limit; k++)
            {
                char c = s[k];
                if (c == '\'') return k > i ? k : -1;       // 找到右引号，且内容非空
                if (!IsLetter(c) && !(c >= '0' && c <= '9') && c != '_' && c != '-' && c != '+')
                    return -1;                              // 有空格/其它符号 → 不是键名
            }
            return -1;                                      // 太长或没有右引号
        }

        /// <summary>算不算「词的一部分」：字母或数字。数字也要算 —— 标识符大量是
        /// 字母前缀 + 编号（Link327、Custom1_256、Mesh0），把数字当边界会把它们拆坏。</summary>
        static bool IsWordChar(char c)
        {
            return IsLetter(c) || (c >= '0' && c <= '9');
        }

        /// <summary>★ v1.4.1：s[i] 是不是「尺寸写法」里的分隔符 `x`（`16x9` / `101x101`）。
        ///
        /// 判据极窄，三个条件全要满足：
        ///   - 当前字符是 `x` 或 `X`；
        ///   - 前一个字符是数字；
        ///   - 后一个字符是数字。
        /// 即只认「数字 + x + 数字」正中间那一个字符。
        ///
        /// 为什么敢放宽「字母必须被消费掉」这条铁律：它只把 `x` **原样透传**
        /// （不产生译文、不增加 matched），且必须两侧夹着数字 —— 任何真正的英文单词
        /// 都不可能长成这样（`Windows` 的 `x` 左边是 `d`，不满足）。所以最坏后果只是
        /// 「本该整串放弃的，现在把能译的碎片译出来」，不会拼出中英混杂的坏句子。</summary>
        static bool IsSizeSep(string s, int i)
        {
            if (i <= 0 || i >= s.Length - 1) return false;
            char c = s[i];
            if (c != 'x' && c != 'X') return false;
            if (s[i - 1] < '0' || s[i - 1] > '9') return false;
            if (s[i + 1] < '0' || s[i + 1] > '9') return false;
            return true;
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
            string key = MissKey(s);
            if (missSeen.Count >= MissCap && !missSeen.ContainsKey(key)) return;
            if (!missSeen.ContainsKey(key))
            {
                missSeen[key] = s;                       // 留首次出现的原串，打印时用它
                // 只在"看起来像英文 UI 字符串"时记，避免刷屏
                if (HasLetter(s)) Say("  [未命中] " + Trunc(s, 60));
                if (s.Length >= 20) DumpMissDetail(s);
            }
        }

        /// <summary>★ v1.4.1：算未命中的**去重键** —— 把连续数字归一成 `#`。
        ///
        /// 只动数字，不动字母/标点/空格/换行：
        ///   `Map Size: 162x121` → `Map Size: #x#`
        ///   `Collected: 12`      → `Collected: #`
        ///   `Amt: 7/30`          → `Amt: #/#`
        ///   `Sector 3 Name`      → `Sector # Name`
        /// 32 位以上长数字（时间戳、种子）归一后长度也稳定，不会撑爆键。
        /// 为什么不去字母：去字母会把 `Return to Origin` 与 `Return to Origin2`
        /// 之外的无关串也并到一起（`Next` / `Nexus` 会塌成同一个键），丢失信息。</summary>
        static string MissKey(string s)
        {
            try
            {
                StringBuilder sb = new StringBuilder(s.Length);
                bool inNum = false;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    bool dig = (c >= '0' && c <= '9');
                    if (dig)
                    {
                        if (!inNum) { sb.Append('#'); inNum = true; }
                        continue;
                    }
                    inNum = false;
                    sb.Append(c);
                }
                return sb.ToString();
            }
            catch (Exception) { return s; }
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
                + "  未命中 " + missSeen.Count + " 种（上限 " + MissCap + "）");
            Say("  IMGUI 调用 " + imguiCalls + "  命中 " + imguiHits
                + "  已装重载 " + imguiPatched + " 个（GUILayout，v" + Ver + "）");
            Say("  GUI  调用 " + guiCalls + "  命中 " + guiHits
                + "  已装重载 " + guiPatched + " 个（GUI.* 精确命中，v" + Ver + "）");
            int i = 0;
            foreach (KeyValuePair<string, string> kv in missSeen)
            {
                i++;
                // v1.4.1：数字已归一，同一模板只出一条；这里打**首次出现的原串**
                Say("  " + i + ". " + Trunc(kv.Value, 90));
            }
            if (missSeen.Count == 0) Say("  （空 —— 本场景所有经过 ProcessText 的串都命中词表了）");
        }

        // ================================================================ HUD
        // ★ 2026-10-21：HUD 只报**开关状态**，内部数字全部移到 F11 的 dump
        //   （TextPlugin.DumpLine）—— 用户明确要求"这些信息不用暴露给用户"。
        //   宿主是 HanhuaPlugin 的 OnGUI。
        //   ★ 2026-10-23：HudLine() 已删除。它返回的 "文本层:开/关" 与总开关
        //     永远同步（两层由 F3 同生同死），摆在 HUD 上只会让人以为能分开调。
        //     文本层的真实状态仍然在 F11 dump 的 `开关=` 里（见 DumpLine）。
        //   ★ v1.0.0：本层不再有自己的 OnGUI 兜底条 —— 两层同一个插件，
        //     不再有"对方没装载"这种情况。

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
                    + "  crpl=" + crplCalls + "/" + crplHits
                    + "  imgui=" + imguiCalls + "/" + imguiHits + "(" + imguiPatched + ")"
                    + "  gui=" + guiCalls + "/" + guiHits + "(" + guiPatched + ")"
                    + "  CRPL字体=" + (cjkFontName.Length > 0 ? cjkFontName : "(无)")
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
