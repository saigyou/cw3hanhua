// ============================================================================
//  CW3 汉化插件 v1.3.2 —— 合并图集字体层（NOTES 4.4）+ 文本层（词表）+ 合并 HUD
//
//  ★ 2026-10-23：两个 DLL 合成一个。本文件与 CW3HanhuaText.cs 一起编进
//    **CW3Hanhua.dll**，两层之间不再有任何反射调用 —— 字体层直接
//    `TextPlugin.SetOn(..)` / `TextPlugin.ErrorLine()` / `TextPlugin.DumpLine()`。
//    BepInEx\plugins\CW3HanhuaText.dll 已由 build_plugin.cmd 自动删除。旧的
//    CW3HanhuaText.dll 是个**独立的插件**（GUID cw3hanhua.text），留着会在同一批
//    游戏方法上再装一遍 Harmony 补丁、再抢一遍 F5/F9、再画一条 HUD。
//    两个 .cs 各自保留一个类（HanhuaPlugin / TextPlugin），一层一文件，
//    出问题照样能分层看日志 —— 但它们在同一个程序集里，编译期就认识对方。
//
//  方案：不拆 draw call。
//    把中文字形并进游戏原有的 GUI Atlas（512x1024 → 4096x8192），
//    文字的 UIFont.mAtlas 保持指向 GUI Atlas 不动，于是 mAtlas.spriteMaterial
//    与精灵共用同一份材质 → 文字与背景天然落在同一个 draw call 里，
//    遮挡关系自动恢复原版（这正是拆 draw call 那一派做法失败的地方，见 3.8）。
//
//  坐标系（★ 千万别改，踩过四轮坑）：
//    sprite.outer 与 BMFont glyph.y 都是【顶左原点】（从顶部往下量）。
//    所以原图 1:1 贴 PIL (0,0)，中文字形从 y=1025 起排，101 个精灵的 UV 一个都不用改。
//    font_XX.png 是灰度图（白字黑底），必须转成「RGB=255 + alpha=覆盖率」，
//    直接 convert("RGBA") 会得到不透明黑块。
//
//  数据来源（tools/build_atlas.py 产出）：
//    _cw3hanhua/_font/atlas_zh.png   4096x8192 合并图集
//    _cw3hanhua/_font/atlas_zh.bin   字形表（插件直读，手写二进制比 JSON 可靠）
//
//  按键（共 5 个，全部列在 HUD 上；HUD 只报开关状态，不报内部数字）：
//    ★ 2026-10-23：F3 与 F4 **合并成一个总开关**，F4 已退役（详见下方「开关」一节）。
//    F3    汉化总开关（字体层套合并图集 + 文本层接词表，一起开/关）
//    F5    打印未命中清单（缺哪些译文，文本层）
//    F9    重载词表（改完 zh_cn.dict 不用重启，文本层）
//    F11   dump → BepInEx/CW3HanhuaFont.txt（详细数字都在这里，不在 HUD 上）
//    F12   HUD 开关（**默认关闭**，v1.3.1 起；画在左上角）
//
//  ★ 2026-10-23（v1.3.1）：HUD 默认关闭；HUD 上**不再显示「文本层:开/关」** ——
//    两层由 F3 同生同死、状态永远一样，摆两个开关只会让人以为能分开调。
//
//  启动自动开启（v1.0.0）：**无条件自动** —— 没有任何配置项、没有开关。
//    Update 里等"图集文件在 + UIFont 已 Create"再自动把总开关拨到开，
//    没就绪就每 60 帧重试，直到真的换上为止（加载慢的机器也等得到）。
//
//  编译：tools\build_plugin.cmd（封装了 csc 3.5 /platform:x86 /codepage:65001 与全部引用）
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace CW3Hanhua
{
    [BepInPlugin("cw3hanhua.core", "CW3 Hanhua", "1.3.2")]
    public class HanhuaPlugin : BaseUnityPlugin
    {
        const string Ver = "1.3.2";

        static HanhuaPlugin inst;
        static string reportPath;
        static string fontDir;
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // ---------------------------------------------------------------- 开关
        // ★ 2026-10-23：F3 与 F4 **合并成一个总开关**（hanhuaOn）。
        //   本类（字体层）是主：它装着 F12 的 HUD 与 OnGUI，
        //   所以按键与自动开启都放在这里；文本层在同一个程序集里
        //   （TextPlugin，见 CW3HanhuaText.cs），直接调用，不走反射。
        static bool hanhuaOn = false;
        // ★ 2026-10-23：HUD **默认关闭**。版本已经收敛，正常玩的时候不该有一块
        //   常驻面板；需要看就按 F12（F12 仍是双向开关：开→关，关→开）。
        static bool showHud = false;

        // 自动开启（v1.0.0）：**无条件**，没有配置项。
        //   autoDone 只表示"自动开启这件事结案了"，不是用户开关：
        //   换上了字体（或用户先按了 F3）就置真，不再插手。
        static bool autoDone;
        static bool autoTrying;         // 自动开启已经拨过、但还没确认换上（用于重试）
        static int autoNextFrame;       // 失败退避：每 60 帧才重试一次
        static int autoTries;           // 已尝试次数（到 30 次就停手，别无限刷日志）
        static bool textSynced;         // 文本层已接受总开关状态

        // ---------------------------------------------------------------- 位图字体数据
        class FontData
        {
            public int size;
            public int texW;
            public int texH;
            public int ascent;
            public List<int[]> glyphs = new List<int[]>();
        }

        static readonly Dictionary<int, FontData> fontDataCache = new Dictionary<int, FontData>();
        static string loadError = "";

        // ---------------------------------------------------------------- 备份
        class FontBackup
        {
            public UIFont font;
            public UIAtlas atlas;
            public Material mat;
            public Rect uv;
            public Font dyn;
            public int dynSize;
            public FontStyle dynStyle;
            public BMFont bmFont;
        }

        static readonly List<FontBackup> fontBackups = new List<FontBackup>();

        // ---------------------------------------------------------------- 反射
        static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        static FieldInfo fAtlas, fMat, fUV, fDyn, fDynSize, fDynStyle, fBMFont;
        static FieldInfo fPMA, fSprite, fSpriteSet;

        // ---------------------------------------------------------------- 状态
        static int frame;
        static string sceneName = "";
        static string lastEvent = "(无)";
        static int materialFixCount;



        // ================================================================ 已废弃的方案（留作教训）
        //
        //  曾试过「按 UIPanel 克隆字形/精灵材质，再用 renderQueue 分层」，共四轮，全部失败：
        //    ① 固定 3001            → 悬停框浮在设置界面之上（原版是设置界面盖住悬停框）
        //    ② 按 widget.depth 分层  → 无效，跨面板顺序跟 widget.depth 无关
        //    ③ 按面板兄弟序号分层    → 悬停框被盖住了，但**连带破坏所有其它 UI 的遮挡**
        //    ④ 只给文字分层、背景不动 → 文字永远后画，悬停框照样浮在上面
        //
        //  根因：原版文字与背景**共用 GUI Atlas 材质 → 同一个 draw call**，
        //        遮挡由 mesh 内按 depth 排序的三角形决定。一拆材质就必然拆 draw call，
        //        任何 renderQueue 方案都是在错误的层面上打补丁。
        //
        //  最终解：把中文字形并进 GUI Atlas（见下方「合并图集」），不拆 draw call。


        // ================================================================ 工具
        /// <summary>写日志 + 追加到 F11 的报告文件。文本层（TextPlugin.Say）也走这里，
        /// 合并成一个 DLL 后两份日志自然汇到同一处。</summary>
        internal static void Say(string s)
        {
            try { if (inst != null) inst.Logger.LogInfo(s); } catch (Exception) { }
            try { if (reportPath != null) System.IO.File.AppendAllText(reportPath, s + "\r\n", Utf8NoBom); }
            catch (Exception) { }
        }

        static string Trunc(string s, int n)
        {
            if (s == null) return "(null)";
            s = s.Replace("\r", "\\r").Replace("\n", "\\n");
            return (s.Length <= n) ? s : (s.Substring(0, n) + "…");
        }

        static string PathOf(GameObject go)
        {
            if (go == null) return "(null)";
            StringBuilder sb = new StringBuilder(go.name);
            Transform t = go.transform.parent;
            int guard = 0;
            while (t != null && guard++ < 8) { sb.Insert(0, "/"); sb.Insert(0, t.name); t = t.parent; }
            return sb.ToString();
        }

        static string NameOf(UnityEngine.Object o)
        {
            try { return (o == null) ? "(null)" : o.name; } catch (Exception) { return "(?)"; }
        }

        static object GetFieldU(object o, FieldInfo f)
        {
            try { return (f != null && o != null) ? f.GetValue(o) : null; } catch (Exception) { return null; }
        }

        static void SetFieldU(object o, FieldInfo f, object v)
        {
            try { if (f != null && o != null) f.SetValue(o, v); } catch (Exception) { }
        }

        static bool SafeIsDynamic(UIFont f)
        {
            try { return f != null && f.isDynamic; } catch (Exception) { return true; }
        }

        static int SafeSize(UIFont f)
        {
            try { if (f != null) { int s = f.size; if (s > 0) return s; } } catch (Exception) { }
            return 16;
        }

        static void SafeMarkAsDirty(UIFont f)
        {
            try { if (f != null) f.MarkAsDirty(); } catch (Exception) { }
        }

        static string Ok(FieldInfo f) { return (f != null) ? "OK" : "缺失"; }

        // ================================================================ 生命周期
        void Awake()
        {
            inst = this;
            try
            {
                reportPath = System.IO.Path.Combine(Paths.BepInExRootPath, "CW3HanhuaFont.txt");
                if (System.IO.File.Exists(reportPath)) System.IO.File.Delete(reportPath);
            }
            catch (Exception) { reportPath = null; }

            try
            {
                fontDir = System.IO.Path.Combine(Paths.GameRootPath, "_cw3hanhua");
                fontDir = System.IO.Path.Combine(fontDir, "_font");
            }
            catch (Exception) { fontDir = null; }

            fAtlas = typeof(UIFont).GetField("mAtlas", Priv);
            fMat = typeof(UIFont).GetField("mMat", Priv);
            fUV = typeof(UIFont).GetField("mUVRect", Priv);
            fDyn = typeof(UIFont).GetField("mDynamicFont", Priv);
            fDynSize = typeof(UIFont).GetField("mDynamicFontSize", Priv);
            fDynStyle = typeof(UIFont).GetField("mDynamicFontStyle", Priv);
            fBMFont = typeof(UIFont).GetField("mFont", Priv);
            fPMA = typeof(UIFont).GetField("mPMA", Priv);
            fSprite = typeof(UIFont).GetField("mSprite", Priv);
            fSpriteSet = typeof(UIFont).GetField("mSpriteSet", Priv);

            Say("==============================================================");
            Say(" CW3 汉化插件 v" + Ver + "  合并图集字体层 + 文本层");
            Say("==============================================================");
            Say("报告: " + reportPath);
            Say("字体目录: " + fontDir);
            Say("反射: mAtlas=" + Ok(fAtlas) + " mMat=" + Ok(fMat) + " mUVRect=" + Ok(fUV)
                + " mDynamicFont=" + Ok(fDyn) + " mFont(BMFont)=" + Ok(fBMFont));
            Say("");
            Say("按键: F3 汉化总开关（管两层） | F5 未命中清单 | F9 重载词表");
            Say("      F11 dump | F12 HUD");
            Say("");
            Say("启动自动开启: 是（无配置项，等图集与 UIFont 就绪后自动套）");

            // ★ v1.0.0：文本层（同一个 DLL 里的 TextPlugin）在这里初始化一次。
            //   它老早是自己 Awake 的 —— 合并成一个插件后，由本类统一按序驱动：
            //   先装好字体层的东西，再装文本层的 Harmony 补丁。
            //   文本层的每帧工作走 TextPlugin.Tick()（见 Update）。
            try { TextPlugin.Init(); }
            catch (Exception e) { Say("!! 文本层初始化失败（字体层仍可用）: " + e); }
        }

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
                    PruneDead();
                    // 文本层也跟着清理（原先它自己 Update 里也做一份，合并后统一由这里通知）
                    TextPlugin.OnSceneChanged();
                    // 新场景可能带进新的 UIFont，总开关开着就顺手把它们也换掉
                    if (hanhuaOn) ApplyFont();
                }

                // 每 30 帧兜底：新激活的 label 可能还持有旧材质，重新绑一次。
                //
                // ★ 2026-10-18 第六轮：文字现在与背景**共用图集材质**，
                //   不再需要 FixRenderOrder（那套按面板分队列的方案已被证伪并删除）。
                if (fontBackups.Count > 0 && (frame % 30) == 0)
                {
                    RefreshAllLabelMaterials();
                }

                // 总开关开着、但文本层还没接上（启动早期它可能还没 Install 完）——
                // 每 60 帧补一次，接上就不再试。同一个程序集，直接调，不再反射。
                if (hanhuaOn && !textSynced && (frame % 60) == 0)
                {
                    textSynced = TextPlugin.SetOn(true);
                    if (!textSynced) Say("  (等文本层就绪：词表还没载入？)");
                }

                // 文本层的每帧工作（F5 / F9 按键、场景切换清理）
                TextPlugin.Tick();

                // 启动自动开启（无配置项，无条件）
                TryAutoEnable();

                // 按键：F3 总开关 / F11 dump / F12 HUD。F5 与 F9 在 TextPlugin.Tick 里。
                //   HUD 第一行会把 5 个键全列出来 —— 加/删按键时**两处都要改**。
                //   历史：F2 TextMesh / F6 关裁剪 / F7 F8 中文测试 / F10 全部还原 / F4 文本层，
                //   都随对应方案废弃或合并而删除，不要在 HUD 里"保留"它们。
                if (Input.GetKeyDown(KeyCode.F3)) ToggleHanhua();
                if (Input.GetKeyDown(KeyCode.F11)) DumpReport();
                if (Input.GetKeyDown(KeyCode.F12)) { showHud = !showHud; }
            }
            catch (Exception e) { Say("!! Update 异常: " + e.Message); }
        }

        // ================================================================ 自动开启
        // 为什么不能"Awake 里直接开"：Awake 跑得太早，Resources.FindObjectsOfTypeAll
        // 还拿不到 GUI Atlas 与各 UIFont（它们随场景/预制体在之后才 Create），
        // 那时 ApplyFont 会扫到 0 个对象、静悄悄什么也不做，而且看起来"已经开了"。
        // 所以改成在 Update 里**等就绪**：找到 UIFont 且图集文件在，才真正拨开关。
        // 失败按 60 帧退避重试，不刷日志、不放弃 —— 有的机器加载慢。
        /// <summary>自动开启的就绪门槛：至少有一个 UIFont 能换，且合并图集文件在。</summary>
        static bool AutoReady()
        {
            try
            {
                if (string.IsNullOrEmpty(fontDir)) return false;
                if (!System.IO.File.Exists(System.IO.Path.Combine(fontDir, "atlas_zh.png"))) return false;
                if (!System.IO.File.Exists(System.IO.Path.Combine(fontDir, "atlas_zh.bin"))) return false;
                UnityEngine.Object[] fonts = Resources.FindObjectsOfTypeAll(typeof(UIFont));
                return fonts != null && fonts.Length > 0;
            }
            catch (Exception) { return false; }
        }

        void TryAutoEnable()
        {
            if (autoDone) return;
            // 开关已经开着、但不是自动拨的 —— 那是用户自己按的，交给他，不要抢。
            // （autoTrying 就是为了区分这两种"开着"：没它就分不清，见下面 else 分支。）
            if (hanhuaOn && !autoTrying) { autoDone = true; return; }
            if (frame < autoNextFrame) return;
            autoNextFrame = frame + 60;
            if (!AutoReady()) return;

            if (autoTries >= 30)
            {
                autoDone = true;
                Say("!! 自动开启试了 30 次仍未成功（最近一次没换到 UIFont）—— 放弃。");
                Say("   请按 F11 看 dump 里的图集报错；手动按 F3 仍可再试。");
                return;
            }
            autoTries++;

            Say("");
            Say("★ 自动开启：UIFont 与图集已就绪（第 " + frame + " 帧，第 " + autoTries + " 次尝试），拨开总开关");
            autoTrying = true;
            SetHanhua(true, false);

            if (fontBackups.Count > 0)
            {
                autoDone = true;                 // 真的换上了才算结案
                Say("  已换 " + fontBackups.Count + " 个 UIFont；文本层=" + (textSynced ? "已接上" : "等待装载"));
            }
            else
            {
                Say("  !! 没换到任何 UIFont，60 帧后再试（见上方 ApplyFont 报错）");
            }
        }

        // ================================================================ 位图字体装载
        // ※ v0.7.0 的独立字体纹理方案（LoadFontData 读 font_XX.fnt / LoadFontTexture
        //   读 font_XX.png）已于 v0.8.0 合并图集方案中废弃，代码已删除。
        //   现在字形表只从 atlas_zh.bin 读（见 LoadMergedFontData）。

        static BMFont BuildBMFont(FontData fd)
        {
            BMFont bm = new BMFont();
            bm.charSize = fd.size;
            bm.texWidth = fd.texW;
            bm.texHeight = fd.texH;
            bm.baseOffset = fd.ascent;
            for (int i = 0; i < fd.glyphs.Count; i++)
            {
                int[] g = fd.glyphs[i];
                BMGlyph gl = bm.GetGlyph(g[0], true);
                gl.x = g[1];
                gl.y = g[2];
                gl.width = g[3];
                gl.height = g[4];
                gl.offsetX = g[5];
                gl.offsetY = g[6];
                gl.advance = g[7];
                gl.channel = 15;      // 15 = 普通 alpha（非 packed 字体）
            }
            return bm;
        }

        // ================================================================ 合并图集
        //
        // ★ 方案（2026-10-18 第六轮）：
        //   原版 GUI Atlas 只有 512x1024，放不下中文。离线用 tools/build_atlas.py
        //   生成 4096x8192 的 atlas_zh.png：左上角是原图集 1:1（101 个精灵的
        //   outer/inner 一个都不用改），下方是三套字号的中文字形。
        //   运行时把 UIAtlas 的纹理换成它 → 文字与背景共用同一个材质
        //   → 同一个 draw call → 遮挡关系恢复原版。
        //
        //   注意：**不改 spriteList**、**不改 renderQueue**、**不新建材质**。
        static bool mergedAtlasApplied;
        static string mergedAtlasError = "";
        static Texture2D mergedTex;
        // 替换前的原始纹理与材质参数，F3 关闭时要还原，否则整个 UI 的 UV 全错
        static Texture origAtlasTex;
        static Vector2 origTexScale = Vector2.one;
        static Vector2 origTexOffset = Vector2.zero;

        /// <summary>
        /// 让所有 UISprite 重新按【当前纹理尺寸】计算 UV。
        ///
        /// 为什么必须手动做：UISprite.UpdateUVs 把换算结果缓存在 mOuterUV/mInnerUV，
        /// 换纹理时不会自动失效。旧的 UV（按 512x1024 算）套到 4096x8192 上，
        /// 精灵会采样到错位区域（表现为"背景框整个不见了"）。
        ///
        /// UpdateUVs 是 protected virtual，只能反射调用。
        /// </summary>
        static int RefreshAllSpriteUVs()
        {
            int n = 0;
            try
            {
                MethodInfo m = typeof(UISprite).GetMethod("UpdateUVs",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m == null) { Say("  !! 找不到 UISprite.UpdateUVs"); return 0; }
                object[] args = new object[] { true };
                UnityEngine.Object[] objs = Resources.FindObjectsOfTypeAll(typeof(UISprite));
                for (int i = 0; i < objs.Length; i++)
                {
                    UISprite s = objs[i] as UISprite;
                    if (s == null) continue;
                    try
                    {
                        m.Invoke(s, args);
                        n++;
                    }
                    catch (Exception) { continue; }

                    // ★★ 关键：UpdateUVs **只改 mOuterUV/mInnerUV 字段**，
                    //    不会置 mChanged。而 UIWidget.UpdateGeometry 只有
                    //    `if (mChanged) { mGeom.Clear(); OnFill(...); }` —— 不置位，
                    //    网格里永远是旧 UV（按 512x1024 算的），精灵就采样到错位区域。
                    //    这是 2026-10-18 第二版实测「背景错位/撕裂」的直接原因。
                    try { s.MarkAsChanged(); } catch (Exception) { }
                }
            }
            catch (Exception e) { Say("  !! 重算精灵 UV 异常: " + e.Message); }
            return n;
        }

        /// <summary>把 GUI Atlas 的纹理换成合并图集（只做一次）。</summary>
        static bool EnsureMergedAtlas()
        {
            if (mergedAtlasApplied) return true;
            try
            {
                UnityEngine.Object[] objs = Resources.FindObjectsOfTypeAll(typeof(UIAtlas));
                UIAtlas atlas = null;
                for (int i = 0; i < objs.Length; i++)
                {
                    UIAtlas a = objs[i] as UIAtlas;
                    if (a == null) continue;
                    if (a.name == "GUI Atlas") { atlas = a; break; }
                    if (atlas == null) atlas = a;
                }
                if (atlas == null) { mergedAtlasError = "找不到 UIAtlas"; return false; }

                string png = System.IO.Path.Combine(fontDir, "atlas_zh.png");
                if (!System.IO.File.Exists(png))
                {
                    mergedAtlasError = "缺 " + png + "（先跑 tools/build_atlas.py）";
                    Say("!! " + mergedAtlasError);
                    return false;
                }

                byte[] data = System.IO.File.ReadAllBytes(png);
                // ★ 直接把解码结果当作正式纹理 —— 不要「LoadImage 到 tmp 再 SetPixels32 复制」。
                //   LoadImage 出来的 RGB24 只是 4x4 占位，解码后它会自己扩到图片尺寸；
                //   但 4096x8192 RGBA32 = 128MB，GetPixels32 会再复制一份 128MB，
                //   32 位进程里这个瞬时峰值很容易 OOM。LoadImage 直接得到 RGBA32 就够用。
                mergedTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                if (!mergedTex.LoadImage(data))
                {
                    mergedAtlasError = "LoadImage 失败：atlas_zh.png";
                    Say("!! " + mergedAtlasError);
                    return false;
                }
                mergedTex.name = "GUI Atlas (zh)";
                try { mergedTex.filterMode = FilterMode.Bilinear; } catch (Exception) { }
                try { mergedTex.wrapMode = TextureWrapMode.Clamp; } catch (Exception) { }

                Material sm = null;
                try { sm = atlas.spriteMaterial; } catch (Exception) { }
                if (sm == null) { mergedAtlasError = "UIAtlas.spriteMaterial 为空"; return false; }

                // 记下原始状态，F3 关闭时还原
                origAtlasTex = sm.mainTexture;
                origTexScale = sm.mainTextureScale;
                origTexOffset = sm.mainTextureOffset;

                // ★ 只换 mainTexture，**不动** mainTextureScale / mainTextureOffset。
                //   原因：① 原值未知，原版材质可能不是 (1,1)/(0,0)；
                //         ② UIDrawCall.OnWillRenderObject() 每帧给 mClippedMat 写
                //            mainTextureOffset/mainTextureScale（裁剪用），共享材质的 ST
                //            一动就会和裁剪副本打架；
                //         ③ 精灵 UV 走 mOuterUV（已归一化到 0..1），与 ST 无关。
                sm.mainTexture = mergedTex;
                mergedAtlasApplied = true;

                // ★ 必须强制重算所有精灵的 UV。
                //   UISprite.UpdateUVs 里 mOuterUV = ConvertToTexCoords(mOuter, texture.width, texture.height)
                //   是**缓存在 mOuterUV 字段里**的：换纹理不会自动重算，
                //   旧的 UV（按 512x1024 算的）会把精灵采样到完全错误的位置
                //   —— 这是 2026-10-18 第一版实测「文字和背景一起消失」的第二个原因。
                int nSpr = RefreshAllSpriteUVs();

                Say("  [合并图集] GUI Atlas 纹理 " + mergedTex.width + "x" + mergedTex.height
                    + " 已替换（原图集在左上角 1:1，spriteList 未改动，重算 " + nSpr + " 个精灵 UV）");
                return true;
            }
            catch (Exception e)
            {
                mergedAtlasError = e.Message;
                Say("!! 合并图集失败: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 读 atlas_zh.bin，取出某个字号的字形表，转成 FontData。
        ///
        /// 二进制格式（build_atlas.py 产出，小端）：
        ///   "CWA1" | int32 W | int32 H | int32 nSizes
        ///   每字号: int32 size | int32 cell | int32 count
        ///           count × (code,x,y,w,h,offX,offY,adv) 各 int32
        ///   uint32 FNV-1a
        ///
        /// 为什么用 bin 而不是直接读 atlas_zh.json：插件是 C# 3.5，
        /// 手写 JSON 解析容易在转义/大文件上出错；顺序读二进制零歧义。
        /// </summary>
        static FontData LoadMergedFontData(int size)
        {
            FontData fd;
            if (fontDataCache.TryGetValue(size, out fd)) return fd;
            if (fontDir == null) return null;

            string bin = System.IO.Path.Combine(fontDir, "atlas_zh.bin");
            if (!System.IO.File.Exists(bin))
            {
                loadError = "找不到 " + bin + "（先跑 tools/build_atlas.py）";
                Say("!! " + loadError);
                return null;
            }
            try
            {
                using (BinaryReader br = new BinaryReader(System.IO.File.OpenRead(bin), Encoding.UTF8))
                {
                    byte[] magic = br.ReadBytes(4);
                    if (magic.Length != 4 || magic[0] != (byte)'C' || magic[1] != (byte)'W'
                        || magic[2] != (byte)'A' || magic[3] != (byte)'1')
                    {
                        loadError = "atlas_zh.bin magic 不对";
                        Say("!! " + loadError);
                        return null;
                    }
                    int W = br.ReadInt32();
                    int H = br.ReadInt32();
                    int nSizes = br.ReadInt32();

                    FontData found = null;
                    for (int s = 0; s < nSizes; s++)
                    {
                        int sz = br.ReadInt32();
                        int cell = br.ReadInt32();
                        int count = br.ReadInt32();
                        bool want = (sz == size);
                        FontData cur = null;
                        if (want)
                        {
                            cur = new FontData();
                            cur.size = sz;
                            cur.texW = W;
                            cur.texH = H;
                        }
                        for (int i = 0; i < count; i++)
                        {
                            int code = br.ReadInt32();
                            int x = br.ReadInt32();
                            int y = br.ReadInt32();
                            int w = br.ReadInt32();
                            int h = br.ReadInt32();
                            int ox = br.ReadInt32();
                            int oy = br.ReadInt32();
                            int adv = br.ReadInt32();
                            if (cur != null)
                                cur.glyphs.Add(new int[] { code, x, y, w, h, ox, oy, adv });
                        }
                        if (cur != null) found = cur;
                    }

                    if (found == null)
                    {
                        loadError = "atlas_zh.bin 里没有 size=" + size;
                        Say("!! " + loadError);
                        return null;
                    }
                    // baseOffset 用整数字高的一半做近似（Print 不强依赖它，mBase 全程未使用）
                    found.ascent = 0;
                    fd = found;
                }
                fontDataCache[size] = fd;
                return fd;
            }
            catch (Exception e)
            {
                loadError = "读 atlas_zh.bin 失败: " + e.Message;
                Say("!! " + loadError);
                return null;
            }
        }

        // ================================================================ UIFont
        // ★ 2026-10-23：F3 现在是**汉化总开关**，一个键同时管两层：
        //   ① 字体层（本文件）：UIFont 换成合并图集里的位图字体 → ApplyFont / RestoreFont
        //   ② 文本层（本文件里的 TextPlugin）：让它接上/断开词表 → TextPlugin.SetOn(bool)
        //   v1.0.0：两层已合并进同一个 DLL，直接调用，不再有任何反射。
        //     （旧方案是两个独立 DLL 靠反射互调，只因为"分层排查"这一个理由，
        //       代价是类型/方法名全靠字符串约定、拿不到编译期检查，得不偿失。）
        //   F4 已退役，文本层不再有自己的按键。
        void ToggleHanhua()
        {
            // 用户一旦亲手按 F3，自动开启立刻让位 —— 否则自动开启的重试会把手动关掉的状态再拨回来。
            autoDone = true;
            SetHanhua(!hanhuaOn, true);
        }

        /// <summary>把总开关拨到 on/off，两层一起。
        /// dump=false 用于启动自动开启：那一次不需要再往报告里抄一遍整份 dump。</summary>
        void SetHanhua(bool on, bool dump)
        {
            hanhuaOn = on;
            Say("");
            Say("======== F3: 汉化总开关 = " + (hanhuaOn ? "开" : "关") + " ========");
            if (hanhuaOn) ApplyFont(); else RestoreFont();
            textSynced = TextPlugin.SetOn(hanhuaOn);
            if (!textSynced) Say("  (文本层未就绪：词表还没载入？字体层不受影响)");
            lastEvent = "F3 汉化=" + hanhuaOn;
            if (dump) DumpReport();
        }

        void ApplyFont()
        {
            try
            {
                UnityEngine.Object[] fonts = Resources.FindObjectsOfTypeAll(typeof(UIFont));
                for (int i = 0; i < fonts.Length; i++)
                {
                    UIFont f = fonts[i] as UIFont;
                    if (f == null) continue;
                    if (f.gameObject.name.StartsWith("HX_")) continue;
                    if (FindFontBackup(f) != null) continue;
                    SwapOneUIFont(f);
                }
            }
            catch (Exception e) { Say("!! ApplyFont 异常: " + e.Message); }
        }

        FontBackup FindFontBackup(UIFont f)
        {
            for (int i = 0; i < fontBackups.Count; i++)
                if (fontBackups[i] != null && ReferenceEquals(fontBackups[i].font, f)) return fontBackups[i];
            return null;
        }

        void SwapOneUIFont(UIFont f)
        {
            try
            {
                int size = SafeSize(f);
                FontData fd = LoadMergedFontData(size);
                if (fd == null) { Say("!! 跳过 '" + NameOf(f) + "'：合并图集数据装载失败"); return; }

                FontBackup b = new FontBackup();
                b.font = f;
                b.atlas = GetFieldU(f, fAtlas) as UIAtlas;
                b.mat = GetFieldU(f, fMat) as Material;
                object uvObj = GetFieldU(f, fUV);
                b.uv = (uvObj is Rect) ? (Rect)uvObj : new Rect(0, 0, 1, 1);
                b.dyn = GetFieldU(f, fDyn) as Font;
                object dsObj = GetFieldU(f, fDynSize);
                b.dynSize = (dsObj is int) ? (int)dsObj : 16;
                object dsfObj = GetFieldU(f, fDynStyle);
                b.dynStyle = (dsfObj is FontStyle) ? (FontStyle)dsfObj : FontStyle.Normal;
                b.bmFont = GetFieldU(f, fBMFont) as BMFont;

                // ★★★ 关键差异（2026-10-18 第六轮方案）：
                //   不新建材质，而是把 **GUI Atlas 的纹理换成合并图集**。
                //   这样文字继续走 mAtlas.spriteMaterial（UIFont.material getter 里
                //   `if (mAtlas != null) return mAtlas.spriteMaterial;` 优先级最高），
                //   与背景用的是**同一个材质对象** → UIPanel.Fill 的引用相等判断成立
                //   → 文字和背景进同一个 draw call → 遮挡关系恢复原版。
                if (!EnsureMergedAtlas())
                {
                    Say("!! 跳过 '" + NameOf(f) + "'：合并图集未就绪");
                    return;
                }

                BMFont bm = BuildBMFont(fd);

                // mAtlas 保持不动；只改 UV 基准、BMFont、清掉动态字体与 sprite 绑定
                SetFieldU(f, fDyn, null);          // ★ 走位图分支
                SetFieldU(f, fBMFont, bm);
                SetFieldU(f, fUV, new Rect(0, 0, 1, 1));   // 整张图集作为 UV 基准
                SetFieldU(f, fPMA, -1);
                SetFieldU(f, fSprite, null);
                SetFieldU(f, fSpriteSet, false);
                // mMat 显式指回图集材质，双保险
                try
                {
                    UIAtlas at = GetFieldU(f, fAtlas) as UIAtlas;
                    if (at != null && at.spriteMaterial != null)
                        SetFieldU(f, fMat, at.spriteMaterial);
                }
                catch (Exception) { }

                fontBackups.Add(b);

                SafeMarkAsDirty(f);
                RefreshLabelMaterials(f);

                Say("  [UIFont] '" + NameOf(f) + "' size=" + size
                    + " → BMFont(charSize=" + bm.charSize + " tex=" + bm.texWidth + "x" + bm.texHeight
                    + " glyphs=" + fd.glyphs.Count + ")"
                    + " mAtlas=" + NameOf(b.atlas) + "（保持）"
                    + " 材质=图集材质");
            }
            catch (Exception e) { Say("!! UIFont 异常 (" + NameOf(f) + "): " + e.Message); }
        }

        void RestoreFont()
        {
            int n = 0;
            for (int i = 0; i < fontBackups.Count; i++)
            {
                FontBackup b = fontBackups[i];
                if (b == null || b.font == null) continue;
                try
                {
                    SetFieldU(b.font, fAtlas, b.atlas);
                    SetFieldU(b.font, fMat, b.mat);
                    SetFieldU(b.font, fUV, b.uv);
                    SetFieldU(b.font, fDyn, b.dyn);
                    SetFieldU(b.font, fDynSize, b.dynSize);
                    SetFieldU(b.font, fDynStyle, b.dynStyle);
                    SetFieldU(b.font, fBMFont, b.bmFont);
                    SetFieldU(b.font, fPMA, -1);
                    SetFieldU(b.font, fSprite, null);
                    SetFieldU(b.font, fSpriteSet, false);
                    SafeMarkAsDirty(b.font);
                    n++;
                }
                catch (Exception) { }
            }

            // ★ 还原图集纹理 —— 否则关掉字体层后，所有精灵仍按 4096x8192 的 UV 采样 512x1024 的图，整个 UI 会错乱
            if (mergedAtlasApplied)
            {
                try
                {
                    UnityEngine.Object[] objs = Resources.FindObjectsOfTypeAll(typeof(UIAtlas));
                    for (int i = 0; i < objs.Length; i++)
                    {
                        UIAtlas a = objs[i] as UIAtlas;
                        if (a == null) continue;
                        Material sm = a.spriteMaterial;
                        if (sm == null) continue;
                        sm.mainTexture = origAtlasTex;
                        sm.mainTextureScale = origTexScale;
                        sm.mainTextureOffset = origTexOffset;
                        break;
                    }
                    // 还原纹理尺寸后同样要重算 UV，否则精灵还按 4096x8192 采样 512x1024 的图
                    int nSpr = RefreshAllSpriteUVs();
                    Say("  [合并图集] 纹理已还原为原 GUI Atlas（重算 " + nSpr + " 个精灵 UV）");
                }
                catch (Exception e) { Say("!! 还原图集失败: " + e.Message); }
                // ★ 不 Destroy(mergedTex)：裁剪 draw call 的 mClippedMat 可能仍引用它，
                //   销毁后下一帧 OnWillRenderObject 会拿到空纹理。留着复用即可（128MB 常驻）。
                mergedAtlasApplied = false;
            }

            Say("  [UIFont] 还原 " + n + " 个");
            fontBackups.Clear();
            MarkAllLabelsChanged();
        }


        // ================================================================ 通用
        /// <summary>
        /// ★ 把引用这个 UIFont 的所有 label 的 material 重新设一遍。
        ///
        /// 根因（反编译 UIWidget.material 的 setter 得知）：
        ///   setter 里才做 RemoveWidget / mMat = value / CreatePanel()，
        ///   也就是说「控件属于哪个 UIPanel.draw call」是由 material 赋值驱动的。
        ///   而 UILabel.material 的 getter 只有 base.material == null 时才回落到 mFont.material，
        ///   所以 label 会一直持有旧材质（GUI Atlas）。
        ///
        /// 我们改的是 UIFont.mMat（字体级材质），label 自己的 mMat 不变 →
        ///   UIPanel.Fill() 里 `uIWidget.material == mat` 匹配不上 →
        ///   label 永远不进入任何 draw call → 设置面板这类「之后才激活」的界面整片空白。
        /// 主菜单之所以看着正常，是因为替换时它正在被 Fill，走的是 WriteToBuffers。
        /// </summary>
        void RefreshLabelMaterials(UIFont f)
        {
            int n = 0;
            try
            {
                UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(UILabel));
                for (int i = 0; i < all.Length; i++)
                {
                    UILabel l = all[i] as UILabel;
                    if (l == null) continue;
                    UIFont lf = null;
                    try { lf = l.font; } catch (Exception) { }
                    if (!ReferenceEquals(lf, f)) continue;
                    try
                    {
                        Material desired = f.material;
                        if (desired != null)
                        {
                            // 先置 null 再赋值，确保 setter 里的 mMat != value 成立
                            l.material = null;
                            l.material = desired;
                            n++;
                        }
                    }
                    catch (Exception) { }
                    try { l.MarkAsChanged(); } catch (Exception) { }
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 周期性兜底：面板/页签 later 才激活时，label 会在激活瞬间重新 Fill，
        /// 但那时它的 mMat 仍是旧材质。这里定期扫一遍，把仍持有旧材质的 label 纠正过来。
        ///
        /// ★ 合并图集方案下文字的材质**就是图集材质**（不再按面板克隆副本，
        ///   那套分层方案已证伪并删除，见文件上半部「已废弃的方案」）。
        ///   所以这里只做一件事：材质不是我们的图集材质时（首次绑定 / 激活时被重填）
        ///   设回 `lf.material`；已经是了就不干预。
        /// </summary>
        void RefreshAllLabelMaterials()
        {
            if (fontBackups.Count == 0) return;
            int fixedCount = 0;
            try
            {
                UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(UILabel));
                for (int i = 0; i < all.Length; i++)
                {
                    UILabel l = all[i] as UILabel;
                    if (l == null) continue;
                    UIFont lf = null;
                    try { lf = l.font; } catch (Exception) { }
                    if (lf == null) continue;
                    FontBackup b = FindFontBackup(lf);
                    if (b == null) continue;
                    try
                    {
                        Material desired = lf.material;
                        if (desired == null) continue;
                        Material cur = l.material;
                        // 新方案下文字的材质就是图集材质，已经是了就不干预
                        if (cur != null && ReferenceEquals(cur, desired))
                            continue;
                        l.material = desired;                                  // setter 会重新注册
                        try { l.MarkAsChanged(); } catch (Exception) { }
                        fixedCount++;
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            if (fixedCount > 0)
            {
                materialFixCount += fixedCount;
                Say("  [兜底] 第 " + frame + " 帧修正了 " + fixedCount + " 个 label 的材质（累计 " + materialFixCount + "）");
            }
        }





        void MarkAllLabelsChanged()
        {
            try
            {
                UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(UILabel));
                for (int i = 0; i < all.Length; i++)
                {
                    UILabel l = all[i] as UILabel;
                    if (l != null) { try { l.MarkAsChanged(); } catch (Exception) { } }
                }
            }
            catch (Exception) { }
        }

        // ※ 原 RestoreAll()（F10 全部还原）已删：F3 本身就能关，F10 早已不再绑定。

        void PruneDead()
        {
            for (int i = fontBackups.Count - 1; i >= 0; i--)
                if (fontBackups[i] == null || fontBackups[i].font == null) fontBackups.RemoveAt(i);
        }

        // ================================================================ F11
        void DumpReport()
        {
            Say("");
            Say("======== F11: dump（第 " + frame + " 帧）========");
            Say("  插件=v" + Ver + "  开关: 汉化=" + hanhuaOn + "  HUD=" + showHud);
            Say("  场景='" + sceneName + "'  帧=" + frame);
            Say("  已装载字号: " + DictKeys(fontDataCache));
            Say("  合并图集=" + (mergedAtlasApplied ? "已启用 (4096x8192)" : "未启用 (原版 512x1024)")
                + "  UIFont 备份=" + fontBackups.Count + "  累计兜底修正=" + materialFixCount);
            if (loadError.Length > 0) Say("  !! 最近装载错误: " + loadError);
            if (mergedAtlasError.Length > 0) Say("  !! 最近图集错误: " + mergedAtlasError);
            Say("  最近事件: " + lastEvent);

            // ★ HUD 上只留按键与开关状态（用户要求：内部数字不要暴露）。
            //   所有数字集中到 F11 这一份 dump 里 —— 包括文本层的（直接取 DumpLine）。
            //   v1.0.0：同一程序集，直接调用。文本层若还没 Awake（词表未载入），
            //   DumpLine 自己会返回说明串，不会抛。
            try
            {
                string s = TextPlugin.DumpLine();
                Say("  " + ((s != null && s.Length > 0) ? s : "文本层: 状态不可用"));
            }
            catch (Exception) { Say("  文本层: 未就绪（TextPlugin 还没 Awake）"); }

            try
            {
                UnityEngine.Object[] fonts = Resources.FindObjectsOfTypeAll(typeof(UIFont));
                Say("");
                Say("  --- UIFont（共 " + fonts.Length + " 个）---");
                for (int i = 0; i < fonts.Length; i++)
                {
                    UIFont f = fonts[i] as UIFont;
                    if (f == null) continue;
                    BMFont bm = null;
                    try { bm = f.bmFont; } catch (Exception) { }
                    Say("   [" + i + "] '" + NameOf(f) + "' isDynamic=" + SafeIsDynamic(f)
                        + " size=" + SafeSize(f)
                        + " bm=" + (bm != null ? (bm.charSize + "/" + bm.texWidth + "x" + bm.texHeight + "/glyphs=" + bmGlyphCount(bm)) : "null")
                        + " mAtlas=" + NameOf(GetFieldU(f, fAtlas) as UnityEngine.Object)
                        + " mMat=" + NameOf(GetFieldU(f, fMat) as UnityEngine.Object));
                }
            }
            catch (Exception e) { Say("  UIFont dump 异常: " + e.Message); }

            try
            {
                UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(UILabel));
                int act = 0, hasText = 0;
                Say("");
                Say("  --- 活动 UILabel 明细 ---");
                for (int i = 0; i < all.Length; i++)
                {
                    UILabel l = all[i] as UILabel;
                    if (l == null || !l.gameObject.activeInHierarchy) continue;
                    act++;
                    string t = "";
                    try { t = l.text; } catch (Exception) { }
                    if (!string.IsNullOrEmpty(t)) hasText++;
                    UIFont lf = null;
                    try { lf = l.font; } catch (Exception) { }
                    UIPanel lpanel = null;
                    try { lpanel = l.panel; } catch (Exception) { }
                    Material lmat = null;
                    try { lmat = l.material; } catch (Exception) { }
                    Say("   " + PathOf(l.gameObject) + " font=" + NameOf(lf)
                        + " size=" + ((lf != null) ? SafeSize(lf) : -1)
                        + " depth=" + l.depth
                        + " panel=" + NameOf(lpanel)
                        + " clip=" + ((lpanel != null) ? lpanel.clipping.ToString() : "?")
                        + " mat=" + NameOf(lmat)
                        + " color=" + l.color
                        + " 文本=[" + Trunc(t, 40) + "]");
                }
                Say("   合计: 活动=" + act + "  其中非空文本=" + hasText + "  （全部 label=" + all.Length + "）");
            }
            catch (Exception e) { Say("  UILabel dump 异常: " + e.Message); }

            // ★ 诊断 draw call：它的 sharedMaterial 才是最终上传给 GPU 的材质，
            //   label.material 只是"请求"，未必等于 draw call 实际用的材质。
            //   ★ 自合并图集方案起**文字与背景共用同一份图集材质**，所以这里只看
            //     两件事：① 材质是不是合并图集（"合并图集" / "原版"）；
            //     ② 顶点数是否为 0（顶点 0 = 该 draw call 没被 Fill 到）。
            //     历史列（renderQueue、材质实例 ID）属于已废弃的"拆 draw call +
            //     按队列分层"诊断口径，NOTES 4.4 已证明无意义，故删掉。
            try
            {
                UnityEngine.Object[] dcs = Resources.FindObjectsOfTypeAll(typeof(UIDrawCall));
                Say("");
                Say("  --- UIDrawCall（共 " + dcs.Length + " 个）---");
                for (int i = 0; i < dcs.Length; i++)
                {
                    UIDrawCall dc = dcs[i] as UIDrawCall;
                    if (dc == null) continue;
                    Renderer r = dc.GetComponent<Renderer>();
                    if (r == null) continue;
                    Material[] mats = r.sharedMaterials;
                    if (mats == null || mats.Length == 0) continue;
                    StringBuilder sb = new StringBuilder();
                    for (int k = 0; k < mats.Length; k++)
                    {
                        Material m = mats[k];
                        if (m == null) { sb.Append(" [null]"); continue; }
                        bool isAtlas = mergedAtlasApplied && mergedTex != null
                                       && ReferenceEquals(m.mainTexture, mergedTex);
                        sb.Append(" ").Append(isAtlas ? "合并图集" : ("原版/" + NameOf(m)));
                    }
                    int verts = -1;
                    try { MeshFilter mf = dc.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) verts = mf.sharedMesh.vertexCount; } catch (Exception) { }
                    Say("   [" + i + "] clip=" + dc.clipping + " 顶点=" + verts + " 材质:" + sb);
                }
            }
            catch (Exception e) { Say("  UIDrawCall dump 异常: " + e.Message); }

            Say("  （完整记录见 " + reportPath + "）");
        }

        /// <summary>BMFont 的真字形数（`BMFont.glyphCount` 是 public 属性，见 ilx find BMFont）。</summary>
        static int bmGlyphCount(BMFont bm)
        {
            try { return (bm != null) ? bm.glyphCount : 0; } catch (Exception) { return 0; }
        }

        static string DictKeys<T>(Dictionary<int, T> d)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, T> kv in d) { if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Key); }
            return (sb.Length == 0) ? "(无)" : sb.ToString();
        }

        // ================================================================ HUD
        // ★ 2026-10-21 重做：HUD 只承担两件事 ——
        //     ① 把**所有能按的键**摆在眼前（用户要求：能按的键都要提供出来）；
        //     ② 报告总开关的当前状态，以及出错时的原因。
        //   内部数字（字号表、备份数、帧号、码表统计、补丁自检…）一律不上 HUD，
        //   全部挪到 F11 的 dump 文件里 —— 看 HUD 的人不需要它们。
        //   ★ 2026-10-23（v1.3.1）：HUD **默认关闭**（玩家按 F12 才出来）；
        //     并且不再显示「文本层」状态 —— 两层被 F3 绑死，永远同步。
        //
        //   宿主 = 本类（HanhuaPlugin）。v1.0.0 起文本层与本类**在同一个程序集**，
        //   出错行直接调 TextPlugin.ErrorLine() 取，不再反射。
        //   （旧的双 DLL 方案里那些 peerMethods / FindPeerMethod / PeerString 已全部删除。）
        void OnGUI()
        {
            if (!showHud) return;
            Color old = GUI.color;
            try
            {
                GUI.color = Color.white;
                List<string> lines = new List<string>();

                // ---- 第 1 行：全部按键 + 总开关的状态（HUD 的主体）----
                //   ★ 2026-10-23：不再显示「文本层:开/关」。文本层与字体层由 F3
                //     同生同死（见 SetOn 那条链），两个状态永远一样，摆两个开关
                //     只会让用户以为还能分开调。文本层的真实状态留在 F11 dump 里
                //     （TextPlugin.DumpLine 的 `开关=`）。
                string textErr = null;
                try { textErr = TextPlugin.ErrorLine(); } catch (Exception) { }
                lines.Add("按键   F3 汉化总开关:" + (hanhuaOn ? "开" : "关")
                        + "   F5 未命中清单   F9 重载词表   F11 dump   F12 关HUD");

                // ---- 第 2 行起：只在真出问题时出现 ----
                if (loadError.Length > 0) lines.Add("!! 字体层: " + loadError);
                if (mergedAtlasError.Length > 0) lines.Add("!! 图集: " + mergedAtlasError);
                if (textErr != null && textErr.Length > 0) lines.Add("!! 文本层: " + textErr);

                // ---- 自适应宽度：量出最长一行，不写死像素 ----
                GUIStyle st = GUI.skin.label;
                float maxW = 0f;
                for (int i = 0; i < lines.Count; i++)
                {
                    float x = 0f;
                    try { x = st.CalcSize(new GUIContent(lines[i])).x; } catch (Exception) { }
                    if (x > maxW) maxW = x;
                }
                float w = maxW + 28f;
                if (w < 480f) w = 480f;
                if (w > Screen.width - 12f) w = Screen.width - 12f;

                int h = 20 * lines.Count + 14;
                GUI.Box(new Rect(4, 4, w, h), "");
                for (int i = 0; i < lines.Count; i++)
                    GUI.Label(new Rect(14, 12 + 20 * i, w - 20, 20), lines[i]);
            }
            catch (Exception) { }
            GUI.color = old;
        }
    }
}
