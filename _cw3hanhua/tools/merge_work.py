#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
把 _dict/work/full_NN.src.json + full_NN.zh.json 合并成 apply_translations.py
能吃的批次文件 _dict/batches/full_NN.json（{"items": {英文原文: 译文}}）。

跳过项：
    "@"      -> 明确跳过不译
    与原文完全相同 -> 视为漏翻，报出来并从批次里剔除

用法：
    .venv\\Scripts\\python.exe tools/merge_work.py            # 全部 full_NN
    .venv\\Scripts\\python.exe tools/merge_work.py full_05    # 指定若干份
    .venv\\Scripts\\python.exe tools/merge_work.py --dry-run
"""
import glob
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict")
WORK = os.path.join(DICT, "work")
BATCH_DIR = os.path.join(DICT, "batches")


def build_one(tag, dry_run):
    src_path = os.path.join(WORK, tag + ".src.json")
    zh_path = os.path.join(WORK, tag + ".zh.json")
    with open(src_path, encoding="utf-8") as f:
        src = json.load(f)
    with open(zh_path, encoding="utf-8") as f:
        zh = json.load(f)

    items = src["items"]
    n = len(items)
    raw = zh.get("zh", {})
    if isinstance(raw, list):
        arr = raw if len(raw) == n else None
        if arr is None:
            print("!! %s 译稿是数组且长度 %d != %d，改用序号字典形式" % (tag, len(raw), n))
            return None
    else:
        arr = [raw.get(str(i), raw.get(i, "@")) for i in range(n)]
        bad = [k for k in raw if not str(k).isdigit() or not (0 <= int(k) < n)]
        if bad:
            print("!! %s 序号越界：%r" % (tag, bad[:10]))
            return None

    out = {}
    skipped = same = 0
    same_list = []
    for it, z in zip(items, arr):
        e = it["en"]
        if z == "@":
            skipped += 1
            continue
        if not isinstance(z, str) or z.strip() == "":
            skipped += 1
            continue
        if z == e:
            same += 1
            same_list.append(e)
            continue
        out[e] = z

    dst = os.path.join(BATCH_DIR, tag + ".json")
    if not dry_run:
        os.makedirs(BATCH_DIR, exist_ok=True)
        with open(dst, "w", encoding="utf-8", newline="\n") as f:
            json.dump({"items": out}, f, ensure_ascii=False, indent=1)
    print("%s: 共 %d，写入 %d，跳过 %d，与原文相同 %d%s"
          % (tag, n, len(out), skipped, same, "" if not dry_run else "（dry-run）"))
    for e in same_list[:10]:
        print("    = %r" % e)
    return len(out)


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    dry_run = "--dry-run" in sys.argv
    if args:
        tags = args
    else:
        tags = sorted(os.path.basename(p)[:-len(".src.json")]
                      for p in glob.glob(os.path.join(WORK, "full_*.src.json")))
    total = 0
    if not tags:
        print("!! %s 下没有 full_*.src.json —— 需要重跑 tools\\prep_batches.py 生成" % WORK)
        return 1
    for tag in tags:
        r = build_one(tag, dry_run)
        if r:
            total += r
    print("")
    print("合计写入 %d 条到 %s" % (total, BATCH_DIR))
    return 0


if __name__ == "__main__":
    sys.exit(main())
