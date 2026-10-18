# CW3 汉化项目说明 - 给 AI Agent

这是游戏 **Creeper World 3** 的安装目录。游戏本身不支持中文，用户要汉化它。

## 先读这个

**`_cw3hanhua/NOTES.md` 是项目入口** —— 当前状态、已确认的技术事实、下一步计划、
环境陷阱。读完它再动手。

**只有一份文档。不要新建 md**：新内容写进 `NOTES.md` 的对应章节（文档末尾有维护纪律）。

## 与用户协作的硬规则

- **不要自己判断 Steam 客户端是否在运行 —— 直接问用户。**
- **不要自行启动 Steam 或游戏本体。** 游戏/Steam 的启动与实测由用户操作；
  需要时明确告知用户要做什么。
- 终端使用pwsh.exe，这是ps7，比cmd和系统自带的ps5都好用。
- 除非用户显式命令，否则不要主动进行git提交。

## 快速事实

- 游戏：Unity 5.2.3f1、**32 位**、NGUI UI 框架、版本 2.12 (Steam)
- **BepInEx 5.4.23.5 已安装并启用**（`winhttp.dll` 就位；停用则改名为 `winhttp.dll.off`）
- Python 环境：`_cw3hanhua\.venv\Scripts\python.exe`（UnityPy / dnfile / fontTools）
- 编译插件：`C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe`，
  必须 `/platform:x86`，必须引用游戏自带 `CW3_Data\Managed\` 下的 DLL
- **IL 反汇编统一用 `_cw3hanhua\tools\ilx.exe`**（Mono.Cecil，见 NOTES 第 5.4 节）。
  要读 C# 逻辑用 `_cw3hanhua\tools\ilspy.cmd`
