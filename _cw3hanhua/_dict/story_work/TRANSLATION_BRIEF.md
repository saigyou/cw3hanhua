# 剧情文本翻译规范（CW3 汉化）

你要做的是：把一个批次里的**英文游戏台词**逐条译成简体中文，并按指定格式写出 JSON 文件。
这是科幻即时战略游戏 *Creeper World 3*（时之弧战役）的剧情对白，说话人只有两类：
**指挥官斯卡斯加德·阿布拉克斯**（玩家，编号 `0`，坚毅、沉郁、偶尔自嘲式幽默）与
**莉娅**（舰载 AI/守护者，编号 `1`，温和、理性、克制）。
其余编号是各关卡的配角（如 CAIN、船员），语气按文本自行判断。

## 硬性规则

1. **只输出 JSON 文件**，不要在文件里写解释、不要加 markdown 代码块围栏。
2. 条目 **id 必须与输入一致**，不能增加、遗漏或合并条目。一条英文 = 一条中文。
3. **保留换行结构**：原文里 `\n` 的个数、空行（连续两个 `\n`）必须与译文一致。
4. 标点用中文全角：`……`（省略号）、`，` `。` `？` `！` `；` `：`；
   原文的引号 `'xxx'` / `"xxx"` 译成 `「xxx」`。
5. 英文双空格分隔句子的习惯**不必保留**，中文直接连写即可。
5b. **中文与拉丁字母/数字之间不加空格**（见 `_dict/glossary.md` 的「排版：空格」一节）。
   所以 `他看见 Creeper 了` 要写成 `他看见Creeper了`，`30 秒` 写成 `30秒`，`等级: 0` → `等级:0`。
   半角标点 `` : ' " ( ) [ ] - = , `` 等夹在中间**也当没有**，外侧的空格照样删：
   `按 '[ffff00]C[-]' 克隆` → `按'[ffff00]C[-]'克隆`，`(PZ) 挖掘机` → `(PZ)挖掘机`，
   `麻烦列车 01 - 教程` → `麻烦列车01-教程`。
   例外只有三类：**行尾拼接空白**（`PACKET SPEED.  ` 这种后面还要接数字的前缀）、
   **多空格做的列对齐**（`A：45 J     B：10800 J`）、
   以及**纯拉丁内部**（`Knuckle Cracker, LLC`、`Finn M-K`、URL）。
   连 `1014, 0201, 第144循环历` 这种数字列表里的分隔空格也要删。
   **拼接标签的尾空格也删**：en 是 `'Collected: '` / `'Amt: '` / `'FPS: '` 时，
   译成 `'已收集：'` / `'数量：'` / `'帧率：'` 就好，别为对齐 en 再缀一个半角空格
   （拼出来会是 `已收集： 指挥节点`）。工具：`tools/fix_tail_space.py`。
   ⚠ NGUI 的 `UIFont.WrapText` 会把折行处的行尾空格**原地改成 `\n`**，
   所以词中间的游离空格会在界面上变成莫名其妙的换行。
6. 下列内容保持英文原样，不要翻译：
   - `Creeper`、`Anti-Creeper`、`AC`（核心敌人名，保留英文）
   - `CAIN`、`Odin`、`Knuckle Cracker`、`Platius`（见下表已给中文的按表）
   - 数字、单位缩写、URL、文件名（`.cw3` `.crpl` `.png`）、按键名（`WASD` `SPACE` `TAB` `SHIFT` `ESC`）
   - 拉丁语/外语引文（例：`Fortuna favet fatuis`）
   - `[ffffd0]` 这类富文本颜色标记
7. 方括号里的系统提示（例 `[Scanning System...]`、`[Gel maintenance in process]`）
   **保留方括号**，括号内文字译成中文。
8. 无法解读的乱码串（例 `Pzworpf. ...qf. .. [^flpf...=(`）**原样搬运，不译**。
9. 不要扩写、不要漏句、不要加译注。译文长度大致与原文相当（中文比英文短是正常的）。

## 术语（必须一致）

`_cw3hanhua/_dict/glossary.md` 里有完整表，常用的：

| 英文 | 中文 |
|---|---|
| Creeper / Anti-Creeper / AC | 保留英文 |
| Emitter | 发射器 |
| Anti-Emitter | 反发射器 |
| Inhibitor | 抑制器（`Warp Inhibitor` → 跃迁抑制器） |
| Nullifier | 灭除器 |
| Collector / Relay / Reactor | 收集器 / 中继器 / 反应堆 |
| Shield / Shield Generator | 护盾 / 护盾发生器 |
| Mortar / Bomber / Sprayer / Sniper / Strafer | 迫击炮 / 轰炸机 / 喷吐者 / 狙击手 / 俯冲机 |
| Terp | 挖掘机 |
| Bertha | 贝莎炮 |
| Forge / Guppy / Thor | 锻造厂 / 补给船 / 索尔 |
| Numen / Ore / Ore Mine | 符文方尖碑 / 矿 / 矿场 |
| Runner | 空降兵 |
| Spore / Spore Tower | 孢子 / 孢子塔 |
| Totem / Siphon | 图腾 / 虹吸器 |
| Artifact | 圣物 |
| Sector / Planet / World | 星区 / 星球 / 世界 |
| Launch / Resupply | 发射 / 补给 |
| Terraform | 地形改造 |
| Rift Space | 裂隙空间 |
| Command Node | 指挥节点（`Command Node Artifical Intelligence` → 指挥节点人工智能） |
| ground activities / ground operations | 地面行动 / 地面作战 |
| suspended animation | 休眠 |
| singularity | 奇点 |
| warp | 跃迁 |
| shield key | 护盾钥匙 |

## 专有名词表（必须用这些译名）

| 英文 | 中文 |
|---|---|
| Skarsgard Abraxis / Skars | 斯卡斯加德·阿布拉克斯 / 斯卡斯 |
| Lia | 莉娅 |
| Platius | 普拉提乌斯 |
| Dax / Varro / Aliana | 达克斯 / 瓦罗 / 阿利安娜 |
| CAIN | CAIN |
| Seloi / Loki | 塞洛伊 / 洛基 |
| Inceptus | 因塞普图斯 |
| Tempus | 坦普斯 |
| Carcere | 卡塞雷 |
| Meso | 梅索 |
| Navox | 纳沃克斯 |
| Starsync | 星同步 |
| Telos | 泰洛斯 |
| Far York | 远约克 |
| Arca / Chanson / Choix / Crosslaw | 阿尔卡 / 尚松 / 舒瓦 / 克罗斯劳 |
| Defi / Farbor / Flick / Jojo | 德菲 / 法博 / 弗里克 / 乔乔 |
| Krig / Lemal / Mistet / Ormos | 克里格 / 莱马尔 / 米斯特 / 奥尔莫斯 |
| Otrav / Ruine / Seedet | 奥特拉夫 / 鲁因 / 西德特 |
| Tiplex / Vapen | 蒂普莱克斯 / 瓦彭 |

表里没有的**地名/人名**：按发音音译成 2~5 个常用汉字（例 `Xeniveral` → 泽尼维拉尔），
同一个词在同一批次内保持一致。**不要**自创意译。

## 风格

- 台词像电影字幕：短句、有节奏，不要书面语腔。
- 莉娅：温和克制，偶尔流露情感；不用感叹号堆砌。
- 斯卡斯：军人语气，沉痛处短促，怀念处略舒缓。
- 系统/机械文本：简短、名词化（例 `[正在扫描星系……]`）。
- 允许把 `...` 译成 `……`，把 `--` 译成 `——`。

## 参考样例（已定稿，照这个水准写）

输入：
```
[188] (1) Skars... wake up.\nSKARS!....\n\nCOMMANDER SKARSGARD ABRAXIS!
[191] (0) I don't know any 'Lia'.  The last thing I remember is Platius laughing and saying, 'Fortuna favet fatuis'.
[198] (0) My Gods....\n...\nWHY?  ANSWER ME, WHY???
[376] (1) [Gel maintenance in process]
```
输出：
```
{"id": 188, "zh": "斯卡斯……醒醒。\n斯卡斯！……\n\n指挥官斯卡斯加德·阿布拉克斯！"}
{"id": 191, "zh": "我不认识什么「莉娅」。我记得的最后一幕，是普拉提乌斯大笑着说了一句「Fortuna favet fatuis」。"}
{"id": 198, "zh": "我的神啊……\n……\n为什么？回答我，为什么？！"}
{"id": 376, "zh": "[凝胶维护进行中]"}
```

## 输出格式

写到任务指定的 `.out.json`，结构如下（`items` 顺序不限，但 id 要齐）：

```json
{"batch": "<批次名>", "items": [{"id": 188, "zh": "……"}, {"id": 189, "zh": "……"}]}
```
