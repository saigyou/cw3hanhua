#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""准备全量翻译工作集：分出「不译」与「待译」，切块输出到 _dict/work/。

不译规则（可复现）：
  R1  纯 ASCII 标识符/PascalCase/全大写单词+下划线（如 GalaxyNavMapOpen / STAT_AOO / CN1Button）
  R2  文件名与扩展名（.cw3 / .crpl / -auto.cw3）
  R3  32 位十六进制哈希、纯数字/坐标
  R4  RandomPlanetName 的音节碎片（程序化星球名，属专名）
  R5  Custom<数字>(_256) 资源名
  R6  第三方/NGUI/音频库的 Debug.Log* 开发者消息（类在黑名单里且不含游戏语义）
"""
import json, os, re, sys, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict")
WORK = os.path.join(DICT, "work")
P = os.path.join(DICT, "strings.json")
try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

if not os.path.isdir(WORK):
    os.makedirs(WORK)

d = json.load(open(P, encoding="utf-8"))
items = d["strings"]
un = [x for x in items if not x.get("zh")]

REXP = [
    (re.compile(r"^[A-Za-z_][A-Za-z0-9_]*(`\d+)?$"), "R1-id"),
    (re.compile(r"^[A-Z][A-Z0-9_]{2,}$"), "R1-id"),
    (re.compile(r"^[A-Za-z0-9_@-]+\.(cw3|crpl|xml|png|txt|dll|exe)$", re.I), "R2-file"),
    (re.compile(r"^[0-9a-f]{32}$"), "R3-hash"),
    (re.compile(r"^[\d\s.,x:+/#%-]+$"), "R3-num"),
    (re.compile(r"^Custom\d+(_256)?$"), "R5-asset"),
    (re.compile(r"^(Alpha|Beta|Gamma)(\d+)?$"), "R4-name"),
]

def skip_reason(x):
    en = x["en"]
    cls = x.get("cls") or []
    if "RandomPlanetName" in cls:
        return "R4-name"
    for rx, why in REXP:
        if rx.match(en):
            return why
    return None

to_do, skipped = [], collections.defaultdict(list)
for x in un:
    r = skip_reason(x)
    if r:
        skipped[r].append(x)
    else:
        to_do.append(x)

print("未译 %d → 待译 %d，不译 %d" % (len(un), len(to_do), sum(len(v) for v in skipped.values())))
for k, v in sorted(skipped.items()):
    print("  %-8s %d" % (k, len(v)))
print("待译字符数 %d" % sum(len(x["en"]) for x in to_do))

# 切块：按 en 长度均衡，块内尽量同类名相邻（便于术语一致）
to_do.sort(key=lambda x: ((x.get("cls") or ["(asset)"])[0], x["en"]))
NCHUNK = 18
total = sum(len(x["en"]) for x in to_do)
target = total / NCHUNK
chunks, cur, curlen = [], [], 0
for x in to_do:
    cur.append(x)
    curlen += len(x["en"])
    if curlen >= target:
        chunks.append(cur)
        cur, curlen = [], 0
if cur:
    chunks.append(cur)

for n, ch in enumerate(chunks, 1):
    name = "full_%02d" % n
    payload = {
        "batch": name,
        "count": len(ch),
        "items": [{"i": i, "cls": ",".join(x.get("cls") or ["(asset)"]), "en": x["en"]}
                  for i, x in enumerate(ch)],
    }
    with open(os.path.join(WORK, name + ".src.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(payload, f, ensure_ascii=False, indent=1)
    print("%s: %d 条 / %d 字符" % (name, len(ch), sum(len(x["en"]) for x in ch)))

with open(os.path.join(WORK, "_skipped.json"), "w", encoding="utf-8", newline="\n") as f:
    json.dump({k: [dict(cls=",".join(x.get("cls") or ["(asset)"]), en=x["en"]) for x in v]
               for k, v in skipped.items()}, f, ensure_ascii=False, indent=1)
print("→ %s" % WORK)
