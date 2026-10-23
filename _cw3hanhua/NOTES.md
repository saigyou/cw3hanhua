# CW3 汉化项目 · 技术文档

> **这是唯一文档。** 只保留**结论、证据、可复现操作**。
> 过程性叙述、已废弃的方法不单独存档 —— 历史结论压成一句留在对应章节，或者直接删。
> 维护纪律见文末。

---

## 一、一页速览

| 问题 | 答案 |
|---|---|
| 怎么改 | **不改游戏文件**：BepInEx 5 + 一个插件 DLL（含字体层与文本层）+ 外置词表 + 外置图集 |
| 字体怎么办 | **离线烘 BMFont 图集 → 运行时把中文字形并进 GUI Atlas**，走游戏原生的**位图分支**（3.4 / 4.4） |
| UILabel 文本怎么变中文 | `UILabel.ProcessText` 的 prefix + postfix（**全部 1029 个 UILabel 的必经之路**，见 3.3） |
| TextMesh 文本怎么变中文 | 两条腿：**改资产**把 19 条静态串烘进 prefab（3.6 / 4.6）；**代码补丁**管运行时才写的那几条（`ShowText` / `set_starName` / `set_planetName`，4.6a） |
| 游戏自带多语言 | **是死的**（词典为空、`Get` 零调用，3.7） |
| 当前进度 | 字体层 ✅ / UILabel 文本层 ✅ / TextMesh 静态 19 条 ✅ / TextMesh 动态补丁 ✅ / 单 DLL + F3 合并 + 无条件自动开启 ✅ / v1.1.0 分段重组 ✅ / v1.3.0 CRPL 源头换字 ✅（用户游戏内确认）/ **v1.3.1 HUD 默认关 + 去掉文本层开关 + Cannon/ARM 定名 ✅** / **v1.3.2 单位标题与「Click or 'P'」修好 ✅** / Alternate=备用 ✅ / **v1.4.0 IMGUI 层（结算面板等）✅ 用户游戏内确认** / **`Tech: RELAY` 等 20 个枚举名 + 主菜单新闻条 ✅（纯词表，2127 条）** |
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
| 插件（**单 DLL**） | `BepInEx/plugins/CW3Hanhua.dll` **v1.4.0** = `plugin/CW3Hanhua.cs`（字体层）**+** `plugin/CW3HanhuaText.cs`（文本层）<br>唯一的 `[BepInPlugin("cw3hanhua.core")]` 在字体层，文本层是同程序集普通类 `TextPlugin`，两层之间**零反射**（4.1）。<br>① 字体层：合并图集 + HUD（**默认关闭**，F12 开）+ 启动**无条件自动开启**（无配置项，4.1）。<br>② 文本层：读 `_dict/zh_cn.dict`（**2127 条**）；**四条替换路径**（4.3）：`ProcessText` 覆盖全部 UILabel、5 个 TextMesh 写入点、`CrplCore.ProcessCommands` postfix 覆盖全部 CRPL 消息、`GUILayout.*` 文字重载覆盖全部 IMGUI 界面（4.3a）。**四层全部已实测通过**。 |
| 合并图集 | `_font/atlas_zh.png` 4096×8192（6.0MB）+ `_font/atlas_zh.bin` —— **字体层的全部运行时输入**（4.4）<br>**2026-10-21 已修 alpha 平方 bug 并重建**（详见 4.4「已修的 bug」），**用户已目视确认效果好多了**（提交 `cd60b9a`）。回退旧版本：`git cat-file blob 4933cf5:_cw3hanhua/_font/atlas_zh.png > _font/atlas_zh.png`（`.bin` 未变，不用换） |
| 字形度量 | `_font/font_{16,20,28}.{json,png}` —— **重烘图集的必需输入**<br>雅黑 Bold @ `--scale 0.80`，每套 **7544 字形**。改了字集必须重跑 `build_atlas.py` |
| 按键 | **F3 汉化总开关**（两层一起开/关）/ **F5** 打印未命中 / **F9** 重载词表 / **F11** dump / **F12** HUD（共 5 个；**F4 已于 2026-10-23 退役并入 F3**）<br>启动**无条件自动开启**（**无配置项**，4.1「自动开启」） |
| 待办 | `Andro` / `DMD` / `'4'` 是否翻待定（4.7②）；`' sec'` / `' spores'` 等尾缀常量保持英文（4.7①）；其余待确认项见 4.7② |

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
| **DLL 写死字符串** | 822 条（与资产去重后，`strings.json` 现行 **3317 条**候选串，见 4.2） | 最终都落到上面两种载体上（CRPL 消息另走 IMGUI，见 4.7④） |

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

### 3.3 ★ UILabel 在 `UILabel.ProcessText` 上收口

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

其余 `get_text` 调用点无害（都是读回自己的旧值做拼接/判空，已逐个核过）：`UIInput.Init`×2、
`UIInput.OnSelect`×2、`UIInput.set_defaultText`、`UILocalize.Localize`、`InputManager.OnPress`、
`NumenManager.SetPriceAndLevelLabels`、`ResourcePackManager.Update`、
`UnitToBuildManager.SetInfoLabel`、`UILabel.get_hasChanged`/`set_hasChanged`。

#### ⚠ 拼接串：词表里查不到整串

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

> **`LookupConcat` 只切 `\n`**，「同一行内拼接数字」的情形它管不了 —— 那类靠 4.7③ 的分段重组。
> **`Lookup` 目前是四级**（精确 → 拼接 → 归一化 → 分段），完整定义见 4.3 的表。

### 3.4 ★ 字体层：把中文字形并进 GUI Atlas

**一句话**：不新建材质、不改 `mAtlas`，而是**把 4096×8192 的新图集直接换给
`atlas.spriteMaterial.mainTexture`**，于是文字与精灵仍是同一份材质 → 同一个 draw call →
遮挡关系天然与原版一致。细节与踩过的坑见 **4.4**。

> 早期方案是「拆 draw call：`mAtlas=null` + 自建材质」，**已废弃**（会破坏遮挡，
> 见 3.8 ①）。

### 3.5 BMFont / BMGlyph 字段语义

```csharp
// BMGlyph
public int index;   // 字形索引 = 字符码
public int x, y;    // 图集内像素坐标（左上角）
public int width, height;
public int offsetX; // 相对笔位左侧的偏移
public int offsetY; // 相对【行顶】的下移量
public int advance; // 前进宽度
public int channel; // 15 = 普通 alpha；1/2/4/8 = packed 字体的通道

// BMFont：mSize(charSize) / mBase(baseOffset) / mWidth(texWidth) / mHeight(texHeight) / mSaved
```

**构造方式**：`bm.GetGlyph(code, true)` 创建并登记（内部 `mSaved.Add` + `mDict.Add`）。

**三条结论**（`UIFont.Print` 的几何公式见 4.4「坐标约定」，不在这里重复）：
① 几何全按 `size = mFont.charSize` 归一化，顶点坐标是「相对笔位」的比例值；
② **`mBase` 在整个 `Print` 中从未被使用**；③ `mUVRect=(0,0,1,1)` 时 `glyph.x/y`
就是图集内像素坐标。

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
> 后 label 必须重新赋 `material` 才会注册进 panel）—— **合并图集方案根本不拆 draw call，
> 这两条不会再碰到**，故不展开。若将来有人想回到独立材质路线，先读 `UIWidget.material` 的
> setter：它才是 `CreatePanel()`（注册控件）的唯一入口。

**① 不能整张图集当 `_MainTex` 交给裁剪**
`UIDrawCall.OnWillRenderObject` 里 NGUI 的裁剪对材质做**纹理重映射**（`mainTextureOffset` /
`mainTextureScale`），它假定 `_MainTex` 就是面板内容本身。字体图集整张用（`mUVRect=(0,0,1,1)`）
会被打乱 UV。`SoftClip` 面板（`KeyMappingsPanel`）会踩到。**接入真正的滚动列表时必须处理。**

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
| A1–A3b | 离线烘焙（图集 + 字集检查）→ 插件字体层 → 英文/中文验证 | ✅ |
| B1 | 静态全量收集 + 词表骨架 | ✅ |
| B2 | `ProcessText` prefix+postfix+finalizer | ✅ v0.9.0 实测通过 |
| B4 | HUD 合并成左上角一块 | ✅ v0.8.2 通过；v0.8.3 重做；v1.0.0 改成单 HUD 宿主 |
| B6 | 改资产：TextMesh 中文烘进 prefab（19 条） | ✅ 实测通过（`script_type_index` bug 修好后，4.6） |
| B7 | ③ 类运行时覆盖对象的代码补丁（v0.10.0） | ✅ 已实测 |
| C1–C4 | 翻译：主菜单 / HUD / 建造菜单 / 剧情 / 剩余分流 | ✅（数字见 4.5） |
| D | 打包 / 分发 | ⬜ |

> 已废弃的阶段（不要再做）：**B3** 的 `set_text` prefix + 周期 sweep、
> **B5** 的「缓存 + 每帧快扫」。教训见 4.3 末的「已废弃：sweep 路线」。
> 当前全部替换路径见 4.3 的「替换路径」表 —— **那里是唯一的口径**，本表不重复。

**为什么字体层先做**：否则中文显示成方块，无法区分"字体没做"还是"翻译错"。
**为什么先用英文验证**：文本不变时，布局/裁剪/遮挡的异常只能归因于字体层。

### 4.1 字体烘焙与插件按键

**按键（`plugin/CW3Hanhua.cs` + `plugin/CW3HanhuaText.cs` → 同一个 `CW3Hanhua.dll` v1.4.0，共 5 个）**

| 键 | 层 | 作用 |
|---|---|---|
| **F3** | 两层 | **汉化总开关**：字体层换合并图集 / 还原 **+** 文本层接词表 / 还原 |
| **F5** | 文本 | 打印未命中清单（`missSeen`，缺哪些译文） |
| **F9** | 文本 | 重载 `zh_cn.dict`（改完词表不用重启） |
| **F11** | 字体 | dump 到 `BepInEx/CW3HanhuaFont.txt`（所有内部数字都在这里） |
| **F12** | 字体 | HUD 开关（**HUD 默认关闭**，v1.3.1 起） |

★ **F3 是唯一总开关（2026-10-23，F4 已退役并入 F3）**：两个键意味着每次按两下、且容易
只开一半（字形换了文本还是英文，看着像"翻译坏了"）。`HanhuaPlugin.SetHanhua(bool,bool)`
里 `ApplyFont()/RestoreFont()` 之后直接调 `TextPlugin.SetOn(hanhuaOn)`，两层同生同死。
`TextPlugin.SetOn` 仍返回 `bool`：词表为空时 `Enable()` 会报 `!!`，返回值让字体层把
"文本层=未就绪"写进同一次 dump。

★ **两个 DLL 已合成一个**（v1.0.0）：`HanhuaPlugin` 是唯一 `[BepInPlugin]`/MonoBehaviour，
`TextPlugin` 退化成同程序集普通类，**两层零反射**（`Init`/`Tick`/`SetOn`/
`ErrorLine`/`DumpLine` 全是直接调用；`HudLine` 已在 v1.3.1 删除，见 4.1「HUD」）。生命周期统一由 `HanhuaPlugin` 驱动：
`Awake`→`TextPlugin.Init()`；`Update`→先 `Tick()` 再处理按键；场景切换只在 `Update`
检测一处再通知 `OnSceneChanged()`。两个 `.cs` **仍一层一个文件**（各自教训留在原处）。
`BepInEx/plugins/CW3HanhuaText.dll` 由 `build_plugin.cmd` 自动删 —— 旧文件是独立插件
（GUID `cw3hanhua.text`），留着会重复装 Harmony 补丁。**不要再拆回两个 DLL**。

#### 自动开启（无条件，无配置项）

用户明确要求过"一定是自动打开的，我没说就不要额外加新架构"，所以
`Config.Bind("General","AutoEnableOnStartup",…)` + `cw3hanhua.core.cfg` **已整段删除**，
启动一定自动开，没有逃生口。

**为什么不在 `Awake()` 里直接开**：`Awake` 太早，`Resources.FindObjectsOfTypeAll(typeof(UIFont))`
那时还是空的（UIFont 随后才 Create）→ 扫到 0 个对象、静悄悄什么都不做，状态却显示"已开"。
实际做法是 `Update()` 每帧轮询 `TryAutoEnable()`，就绪门槛三条缺一不可：
① `_font/atlas_zh.png` 存在 ② `_font/atlas_zh.bin` 存在 ③ `UIFont` 至少 1 个。
失败按 **60 帧退避**重试（不刷日志、不放弃），只有 `fontBackups.Count > 0`（真换上了）
才 `autoDone` 结案。

★ **两个记账细节不能省**（都是 v1.0.0 修掉的真 bug）：
`TryAutoEnable` 用 `autoTrying` 区分"自动拨的"和"用户按 F3 拨的" —— 否则没换上 UIFont 时
`hanhuaOn` 已为 true，下一帧 `if (hanhuaOn) autoDone = true` 立刻结案，慢机器一辈子英文。
`ToggleHanhua()` 第一行置 `autoDone = true`（用户按了 F3 后自动开启彻底让位），
`autoTries` 到 **30 次**写一行 `!!` 认输并结案，手动 F3 仍可再试。

**场景切换会补套**：`Update()` 的场景切换分支里 `if (hanhuaOn) ApplyFont();` ——
新场景带进来的新 UIFont 也要换，否则进第二关又变回英文方块。

**装载流程**（详见 4.4）：读 `atlas_zh.png` 建 4096×8192 纹理 →
换 `atlas.spriteMaterial.mainTexture` → 重算全部 `UISprite.UpdateUVs(true)` + `MarkAsChanged()`
→ 读 `atlas_zh.bin` 建 BMFont → 反射填 `mFont`/`mUVRect`/`mDyn`/`mPMA`（**`mAtlas` 不动**）。

#### HUD

**设计口径**：HUD 只干两件事 —— ① 把所有能按的键摆在眼前；② 报告**总开关**的状态。
**内部数字一律不上 HUD**（用户要求："反正也不知道啥意思"）—— 字号表、帧号、备份数、图集尺寸、
场景名、词表条数、各计数、补丁自检全部只进 F11 的 dump。**只有真出错时**才自动多画一行 `!!`。

★ **v1.3.1（2026-10-23）两处变化，都是用户要求的**：
① **HUD 默认关闭**（`showHud = false`）—— 版本收敛了，正常玩不该有常驻面板；F12 仍是双向开关。
② **不再显示「文本层:开/关」** —— 两层由 F3 同生同死，状态永远一样，摆两个开关只会让人以为
   能分开调。`TextPlugin.HudLine()` 因此**整段删除**；文本层的真实状态仍在 F11 dump 的 `开关=`。

正常就 1 行：`按键   F3 汉化总开关:开   F5 未命中清单   F9 重载词表   F11 dump   F12 关HUD`

- **宽度不写死**：`GUIStyle.CalcSize` 量最长一行 + 28px（下限 480，上限 `Screen.width-12`）。
- 两个 `public static string` 工具方法（同程序集直接调用，不是"反射契约"）：
  `ErrorLine()` 空串=正常、`DumpLine()` 一整行全部数字。
- HUD 归字体层 `showHud` / F12 管。**加/删按键时两处都要改**：`HanhuaPlugin.Update()` 的
  `Input.GetKeyDown` + `HanhuaPlugin.OnGUI()` 第一行那个字符串。**F5/F9 例外** ——
  它们在 `TextPlugin.Tick()` 里，因为 `TextPlugin` 没有自己的 `Update()`。

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
**符号区必须有**（中文标点全在 1–9 区）、**二级必须有**（渲/阈/阱 在一级 16–55 区之外）。

**调字号的两个约束**：① 当前 0.80 是靠英文调的，中文观感可能不同；
② 雅黑 Bold 笔画粗、覆盖率高于 Aldrich，粗体要比常规体用更小的 scale。

### 4.2 文本层：静态全量收集与词表

**为什么不靠跑游戏收集**：一次会话只覆盖走过的路径，剧情/任务/教程要走很远才出现，
**全量量化不可能靠回放**。所以静态分析两条路：IL（每条 `ldstr` 后面紧跟的指令若"消费"了它，
说明这条串进了逻辑；dnfile + dncil 扫两个 DLL，覆盖**全部 8289 个有 IL 体的方法**）
+ 资产（`UILabel.mText` / `TextMesh.m_Text` 序列化值 —— 反序列化不经 setter，必须单独收）。

**工具**（都在 `tools/`，Python 用 `.venv`）

| 脚本 | 作用 | 产物 |
|---|---|---|
| collect_ui_strings.py | 静态全量收集 + 分类 + 去重（`--no-assets` 跳过资产扫描，5.5s ↔ 全量 170s） | `_dict/strings.json` |
| test_classify.py | `classify()` 的回归样例（改 `RE_*` 必跑） | 终端 |
| dict_stats.py | 覆盖率报告 | `_dict/STATUS.md` |
| build_dict.py | 编译成插件读的二进制词表（**必须带 `--include-other`**，否则 other 档译文全部被丢） | `_dict/zh_cn.dict` |
| check_charset.py | 译文缺字检查（复用 bake_font 的字符集定义） | 终端 |
| bake_font.py | 烘字形；`gb2312_chars()` / `charset_for(sizes)` 同时供上面两个用 | `_font/` |

**分类三档**（`strings.json` 每条的 `grade`）：`ui`（多词英文、冒号结尾、含数据的格式串、
以空格结尾的拼接前缀）确定要翻；`other`（单词、代码片段、疑似调试）拿不准后翻；
`noise`（标识符 / PascalCase / 资源名 / 标准库异常 / 节点路径 / 正则 / 纯数字）丢弃。

**当前量化结果**（权威数字只看 `_dict/STATUS.md`，由 `tools/dict_stats.py` 生成）

| 项 | 数量 |
|---|---|
| IL 字符串引用 | 11001（Assembly-CSharp）+ 238（firstpass） |
| 资产文本 | 467 条（UILabel 455 + TextMesh 12；另有 19 条已被 bake 成中文，按 CJK 跳过） |
| 候选合计 | `strings.json` **3317** 条 = ui 1352 + other 1936 + none 29；来源 both 292（译 221）/ asset 1025（译 1008）/ dll 2000（译 895） |
| 已译 | **2127**（ui 969 + other 1158，64.1%）→ `zh_cn.dict` **2127 条**（211 741 字节） |

> `ui` 不是"一定会显示"，是"几乎一定"；`other` 里约 900 条是单词需人工判定。
> **不要拿 ui 条数当工作量上限。** 给出新数字前先跑 `tools/dict_stats.py`。

**★★ `--merge` 踩过的两个坑（务必看）**

1. **资产 bake 过之后再跑收集，会把中文当源串收进来** —— `scan_assets()` 读到的是中文，
   `'载入文件'`、`'制作组'` 这类译文被当成"新发现的源串"写进 `strings.json`（实测混进 22 条）。
   修法：`scan_assets()` 用 `RE_CJK` 跳过任何含中日韩字符的文本（游戏原文全是英文）。
2. **只按"新扫到的条目"合并会静默丢译文** —— 那 19 条英文源串一旦扫不到，译文就被丢弃
   （实测丢 10 条 `zh_cn.dict`），而界面上那些字已经是中文、**完全看不出异常**。
   修法：`save(merge=True)` 除沿用同名条目译文，还要把**"旧文件里有译文、这一轮却没扫到"的
   条目整条搬回来**，并打印补回了哪些。

**词表骨架**（`_dict/`）

```
_dict/
├─ strings.json     ★ 唯一词表。每条 {en, zh, grade, cat, kind, cls, refs, note}
│                   翻一条 = 把 zh 从 "" 填成中文。--merge 重跑不会丢译文（含上面第 2 条的搬回逻辑）。
├─ glossary.md      术语表。改译名只能在这里改，译文里不许随手换。
├─ zh_cn.dict       由 build_dict.py 生成，插件直接读
├─ STATUS.md        由 dict_stats.py 生成，勿手改
├─ batches/         25 份批次源（both_ui + full_01…full_25_crpl；编号有跳，full_23 已撤）
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
& $p _cw3hanhua\tools\add_strings.py _dict\batches\<新条目批次>.json   # 仅限**新条目**（原表里没有的 en）
& $p _cw3hanhua\tools\del_strings.py _dict\batches\<要删的批次>.json   # 删条目（误加/方案变更时）
& $p _cw3hanhua\tools\check_charset.py _dict\strings.json   # 缺字就补烘
& $p _cw3hanhua\tools\build_dict.py --include-other  # 编出 zh_cn.dict
& $p _cw3hanhua\tools\verify_lookup.py               # 离线复刻插件查表链，验证命中/幂等
# 改完词表游戏内按 F9 重载；改了插件源码则要重编译 + 重启游戏
```

`verify_lookup.py` 四种用法（改词表后至少跑前两种）：无参数跑内置 21 个目标场景 + 10 项安全抽查；
`--idempotent` 把词表译文再喂回查表链、**必须 0 条被改写**；`--regress` 对比 `strings.json`
全量新旧行为差异；`--missing` 列仍查不到的条目。

**译文纪律**

- 只改 `batches/*.json`，**不要手改 `strings.json`**（`apply_translations.py` 用原文精确匹配）。
  **例外**：运行时拼接出来的整串（4.7③ 那类）原表里不存在，用 `tools\add_strings.py` 新增。
- 译文里的 `[颜色]` 标记、`\n`、首尾空格**必须保留**。像 `PACKET SPEED.  `（两个尾空格）
  是拼接前缀，尾空格删掉就会和后面的数字粘在一起。
- 组合键（`SHIFT-E` / `SHIFT-A` / `CTRL-C`）整串保留英文 —— 玩家要按着它去找键。
- 改术语先改 `glossary.md`，再批量改批次。
- 译稿用**序号字典** `{"zh": {"3": "译文"}}`，缺省项视为 `@`（跳过不译）；
  **不要用数组形式**（漏一项就整体错位，踩过一次：89 vs 88，后 60 条全串行）。

### 4.3 文本层（`CW3HanhuaText.cs` → 编进 `CW3Hanhua.dll` v1.4.0）

**查表链（四级，v1.1.0 新增第四级）**：

| 级 | 函数 | 覆盖什么 | 什么时候落空 |
|---|---|---|---|
| 1 | `dict[s]` 精确 | 整串在词表里 | 运行时拼出来的串 |
| 2 | `LookupConcat` | 在 `'\n'` 处切两段、两段都精确命中 | 没有换行的拼接；尾段不在词表 |
| 3 | `LookupNormalized` | CRLF / 连续空格 / 尾部空白差异 | 真的不是同一串 |
| 4 | **`LookupSegment`** | **碎片常量 + 运行时值**（色标透传、变量透传） | 有字母消费不掉（全有或全无） |

前三级一字未动，第四级纯兜底。`LookupSegment` 的规则表与踩坑见 **4.7③**。
`HasCjk` 闸门在第四级之前：**串里已有汉字就直接不翻**，防止 TextMesh 那条路
把写进去的中文再喂回来二次改写。

**生命周期由字体层统一驱动**（`TextPlugin` 没有自己的 `Awake`/`Update`/`OnGUI`）：

| 时机 | 谁调 | 做什么 |
|---|---|---|
| `HanhuaPlugin.Awake()` | 字体层 | `TextPlugin.Init()` —— 读词表、装 Harmony 补丁、打 TextMesh/CRPL 补丁 |
| `HanhuaPlugin.Update()` 每帧 | 字体层 | `TextPlugin.Tick()` —— F5 未命中清单、F9 重载词表 |
| 场景切换（`Update` 检测到） | 字体层 | `TextPlugin.OnSceneChanged()` —— 清已销毁 label / TextMesh 缓存、清 `missSeen` |
| F3 / 自动开启 | 字体层 | `TextPlugin.SetOn(bool)` —— 接词表 / 还原 |
| `OnGUI` / `DumpReport` | 字体层 | `TextPlugin.ErrorLine()` / `.DumpLine()`（`HudLine()` 已删除，见 4.1） |

**为什么 `Init()` 要早**：prefix 必须装在场景创建之前才能拦到首批文本；
`HanhuaPlugin.Awake()` 里调是来得及的（BepInEx 的 `Awake` 早于任何游戏场景对象）。
**日志也合并成一份**：`TextPlugin.Say` 转发到 `HanhuaPlugin.Say`，两层每行都同时进控制台和
F11 的 dump，**一个文件看全两层**。装载顺序问题随之消失（同一个 DLL = 同一个 `Awake`）。

**替换路径（就这四条，别处没有）**

| 路径 | 覆盖什么 | 实现 |
|---|---|---|
| `UILabel.ProcessText` **prefix + postfix/finalizer** | **全部 1029 个 UILabel**：运行时动态文本 + 资产序列化的 `mText` | 见 3.3 |
| `PreludeManager/EpilogueManager/DemoScreenManager.ShowText` **prefix 改 `__0`** + `StarManager.set_starName` / `PlanetManager.set_planetName` **postfix 只改 TextMesh** | TextMesh 五个写入点：三段过场台词（31+21+10 处调用）、星区名、星球名 | 见 4.6a，**name setter 不能改入参** |
| `CrplCore.ProcessCommands` **postfix 改 `GameSpace.message`** | **全部 CRPL 教学提示**（`ShowMessage` / `ShowMessageDismissible` 的唯一出口） | 见 4.7④ |
| **`GUILayout.Label/Box/Button/RepeatButton/Toggle` 的文字重载 prefix 改 `__0`/`__1`** | **全部 IMGUI 界面**：结算/在线得分、地图编辑器（237 处）、殖民空间站（88 处）、世界机器（74 处）、统计成就等 —— 689 处 `GUILayout.*` 调用 | 见 4.3a（v1.4.0） |

**代价（可接受）**：`label.text` 与屏上显示不一致（屏上中文、`.text` 英文）。
`mSize` 是按中文算的 —— 这正是我们要的，字宽/换行/`mShrinkToFit` 全对。
若要 `get_text()` 也返回中文，就得改 `mText` 并自己兜住 3.3 那 6 处判定，
**当前故意不做**。

**关总开关时的还原**：`mText` 一直是英文（中文只在 `ProcessText` 内部临时存在），所以不需要
写回英文，只要逼它重算一次 `mProcessedText`。**踩过的坑**：写 `s.label.text = s.en` 是
**无效的** —— `set_text` 里 `if (mText != arg)` 不成立就什么都不做，`mProcessedText` 还是中文。
正确做法是 `ForceRefresh(label)` → 优先 `UILabel.MarkAsChanged()`（public，
IL = `set_hasChanged(true)` + `UIWidget.MarkAsChanged()`），失败才反射调私有的 `set_hasChanged(true)`。

★ **本层的边界**：只能还原**本层经手**的文本。改资产烘进 prefab 的中文不归本层管、
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
对象跨场景存活，记录被清掉后 `RestoreAll()` 对它是空操作 → 关总开关时留下中英混杂。

**排查工具**

- `missSeen` + **F5**：`ProcessText` 查不到译文时记一条，F5 打印清单。
  列出来的说明"走了 UILabel 但没译文"（补词表 + F9 重载即可）；
  界面上仍英文、清单里又没有的，才是真正**必须改资产**的对象（TextMesh 那批）。
- **F11 dump 里那一行**（v1.0.0 起直接调 `TextPlugin.DumpLine()`）：
  `文本层 v1.4.0  开关=…  词表=…  prefix=调用/命中  拼接=…  分段=…  mesh=调用/命中  crpl=调用/命中  imgui=调用/命中(已装重载数)
  未命中=…  CRPL字体=…`。
  `prefix` 计数不动 = 补丁没装上；`mesh` 计数不动 = TextMesh 那五个补丁没装上或那段文本没被写过；
  **`分段` 计数不动 = 一条运行时拼接串都没走到第四级**（说明前三级全接住了，或者补丁没装）；
  **`crpl` 调用>0 而命中 0 = CRPL 文案词表未命中**（F5 会列出来），调用恒 0 = 补丁没装上。
  ★ **HUD 上只剩总开关一个状态**（v1.3.1 起不再显示「文本层:开/关」，见 4.1「HUD」）——数字要看 dump 文件。

**★ 装补丁必须自检**：`Harmony.Patch` 失败时**只往日志写一行 Error、不抛异常**
（外层 try/catch 什么也接不到，表现为"什么都没发生"）。所以装完立刻回读
`Harmony.GetPatchInfo(m)`，核对 `Prefixes/Postfixes/Finalizers` 各 1、`ILManipulators` 为 0，
不合格就打印警告。**这是 v0.9.0 首次实测"完全没中文"的直接教训。**

#### 4.3a 第四层：IMGUI（`GUILayout.*`）—— v1.4.0

**为什么需要**：前三条路径全部只覆盖 **NGUI**（UILabel / TextMesh）。游戏里还有一大片界面是用
**Unity 原生 IMGUI**（`OnGUI` + `GUILayout.*`）画的，那些字根本不经过 NGUI，前三条一条也拦不到。
最典型的就是**通关结算面板**（`ResultsManager.DoScoreWindow`：`GUILayout.Label("Mission Results")`）。

**为什么收了 `GUILayout` 而不是 `GUI`**：两者最终都收口到 `GUILayout.DoLabel`
→ `GUILayoutUtility.GetRect(content, style, options)` → `GUI.DoLabel(rect, content, style)`。
收口点选择（`ilx dump` + dnfile 读 flags 实证）：

| 候选 | 可见性 / IL 体 | 能不能打 |
|---|---|---|
| `GUILayout.DoLabel` / `DoBox` / `DoButton` | **private**，有 IL | ✗ Harmony 只打 public/internal |
| `GUI.Label`(6) / `GUI.Box`(5) / `GUI.Button`(6) | public，**abstract**（reference stub，RVA 指向 extern，无 IL 体） | ✗ |
| **`GUILayout.Label/Box/Button/RepeatButton/Toggle`（各 6 个重载）** | **public，有真实 IL 体** | ✓ **就是它** |

**为什么必须在 `GUILayout` 这一层改、不能改 `GUI` 那一层**：`GetRect` 就在 `GUILayout.Label`
体内，用它**已经改过的入参**去量宽 —— 中文宽度天然算对，`GUILayout.Width(120f)` 这类定宽
选项也照原样生效。若在 `GUI.Label` 里改（v1.2.0 的老做法），矩形早已按英文算完，
只能去「猜这个盒子是不是自动量的」（`|r.width - enSize.x| <= 28f`），结果误伤
`SGT_CameraFPS` / `SGT_CameraMessage` / `KeyEntryGUI` 的定宽盒子 → v1.3.0 整段删除。
**这一版不再猜盒子。**

**装法**：对每个 `(名字, 文字参数下标)` 组合，反射遍历 `typeof(GUILayout).GetMethods(...)`，
跳过 `IsAbstract` / 无方法体 / 文字参数不是 `string|GUIContent`（`Texture` 重载不管），
用 `PatchProcessor.AddPrefix` 装 4 个 prefix 之一：

| prefix | 形态 |
|---|---|
| `PrefixGuiStr0(ref string __0)` | `Label(string,…)` / `Box` / `Button` / `RepeatButton` |
| `PrefixGuiStr1(ref string __1)` | `Toggle(bool, string, …)` |
| `PrefixGuiCon0(ref GUIContent __0)` | `Label(GUIContent,…)` 等 |
| `PrefixGuiCon1(ref GUIContent __1)` | `Toggle(bool, GUIContent, …)` |

★ **不用 `object[] __args` 改参数**：那依赖「改完会回写」，不同 Harmony 版本行为不一。
`ref string __N` 是确定生效的写法。

★ **GUIContent 必须换引用，绝不能改 `.text`**：`GUILayout` 内部用的是
`GUIContent.Temp(String)`，它返回**静态共享单例 `s_Text`**。写 `c.text = zh` 等于改全进程
下一个 `GUIContent`（v1.2.0 踩过：改完所有 Label 一起串味）。正确写法
`c = new GUIContent(zh, c.image, c.tooltip);`。

★ **不碰 `GUILayout.TextField`**：它的**返回值是玩家输入的文本**，翻了会毁数据
（玩家名、存档名、CRPL 代码都从这里出去）。同理不碰 `Toolbar` / `SelectionGrid` 的返回值。

**不设字体**：HUD 本身就是 IMGUI 画的中文、一直正常 —— Unity 5 内置动态字体对缺字有
OS 回退。保持原字体才能让拉丁字母仍用 Aldrich（结算面板风格一致）。
`Font.CreateDynamicFontFromOSFont` 那条路只留给 `GameSpace.skin` 的 `"message"` 样式。

**覆盖面实测（反编译统计 `GUILayout.(Label|Button|TextField|Box)` + `GUI.*` 共 689 处）**：
`EditMapGUI` 237、`ColonialSpaceManager` 88、`WorldMachineManager` 74、`EditSystemGUI` 43、
`MySectorGUI` 40、`ResultsManager` 30、`ResultsGUI` 21、`ScoreViewerManager` 20、
`StatsAndAchievements` 13、`TexturePickerManager` 11、`MessageDialogManager` 7、
`ResizeMapManager` 7、`KeyEntryGUI` 7、`MainMenu` 3、`GUIUtils` 2（窗口标题也走
`GUILayout.Label`，一并覆盖）。

**装了几个**：5 类 × 每类 4 个文字重载 = **期望 `imguiPatched = 20`**
（每类的 6 个重载里各有 2 个 `Texture` 重载，跳过）。少于 20 说明有重载没装上，日志里会有
`!! GUILayout.X/参数数 没装上（prefix=0）`。

**排查**：F5 清单末尾多一行 `IMGUI 调用 N  命中 M  已装重载 K 个（v1.4.0）`。
`K=0` = 一个都没装上（看日志里的 `!! 装 GUILayout.* 补丁失败`）；
`N` 不动 = 补丁没生效或那段代码没跑；`N>0 而 M=0` = 词表未命中（F5 会列出具体串）。
#### 已废弃：sweep 路线（不要再实现）

`Sweep` / `RebuildCache` / `ReplaceLabels` / `ReplaceMeshes` / `LateUpdate` / `IsSwapped` /
`labelCache` / `meshCache` / `CacheRebuildFrames` 与全部命中计数已删除。
UILabel 周期 sweep 被 `ProcessText` 补丁一并覆盖（零闪烁）；TextMesh 周期 sweep / 每帧快扫
用户实测两次都不满意 —— 理论上限就是「总有一帧是英文」，靠事后扫描必然先上屏一次英文。
**结论：TextMesh 只能改资产。** `missSeen` 保留，但只作排查工具（见上）。

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
产物：_font/atlas_zh.png（6.0MB 文件 / 128MB 显存；2026-10-21 修 alpha 平方后从 9.9MB 降到 6.0MB）
      _font/atlas_zh.bin（707KB，插件直读；格式 CWA1 + W/H/nSizes + 每字号字形表 + FNV）
      ↑ .bin 只含几何度量，修 alpha 后**逐字节不变**（SHA256 相同），旧 .bin 可继续用
```

#### ⚠ 坐标约定：精灵和字形都是【顶左原点】

`NGUIMath.ConvertToTexCoords`（`yMin = 1 - rect.yMax/h`、`yMax = 1 - rect.yMin/h`）与
`UIFont.Print`（`u0.y = mUVRect.yMax - num9*glyph.y`、`u1.y = u0.y - num9*glyph.height`）
**两边同向**：`sprite.outer` 和 `glyph.y` 都从**图像顶部**往下量，`mUVRect=(0,0,1,1)` 时
`yMax=1` 即顶部。**像素级实证**：已知精灵 `'NGUI' outer=(0,646,255,75)`，按从顶量裁
第 646..720 行有 19087 个非透明像素（目视就是 NGUI logo），从底量只有 1523 个零碎像素。
原图必须放顶部 —— `build_atlas.py` 顶部有断言，中文字形 `y_top < 1024` 直接报错退出。

#### ⚠ 灰度图必须转成 alpha

`font_XX.png` 是 PIL **`L` 模式**（白字黑底）。直接 `convert("RGBA")` 会把黑底变成
**alpha=255 的不透明黑块**（实测成品像素 `(0,0,0,255)` 满屏）。

```python
g = Image.open(glyph_png).convert("L")
gimg = Image.new("RGBA", g.size, (255, 255, 255, 0))
gimg.putalpha(g)          # RGB 全 255，alpha 承载覆盖率
```

理由：draw call 的顶点色（`label.color` / 富文本标记）会乘上纹理 RGB，所以 RGB 必须全白。

#### ✗ 已修的 bug：贴字形时不能拿 tile 自己当 mask（alpha 被平方）

**症状**（用户报「中文似乎有点奇怪的模糊」）：笔画发虚、毛边、细笔画断断续续，**不是**运行时
缩放/重采样造成的。修法就一个字符：`canvas.paste(tile, (dx, dy), tile)` ✗ → `canvas.paste(tile, (dx, dy))` ✓。

`tile` 的 RGB 纯白、alpha 是灰度。拿自己当 mask 往全透明画布上合成 → alpha 变 `a²`、
RGB 变 `255·a`，而 draw call 仍按 `rgb·alpha` 混合 → 屏幕覆盖率 = `a³`（50% 的边缘像素只剩
12.5%）。整字集墨量 464526 → **385399（83%）**，损失全在浅灰像素上。修后平均 alpha 176.8
（原 157.4）、RGB 全 255。**确认方式**：截图逐字对齐图集，10 个字最佳偏移全在整数 (0,0)、
相关系数 0.999~1.000；亮度三模型回归（n=1508）白字×alpha 的 RMS 36.9、**灰度RGB×alpha² 1.5** ✅、
白字×alpha² 17.9。原版半透明像素 RGB 多为 (0,0,0)/(255,255,255)，说明原版也是「RGB 不预乘」
（修复提交 `cd60b9a`）。

**顺带确认（这一层不是 bug）**：`--scale 0.80` 让实际栅格化字号是 **13/16/22 px**（charSize 仍记
16/20/28）。`font_16.png` 与「直接用 PIL 13px 渲染、无任何重采样」**10/10 逐像素一致** → 烘焙本身
没有额外损失，13px 的雅黑 Bold 就是那个观感下限。要更大只能改 `--scale`，但 `adv` 会跟着变大，
得逐处核对译文长度会不会撑破按钮。

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

---

### 4.4a 资产只读不写（「不改游戏文件」指什么）

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
**「改资产让图集可读」这条方案从未执行，也不需要** —— `m_IsReadable = False` 只约束
**Unity 运行时 API**（`GetPixels32` 报 `Texture 'GUI Atlas' is not readable`）；离线用 Python
直接取 `image data` 字段是读**文件里的数据**，绕开该限制；运行时只要把材质主纹理指过去即可。

### 4.5 全量翻译

**当前数字**（`_dict/STATUS.md`，`tools/dict_stats.py` 生成）：`strings.json` **3317 条 / 已译 2127**；
插件读的 `zh_cn.dict` = **2127 条**（211 741 字节，`--include-other` 编译、FNV 校验通过）；
`check_charset.py` 报 **1547 字符（汉字 1448）✓ 全部在字集内** —— **不用重烘图集**。
未译的 1209 条按设计不译（见下方桶表）。给新批次加条目后这三条要一起刷新。

> 沿革（只看结论）：翻译是分四批进来的 —— 4.2 的 1144 条 → 4.8 剧情 +852 → 4.7③ 分段碎片 +22
> （并删掉 41 条护盾枚举）→ 4.7④ CRPL 教学提示 +60。`kind` 维度与「故意不译」清单见下表。

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

**未译 1211 条的桶**（下表是 2026-10-20 统计的 1260 条分布，差额来自后续批次补译；
口径为 `strings.json` 自算，可复现）

| 桶 | 条数 | 为什么不译 |
|---|---:|---|
| 第三方库/框架日志（VectorLine / CrplCore / AudioToolkit / NGUI / SGT_* / iTween / SevenZip） | 471 | 只进控制台 |
| 标识符 / 资源名（`PRODUCTIONINTERVAL`/`ACBackground`/`1XButton`） | 237 | 改了就断 |
| 程序化星球名音节碎片（`Aber`/`Abra`/`Ale`…） | 146 | 保留原文风味 |
| 其它（按键名、占位文案、`', a = '`、`{0:D}:{1:D2}` 格式串） | 133 | — |
| 自定义图资源名（`Custom0`/`Custom0_256`） | 118 | 资产 key |
| Steam 成就日志（`m_flGameDurationSeconds: ` 等） | 61 | — |
| 文件名（`*.dat`/`*.xml`/`maps/`）+ 调试日志（`ops` 带 `call Log`） | 53 | — |
| 资源条模板串 17（已由 4.7③ 覆盖，只剩 `AC:`）+ 1–2 字符符号 13 + 哈希 11 | 41 | — |

**批次清单**：`_dict/batches/` 共 25 份（`both_ui` + `full_01`…`full_25_crpl`；`full_03`/`full_16`
全跳 —— 音频库与 VectorLine 日志）。**只改批次，不要手改 `strings.json`**（见「译文纪律」）。

**几条译法备忘**（术语表之外新定的）：
- `Return` → `返回`（按钮，不是回车键）
- DMD 名册表头：`Owner`→`作者`、`Map`→`地图`、`Started`→`创建`、`Played`→`游玩`、
  `Plays`→`次数`、`Filter`→`筛选`、`Score`→`得分`、`Player`→`玩家`
- `[00e000]FLOWING`→`[00e000]流动中`，与 `[ff0000]PAUSED`→`[ff0000]已暂停` 对齐
- `Targets: `/`Price: ` 这类**拼接前缀**照样译，靠 `check_batch.py` 保住尾部空格
- `Credits` → **制作组**（"制作名单"实测宽 4.00em = 英文 3.82em 的 105%，会溢出）

**字宽实测**（决定要不要为排版改写译文）：**每个汉字 = 1.000 em**（与字号无关），
Aldrich 空格 = 0.300 em。18 条主菜单译文平均只占英文宽的 **68%**，唯一超标的是
`Credits`（已改）。→ **不需要任何"插空格/缩字号"的特殊手段**，直接替换即可。
（另：`zh_cn.dict` 是 TextMesh 与 UILabel 共用的，往词表里塞空格会污染 UILabel 路径。）

### 4.6 改资产让 TextMesh 显示中文（已执行）

**目标**：把中文写进 prefab 的 `m_Text`，对象一出现就是中文，零闪烁。
`TextMesh.set_text` 是 extern icall（RVA=0），Harmony 打不上，所以这批只能改资产。

**盘点结果**（`_cw3hanhua/tools/scan_textmesh.py`，只读，全部 19 个资产文件）：TextMesh 对象 30、
有文本 27、唯一文本 21 条（词表原有 18，缺 3：`4`×5、`Andro`×2、`DMD`×1）。
分布：`level1` 14 · `resources.assets` 12（9 有文本）· `level2`/`level5`/`mainData`/
`sharedassets6.assets` 各 1 · 其余 12 个文件全 0。

| 类 | 数量 | 说明 |
|---|---|---|
| ① 纯静态可改 | 20 | 主菜单大按钮、标题、序章旁白 —— 直接改 |
| ② prefab 占位值 | 5 | `TextLabel` 的 `4`（`m_Alignment=0` `m_CharacterSize=2.0`） |
| ③ 运行时覆盖 | 5 | `StarTitle`/`Wormhole/StarTitle` 的 `Andro`、3 个空模板 `StarInfo`/`PlanetTitle`/`CRPLTextMesh` —— **改资产无效**，留给代码路线（4.6a） |

**排版参数实测**：23 个有文本的 TextMesh **无一例外 `m_Anchor=4`(中中) + `m_Alignment=1`(居中)**
（`TextLabel` 那 5 个例外：左对齐、`m_CharacterSize=2.0`）。字号 15–96 不等。
**字体引用**：level/mainData/sharedassets6 的 TextMesh 全指 `Aldrich`
（`{m_FileID:1, m_PathID:755}`），`resources.assets` 的指 unity 内置 Arial
（`{m_FileID:5, m_PathID:10102}`）—— **两条都无 CJK 字形，但都不影响**（见 3.2 末）。

**★ 已落地：`tools/bake_textmesh_assets.py`（2026-10-20 执行通过）**

```powershell
# 备份（幂等，永不覆盖已有备份）→ _cw3hanhua\_assets_orig\（71.5 MB，已 gitignore）
$env:PYTHONPATH = (Resolve-Path "_cw3hanhua\.venv\Lib\site-packages").Path
& "C:\Python314\python.exe" _cw3hanhua\tools\bake_textmesh_assets.py --backup
& "C:\Python314\python.exe" _cw3hanhua\tools\bake_textmesh_assets.py --dry-run   # 看会改什么
& "C:\Python314\python.exe" _cw3hanhua\tools\bake_textmesh_assets.py             # 恢复+烘焙
& "C:\Python314\python.exe" _cw3hanhua\tools\bake_textmesh_assets.py --restore   # 全部还原
```

**先把备份复制回游戏目录（清除旧修改）→ 再从备份副本重新烘焙 → 写回**，所以反复运行幂等
（实测第二次跑打印「已是原始状态，无需恢复」）。只改 ① 类 20 条中的 19 条（`DMD` 词表无译文，
跳过）；②③ 类不动。自带回读校验（写完重新解析资产、逐条比对中文），实测 **19/19 通过**。
（UnityPy 重打包会重排字符串表，六个文件体积有小幅变化，属正常。）

**★ 沙箱坑：`.venv\Scripts\python.exe` 写不进游戏资产**

现象 `PermissionError: [Errno 13] Permission denied: 'CW3_Data\level1'`，而同一 shell 里 `pwsh` 和
`C:\Python314\python.exe` 都能写。根因：DSH 沙箱让「**从工作区目录内启动的可执行文件**」以
**Low 完整性级别**运行，游戏资产是 Medium —— Windows 的 no-write-up 规则禁止低完整性进程覆盖
高完整性文件；新建文件和写 `_cw3hanhua` 下自己的文件都不受影响，所以只有覆盖资产这一步失败，
极易误判成"文件被占用"。**解法：系统 python + venv 的 site-packages**（`$env:PYTHONPATH` 指到
venv 的 `Lib\site-packages`，由沙箱外的 `C:\Python314\python.exe` 启动，拿 Medium 完整性）。
脚本里 `precheck_writable()` 会直接把这条建议印出来。**只读脚本用 venv 跑没问题。**

**★ `--restore` 曾静默失效（已修）**：资产已是中文后跑 `--restore` 什么都不做、退出码 0。
根因：`--restore` 排在 `plan = scan(d)` 之后，而 `scan()` 按「英文原文查词表」生成计划 ——
资产已是中文 → `plan` 为空 → 提前 `return 0`。修法：`--restore` 提到最前、完全不依赖 `scan()`；
顺带护栏：`--backup` 要求 `plan` 非空，否则会把**中文状态当原件**冻存进 `_assets_orig/`。
修后实测 `--restore` 六文件 sha256 与 `manifest.json` 逐字节一致。

**回归自检**（`tools/diff_bake.py`，只读）：以 `tools/uilabel_inventory.json` 为基线重扫现役资产 →
实测 **1029 条 UILabel 文本变了 0 / 丢了 0 / 增了 0**，六文件对象数也没变。

> ★ 两个对比陷阱，都踩过：
> ① **不能直接读 `_assets_orig/` 里的备份做 diff** —— 备份离开 `CW3_Data/` 后，MonoBehaviour 的
> MonoScript 要跨文件引 `sharedassets0.assets`，UnityPy 报 `FileNotFoundError`，备份侧
> **0/1827** 可读（现役 1807/1827）。**备份只能整体拷回，不能就地解析。**
> ② 基线只记**非空**文本（1029 条），全场有 1206 个 UILabel —— 多出的 177 个 `mText` 是空串。
> 对比前必须滤空，否则会误报「新增 177 条、有 UILabel 被波及」。

**★★ 最大的坑：UnityPy 会把 `script_type_index` 抹平 → 主菜单按钮点不动（已修）**

现象：烘焙后**主菜单所有按钮点不动**，不崩溃不报错，日志一堆 `MainMenu` 的 NRE；`--restore` 立刻恢复。

根因：UnityPy 1.25.4 `files/ObjectReader.py:109-112` 把 `script_type_index` 写进**整张类型表共享的**
`serialized_type`（每读一个对象覆盖一次），`write()`（`:175`）时所有对象都写回最后读到的值 →
每个对象那 2 字节被抹成同一个数 → Unity 靠它找脚本类，组件绑错 → 「C# 壳还在、native ptr=0」的
死组件。`MainMenu` 6 个 sprite 字段里 3 个（`creditsSprite`/`exitGameSprite`/`loadFileSprite`）
`Awake` 就死 → `Update` 的悬停比较链 `callvirt Component.get_gameObject()` 抛 NRE → Update 中途
返回，**永远走不到 `Input.GetMouseButtonDown`**。

**弯路教训**：`get_raw_data()` 逐字节相同，一度判定「烘焙无辜」，静态查了对象表/type_id/
externals/类型表/对齐/区间重叠/重名十几项全清白。**「对象数据未变」≠「文件可用」，坏的可能是
metadata 里那几字节。** 决定性实验是**纯往返**（`load()`→`save()` 一个文本都不改）：`level1`
出现 **316 字节差异，全部落在 `data_offset`=148048 之前的 metadata 区**，每处只差 1 字节，
间距 84/140/112/56/196 正好是记录步长。

**修法**（固化为 `install_unitypy_fix()`）：monkey-patch `ObjectReader.from_reader`，读完从
`reader.Position` 回退 2 字节（version 15/16 多退 1 字节的 `is_stripped`）记下**每个对象自己的**索引；
再 patch `ObjectReader.write`，写前设回该对象的值。★ 键必须是 `(id(assets_file), path_id)`
—— **不同资产文件的 path_id 会重号**。**验证**：纯往返差异 **316 → 0**、文件逐字节相同。

> 一个易误判的假象：这 6 个组件 `read_typetree()` 的 `m_Script` 是垃圾值
> `{'m_FileID': 16777216, 'm_PathID': 1895825408}` —— **原件里也是这个值**（level1 有 20 个
> MonoBehaviour 因缺 `Assembly-UnityScript.dll` 解析不出），不是烘焙造成的。

**③ 类留给代码路线（已实现 → 4.6a）**
`TextMesh.set_text` 全游戏只有 **8 个宿主方法 / 15 个调用点**（`ilx callers --type TextMesh set_text`）：
`TextLabel.set_text`×5（prefab 驱动，命中 0 处）、`PlanetManager.set_planetName`×2、
`StarManager.set_starName`×2、`StarManager.Refresh`×2、`CRPLTowerManager.set_text`×1、
`DemoScreenManager.ShowText`(params=9)、`EpilogueManager.ShowText`(params=8)、
`PreludeManager.ShowText`(params=7)。补丁只能打在这 8 个宿主方法上。

**★ 读 MonoBehaviour typetree 的必要条件**（两个扫描脚本都踩过）：
必须 `TypeTreeGenerator("5.2.3f1")` + `load_local_dll_folder(CW3_Data/Managed)` 并赋给
`env.typetree_generator`，否则 `read_typetree()` 对 86% 的 MonoBehaviour 抛异常
（`level1` 实测：无 generator 时 258/1827 成功，有 generator 时 1807/1827）。
**没有 generator 时读不到会表现为「扫出 0 个」，很容易误判成"资产里没有"。**
另：`resources.assets` 解 typetree 极慢（**138 秒**），全量扫描要给足超时。

### 4.6a TextMesh 的代码补丁（五个写入点）

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
  与 UILabel 的 `swaps` 分开；关总开关时 `mesh.text = en` 即可。
- F11 dump 那行带 `mesh=调用/命中`，计数不动就说明补丁没装上或那段文本没被写过。
  （v0.10.2 起 HUD 上不再显示计数，见 4.1「HUD」。）

### 4.7 待办与已知缺口

#### ① 资源条模板串 —— 已由 ③ 的分段重组覆盖

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
**模板既不在头也不在尾** → 精确匹配和前缀匹配都无效；`LookupConcat` 只切 `\n`，也兜不住。
**这个问题现在由 ③ 的 `LookupSegment` 解决** —— 模板串就是词表里的碎片，分段器原地换掉，
两端的 unitName 与数字原样透传。

**这批模板条目的译文已补齐**（批次 `_dict/batches/full_24_fragments.json`，12 条）：
`  [ffffff]Upgrading to Lvl: `、`Building:`、`Building...`、`Health:`、`Cargo:`、`Ore:`、
`Armed:`、`Pad:`（两种颜色）、`Ammo:`（两种颜色）、`Energy:`（两种颜色）、`Aether:`、
`Fully Upgraded!`。**故意不译** `AC:`（术语表规定 AC 保留英文）。

**仍未覆盖的**：`Interval: 3 sec` / `Spawn Interval: 3 sec` 这类**尾缀**常量
（`' sec'` 不在词表），以及 `MELEE`/`SPORE` 等被拼进名字的词。要覆盖得先补
`' sec'`→`' 秒'` 这类尾缀条目；目前 `sec` 保持英文，属可接受残留。

#### ② 其它待确认

- **`Andro` / `DMD` / `'4'` 三个是否翻译未定**：`Andro`（`resources.assets` pid=1640/1644，
  `Wormhole/StarTitle` 与 `Star/StarTitle`）与 `DMD`（`level1` pid=3342，`Star/SpriteLabel`）
  词表里都没有译文；`'4'` 是 `TextLabel` prefab 的五层占位值（`ilx callers` 命中 0 处，无处施力）。
- 打包/分发方案未定（BepInEx + 单个 `CW3Hanhua.dll` + `_font/` + `_dict/` 的目录形态）。

#### ③ 「Planet is shielded...」不翻 —— 已修：查表加第四级「分段重组」

**现象**：用户 F5 报未命中 6 条，其中最长的 `len=62`：
`Planet is shielded. Keys needed from planet(s):\n[ff7f7f]Tempus`。

**为什么词表里有前缀还是不翻**（两重错位，缺一不可）：

1. 该串在 `SystemManager.ShowStorySystemMessageForPlanet` 里**运行时拼接**
   （`Assembly-CSharp.decompiled.cs:66952` 起）：`string text = "Planet is shielded.
   Keys needed from planet(s):\n";` 然后各分支 `ShowMessage(text + "[ff7f7f]Tempus")`，
   IL 里**只有前缀这一个 ldstr**，整串不存在于任何资产。
2. 词表存的正是那条前缀（47 字符，**带尾随 `\n`**），tail 是 `[ff7f7f]Tempus` ——
   而词表里 7 条 `[ff7f7f]名字` 的 `zh` 全空（静态收集器把 ldstr 色标与名字碰巧拼一起，
   不是可用条目）。精确匹配中不了整串，`LookupConcat` 只切 `\n` 且要求两段都命中，也中不了。

**第一版修法（已撤销）**：把分支能拼出的整串全部枚举进词表（41 条，含 Tiplex/Chanson/
Farbor 三组 2ⁿ 颜色组合）。用户当即质疑「再发现新的不就没完没了」—— 可扩展性不可接受，
而那 41 条本来就能被分段器自动拼出来。

**最终修法：`Lookup` 加第四级 `LookupSegment`（插件 v1.1.0）**

```
精确匹配 → LookupConcat('\n' 拼接) → LookupNormalized(归一化) → LookupSegment(分段重组)
```

前三级一个字没动，分段只是末端兜底。逐字符扫描，规则见
`plugin/CW3HanhuaText.cs` 的 `LookupSegment` 注释，要点：

| 规则 | 为什么 |
|---|---|
| `[rrggbb]` / `[-]` / `[tag]` 整段原样透传 | 颜色不是语言，不该参与匹配；也省掉 2ⁿ 枚举 |
| 非字母原样透传 | 数字、标点、换行、路径分隔符都要保住 |
| 遇字母/空格/`[` 在 `byFirst` 里试**最长前缀** key | 一次扫描把所有碎片换掉 |
| **左边界**：起头前一字符是字母**且本 key 也以字母起头**才放弃 | 否则 `Windows` 被 `Window` 吃成「窗口s」。**v1.3.2 起只在 key 首字符是字母时判** —— 见下方坑 5 |
| **右边界**：key 以字母结尾时，后一字符**是字母或数字**都算词内 | `Link327`（玩家名）、`Custom1_256`（纹理内部名）、`Mesh0` 都不该拆 |
| **引号内键名透传**：单引号包住且内容像键名（`P`、`LeftControl-P`、`Space`）→ 原样透传 | 键名不是语言。`Click or 'P'` 的 `P` 是单字母，绝不能进词表 |
| **全有或全无**：有字母消费不掉就整串放弃 | 宁留英文，也不出「错误： The remote server…」这种半英半中 |
| 至少命中 1 段才返回 | 纯变量串（`55/100`）不该走这条路 |
| **`HasCjk` 闸门**：串里已有汉字就直接返回 null | TextMesh 那条路会把自己写进去的中文再喂回来，会二次吃掉「space」「Auto」「The Name」 |

**五个边界踩坑**（成因都写在 `LookupSegment` 的注释里）：

1. 色标 `TagLength` 必须 `return close - i + 1`（**含 `]`**）—— 少算一个字节，下一轮左边界
   就撞上 `f`，整串放弃。
2. **必须先 `MatchKey` 再判标记**，否则自带 `[色标]` 前缀的 key（`[-][ffffff]  Pad:[-][ff7f7f] `）
   永远匹配不上；`BuildSegmentIndex` 也要收 `'['` 开头的 key。
3. **右边界只在 key 以字母结尾时才查** —— 写成"无条件要求右侧非字母"会把 `Collected: Collector`
   全误杀（尾空格已被消费，与后继词边界冲突）。
4. **左边界只看字母，右边界看字母+数字** —— 左边界带上数字会把 `Link327` 拆成「连接327」。
   左边界实测是冗余保险（3304 条语料去掉它差异 0 条），留着防将来加透传分支时踩坑。
5. ★ **v1.3.2：左边界只在 key 自己也以字母起头时才判**（旧写法 `if (i > 0 && IsLetter(s[i-1])) return null`）。
   旧规则对「**英文名 + 自带前导空格的碎片**」全线失效：词表里的碎片是 `"  [ffffff]Health:[-][00ff00] "`
   （自带两个前导空格，边界本就成立），可单位标题由 `CommandCenter.GetUnitTitleDisplay()` 拼成
   `GetUnitGameName() + "  [ffffff]Health:…"`，前一字符是 `Key` 的 `y` → 直接被拒 → **所有单位标题**
   （`Shield Key`、`Collector`、`Mortar`、`Pulse Cannon` …）整串放弃、留英文。
   改成 `if (i > 0 && IsLetter(s[i-1]) && IsLetter(s[i])) return null` 后只保留真正要防的
   「字母接字母」。**代价检查**：3304 条语料结果差异 **0 条**、幂等 0 条、安全抽查全过。
   同一个 `MatchKey` 同时服务 `LookupSegment` 与分段索引，改一处两处生效。

**配套批次**：`batches/full_24_fragments.json`（22 条）= 10 条名字碎片
（`' Jojo,'`→`' 乔乔、'`，英文逗号换中文顿号靠这里）+ 12 条资源条碎片。
`AC:` 按术语表**保留英文**，那条 zh 仍为空。工具：`del_strings.py`（按批次删串）、
`add_strings.py`（新增 en，`apply_translations.py` 只能填已存在的）。

**验证（离线，`tools/verify_lookup.py` 复刻四级链）**：28 个目标场景全中（6 种护盾串含混色、
7 条资源条、`Collected: Collector`、`Score: 1234`、`Time:1:00:00.0`、`To Access`、
v1.3.2 新增的 4 条**单位标题**与 3 条 **`Click or '…'`**）；
16 项安全抽查全部正确拒绝（`Windows`/`GameObject`/`RESET ALL STATS…`/`Interval: 3 sec`/
`don't panic`/`it's a trap`/`Don't 'quote' me`…）；
**幂等**：全部译文再喂一遍被二次改写 **0 条**；全量回归「旧已有译文但新结果不同」**0 条**。

★ **v1.3.2 的两个边界修复（都在这条链上，成因见上方踩坑 5 与规则表「引号内键名透传」）**：

| 串 | 旧结果 | 新结果 | 根因 |
|---|---|---|---|
| `Shield Key  [ffffff]Health:[-][00ff00] 100%[-]`（**所有单位标题**） | None | `护盾钥匙  [ffffff]生命值:[-][00ff00] 100%[-]` | `GetUnitTitleDisplay()` 拼 `名字 + "  [ffffff]Health:…"`，碎片自带前导空格，却被旧左边界按 `Key` 的 `y` 拒掉 |
| `Click or 'P'` | None | `点击或 'P'` | `GeneralHandlers.SetPauseButton()` 拼 `"Click or '" + GetKeyText(…) + "'"`，键名是单字母，`IsLetter` 消费不掉 |

引号透传只认「前一字符是 `'` 且再前一字符不是字母、内容全是键名字符（字母/数字/`_`/`-`/`+`）、
≤24 字符、以 `'` 收尾」—— 所以 `don't`/`it's`/`'music'` 这类英文撇号一律不碰（负例全过）。

**注意**：改这一级要动插件 DLL → **需重启游戏**（F9 只重载词表）。

#### ④ 关卡教学提示不翻 —— 已修：文案在**关卡内嵌 CRPL 脚本**里

**现象**：Tempus 关开局那句 `Select a Command Node from the menu below and land it\nin the
gray area…` 是英文。

**四条路全走空（别再重复）**：全部托管 DLL 的 `#US` 堆（56600 条）、`resources.assets` 的 192 个
TextAsset、`level0..7`/`mainData`/`sharedassets*`（根本没有 TextAsset 类型）、存档
`Tempus.cw3`/`system.dat`/`story_0_system`、词表 —— **都没有这句**。

**真正的来源**：关卡资产的 `<Scripts><scripts>Tutorial.crpl;BASE64…</scripts>` —— 关卡自带的
**CRPL 脚本**，base64 解出来是 UTF-16LE 源码。`ShowMessage("…")` 在脚本里，
静态收集器（只认 `ldstr`）当然收不到。

**提取工具链**（`tools/scan_crpl.py`；`--json` 存结构化结果、`--asset <名>` 只看一关、`--raw` 连源码一起打）：

1. `UnityPy.load()` → `o.type.name == "TextAsset"` → **`m_Script` 是 str 不是 bytes**，必须
   `s.encode("utf-8","surrogateescape")` 才拿到原始字节（否则 LZMA 必失败，这是 0 命中的原因）；
2. 前 3 字节 `\x5d\x00\x00` 才 `lzma.decompress(b, format=lzma.FORMAT_ALONE)`；
3. `re.findall(r"<scripts>(.*?)</scripts>")` → 按 `;` 切 → `base64.b64decode(part).decode("utf-16-le")`；
4. 抓字面量**必须整篇 `finditer(r'"((?:[^"\\]|\\.)*)"', re.S)`**，不能逐行 ——
   `alpha_Troublesome Trains 01` 整篇只有 1 行（264 个真 CRLF），逐行做几乎全漏；
5. `\n`/`\t`/`\r` 反转义成真字符再入库。

**规模**：15 个关卡资产含 `ShowMessage`，共 **67 条字面量**（去重 60 条）：
`Tempus`(8) / `Trains 01`(16) / `02`(17) / `Carcere`(4) / `Tiplex`(4) / `Telos`(4) /
`Starsync`(4) / `Ormos`(3) / `Seedet`(1) / `Flick`(1) / `Jojo`(1) / `Ruine`(1) /
`alpha_NoWasteLand`(1) / `alpha_Super Tower`(1) / `alpha_Electricity Quiz`(1)。

**渲染链（★ 它是 IMGUI，不是 UILabel —— 这是整件事的关键）**：

```
关卡 CRPL: ShowMessage("…\n…")
  → CrplCore.ProcessCommands      ★ Replace("\\n","\n") / ("\\t","\t") 还原转义
  → GameSpace.message / messageX / messageY / messageDismissible   ← 字段属主是 GameSpace
  → GameSpace.OnGUI()                             token=0x06000EF3
  → GUI.Label(Rect, String, GUIStyle)             token=0x06001A04  ← 唯一绘制点
  → GUI.Label(Rect, GUIContent, GUIStyle)         token=0x06001A06  ← 六重载唯一收口
  → GUI.DoLabel(Rect, GUIContent, IntPtr)
```

**为什么容易看走眼**：`CrplCore` 里另有一条 `ShowMessageDialog`（`0x06000A00`）→
`CRPLInfoPanel.ShowInfoMessage`（`0x06000FCC`）→ **真的是 UILabel**。但那是带 OK/Cancel 的
对话框；`SHOWMESSAGE`/`SHOWMESSAGEDISMISSIBLE` 走 `GameSpace.message`，由 `GameSpace.OnGUI`
的 `GUI.Label` 画 —— **`ProcessText` 覆盖不到这一条**。

**最终修法（v1.3.0）：`CrplCore.ProcessCommands` postfix**
补丁点 `ProcessCommands(Boolean,Boolean,Boolean,Boolean)` `token=0x060009E1`，调用方只有
`PreGameUpdate`/`GameLoadedGameUpdate`/`GameUpdate`，postfix 正好落在「已 `stfld message`、
还没到 `OnGUI` 绘制」之间。**在 `message` 被赋值那一刻就换中文**，于是 `OnGUI` 后面
`skin.GetStyle("message").CalcSize(...)` 量到的就是中文尺寸，盒子由游戏自己算准，一行 `Rect`
都不用写。安全性（实测）：`GameSpace`/`CrplCore` 都是 `tdPublic` 顶层类、相关字段全 `fdPublic`；
`message` 的读者只有 `GameSpace.OnGUI`（4 处 `ldfld`），**没有任何逻辑分支读它**；
`GameSpace.GameUpdate` 每帧开头 `stfld message = null`，所以每帧重走一遍，`HasCjk` 挡住重复翻译
—— 天然无状态，不需要还原表。实现：`PatchCrplMessage()` / `PostfixCrplMessage()`
（字段反射缓存 `fldGsInstance`/`fldGsMessage`/`fldGsSkin`）→ `Lookup` → `SetValue(gs, zh)` →
`ApplyCjkFont(gs.skin.GetStyle("message"))`。

**★ 中文缺字形**：IMGUI 走 `UnityEngine.Font`，游戏只有两个字体资产（`PathID 755`
`Aldrich-Regular`、`PathID 756` `arial`），cmap 无汉字 → 光换文字会显示成空白/方框。所以首次命中时
`Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16)`（`0x0600190D`）现场造字体塞进
`GUIStyle.font`（`0x06001C50`）。`GUISkin.GetStyle` 返回皮肤内部对象引用，改它**持久生效**。
候选按「雅黑 → 黑体 → 宋体 → Noto」用 `Font.GetOSInstalledFontNames()` 比对。

**两个入库坑**：① `Trains 01` 的换行是脚本里的**真 CRLF**（不是转义），而 `ProcessCommands`
只换 `\\n`、**不动 CR** → 运行时是 CRLF，批次里 CRLF 与 LF 两种形态都登记了；
② `Starsync` 那句原文自身残缺（开头缺 `***NOTE***` 前半）—— **照原样译，别替游戏补全**。

**入库**：`batches/full_25_crpl.json`（60 条），`check_charset` 报 1547 字符（汉字 1448）✓ 在字集内，
**不用重烘图集**。改词表只需 **F9**；改插件 DLL **必须重启游戏**。

> ★ 一条仍有效的知识（v1.2.0 实验留下的）：`GUIContent.Temp(String)`（`0x06001AD8`）返回
> **静态共享单例** `s_Text`，任何 `GUI.Label` 补丁都**绝不能**改 `content.text`，必须
> `new GUIContent(zh)` 换引用。（v1.2.0 那套 `GUI.Label` prefix + `FitBox` 猜盒子宽度的方案
> 已被 v1.3.0 整段删除，用户要求「出问题时只能有一个来源」。）

#### ⑤ 「Tech: RELAY」不翻 —— 已修：`TECH_TYPE` 枚举名（20 条全大写）

**现象**：v1.3.2 修好单位标题（`Shield Key  [ffffff]Health:…`）之后，用户仍报
`Tech: RELAY  [ffffff]Health:[-][00ff00] 100%[-]` 整条不翻，并质疑
「这个类似的问题我们应该修过了啊」。

**根因（与 §4.7⑤「左边界」那条**不是**同一个问题）**：`RELAY` 不是任何资产字面量，
而是 **`TechArtifactManager.TECH_TYPE` 枚举的 `ToString()`**：

```
UnitManager.GetUnitGameName()          ← 拼在标题最前面
  → if (name == "TechArtifact") return "Tech: " + (this as TechArtifactManager).techType;
  → CommandCenter.GetUnitTitleDisplay() → text2 + "  [ffffff]Health:[-]…" → unitTitle.text
```

`TECH_TYPE` 共 20 项，`ToString()` 出**全大写枚举名**：`COLLECTOR RELAY REACTOR SHIELD
OREMINE SIPHON TERP GUPPY CANNON MORTAR STRAFER BOMBER NULLIFIER SPRAYER BEAM SNIPER
FORGE BERTHA THOR COMMANDNODE`。

词表里 **19 条本来就存在、但 `zh` 全是空串**（`grade=other`、`cat=dll`，
静态收集器给它们的 note 是「单个单词：可能是按钮文字，也可能是资源名 —— 需人工确认」），
`CANNON` 那条**根本缺席**。光标扫到 `RELAY` 时 `byFirst` 里其实有这个 key，
但 `dict` 编译时只收**非空译文**，所以取出来是空串，等于未命中。

**为什么之前没暴露**：这批条目全是 `grade=other`（低可信度池），一直没人确认；
而 `Tech:` 开头的单位标题只有拿到科技圣物时才出现，露面机会少。

**安全性（已核实才敢填）**：全库 `.text == "全大写串"` 只有 3 处
（`ACTIVATE` / `ARM` / `RESUPPLY`），**都不在这 20 个里**；`.text.Equals` / `.ToUpper` /
`.ToLower` 零处；`CrplCore` 的 `UNITS[unit.ToUpper()]` 与 `GetUnitType()` **只喂 CRPL 脚本、
不上屏**，TRACE 输出走 `traceQueue` → `GetTraceLog()` → CRPL 塔调试窗口（玩家脚本数据，不能译）。

**入库**：`batches/full_28_tech_upper.json`（19 条填空，`apply_translations.py`）+
`batches/full_28_add_new.json`（3 条新增：`CANNON` 与主菜单新闻条两条，`add_strings.py`）。
译文与正常名逐条对齐：`RELAY`=中继器、`CANNON`=脉冲炮、`COMMANDNODE`=指挥节点 …。

**★ 教训**：「全大写单词」这一档（`grade=other`、`cat=dll`，词表里 150+ 条）里混着
**枚举名**，它们是真上屏的；但同一档里也混着 CRPL 内部名（`RESOURCETYPE`、`FIRERATE`、
`STAT_*` 等），那些**绝不能填**。判断依据只能是「有没有上屏路径」——
用反编译看它是否被拼进 `label.text`。顺便：这批 `STAT_*`、`D_DA`、`AC_P` 之类
永远不上屏，属词表噪声，不必再管。

#### ⑥ 主菜单新闻条不翻 —— 已修：词表补 2 条

`MainMenu.OnGUI()` 拉服务器新闻（`http://knucklecracker.com/creeperworld3/news.php`），
按 `|` 切段：`u=` 开头的段 → `GUILayout.Button(链接文字)`；其余拼起来 → `GUILayout.Label(text)`。
当前服务器发的是 `CREEPER WORLD 5 WISHLIST NOW! -> |u=<url> Open_Web_Page`，
于是屏上出现 `CREEPER WORLD 5 WISHLIST NOW! -> `（Label）与 ` Open_Web_Page`（Button）。
这两条**正是 v1.4.0 新 IMGUI 层该抓的**，只是词表没有它们 → 补上即可。
注意 `Open_Web_Page` 拼写带下划线、且按钮文字**带前导空格**，两处都要照原样。

> 新闻条每次启动会重新拉取，**服务器随时可能换文案**；这类「外部内容」漏译属正常，
> 看到 F5 里有整句英文新闻时，先判断是不是服务器新发的。

---

### 4.8 ★ 剧情对话文本（579 条台词 + 294 条名字）

**问题**：游戏里对话框（Lia 那段）全是英文，F5 未命中清单里能看到整段台词。

**根因**：剧情台词不在 DLL 的 `ldstr` 里，而在 **`CW3_Data\resources.assets` 的 TextAsset**
里，LZMA 压缩的 XML。`collect_ui_strings.py` 只扫 IL 与资产 `mText`，自然收不到。

**数据链路（已实测）**：

```
TextAsset.m_Script     ← UnityPy 读出来是 str，必须 .encode('utf-8','surrogateescape')
  → lzma.decompress(format=lzma.FORMAT_ALONE)   ← 不是 RC4！RC4 只在存档加密时用
  → XML <Messages><Message><c>角色</c><m>整条台词</m></Message>
```

**为什么加词表就能翻**（不改插件）：`MessageDialogManager.set_currentMessage`（`0x06000C2F`）
是 `messageText.text = msg.message` —— **一次只写一条**进 UILabel，所以精确匹配就能命中，
`\n` 与空行原样保留。角色名走另一个 UILabel `characterName`，值来自 `Awake`（`0x06000C31`）
硬编码的 8 元数组：`Skarsgard Abraxis` / `Lia` / `Loki Hive` / `Abrax... Imper...???` /
`Loki Imperator` / `Aliana` / 空 / 空（后两位留给自定义角色）。

**工具（`tools/`，一次跑完）**：`extract_story_text.py`（解压 TextAsset → 抽 `<m>` 台词与
`<n>/<g>` 名字 → `_dict/story_candidates.json`）→ `story_batches.py`（按文件切批，≤60 条、
不拆单文件）→ `story_normalize.py`（跨批译名统一，`--write` 写回）→ `story_merge.py`
（校验覆盖/id/`\n` 个数/残留英文/句数，`--write` 并入 `strings.json`、`--sync` 覆盖
kind=Dialog 译文）；另有 `audit_xml_text.py` 反向审计漏网文本（如星系名）。

**规模**：584 条 `<m>`（去重 579，69494 字符，最长 327）；名字 273 条（星球/星区/任务名）；
另有 `story_sector`/`credits_sector` 的 9 颗恒星名 + 1 个 credits 星名、12 个 `X Shield Key`、
`Right Click To Enable`、5 个角色名 —— 共 23 条走 `story_work/12_extra.out.json`（格式 `{en,zh,kind}`）。

**不译的三类**（刻意跳过，别重开）：
- `license`（EULA 10203B）、`uploadLicense`（投稿协议 4482B）—— 法律文本
- `planetnames`（94967B，约一万个一行一个的随机星球名）—— 随机名无意义
- XML 里的 `.crpl` 脚本文件名、`MAIN;Default`、`name;STRING:...`、`sun`/`False` 等内部标记

**命名口径**（以 `glossary.md` 为准，跨批已统一）：Ticon 泰肯、Prospector Zone 探矿者星区、
Digitalis 魔苋、Tech Artifact 技术圣物、Loki Hive 洛基巢穴、Loki Imperator 洛基统领、
C.E. → **第 N 循环历**（不写「公元」，也不写「第 X 纪元」，全词表统一；见 4.9）。

**结果**：`strings.json` 2404 → 3254、`zh_cn.dict` 1144 → 2021（含 579 台词 + 294 名字类），
**现行数字见 4.5**。✅ 游戏内实测通过（对话框、角色名、星系名全变中文）。
但第一次实测是**大面积失败**的，根因与修法见 4.8a —— 这条坑值得单独记住。

---

### 4.8a ★ 剧情台词「词表有、游戏里查不到」的根因：CRLF

**现象**：词表 579 条台词离线逐条校验全对（`dict[key]` 都能取到），进游戏却大部分不翻译；
角色名、UI 反而正常。F5 未命中清单里出现的正是这些台词（前缀看着和原文一模一样）。

**根因**：**XML 资源里的换行是 CRLF**（实测 189 个 TextAsset 解压后 244 533 个 `\r\n`）。

- `xml.etree.ElementTree` 按 XML 规范在解析时把 `\r\n` **归一成 `\n`** → 我们用到的 key 全是 LF。
- 游戏自己的解析路径保留了 `\r` → 运行时 `msg.message` 里是 `\r\n`。
- 于是 `"A\r\nB"` ≠ `"A\nB"`，`Dictionary.TryGetValue` 必然落空 —— 只有**单行台词**
  （不含换行）能命中，所以看起来「有的翻了有的没翻」。另有若干条原本尾部带空格，
  即使单行也会差一个空格。

**修法（插件 v0.10.2，`Lookup` 第三级兜底）**：精确 → `\n` 拼接 → **归一化匹配**。
归一化（建表与查表两端一致）：`\r\n`→`\n`、连续空格折叠成一个、去掉尾部空白（含换行）。
`dictNorm` 在 `LoadDict` 里与 `dict` 同时建，HUD/F11 的「归一化=N」就是它命中的条数。
只在前面两级都失败后才走，对既有 1000+ 条译文零影响。

**顺带留下的诊断件（`NoteMiss` 里，v0.10.2 起）**：长度 ≥20 的未命中串会写进
`_cw3hanhua\_dict\miss_dump.txt` —— 全文（`\n` 转义）+ 变体试探（TrimEnd / Trim / +`\n` /
双空格折叠 / CRLF→LF / TrimEnd+`\n`，命中的那行标 `[HIT:xxx]`）+ 前缀相同的最接近 key
与**第一个不同字符的码位**。以后再遇到「应该翻却没翻」，先看这个文件，不用猜。

> 教训：**别用会做规范化的 XML 解析器去产出「要拿去精确匹配运行时字节」的 key。**
> 正确做法是解析前把 `\r\n` 手工留住（`xml.replace(b"\r\n", b"&#13;\n")` 之类），
> 或者像现在这样在查表侧做归一化。两者选一，别两边都不做。

---

### 4.9 开场动画（PreludeManager）译文修正

**链路**：`PreludeManager.FixedUpdate` 里 31 处 `ldstr` 紧接
`PreludeManager.ShowText(String,Single,Single,Single,Int32,Single,Single)`，由文本层
`PrefixAnyShowText` 查词表（4.6a）。**跨行旁白是真的拆成三条 ldstr**，每条单独查表，
照原文逐句译即可。

**改掉的 5 条**（批次 `full_10.json` / `full_11.json`，`apply_translations.py --override`）：

| 原文 | 原译（问题） | 现译 |
|---|---|---|
| `20th Century, 1st C.E.` | 公元 1 世纪，20 世纪（**年代倒置**，且丢掉"第 1 个循环历"） | **20 世纪，第 1 循环历** |
| `Their impossible journey ended in the destruction of the` +<br>`enemy Nexus and the founding of a new, sole human colony.` | `ended in` 后并列**两个宾语**（毁灭 / 建立），旧译硬塞成"以…，以及…" | 「那场不可能的征途，终结于摧毁」+「敌人的枢纽，并建立了人类唯一的新殖民地。」 |
| `Yet, a small group of less than 50 thousand humans survived.` | 一小群不足 5 万的人类（修饰顺序颠倒） | **然而，不到 5 万人的一小群人类幸存了下来。** |
| `Thousands of worlds…` | **成千上万**个世界（只对应 tens of thousands） | **数千个世界毁于一个未知的敌人。** |
| `All they had achieved ended when the Creeper arrived.` | Creeper **降临**时（降临是神性用词） | 当 Creeper **袭来**时，他们的一切成就都终结了。 |

**`C.E.` = 「第 N 循环历」，不是公元**。游戏自己把它用成**可重复的纪元编号**
（`1st Common Era` / `3rd C.E.` / `144th C.E.` / `early Common Eras` 复数同现），
指"一轮人类文明"。全词表 7 处统一：`20th Century, 1st C.E.` → 20 世纪，第 1 循环历；
`13271 in the first Common Era` → 第 1 循环历 13271 年；`144th C.E.` → 第 144 循环历
（`3rd`/`300th`/`1st` 同构）。

> 年代倒置的实质错误（原译"公元 1 世纪，20 世纪"）就是丢掉了"N 循环历"这一层。
> 补一条度量警告：CW3 文本是梗概不是硬数字，**第 1→第 144 循环历之间的间隔没写明**，
> 别把旁白里的"三十年后"当成下一轮的起点。

**保留的既有问题（不擅改）**：旁白里的 `Creeper` 是地名式的"它"（`when the Creeper arrived`
→ "当 Creeper 袭来时"），而术语表规定核心敌人名**保留英文**。若要读顺，得在术语表里给旁白加
一条意译，**需要用户拍板**，不要单方面改。

**入库路径（C.E. 改词表时走的是第二条）**：
- 批次的普通条目 → `apply_translations.py <batch> --override` → `build_dict.py --include-other` → `check_charset.py`
- **剧情对白（kind=Dialog）改的是 `story_work/NN_*.out.json`，必须 `story_merge.py --write --sync`**
  （`--write` 本身就带 sync；`--sync` 只覆盖 `kind == "Dialog"` 的条目，其它走 `conflict` 不动）
- 改完按 **F9** 重载词表即可验证，不用重烘图集。

---

## 五、环境与陷阱

### 5.1 沙箱写入权限

游戏重装后，shell 的受限进程可能无法写工作区（连游戏目录根都不行），但磁盘权限本身正常
（`diagnose-windows-sandbox-acl` 判定 `NOT_THIS_CLASS`）。
**`write`/`edit` 文件工具仍正常**；`csc.exe` 输出 DLL 可能失败。

### 5.2 编译插件

**用 `_cw3hanhua\tools\build_plugin.cmd`（一次编出**一个** DLL）**，不要手敲命令：

```powershell
& "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3\_cw3hanhua\tools\build_plugin.cmd"
```

★ **两个 `.cs` 一起编进 `BepInEx\plugins\CW3Hanhua.dll`**（现 v1.4.0，4.1）。
脚本在编译前会**自动 `del` 掉旧的 `CW3HanhuaText.dll`** —— 那个文件是独立插件
（GUID `cw3hanhua.text`），留着会在同一批游戏方法上**重复装一遍 Harmony 补丁**。

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
- **`if exist (...)` 块里的 `echo` 文案不许出现裸 `)`** —— cmd.exe 在**第一个不平衡的 `)`**
  处就闭合 if 块，后面的 csc 命令行全漏到块外**无条件执行**（症状：源文件已删仍报
  `CS2001` / `CS2008`，极易误判成"文件没删干净"）。块内一律用 `-` 或 `[]`。

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
NGUI 行为怪异时**先反编译再推理**。反编译产物是临时的，随手丢进系统临时目录或
`_cw3hanhua\_diag\`（该目录不进版本库，已清空，可随时重建）都行，**不要入库**。

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
| 手写 IL 解析器 `il.py` / `ilscan.py` / `ilscan_field.py` / `scan_all_text.py` / `collect_fingerprint.ps1` | 解码 bug（tiny header、`throw` 长度、`conv.ovf.*` 缺失）污染过计数（`set_enabled` 371 vs 真 249）。**只信 `ilx`**（5.4） |
| 三个图集探针 `AtlasProbe` / `AtlasWriteProbe` / `UIFeasibilityProbe` | 结论已并入 4.4 / 4.4a，源码已删 |
| `CW3HanhuaText.dll`（独立插件）及整套 peer 反射（`FindPeerMethod` / `peerMethods` / `PeerSetText` / `PeerString` / `PeerHudAlive` / 文本层自己的 `OnGUI` 兜底条） | 2026-10-23 v1.0.0：两个 `.cs` 合进一个 DLL，两层改成直接方法调用。**不要再拆回去**（4.1 / 4.3） |
| `Config.Bind("General","AutoEnableOnStartup",…)` + `cw3hanhua.core.cfg` | 用户要求"一定是自动打开的、不要额外架构"：自动开启改为**无条件**，配置项整段删除 |
| 41 条护盾提示语整串枚举（`gen_shieldmsg.py` + `batches/full_23_shieldmsg.json`） | 当天立了又撤（用户："再发现新的就没完没了"）。改由 `LookupSegment` 通用解决（4.7③） |
| 「资源条碎片替换表」方案（`IndexOf` 原地替换） | 被 `LookupSegment` 取代 —— 同思路的通用版（4.7①） |

### 5.8 已排除的假设（查过、不成立，别再花时间）

| 假设 | 否证方式与结论 |
|---|---|
| 「`LookupConcat` 把 `\n` 归给右段，改成归左就能修好护盾串」 | 双版对比 3295 条语料：「只有旧能中」「只有新能中」都是 **0**；且改动**会破坏** `To Access` 场景。**`LookupConcat` 保持原样**（4.7③） |
| 「不做词边界，直接对任意位置做子串替换」 | 词中命中灾难：**124 个 key、490 次**（`Windows`→`风dows`、`OnDestroy()`→`On摧毁()`）；且译文本身会被二次吃掉（4.7③） |
| 「关卡教学提示是 IMGUI/OnGUI 画的」 | **初版判错，已平反**：它**确实是 IMGUI** —— `GameSpace.OnGUI`（`token=0x06000EF3`）的 `GUI.Label(Rect,String,GUIStyle)`（4.7④）。初版以为落 `CRPLInfoPanel` 的 UILabel，是因为把 `ShowMessageDialog` 当成了 `ShowMessage` |
| 「教学提示在 `resources.assets` 的 TextAsset 明文里」 | 192 个 TextAsset 全解压后 0 命中；脚本是 **base64 编码在关卡 XML 的 `<scripts>` 节点里**，解出是 UTF-16LE（4.7④） |
| 「`MessageDialogManager` 是 CRPL `ShowMessage` 的落点」 | 不是，它是**剧情对话专用**（4.8）。`ShowMessage` 走 `GameSpace.message`；只有带 OK/Cancel 的 `ShowMessageDialog` 才走 `CRPLInfoPanel` 的 UILabel |

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
**所以读 `UIFont` 一律以反编译为准**（现场生成：`tools\ilspy.cmd
"...\CW3_Data\Managed\Assembly-CSharp.dll" -t UIFont`）。

要拿上游 `UILabel` 源码：`_ngui_repo/` 里有完整历史，
`git -C _ngui_repo show 625abae8:Assets/NGUI/Scripts/UI/UILabel.cs`。

`UILabel` 判别特征：26 个字段，**同时具备 `mLastScale` 且不具备 `mFontSize`/`mFontStyle`/`mOverflow`**。

---

## 七、目录与维护纪律

```
_cw3hanhua/
├─ NOTES.md            ← 本文（唯一文档）
├─ tools/              ★ 全部可复现工具（见 tools/README.md）
│   ├─ build_plugin.cmd       一次编出一个插件 DLL（唯一编译入口，5.2）
│   ├─ ilx.exe / ilx\ilx.cs    基于 Mono.Cecil 的 IL 检查器（5.4）
│   ├─ ilspy.cmd / ilspycmd/   反编译成 C#（5.4）
│   ├─ bake_font.py            烘字形（4.1）
│   ├─ build_atlas.py          ★ 合并图集 4096×8192 生成器（4.4）
│   ├─ check_charset.py        字集覆盖检查
│   ├─ collect_ui_strings.py   静态全量收集（4.2）
│   ├─ scan_crpl.py            关卡内嵌 CRPL 脚本字面量（4.7④）
│   ├─ build_dict.py           编译 zh_cn.dict
│   ├─ apply_translations.py   批次译文注入 strings.json
│   ├─ audit_translations.py   ★ 全表一致性审计（标记/换行/占位符/首尾空白）
│   ├─ test_classify.py        classify() 回归样例
│   ├─ prep_batches.py         未译分流 + 切批（会重建 _dict/work/）
│   ├─ merge_work.py           work/ 译稿 → batches/
│   ├─ check_batch.py          单批译稿校验
│   ├─ pack_skips.py           「人工判定不译」固化 → _dict/skipped_manual.json
│   ├─ dict_stats.py           覆盖率报告 → _dict/STATUS.md
│   ├─ scan_uilabel.py / scan_textmesh.py  只读资产盘点
│   ├─ diff_bake.py            ★ 烘焙回归：现役资产 vs 基线，确认没波及 UILabel
│   └─ uilabel_inventory.json / textmesh_inventory.json
│                              ★ 烘焙前的历史基线快照，**不可再生**（重跑 scan 会覆盖）
├─ plugin/             ★ 两个在用源文件，**一起编进同一个 CW3Hanhua.dll v1.4.0**（4.1）
│                      CW3Hanhua.cs（字体层 = 唯一 BepInPlugin / MonoBehaviour）
│                      CW3HanhuaText.cs（文本层 = 同程序集普通类 TextPlugin）
├─ _font/              ★ 运行时：atlas_zh.png + atlas_zh.bin（字体层的全部输入）
│                      ★ 工具链：font_{16,20,28}.{json,png}（重烘图集的必需输入）
├─ _dict/              ★ 词表：strings.json（唯一数据）/ glossary.md（术语表）
│                      / zh_cn.dict（编译产物）/ STATUS.md（自动生成）/ batches/（25 份批次）
│                      / skipped_manual.json（565 条「人工判定不译」，勿删）
├─ _diag/              【已删除】原为 ilspy 反编译源码 + 诊断 PNG 的临时目录（见下）
├─ _ngui_repo/         NGUI 仓库 clone，77 MB（gitignore；查上游源码用）
└─ .venv/              Python 环境（UnityPy / dnfile / dncil / fontTools / Pillow，gitignore）
```

★ **`_diag/` 已于 2026-10-21 清空删除**（26 个文件 / 11.1 MB）。里面只有两类东西：
① `tools\ilspy.cmd` 一次就能重生成的反编译源码（`UIFont.cs` 等）—— 要用就现场生成，
例如 `& tools\ilspy.cmd CW3_Data\Managed\Assembly-CSharp.dll -t UIFont`；
② 历次诊断的对照 PNG —— 属于中间产物，结论与数字都已写进本文 4.4。
其中真正不可再生的 3 个只读脚本 + 2 份基线 JSON 已迁到 `tools\`（见上）。
该目录仍在 `.gitignore`，将来临时放反编译产物没问题，但**不要再往里堆需要长期保留的东西**。

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
   文本条数的唯一权威是 `_dict/STATUS.md`（现行 **3317** 条候选，已译 **2127** 条；
   `zh_cn.dict` 同数，见 4.2）。
4. **结论必须有证据等级**：区分「IL 静态证据」「运行时实测」「源码推断」。
   不要把推断写成事实 —— 本项目曾多次因"看起来像"而下错结论（最近一次：
   把资源条模板串的修法说成"前缀匹配"，反汇编后才证伪，见 4.7）。
5. **失败与假象也要记**，一张"已排除假设"表比结论本身更省时间（5.6）。
