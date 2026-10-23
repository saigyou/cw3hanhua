#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""把 _dict/crpl_zh_src.txt（人工译文，按 crpl_all.json 的展开顺序编号）拼成批次 JSON。

为什么要这一步：`crpl_all.json` 里的英文是**已反转义**的真串（含真换行/Tab），
而译文文件里为了肉眼可读写的是 `\\n` / `\\t` 两个字面字符。直接手抄进批次 JSON
必然对不上键（过去踩过：键对不上时 apply_translations 只报「原文不在 strings.json 里」，
而 add_strings 会**悄悄新增一条永远命中不了的死条目**）。所以统一用本脚本生成。

用法：
    .venv\\Scripts\\python.exe tools\\build_crpl_batch.py _dict\\crpl_zh_src.txt _dict\\batches\\full_29_crpl_full.json
"""
import io
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict")

# 只出现在 CRPL `trace()` / `trace2` 调试输出里的碎片，不面向玩家 → 故意不译。
SKIP = {"at facility", "Closest Unit "}


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    src_file = sys.argv[1]
    out_file = sys.argv[2]

    # 1) 按与 crpl_todo.txt 完全相同的顺序重建英文表
    with io.open(os.path.join(DICT, "crpl_all.json"), encoding="utf-8") as f:
        assets = json.load(f)
    with io.open(os.path.join(DICT, "strings.json"), encoding="utf-8") as f:
        known = {e["en"] for e in json.load(f)["strings"] if e.get("zh")}

    en_list, seen = [], set()
    for a in assets:
        for s in a["strings"]:
            if s in known or s in seen:
                continue
            seen.add(s)
            en_list.append(s)

    # 2) 读译文
    zh = {}
    with io.open(src_file, encoding="utf-8") as f:
        for ln in f:
            ln = ln.rstrip("\n")
            if not ln.strip():
                continue
            idx, _, text = ln.partition("\t")
            if not idx.isdigit():
                raise SystemExit("译文文件格式不对（缺 '编号<TAB>译文'）：%r" % ln[:40])
            zh[int(idx)] = text.replace("\\n", "\n").replace("\\t", "\t")

    if len(zh) != len(en_list):
        raise SystemExit("条数对不上：译文 %d 条，英文 %d 条" % (len(zh), len(en_list)))
    miss = [i for i in range(len(en_list)) if i not in zh]
    if miss:
        raise SystemExit("译文缺编号：%s" % miss[:20])

    # 3) 拼批次
    items, skipped = {}, []
    for i, en in enumerate(en_list):
        if en in SKIP:
            skipped.append(en)
            continue
        items[en] = zh[i]

    os.makedirs(os.path.dirname(os.path.abspath(out_file)), exist_ok=True)
    with io.open(out_file, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"items": items}, f, ensure_ascii=False, indent=1)

    print("英文 %d 条；写入批次 %d 条；跳过(trace 专用) %d 条"
          % (len(en_list), len(items), len(skipped)))
    for s in skipped:
        print("   skip: %r" % s)
    print("wrote", out_file)

    # 4) 自检：键必须和英文表逐字一致
    with io.open(out_file, encoding="utf-8") as f:
        back = json.load(f)["items"]
    bad = [k for k in back if k not in set(en_list)]
    if bad:
        print("!! 有 %d 个键不在英文表里：" % len(bad))
        for k in bad[:5]:
            print("   %r" % k)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
