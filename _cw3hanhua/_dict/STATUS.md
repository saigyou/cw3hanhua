# CW3 词表状态（自动生成，勿手改）

生成时间：2026-10-18 13:05:01    数据：`_dict/strings.json`

## 1. 总览

| 可信度 | 条数 | 已译 | 完成率 |
|---|---:|---:|---:|
| ui | 1261 | 153 | 12.1% |
| other | 1093 | 0 | 0.0% |
| **合计** | **2383** | **153** | **6.4%** |

> `ui` = 几乎一定是显示文本；`other` = 拿不准（单个词/代码片段），先翻 `ui`。

## 2. 按来源

| 来源 | 含义 | 条数 | 已译 |
|---|---|---:|---:|
| both | 资产 + DLL 都有（最该先翻） | 297 | 153 |
| asset | 只在资产里序列化（不走 setter，靠 sweep） | 174 | 0 |
| dll | 只在 DLL 里（走 set_text） | 1912 | 0 |

## 3. 按命中类名分组（看哪块界面还没动）

| 类 | 条数 | 已译 | 示例 |
|---|---:|---:|---|
| (资产) | 376 | 133 | `[ffffe0]Precursor worlds.[ffffff]
Before` |
| WorldMachineManager | 190 | 2 | `Unit Limits.  -1 mean unlimited` |
| EditMapGUI | 158 | 7 | `To Edit, you must set a default applicat` |

## 4. 译文缺字检查

✓ 全部译文字符都在烘焙范围内

## 5. 最长的未译条目（优先处理长文本）

- `To Edit, you must set a default application to open '.crpl' files.\nOn Windows, select an editor (like Notepad.exe, or th`
- `Select a terrain level, then select a texture below from the palette.\nClick the 'APPLY IMAGE TO TERRAIN' button to assig`
- `Resizing the map will increase or decrease the amount of land.\nIf you decrease either dimension, terrain/units will get `

