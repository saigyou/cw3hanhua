# CW3 汉化项目 · 技术文档

> **这是唯一文档。** 本文只保留**结论、证据、可复现操作**；
> 试错过程不单独存档（2026-10-18 已清理 `_archive/`），历史结论都压进本文对应章节。
> 维护纪律见文末第十节。

---

## 一、一页速览

| 问题 | 答案 |
|---|---|
| 怎么改 | **不改游戏文件**：BepInEx 5 + 一个插件 DLL + 一个外置词表 |
| 字体怎么办 | **离线烘 BMFont 图集 → 运行时替换 `UIFont.mFont`**，走游戏原生的**位图分支**（3.4） |
| ~~动态字体~~ | 只在 **TextMesh** 上可用；**UILabel 上已证伪**（3.2） |
| 文本在哪收口 | `UILabel.set_text`（**322** 个调用点）+ 未激活 label 的全量 sweep + TextMesh 的 **15** 个写入点 |
| 游戏自带多语言 | **是死的**（词典为空、`Get` 零调用） |
| 游戏本体 | 原版，零改动；BepInEx 的开关只靠 `winhttp.dll` 改名 |
| 当前进度 | 字体层**已打通**（A1–A3b 全过）；文本层：词表 **1140 / 2383 条已译**，`zh_cn.dict` 已重编（4.5），**可见 UI 文本已清零**；下一步 = 实测 + 布局微调 |
| 排查 NGUI 怪现象 | **先 `tools\ilspy.cmd` 反编译真实实现再推理** —— 这是本项目最有效的动作 |
| 想量化文本量 | **不要靠跑游戏**，跑 `tools\collect_ui_strings.py`（静态扫全部 IL） |
| 探针报「Awake=0」 | **先读 6.1a/6.1b**：v1.0 与静态补丁类都拦不到 `Awake`，计数恒为 0；只看最终判定行 |

### 协作硬规则（用户明确要求过）

1. **不要自己判断 Steam 是否在运行 —— 直接问用户。**
   DSH 的 shell 跑在 **Low 完整性级别**，进程全表枚举**看不到**高完整性进程
   （实测：活跃的 `steam.exe` PID 28188 能用 `-Id` 拿到，却不在全表 42 个进程里）。
   **「枚举看不到」≠「没在运行」**；`-ErrorAction SilentlyContinue` 还会把权限错误吞成"没找到"。
2. **不要自行启动 Steam 或游戏本体。** 需要时明确告知用户要做什么。
3. **实验前先讨论。** 一次只改一个变量。

---

## 二、当前状态

| 项 | 状态 |
|---|---|
| 游戏本体 | **原版**（Steam 校验 + 重装核对过 `level1`/`level2`/`sharedassets0`/`sharedassets2`） |
| 资产改动 | **无** |
| 存档与设置 | 已清空重建（`Documents\creeperworld3`、`%APPDATA%\creeperworld3`） |
| 主菜单 | **现正常**（曾出现"3D 按钮点不动"，靠重启电脑恢复，见第六节） |
| BepInEx 5.4.23.5 | **已启用**（`winhttp.dll` 就位；注入点改动见 6.8） |
| 字体层插件 | `BepInEx/plugins/CW3Hanhua.dll` = **v0.8.0 合并图集**（`plugin/CW3Hanhua.cs`）<br>把中文字形并进 GUI Atlas，不拆 draw call。**已实测通过** |
| 合并图集 | `_font/atlas_zh.png` 4096×8192（原图集 1:1 贴左上 + 三套中文字形）<br>`atlas_zh.bin` 字形表（插件直读）**这两个是字体层的全部运行时输入** |
| 文本层插件 | `BepInEx/plugins/CW3HanhuaText.dll` = **v0.8.0 文本层**（`plugin/CW3HanhuaText.cs`）<br>读 `_dict/zh_cn.dict`（**现 1140 条**，2026-10-18 全量翻译入库，见 4.5）；F4 开关 / F5 单次 sweep / F9 重载词表。**未实测** |
| 字形度量 | `_font/font_{16,20,28}.{json,png}` —— **重烘图集的必需输入**（json 提供度量、png 提供字形位图）<br>雅黑 Bold @ `--scale 0.80`，每套 **7544 字形**。**改了字集必须重跑 `build_atlas.py`** |
| 探针插件 | `BepInEx/plugins/CW3HanhuaMini.dll`（`plugin/MiniProbe.cs` **v2.3**：`__HarmonyPatches` 自检 +
| | 字段/场景旁证；**补丁计数恒为 0 且不可信**，见 6.1a/6.1b） |
| 工具 | `tools\ilx.exe`（IL 检查）、`tools\ilspy.cmd`（反编译 C#）、`tools\bake_font.py`、`tools\check_charset.py`、**`tools\collect_ui_strings.py` / `dict_stats.py` / `build_dict.py`（文本层，见 4.2）**、`.venv`（UnityPy / dnfile / **dncil** / fontTools / Pillow） |

**启用 / 停用 BepInEx**

```powershell
$g="C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3"
Rename-Item "$g\winhttp.dll.off" winhttp.dll   # 启用
Rename-Item "$g\winhttp.dll" winhttp.dll.off   # 停用
```

`BepInEx\config\BepInEx.cfg` 的 `[Preloader.Entrypoint]` **必须**是
`Assembly-CSharp.dll` / `SteamManager` / `Awake`，否则启动即崩（第七节）。

---

## 三、技术事实

### 3.1 三类文本，互不相干

| 载体 | 规模 | 字体机制 |
|---|---|---|
| **Unity TextMesh** | 27 个（level1 里 14 个 = 主菜单大按钮） | `UnityEngine.Font`（Aldrich 动态字体，无汉字） |
| **NGUI UILabel** | **1206 对象 / 1029 条有文字**（level1 里 354 个，同一时刻仅约 4 个 active） | `UIFont` → BMFont **预烘焙图集** |
| **DLL 写死字符串** | 822 条（与资产去重后 **1324 条唯一串**） | 最终都落到上面两种载体上 |

**不是 uGUI**：全部资产里 `UnityEngine.UI.Text` 为 **0** 个。

### 3.2 ⚠ 动态字体只能用于 TextMesh，**UILabel 上不可靠**

NGUI 的 `UIFont` 确实完整编译了动态字体分支（`Print`/`WrapText`/`CalculatePrintedSize`
都调 `Font.RequestCharactersInTexture` + `GetCharacterInfo`），**TextMesh 路线实测可用**：
`Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 20)` + `TextMesh.font` +
`MeshRenderer.material` → 主菜单 14 个 TextMesh 全部正常显示中文。

**但 UILabel 路线（改 `UIFont.dynamicFont`）经 v0.1→v0.6 六轮实测无法收敛**：

| 现象 | 根因 |
|---|---|
| 大量 label 空白 / 文字跑出框 | `UIFont.Print` 动态分支的 `mDynamicFontOffset` 恒为 0 |
| 手动改 offset 无效 | **`MarkAsDirty()` 内部无条件调 `RecalculateDynamicOffset()`**，把 offset 覆盖成 `size + yMax` |
| 不同 label 基线相差 1.13 个字高 | CW3 的动态分支是**从未被游戏使用过的代码路径**，基线/对齐不完整 |
| 文字被按钮盖住 | 材质一变就拆 draw call（见 3.8①） |

> **`RecalculateDynamicOffset()` 是存在的**（`public`，`_diag/UIFont.cs:571`）。
> 曾有文档误记"不存在"，原因是反射用了 `NonPublic` 去找 `public` 方法。
> **结论：放弃用动态字体改造 UILabel。**

### 3.3 文本可以在 `UILabel.set_text` 上收口

- `UILabel.set_text` 是**可打补丁的普通方法**（token **`0x06000474`**，有 IL 体）
- 程序集内 **322 个调用点**（`ilx callers --type UILabel set_text`）
- 实测游戏确实在调（一次会话 24 次，内容是悬停描述等 DLL 文本）
- **但资产里序列化的 `mText` 不经过 setter**（反序列化直接写字段）
  → 必须再做一次 **sweep**，且要**含未激活的 label**（350 个）

### 3.4 ★ UIFont 位图替换（当前采用的方案）

`UIFont : MonoBehaviour`（不是 ScriptableObject）。运行时把 3 个原始 UIFont 改造成位图字体：

```
Aldrich16    size=20    主菜单/设置文字
Tempesta     size=16    版本号、按钮、悬停描述
RadioStars22 size=28    大标题
```

```csharp
SetField(f, "mAtlas", null);
SetField(f, "mDynamicFont", null);        // ★ 关键：走位图分支
SetField(f, "mFont", 自建BMFont);
SetField(f, "mMat", 自建材质);
SetField(f, "mUVRect", new Rect(0,0,1,1));
f.MarkAsDirty();
```

**必须处理的四个坑见 3.8。**

### 3.5 BMFont / BMGlyph 字段语义

```csharp
// BMGlyph（权威注释）
public int index;   // 字形索引 = 字符码
public int x, y;    // 图集内像素坐标（左上角）
public int width, height;   // 字形像素尺寸
public int offsetX; // 相对笔位左侧的偏移
public int offsetY; // 相对【行顶】的下移量
public int advance; // 前进宽度
public int channel; // 15 = 普通 alpha；1/2/4/8 = packed 字体的通道

// BMFont：mSize(charSize) / mBase(baseOffset) / mWidth(texWidth) / mHeight(texHeight) / mSaved
```

**构造方式**：`bm.GetGlyph(code, true)` 创建并登记（内部 `mSaved.Add` + `mDict.Add`）。

**`UIFont.Print` 位图分支的几何公式**：

```csharp
int fs = size;                                   // = mFont.charSize
Vector2 invSize = new Vector2(1f/fs, 1f/fs);
float invX = uvRect.width  / mFont.texWidth;
float invY = mUVRect.height / mFont.texHeight;

v0.x =  invSize.x * (x + glyph.offsetX);
v0.y = -invSize.y * (y + glyph.offsetY);
v1.x = v0.x + invSize.x * glyph.width;
v1.y = v0.y - invSize.y * glyph.height;

u0.x = mUVRect.xMin + invX * glyph.x;
u0.y = mUVRect.yMax - invY * glyph.y;
u1.x = u0.x + invX * glyph.width;
u1.y = u0.y - invY * glyph.height;
```

**`mBase` 在整个 `Print` 中从未被使用。** 把 `mAtlas` 置空、`mUVRect=(0,0,1,1)` 后，
`glyph.x/y` 就是图集内的像素坐标。

### 3.6 CW3 自带的多语言系统是**死的**

`Localization` 实例 1 个、`mDictionary` 条目 **0**、`mLanguage` 空、`Get` **零调用**。
`Get` 找不到就原样返回 key（恒等函数）。汉化只能靠 `set_text` / sweep / 字体层。

### 3.7 其它有用的事实

- 文本里有 NGUI 富文本标记（`[ffffd0]...[ffffff]`）和 `\n`，**翻译时必须原样保留**
- 全程序集只有 `UICamera` 用 `Physics.Raycast`；CW3 自己用 `RaycastAll` + `ScreenPointToRay`
- **`Assembly-CSharp` 里有全局 `Path` 类**，会抢掉 `System.IO.Path` → 必须写全名
- Unity 5.2 没有 `UnityEngine.SceneManagement` → 用 `Application.loadedLevelName`
- `UIPanel` 没有 `SetDirty()` → 用 `UILabel.MarkAsChanged()`
- Unity 5.2.3 没有 `GameObject.scene`（5.3+ 才有）→ 用系统库编译能过、运行时出错
- 全程序集 8289 个方法有 IL 体（`ilx stats` 可复现）
- **`TextureFormat.Alpha8` 在 Unity 5.2 采样出 RGB=0**，画字体必须用 RGBA32「RGB=白 + A=覆盖率」
- `GetPixels32` / `SetPixels32` 的索引 0 都是**左下**，两者约定一致，**不要翻转**

### 3.8 ★ NGUI 位图字体接入的四个坑（2026-10-17 实测，全部踩过）

#### ① 材质独立 → 拆 draw call → **渲染顺序变成任意的**（最隐蔽，卡最久）

- 原版：字体与 sprite **共用 GUI Atlas 材质** → 同一 `UIDrawCall` → 遮挡由 mesh 内三角形顺序决定
- 换独立材质后 → 拆成两个 draw call。而 **CW3 这版 `UIDrawCall` 完全不设 `renderQueue`/`sortingOrder`**
  （全文件搜不到）→ 同队列透明物体交给 Unity 按包围盒距离排序 → 两个 draw call 变换相同 →
  **排序任意** → 背景可能后画把文字盖住
- **症状**：有大背景图的界面（设置面板）文字"消失"；**但超框的字露在背景外反而可见**
  —— 这个"露出来的部分"是判定遮挡而非未渲染的关键证据
- ★★ **最终解（2026-10-18）：不要拆 draw call。** 把中文字形并进 GUI Atlas 本身，
  让 `UIFont.mAtlas` 继续指向 GUI Atlas（`UIFont.material` getter 里 `if (mAtlas != null) return mAtlas.spriteMaterial;`
  优先级最高），于是文字与背景用**同一个材质对象** → `UIPanel.Fill` 的引用相等成立 →
  同一个 draw call → 遮挡关系**天然恢复原版**。见 4.4。
- ⚠ **四条走不通的路（勿重试）**：
  1. 字形材质固定 `renderQueue=3001` → 悬停框浮在设置界面之上
  2. 按 `UIWidget.depth` 分层 → 无效，跨面板顺序跟 widget.depth 无关
  3. 按面板兄弟序号给背景+文字分层 → 悬停框对了，但**连带破坏所有其它 UI 遮挡**
  4. 只给文字分层、背景不动 → 文字永远后画，悬停框照样浮着
  根因都是：**在错误的层面上打补丁**。真正的分界线是「有没有拆 draw call」。
- **证据**：`UIDrawCall` 是 `DontDestroyOnLoad` 的独立根对象（`UIPanel.GetDrawCall` 里
  `new GameObject("_UIDrawCall [...]")`），不在任何面板底下；`UIPanel` 也**没有 `depth` 字段**
  （只有 `depthPass`）。所以 NGUI 自己就**没有**跨面板排序机制。
- **辅助事实**：`UIDrawCall.UpdateMaterials` 里 `mClippedMat = new Material(mSharedMat)` 之后紧跟
  `CopyPropertiesFromMaterial(mSharedMat)`，**会**继承 renderQueue（旧文档说"不继承"是错的）。
- **副作用解释**：悬停文本框是**半透明背景**，它压在文字上时文字就"发暗"
  —— 同一个遮挡问题，只是半透明所以表现为变暗而非消失

#### ② `UIWidget.material` 的 setter 才是控件注册进 panel 的入口

```csharp
set {
    if (mMat != value) {
        if (mMat != null && mPanel != null) mPanel.RemoveWidget(this);
        mPanel = null; mMat = value;
        if (mMat != null) CreatePanel();      // ← 只有这里才注册
    }
}
```

而 `UILabel.material` 的 getter **只在 `base.material == null` 时才回落**到 `mFont.material`。
所以改 `UIFont.mMat`（字体级）后，label 自己的 `mMat` 仍是旧材质 →
`UIPanel.Fill()` 里 `uIWidget.material == mat` 匹配不上 → 该 label 不进任何 draw call。
**修法**：改完字体后对每个相关 label 把 `material` 重新赋一次（`null` → 新材质）。

#### ③ 不能整张图集当 `_MainTex` 交给裁剪

`UIDrawCall.OnWillRenderObject` 里，NGUI 的裁剪不是 shader 读 `_ClipRange`，而是对材质做**纹理重映射**：

```csharp
mClippedMat.mainTextureOffset = (-clipRange.x/z, -clipRange.y/w);
mClippedMat.mainTextureScale  = (1/z, 1/w);
```

它**假定 `_MainTex` 就是面板内容本身**。字体图集整张用（`mUVRect=(0,0,1,1)`）会被这层重映射打乱 UV。
本次实测中 `SettingsPanel` 恰好没有裁剪、只有 `KeyMappingsPanel` 是 `SoftClip`，所以未成为主因，
**但接入真正的滚动列表时必须处理。**

#### ④ `UIPanel.LateUpdate` 只重填"几何变了"的材质

```csharp
if (uIWidget.UpdateGeometry(this, forceVisible) && !mChanged.Contains(uIWidget.material))
    mChanged.Add(uIWidget.material);
...
for (...) Fill(mChanged.buffer[k]);      // ← 只填这些
```

所以 `SoftClip` 面板里若 `IsVisible` 判为假，`OnFill` 不会被调用，顶点就是 0。
诊断时注意：**dump 读 `mGeom.verts` 不可靠**，真正上传的是 `mRtpVerts`。

---

## 四、实施方案

### 4.0 执行计划与进度

**路线**：字体层用**位图图集**（游戏原生分支），文本层用 `set_text` + 词表。两层正交可独立验证。

| 层 | 解决什么 | 手段 |
|---|---|---|
| ① 字体层 | 字形从哪来 | 离线烘 BMFont → 运行时填 `UIFont.mFont/mMat/mUVRect`，`mDynamicFont=null` |
| ② UILabel 文本层 | 英文串怎么变中文 | `set_text` prefix（322 调用点）+ 全量 sweep（354 个，含未激活） |
| ③ TextMesh 文本层 | 同上 | 动态字体（已验证）+ 15 个写入点 hook 或周期 sweep |

| # | 阶段 | 状态 |
|---|---|---|
| A1 | 离线烘焙（图集 + 字集检查） | ✅ |
| A2 | 插件字体层 | ✅ |
| A3a | 英文验证（位置/布局/遮挡） | ✅ |
| A3b | 中文验证 | ✅ |
| B1 | 静态全量收集 + 词表骨架 | ✅ |
| B2 | `set_text` prefix（**已实现，待实测**） | 🟡 |
| B3 | prefix 接词表 + sweep（**已实现，待实测**） | 🟡 |
| C1 | 翻译：主菜单 + 设置页 | ✅ 首批 153 条 |
| C2 | 翻译：游戏内 HUD / 建造菜单 / 悬停 | ✅ 全量批 full_01–21 已入库 |
| C3 | 翻译：教程 / 剧情 / 长文本 | ✅ 同上（序章旁白/尾声议会/圣物说明都在里面） |
| C4 | 翻译：剩余 1243 条按设计不译 | ✅ 分流完毕，理由见 4.5 |
| D | 布局微调 / 术语统一 / 打包 | ⬜ **下一步：实测 + 布局微调** |

**为什么字体层先做**：否则中文显示成方块，无法区分"字体没做"还是"翻译错"。
**为什么先用英文验证**：文本不变时，布局/裁剪/遮挡的异常只能归因于字体层。

> **2026-10-18 修正**：此前本节把 B1 标成"下一步"，但字数层插件（v0.7.0）里**一行 Harmony 都没有**，
> B2/B3 实际从未开工。见 4.2。

### 4.2 文本层：静态全量收集与词表骨架（2026-10-18）

**为什么不靠跑游戏收集字符串**：一次会话只能覆盖走过的路径（主菜单、设置页），
剧情/任务/教程要走很远才出现，**全量量化不可能靠回放**。改用静态分析：

```
路 1  IL：每条 ldstr 后面紧跟的指令若"消费"了它（call/concat/stfld…）→ 这条字符串进了逻辑
      用 dnfile(读元数据) + dncil(解析方法体) 扫 Assembly-CSharp.dll / -firstpass.dll
      → 覆盖**全部 8289 个有 IL 体的方法**，而不是走过的路径
路 2  资产：UILabel.mText / TextMesh.m_Text 序列化值（反序列化不经 setter，必须单独收）
```

**产物与工具**（都在 `tools/`，Python 用 `.venv`）

| 脚本 | 作用 | 产物 |
|---|---|---|
| `collect_ui_strings.py` | 静态全量收集 + 分类 + 去重 | `_dict/strings.json` |
| `dict_stats.py` | 覆盖率报告（按可信度/来源/类名分组 + 缺字） | `_dict/STATUS.md` |
| `build_dict.py` | 编译成插件读的二进制词表 | `_dict/zh_cn.dict` |
| `check_charset.py` | 译文缺字检查（**复用 bake_font 的字符集定义**） | 终端 |
| `bake_font.py` | 烘字形；`gb2312_chars()` / `charset_for(sizes)` 同时供上面两个用 | `_font/` |

**分类三档**（`strings.json` 每条的 `grade`）

| grade | 含义 | 处理 |
|---|---|---|
| `ui` | 多词英文、以冒号结尾、含数据的格式串（`Score: 9999`）、以空格结尾的拼接前缀 | 确定要翻 |
| `other` | 单个词（`Yes`/`Creeper`/`Close`）、代码片段、疑似调试消息 | 拿不准，后翻 |
| `noise` | 标识符 / PascalCase / 资源名 / 标准库异常 / 节点路径 / 正则 / 纯数字 | 丢弃 |

**当前量化结果**（2026-10-18，`collect_ui_strings.py` 可复现）：

| 项 | 数量 |
|---|---|
| IL 字符串引用 | 11001（Assembly-CSharp）+ 238（firstpass） |
| 资产文本 | 1056 条序列化值 |
| 去重后候选 | **2383**（ui 1261 + other 1093 + …） |
| 丢弃 | 约 2700 条（标识符 1001 / PascalCase 983 / 库异常 300 / 资源名 208 …） |
| 来源 | both 297 / asset 174 / dll 1912 |

> **数字口径**：`ui` 不是"一定会显示"，是"几乎一定"；`other` 里约 900 条是单词，
> 需要人工判定（`Yes` 要翻，`GalaxyNavMapOpen` 不翻）。**不要拿 ui 条数当工作量上限**。

**词表骨架**（`_dict/`）

```
_dict/
├─ strings.json     ★ 唯一词表。每条 {en, zh, grade, cat, kind, cls, refs, note}
│                   翻一条 = 把 zh 从 "" 填成中文。--merge 重跑不会丢译文。
├─ glossary.md      术语表。改译名只能在这里改，译文里不许随手换。
├─ zh_cn.dict       由 build_dict.py 生成，插件直接读（格式见脚本头注释）
└─ STATUS.md        由 dict_stats.py 生成，勿手改
```

**二进制词表格式**（`zh_cn.dict`，小端）

```
"CWH1"                      4 字节
int32 count                 条数
count × { uint16 klen, UTF-8 en, uint16 zlen, UTF-8 zh }
uint32 FNV-1a               校验（★ 无符号：用 ReadUInt32，别用 ReadInt32）
```

**已定的译名**（见 `glossary.md`）：**Creeper / Anti-Creeper / AC 保留英文**（2026-10-18 用户决定），
其余按术语表。注意 Creeper 保留英文后，中文里夹英文的排版问题要到 C1 实测时再看。

**工作流**

```powershell
$p = "_cw3hanhua\.venv\Scripts\python.exe"
& $p _cw3hanhua\tools\dict_stats.py            # 看还差多少、哪块没动
# 1) 写批次：_dict\batches\<名字>.json  {"items": {"英文原文": "译文"}}
& $p _cw3hanhua\tools\apply_translations.py --dry-run   # 先看匹配情况，问题会列出来
& $p _cw3hanhua\tools\apply_translations.py             # 只填空，不覆盖
& $p _cw3hanhua\tools\check_charset.py _dict\strings.json   # 缺字就补烘（★ 别写成 _cw3hanhua\_dict\...，那样会变 0 文件扫描）
& $p _cw3hanhua\tools\build_dict.py --include-other  # 编出 zh_cn.dict（要带上 other 档，否则单词级 UI 词不进包）
```

**译文纪律**

- 只改 `batches/*.json`，**不要手改 `strings.json`**：`apply_translations.py` 会用原文精确匹配，
  手改容易和批次对不上，重新收集时会被覆盖。
- 译文里的 `[颜色]` 标记、`\n`、首尾空格**必须保留**。像 `PACKET SPEED.  `（两个尾空格）
  是拼接前缀，尾空格删掉就会和后面的数字粘在一起。
- 组合键（`SHIFT-E` / `SHIFT-End` / `CTRL-C`）整串保留英文 —— 玩家要按着它去找键。
- 改术语先改 `glossary.md`，再批量改批次。

### 4.3 文本层插件（`CW3HanhuaText.cs` v0.8.0）

**为什么单独一个 DLL**：字体层已验证通过、不想动；两层独立开关，出问题时能立刻判断是谁的锅。
BepInEx 5 按文件名顺序装载插件，`CW3Hanhua.dll` → `CW3HanhuaText.dll` 的顺序是**字母序碰巧正确**的，
**改文件名可能改变装载顺序**。文本层的 prefix 装在 `Awake` 里（早于场景创建），所以能拦到首批文本。

**三条替换路径**

| 路径 | 覆盖什么 | 实现 |
|---|---|---|
| ① `UILabel.set_text` prefix | 运行时动态文本（322 个调用点） | 改写 `__0`（**改参数，不改 `__result`** —— 见 5.6 第一个假象） |
| ② 周期 sweep：所有 UILabel | 资产里序列化的 `mText`（**不经 setter**，见 3.3） | 每 30 帧 `FindObjectsOfTypeAll`，含未激活 |
| ③ 周期 sweep：所有 TextMesh | 主菜单大按钮等 15 个写入点 | 同上 |

**改文本会触发重复替换**：sweep 每 30 帧跑一次，如果不清标记，译文会被当成新原文再查一遍
（虽然查不到，但会浪费并污染"未命中"日志）。插件用 `swaps` 列表按引用去重。
**换场景时 `swaps.Clear()`** —— 旧引用全死了，不清会让新场景的对象被误判为"已替换"。

**按键**：F4 开/关 | F5 单次 sweep（详单日志）| F9 重载词表（改完 dict 不用重启游戏）

### 4.5 全量翻译（2026-10-18，词表 1140 / 2383）

**结论**：`strings.json` 2383 条里，**1140 条已译**（ui 880 / other 260），
**剩余 1243 条按设计不译**。评测口径：`plugins/CW3HanhuaText.dll` 读的 `zh_cn.dict`
从 153 条涨到 **1140 条（49 327 字节，FNV 校验通过）**，`check_charset.py` 报
`用到字符 943 个（汉字 857）✓ 汉字全部在字集内` —— **没有新增缺字，不用重烘图集**。

`kind` 维度（判断"玩家到底看不看得见"）：
| kind | 条数 | 已译 |
|---|---:|---:|
| UILabel | 454 | **369** |
| TextMesh | 17 | **15** |
| None（纯 DLL 字符串，多为日志/资源名） | 1912 | 756 |

未译的 85 条 UILabel/TextMesh 全部是**故意不译**的：单字母 `A`–`Z`、`F0`–`F12`、
`SHIFT`/`CTRL`、`SHIFT-A`…`SHIFT-X`、`CTRL-C`/`CTRL-V`（组合键规则）、
`Alternate`/`Backslash`/`Equals`/`Escape`/`Minus`/`Space`/`Tab`（按键绑定名）、
`Alpha1`–`Alpha8`、`CN 1`–`CN 3`、占位文案（`This is a message`/`This is a great big long label`/
`View Demo Screen`/`Time:1:00:00.0`）。**换句话说：可见 UI 文本已清零。**

**剩余 1243 条的桶（`strings.json` 自算，可复现）**

| 桶 | 条数 | 为什么不译 |
|---|---:|---|
| 第三方库/框架日志 | 471 | VectorLine / CrplCore / CrplCompiler / AudioToolkit / NGUI / SGT_* / iTween / SevenZip 等，写 `Debug.Log`，控制台才看得到 |
| 标识符/资源名 | 237 | `PRODUCTIONINTERVAL`/`ACBackground`/`1XButton`/`Andro` 之类，改了就断 |
| 程序化星球名 | 146 | `RandomPlanetName` 音节碎片（`Aber`/`Abra`/`Ale`…），拼出来的名字要保留原文风味 |
| 其它未译 | 133 | 见上（按键名、占位文案、`', a = '` 调试碎片、`{0:D}:{1:D2}` 格式串） |
| 自定义图资源名 | 118 | `CustomImages` 的 `Custom0`/`Custom0_256`，资产 key |
| Steam 成就日志 | 61 | `StatsAndAchievements` 的 `m_flGameDurationSeconds: ` / `STAT testStat1: ` |
| 文件名 | 36 | `*.dat`/`*.xml`/`maps/`/`thumbnails/` |
| 调试日志 | 17 | `ops` 里带 `call Log` |
| 1–2 字符符号 | 13 | `()`/`<<`/`>>`/`-c`/`~` |
| 哈希 | 11 | 32 位 hex |

**新增工具**（都在 `tools/`，Python 用 `.venv`）

| 脚本 | 作用 |
|---|---|
| `prep_batches.py` | 把未译项按规则分流（不译桶 + 待译），切 `full_NN.src.json` |
| `merge_work.py` | `full_NN.src.json` + `full_NN.zh.json` → `_dict/batches/full_NN.json` |
| `check_batch.py` | 译稿校验：条数/首尾空格/`\n` 数/`[标记]`/占位符，**不过 0 不许入库** |
| `audit_translations.py` | ★ 全表审计（en/zh 的标记、换行、占位符、首尾空白），**四项全 0 才算过** |
| `pack_skips.py` | 把 `work/*.zh.json` 里标 `@` 的「人工判定不译」固化到 `_dict/skipped_manual.json` |

**工作目录 `_dict/work/` 已清理**（2026-10-18）：`full_NN.src.json` / `.zh.json` /
`_skipped.json` / `left_01.json` 已删——译稿全部并入 `_dict/batches/`（22 份、1140 条，
`apply_translations.py --dry-run` 复核过「新增 0、已相同 1140」）。删前用
`pack_skips.py` 把 565 条「人工判定不译」固化成 `_dict/skipped_manual.json`（94 KB），
**这个文件别删**：它记录了"这些条目是特意跳过的"，否则以后有人会重翻一遍。
若还要再切批次：`prep_batches.py` 会重建 `work/`。

**译稿格式的坑**：`{"zh": ["...", "..."]}` 数组形式**漏一项就整体错位**（踩过一次：89 vs 88，
后 60 条全串行）。`check_batch.py` 已支持并**推荐**序号字典 `{"zh": {"3": "译文"}}`，
缺省项视为 `@`（跳过不译），不会错位。

**批次清单**：`full_01`…`full_18`（共 745 条）+ `full_19_uiwords`（97 条单词级 UI）+
`full_20_words`（118 条）+ `full_21_lastui`（27 条：`[ff0000]Sound Off`/`Return`/`PURCHASED`/DMD 表头等）。
`full_03`/`full_16` 全跳（音频库与 VectorLine 日志）。

**几条译法备忘**（术语表之外新定的）：
- `Return` → `返回`（`ColonialSpaceManager.DrawMapBox` / `WorldMachineManager.DoWorldMachineWindow` 的按钮，不是回车键）
- DMD 名册表头：`Owner`→`作者`、`Map`→`地图`、`Started`→`创建`、`Played`→`游玩`、`Plays`/`Plays`→`次数`、`Filter`→`筛选`、`Score`→`得分`、`Player`→`玩家`
- `[00e000]FLOWING`→`[00e000]流动中`，与已有的 `[ff0000]PAUSED`→`[ff0000]已暂停` 对齐
- `Targets: `/`Price: ` 这类**拼接前缀**照样译，靠 `check_batch.py` 保住尾部空格
- **`Author:`/`Email:`/`Desc: (`/`Screen Res: (`/`Apply Filters (`/`UploadMap: ` 已译**（它们确实出现在地图信息面板上）；`', a = '` 系列调试碎片不译

**下一道坎**：`zh_cn.dict` 已更新，但插件**仍未实测**（B2/B3 都是 🟡）。
改词表后游戏内按 **F9 重载** 即可，不用重启游戏。

### 4.4 ★ 合并图集方案（2026-10-18，解决遮挡的最终方案）

**思路**：不拆 draw call —— 把中文字形并进 GUI Atlas 本身，`UIFont.mAtlas` 继续指向它。

**离线：`tools/build_atlas.py`**

```
GUI Atlas 原图 512x1024（RGBA32 + 11 级 mip）
  → 解出（UnityPy 读 typetree 的 'image data'，取前 w*h*4 字节，PIL 翻转）
  → 新建 4096x8192（maxTextureSize 实测 16384，够）
  → 原图 1:1 贴【PIL 顶部 (0,0)】→ 101 个 sprite 的 outer/inner 一个都不用改
  → 中文字形从 y=1025 往下排：16(1025..1764) / 20(1766..2845) / 28(2847..4734)
  → 余下 4736..8191 是空档（3456 行，留给将来补字）
产物：_font/atlas_zh.png（9.9MB 文件 / 128MB 显存）
      _font/atlas_zh.bin（707KB，插件直读；格式 CWA1 + W/H/nSizes + 每字号字形表 + FNV）
```

#### ⚠⚠ 坐标约定：精灵和字形都是【顶左原点】（踩了四轮才对）

```csharp
// NGUIMath.ConvertToTexCoords —— 这个翻转换算成立，前提就是 rect 本身是顶左原点
result.yMin = 1f - rect.yMax / height;
result.yMax = 1f - rect.yMin / height;

// UIFont.Print（反编译原文）
float num8 = uvRect.width  / (float)mFont.texWidth;
float num9 = mUVRect.height / (float)mFont.texHeight;
zero3.x = mUVRect.xMin + num8 * (float)glyph.x;
zero3.y = mUVRect.yMax - num9 * (float)glyph.y;
zero4.y = zero3.y - num9 * (float)glyph.height;
```

**两边同向**：`sprite.outer` 和 `glyph.y` 都从**图像顶部**往下量。
`mUVRect=(0,0,1,1)` 时 `yMax=1`（顶部），`glyph.y` 越大越往下。

**像素级实证**（怎么确认的，别再猜一遍）：取已知精灵 `'NGUI' outer=(0,646,255,75)`，
在原图上裁两块对比 —— PIL 行 646..720（从顶量）有 19087 个非透明像素，目视就是 NGUI logo；
行 303..377（从底量）只有 1523 个零碎像素。**顶左原点成立。**

**原图放顶部的原因**：`build_atlas.py` 顶部断言 —— 中文字形 `y_top < 1024` 就直接报错退出，
物理上不可能压到原图区。

#### ⚠⚠ 灰度图必须转成 alpha（第二个 bug）

`font_XX.png` 是 PIL **`L` 模式**（白字黑底）。直接 `convert("RGBA")` 会把黑底变成
**alpha=255 的不透明黑块**（实测成品像素 `(0,0,0,255)` 满屏）。

```python
g = Image.open(glyph_png).convert("L")
gimg = Image.new("RGBA", g.size, (255, 255, 255, 0))
gimg.putalpha(g)          # RGB 全 255，alpha 承载覆盖率
```

理由：draw call 的顶点色（`label.color` / 富文本标记）会乘上纹理 RGB，所以 RGB 必须全白。

#### ⚠⚠ 换纹理后必须手动重算精灵 UV（第三个 bug）

`UISprite.UpdateUVs` 里 `mOuterUV = ConvertToTexCoords(mOuter, texture.width, texture.height)`
的结果**缓存在 `mOuterUV` 字段**，换纹理不会自动失效 → 旧的 UV（按 512×1024 算）套到
4096×8192 上，精灵采样到完全错位的地方（症状：背景错位/撕裂）。
`RefreshAllSpriteUVs()` 反射调 `UISprite.UpdateUVs(true)` 遍历所有精灵，**开和关都要调**；
且该方法只写字段**不置 `mChanged`**，所以之后必须补 `s.MarkAsChanged()`。

**运行时：`EnsureMergedAtlas()`**

```csharp
sm.mainTexture = mergedTex;        // 只换图集材质的主纹理
// 不改 spriteList、不改 renderQueue、不新建材质、
// ★ 不动 mainTextureScale / mainTextureOffset（裁剪 draw call 每帧会写它们，共享材质一动就打架）
```

**UIFont 的改动（与旧方案的关键差异）**

| 字段 | 旧方案（拆 draw call，已废弃） | 新方案（合并） |
|---|---|---|
| `mAtlas` | **置 null** | **保持不动** |
| `mMat` | 新建独立材质 | 指回 `atlas.spriteMaterial` |
| `mUVRect` | (0,0,1,1) | (0,0,1,1) |
| `mFont.texWidth/Height` | 2048×4096 等 | **4096×8192**（大图集尺寸） |
| `mDynamicFont` | null | null（走位图分支） |
| `mPMA` | — | -1 |

★ **`mAtlas` 必须保持指向 GUI Atlas** —— `UIFont.material` getter 里
`if (mAtlas != null) return mAtlas.spriteMaterial;` 优先级最高，所以文字和精灵自动共用
同一份材质 → 同一个 draw call → 遮挡关系恢复原版。这正是整套方案的意义。

**为什么 UV 不用改 spriteList**：`UISprite.UpdateUVs` 的换算是
**每次刷新时用「精灵像素坐标 ÷ 纹理尺寸」实时算**的。原图 1:1 贴顶部，`outer` 像素坐标不变
→ 重算后 UV 自动正确。**但重算必须手动触发**（见上面的 UV 缓存坑）。

**F3 关闭时必须还原纹理**，否则精灵仍按 4096×8192 的 UV 采样 512×1024 的图，整个 UI 错乱。
`RestoreFont()` 里做了这件事（备份 `origAtlasTex/Scale/Offset`），
**但不要 `Destroy(mergedTex)`** —— 裁剪 draw call 的 `mClippedMat` 可能仍引用它。

**★ 实测结论（2026-10-18，用户实测通过）**：合并图集方案可行。
四个坑全部修完后的状态：F3 开 → 中文正常显示、遮挡关系与原版一致、无方块；
F3 关 → 完整还原。详见第一节状态表。

**探针全部退役**：三个图集探针（`AtlasProbe` / `AtlasWriteProbe` / `UIFeasibilityProbe`）
已连源码一起删除（2026-10-18 清理），结论都在 4.4 —— 图集可写、显存够、`maxTextureSize=16384`。
只剩 `MiniProbe.cs`（`CW3HanhuaMini.dll`）作运行时自检。
`build_plugin.cmd` 只编 `CW3Hanhua.cs` 和 `CW3HanhuaText.cs` 两个。

**踩过的坑：词表校验恒失败**（2026-10-18 实测发现）
`build_dict.py` 算 FNV 的范围是 `raw[8:-4]`（magic + count 之后），
C# 端一开始只累加了 key/zh 而漏掉 `uint16` 长度前缀，两边字节流不同 → 恒报"校验失败"。
**数据本身是好的**（153 条都正确读入），但校验形同虚设。
修法：C# 端把长度前缀也写进 `body`，与 Python 逐字节对应。
> 教训：**自检要跨语言**。Python 内部往返自检通过 ≠ C# 端也对；
> 必须拿同一份产物在**两个实现**里各算一遍。

**环境陷阱（本轮踩到）**

1. **pip install 在本项目 venv 里会失败**：沙箱下 pip 往临时目录写 `*.whl.metadata` 报
   `PermissionError`。解决办法：用 `urllib` 下 wheel，再 `zipfile` 解到 `site-packages`。
   `dncil`（dnfile 配套的 IL 解析器）就是这样装的，**已装好**。
2. `dnfile` 自己**不能解析方法体 IL**（`MethodDef.get_method` 不存在），必须配 `dncil`。
3. `dncil` 用 `CilMethodBody(CilMethodBodyReaderBytes(raw[off:]))`，
   `off = pe.get_offset_from_rva(m.Rva)`；Token 是 `(table<<24|rid)`，**没有** `Token.Type`。
4. 用 `UnityPy` + `TypeTreeGenerator("5.2.3f1")` 读资产会刷
   `The given key 'Assembly-UnityScript.dll' was not present` —— **无害**，只是那几个类解析不出来。
5. `_cw3hanhua\_tmp\` 里的 pip 残留目录（pip 以更高完整性创建，Low IL 的 shell 连 ACL 都读不了）
   **需用户手动删**，已于 2026-10-18 清理完毕。以后装包优先走 `zipfile` 解压，别用 pip。

### 4.1 字体烘焙与插件按键

**插件按键（`plugin/CW3Hanhua.cs` v0.8.0）**

| 键 | 作用 |
|---|---|
| **F3** | 字体层开关（换合并图集 / 还原）—— 主功能 |
| **F11** | dump 到 `BepInEx/CW3HanhuaFont.txt`（draw call / 材质 / 队列） |
| **F12** | HUD 开关 |

文本层插件另一套：**F4** 开关 / **F5** 单次 sweep / **F9** 重载词表。

**装载流程**（v0.8.0，详见 4.4）：读 `atlas_zh.png` 建 4096×8192 纹理 → 换
`atlas.spriteMaterial.mainTexture` → 重算全部 `UISprite.UpdateUVs(true)` + `MarkAsChanged()`
→ 读 `atlas_zh.bin` 建 BMFont → 反射填 `mFont`/`mUVRect`/`mDyn`/`mPMA`（**`mAtlas` 不动**）。

**字形烘焙**（产出 `font_<size>.{png,json}`，是 `build_atlas.py` 的输入）

```powershell
# 当前参数：雅黑 Bold + 0.80
& .venv\Scripts\python.exe tools\bake_font.py --font "C:\Windows\Fonts\msyhbd.ttc" --scale 0.80
# 纵向单独压（汉字满格、Latin 偏窄，等比缩放无法两方向同时对齐）
... --scale 0.80 --scale-y 0.76
# 缺字时（check_charset.py 会直接打印这条命令）
... --extra "缺的字"
```

烘完必须重跑 `build_atlas.py` 才能生效。

**字集**：GB2312 全集（符号区 + 一级 3755 + 二级 3008）+ ASCII 95 + 常用符号 = **7544 字**。
- **必须含符号区**：中文标点全在 GB2312 1–9 区
- **必须含二级**：渲 / 阈 / 阱 这类常用字在一级（16–55 区）之外

**调字号的两个已知约束**

1. **当前 0.80 是靠英文调的**，中文观感可能不同，等有真正中文文本要复调
2. 雅黑 Bold 笔画粗、覆盖率高于 Aldrich，所以**粗体要比常规体用更小的 scale**

---

## 五、环境与陷阱

### 5.1 沙箱写入权限

游戏重装后，shell 的受限进程可能无法写工作区（连游戏目录根都不行），但磁盘权限本身正常
（`diagnose-windows-sandbox-acl` 判定 `NOT_THIS_CLASS`）。
**`write`/`edit` 文件工具仍正常**；`csc.exe` 输出 DLL 可能失败。
2026-10-17 实测已恢复，可正常写入。

### 5.2 编译插件

**用 `_cw3hanhua\tools\build_plugin.cmd`（一次编出两个 DLL）**，不要手敲命令：

```powershell
& "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3\_cw3hanhua\tools\build_plugin.cmd"
```

它内部等价的单条命令：

```powershell
$g = "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3"
& 'C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe' /nologo /target:library /platform:x86 /codepage:65001 `
  /out:"$g\BepInEx\plugins\XX.dll" `
  /reference:"$g\BepInEx\core\BepInEx.dll" `
  /reference:"$g\BepInEx\core\0Harmony.dll" `
  /reference:"$g\CW3_Data\Managed\UnityEngine.dll" `
  /reference:"$g\CW3_Data\Managed\Assembly-CSharp.dll" `
  "源文件.cs"
```

- **`/platform:x86` 必须**（游戏 32 位）
- **`/codepage:65001` 必须**（源码含中文，否则 csc 3.5 按 ANSI 读成乱码）
- **必须引用游戏自带 `Managed` 目录的 DLL**（Unity 打包版 `mscorlib 2.0.5.0`）
- 目标框架只能是 **net35**（游戏跑在 Mono 2.x）。用 VS2022 的 Roslyn 也能编 net35，
  但要 `/noconfig /nostdlib+` 并显式引用游戏 DLL；用 4.0+ csc 按默认方式编会引用
  `mscorlib 4.0`，加载即失败
- **`.cmd` 必须是 ASCII + CRLF**：cmd.exe 按 OEM 代码页（936）解析批处理，
  写 UTF-8 中文注释会被切碎成乱码命令（2026-10-18 踩过：报一堆
  `'★失败' is not recognized as an internal or external command`）。

### 5.3 C# 3.0 的坑（用 csc 3.5 时）

1. **没有命名实参、没有可选参数、没有字符串插值**
2. **HarmonyX 的 `AccessTools.Method` 是 4 参数带默认值** → 必须写全
   `AccessTools.Method(t, "name", null, null)`（`AccessTools.PropertySetter(t,n)` 只有 2 参数）
3. **`Harmony.Patch` 用 6 参数重载**：不用的槽位传字面 `null`，**不要**传 `new HarmonyMethod(null)`
4. **游戏程序集里有全局 `Path` 类** → 写 `System.IO.Path.Combine`
5. Unity 5.2 没有 `SceneManagement` → 用 `Application.loadedLevelName`
6. `UIPanel` 没有 `SetDirty()` → 用 `UILabel.MarkAsChanged()`
7. `UIFont` 是 MonoBehaviour → 运行时用 `new GameObject().AddComponent<UIFont>()`

### 5.4 工具链

**主用 `tools\ilx.exe`（Mono.Cecil 0.10.4）**：

```powershell
$t = "C:\...\Creeper World 3\_cw3hanhua\tools"
& "$t\ilx.exe" dump UILabel set_text              # 精读 IL
& "$t\ilx.exe" callers set_enabled                # 谁调用了它
& "$t\ilx.exe" callers --type UILabel set_text    # 按声明类型过滤
& "$t\ilx.exe" fields enabled                     # 谁直接读写字段
& "$t\ilx.exe" find MainMenu                      # 按名字搜类型/方法/字段
& "$t\ilx.exe" stats                              # 全程序集方法清单
```

**★ `tools\ilspy.cmd <dll> -t <类型>` 把方法反编译成 C#** —— 本项目最有效的排查手段。
NGUI 行为怪异时**先反编译再推理**，产物在 `_cw3hanhua/_diag/`。

**⚠ 手写 IL 解析器曾有三类解码 bug，历史数字被污染过**：tiny header 用 `hdr>>2` 取 size
（8289 个方法里 3992 个读多）、`throw` 标成 4 字节、`conv.ovf.*`/`ldftn`/`constrained.`
整段缺失导致失步。后果：`set_enabled` 曾统计成 371（真 **249**）、`UILabel.set_text` 曾
292（真 **322**）、`TextMesh.set_text` 曾 12（真 **15**）。修好后与 `ilx` 在全部 8289 个
方法上指令数完全一致。
→ 手写解析器 `il.py` / `ilscan.py` / `ilscan_field.py` 已删除（2026-10-18），
**只信 `ilx`**。（教训留在这一行就够了，不必留着一份会记错的代码。）

**其它**：早期全量文本扫描 `scan_all_text.py` 已删除，**现行口径是 `tools\collect_ui_strings.py`**（见 4.2）。
Python 脚本一律用 `_cw3hanhua\.venv\Scripts\python.exe` 跑。

### 5.5 游戏状态文件的坑

`%APPDATA%\creeperworld3\GameSettings.xml` **声明 `encoding="utf-16"，实际是 UTF-8 无 BOM**。
按声明读会乱码，写回会写坏。改前**先备份**，显式按 UTF-8 读写。

### 5.6 Harmony 补丁的六个假象（勿重走）

| 假象 | 真实原因 |
|---|---|
| 误判「`__result` 改写失效」 | 改写补丁被传进了 **prefix** 槽位；prefix 里改的值会被原方法返回值覆盖 |
| 补丁全部不生效，抛 `ArgumentNullException` | prefix 写成 `new HarmonyMethod(null)`（包装了 null 方法） |
| 误判「Harmony detour 无效」 | 自检目标是**空方法**被 JIT 内联；加 `[MethodImpl(NoInlining)]` 后正常 |
| HUD 探测值无意义 | 压根没给被显示的那个方法打补丁 |
| HUD 文字黑字看不清 | `GUI.color` 污染后续绘制，画完 `GUI.Box` 未恢复 |
| 场景 UI 枚举全为 0 | 枚举放在 `Awake`，此时场景 UI 尚未创建 |

---

## 六、环境故障档案：「SteamManager.Awake 不执行」

> 与汉化无关，但会让**游戏完全不能玩**。根因已查清，**触发条件未查明**（当前无法复现）。

### 6.1 指纹与症状

**`BepInEx/CW3Mini.txt` 的最终判定行**（`plugin/MiniProbe.cs` v2.0 产出）：
`★ 故障特征：计数 0 且两个标志均为假` ← 只有这一行才是命中。

症状：3D 按钮**能悬停、点不动**；CW5 广告消失；NGUI 按钮（Exit Game / Music Credits）正常；日志无异常。

> ### ⚠ 6.1a v1.0 探针的假故障（2026-10-18 查实，**已修**）
>
> **v1.0 报的 `SteamManager.Awake 次数 = 0` 不可信，≠ 故障。** 用户实测主菜单一切正常。
>
> **根因：时序竞争。** v1.0 在插件 `Start()` 里 `harmony.Patch`，而 `SteamManager` 是第一个被创建的
> 脚本，它的 `Awake` 早在插件 `Start` 之前就跑完了 → 补丁挂上时方法已经执行过 → 永远记 0。
>
> **铁证**：同一份产物里 `s_instance = 非空`、`s_EverInialized = True`、`m_bInitialized = True`
> —— 这三个字段**只有 `Awake` 会写**（IL 实证：`0x0027 stsfld s_instance`、`0x00C5 call SteamAPI.Init`
> → `0x00CA stfld m_bInitialized`）。值全对而计数为 0，只能是补丁没拦到。
>
> **v2.0 的修法**（`plugin/MiniProbe.cs`，已编译进 `BepInEx/plugins/CW3HanhuaMini.dll`）：
> 1. 改用 Harmony **静态补丁类**（BepInEx 的构建时补丁器在加载 `Assembly-CSharp` 时装补丁，早于任何 `Awake`）
> 2. **三路交叉验证**：计数 + `s_EverInialized` + `m_bInitialized`；只要后两个为真而计数为 0，
>    一律判「探针失效」而**不判故障**
> 3. 运行时兜底安装前先查 `s_EverInialized`，已为真就明确标注「晚装，计数无意义」
> 4. 新增 `MainMenu.Awake` 的 pre/post/finalizer —— 主菜单是否真的被异常打断，从此有直接证据
>
> **另一个必须记住的坑**：`SteamManager / MainMenu` 是 **internal 类**，
> `[HarmonyPatch(typeof(T), ...)]` 编不过（泛型约束要 public），**必须用字符串形式**
> `[HarmonyPatch("SteamManager", "Awake")]`。
>
> **为何不能靠重新调用 `get_Instance` 来补救**：它是懒加载，`s_instance` 非空就原样返回，
> 不会重新触发 `Awake`；而 `Awake` 在 `s_EverInialized=True` 时会 `throw`（IL `0x0036`），
> 任何 late hook 都碰不得。只读字段才是安全手段。
>
> ### ⚠ 6.1b 静态补丁类也来不及（2026-10-18 实测，v2.1 已加自检）
>
> v2.0 的假设「静态补丁类早于所有 `Awake`」**是错的**，实测证据（2026-10-18 12:20:54 启动）：
>
> | 观测 | 推论 |
> |---|---|
> | 插件 `Awake` 时 `s_instance=非空 s_EverInialized=True` | `SteamManager` 已在插件 `Awake` **之前**完成初始化 |
> | 日志无任何 HarmonyX 关于静态补丁类的报错 | 不能靠「没报错」证明装上了 |
> | `OnEnable` 补丁**装上了**但 `次数=0` | 连 `OnEnable` 都在补丁安装前跑完了 |
>
> **机制**：BepInEx 的 `Chainloader`（HarmonyX 的静态补丁类扫描发生在这里）是在
> `SteamManager.Awake` **内部**被触发的。所以静态补丁类装得上，但对 `SteamManager.Awake`
> 而言已经太晚 —— **任何在 BepInEx 生命周期内安装的补丁都拦不到它**。
>
> **v2.1 的做法：不再猜，直接查**（`VerifyStaticPatchClasses()`）：
> 读 `SteamManager` 类型上的静态字段 `__HarmonyPatches`（`BindingFlags.Static|Public|NonPublic`，
> 注意 `AccessTools.Field` 只接受 `Type`，读方法上的字段得用裸反射），列出实际挂着的补丁项。
> 判定因此能区分四种情况：**补丁拦到了** / **补丁在但跑太晚** / **补丁压根不在** / **真故障**。
>
> **实测结论（2026-10-18 12:23:28 启动，v2.1）**：
> `补丁自检 = 静态补丁类未生效` —— `SteamManager.Awake` 的 `__HarmonyPatches` **完全为空**，
> 不是「装了但太晚」，而是**压根没装上**。HarmonyX 对静态补丁类的扫描没有覆盖到本 DLL，且
> **没有任何报错**（重要：日志干净 ≠ 补丁生效）。
> → 结论：**走 BepInEx 插件这条路，`SteamManager.Awake` 的补丁计数永远会是 0**，
> 不要再用计数当证据；一切以字段旁证为准。
>
> **对真故障判定的影响**：真故障下 `s_EverInialized` 与 `m_bInitialized` 必然都是假，
> 所以「计数 0 + 两标志全假」**不依赖补丁是否装上**，依然成立（这可能正是当初 v1.0
> 在真故障时也能报对的原因 —— 计数是假的，但结论碰巧对）。
>
> **v2.2/v2.3 补的第二条旁证（MainMenu）**：`MainMenu.Awake` 计数同样不可信。改为读场景对象：
> `MainMenu.Awake` 在 IL `0x0091` 才 `UberFind.Find("UI Root (2D)", "GameVersion")`。
> ⚠ **`UberFind.Find(root, child)` 不是"找 MainMenu 的子对象"** —— 反编译实证它是
> `GameObject.Find(root)` 全局找根 + `GetComponentsInChildren` 按名字找子。
> v2.2 首版用 `mm.transform.Find("GameVersion")` 找错方向，实测报"找不到"（12:25:11 启动）；
> v2.3 改成与游戏完全一致的两步查找。这条旁证**不依赖 mm 引用，也不依赖任何补丁**。

>
> **要真拦到 `Awake`，补丁必须早于 BepInEx 插件加载** —— 这条路本项目不走，
> 因为探针的定位是**只读诊断**，靠字段/场景旁证已经足够。
>
> **假象台账（新增，勿重走）**：
> | 假象 | 真相 |
> |---|---|
> | 日志里没有 HarmonyX 报错 → 补丁装上了 | `__HarmonyPatches` 为空，压根没装 |
> | 计数 = 0 → Awake 没执行 | 三个状态字段全为真，Awake 明显执行过 |
> | 静态补丁类 = 构建时安装 = 早于 Awake | Chainloader 在 `SteamManager.Awake` 内部才触发 |


### 6.2 因果链

```
系统级运行时状态异常（内核态/服务层，重启电脑才清除）
  ↓ SteamManager.Awake 从未执行（对象存在、enabled=True、finalizer 未触发 → 没被调用，不是崩溃）
  ↓ SteamAPI.Init() 从未调用 → m_bInitialized=False
  ↓ MainMenu.Awake → UpdateCheck → FileManager.GetKey() → SteamUser.GetSteamID()
  ↓ 抛 InvalidOperationException: Steamworks is not initialized.
  ↓ MainMenu.Awake 无 try/catch（异常子句数=0）→ Awake 中断
  ↓ NewsCheck() 排在后面 → 永不执行 → CW5 广告消失
  ↓ MainMenu.Start/Update 永不执行 → 3D 按钮点击无反应
  ↓ 组件最终 enabled=False（场景里 m_Enabled=1，没有任何代码关它）
```

**`enabled=False` 是异常中断的后果，不是被谁显式关闭的** —— 这是"谁关掉了 MainMenu"这个
长期悬案的答案：**问题问错了**。

**为什么只有 3D 按钮失效**：悬停走 Unity 的 `OnMouseEnter/OnMouseExit`（`EntryManager`），
NGUI 按钮走 `UICamera`，都不经过 `MainMenu.Update`。

### 6.3 关键 IL 证据

| 位置 | 事实 |
|---|---|
| `SteamManager.Awake` | `call SteamAPI.Init` → `stfld m_bInitialized` → `brtrue`，**失败即 LogError 后 ret** |
| `SteamManager.get_Instance` | **懒加载**：`newobj GameObject` + `AddComponent<SteamManager>()` |
| `FileManager.GetKey` | 判空 → 空则返回 `'steam-uninitialized'` → 否则 `SteamUser.GetSteamID()` |
| `MainMenu.Awake` | 异常子句数 **0** |
| 全程序集 `set_enabled` | **249** 处，**无一指向 MainMenu** |

### 6.4 已排除的假设

资产被改坏（回滚后照旧）· 插件逻辑（空插件照旧）· 代码关掉 MainMenu（249 处无一指向）·
场景里就是 disabled（`m_Enabled=1`）· HUD 挡输入 · 缺 Collider（都有，`RaycastAll` 能命中）·
游戏文件损坏（**重装**照旧）· BepInEx 破坏 Steamworks（**彻底移除**照旧）·
Steam 客户端问题（**重启 Steam** 无效，日志显示已 `Logged On 'OK'`）· AppID 未传（实测 `280220` 正常）·
缺 `steam_appid.txt` · 补丁打错程序集 · Steamworks.NET 报错（其 `SteamAPI_Init() failed` **从未出现**）

### 6.5 唯一有效的恢复手段

**彻底重启电脑。** 重装游戏、重启 Steam、移除 BepInEx 均无效
→ 病根在**系统级运行时状态**（WFP 内核过滤 / LSP / 内核命名管道 / Steam 网络栈残留）。
普通 DLL 注入只存在于进程内存，无法解释"活过游戏退出、活过 Steam 重启、活不过系统重启"。

> ⚠ 注意第一节硬规则：当时那个高完整性 Steam 进程**可能从未被真正结束**，
> 所以"重启 Steam 无效"这一步的排除范围需要重算。

### 6.6 复现时怎么取证

1. **看 `BepInEx\CW3Mini.txt` 的最终判定行**。只有
   `★ 故障特征：计数 0 且两个标志均为假` 才算命中；
   看到「探针失效」一律**不是**故障（见 6.1a）。同时看 `MainMenu.Awake 次数 / 异常次数`：
   故障时应当出现「`MainMenu.Awake` 抛异常」。
2. 冻结系统现场：采一份指纹快照存到 `_diag\fingerprint_<时间戳>.txt`。
   （`collect_fingerprint.ps1` 已于 2026-10-18 删除——它只是一次性取证工具，
   判读要点全在本节，需要时现写即可。）
   重点看管道（第 3 节）、`netsh wfp show state`（第 4 节）、非微软 LSP（第 5 节）、`gameprocess_log`（第 9 节）
3. 记录坏掉之前最后做过什么

> 第 4、5 节需管理员权限；文件用 **UTF-8 with BOM** 保存
> （PowerShell 5.1 读无 BOM 的 UTF-8 会把中文当 ANSI）。

### 6.7 对汉化的影响

**没有影响**（汉化只操作 UI 字体与文本）。但**分发时要考虑**：玩家机器若也出现此故障，
汉化装了也没用（游戏本身跑不起来）。

### 6.8 BepInEx 的重装与登记

> `_bepinex_backup/` 已于 2026-10-18 删除（占 2 MB，且 BepInEx 重装成本很低）。
> **改过的只有一处**，就是下面的 `BepInEx.cfg`；其余文件都是 BepInEx 原版。

要恢复：从 [BepInEx 5.4.23.5 x86](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5)
重新解压到游戏根目录，然后把 `BepInEx\config\BepInEx.cfg` 的 `[Preloader.Entrypoint]`
改成下面三行（**原版三行是 `UnityEngine.dll` / `Application` / `.cctor`**，改回去即还原）：

```ini
Assembly = Assembly-CSharp.dll
Type = SteamManager
Method = Awake
```

装完检查三件事：

```powershell
$g = "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3"
Test-Path "$g\winhttp.dll"                     # 必须 True
Get-Content "$g\.doorstop_version"             # 应含 4.5.0
Get-Content "$g\BepInEx\config\BepInEx.cfg" -Encoding UTF8 |
  Select-String -Pattern '^Assembly|^Type|^Method'   # 三行 = 上面的值
```

---

## 七、附录：BepInEx 与 NGUI 版本

### A. BepInEx 注入方案

**BepInEx 5.4.23.5 (x86) 完全可用。** 唯一必需改动是 `BepInEx.cfg` 三行：

```ini
[Preloader.Entrypoint]
Assembly = Assembly-CSharp.dll
Type = SteamManager
Method = Awake
```

默认值（`UnityEngine.dll` / `Application` / `.cctor`）**必须改**，否则必崩：
`Application..cctor` 在 Mono 装载引擎程序集时就被触发，此时 Unity 对象系统未就绪，
`AddComponent(Type)` 会原生 AV（`0xC0000005`）。

- `BepInEx.dll` **不需要修改**：`SHA256 = 8255B28902886085C578B9E427D3073C97002DB85176D2090CDEDA90EF14CE70`
- **必须用 BepInEx 5**：be6 的 MonoMod 在这套老 Mono 上 `ReflectionHelper` 静态初始化 NRE，换注入点也绕不过
- **无害警告** `Could not find property ... isBatchMode`（Unity 5.6+ API）——反是 `AccessTools` 初始化成功的正面信号
- **启动方式**：Steam 正版目录从 Steam 启动不需要 `steam_appid.txt`；副本目录直接双击 `CW3.exe`
  需要同目录放该文件（内容 `280220`）。两种情况都需要 **Steam 客户端正在运行**
- **卸载**：删 `winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`BepInEx\` 四项。
  升级/重装**务必保留 `BepInEx.cfg`**，否则注入点被还原，游戏无法启动
- 其它可用注入点（全局命名空间）：`SteamManager`（已验证）、`Localization`

### B. NGUI 版本定位

CW3 的 NGUI 代码编译在 `Assembly-CSharp.dll`（无命名空间）。最接近的官方提交是 `625abae8`
（2013-09-03），但**只对 `UILabel` 成立**：CW3 的 `UIFont` 是更晚的修订——有
`RecalculateDynamicOffset`、**没有** `Request(string)`、动态分支不受 `DYNAMIC_FONT` 宏控制。
**所以读 `UIFont` 一律以反编译为准**（`_diag/UIFont.cs`）。

要拿上游 `UILabel` 源码：`_ngui_repo/` 里有完整历史，
`git -C _ngui_repo show 625abae8:Assets/NGUI/Scripts/UI/UILabel.cs`。

`UILabel` 判别特征：26 个字段，**同时具备 `mLastScale` 且不具备 `mFontSize`/`mFontStyle`/`mOverflow`**。
仓库 clone 在 `_ngui_repo/`（只有 `master`，无 tag）。

---

## 八、目录与维护纪律

```
_cw3hanhua/
├─ NOTES.md            ← 本文（唯一文档）
├─ tools/              ★ 全部可复现工具（见 tools/README.md）
│   ├─ build_plugin.cmd       一次编出两个插件 DLL（唯一编译入口，5.2）
│   ├─ ilx.exe / ilx\ilx.cs    基于 Mono.Cecil 的 IL 检查器（5.4）
│   ├─ ilspy.cmd / ilspycmd/   反编译成 C#（5.4）
│   ├─ bake_font.py            烘字形（4.1）
│   ├─ build_atlas.py          ★ 合并图集 4096×8192 生成器（4.4）
│   ├─ check_charset.py        字集覆盖检查
│   ├─ collect_ui_strings.py   静态全量收集（4.2）
│   ├─ build_dict.py           编译 zh_cn.dict
│   ├─ apply_translations.py   批次译文注入 strings.json
│   ├─ audit_translations.py   ★ 全表一致性审计（标记/换行/占位符/首尾空白）
│   ├─ prep_batches.py         未译分流 + 切批（会重建 _dict/work/）
│   ├─ merge_work.py           work/ 译稿 → batches/
│   ├─ check_batch.py          单批译稿校验
│   ├─ pack_skips.py           「人工判定不译」固化 → _dict/skipped_manual.json
│   └─ dict_stats.py           覆盖率报告 → _dict/STATUS.md
├─ plugin/             ★ 两个在用源文件：CW3Hanhua.cs（字体层 v0.8.0）
│                      CW3HanhuaText.cs（文本层 v0.8.0）
│                      MiniProbe.cs（探针 v2.3，补丁计数不可信，见 6.1a）
├─ _font/              ★ 运行时：atlas_zh.png + atlas_zh.bin（字体层的全部输入）
│                      ★ 工具链：font_{16,20,28}.{json,png}（重烘图集的必需输入）
├─ _dict/              ★ 词表：strings.json（唯一数据）/ glossary.md（术语表）
│                      / zh_cn.dict（编译产物）/ STATUS.md（自动生成）/ batches/（22 份批次）
│                      / skipped_manual.json（565 条「人工判定不译」，勿删）
├─ _diag/              ilspy 反编译源码：UIFont / UIAtlas / UISprite / UILabel /
│                      UIPanel / UIDrawCall / UIWidget / BMFont / NGUITools
│                      ★ 改插件前必查；不进版本库（gitignore），可随时重生成
├─ _ngui_repo/         NGUI 仓库 clone，77 MB（gitignore；查上游源码用）
└─ .venv/              Python 环境（UnityPy / dnfile / dncil / fontTools / Pillow，gitignore）
```

`_diag/` **不进版本库**：里面全是 `tools\ilspy.cmd` 一次就能重生成的反编译产物
（例如 `& tools\ilspy.cmd CW3_Data\Managed\Assembly-CSharp.dll -t UIFont`）。
它叫 `diag` 是因为最初是个「诊断产物」目录，后来实际装的都是反编译源码，名字沿用下来。

**版本控制**：仓库根 = 游戏安装根目录（`.git` 与 `CW3.exe` 同级）。
`.gitignore` 用「顶层白名单 + 子目录黑名单」，只放行 `_cw3hanhua\`、`BepInEx\config\`、
`AGENTS.md`、`doorstop_config.ini`。
★ **本目录就是游戏安装目录，永远不要在这里执行 `git clean`** —— 会连游戏本体一起删掉。

### 维护纪律

1. **只维护这一份文档。** 不要新建 `XXX.md` —— 新内容写进对应章节。
2. **控制篇幅，但不要为行数牺牲信息。** 本文只留**结论、证据、可复现操作**。
   判断标准是「这条信息会不会在下一次动手时救我一命」，而不是具体行数。
   **过时的中间过程直接删**（2026-10-18 教训：`_archive/` 攒了 54 个文件从来没人回看，
   真正被反复用的是本文和反编译源码）。
3. **数字必须可复现。** 引用调用点/指令数时写明用 `tools\ilx.exe` 的哪条命令
   或 `tools\collect_ui_strings.py` 的哪次输出；手写解析器的数字一律不要引用（见 5.4）。
   文本条数的唯一权威是 `_dict/strings.json`（现行口径 2383 条候选，见 4.2）。
4. **结论必须有证据等级**：区分「IL 静态证据」「运行时实测」「源码推断」。
   不要把推断写成事实 —— 本项目曾多次因"看起来像"而下错结论，浪费了大量往返。
5. **失败与假象也要记**，一张"已排除假设"表比结论本身更省时间（6.5 / 5.6）。
