#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
把 _dict/batches/*.json 里的译文注入 strings.json。

设计要点：
    - **默认只填 zh 为空的条目**（不覆盖已有译文）；要改译文用 --override。
    - 批次里的 key 必须在 strings.json 里**精确存在**，否则报错并列出 —— 防止
      加了引号/改了空格导致静默失效（"翻译了但不生效"比没翻更难查）。
    - 空译文（""）视为"取消翻译"，需要 --clear 才生效。

用法：
    .venv\\Scripts\\python.exe tools\\apply_translations.py            # 全部批次，只填空
    .venv\\Scripts\\python.exe tools\\apply_translations.py --override # 覆盖已有译文
    .venv\\Scripts\\python.exe tools\\apply_translations.py --dry-run  # 只报告不改
    .venv\\Scripts\\python.exe tools\\apply_translations.py _dict\\batches\\both_ui.json
"""
import argparse
import glob
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict")
STRINGS = os.path.join(DICT, "strings.json")
BATCH_DIR = os.path.join(DICT, "batches")


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="*", help="批次文件，默认取 batches/ 下全部")
    ap.add_argument("--override", action="store_true", help="覆盖已有译文")
    ap.add_argument("--clear", action="store_true", help="允许把译文清空")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    files = args.files or sorted(glob.glob(os.path.join(BATCH_DIR, "*.json")))
    if not files:
        print("!! 没找到批次文件：%s" % BATCH_DIR)
        return 2

    with open(STRINGS, encoding="utf-8") as f:
        data = json.load(f)
    items = data["strings"]
    index = {it["en"]: it for it in items}

    added = updated = same = 0
    problems = []
    for path in files:
        with open(path, encoding="utf-8") as f:
            batch = json.load(f)
        items_map = batch.get("items", {})
        print("[%s] %d 条" % (os.path.basename(path), len(items_map)))
        for en, zh in items_map.items():
            it = index.get(en)
            if it is None:
                problems.append(("原文不在 strings.json 里", en))
                continue
            if zh == "" and not args.clear:
                problems.append(("译文为空但没加 --clear", en))
                continue
            cur = it.get("zh", "")
            if cur == zh:
                same += 1
                continue
            if cur and not args.override:
                problems.append(("已有译文，跳过（要改加 --override）", "%s  [现: %s]" % (en[:40], cur[:20])))
                continue
            if not args.dry_run:
                it["zh"] = zh
            if cur:
                updated += 1
            else:
                added += 1

    if problems:
        print("")
        print("!! %d 个问题（未写入）：" % len(problems))
        for kind, x in problems[:40]:
            print("   - %s: %r" % (kind, x))
        if len(problems) > 40:
            print("   … 还有 %d 条" % (len(problems) - 40))

    if not args.dry_run and (added or updated):
        # 与 add_strings.py / del_strings.py 保持一致：写盘就刷新 generated，
        # 否则 STATUS.md 里的「生成时间」会停在很久以前，看不出词表动过。
        data["generated"] = time.strftime("%Y-%m-%d %H:%M:%S")
        with open(STRINGS, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)

    done = sum(1 for it in items if it.get("zh"))
    print("")
    print("本次新增 %d，覆盖 %d，已相同 %d；问题 %d" % (added, updated, same, len(problems)))
    print("词表现状：%d / %d 条已译（%.1f%%）"
          % (done, len(items), 100.0 * done / len(items) if items else 0))
    if args.dry_run:
        print("（--dry-run，未写盘）")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
