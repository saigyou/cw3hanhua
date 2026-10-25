# Creeper World 3 汉化补丁

[**Creeper World 3: Arc Eternal**](https://store.steampowered.com/app/280220/) 的简体中文汉化。

游戏本身不支持中文，也不支持多语言系统。本补丁用
[BepInEx](https://github.com/BepInEx/BepInEx) 挂一个插件，在运行时把文本换成中文 ——
**不修改任何游戏文件**（唯一例外是第四节的**可选**静态文本烘焙）。

- 当前版本：**v1.5.0**
- 适用游戏版本：Steam 版 **2.12**（Unity 5.2.3f1，32 位）
- 平台：Windows

---

## 一、先看这里：它翻了什么

| 类别 | 状态 |
|---|---|
| 界面文本（面板、按钮、提示、单位名、科技名……） | ✅ 已译 |
| 剧情对话（584 条消息，去重 579 条台词） | ✅ 579/579 |
| 星球名 / 星区名 / 任务名 | ✅ 已译 |
| CRPL 教学提示（关卡脚本产出） | ✅ 已译 |
| 结算面板 / DMD 界面 | ✅ 已译 |
| 主菜单那 19 条静态大字（见下） | ⚠️ **需要额外一步烘焙**，否则显示英文 |

词表共 **2342** 条译文。另有 **565** 条**刻意不译**（`Creeper`、`AC` 等专有名词，
以及 `SHIFT`、`F11`、`Alpha3` 这类标识符）。

按「玩家实际看得到的文本」统计（`kind` 是 `UILabel`/`TextMesh`，或出现在游戏资产里的），
覆盖 **1231/1317 = 93.5%**；剩下的 86 条里，28 条在刻意不译清单内，其余基本是
`[00ff00]100`、`Time:1:00:00.0`、`()` 这类占位符与标识符。

未翻的大头是**玩家永远看不到**的引擎日志与开发期异常文案
（`VectorLine.SetCamera3D: no camera tagged...` 之类，共约 1100 条 dll-only 条目）。

> 数字口径：`_cw3hanhua/_dict/STATUS.md` 里的「66.3%」是按**全部** 3530 条候选算的，
> 分母里混着 636 条纯标识符、11 条 md5 哈希和 565 条刻意不译 —— 那个数字不代表
> 翻译质量，别看它。

### 那 19 条「静态大字」是什么

主菜单的 `START HERE!`、`Arc Eternal`、`Settings`、`Credits`、`Exit Game`、
`Prospector Zone`、`Tormented Space` 等，以及过场旁白、`Click To Engage`、
`Show Sector Map`、`Click Here!`、`Fresh Ideas In Gaming`。

这些文字的载体是 Unity 的 `TextMesh` 组件 —— 它的 `set_text` 是 extern icall，
Harmony 补丁打不上，只能在**游戏资产文件**里把中文烧进去。所以：

- **不做这一步**：这 19 条保持英文，其余一切正常。
- **做了这一步**：全中文，零闪烁。

因为资产文件是游戏本体的数据，**补丁包不能附带**（见「四、烘焙静态文本」自己跑脚本）。

---

## 二、安装

### 1. 确认游戏版本

Steam 库里右键 Creeper World 3 → 属性 → 已安装文件，确认是 **2.12**。
别的版本没测过，出问题先看这条。

### 2. 解压到游戏根目录

把 Release 压缩包**整个**解压到游戏安装目录，和 `CW3.exe` 同级。解压后应该是这样：

```
Creeper World 3\
├─ CW3.exe                       ← 游戏本体（原有）
├─ CW3_Data\                     ← 游戏数据（原有，不动）
├─ winhttp.dll                   ← 解压进来的
├─ doorstop_config.ini
├─ .doorstop_version
├─ BepInEx\
│   ├─ core\                     ← BepInEx 5.4.23.5 官方二进制
│   ├─ config\BepInEx.cfg
│   └─ plugins\CW3Hanhua.dll     ← 汉化插件
└─ _cw3hanhua\
    ├─ _font\atlas_zh.png        ← 合并图集 4096×8192
    ├─ _font\atlas_zh.bin        ← 字形表
    └─ _dict\zh_cn.dict          ← 词表 2342 条
```

> **⚠️ 别只拷 `CW3Hanhua.dll`。**
> 插件是按「游戏根目录 + `_cw3hanhua\_font`」这个固定路径找图集和词表的
> （`Paths.GameRootPath`）。只拷 DLL 的话它找不到资源，会**静默地什么都不做** ——
> 游戏正常启动、日志也没有红色报错，就是全英文。这是最常见的"装了没反应"。

> **⚠️ 别用 BepInEx 默认配置。**
> 本补丁附带的 `BepInEx\config\BepInEx.cfg` 里，注入点是**改过的**：
>
> ```ini
> [Preloader.Entrypoint]
> Assembly = Assembly-CSharp.dll
> Type = SteamManager
> Method = Awake
> ```
>
> 默认值（`UnityEngine.dll` / `Application` / `.cctor`）在这游戏上**必崩**
> —— `Application..cctor` 在 Mono 装载引擎程序集时就被触发，此时 Unity 对象系统
> 还没就绪，`AddComponent` 会直接野指针崩溃（`0xC0000005`）。
>
> 所以：**装/升级时务必保留这个 `BepInEx.cfg`**。如果你还装着别的 BepInEx 插件，
> 注意别让它把这份配置覆盖回默认值。

### 3. 从 Steam 启动游戏

正常点「开始游戏」即可。进游戏后汉化**自动生效**，不需要按键。

---

## 三、按键

汉化本身不需要按任何键，下面这些是**排查用**的。

| 键 | 作用 |
|---|---|
| **F3** | 汉化总开关（字体层 + 文本层一起开关） |
| **F5** | 打印「未命中清单」—— 哪些英文没翻到。写进 `_cw3hanhua\_dict\miss_dump.txt` |
| **F9** | 重新载入词表 —— 改完 `zh_cn.dict` 不用重启游戏 |
| **F11** | dump 详细报告 → `BepInEx\CW3HanhuaFont.txt` |
| **F12** | 显示/隐藏 HUD（默认**关闭**，左上角，只报开关状态） |

F5 和 F11 的输出贴到 Issue 里，比说「某处没翻」有用得多。

---

## 四、烘焙静态文本（可选，让那 19 条大字也变中文）

**不做这一步完全不影响其他内容。** 想让主菜单也全中文才需要。

这一步需要 **Python 3.8+**（UnityPy 的要求）。脚本在动手前会先探测能不能写游戏文件，
写不了会直接告诉你原因 —— 不必预先提权。

> 游戏装在 `C:\Program Files (x86)\` 下时，Steam 装出来的目录通常已给本机用户完全控制权，
> **一般不需要管理员**。如果探测报 `PermissionError`，用管理员终端重跑即可。

```powershell
# 1) 建虚拟环境、装依赖（和项目约定一致，放在 _cw3hanhua\.venv）
cd "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3\_cw3hanhua"
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install UnityPy

# 2) 回游戏根目录，跑烘焙（脚本自己按 __file__ 定位，从哪跑都行）
cd ..
$py = "_cw3hanhua\.venv\Scripts\python.exe"

& $py _cw3hanhua\tools\bake_textmesh_assets.py --backup    # 备份，幂等，永不覆盖已有备份
& $py _cw3hanhua\tools\bake_textmesh_assets.py --dry-run   # 预演，看看会改哪几条
& $py _cw3hanhua\tools\bake_textmesh_assets.py             # 烘焙
```

> 只想重烘图集（上一节）的话，`pip install UnityPy` 就够了 —— 它会带上 `Pillow`。
> 想跑取证类工具（`scan_uilabel.py`、`diff_bake.py`、`collect_ui_strings.py`）才需要
> `pip install "UnityPy[ttgen]"`。

正常输出 `校验通过 19 条，失败 0 条` 就对了，耗时约 2~3 秒。

### 撤销烘焙

```powershell
$py = "_cw3hanhua\.venv\Scripts\python.exe"
& $py _cw3hanhua\tools\bake_textmesh_assets.py --restore
```

逐字节回到英文原版（脚本用 `_cw3hanhua\_assets_orig\manifest.json` 里的 sha256 校验）。

### 烘焙的两个代价

1. **会改 6 个游戏资产文件**（`level1`、`level2`、`level5`、`mainData`、
   `resources.assets`、`sharedassets6.assets`），备份在 `_cw3hanhua\_assets_orig\`（约 71.5 MB）。
2. **Steam「验证游戏文件完整性」会把它们整份重下**，汉化当场消失。
   重新跑一次烘焙即可。除这 6 个文件外，本补丁只读游戏文件、不写入，
   所以「不烘焙」的用法完全不受 Steam 更新与文件校验影响。

---

## 五、卸载

删掉这五项就干净了（游戏本体一点没动过）：

```
winhttp.dll
doorstop_config.ini
.doorstop_version
BepInEx\
_cw3hanhua\
```

如果烘焙过，先跑一次 `--restore` 还原游戏资产，再删 `_cw3hanhua\`。

只想去掉汉化、保留 BepInEx 给别的插件用：删 `BepInEx\plugins\CW3Hanhua.dll` 即可。

---

## 六、已知限制

- **只支持 2.12 版**。
- **自定义地图的 CRPL 脚本字面量不一定翻到**。插件翻的是游戏自己产出的消息，
  地图作者在自己脚本里硬写的英文不会翻。
- **19 条 Static TextMesh 需要烘焙**（见第四节的取舍）。

---

## 七、从源码构建

只需要 **.NET Framework 3.5** 自带的 `csc.exe`（Windows 10/11 默认就装着）。
`build_plugin.cmd` 里写死的路径是：

```
C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe
```

安装目录不在默认位置、或者系统是 64 位且只装了别的运行时，改脚本第 18 行
（`set "CSC=..."`）即可。**必须用 3.5 那一份**，不能用 `v4.0.30319` 里的。

```powershell
cd "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3"
_cw3hanhua\tools\build_plugin.cmd
```

产物是 `BepInEx\plugins\CW3Hanhua.dll`。

编译参数不能改，原因写在脚本注释里：必须 `/platform:x86`（游戏是 32 位进程）、
必须 `/codepage:65001`（源码是无 BOM 的 UTF-8）、必须用 csc 3.5（对齐游戏里的 Mono
运行时，用新 BCL API 会运行时崩）。

源码只有两个文件，编进同一个程序集：

- `_cw3hanhua/plugin/CW3Hanhua.cs` —— 字体层（图集合并），唯一 `BepInPlugin`
- `_cw3hanhua/plugin/CW3HanhuaText.cs` —— 文本层（词表 + 5 条替换路径）

技术细节写在 `_cw3hanhua/NOTES.md`（2200+ 行，是给改代码的人看的，不是使用文档）。

### 只改译文、不改代码

```powershell
# 改 _cw3hanhua\_dict\strings.json 里的 zh 字段，然后重新编译词表：
_cw3hanhua\.venv\Scripts\python.exe _cw3hanhua\tools\build_dict.py --include-other
# 游戏里按 F9 重载，不用重启
```

`_cw3hanhua/_dict/batches/` 下是按批次组织的译稿，`apply_translations.py` 负责把它们
注入 `strings.json`。贡献译文请从 `_cw3hanhua/_dict/story_work/TRANSLATION_BRIEF.md`
读规范，并跑 `audit_translations.py` 确认标记/换行/占位符/首尾空白四项全 0。

### 只改字形、重烘图集

正常情况下**不需要**做这一步 —— 仓库里的 `_font/atlas_zh.png` 和 `atlas_zh.bin` 已经能直接用。
只有想换字体、换字号、或者发现缺字时才要重烘。

字体用的是 **微软雅黑 Bold**，默认值写死在 `_cw3hanhua/tools/bake_font.py`：

```python
DEFAULT_FONT  = r"C:\Windows\Fonts\msyhbd.ttc"   # 雅黑 Bold
DEFAULT_SCALE = 0.80
```

Windows 自带这个文件（`msyhbd.ttc` 是粗体，`msyh.ttc` 是常规体，同一目录下）。
**默认值就是发布图集的参数，不带任何参数跑就能 1:1 复现仓库里的 `_font/`**：

```powershell
cd "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3"
$py = "_cw3hanhua\.venv\Scripts\python.exe"

# 1) 烘字形（产出 _cw3hanhua\_font\font_{16,20,28}.{png,json}）
& $py _cw3hanhua\tools\bake_font.py

# 2) 合并成插件用的图集（产出 _cw3hanhua\_font\atlas_zh.{png,bin}）
& $py _cw3hanhua\tools\build_atlas.py
```

换字体才需要加参数，比如 `--font "C:\Windows\Fonts\msyhl.ttc"`（细体）。

常见参数：

| 参数 | 默认 | 说明 |
|---|---|---|
| `--font` | `C:\Windows\Fonts\msyhbd.ttc` | 字体文件；默认即发布用的雅黑 Bold |
| `--index` | `0` | `.ttc` 里含多款字体（雅黑是 0=YaHei、1=YaHei UI），一般不用改；单字体 `.ttf` 保持 0 |
| `--scale` | `0.80` | 统一缩放；发布值。雅黑 Bold 笔画粗、覆盖率高于原版 Aldrich，所以要缩到 0.80（实际栅格化字号 13/16/22 px） |
| `--scale-y` | 跟随 `--scale` | 纵向单独压；汉字满格而拉丁偏窄时可试 `0.76` |
| `--sizes` | `16,20,28` | 游戏里的三档字号，改这个要同步改插件 |
| `--extra` | 空 | 补烘字集之外的字符（缺字时用） |

`build_atlas.py` 需要读游戏里的原版 `GUI Atlas` 纹理（`CW3_Data` 下），
所以**必须在装好游戏的机器上跑**，而且游戏不能是「GUI Atlas 已被别人改过」的状态。

**改字号/换字体后必须重跑 `build_atlas.py`**，否则插件读到的还是旧图集。
图集是启动时读进内存的（`atlas_zh.bin` 的字形表还带缓存），**换完要重启游戏**才生效
—— 这一点和词表不同，词表按 F9 就能热重载。

缺字时 `_cw3hanhua/tools/check_charset.py` 会打印出缺哪些字，把那些字喂给
`bake_font.py --extra "缺的字"` 再重烘就行。

---

## 八、许可

本项目**分两类授权**：

| 对象 | 许可 |
|---|---|
| 插件与工具源代码（`_cw3hanhua/plugin/`、`_cw3hanhua/tools/`） | **GNU LGPL-2.1**，见 [LICENSE](LICENSE) |
| 译文、词表与文档（`_cw3hanhua/_dict/`、`README.md`、`NOTES.md`） | **CC BY-SA 4.0**，见 [NOTICE](NOTICE) |

### 免责与致谢

- Creeper World 3 及其全部资产版权归 **Knuckle Cracker** 所有。本项目与 Knuckle Cracker
  无任何关联，未获其授权或认可。
- 本项目**不包含**任何游戏本体文件。`atlas_zh.png` 里保留了游戏原版 GUI 图集的像素
  （文字与背景共用一份材质才能落在同一个 draw call 里），该部分的版权归 Knuckle Cracker。
- `_cw3hanhua/_dict/story_raw.json` 是游戏剧情原文，用于翻译对照，版权归 Knuckle Cracker；
  它可以用 `_cw3hanhua/tools/story_raw_check.py` 从你自己的游戏副本重新生成。
- `_cw3hanhua/_font/font_{16,20,28}.*` 是微软雅黑烘焙出的字形位图，字形权利归字体版权方，
  本项目对其**不主张著作权**。仓库内不含任何字体文件。
- 内置 [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx)（LGPL-2.1）及其依赖，
  未做任何修改（`BepInEx.dll` 的 SHA256 与官方发布一致）。重新分发本 Release 时请保留
  [NOTICE](NOTICE) 里的 BepInEx 声明。
- 请先购买正版再使用本补丁。
