#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""把字符串批次（只含 en/zh 对）追加进 strings.json，用于**运行时拼接串**。

apply_translations.py 只能填已存在的条目，而这类整串（例：
"Planet is shielded. Keys needed from planet(s):\n[ff7f7f]Tempus"）是游戏在
运行时 String.Concat 出来的，静态收集器只抓到碎片，所以必须新增条目。

纪律：
    - 只新增，不覆盖已有 zh；同 en 已存在且 zh 非空则原地跳过（报出来）。
    - 新条目统一 cat="dll"、grade="ui"（build_dict.py 默认只导出 ui，
      grade=other 会被静默丢掉，这是过去踩过的坑）。
    - 写盘格式与 apply_translations.py 完全一致：ensure_ascii=False, indent=1,
      newline="\\n"；并刷新 S["generated"]。

用法：
    .venv\\Scripts\\python.exe tools\\add_strings.py _dict\\batches\\full_23_shieldmsg.json --dry-run
    .venv\\Scripts\\python.exe tools\\add_strings.py _dict\\batches\\full_23_shieldmsg.json
"""
import argparse
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict")
STRINGS = os.path.join(DICT, "strings.json")


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+", help="批次文件（{\"items\": {en: zh}}）")
    ap.add_argument("--kind", default="", help="写进 kind 字段，默认空")
    ap.add_argument("--note", default="", help="写进 note 字段")
    ap.add_argument("--grade", default="ui", help="默认 ui；other 不会进词表")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    with open(STRINGS, encoding="utf-8") as f:
        data = json.load(f)
    items = data["strings"]
    index = {it["en"]: it for it in items}

    added = 0
    problems = []
    for path in args.files:
        with open(path, encoding="utf-8") as f:
            batch = json.load(f)
        m = batch.get("items", {})
        print("[%s] %d 条" % (os.path.basename(path), len(m)))
        for en, zh in m.items():
            if not zh:
                problems.append(("译文为空", en))
                continue
            cur = index.get(en)
            if cur is not None:
                if cur.get("zh"):
                    problems.append(("已存在且有译文，跳过", en))
                else:
                    if not args.dry_run:
                        cur["zh"] = zh
                    added += 1
                continue
            rec = {
                "en": en,
                "zh": zh,
                "cat": "dll",
                "grade": args.grade,
                "kind": args.kind,
                "ops": ["call Concat"],
                "srcs": ["Assembly-CSharp.dll"],
                "cls": ["SystemManager", "GalObject"],
                "refs": [],
                "hits": 1,
                "note": args.note,
            }
            if not args.dry_run:
                items.append(rec)
                index[en] = rec
            added += 1

    if problems:
        print("")
        print("!! %d 个问题：" % len(problems))
        for kind, x in problems[:20]:
            print("   - %s: %r" % (kind, x))

    if not args.dry_run and added:
        data["generated"] = time.strftime("%Y-%m-%d %H:%M:%S")
        with open(STRINGS, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)

    print("")
    print("新增 %d 条；问题 %d；现在共 %d 条"
          % (added, len(problems), len(items)))
    if args.dry_run:
        print("（--dry-run，未写盘）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
