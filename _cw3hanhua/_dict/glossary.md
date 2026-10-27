# CW3 汉化 · 术语表

**用途**：翻译前先在这里定名；`tools/dict_stats.py` 与 `tools/check_charset.py` 都会读它。
**纪律**：一个英文词只对应一个中文译名；改译名必须在这里改，不要在译文里随手换。

| 英文 | 中文 | 备注 |
|---|---|---|
| Creeper | Creeper | **保留英文**（用户 2026-10-18 定：核心敌人名不译） |
| creeper (小写, 泛指) | Creeper | 同上，大小写统一成首字母大写 |
| Anti-Creeper | Anti-Creeper | 同上，保留英文 |
| AC | AC | Anti-Creeper 的缩写，**同样保留英文**；`AC Production` → `AC 产量` |
| Emitter | 发射器 | |
| Anti-Emitter | 反发射器 | |
| Inhibitor | 抑制器 | |
| Nullifier | 灭除器 | 摧毁发射器的武器 |
| Collector | 收集器 | |
| Relay | 中继器 | |
| Reactor | 反应堆 | |
| Shield / Shield Generator | 护盾 / 护盾发生器 | |
| Power Zone | 供能区 | |
| Cannon | 脉冲炮 | **统一口径**：建造面板按钮上的 `Cannon` 就是 `Pulse Cannon` 那个单位，一律译「脉冲炮」，不要写成「炮」（2026-10-23 用户报：一会儿「炮」一会儿「脉冲炮」） |
| Cannons（Thor 面板标题） | 主炮 | 索尔面板顶部标题，指它挂的主炮；`FIRE MAIN CANNONS` → `主炮开火` 与之对齐 |
| Mortar | 迫击炮 | |
| Bomber | 轰炸机 | |
| Sprayer | 喷吐者 | |
| Sniper | 狙击手 | |
| Strafer | 俯冲机 | |
| Terp | 挖掘机 | |
| Bertha | 贝莎炮 | 大炮巨炮，玩家俗称 |
| Forge | 锻造厂 | |
| Guppy | 补给船 | |
| Thor | 索尔 | 轨道武器，音译 |
| Numen | 符文方尖碑 | |
| Ore | 矿 | |
| Ore Mine | 矿场 | |
| Aether | 以太 | |
| Digitalis | 魔苋 | 紫色蔓生植物 |
| Runner | 空降兵 | 陆行敌人 |
| Spore / Spore Tower | 孢子 / 孢子塔 | |
| Totem | 图腾 | |
| Siphon | 虹吸器 | |
| Terraform | 地形改造 | `Terraform Button` → `地形改造按钮` |
| Soylent | 营养液 | 游戏内特殊资源 |
| Pulse Cannon | 脉冲炮 | |
| Air Exclusion Tower | 禁空塔 | |
| Beam | 光束 | `Build Beam` → `建造光束` |
| T-Brush | 地形笔刷 | |
| D-Growth | 魔苋生长 | Digitalis 生长的简称 |
| CN | CN | 不译（游戏内缩写，见下"保留不译"） |
| Packet | 能量包 | |
| Pack | 包 | `Ore Pack` → `矿物包` |
| Energy | 能量 | |
| Ammo | 弹药 | |
| Artifact | 圣物 | |
| Sector | 星区 | `Alpha Sector` → `阿尔法星区` |
| Planet | 星球 | |
| Mission | 任务 | |
| World / World Editor | 世界 / 世界编辑器 | |
| Map | 地图 | `本关地图` 与 `世界` 是两回事：map = 地图文件 |
| Launch | 发射 | |
| Resupply | 补给 | |
| Arm / Disarm | 武装 / 解除武装 | 单位面板 V 形按钮的两种状态（`GameSpace.UpdateUnitPanel`）。**`label.text` 必须保持英文 `"ARM"`/`"DISARM"`**，`CommandCenter.DisarmSelectedUnits` 拿它做判定（NOTES 3.3），中文只由 ProcessText 临时替换 |
| Formation Move | 编队移动 | |
| DMD | 动态地图 / DMD | 保留 DMD 缩写，首次出现给全称 |

## 专有名词

| 英文 | 中文 | 备注 |
|---|---|---|
| Prospector Zone | 探矿者星区 | |
| Tormented Space | 折磨空间 | |
| Colonial Space | 殖民空间 | |
| Alpha Sector | 阿尔法星区 | |
| Arc Eternal | 永恒之弧 | |
| Credits | 制作组 | ★ 字宽实测：英文 `Credits`=3.82 em（Aldrich），"制作名单"=4.00 em（105%，溢出），"制作组"=3.00 em（78%，安全），见 NOTES 3.3 |
| Knuckle Cracker | Knuckle Cracker | 开发商名，保留英文 |
| Odin | 奥丁 | |
| Digitalis | 魔苋 | |
| Skarsgard Abraxis | 斯卡斯加德·阿布拉克斯 | 剧情人物 |
| Platius | 普拉提乌斯 | 剧情人物 |
| Aliana | 阿利安娜 | 剧情人物，Skars 的女儿 |
| Lia | 莉娅 | Aliana 的化名，**必须与「阿利安娜」区分**，不要统一 |
| Nexus | 枢纽 | **不要写成「内克苏斯」**（旧批量译文里出现过，已回归「枢纽」） |
| Loki | 洛基 | |
| Loki Hive | 洛基巢穴 | 单说 `Hive` 时作「巢穴」 |
| Imperator | 统领 | `Loki Imperator` → `洛基统领` |
| Arc / Arc Eternal | 永恒之弧 | 单说 `the Arc` 也作「永恒之弧」 |
| Anomaly | 异常 | `the anomaly` 指 Aliana 一方 |
| Prism Ship | 棱镜飞船 | |
| Enhanced Totem | 强化图腾 | |
| Rift Space | 裂隙空间 | |
| Missile Silo | 导弹发射井 | 单说 `silo` 作「发射井」 |
| Dark Beam | 暗黑光束 | |
| automaton | 自动机械 | 指 Skars 的机械身体 |
| CRPL | CRPL | **保留英文**。全名 `Creeper Reverse Polish Language`，是 CW3 的脚本语言；玩家不拼读它，四字母原样最稳（3317/3318/3319 一直如此） |
| Reverse Polish Language | 逆波兰式语言 | ★ **不要译成「逆波兰语言」**——中文里「波兰语言」是「波兰语」这个现成词，会被读成「一种波兰的语言」。`Polish` 在这里指**国籍**：1924 年波兰逻辑学家 Jan Łukasiewicz 发明了前缀的波兰表示法，运算符挪到后面就成了「逆波兰表示法」（RPN，中文标准名也叫「后缀表达式」）。全表只有 index 3316 出现一次，2026-10-27 用户定 |

## 排版：空格（2026-10-24 用户定）

**中文与拉丁字母 / 数字之间一律不加半角空格**。专有名词两侧也一样：

- ✅ `他看见Creeper了`　`30秒后发射孢子`　`按C克隆上次编辑的单位`　`等级:0`
- ❌ `他看见 Creeper 了`　`30 秒后发射孢子`　`等级: 0`

历史：早期批次按「盘古之白」在外语词两侧加了空格，`story_normalize.py` 换词时也没清，
于是在 `08_campaign.out.json` 留下 8 条 `被 魔苋 包围`。这些空格在 NGUI
`UIFont.WrapText` 折行时会被**原地改写成 `\n`**（`if (num > 0 && s[num] == ' ') s[num] = '\n';`），
看起来就像凭空多出一个回车 —— 这就是用户 2026-10-24 报的现象。删除用
`tools/fix_spaces.py`（规则同样适用于以后的新译文）。

**半角标点不算汉字，也要删**（用户 2026-10-24 补充「也一并清掉」）。
判邻字符时把 `` : ' " ( ) [ ] { } < > / \ | - _ + = * & ^ % $ # @ ! ? . , ; ~ ``
一律当**透明**跳过去，再往外找真正的邻居：

- ✅ `按'[ffff00]C[-]'克隆上次编辑的单位。`　`在星球上按住'M'可拖动它。`　`(PZ)挖掘机是关键`
- ✅ `（仅允许字母、数字、_和-）`　`麻烦列车01-教程`　`20-256之间`　`-1=不限制`
- ❌ `按 '[ffff00]C[-]' 克隆`　`(PZ) 挖掘机`　`麻烦列车 01 - 教程`　`20 - 256 之间`

`X - Y` 这种两边都被空格撑开的符号结构，整体塌成 `X-Y`（不吃多空格、不碰纯拉丁）。

仍然要保留空格的几类，别误删：

1. **行尾拼接空白**：`PACKET SPEED.  ` 这种后面还要接数字的前缀（见下节）。
2. **多空格列对齐**：选择题选项 `A：45 J     B：10800 J`。
3. **纯拉丁文本内部**：`Knuckle Cracker, LLC`、`Finn M-K`、URL、`Creeper World`。
4. **程序拼接用的前缀/后缀空白**：`'  建造: '`、`'贝莎炮自动瞄准 '`、`'点击或 \''` 这类
   —— 译文本身不完整，前后空白是给 DLL 里的 `+` 拼接留位，删了会和数字/后续文字粘连。

⚠ **数字列表的逗号后空格也一样删掉**：`日期：1014, 0201, 第144循环历` 现在写成
`日期：1014, 0201,第144循环历`（`EpilogueManager.FixedUpdate` 的结局日期行）。
早期为它开过 `RX_KEEP = (?<=[0-9]),[ \t]` 白名单，理由是「英文书写格式」，
但用户的原则是「不加空格」，白名单已删除。**判定标准统一为字符类别，不按英文原样照顾**。

统一由 `tools/fix_spaces.py` 执行（`--write` 才写回；重跑应报 0 条改动 ⇒ 幂等）。
只删**单个** U+0020，且删之前先看跳过颜色标记/半角标点后两侧真正的字符类别。

**拼接前缀的尾空格要删（2026-10-24 补充）**：`'Collected: '` / `'Amt: '` / `'FPS: '` /
`'Energy: '` 这类 en 自带尾空格的标签，译文写成 `'已收集：'` 就**不要**再缀那个半角空格 ——
中文全角标点自带间隔，缀上去拼出来就是 `已收集： 指挥节点`。
用 `tools/fix_tail_space.py` 清理（规则 `([\u3000-\u303f\uff01-\uff60\u2014\u2026])[ ]+$`，
只删尾、前导 `'  地形：'` 保留；纯拉丁结尾如 `'Knuckle Cracker, LLC'` 不受影响）。
但上面第 1/4 条那种**以拉丁字母或句点结尾**的拼接前缀（`'能量包速度.  '`、`'贝莎炮自动瞄准 '`）
仍按原样保留尾空格。

**拼接串里的词条要以「能被匹配的首字符」开头**：插件分段器的索引只收
`字母 / 空格 / [` 起头的键，**以 `\n` 起头的键是死键**（`'\nAdditional Number: '` 永远不吃）。
遇到这种，除了原键，**再补一条去掉前导 `\n` 的同义键**。见 `NOTES.md` 4.14。

## 保留不译

URL、`steam_appid`、文件名（`.cw3` / `.xml` / `.png`）、版本号、玩家名、Steam 成就 API 名、
`[ffffd0]` 这类富文本颜色标记、`\n` 换行、以及所有 `{}` 占位符。

另外这两类**按键标签必须整串保留英文**（它们是玩家要在键盘上找的键位）：

- `SHIFT-E` / `SHIFT-End` / `CTRL-C` 这类组合键
- `PACKET SPEED.  ` 结尾带空格的拼接前缀 —— **尾部空格和句点都要保留**，否则会跟后面的数字粘连

## 全大写枚举名（`TECH_TYPE`，20 项，会真上屏）

`TechArtifactManager.TECH_TYPE.ToString()` 的结果被拼进单位标题
（`UnitManager.GetUnitGameName()` → `"Tech: " + techType`），所以这 20 个全大写串
**必须与上表正常名逐条一致**：

`COLLECTOR`=收集器 / `RELAY`=中继器 / `REACTOR`=反应堆 / `SHIELD`=护盾 /
`OREMINE`=矿场 / `SIPHON`=虹吸器 / `TERP`=挖掘机 / `GUPPY`=补给船 / `CANNON`=脉冲炮 /
`MORTAR`=迫击炮 / `STRAFER`=俯冲机 / `BOMBER`=轰炸机 / `NULLIFIER`=灭除器 /
`SPRAYER`=喷吐者 / `BEAM`=光束 / `SNIPER`=狙击手 / `FORGE`=锻造厂 / `BERTHA`=贝莎炮 /
`THOR`=索尔 / `COMMANDNODE`=指挥节点

★ 但词表里同一档（全大写、`grade=other`）还混着大量**不上屏**的 CRPL 内部名
（`RESOURCETYPE` / `FIRERATE` / `STAT_*` …）—— 那些保持空 `zh`，
**不要看到全大写就顺手填**。
