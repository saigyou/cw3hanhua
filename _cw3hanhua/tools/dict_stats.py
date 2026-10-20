#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
词表覆盖率报告 —— "翻译了多少、还差多少、差的是哪几块"。

输出（默认也写一份到 _dict/STATUS.md）：
    - ui / other 两档各自的完成率
    - 按来源（asset / both / dll）拆分
    - 按命中的类名分组，看哪块界面还没动
    - 缺字检查（调用 bake_font.charset_for，与 check_charset 同一口径）

用法：
    .venv\\Scripts\\python.exe tools\\dict_stats.py
    .venv\\Scripts\\python.exe tools\\dict_stats.py --top 40 --no-write
"""
import argparse
import json
import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
import bake_font          # noqa: E402

DICT = os.path.join(ROOT, "_dict")
STRINGS = os.path.join(DICT, "strings.json")
STATUS = os.path.join(DICT, "STATUS.md")


def load():
    with open(STRINGS, encoding="utf-8") as f:
        return json.load(f)


def hz_len(s):
    return len(s)


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    ap = argparse.ArgumentParser()
    ap.add_argument("--top", type=int, default=25, help="每个榜单列多少条")
    ap.add_argument("--no-write", action="store_true", help="不写 STATUS.md")
    args = ap.parse_args()

    data = load()
    items = data["strings"]
    bake_font.load_baked()
    baked = bake_font.charset_for(bake_font.DEFAULT_SIZES)

    lines = []

    def out(s=""):
        print(s)
        lines.append(s)

    out("# CW3 词表状态（自动生成，勿手改）")
    out()
    out("生成时间：%s    数据：`_dict/strings.json`" % data.get("generated", "?"))
    out()

    # ---------------- 总览
    by_grade = defaultdict(lambda: [0, 0])
    for it in items:
        g = it.get("grade", "?")
        by_grade[g][0] += 1
        if it.get("zh"):
            by_grade[g][1] += 1

    out("## 1. 总览")
    out()
    out("| 可信度 | 条数 | 已译 | 完成率 |")
    out("|---|---:|---:|---:|")
    for g in ("ui", "other"):
        if g in by_grade:
            n, done = by_grade[g]
            out("| %s | %d | %d | %.1f%% |" % (g, n, done, 100.0 * done / n if n else 0))
    tot = sum(v[0] for v in by_grade.values())
    done = sum(v[1] for v in by_grade.values())
    out("| **合计** | **%d** | **%d** | **%.1f%%** |" % (tot, done, 100.0 * done / tot if tot else 0))
    out()
    out("> `ui` = 几乎一定是显示文本；`other` = 拿不准（单个词/代码片段），先翻 `ui`。")
    out()

    # ---------------- 按来源
    out("## 2. 按来源")
    out()
    out("| 来源 | 含义 | 条数 | 已译 |")
    out("|---|---|---:|---:|")
    src = defaultdict(lambda: [0, 0])
    for it in items:
        key = it["cat"]
        src[key][0] += 1
        if it.get("zh"):
            src[key][1] += 1
    meaning = {
        "asset": "只在资产里序列化（不走 setter；UILabel 走 ProcessText 补丁，TextMesh 改资产）",
        "both": "资产 + DLL 都有（最该先翻）",
        "dll": "只在 DLL 里（走 ProcessText）",
    }
    for k in ("both", "asset", "dll"):
        if k in src:
            out("| %s | %s | %d | %d |" % (k, meaning.get(k, ""), src[k][0], src[k][1]))
    out()

    # ---------------- 按类分组
    out("## 3. 按命中类名分组（看哪块界面还没动）")
    out()
    out("| 类 | 条数 | 已译 | 示例 |")
    out("|---|---:|---:|---|")
    groups = defaultdict(list)
    for it in items:
        for c in (it.get("cls") or ["(资产)"]):
            groups[c].append(it)
    rows = sorted(groups.items(), key=lambda kv: -len([x for x in kv[1] if not x.get("zh")]))
    for c, lst in rows[:args.top]:
        n = len(lst)
        d = sum(1 for x in lst if x.get("zh"))
        sample = next((x["en"] for x in lst if not x.get("zh")), lst[0]["en"])
        out("| %s | %d | %d | `%s` |" % (c, n, d, sample[:40].replace("|", "\\|")))
    out()

    # ---------------- 缺字
    out("## 4. 译文缺字检查")
    out()
    zh_text = "".join(it["zh"] for it in items if it.get("zh"))
    used = set(zh_text)
    missing = sorted(c for c in used if c not in baked and c.strip())
    if not zh_text:
        out("（还没有译文）")
    elif missing:
        out("!! 有 %d 个字符不在烘焙范围内，游戏里会显示成方块：" % len(missing))
        out()
        out("```")
        out("".join(missing))
        out("```")
        out()
        out("补烘：`.venv\\Scripts\\python.exe tools\\bake_font.py --extra \"%s\"`" % "".join(missing))
    else:
        out("✓ 全部译文字符都在烘焙范围内")
    out()

    # ---------------- 最长未译
    out("## 5. 最长的未译条目（优先处理长文本）")
    out()
    left = [it for it in items if not it.get("zh") and it.get("grade") == "ui"]
    left.sort(key=lambda it: -len(it["en"]))
    for it in left[:args.top]:
        out("- `%s`" % it["en"][:120].replace("`", "'").replace("\n", "\\n"))
    out()

    if not args.no_write:
        with open(STATUS, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lines) + "\n")
        print("")
        print("→ 已写入 %s" % STATUS)


if __name__ == "__main__":
    main()
