#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""译文一致性审计：逐条比对 en / zh 的 [颜色标记]、换行数、占位符、首尾空白。

用法：
    .venv\\Scripts\\python.exe tools\\audit_translations.py
    .venv\\Scripts\\python.exe tools\\audit_translations.py --max 30   # 每类最多列 30 条

四项全为 0 才算过。改完译文（尤其手工补的）都该跑一次；
`apply_translations.py` 只保证"原文能匹配上"，不保证标记/空白没被译丢。
"""
import argparse
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
P = os.path.join(ROOT, "_dict", "strings.json")

# [ffff00] 这类富文本色标；{0} %s <b> 这类占位符
TAG = re.compile(r"\[[^\[\]\n]{1,20}\]")
PH = re.compile(r"\{[^{}\n]{1,12}\}|%[sdf]|<[a-zA-Z/][^<>\n]{0,12}>")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--max", type=int, default=15, help="每类最多列几条")
    a = ap.parse_args()

    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    d = json.load(open(P, encoding="utf-8"))
    S = d["strings"]

    tag_bad, nl_bad, ph_bad, sp_bad = [], [], [], []
    for i, it in enumerate(S):
        en, zh = it["en"], it.get("zh") or ""
        if not zh:
            continue
        if TAG.findall(en) != TAG.findall(zh):
            tag_bad.append((i, en, zh))
        if en.count("\n") != zh.count("\n"):
            nl_bad.append((i, en, zh))
        if PH.findall(en) != PH.findall(zh):
            ph_bad.append((i, en, zh))
        # 首尾空白必须逐字保留：拼接前缀靠它对齐
        le = len(en) - len(en.lstrip())
        te = len(en) - len(en.rstrip())
        if zh[:le] != en[:le] or (te and zh[-te:] != en[-te:]):
            sp_bad.append((i, en, zh))

    total = 0
    for name, arr in (("标记", tag_bad), ("换行", nl_bad),
                      ("占位符", ph_bad), ("首尾空白", sp_bad)):
        total += len(arr)
        print("== %s: %d" % (name, len(arr)))
        for i, en, zh in arr[:a.max]:
            print("  %d  %r -> %r" % (i, en, zh))
    print("\n%s" % ("✓ 四项全过" if total == 0 else "✗ 共 %d 处问题" % total))
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main())
