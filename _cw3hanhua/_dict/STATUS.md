# CW3 词表状态（自动生成，勿手改）

生成时间：2026-10-24 14:40:16    数据：`_dict/strings.json`

## 1. 总览

| 可信度 | 条数 | 已译 | 完成率 |
|---|---:|---:|---:|
| ui | 1559 | 1178 | 75.6% |
| other | 1936 | 1158 | 59.8% |
| **合计** | **3524** | **2336** | **66.3%** |

> `ui` = 几乎一定是显示文本；`other` = 拿不准（单个词/代码片段），先翻 `ui`。

## 2. 按来源

| 来源 | 含义 | 条数 | 已译 |
|---|---|---:|---:|
| both | 资产 + DLL 都有（最该先翻） | 292 | 222 |
| asset | 只在资产里序列化（不走 setter；UILabel 走 ProcessText 补丁，TextMesh 改资产） | 1025 | 1009 |
| dll | 只在 DLL 里（走 ProcessText） | 2207 | 1105 |

## 3. 按命中类名分组（看哪块界面还没动）

| 类 | 条数 | 已译 | 示例 |
|---|---:|---:|---|
| RandomPlanetName | 149 | 1 | `V` |
| CustomImages | 120 | 2 | `Custom10_256` |
| CrplCore | 132 | 40 | `WARNING: Loop stack vars not present at ` |
| VectorLine | 86 | 0 | `VectorLine.SetCamera3D: no camera tagged` |
| (资产) | 1227 | 1156 | `This is a great big long label` |
| GameSpace | 120 | 63 | `GameSpace.OnApplicationQuit` |
| CrplCompiler | 43 | 0 | `: A string literal in an input var defin` |
| CW3SteamAchievements | 43 | 0 | `*********** RESET ALL STATS AND ACHIEVEM` |
| SystemManager | 340 | 311 | `21458256d4614ba281fa0607f803722d` |
| AudioToolkitDemo | 30 | 2 | `Play intro-loop-outro sequence
gatling g` |
| SaveGameManager | 35 | 10 | `Tried to load internal system data and f` |
| SectorManager | 41 | 17 | `SectorManager_Crosshairs` |
| StatsAndAchievements | 24 | 1 | `Received stats and achievements from Ste` |
| SkinTest | 21 | 0 | `Im a textfield
Im the second line
Im the` |
| GameEventWidgetManager | 69 | 51 | `UI Root (2D)` |
| GalaxySpace | 23 | 6 | `Create AudioController` |
| CommandCenter | 62 | 45 | `  [ffffff]Fully Upgraded![-]` |
| ColonialSpaceManager | 87 | 71 | `UI Root (2D)` |
| <>__AnonType0`15 | 15 | 0 | `, a = ` |
| EditMapGUI | 158 | 145 | `.crpl
# Created on: ` |
| MainMenu | 40 | 28 | `Using base game data dir: ` |
| AudioController | 12 | 0 | `AudioObject prefab does not have the Poo` |
| InputManager | 23 | 13 | `Creeper World 3 Keys
` |
| FileManager | 10 | 0 | `steam-uninitialized` |
| MissionData | 10 | 0 | `MissionData[GUID=` |

## 4. 译文缺字检查

✓ 全部译文字符都在烘焙范围内

## 5. 最长的未译条目（优先处理长文本）

- `You can't place widgets on a layer different than the UIPanel that manages them.\nIf you want to move widgets to a differ`
- `SendMessage is bugged when you try to pass 'null' in the parameter field. It behaves as if no parameter was specified.`
- `VectorLine.SetCamera3D: no camera tagged "Main Camera" found. Please call SetCamera3D with a specific camera instead.`
- `: A string literal in an input var definition must have a quote at the beginning and at the end: `
- `" would create a circular dependency.  Please check your placement dependencies to resolve this.`
- `Exception caught in UVAnimation.SetAnim(). Make sure you have re-built your atlases!\nException: `
- `" is not associated with a SpriteManager, and can therefore not be included in the atlas build.`
- `". Please verify the spelling and capitalization of the name, including any extra spaces, etc.`
- `VectorLine.MakeLine: Must call SetLineParameters before using MakeLine with these parameters`
- `: Improperly formatted input var.  There must be a name for the var and a default value.: `
- `" has not been assigned a material, and can therefore not be included in the atlas build.`
- `: In an input var a dot is only allowed in string literals and floating point numbers: `
- `UIRoot should not be active at the same time as UIOrthoCamera. Disabling UIOrthoCamera.`
- `VectorLine.BytesToVector3Array: Incorrect input byte length...must be a multiple of 12`
- `AudioObject prefab does not have the PoolableObject component. Pooling will not work.`
- `VectorLine.BytesToVector2Array: Incorrect input byte length...must be a multiple of 8`
- `VectorLine.SetTextureScale: You must call SetCamera3D before calling SetTextureScale`
- `: A 'break' must be found inside a 'while/endwhile block' or if 'do/loop' block`
- `*********** RESET ALL STATS AND ACHIEVEMENTS!!!! ***************************`
- `Line unknown: Every 'while/endwhile' block must contain a 'repeat' statement`
- `UISlider expected to find a foreground object or a box collider to work with`
- `VectorManager: object number exceeds array length in VisibilityStaticRemove`
- `RegisteredComponentController error: Tried to register same instance twice`
- `RegisteredComponent: Awake() / OnDestroy() not correctly called. Object: `
- `RegisteredComponentController error: Tried to unregister unknown instance`

