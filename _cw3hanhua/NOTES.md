# CW3 汉化项目 · 技术文档

> **这是唯一文档。** 只保留**结论、证据、可复现操作**。
> 过程性叙述、已废弃的方法不单独存档 —— 历史结论压成一句留在对应章节，或者直接删。
> 维护纪律见文末。

---

## 一、一页速览

| 问题 | 答案 |
|---|---|
| 怎么改 | **不改游戏文件**：BepInEx 5 + 两个插件 DLL + 外置词表 + 外置图集 |
| 字体怎么办 | **离线烘 BMFont 图集 → 运行时把中文字形并进 GUI Atlas**，走游戏原生的**位图分支**（3.4 / 4.4） |
| UILabel 文本怎么变中文 | `UILabel.ProcessText` 的 prefix + postfix（**全部 1029 个 UILabel 的必经之路**，见 3.3） |
| TextMesh 文本怎么变中文 | 两条腿：**改资产**把 19 条静态串烘进 prefab（3.6 / 4.6）；**代码补丁**管运行时才写的那几条（`ShowText` / `set_starName` / `set_planetName`，4.6a） |
| 游戏自带多语言 | **是死的**（词典为空、`Get` 零调用，3.7） |
| 当前进度 | 字体层 ✅ / UILabel 文本层 ✅（**v0.9.0 已实测通过**）/ TextMesh 静态 19 条 ✅ / TextMesh 动态补丁 🟡 **v0.10.0 待实测** |
| 排查 NGUI 怪现象 | **先 `tools\ilspy.cmd` 反编译真实实现再推理** —— 本项目最有效的动作 |
| 想量化文本量 | **不要靠跑游戏**，跑 `tools\collect_ui_strings.py`（静态扫全部 IL + 资产） |

### 协作硬规则（用户明确要求过）

1. **不要自己判断 Steam 是否在运行 —— 直接问用户。**
   DSH 的 shell 跑在 **Low 完整性级别**，进程全表枚举**看不到**高完整性进程
   （实测：活跃的 `steam.exe` PID 28188 能用 `-Id` 拿到，却不在全表 42 个进程里）。
   **「枚举看不到」≠「没在运行」**；`-ErrorAction SilentlyContinue` 还会把权限错误吞成"没找到"。
2. **不要自行启动 Steam 或游戏本体。** 需要时明确告知用户要做什么。
3. **实验前先讨论。组改之前先问。** 一次只改一个变量。
4. **除非用户显式命令，否则不要 git 提交。**
5. **只维护这一份文档，不要新建 md。**

---

## 二、当前状态

| 项 | 状态 |
|---|---|
| 游戏本体 | **原版**，资产零改动（4.4a） |
| 存档与设置 | 已清空重建（`Documents\creeperworld3`、`%APPDATA%\creeperworld3`） |
| BepInEx 5.4.23.5 | **已启用**（`winhttp.dll` 就位；注入点改动见附录 A） |
| 字体层插件 | `BepInEx/plugins/CW3Hanhua.dll` **v0.8.2**（`plugin/CW3Hanhua.cs`）<br>合并图集 + 合并 HUD。**已实测通过** |
| 合并图集 | `_font/atlas_zh.png` 4096×8192 + `_font/atlas_zh.bin` —— **字体层的全部运行时输入**（4.4） |
| 文本层插件 | `BepInEx/plugins/CW3HanhuaText.dll` **v0.10.0**（`plugin/CW3HanhuaText.cs`）<br>读 `_dict/zh_cn.dict`（**1144 条**）。`ProcessText` prefix+postfix 覆盖全部 UILabel；**另打 5 个 TextMesh 写入点**（3 个 `ShowText` prefix + 2 个 name setter postfix）。UILabel 部分**已实测通过**，TextMesh 补丁**待实测** |
| 字形度量 | `_font/font_{16,20,28}.{json,png}` —— **重烘图集的必需输入**<br>雅黑 Bold @ `--scale 0.80`，每套 **7544 字形**。改了字集必须重跑 `build_atlas.py` |
| 按键 | 字体层 **F3** 开关 / **F11** dump / **F12** HUD；文本层 **F4** 开关 / **F5** 打印未命中 / **F9** 重载词表 |
| 待办 | 15 条资源条模板串的碎片替换（4.7）；`Andro` / `DMD` / `'4'` 是否翻待定（4.7） |

**启用 / 停用 BepInEx**

```powershell
$g="C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3"
Rename-Item "$g\winhttp.dll.off" winhttp.dll   # 启用
Rename-Item "$g\winhttp.dll" winhttp.dll.off   # 停用
```

`BepInEx\config\BepInEx.cfg` 的 `[Preloader.Entrypoint]` **必须**是
`Assembly-CSharp.dll` / `SteamManager` / `Awake`，否则启动即崩（附录 A）。

---

## 三、技术事实

### 3.1 三类文本，互不相干

| 载体 | 规模 | 字体机制 |
|---|---|---|
| **Unity TextMesh** | 30 个对象 / 27 个有文字（level1 里 14 个 = 主菜单大按钮） | `UnityEngine.Font`（Aldrich 动态字体，无汉字） |
| **NGUI UILabel** | 1206 对象 / **1029 条有文字**（level1 里 354 个，同一时刻仅约 4 个 active） | `UIFont` → BMFont **预烘焙图集** |
| **DLL 写死字符串** | 822 条（与资产去重后 **2400 条唯一串**） | 最终都落到上面两种载体上 |

**不是 uGUI**：全部资产里 `UnityEngine.UI.Text` 为 **0** 个；`Assembly-CSharp.dll` 里
搜 `UnityEngine.UI` / `Canvas` 全部无命中。文本只有上面两条链路。

### 3.2 ⚠ 动态字体只能用于 TextMesh，**UILabel 上不可靠**

NGUI 的 `UIFont` 确实完整编译了动态字体分支（`Print`/`WrapText`/`CalculatePrintedSize`
都调 `Font.RequestCharactersInTexture` + `GetCharacterInfo`），但 UILabel 上实测不成立
（排版错乱）。**TextMesh 路线实测可用**：`Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 20)`
+ `TextMesh.font`。

> **本项目 TextMesh 走的是改资产不是动态字体**（4.6），因为动态字体每次改文本仍要过一次
> 赋值，改资产则是对象一出现就是中文。动态字体这条能力留着当备用方案。

**TextMesh 的字体资产本身不是障碍**：全游戏只有两个 `Font`（`sharedassets0.assets` 的
`Aldrich-Regular` pid 755、`arial` pid 756），cmap 里 **CJK 汉字 0 个**；但运行时能显示中文，
说明 Unity 5 的 Windows 动态字体在回退到系统字体，与 `m_FontData` 有无字形无关。
`ilx find RequestCharactersInTexture` 命中 0 项 —— 游戏代码不碰动态字体 API。

### 3.3 ★ UILabel 在 `UILabel.ProcessText` 上收口（v0.9.0 主方案）

- `UILabel.set_text`（token **`0x06000474`**，`il=31`）是可打补丁的普通方法，
  程序集内 **322 个调用点**（`ilx callers --type UILabel set_text`）。
- **但资产里序列化的 `mText` 不经过 setter**（反序列化直接写字段）→ `set_text` 补丁覆盖不到。
- ★ **`UILabel.ProcessText`（token `0x06000490`，RVA=0x1DD48，`il=301`）才是所有 UILabel 的唯一收口**：
  只有 3 个调用方，**全在上屏路径上** ——
  `UILabel.get_processedText` / `UILabel.get_relativeSize` / `UILabel.MakePixelPerfect`。
  任何画得出来的 label 都必然执行它，**包括资产序列化的那些**：
  `get_processedText`（`il=23`）先 `call get_hasChanged()`，不脏则 `brfalse` 跳过；
  `get_hasChanged`（`il=44`）拿 `mLastText` 比 `get_text()` —— `mLastText` **不序列化**，
  初值 null ≠ 英文 → 首帧必然触发一次 `ProcessText`。

**实现**：prefix 把 `mText` 临时换中文 → 原方法体算出中文 `mProcessedText` / `mSize` /
localScale → postfix（+finalizer 兜异常）把 `mText`/`mLastText` 写回英文。
**效果：屏上中文，`label.text` 永远英文。**

★ **两个字段都必须还原**：`ProcessText` 开头 `000E–0015` 就是 `mLastText = mText`，
若只还原 `mText`，`get_hasChanged`（比 `mLastText` 与 `get_text()`）永远不等
→ 每帧重复 `WrapText`，白烧 CPU 且 `mShrinkToFit` 反复缩。
★ **finalizer 不能省**：`ProcessText` 抛异常时 postfix 不执行，`mText` 会永远卡在中文
→ 下面那张表里的判定全坏。

**为什么 `label.text` 必须留英文**（游戏逻辑拿它当数据）：

| 位置 | 拿 label.text 干什么 | 改中文的后果 |
|---|---|---|
| `CommandCenter.DeactivateSelectedUnits`（`0x06000A83 il=49`） | `get_text` 与 `'ACTIVATE'` 比 `op_Equality` | 永远 false → 单位停用后无法重新启用 |
| `CommandCenter.StopResupplySelectedUnits`（`0x06000A85 il=49`） | 与 `'RESUPPLY'` 比 | 永远 false → 补给开关失效 |
| `CommandCenter.DisarmSelectedUnits`（`0x06000A84 il=49`） | 与 `'ARM'` 比 | 碰巧无害（`'ARM'` 未译，本来走 false 分支） |
| `TerraformCommandCenter.HandleUnitInteraction`（`0x06000AD8`） | `UnitToBuildLabel.text` 当 unitType 传给 `CreateUnit(String,Int32,Int32)` | 建造失败（`CreateUnit` 内逐个比 24 个英文常量：`'Collector'` `'Relay'` … `'Shield Key'`） |
| `CRPLTowerManager.get_popupText`（`0x06001843 il=11`） | `return popupTextLabel.text`（label 就是存储） | 弹出文本变中文、且被 `WriteXML` 写进存档 |
| `CRPLTowerManager.WriteXML`（`0x06001878 il=461 eh=3`） | `tem_t` 节点写 `TextMesh.get_text()` | CRPL 自定义文本进存档 |

反向确认安全：`ilx fields --type UILabel mProcessedText` 显示只有 `UILabel.get_processedText`
一处 `ldfld`（其余 9 写 6 读都在 `ProcessText` 内部）→ **没有任何游戏逻辑读 `mProcessedText`**。

其余 `get_text` 调用点无害（都是读回自己的旧值做拼接/判空）：`UIInput.Init` ×2、
`UIInput.OnSelect` ×2、`UIInput.set_defaultText`、`UILocalize.Localize`、
`InputManager.OnPress`、`NumenManager.SetPriceAndLevelLabels`、`ResourcePackManager.Update`、
`UnitToBuildManager.SetInfoLabel`、`UILabel.get_hasChanged`、`UILabel.set_hasChanged`。

#### ⚠ 拼接串：词表里查不到整串（v0.8.3 修的）

主菜单那圈悬浮说明（Story / Prospector / Tormented / MySector / Alpha / Colonial）
是**运行时拼出来的**，拼完才 `set_text`：

```
MainMenu.OnMouseEnterEntryManager / MainMenu.Update:
  ldstr '[ffffd0]You are the supreme being.[ffffff]\nMake your own custom worlds...'
  stloc.0
  ...if (locked):
  ldloc.0 ; ldstr '\n[ff5050]To Access:\n[ff0000]Complete first 5 Arc Eternal missions.'
  call String.Concat(String, String) ; stloc.0
  ...
  ldloc.0 ; callvirt UILabel.set_text(String)
```

静态收集器只认 `ldstr`，所以词表里只有两个半截，没有拼好的整串 → 精确匹配必然落空。

**修法**：`Lookup` 精确匹配失败后调 `LookupConcat` —— 在 `'\n'` 处切成 head/tail，
两段**各自精确命中**词表才拼接返回，否则返回 null（宁可留英文，不拼中英混杂）。
从右往左找切点（动态拼接多是「正文 + 追加段」），递归限 2 层。

其余已知拼接点（都已核过，两半都在词表里）：
`[ffffd0](D)ial (M)ap (D)evice...`、`[ffffd0]Credits[ffffff]...`（两种变体）、
以及 4 种 `To Access` 后缀 —— 注意有一处是 `[ff0000][ff0000]` **重复色标**，
词表按原样存了这一条，别"顺手清理"。

> **`LookupConcat` 只切 `\n`**，所以「同一行内拼接数字」的情形它管不了 —— 见 4.7 的
> 资源条模板串。

### 3.4 ★ 字体层：把中文字形并进 GUI Atlas

**一句话**：不新建材质、不改 `mAtlas`，而是**把 4096×8192 的新图集直接换给
`atlas.spriteMaterial.mainTexture`**，于是文字与精灵仍是同一份材质 → 同一个 draw call →
遮挡关系天然与原版一致。细节与踩过的坑见 **4.4**。

> 早期方案是「拆 draw call：`mAtlas=null` + 自建材质」，**已废弃**（会破坏遮挡，
> 见 3.8 ①）。

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

**`mBase` 在整个 `Print` 中从未被使用。** `mUVRect=(0,0,1,1)` 时
`glyph.x/y` 就是图集内的像素坐标。

### 3.6 TextMesh 走不通代码路线

`UnityEngine.TextMesh.set_text` / `get_text` 都是 **RVA=0x0 的 extern icall，没有方法体**
（`ilx dump TextMesh set_text get_text --dll CW3_Data\Managed\UnityEngine.dll` → 「无方法体 / extern」）
→ **Harmony 打不上**。这与 `UILabel.set_text`（有 IL 体）形成关键区别。

**所以 TextMesh 只有两条路**：改资产（4.6，采用）或给上游 8 个宿主方法打 prefix（4.6 备选）。
全部 15 个 `TextMesh.set_text` 调用点已查清，见 4.6。

### 3.7 CW3 自带的多语言系统是**死的**

`Localization` 实例 1 个、`mDictionary` 条目 **0**、`mLanguage` 空、`Get` **零调用**。
`Get` 找不到就原样返回 key（恒等函数）。汉化只能靠 `ProcessText` 补丁 / 改资产 / 字体层。

### 3.8 其它有用的事实

- 文本里有 NGUI 富文本标记（`[ffffd0]...[ffffff]`）和 `\n`，**翻译时必须原样保留**
- **`Assembly-CSharp` 里有全局 `Path` 类**，会抢掉 `System.IO.Path` → 必须写全名
- Unity 5.2 没有 `UnityEngine.SceneManagement` → 用 `Application.loadedLevelName`
- Unity 5.2.3 没有 `GameObject.scene`（5.3+ 才有）→ 用系统库编译能过、运行时出错
- `UIPanel` 没有 `SetDirty()` → 用 `UILabel.MarkAsChanged()`
- `UIFont` 是 MonoBehaviour（不是 ScriptableObject）→ `new GameObject().AddComponent<UIFont>()`
- 全程序集 **8289** 个方法有 IL 体（`ilx stats` 可复现）
- 全程序集只有 `UICamera` 用 `Physics.Raycast`；CW3 自己用 `RaycastAll` + `ScreenPointToRay`
- **`TextureFormat.Alpha8` 在 Unity 5.2 采样出 RGB=0**，画字体必须用 RGBA32「RGB=白 + A=覆盖率」
- `GetPixels32` / `SetPixels32` 的索引 0 都是**左下**，两者约定一致，**不要翻转**
- **`UILabel` 自己没有 `Update` / `LateUpdate`**，文本靠
  `UIPanel.LateUpdate → Fill → UILabel.OnFill → ProcessText` 驱动。Unity **不保证脚本间
  `LateUpdate` 顺序**（本作没设 Script Execution Order）→ 任何「改 label 文本」的代码
  必须放在 `Update` 里，否则要等下一帧 `Fill` 才上屏。TextMesh 则改完立刻重建网格。

#### 3.8a NGUI 位图字体的两个现行坑

> **另有两条属于已废弃的「拆 draw call」方案**（材质独立导致渲染顺序任意、改 `UIFont.mMat`
> 后 label 必须重新赋 `material` 才会注册进 panel）—— **v0.9.0 的合并图集方案根本不拆
> draw call，这两条不会再碰到**，故不展开。若将来有人想回到独立材质路线，先读
> `UIWidget.material` 的 setter：它才是 `CreatePanel()`（注册控件）的唯一入口。

**① 不能整张图集当 `_MainTex` 交给裁剪**
`UIDrawCall.OnWillRenderObject` 里 NGUI 的裁剪不是 shader 读 `_ClipRange`，而是对材质做
**纹理重映射**（`mainTextureOffset` / `mainTextureScale`），它假定 `_MainTex` 就是面板内容本身。
字体图集整张用（`mUVRect=(0,0,1,1)`）会被打乱 UV。`SoftClip` 面板（`KeyMappingsPanel`）
会踩到。**接入真正的滚动列表时必须处理。**

**② `UIPanel.LateUpdate` 只重填"几何变了"的材质**

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

| 层 | 解决什么 | 手段 |
|---|---|---|
| ① 字体层 | 字形从哪来 | 离线烘 BMFont → **并进 GUI Atlas**（4.4） |
| ② UILabel 文本 | 英文串怎么变中文 | `ProcessText` prefix+postfix（全部 1029 个 label，3.3） |
| ③ TextMesh 文本 | 同上 | **两条腿**：静态的 19 条改资产烘进 prefab（4.6）；运行时才写的那几条打代码补丁（4.6a） |

| # | 阶段 | 状态 |
|---|---|---|
| A1 | 离线烘焙（图集 + 字集检查） | ✅ |
| A2 | 插件字体层 | ✅ |
| A3a | 英文验证（位置/布局/遮挡） | ✅ |
| A3b | 中文验证 | ✅ |
| B1 | 静态全量收集 + 词表骨架 | ✅ |
| B2 | `ProcessText` prefix+postfix+finalizer | ✅ **v0.9.0 实测通过** |
| B4 | HUD 合并成左上角一块 | ✅ **v0.8.2 实测通过** |
| B6 | 改资产：TextMesh 中文烘进 prefab（19 条） | ✅ **实测通过**（`script_type_index` bug 修好后，见 4.6） |
| B7 | ③ 类运行时覆盖对象的代码补丁（v0.10.0） | 🟡 **已实现，待实测**（4.6a） |
| C1–C3 | 翻译：主菜单 / HUD / 建造菜单 / 剧情 | ✅ 全量批 full_01–22 入库 1144 条 |
| C4 | 剩余按设计不译的分流 | ✅ |
| D | 打包 / 分发 | ⬜ |

> 已废弃的阶段（不要再做）：**B3** 的 `set_text` prefix + 周期 sweep、
> **B5** 的「缓存 + 每帧快扫」。这两个都已 `❌`。
> 前两者的教训见 4.3 末，B7 的依据见 4.6。

**为什么字体层先做**：否则中文显示成方块，无法区分"字体没做"还是"翻译错"。
**为什么先用英文验证**：文本不变时，布局/裁剪/遮挡的异常只能归因于字体层。

### 4.1 字体烘焙与插件按键

**按键（`plugin/CW3Hanhua.cs` v0.8.2）**

| 键 | 作用 |
|---|---|
| **F3** | 字体层开关（换合并图集 / 还原）—— 主功能 |
| **F11** | dump 到 `BepInEx/CW3HanhuaFont.txt`（draw call / 材质 / 队列） |
| **F12** | HUD 开关（两个插件的状态合并画在左上角） |

文本层另一套：**F4** 开关 / **F5** 打印未命中清单 / **F9** 重载词表。

**装载流程**（详见 4.4）：读 `atlas_zh.png` 建 4096×8192 纹理 →
换 `atlas.spriteMaterial.mainTexture` → 重算全部 `UISprite.UpdateUVs(true)` + `MarkAsChanged()`
→ 读 `atlas_zh.bin` 建 BMFont → 反射填 `mFont`/`mUVRect`/`mDyn`/`mPMA`（**`mAtlas` 不动**）。

#### HUD 合并

原先两块：字体层左上角 3 行，文本层左下角 1 行（`GUI.Box(Rect(8, Screen.height-30, ...))`）。
现在**只留左上角一块 4 行**。

```csharp
// CW3Hanhua.cs —— 宿主：把对方那行拼进自己的 GUI
Type t = AppDomain.CurrentDomain.GetAssemblies()[i].GetType("CW3Hanhua.TextPlugin", false);
MethodInfo m = t.GetMethod("HudLine", BindingFlags.Static | BindingFlags.Public);

// CW3HanhuaText.cs —— 被调用方：只读地报状态，不碰 UI
public static string HudLine() { return "文本层 v" + Ver + "  F4=" + ... }
```

- **为什么用反射而不是直接引用**：两个 DLL 必须保持**无编译期依赖**（独立编译、独立开关，
  出问题能立刻判断是谁的锅）。BepInEx 按文件名序装载，`CW3Hanhua.dll` 先于
  `CW3HanhuaText.dll` → 字体层第一次 `OnGUI` 时对方可能还没就绪，所以
  **不缓存否定结果**，每 60 帧重试一次。
- **反向兜底**：只剩文本层时，`TextPlugin.OnGUI` 自己画左下角那一条（每 60 帧探一次
  `CW3Hanhua.HanhuaPlugin` 类型是否存在，在就完全不画）。→ **永远只有一块 HUD**。
- HUD 归字体层的 `showHud` / F12 管；`HudLine()` 是反射契约，**签名不能改**。

**字形烘焙**（产出 `font_<size>.{png,json}`，是 `build_atlas.py` 的输入）

```powershell
# 当前参数：雅黑 Bold + 0.80
& .venv\Scripts\python.exe tools\bake_font.py --font "C:\Windows\Fonts\msyhbd.ttc" --scale 0.80
# 纵向单独压（汉字满格、Latin 偏窄，等比缩放无法两方向同时对齐）
... --scale 0.80 --scale-y 0.76
# 缺字时（check_charset.py 会直接打印这条命令）
... --extra "缺的字"
```

烘完必须重跑 `build_atlas.py` 才生效。

**字集**：GB2312 全集（符号区 + 一级 3755 + 二级 3008）+ ASCII 95 + 常用符号 = **7544 字**。
- **必须含符号区**：中文标点全在 GB2312 1–9 区
- **必须含二级**：渲 / 阈 / 阱 这类常用字在一级（16–55 区）之外

**调字号的两个已知约束**：① 当前 0.80 是靠英文调的，中文观感可能不同；
② 雅黑 Bold 笔画粗、覆盖率高于 Aldrich，所以粗体要比常规体用更小的 scale。

### 4.2 文本层：静态全量收集与词表

**为什么不靠跑游戏收集字符串**：一次会话只能覆盖走过的路径，剧情/任务/教程要走很远才出现，
**全量量化不可能靠回放**。改用静态分析：

```
路 1  IL：每条 ldstr 后面紧跟的指令若"消费"了它（call/concat/stfld…）→ 这条字符串进了逻辑
      用 dnfile(读元数据) + dncil(解析方法体) 扫 Assembly-CSharp.dll / -firstpass.dll
      → 覆盖**全部 8289 个有 IL 体的方法**，而不是走过的路径
路 2  资产：UILabel.mText / TextMesh.m_Text 序列化值（反序列化不经 setter，必须单独收）
```

**工具**（都在 `tools/`，Python 用 `.venv`）

| 脚本 | 作用 | 产物 |
|---|---|---|
| collect_ui_strings.py | 静态全量收集 + 分类 + 去重（`--no-assets` 跳过资产扫描，5.5s ↔ 全量 170s） | `_dict/strings.json` |
| test_classify.py | `classify()` 的回归样例（改 `RE_*` 必跑） | 终端 |
| dict_stats.py | 覆盖率报告 | `_dict/STATUS.md` |
| build_dict.py | 编译成插件读的二进制词表（**必须带 `--include-other`**，否则 250 条 other 档译文被丢） | `_dict/zh_cn.dict` |
| check_charset.py | 译文缺字检查（复用 bake_font 的字符集定义） | 终端 |
| bake_font.py | 烘字形；`gb2312_chars()` / `charset_for(sizes)` 同时供上面两个用 | `_font/` |

**分类三档**（`strings.json` 每条的 `grade`）

| grade | 含义 | 处理 |
|---|---|---|
| `ui` | 多词英文、以冒号结尾、含数据的格式串（`Score: 9999`）、以空格结尾的拼接前缀 | 确定要翻 |
| `other` | 单个词（`Yes`/`Creeper`/`Close`）、代码片段、疑似调试消息 | 拿不准，后翻 |
| `noise` | 标识符 / PascalCase / 资源名 / 标准库异常 / 节点路径 / 正则 / 纯数字 | 丢弃 |

**当前量化结果**（2026-10-20 晚重跑，可复现）：

| 项 | 数量 |
|---|---|
| IL 字符串引用 | 11001（Assembly-CSharp）+ 238（firstpass） |
| 资产文本 | 467 条（UILabel 455 + TextMesh 12；另有 19 条已被 bake 成中文，按 CJK 跳过） |
| 去重后候选 | **2404**（ui 1292 + other 1083 + none 29） |
| 来源 | both 292 / asset 175 / dll 1937 |
| 已译 | **1144**（ui 880 + other 260 + 4 条过场叙事） |

> **数字口径**：`ui` 不是"一定会显示"，是"几乎一定"；`other` 里约 900 条是单词，
> 需要人工判定。**不要拿 ui 条数当工作量上限。**

**★★ `--merge` 踩过的两个坑（2026-10-20，务必看）**

1. **资产被 bake 过之后再跑收集，会把中文当源串收进来** —— `bake_textmesh_assets.py`
   把 19 条 TextMesh 英文烘成中文后，`scan_assets()` 读到的是中文，
   于是 `'载入文件'`、`'制作组'` 这类译文被当成"新发现的源串"写进 `strings.json`（实测混进 22 条）。
   修法：`scan_assets()` 里用 `RE_CJK` 跳过任何含中日韩字符的文本（游戏原文全是英文）。
2. **只按"新扫到的条目"合并会静默丢译文** —— 上面那 19 条英文源串一旦扫不到，
   它们的译文就会被丢弃（实测丢了 10 条，`zh_cn.dict` 直接少 10 条），
   而界面上那些字已经是中文、**完全看不出异常**。
   修法：`save(merge=True)` 除了沿用同名条目的译文，还要把
   **"旧文件里有译文、这一轮却没扫到"的条目整条搬回来**，并打印补回了哪些。

**词表骨架**（`_dict/`）

```
_dict/
├─ strings.json     ★ 唯一词表。每条 {en, zh, grade, cat, kind, cls, refs, note}
│                   翻一条 = 把 zh 从 "" 填成中文。--merge 重跑不会丢译文（含上面第 2 条的搬回逻辑）。
├─ glossary.md      术语表。改译名只能在这里改，译文里不许随手换。
├─ zh_cn.dict       由 build_dict.py 生成，插件直接读
├─ STATUS.md        由 dict_stats.py 生成，勿手改
├─ batches/         23 份批次源（full_01…22 + 19/20/21 的单词级补充）
└─ skipped_manual.json  565 条「人工判定不译」，**别删**（否则以后有人会重翻一遍）
```

**二进制词表格式**（`zh_cn.dict`，小端）

```
"CWH1"                      4 字节
int32 count                 条数（offset 4）
count × { uint16 klen, UTF-8 en, uint16 zlen, UTF-8 zh }   ← 从 offset 8 开始，长度是 2 字节
uint32 FNV-1a               校验（★ 无符号：用 ReadUInt32，别用 ReadInt32；范围 raw[8:-4]）
```

**已定的译名**（见 `glossary.md`）：**Creeper / Anti-Creeper / AC 保留英文**（用户决定）。

**工作流**

```powershell
$p = "_cw3hanhua\.venv\Scripts\python.exe"
& $p _cw3hanhua\tools\dict_stats.py            # 看还差多少
# 1) 写批次：_dict\batches\<名字>.json  {"items": {"英文原文": "译文"}}
& $p _cw3hanhua\tools\apply_translations.py --dry-run   # 先看匹配情况
& $p _cw3hanhua\tools\apply_translations.py             # 只填空，不覆盖
& $p _cw3hanhua\tools\check_charset.py _dict\strings.json   # 缺字就补烘
& $p _cw3hanhua\tools\build_dict.py --include-other  # 编出 zh_cn.dict
# 改完词表游戏内按 F9 重载，不用重启
```

**译文纪律**

- 只改 `batches/*.json`，**不要手改 `strings.json`**（`apply_translations.py` 用原文精确匹配，
  手改容易和批次对不上）。
- 译文里的 `[颜色]` 标记、`\n`、首尾空格**必须保留**。像 `PACKET SPEED.  `（两个尾空格）
  是拼接前缀，尾空格删掉就会和后面的数字粘在一起。
- 组合键（`SHIFT-E` / `SHIFT-A` / `CTRL-C`）整串保留英文 —— 玩家要按着它去找键。
- 改术语先改 `glossary.md`，再批量改批次。
- 译稿用**序号字典** `{"zh": {"3": "译文"}}`，缺省项视为 `@`（跳过不译）；
  **不要用数组形式**（漏一项就整体错位，踩过一次：89 vs 88，后 60 条全串行）。

### 4.3 文本层插件（`CW3HanhuaText.cs` v0.10.0）

**为什么单独一个 DLL**：字体层已验证通过、不想动；两层独立开关，出问题时能立刻判断是谁的锅。
BepInEx 5 按文件名顺序装载，`CW3Hanhua.dll` → `CW3HanhuaText.dll` 是**字母序碰巧正确**的，
**改文件名可能改变装载顺序**。prefix 装在 `Awake` 里（早于场景创建），能拦到首批文本。

**替换路径（UILabel 一条 + TextMesh 五个写入点）**

| 路径 | 覆盖什么 | 实现 |
|---|---|---|
| `UILabel.ProcessText` **prefix + postfix/finalizer** | **全部 1029 个 UILabel**：运行时动态文本 + 资产序列化的 `mText` | 见 3.3 |
| `PreludeManager/EpilogueManager/DemoScreenManager.ShowText` **prefix 改 `__0`** | 三段过场的全部台词（31 + 21 + 10 处调用） | 见 4.6a |
| `StarManager.set_starName` / `PlanetManager.set_planetName` **postfix 只改 TextMesh** | 星区名 / 星球名的显示 | 见 4.6a，**不能改入参** |

**代价（可接受）**：`label.text` 与屏上显示不一致（屏上中文、`.text` 英文）。
`mSize` 是按中文算的 —— 这正是我们要的，字宽/换行/`mShrinkToFit` 全对。
若要 `get_text()` 也返回中文，就得改 `mText` 并自己兜住 3.3 那 6 处判定，
**当前故意不做**。

**F4 的还原**：`mText` 一直是英文（中文只在 `ProcessText` 内部临时存在），所以不需要
写回英文，只要逼它重算一次 `mProcessedText`。**踩过的坑**：写 `s.label.text = s.en` 是
**无效的** —— `set_text` 里 `if (mText != arg)` 不成立就什么都不做，`mProcessedText` 还是中文。
正确做法是 `ForceRefresh(label)` → 优先 `UILabel.MarkAsChanged()`（public，
IL = `set_hasChanged(true)` + `UIWidget.MarkAsChanged()`），失败才反射调私有的 `set_hasChanged(true)`。

★ **F4 的边界**：只能还原**本层经手**的文本。改资产烘进 prefab 的中文不归本层管、
关不掉 —— 要还原得跑 `tools/bake_textmesh_assets.py --restore`（4.6）。
TextMesh 那五个写入点登记在独立的 `meshSwaps`（`MeshSwap = {mesh, en}`）里，
`RestoreAll` 直接 `mesh.text = en` 就够（TextMesh 没有 processed 中间层，不必 `ForceRefresh`）。

**换场景 / 还原记录**

| 项 | 做法 |
|---|---|
| 容器 | `Dictionary<int, Swap>`，键 = `GetInstanceID()`，`Swap = {label, en, zh}` |
| 换场景 | `PruneSwaps()`，**只删已销毁的**（`s.label == null`），活着的必须留住 |
| 过期记录 | `Lookup(mText)` 为 null 时删记录 —— 但**只在 `HasLetter(en)` 为真时删** |

★ **`HasLetter` 守卫不能省**：prefix 把 `mText` 换成中文之后，原方法内部若再触发一次
同一实例的 `ProcessText`（`UIFont.WrapText` 读 `relativeSize` 就会），此时读到的 `mText`
已是中文、查不到词表。若无守卫，刚写好的记录会立刻被自己删掉。`HasLetter` 对中文恒为 false。

★ **不能用 `List` + 切场景 `Clear()`**（v0.8.0 的缺陷）：常驻 UI / `DontDestroyOnLoad`
对象跨场景存活，记录被清掉后 `RestoreAll()` 对它是空操作 → 关 F4 留下中英混杂。

**排查工具**

- `missSeen` + **F5**：`ProcessText` 查不到译文时记一条，F5 打印清单。
  列出来的说明"走了 UILabel 但没译文"（补词表 + F9 重载即可）；
  界面上仍英文、清单里又没有的，才是真正**必须改资产**的对象（TextMesh 那批）。
- **HUD 第 4 行**：`文本层 v0.10.0  F4=…  词表=…  prefix=调用/命中  拼接=…  未命中=…  mesh=调用/命中`。
  `prefix` 计数不动 = 补丁没装上；`mesh` 计数不动 = TextMesh 那五个补丁没装上或那段文本没被写过。

**★ 装补丁必须自检**：`Harmony.Patch` 失败时**只往日志写一行 Error、不抛异常**
（外层 try/catch 什么也接不到，表现为"什么都没发生"）。所以装完立刻回读
`Harmony.GetPatchInfo(m)`，核对 `Prefixes/Postfixes/Finalizers` 各 1、`ILManipulators` 为 0，
不合格就打印警告。**这是 v0.9.0 首次实测"完全没中文"的直接教训。**

#### 已废弃：sweep 路线（不要再实现）

删掉的是：`Sweep` / `RebuildCache` / `ReplaceLabels` / `ReplaceMeshes` / `LateUpdate` /
`IsSwapped` / `labelCache` / `meshCache` / `CacheRebuildFrames` / 全部命中计数。

- **UILabel 周期 sweep**：被 `ProcessText` 补丁一并覆盖，零闪烁，不需要了。
- **TextMesh 周期 sweep / 每帧快扫**：用户实测两次都不满意。理论上限就是
  「总有一帧是英文」——只要靠**事后扫描**，英文必然先上屏一次，把窗口从 0.5s 压到 0.17s
  也只是压小、没有消除。**结论：TextMesh 只能改资产。**

残留的 `missSeen` 保留，但只作排查工具（见上）。

### 4.4 ★ 合并图集方案（字体的最终方案）

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

#### ⚠ 坐标约定：精灵和字形都是【顶左原点】

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

#### ⚠ 灰度图必须转成 alpha

`font_XX.png` 是 PIL **`L` 模式**（白字黑底）。直接 `convert("RGBA")` 会把黑底变成
**alpha=255 的不透明黑块**（实测成品像素 `(0,0,0,255)` 满屏）。

```python
g = Image.open(glyph_png).convert("L")
gimg = Image.new("RGBA", g.size, (255, 255, 255, 0))
gimg.putalpha(g)          # RGB 全 255，alpha 承载覆盖率
```

理由：draw call 的顶点色（`label.color` / 富文本标记）会乘上纹理 RGB，所以 RGB 必须全白。

#### ⚠ 换纹理后必须手动重算精灵 UV

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

**UIFont 的改动**

| 字段 | 值 |
|---|---|
| `mAtlas` | **保持不动**（指向 GUI Atlas） |
| `mMat` | 指回 `atlas.spriteMaterial` |
| `mUVRect` | (0,0,1,1) |
| `mFont.texWidth/Height` | **4096×8192**（大图集尺寸） |
| `mDynamicFont` | null（走位图分支） |
| `mPMA` | -1 |

★ **`mAtlas` 必须保持指向 GUI Atlas** —— `UIFont.material` getter 里
`if (mAtlas != null) return mAtlas.spriteMaterial;` 优先级最高，所以文字和精灵自动共用
同一份材质 → 同一个 draw call → 遮挡关系恢复原版。**这正是整套方案的意义。**

**为什么 UV 不用改 spriteList**：`UISprite.UpdateUVs` 的换算是**每次刷新时用
「精灵像素坐标 ÷ 纹理尺寸」实时算**的。原图 1:1 贴顶部，`outer` 像素坐标不变
→ 重算后 UV 自动正确。**但重算必须手动触发**（见上面的 UV 缓存坑）。

**F3 关闭时必须还原纹理**，否则精灵仍按 4096×8192 的 UV 采样 512×1024 的图，整个 UI 错乱。
`RestoreFont()` 里做了这件事（备份 `origAtlasTex/Scale/Offset`），
**但不要 `Destroy(mergedTex)`** —— 裁剪 draw call 的 `mClippedMat` 可能仍引用它。

### 4.4a 资产只读不写（「不改游戏文件」到底指什么）

**游戏资产一个字节都没被写过。** `sharedassets2.assets` 的 `LastWriteTime` / `CreationTime`
都还是 Steam 安装时刻；`CW3_Data` 下唯一被改过的文件是 `output_log.txt`（游戏自己写的）。

**只读的读法**（`tools/build_atlas.py` 的 `load_gui_atlas()`）：

```python
env = UnityPy.load(p)                    # 只读打开
d   = o.read_typetree()                  # 只读
raw = bytes(d["image data"])             # 取原始字节
base = raw[: w * h * 4]                  # 前 w*h*4 = RGBA32 基础层（其后是 11 级 mip）
img = Image.frombytes("RGBA", (w, h), base)
img = img.transpose(Image.FLIP_TOP_BOTTOM)   # Unity 左下原点 → PIL 左上
```

全部 `tools\*.py` 里**没有任何** `env.save()` / `save_assets()` / `write_typetree`。

**「改资产让图集可读」这条方案从未执行，也不需要。**
`m_IsReadable = False` 只约束 **Unity 运行时 API**（`GetPixels32` 报
`Texture 'GUI Atlas' is not readable`）。离线用 Python 直接取 `image data` 字段的原始字节，
是**文件里的数据**，绕开该限制；运行时则只要把材质的主纹理引用指过去即可。

> ⚠ **4.6 的「改资产」是唯一会写游戏文件的操作**，会先备份、由脚本完成，
> 与「当前资产零改动」不矛盾（那个状态描述的是现在）。

### 4.5 全量翻译

**结论**：`strings.json` **2404 条里 1144 条已译**（ui 880 / other 260 / 过场叙事 4），
**其余 1260 条按设计不译**。插件读的 `zh_cn.dict` = **1144 条**（49 481 字节，FNV 校验通过）；
`check_charset.py` 报 `用到字符 943 个（汉字 857）✓ 汉字全部在字集内` —— **不用重烘图集**。

> 2026-10-20 晚补的 4 条是过场叙事台词：`presents`→`出品`、`-- EARTH --`→`—— 地球 ——`、
> `--Transcript Follows--`→`—— 以下为会议记录 ——`、`--Transcript Ends--`→`—— 会议记录结束 ——`。
> 它们原本被 `classify()` 当噪声（下划线小写标识符 / 分隔线）丢掉，见 4.2 的叙事特判。
> `Knuckle Cracker` 与 `Creeper World III` 按术语表**保留英文**。

`kind` 维度（判断"玩家到底看不看得见"）：

| kind | 条数 | 已译 |
|---|---:|---:|
| UILabel | 455 | **369** |
| TextMesh | 12 | **10**（另 19 条已烘进资产，收集时按 CJK 跳过） |
| None（纯 DLL 字符串，多为日志/资源名） | 1937 | 765 |

未译的 UILabel/TextMesh 全部是**故意不译**的：单字母 `A`–`Z`、`F0`–`F12`、
`SHIFT`/`CTRL`、`SHIFT-A`…`SHIFT-X`、`CTRL-C`/`CTRL-V`（组合键规则）、
`Alternate`/`Backslash`/`Equals`/`Escape`/`Minus`/`Space`/`Tab`（按键绑定名）、
`Alpha1`–`Alpha8`、`CN 1`–`CN 3`、`DEV`/`VER`/`DMD`/`Andro`（标识符）、
占位文案（`This is a message`/`This is a great big long label`/`View Demo Screen`/
`Time:1:00:00.0`）。**换句话说：可见 UI 文本已清零。**

**剩余 1260 条的桶**（`strings.json` 自算，可复现）

| 桶 | 条数 | 为什么不译 |
|---|---:|---|
| 第三方库/框架日志 | 471 | VectorLine / CrplCore / CrplCompiler / AudioToolkit / NGUI / SGT_* / iTween / SevenZip，只进控制台 |
| 标识符/资源名 | 237 | `PRODUCTIONINTERVAL`/`ACBackground`/`1XButton` 之类，改了就断 |
| 程序化星球名 | 146 | `RandomPlanetName` 音节碎片（`Aber`/`Abra`/`Ale`…），保留原文风味 |
| 其它未译 | 133 | 按键名、占位文案、`', a = '` 调试碎片、`{0:D}:{1:D2}` 格式串 |
| 自定义图资源名 | 118 | `CustomImages` 的 `Custom0`/`Custom0_256`，资产 key |
| Steam 成就日志 | 61 | `m_flGameDurationSeconds: ` / `STAT testStat1: ` |
| 文件名 | 36 | `*.dat`/`*.xml`/`maps/`/`thumbnails/` |
| 资源条模板串 | 17 | 见 4.7（**待做碎片替换**） |
| 调试日志 | 17 | `ops` 里带 `call Log` |
| 1–2 字符符号 | 13 | `()`/`<<`/`>>`/`-c`/`~` |
| 哈希 | 11 | 32 位 hex |

**批次清单**：`full_01`…`full_18`（745 条）+ `full_19_uiwords`（97 条单词级 UI）+
`full_20_words`（118 条）+ `full_21_lastui`（27 条）。`full_03`/`full_16` 全跳（音频库与
VectorLine 日志）。

**几条译法备忘**（术语表之外新定的）：
- `Return` → `返回`（`ColonialSpaceManager.DrawMapBox` / `WorldMachineManager.DoWorldMachineWindow`
  的按钮，不是回车键）
- DMD 名册表头：`Owner`→`作者`、`Map`→`地图`、`Started`→`创建`、`Played`→`游玩`、
  `Plays`→`次数`、`Filter`→`筛选`、`Score`→`得分`、`Player`→`玩家`
- `[00e000]FLOWING`→`[00e000]流动中`，与 `[ff0000]PAUSED`→`[ff0000]已暂停` 对齐
- `Targets: `/`Price: ` 这类**拼接前缀**照样译，靠 `check_batch.py` 保住尾部空格
- `Credits` → **制作组**（"制作名单"实测宽 4.00em = 英文 3.82em 的 105%，会溢出）

**字宽实测**（决定要不要为排版改写译文）：**每个汉字 = 1.000 em**（与字号无关），
Aldrich 空格 = 0.300 em。18 条主菜单译文平均只占英文宽的 **68%**，唯一超标的是
`Credits`（已改）。→ **不需要任何"插空格/缩字号"的特殊手段**，直接替换即可。
（另：`zh_cn.dict` 是 TextMesh 与 UILabel 共用的，往词表里塞空格会污染 UILabel 路径。）

### 4.6 改资产让 TextMesh 显示中文（2026-10-20 已执行）

**目标**：把中文写进 prefab 的 `m_Text`，对象一出现就是中文，零闪烁。
`TextMesh.set_text` 是 extern icall（RVA=0），Harmony 打不上，所以这批只能改资产。

**盘点结果**（`_cw3hanhua/_diag/scan_textmesh.py`，只读，全部 19 个资产文件）

| 项 | 数量 |
|---|---|
| TextMesh 对象 | 30 |
| 有文本 | 27 |
| 唯一文本 | 21 条（词表已有 18，缺 3：`4`×5、`Andro`×2、`DMD`×1） |

分布：`level1` 14 · `resources.assets` 12（9 有文本）· `level2` 1 · `level5` 1 ·
`mainData` 1 · `sharedassets6.assets` 1 · 其余 12 个文件全 0。

**三类划分**

| 类 | 数量 | 说明 |
|---|---|---|
| ① 纯静态可改 | 20 | 主菜单大按钮、标题、序章旁白 —— 直接改 |
| ② prefab 占位值 | 5 | `TextLabel` 的 `4`（`m_Alignment=0` `m_CharacterSize=2.0`） |
| ③ 运行时覆盖 | 5 | `StarTitle`/`Wormhole/StarTitle` 的 `Andro`、3 个空模板 `StarInfo`/`PlanetTitle`/`CRPLTextMesh` —— **改资产无效**，留给代码路线 |

**排版参数实测**：23 个有文本的 TextMesh **无一例外 `m_Anchor=4`(中中) + `m_Alignment=1`(居中)**
（`TextLabel` 那 5 个例外：左对齐、`m_CharacterSize=2.0`）。字号主界面 35–40，
`Credits`/`Settings`/`Web` 36，`Click To Engage` 18，`Show Sector Map` 26，`Andro` 15，
`TextLabel` 48，`Fresh Ideas In Gaming` 96。**字体引用**：level/mainData/sharedassets6 的
TextMesh 全指 `Aldrich`（`{m_FileID:1, m_PathID:755}`），`resources.assets` 的指
unity 内置 Arial（`{m_FileID:5, m_PathID:10102}`）—— **两条都无 CJK 字形，但都不影响**
（见 3.2 末）。

**★ 已落地：`tools/bake_textmesh_assets.py`（2026-10-20 执行通过）**

```powershell
# 备份（幂等，永不覆盖已有备份）→ _cw3hanhua\_assets_orig\（71.5 MB，已 gitignore）
$env:PYTHONPATH = (Resolve-Path "_cw3hanhua\.venv\Lib\site-packages").Path
& "C:\Python314\python.exe" _cw3hanhua\tools\bake_textmesh_assets.py --backup
& "C:\Python314\python.exe" _cw3hanhua\tools\bake_textmesh_assets.py --dry-run   # 看会改什么
& "C:\Python314\python.exe" _cw3hanhua\tools\bake_textmesh_assets.py             # 恢复+烘焙
& "C:\Python314\python.exe" _cw3hanhua\tools\bake_textmesh_assets.py --restore   # 全部还原
```

脚本行为：**先把备份复制回游戏目录（清除旧修改）→ 再从备份副本重新烘焙 → 写回**，
所以反复运行结果幂等（实测第二次跑打印「已是原始状态，无需恢复」）。
只改 ① 类 20 条中的 19 条（`DMD` 词表无译文，跳过）；②③ 类不动。
自带回读校验（写完重新解析资产、逐条比对中文），实测 **19/19 通过**。

**改后体积变化**（UnityPy 重打包会重排字符串表，不必惊慌）：
`level1` 833288→833304 · `level2` 102840→102840 · `level5` 10776→10000 ·
`mainData` 173728→172176 · `resources.assets` 65430296→65430304 · `sharedassets6.assets` 8396920→8393536。

**★ 沙箱坑：`.venv\Scripts\python.exe` 一个字节都写不进去（2026-10-20 卡了近 1 小时）**

现象：烘焙脚本报 `PermissionError: [Errno 13] Permission denied: 'CW3_Data\level1'`，
但**同一个提权 shell 里** `pwsh` 能写、`C:\Python314\python.exe` 能写、只有 venv 的 python 不能。

根因：DSH 沙箱让「**从工作区目录内启动的可执行文件**」以 **Low 完整性级别**运行，
而游戏资产文件是 Medium —— Windows 的 no-write-up 规则禁止低完整性进程覆盖高完整性文件。
（新建文件、写 `_cw3hanhua` 下自己的文件都不受影响，所以 `--backup`、`_diag` 写 JSON 都"正常"，
只有覆盖游戏资产这一步失败，极易误判成"文件被占用"。）

解法：**用系统 python 解释器 + venv 的 site-packages**（由沙箱外的 `C:\Python314` 启动，
拿 Medium 完整性）。脚本里 `precheck_writable()` 会把这条建议直接印出来。
`_cw3hanhua\tools\collect_ui_strings.py` 等只读脚本用 venv 跑没问题。

**★ `--restore` 的静默失效（2026-10-20 用户实测发现，已修）**

现象：资产已烘焙成中文后跑 `--restore`，**什么都没发生，也没有任何报错，退出码 0**。

根因：`main()` 里 `--restore` 的判断排在 `plan = scan(d)` **之后**，而 `scan()` 是按
「英文原文查词表」生成计划的 —— 资产里已经是中文，查不到任何命中 → `plan` 为空 →
命中 `if not plan: print("没有需要改的 TextMesh"); return 0`，**永远走不到 `--restore` 分支**。
计划与恢复本来毫无关系（恢复只需要 manifest），所以修法是把 `--restore` 提到最前面、
完全不依赖 `scan()`。

顺带加的护栏：`--backup` 现在要求 `plan` 非空。否则「之前没备份、而现在资产已是中文」时
运行 `--backup`，会把**中文状态当成原件**冻存进 `_assets_orig/`，此后 `--restore` 只会还原出
中文，**永远回不到英文**且无从察觉。

修后实测：`--restore` 六个文件全部写回，sha256 与 `manifest.json` **逐字节一致**；
再跑一次打印「已是原始状态，无需恢复」（幂等）；随后完整跑一遍恢复正常，19/19 校验通过。

**回归自检**（`_cw3hanhua/_diag/diff_bake.py`，只读）：拿烘焙前扫的
`_diag/uilabel_inventory.json` 当基线，重扫现役资产逐条对比 → 实测
**1029 条 UILabel 文本变了 0 / 丢了 0 / 增了 0**，完全未被触碰；六个文件的对象数也没变
（`level1` 5196、`resources.assets` 2185 等）。

> ★ 两个对比陷阱，都踩过：
> ① **不能直接读 `_assets_orig/` 里的备份做 diff** —— 备份离开 `CW3_Data/` 后，MonoBehaviour 的
> MonoScript 要跨文件引 `sharedassets0.assets`，UnityPy 报 `FileNotFoundError: File
> sharedassets0.assets not found in ...\_assets_orig`，备份侧 **0/1827** 可读（现役 1807/1827）。
> **备份只能整体拷回，不能就地解析。**
> ② 基线只记**非空**文本（1029 条），而全场有 1206 个 UILabel —— 多出的 177 个 `mText` 是空串。
> 对比前必须滤空，否则会误报「新增 177 条、有 UILabel 被波及」。

**★★ 最大的坑：UnityPy 会把 `script_type_index` 抹平 → 主菜单按钮点不动（2026-10-20 定位并修复）**

现象：烘焙后进游戏，**主菜单所有按钮都点不动**；`--restore` 还原资产后立刻恢复。
不崩溃、不报错，日志里只有一堆 `MainMenu` 的 `NullReferenceException`。

根因在 UnityPy 1.25.4 的 `UnityPy/files/ObjectReader.py:109-112`：

```python
if 11 <= header.version < 17:
    script_type_index = reader.read_short()
    if serialized_type:
        serialized_type.script_type_index = script_type_index   # ★ 写进共享的 type 表
```

`serialized_type` 是**整张类型表里共享的那一个对象**（`:94-97` 按 `class_id` 找），
每读到一个对象就覆盖它一次；到 `write()`（`:175`
`writer.write_short(self.serialized_type.script_type_index)`）时，**所有对象都写回最后
读到的那个值**。→ 每个对象 2 字节的脚本索引被抹成同一个数。

后果不是崩溃，而是**静默的功能损坏**：Unity 加载 MonoBehaviour 时靠这个索引找脚本类，
索引错位 → 组件绑到错误的脚本 → 运行时变成「C# 壳还在、native ptr=0」的死组件。
`MainMenu` 的 6 个 sprite 字段里 3 个（`creditsSprite`/`exitGameSprite`/`loadFileSprite`）
在 `Awake` 时就已经是死的 → `MainMenu.Update` 的悬停比较链里
`callvirt Component.get_gameObject()` 抛 NRE → Update 中途返回，**永远走不到 IL `0477`
的 `Input.GetMouseButtonDown` / `0482 OnMyMouseDown`** → 点击全丢。

**踩过的弯路（务必记住）**：因为 `get_raw_data()` 逐字节相同，一度判定「烘焙无辜」，
静态查了对象表 / type_id / externals 顺序 / 类型表 / 对齐 / 区间重叠 / 6 个组件的
raw md5 / GameObject 重名 / 脚本实例数 —— **十几项全部清白**，完全找不到原因。
教训：**「对象数据未变」不等于「文件可用」**，坏的可能在 metadata 里那几字节。
决定性实验是**纯往返**：`UnityPy.load()` → `sf.save()`，一个文本都不改 ——
`level1` 就出现 **316 字节差异，全部落在 metadata 区**（`data_offset`=148048 之前），
每处只差 1 字节，间距 84/140/112/56/196，正好是对象记录的步长。

**修法**（已固化进 `bake_textmesh_assets.py` 的 `install_unitypy_fix()`）：
monkey-patch `ObjectReader.from_reader`，读完从 `reader.Position` 回退 2 字节
（version 15/16 还要多退 1 字节的 `is_stripped`）把**每个对象自己的**索引记下来；
再 patch `ObjectReader.write`，写前把 `serialized_type.script_type_index` 设回该对象的值。
★ 键必须用 `(id(assets_file), path_id)` —— **不同资产文件的 path_id 各自编号会重号**。

**验证**：纯往返差异字节 **316 → 0**、文件逐字节完全相同；修复后烘焙的六个文件
逐对象比对 `script_type_index` **与原件全部一致**（level1 5196 / level2 859 / level5 78 /
mainData 49 / resources.assets 2185 / sharedassets6 11，不同的都是 0）。

> 顺带澄清一个假象：`read_typetree()` 里这 6 个组件的 `m_Script` 读出来是垃圾
> `{'m_FileID': 16777216, 'm_PathID': 1895825408}` —— **原件里也是这个值**，属于
> level1 那 20 个解析不出的 MonoBehaviour（缺 `Assembly-UnityScript.dll`），
> 不是烘焙造成的，别拿它判断脚本对不对。

**③ 类留给代码路线（步骤 6，已实现 → 见 4.6a）**
`TextMesh.set_text` 全游戏只有 **8 个宿主方法 / 15 个调用点**（`ilx callers --type TextMesh set_text`）：
`TextLabel.set_text`×5（prefab 驱动，`ilx callers` 命中 0 处）、`PlanetManager.set_planetName`×2、
`StarManager.set_starName`×2、`StarManager.Refresh`×2、`CRPLTowerManager.set_text`×1、
`DemoScreenManager.ShowText`(params=9)、`EpilogueManager.ShowText`(params=8)、
`PreludeManager.ShowText`(params=7)。补丁只能打在这 8 个宿主方法上。

**★ 读 MonoBehaviour typetree 的必要条件**（两个扫描脚本都踩过）：
必须 `TypeTreeGenerator("5.2.3f1")` + `load_local_dll_folder(CW3_Data/Managed)` 并赋给
`env.typetree_generator`，否则 `read_typetree()` 对 86% 的 MonoBehaviour 抛异常
（`level1` 实测：无 generator 时 258/1827 成功，有 generator 时 1807/1827）。
**没有 generator 时读不到会表现为「扫出 0 个」，很容易误判成"资产里没有"。**
另：`resources.assets` 解 typetree 极慢（**138 秒**），全量扫描要给足超时。

### 4.6a TextMesh 的代码补丁（`CW3HanhuaText.cs` v0.10.0，步骤 6）

改资产能覆盖**静态**的 19 条，但有一批 TextMesh 的文本是**运行时才写**的，
资产里只是占位值（`'Andro'`、`'4'`、空模板），烘中文也留不住 → 只能打代码补丁。

**★ 五个补丁点，分两种改法，不能混**

| 方法 | 改法 | 为什么 |
|---|---|---|
| `PreludeManager.ShowText`（params=7, `0x060011BF`） | **prefix 改 `__0`** | arg1 是纯文案，IL `008F ldarg.1 / 0090 callvirt TextMesh.set_text` |
| `EpilogueManager.ShowText`（params=8, `0x06000B81`） | **prefix 改 `__0`** | 同上（`009C/009D`） |
| `DemoScreenManager.ShowText`（params=9, `0x06000B4C`） | **prefix 改 `__0`** | 同上（`00A9/00AA`） |
| `StarManager.set_starName`（`0x06000DC5`） | **postfix，只改 `starTitle`** | ★ 见下 |
| `PlanetManager.set_planetName`（`0x06000D75`） | **postfix，只改 `planetTitle`** | ★ 同上 |

★★ **为什么 name setter 绝不能改入参**：IL 是
`0000 ldarg.0 / 0001 ldarg.1 / 0002 stfld _starName` —— 先把名字存进**数据字段**再写 TextMesh。
`get_starName` 有 **10 个调用方**，其中 `SystemManager.LaunchMission` 拿它拼**存档文件名**：
`'Alpha Sector : ' + get_starName() + ' : ' + … + '.cw3'`（`'Prospector Zone : '`/`'Tormented Space : '`/
`'Arc Eternal : '` 同构）；`EditSystemGUI.DoStar`、`StarManager.WriteXML` 也用它。
`get_planetName` 更是有 19 个调用方（`ShowStorySystemMessageForPlanet`×12、`LaunchMission`×4…）。
**改入参 = 存档路径变中文 = 毁存档。** 所以只改显示用的那个 TextMesh 字段
（`starTitle`/`planetTitle` 只被各自的 setter 与 `Awake` 碰，改它不影响任何逻辑）。

**故意不打的三个**

- `StarManager.Refresh`（`0x06000DD4`）：只写 `starInfo` 的进度串（`"3/12"` / `"?"`），纯数字，不翻。
- `CRPLTowerManager.set_text`（`0x06001846`）：传的是玩家 CRPL 脚本产出
  （调用方 `CrplCore.ProcessCommands`、`CRPLTowerManager.ReadXML`），翻了就毁了玩家脚本。
- `TextLabel.set_text`（`0x06001149`）：`ilx callers` 命中 **0 处**（prefab/UnityEvent 驱动），
  资产里那 5 个 `'4'` 是模板占位，没有词表条目 → 无处施力。

**实现要点**
- 三个 `ShowText` 的参数量不同（7/8/9），但**共用一个 prefix 方法** `PrefixAnyShowText(ref string __0)` ——
  Harmony 按**参数名**匹配，只声明关心的那个即可。
- postfix 签名用 `(object __instance)`，运行时按字段名取 `TextMesh`；
  装之前校验 `FieldInfo.FieldType == typeof(TextMesh)`，类型不对就报错不装。
- 登记进独立的 `Dictionary<int, MeshSwap> meshSwaps`（`MeshSwap = {mesh, en}`），
  与 UILabel 的 `swaps` 分开；F4 还原时 `mesh.text = en` 即可。
- HUD 第 4 行加 `mesh=调用/命中`，计数不动就说明补丁没装上或那段文本没被写过。

### 4.7 待办与已知缺口

#### ① 资源条模板串（15 条）—— 已定方案：碎片替换，**暂不实施**

建造中/悬停时显示的单位标题是**运行时 `String.Concat` 拼的**：

```
CommandCenter.GetUnitTitleDisplay(UnitManager)   token=0x06000A33  RVA=0x46EA4  il=876
  0000: UnitManager.GetUnitGameName()          → unitName（如 "Collector"）
  0015: newarr Object, size 6
        [0] = unitName
        [1] = "  [ffffff]Building:[-][00ff00] "   ← 模板串夹在中间
        [2] = health   [3] = "/"   [4] = BUILD_COST   [5] = "[-]"
  0054: String.Concat(Object[])                → 再 set_text
```

最终文本形如 `Collector  [ffffff]Building:[-][00ff00] 100/50[-]`。
**模板既不在头也不在尾** → 精确匹配和前缀匹配都无效（曾误判为"前缀匹配"，
反汇编后证伪）。`LookupConcat` 只切 `\n`，也兜不住。

**已定方案（用户 2026-10-20 拍板）：碎片替换** —— `Lookup` 失败后用一张
「已知碎片表」在整串里做 `IndexOf` + 原地替换（碎片含色标与空格，精确整碎片命中才换）。

**暂不实施，先做 4.6。** 实施时还要先补这批模板串的译文。词表现状（`strings.json` 里
以 `  [ffffff]` / `[-]` 开头的模板条目共 **17 条，目前只译了 1 条**）：

| 状态 | 条目 |
|---|---|
| 已译 | `  [ffffff]Upgrading to Lvl: ` → `  [ffffff]升级至等级： ` |
| 有条目未译（16 条） | `Building:` / `Building...` / `Health:` / `Cargo:` / `Ore:` / `Armed:` / `Pad:`（两种颜色）/ `Ammo:`（两种颜色）/ `Energy:`（两种颜色）/ `Aether:` / `Fully Upgraded!` / `AC:` / `[-] [00ff00]` |
| 单个词已译（碎片替换可直接用） | `Energy` → `能量`、`Ammo` → `弹药`、`Aether:` → `以太：`、`Fully Upgraded!` → `已完全升级！` |

`AC:` 按术语表**保留英文**（AC = Anti-Creeper）。

#### ② 其它待确认

- **`Andro` / `DMD` / `'4'` 三个是否翻译未定**：`Andro`（`resources.assets` pid=1640/1644，
  `Wormhole/StarTitle` 与 `Star/StarTitle`）与 `DMD`（`level1` pid=3342，`Star/SpriteLabel`）
  词表里都没有译文；`'4'` 是 `TextLabel` prefab 的五层占位值（`ilx callers` 命中 0 处，无处施力）。
- 打包/分发方案未定（BepInEx + 两个 DLL + `_font/` + `_dict/` 的目录形态）。

---

## 五、环境与陷阱

### 5.1 沙箱写入权限

游戏重装后，shell 的受限进程可能无法写工作区（连游戏目录根都不行），但磁盘权限本身正常
（`diagnose-windows-sandbox-acl` 判定 `NOT_THIS_CLASS`）。
**`write`/`edit` 文件工具仍正常**；`csc.exe` 输出 DLL 可能失败。

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

- **`/platform:x86` 必须**（游戏 32 位）。用 64 位 pwsh 的
  `[System.Reflection.Assembly]::LoadFrom` 加载插件会报
  `The assembly architecture is not compatible` → **验证插件只能靠 `ilx`，不能靠 PowerShell 反射**
- **`/codepage:65001` 必须**（源码含中文，否则 csc 3.5 按 ANSI 读成乱码）
- **必须引用游戏自带 `Managed` 目录的 DLL**（Unity 打包版 `mscorlib 2.0.5.0`）
- 目标框架只能是 **net35**（游戏跑在 Mono 2.x）
- **`.cmd` 必须是 ASCII + CRLF**：cmd.exe 按 OEM 代码页解析批处理，
  写 UTF-8 中文注释会被切碎成乱码命令
- **`if exist (...)` 块里的 `echo` 文案不许出现裸 `)`** —— 踩过：诊断层那段的
  `echo == CW3HanhuaDiag.dll (TEMP diag, 查完即删) ==` 里有一个 `)`，cmd.exe 在
  **第一个不平衡的 `)`** 处就闭合了 if 块，后面的 csc 命令行全漏到块外**无条件执行**。
  于是删掉 `CW3HanhuaDiag.cs` 之后编译照样报
  `error CS2001: 未能找到源文件…CW3HanhuaDiag.cs` / `CS2008: 未指定输入`，
  而 `if exist` 明明为假 —— 极易误判成"文件没删干净"。块内一律用 `-` 或 `[]`。

### 5.3 C# 3.0 的坑（用 csc 3.5 时）

1. **没有命名实参、没有可选参数、没有字符串插值**（`bool fast = false` → `CS0241`，
   要写成重载）
2. **HarmonyX 的 `AccessTools.Method` 是 4 参数带默认值** → 必须写全
   `AccessTools.Method(t, "name", null, null)`
3. **`Harmony.Patch` 的槽位次序反直觉，首选 `PatchProcessor`**：
   ```csharp
   PatchProcessor proc = h.CreateProcessor(m);
   proc.AddPrefix(new HarmonyMethod(...));
   proc.AddPostfix(new HarmonyMethod(...));
   proc.AddFinalizer(new HarmonyMethod(...));
   proc.Patch();          // 名字即语义，不会数错
   ```
   6 参数重载的真实次序（实测 `0Harmony.dll` 的 IL，`Harmony.Patch token=0x060000CD`）：
   `arg2→Prefix  arg3→Postfix  arg4→Transpiler  arg5→**Finalizer**  arg6→ILManipulator`
   —— **finalizer 在倒数第二位，不是最后**。5 参数重载在本版本标了 `[Obsolete]`
   → 编译报 `CS0619`，用不了。
4. **游戏程序集里有全局 `Path` 类** → 写 `System.IO.Path.Combine`
5. Unity 5.2 没有 `SceneManagement` → 用 `Application.loadedLevelName`
6. `UIPanel` 没有 `SetDirty()` → 用 `UILabel.MarkAsChanged()`
7. `UIFont` 是 MonoBehaviour → 运行时用 `new GameObject().AddComponent<UIFont>()`

### 5.4 工具链

**主用 `tools\ilx.exe`（Mono.Cecil 0.10.4）**：

```powershell
$t = "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3\_cw3hanhua\tools"
$d = "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3\CW3_Data\Managed\Assembly-CSharp.dll"

& "$t\ilx.exe" dump UILabel set_text --dll $d      # 精读 IL ★ 必须带 --dll
& "$t\ilx.exe" callers --type UILabel set_text     # 按声明类型过滤调用点
& "$t\ilx.exe" fields --type UILabel mProcessedText   # 谁直接读写字段（查成员只能用这个）
& "$t\ilx.exe" find MainMenu --dll $d              # 按名字搜类型/方法/字段
& "$t\ilx.exe" stats --dll $d                      # 全程序集方法清单
```

★ **`find` 只匹配类型/方法/字段名** —— 查**字符串字面量**、GameObject 名（`'ACTIVATE'`、
`'UnitToBuildLabel'`）一律命中 0 项，要用 `dump` 或 `callers`。
★ 不带 `--dll` 会默认查 `Assembly-CSharp.dll`，查其它程序集时容易误得「命中 0 项」。
★ **`fields` 才能查"谁读写某字段"**；`find "Type::member"` 这种写法命中 0。

**★ `tools\ilspy.cmd <dll> -t <类型>` 把方法反编译成 C#** —— 本项目最有效的排查手段。
NGUI 行为怪异时**先反编译再推理**，产物在 `_cw3hanhua/_diag/`。

**⚠ 手写 IL 解析器曾被删除**：三类解码 bug（tiny header、`throw` 长度、`conv.ovf.*` 缺失）
污染过历史数字（`set_enabled` 371 vs 真 249、`UILabel.set_text` 292 vs 真 322、
`TextMesh.set_text` 12 vs 真 15）。**只信 `ilx`**，不要再手写解析器。

**其它**：Python 脚本一律用 `_cw3hanhua\.venv\Scripts\python.exe` 跑。
控制台中文乱码解法：`$env:PYTHONIOENCODING="utf-8"` +
`[Console]::OutputEncoding = [System.Text.Encoding]::UTF8`。

### 5.5 游戏状态文件的坑

`%APPDATA%\creeperworld3\GameSettings.xml` **声明 `encoding="utf-16"，实际是 UTF-8 无 BOM**。
按声明读会乱码，写回会写坏。改前**先备份**，显式按 UTF-8 读写。

### 5.6 Harmony 补丁的六个假象（勿重走）

| 假象 | 真实原因 |
|---|---|
| **补丁完全没装上，界面一个字不变，但外层 try/catch 什么也没接到** | **finalizer 传到了第 6 个槽位（ILManipulator）**。Harmony 只往日志写一行 `[Error] Failed to patch … TargetParameterCountException`，**不抛异常**。用 `PatchProcessor` 可根除，并且装完必须回读 `GetPatchInfo` 自检 |
| 误判「`__result` 改写失效」 | 改写补丁被传进了 **prefix** 槽位；prefix 里改的值会被原方法返回值覆盖 |
| 补丁全部不生效，抛 `ArgumentNullException` | prefix 写成 `new HarmonyMethod(null)`（包装了 null 方法） |
| 误判「Harmony detour 无效」 | 自检目标是**空方法**被 JIT 内联；加 `[MethodImpl(NoInlining)]` 后正常 |
| 补丁拦不到某个方法，计数恒为 0 | 该方法的执行**早于补丁安装**（`SteamManager.Awake` 就属于这类：BepInEx 的 Chainloader 在它内部才触发，任何在 BepInEx 生命周期内装的补丁都拦不到它） |
| HUD 文字黑字看不清 | `GUI.color` 污染后续绘制，画完 `GUI.Box` 未恢复 |

### 5.7 已退役的东西（别找，别重建）

| 东西 | 退役原因 |
|---|---|
| `AtlasProbe` / `AtlasWriteProbe` / `UIFeasibilityProbe` | 三个图集探针，结论已在 4.4 / 4.4a，源码已删 |
| 手写 IL 解析器 `il.py` / `ilscan.py` / `ilscan_field.py` | 有解码 bug，只信 `ilx`（5.4） |
| `scan_all_text.py` / `collect_fingerprint.ps1` / `_archive/` / `_bepinex_backup/` | 一次性或过时工具，现行口径见 4.2 / 5.4 |

---

## 六、附录：BepInEx 与 NGUI 版本

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
- **必须用 BepInEx 5**：be6 的 MonoMod 在这套老 Mono 上 `ReflectionHelper` 静态初始化 NRE
- **无害警告** `Could not find property ... isBatchMode`（Unity 5.6+ API）—— 反是
  `AccessTools` 初始化成功的正面信号
- **启动方式**：Steam 正版目录从 Steam 启动不需要 `steam_appid.txt`；副本目录直接双击
  `CW3.exe` 需要同目录放该文件（内容 `280220`）。两种情况都需要 **Steam 客户端正在运行**
- 其它可用注入点（全局命名空间）：`SteamManager`（已验证）、`Localization`

**重装与登记**：从 [BepInEx 5.4.23.5 x86](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5)
重新解压到游戏根目录，然后改上面三行。装完检查三件事：

```powershell
$g = "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3"
Test-Path "$g\winhttp.dll"                     # 必须 True
Get-Content "$g\.doorstop_version"             # 应含 4.5.0
Get-Content "$g\BepInEx\config\BepInEx.cfg" -Encoding UTF8 |
  Select-String -Pattern '^Assembly|^Type|^Method'   # 三行 = 上面的值
```

**卸载**：删 `winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`BepInEx\` 四项。
升级/重装**务必保留 `BepInEx.cfg`**，否则注入点被还原，游戏无法启动。

### B. NGUI 版本定位

CW3 的 NGUI 代码编译在 `Assembly-CSharp.dll`（无命名空间）。最接近的官方提交是 `625abae8`
（2013-09-03），但**只对 `UILabel` 成立**：CW3 的 `UIFont` 是更晚的修订——有
`RecalculateDynamicOffset`、**没有** `Request(string)`、动态分支不受 `DYNAMIC_FONT` 宏控制。
**所以读 `UIFont` 一律以反编译为准**（`_diag/UIFont.cs`）。

要拿上游 `UILabel` 源码：`_ngui_repo/` 里有完整历史，
`git -C _ngui_repo show 625abae8:Assets/NGUI/Scripts/UI/UILabel.cs`。

`UILabel` 判别特征：26 个字段，**同时具备 `mLastScale` 且不具备 `mFontSize`/`mFontStyle`/`mOverflow`**。

---

## 七、目录与维护纪律

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
│   ├─ test_classify.py        classify() 回归样例
│   ├─ prep_batches.py         未译分流 + 切批（会重建 _dict/work/）
│   ├─ merge_work.py           work/ 译稿 → batches/
│   ├─ check_batch.py          单批译稿校验
│   ├─ pack_skips.py           「人工判定不译」固化 → _dict/skipped_manual.json
│   └─ dict_stats.py           覆盖率报告 → _dict/STATUS.md
├─ plugin/             ★ 两个在用源文件：CW3Hanhua.cs（字体层 v0.8.2）
│                      CW3HanhuaText.cs（文本层 v0.10.0）
├─ _font/              ★ 运行时：atlas_zh.png + atlas_zh.bin（字体层的全部输入）
│                      ★ 工具链：font_{16,20,28}.{json,png}（重烘图集的必需输入）
├─ _dict/              ★ 词表：strings.json（唯一数据）/ glossary.md（术语表）
│                      / zh_cn.dict（编译产物）/ STATUS.md（自动生成）/ batches/（23 份批次）
│                      / skipped_manual.json（565 条「人工判定不译」，勿删）
├─ _diag/              ilspy 反编译源码 + 只读扫描脚本（scan_textmesh.py / scan_uilabel.py）
│                      ★ 改插件前必查；不进版本库（gitignore），可随时重生成
├─ _ngui_repo/         NGUI 仓库 clone，77 MB（gitignore；查上游源码用）
└─ .venv/              Python 环境（UnityPy / dnfile / dncil / fontTools / Pillow，gitignore）
```

`_diag/` **不进版本库**：里面是 `tools\ilspy.cmd` 一次就能重生成的反编译产物
（例如 `& tools\ilspy.cmd CW3_Data\Managed\Assembly-CSharp.dll -t UIFont`）。
它叫 `diag` 是因为最初是个「诊断产物」目录，名字沿用下来。

**版本控制**：仓库根 = 游戏安装根目录（`.git` 与 `CW3.exe` 同级）。
`.gitignore` 用「顶层白名单 + 子目录黑名单」，只放行 `_cw3hanhua\`、`BepInEx\config\`、
`AGENTS.md`、`doorstop_config.ini`。
★ **本目录就是游戏安装目录，永远不要在这里执行 `git clean`** —— 会连游戏本体一起删掉。

### 维护纪律

1. **只维护这一份文档。** 不要新建 `XXX.md` —— 新内容写进对应章节。
2. **控制篇幅，但不要为行数牺牲信息。** 只留**结论、证据、可复现操作**。
   判断标准是「这条信息会不会在下一次动手时救我一命」。
   **过时的中间过程、已废弃的方法直接删**（历史结论压成一句就够，或者进 5.7 的退役表）。
3. **数字必须可复现。** 引用调用点/指令数时写明用 `tools\ilx.exe` 的哪条命令
   或 `tools\collect_ui_strings.py` 的哪次输出；手写解析器的数字一律不要引用（见 5.4）。
   文本条数的唯一权威是 `_dict/strings.json`（现行 **2400** 条候选）。
4. **结论必须有证据等级**：区分「IL 静态证据」「运行时实测」「源码推断」。
   不要把推断写成事实 —— 本项目曾多次因"看起来像"而下错结论（最近一次：
   把资源条模板串的修法说成"前缀匹配"，反汇编后才证伪，见 4.7）。
5. **失败与假象也要记**，一张"已排除假设"表比结论本身更省时间（5.6）。
