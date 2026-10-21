# tools —— 工具链

给本项目做 IL 取证、字形烘焙、图集构建、词表编译用。**除了 `build_plugin.cmd`
往 `BepInEx\plugins\` 写两个 DLL 之外，其余工具都不修改游戏文件。**

## 文件总览

| 文件 | 用途 | 文档 |
|---|---|---|
| `build_plugin.cmd` | ★ **唯一编译入口**，一次编出 `CW3Hanhua.dll` + `CW3HanhuaText.dll` | NOTES 5.2 |
| `ilx.exe` / `ilx\ilx.cs` | 基于 **Mono.Cecil 0.10.4** 的 IL 检查器（源码 + 编译产物） | 下文 |
| `Mono.Cecil.dll` | 从 `BepInEx\core\` 复制，MIT。让 tools 目录可整体搬走 | — |
| `build-ilx.ps1` | 改过 `ilx.cs` 后重新编译 | — |
| `ilx.cmd` / `ilspy.cmd` | 命令行包装（`ilx ...` / `ilspy ...`） | 下文 |
| `ilspycmd\` | ILSpy 命令行版 7.2.1（net6.0），反编译成 C# | 下文 |
| `bake_font.py` | 烘字形 PNG+FNT+JSON，`gb2312_chars()` / `charset_for()` 供别人复用 | NOTES 4.1 |
| `build_atlas.py` | ★ 合并图集 4096×8192 生成器 → `_font\atlas_zh.{png,bin}`（`--write-json` 才额外出调试 JSON） | NOTES 4.4 |
| `check_charset.py` | 查词表有没有超出已烘字集（**查三套字号的交集**） | NOTES 4.1 |
| `collect_ui_strings.py` | 静态全量收集候选字符串（IL `ldstr` + 资产文本） | NOTES 4.2 |
| `build_dict.py` | 把 `strings.json` 编译成 `zh_cn.dict` | NOTES 4.2 |
| `apply_translations.py` | 把 `batches/*.json` 译文注入 `strings.json` | NOTES 4.2 |
| `dict_stats.py` | 覆盖率报告 → `_dict\STATUS.md` | NOTES 4.2 |
| `audit_translations.py` | ★ 全表审计：en/zh 的 `[标记]`/换行/占位符/首尾空白，四项全 0 才算过 | NOTES 4.5 |
| `prep_batches.py` | 未译项分流（规则化不译桶 + 待译）后切批，输出 `_dict\work\full_NN.src.json` | NOTES 4.5 |
| `merge_work.py` | `work\full_NN.{src,zh}.json` → `batches\full_NN.json` | NOTES 4.5 |
| `check_batch.py` | 单批译稿校验，不过 0 不许入库 | NOTES 4.5 |
| `pack_skips.py` | 把译稿里标 `@` 的「人工判定不译」固化到 `_dict\skipped_manual.json` | NOTES 4.5 |
| `scan_uilabel.py` | 只读盘点全部资产里的 UILabel（`mText`）→ `uilabel_inventory.json` | NOTES 3 |
| `scan_textmesh.py` | 只读盘点全部资产里的 TextMesh（class 102）→ `textmesh_inventory.json` | NOTES 3 |
| `diff_bake.py` | ★ 烘焙后回归：重扫现役资产 vs `uilabel_inventory.json` 基线，确认没波及任何 UILabel | NOTES 4.3 |
| `uilabel_inventory.json` | **烘焙前**的 UILabel 基线快照（1029 条有文本），`diff_bake.py` 的对比依据 | — |
| `textmesh_inventory.json` | **烘焙前**的 TextMesh 快照（英文原文），改资产前的参照 | — |

★ 三个 `scan_*.py` / `diff_bake.py` 是 2026-10-21 从 `_diag\` 挪进来的：它们本身是只读
诊断脚本，但 `*_inventory.json` 是**烘焙前的历史快照、不可再生**（游戏资产如今已烘成中文，
重跑只会得到现役状态并覆盖基线）。要重跑任一 scan，先把对应 JSON 备份走。

Python 脚本一律用 `..\.venv\Scripts\python.exe` 跑（装了 UnityPy / dnfile / dncil /
fontTools / Pillow）。

## ilx 用法

```powershell
$t = "C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3\_cw3hanhua\tools\ilx.cmd"

& $t dump UILabel set_text                 # 精读 IL（多个方法名可并列）
& $t dump UILabel                         # 只列该类型的全部方法 + token
& $t callers set_enabled                  # 全程序集：谁调用了它
& $t callers --list --type UILabel set_text   # 只要汇总，且限定被调类型
& $t fields enabled                       # 谁直接读写字段（ldfld/stfld/ldsfld/...）
& $t find MainMenu                        # 按子串搜类型 / 方法 / 字段
& $t stats                                # 全部方法：token / code 大小 / 指令数 / EH 数
```

通用选项：`--dll <路径>` 换程序集，`--type <类型>` 限制声明类型。

输出里的几个约定：

- `fld:mText` —— 字段引用
- `STR:'...'` —— 字符串字面量（`\n` 等已转义）
- `type:Enumerator` —— 类型 token
- 跳转目标一律是**方法内 IL 偏移**（4 位十六进制）

## ilspy 用法

```powershell
& "C:\...\_cw3hanhua\tools\ilspy.cmd" "C:\...\CW3_Data\Managed\Assembly-CSharp.dll" -t UILabel
& "...\ilspy.cmd" "...\Assembly-CSharp.dll" -t UIFont -il     # 出 IL 而不是 C#
```

反编译成 C# 用来理解逻辑，比读 IL 快一个数量级；**IL 级取证仍以 ilx 为准**。

## 环境要求

- `ilx.exe`：.NET Framework 2.0+（Windows 自带即可）
- `ilspycmd`：需要 .NET 6 运行时（本机已有 6.0.26）
- 若 `ilspycmd\` 丢失：沙箱内 TLS 拿不到凭证，`dotnet tool install` 会失败；
  用 Python 下载 nupkg 解压即可：
  `https://api.nuget.org/v3-flatcontainer/ilspycmd/7.2.1.6856/ilspycmd.7.2.1.6856.nupkg`
