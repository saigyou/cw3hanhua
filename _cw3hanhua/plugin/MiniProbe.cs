// ============================================================================
//  CW3 最小探针 v2.0  —— 只为回答一个问题：
//      SteamManager.Awake 这次到底执行了没有？
//
//  为什么需要它：
//      故障时观测到「SteamManager.Awake 从未执行」→ SteamAPI.Init() 从未被调用
//      → MainMenu.Awake 里 FileManager.GetKey() 抛异常 → 主菜单卡死。
//      本插件把这个关键信号写成一行，方便每次启动后立刻核对。
//
//  ★ v2.0 修的是 v1.0 的【时序竞争】（2026-10-18 查实）：
//      v1.0 在插件 Start() 里打补丁。但 SteamManager 是第一个被创建的脚本，
//      它的 Awake 早已在插件 Start 之前执行完 → 补丁挂上时方法早就跑过了 →
//      永远记 0，即使游戏完全正常。v1.0 的产物里
//      「Awake 次数=0」与「s_EverInialized=True / m_bInitialized=True」并存，
//      就是这条假信号的铁证：那三个字段只有 Awake 会写（IL 实证）。
//      v2.0 改成 Harmony 的【静态补丁类】，在 BepInEx 的构建时补丁器
//      （Assembly-CSharp 加载时）里装补丁 → 早于任何 Awake 执行。
//
//  ★ 新增【交叉验证】：判「故障」不再只看计数，而是三路互证，
//      并把矛盾如实报出来（探针失效 ≠ 游戏故障）：
//        1) 补丁计数（必须是 0 才有资格谈「没执行」）
//        2) s_EverInialized  —— Awake 成功后置 True 的粘性标志
//        3) m_bInitialized   —— SteamAPI.Init() 的返回值
//      只要 2) 或 3) 为真而 1) 为 0 → 判「补丁没挂上/探针失效」，不判故障。
//
//  为什么不打「二次初始化」风险：
//      Awake 在 s_EverInialized=True 时会 throw（IL 0x0036），任何 late hook 都碰不得。
//      但 get_Instance 是懒加载工厂，读它就安全。
//
//  ⚠ 仍然刻意保持极简（避免插件本身成为变量）：
//      - 只打 SteamManager.Awake 的 3 个补丁 + MainMenu.Awake 的 1 个
//      - 不打 Debug.Log 转发、不打 set_enabled / get_enabled 探针
//      - 不画 HUD、不抢按键、不修改任何游戏状态
//
//  产物：BepInEx/CW3Mini.txt（每行带帧号）
//  目标：.NET 3.5 / C# 3.0（csc 3.5）
// ============================================================================

using System;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace CW3Hanhua
{
    // ========================================================================
    //  ★ 静态补丁类：由 BepInEx 的 harmony 补丁器在【加载 Assembly-CSharp 时】
    //    自动发现并安装 —— 早于 SteamManager.Awake 执行，没有时序竞争。
    //
    //    注意：Harmony 的静态补丁类只会被 BepInEx 的 patcher（HarmonyLib 集成）
    //    自动扫描；如果本 DLL 作为普通插件加载，下面的 InstallRuntimeFallback()
    //    会手动补装一次，两条路都留，互为兜底。
    //
    //    ⚠ 必须用【字符串】形式的 HarmonyPatch（typeof(T) 不行）：
    //      CW3 的 SteamManager / MainMenu 是 internal 类，泛型约束要求 public 才能编译。
    //      字符串形式在运行时按名字解析，不受可见性限制。
    // ========================================================================
    [HarmonyPatch("SteamManager", "Awake")]
    internal static class SteamManagerAwakeProbe
    {
        static void Prefix() { MiniProbe.ProbeAwakeEnter(); }
        static void Postfix() { MiniProbe.ProbeAwakeExit(true); }
        static Exception Finalizer(Exception __exception) { return MiniProbe.ProbeAwakeFinisher(__exception); }
    }

    [HarmonyPatch("MainMenu", "Awake")]
    internal static class MainMenuAwakeProbe
    {
        static void Prefix() { MiniProbe.ProbeMainMenuEnter(); }
        static void Postfix() { MiniProbe.ProbeMainMenuExit(true); }
        static Exception Finalizer(Exception __exception) { return MiniProbe.ProbeMainMenuFinisher(__exception); }
    }

    [BepInPlugin("cw3hanhua.mini", "CW3 Mini Probe", "2.3.0")]
    public class MiniProbe : BaseUnityPlugin
    {
        const string Ver = "2.3.0";

        static MiniProbe inst;
        static Harmony harmony;
        static string reportPath;
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        static readonly object FileLock = new object();

        int frame;

        // SteamManager 状态
        static Type steamType;
        static FieldInfo fInst, fEver, fInit;
        static int awakeCalls, awakeFinCalls, onEnableCalls;
        static bool awakeReturned;
        static string awakeResult = "(未触发)";
        static int samples;

        // 补丁装没装上的证据（v2.0 新增，v2.1 起用 __HarmonyPatches 直接自检）
        static string patchInstallMode = "(未知)";
        static string staticPatchVerdict = "(未自检)";
        static bool staticPatchOnSteamAwake;
        static bool staticPatchOnMainMenuAwake;

        // MainMenu 状态
        //   v2.2：MainMenu.Awake 的计数同样不可信（主菜单场景加载远晚于插件 Start，
        //   但静态补丁类没生效，所以计数必然是 0）。改为【读 UIFont 子对象】判断：
        //   MainMenu.Awake 第 0x0091 行才去 UberFind("UI Root (2D)","GameVersion")，
        //   若该子对象存在，就证明 MainMenu.Awake 至少执行过了那一步。
        static MainMenu mm;
        static string mmLast = null;
        static int mainMenuAwakeCalls, mainMenuAwakeFails;
        static string mainMenuAwakeError = "";
        static string mainMenuUIFontVerdict = "(未检查)";

        static void Say(string s)
        {
            try { if (inst != null) inst.Logger.LogInfo(s); } catch (Exception) { }
            try
            {
                if (reportPath != null)
                {
                    lock (FileLock) File.AppendAllText(reportPath, s + "\r\n", Utf8NoBom);
                }
            }
            catch (Exception) { }
        }

        static void SayF(string s) { Say("[" + (inst != null ? inst.frame : 0) + "f] " + s); }

        void Awake()
        {
            inst = this;
            try
            {
                reportPath = System.IO.Path.Combine(Paths.BepInExRootPath, "CW3Mini.txt");
                if (File.Exists(reportPath)) File.Delete(reportPath);
            }
            catch (Exception) { reportPath = null; }

            Say("==============================================================");
            Say(" CW3 最小探针 v" + Ver + " —— 只看 SteamManager.Awake 是否执行");
            Say("==============================================================");
            Say(" 报告: " + reportPath);
            Say(" 说明: v2.0 用【静态补丁类】在 Assembly-CSharp 加载时装补丁（早于所有 Awake），");
            Say("       并把「补丁计数」与「s_EverInialized / m_bInitialized」三路交叉验证。");
            Say("       v2.1 增加【补丁存在性自检】：直接查 Harmony 的最终方法上有没有我们的补丁，");
            Say("       不再靠「计数为 0」猜补丁装没装上。");
            Say("");
        }

        void Start()
        {
            try
            {
                harmony = new Harmony("cw3hanhua.mini");
                ResolveFields();
                VerifyStaticPatchClasses();
                InstallRuntimeFallback();
                InstallOnEnableProbe();
            }
            catch (Exception e) { Say("!! 安装失败: " + e.Message); }
            Say("");
            Say("===== 采样开始（补丁安装模式: " + patchInstallMode + "）=====");
            Say("");
        }

        static void ResolveFields()
        {
            steamType = AccessTools.TypeByName("SteamManager");
            if (steamType == null) { Say("  !! 找不到 SteamManager 类型"); return; }
            fInst = AccessTools.Field(steamType, "s_instance");
            fEver = AccessTools.Field(steamType, "s_EverInialized");
            fInit = AccessTools.Field(steamType, "m_bInitialized");
            Say("  字段: s_instance=" + (fInst != null)
                + " s_EverInialized=" + (fEver != null)
                + " m_bInitialized=" + (fInit != null));
        }

        /// <summary>
        /// ★ v2.1：直接问 Harmony「SteamManager.Awake 上到底有没有我们的补丁」。
        ///
        /// 原理：Harmony.Patch 之后，HarmonyX 会把补丁信息挂在方法的
        ///   __HarmonyPatches 字段上（`AccessTools.Field(method, "__HarmonyPatches")`），
        /// 里面是 PatchClassProcessor / GetOriginalMethod 等。哪怕方法已经被 detour，
        /// 从这个字段都能读出「谁给我打了什么补丁」。
        ///
        /// 这比 v2.0 的「计数为 0 就猜补丁没装上」可靠得多：现在能区分
        ///   a) 补丁根本没装上   b) 补丁装上了但 Awake 在安装前就跑完了   c) 补丁装上了且拦到了
        /// </summary>
        static void VerifyStaticPatchClasses()
        {
            try
            {
                MethodInfo aw = AccessTools.Method(steamType, "Awake", null, null);
                if (aw == null) { Say("  !! 找不到 SteamManager.Awake，无法做补丁自检"); return; }

                staticPatchOnSteamAwake = HasOurPatch(aw, "SteamManager.Awake");

                Type mmType = AccessTools.TypeByName("MainMenu");
                if (mmType != null)
                {
                    MethodInfo mw = AccessTools.Method(mmType, "Awake", null, null);
                    if (mw != null) staticPatchOnMainMenuAwake = HasOurPatch(mw, "MainMenu.Awake");
                }

                staticPatchVerdict = (staticPatchOnSteamAwake || staticPatchOnMainMenuAwake)
                    ? "静态补丁类已生效"
                    : "静态补丁类未生效（两个 Awake 上都没有本探针的补丁）";
                Say("  补丁自检 = " + staticPatchVerdict);
                if (!staticPatchOnSteamAwake)
                {
                    Say("             SteamManager.Awake 上没有任何补丁 —— BepInEx 的 Chainloader 在");
                    Say("             SteamManager.Awake 内部才触发，补丁永远赶不上它。");
                }
            }
            catch (Exception e) { Say("  !! 补丁自检异常: " + e.Message); }
        }

        /// <summary>读方法上的 __HarmonyPatches，判断有没有本探针的补丁。</summary>
        static bool HasOurPatch(MethodInfo m, string label)
        {
            try
            {
                // AccessTools.Field 只接受 Type；读【方法】的静态字段得用裸反射
                FieldInfo f = m.DeclaringType.GetField("__HarmonyPatches",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                System.Collections.IList list = (f == null) ? null : f.GetValue(null) as System.Collections.IList;
                if (list == null || list.Count == 0)
                {
                    Say("    [" + label + "] __HarmonyPatches = 空");
                    return false;
                }

                int mine = 0;
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < list.Count; i++)
                {
                    object owner = list[i];
                    string desc = (owner == null) ? "(null)" : owner.GetType().FullName;
                    if (desc.IndexOf("Probe") >= 0 || desc.IndexOf("MiniProbe") >= 0) mine++;
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(desc);
                }
                Say("    [" + label + "] __HarmonyPatches = " + list.Count + " 项: " + sb.ToString());
                return mine > 0;
            }
            catch (Exception e)
            {
                Say("    [" + label + "] 自检异常: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 运行时兜底安装。⚠ 只有「Awake 还没跑过」时才安全：
        /// Awake 一旦执行过，再补打补丁也只会让计数看着像 0（v1.0 的坑），
        /// 所以这里先看 s_EverInialized，看到 True 就明确记录「晚装，计数无意义」。
        /// </summary>
        static void InstallRuntimeFallback()
        {
            bool everDone = ReadEverBool();
            bool instExists = ReadInstance() != null;
            Say("  兜底安装前检查: s_EverInialized=" + everDone + "  s_instance=" + (instExists ? "非空" : "null"));
            if (everDone || instExists)
            {
                patchInstallMode = "晚装（Awake 已执行过，计数无意义）";
                Say("  ⚠ 补丁在 Awake 之后才准备安装 —— 计数将永远为 0，本文件的计数列不可信。");
                return;
            }

            try
            {
                MethodInfo aw = AccessTools.Method(steamType, "Awake", null, null);
                harmony.Patch(aw,
                    new HarmonyMethod(AccessTools.Method(typeof(MiniProbe), "ProbeAwakeEnter", null, null)),
                    new HarmonyMethod(AccessTools.Method(typeof(MiniProbe), "ProbeAwakeExit", null, null)),
                    null,
                    new HarmonyMethod(AccessTools.Method(typeof(MiniProbe), "ProbeAwakeFinisher", null, null)),
                    null);
                patchInstallMode = "运行时兜底（Awake 尚未执行）";
                Say("  ★ 已补装: SteamManager.Awake [pre][post][finalizer]");
            }
            catch (Exception e)
            {
                patchInstallMode = "安装失败";
                Say("  !! 补装 SteamManager.Awake 失败: " + e.Message);
            }
        }


        static void InstallOnEnableProbe()
        {
            // OnEnable 在 IL 里没有调用者 → Unity 消息调用，补丁安全（不会二次初始化）
            try
            {
                MethodInfo oe = AccessTools.Method(steamType, "OnEnable", null, null);
                if (oe != null)
                {
                    harmony.Patch(oe,
                        new HarmonyMethod(AccessTools.Method(typeof(MiniProbe), "ProbeOnEnable", null, null)),
                        null, null, null, null);
                    Say("  ★ 已装补丁: SteamManager.OnEnable [pre]");
                }
            }
            catch (Exception e) { Say("  !! OnEnable 补丁失败: " + e.Message); }
        }

        // ------------------------------------------------------------ 补丁体
        // 这些方法名被静态补丁类和运行时兜底共用 —— 一个实现，两条安装路径。

        public static void ProbeAwakeEnter()
        {
            try
            {
                awakeCalls++;
                if (awakeCalls <= 3)
                    SayF("★★★★ SteamManager.Awake 【进入了】（第 " + awakeCalls + " 次）");
            }
            catch (Exception) { }
        }

        public static void ProbeAwakeExit(bool ignored)
        {
            try
            {
                awakeReturned = true;
                SayF("★★★★ SteamManager.Awake 【正常返回】");
                Say("        m_bInitialized = " + ReadInit() + "   s_EverInialized = " + ReadEver());
            }
            catch (Exception) { }
        }

        public static Exception ProbeAwakeFinisher(Exception __exception)
        {
            try
            {
                awakeFinCalls++;
                if (__exception == null)
                {
                    awakeResult = "正常返回";
                    SayF("★★★★ finalizer: Awake 正常结束（无异常）");
                }
                else
                {
                    awakeResult = "抛出 " + __exception.GetType().Name + ": " + __exception.Message;
                    SayF("★★★★ finalizer: Awake 抛出异常");
                    Say("        类型: " + __exception.GetType().FullName);
                    Say("        消息: " + __exception.Message);
                }
                Say("        m_bInitialized = " + ReadInit() + "   s_EverInialized = " + ReadEver());
            }
            catch (Exception) { }
            return __exception;
        }

        public static void ProbeOnEnable()
        {
            try
            {
                onEnableCalls++;
                if (onEnableCalls == 1) SayF("★ SteamManager.OnEnable 进入了");
            }
            catch (Exception) { }
        }

        public static void ProbeMainMenuEnter()
        {
            try
            {
                mainMenuAwakeCalls++;
                if (mainMenuAwakeCalls == 1) SayF("◆ MainMenu.Awake 进入了");
            }
            catch (Exception) { }
        }

        public static void ProbeMainMenuExit(bool ignored)
        {
            try { SayF("◆ MainMenu.Awake 正常返回（主菜单逻辑完整执行）"); }
            catch (Exception) { }
        }

        public static Exception ProbeMainMenuFinisher(Exception __exception)
        {
            try
            {
                if (__exception != null)
                {
                    mainMenuAwakeFails++;
                    mainMenuAwakeError = __exception.GetType().Name + ": " + __exception.Message;
                    SayF("◆◆ MainMenu.Awake 抛出异常 —— 主菜单会卡死");
                    Say("        " + mainMenuAwakeError);
                }
            }
            catch (Exception) { }
            return __exception;
        }

        // ------------------------------------------------------------ 字段读取
        static object ReadInstance()
        {
            try { return fInst == null ? null : fInst.GetValue(null); }
            catch (Exception) { return null; }
        }

        static bool ReadEverBool()
        {
            try { object v = fEver == null ? null : fEver.GetValue(null); return v is bool && (bool)v; }
            catch (Exception) { return false; }
        }

        static string ReadInit()
        {
            try
            {
                object i0 = ReadInstance();
                if (i0 == null) return "(s_instance=null)";
                object v = (fInit == null) ? null : fInit.GetValue(i0);
                return v == null ? "(读失败)" : ("" + v);
            }
            catch (Exception e) { return "(读失败:" + e.Message + ")"; }
        }

        static bool ReadInitBool()
        {
            try
            {
                object i0 = ReadInstance();
                if (i0 == null || fInit == null) return false;
                object v = fInit.GetValue(i0);
                return v is bool && (bool)v;
            }
            catch (Exception) { return false; }
        }

        static string ReadEver()
        {
            try { return fEver == null ? "(未解析)" : ("" + fEver.GetValue(null)); }
            catch (Exception) { return "(读失败)"; }
        }

        // ------------------------------------------------------------ 采样
        void Update()
        {
            frame++;
            // 第 150 / 300 / 600 / 1200 帧各打一次快照
            if (frame != 150 && frame != 300 && frame != 600 && frame != 1200) return;
            Sample();
        }

        /// <summary>
        /// ★ 核心判定：多路交叉验证，**不再靠猜**。
        ///
        /// v2.1 起多了 staticPatchVerdict（补丁存在性自检），所以能区分四种情况：
        ///   1. 计数 &gt; 0                → 补丁拦到了，Awake 确实执行 → 正常
        ///   2. 补丁在 + 计数 0 + 标志真 → 补丁装上了但 Awake 在安装前就跑完了 → 探针失效（不判故障）
        ///   3. 补丁不在 + 计数 0 + 标志真 → 补丁压根没装上 → 探针失效（不判故障）
        ///   4. 计数 0 + 两个标志全假    → 无论补丁在不在，Awake 都没写任何状态 → 真故障
        /// 唯一的例外是「晚装」：那时计数永远是 0，且 Awake 一定跑过，只能判探针失效。
        /// </summary>
        static string Judge()
        {
            bool ever = ReadEverBool();
            bool init = ReadInitBool();

            if (awakeCalls > 0)
                return awakeReturned
                    ? "已执行且正常返回（补丁拦到了 Awake）"
                    : "进入但未返回";

            // 计数为 0：先判断这个 0 可不可信
            if (patchInstallMode.IndexOf("晚装") >= 0)
                return "★ 探针失效：补丁晚装，计数无意义（不等于故障）";

            if (ever || init)
            {
                bool patchOnMethod = staticPatchVerdict.IndexOf("已生效") >= 0;
                return patchOnMethod
                    ? "★ 探针失效：补丁【在】Awake 方法上，但计数为 0 → Awake 在补丁安装前就跑完了"
                      + "（s_EverInialized/m_bInitialized 为真，确认已执行，不判故障）"
                    : "★ 探针失效：补丁【不在】Awake 方法上，计数无意义 → 静态补丁类没生效"
                      + "（s_EverInialized/m_bInitialized 为真，确认 Awake 已执行，不判故障）";
            }

            return "★ 故障特征：计数 0 且两个标志均为假 → Awake 确实没执行";
        }

        void Sample()
        {
            samples++;
            try
            {
                SayF("---- 快照 #" + samples + "（第 " + frame + " 帧）----");
                Say("        SteamManager.Awake 次数 = " + awakeCalls
                    + "  OnEnable 次数 = " + onEnableCalls
                    + "  finalizer 次数 = " + awakeFinCalls);
                Say("        补丁安装模式 = " + patchInstallMode);
                Say("        补丁自检 = " + staticPatchVerdict);
                Say("        Awake 结论 = " + Judge());
                Say("        s_instance = " + (ReadInstance() == null ? "null" : "非空")
                    + "   s_EverInialized = " + ReadEver()
                    + "   m_bInitialized = " + ReadInit());
                Say("        MainMenu.Awake 次数 = " + mainMenuAwakeCalls
                    + "  异常次数 = " + mainMenuAwakeFails
                    + (mainMenuAwakeError.Length > 0 ? ("  最后异常=" + mainMenuAwakeError) : ""));

                try
                {
                    Type sap = AccessTools.TypeByName("Steamworks.SteamAPI");
                    if (sap != null)
                    {
                        MethodInfo m = AccessTools.Method(sap, "IsSteamRunning", new Type[0], null);
                        if (m != null) Say("        SteamAPI.IsSteamRunning() = " + m.Invoke(null, null));
                    }
                }
                catch (Exception) { }

                // 场景与 MainMenu 状态
                Say("        场景 = " + Application.loadedLevelName);
                if (mm == null)
                {
                    UnityEngine.Object[] arr = Resources.FindObjectsOfTypeAll(typeof(MainMenu));
                    if (arr != null && arr.Length > 0) mm = arr[0] as MainMenu;
                }
                if (mm != null)
                {
                    string st = mm.enabled + "|" + mm.gameObject.activeSelf;
                    Say("        MainMenu enabled=" + mm.enabled
                        + " activeSelf=" + mm.gameObject.activeSelf
                        + " activeInHierarchy=" + mm.gameObject.activeInHierarchy
                        + (mmLast != null && mmLast != st ? "   ★★ 状态变化 " + mmLast + " -> " + st : ""));
                    if (mmLast != st) mmLast = st;
                }
                else Say("        MainMenu = (未找到)");

                // ★ v2.2 独立证据：MainMenu.Awake 是否至少跑到了 UberFind 那一步。
                //   IL 0x0091: ldstr 'UI Root (2D)' / ldstr 'GameVersion' / UberFind.Find
                //   ⚠ UberFind.Find(root, child) 是【先 GameObject.Find(root) 全局找根，
                //     再在根下 GetComponentsInChildren 按名字找子】—— 不是 MainMenu 的直接子对象！
                //     （反编译 UberFind.Find 实证；v2.2 首版用 mm.transform.Find 找错了方向）
                //   因为用的是全局查找，所以这里【不依赖 mm 引用】。
                try
                {
                    GameObject root = GameObject.Find("UI Root (2D)");
                    Transform gv = null;
                    if (root != null)
                    {
                        Transform[] allTr = root.GetComponentsInChildren<Transform>(true);
                        for (int k = 0; k < allTr.Length; k++)
                        {
                            if (allTr[k] != null && allTr[k].name == "GameVersion") { gv = allTr[k]; break; }
                        }
                    }
                    if (gv != null)
                        mainMenuUIFontVerdict = "找到 'UI Root (2D)/…/GameVersion' → MainMenu.Awake 已执行到 UberFind 段"
                            + "（= Awake 确实跑过，计数 0 是补丁没拦到）";
                    else if (root == null)
                        mainMenuUIFontVerdict = "找不到 'UI Root (2D)' 根对象（可能不在主菜单场景）";
                    else
                        mainMenuUIFontVerdict = "'UI Root (2D)' 存在但里面没有 GameVersion";
                }
                catch (Exception e) { mainMenuUIFontVerdict = "检查异常: " + e.Message; }
                Say("        MainMenu.Awake 旁证 = " + mainMenuUIFontVerdict);
                Say("");
            }
            catch (Exception e) { SayF("!! 采样异常: " + e.Message); }
        }

        void OnApplicationQuit()
        {
            Say("==============================================================");
            Say(" 退出。存活帧数 = " + frame);
            Say(" SteamManager.Awake 次数 = " + awakeCalls
                + ", OnEnable = " + onEnableCalls + ", finalizer = " + awakeFinCalls);
            Say(" 补丁安装模式 = " + patchInstallMode);
            Say(" 补丁自检 = " + staticPatchVerdict);
            Say(" Awake 结果 = " + awakeResult);
            Say(" s_EverInialized = " + ReadEver() + "   m_bInitialized = " + ReadInit());
            Say(" MainMenu.Awake 次数 = " + mainMenuAwakeCalls + "  异常次数 = " + mainMenuAwakeFails);
            Say(" ★ 最终判定: " + Judge());
            Say("==============================================================");
        }
    }
}
