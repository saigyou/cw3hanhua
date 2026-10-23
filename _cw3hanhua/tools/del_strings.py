# -*- coding: utf-8 -*-
"""从 strings.json 里**删除**批次中列出的条目（按 en 精确匹配）。

用途：撤回不再需要的条目（例如被通用规则取代的枚举串）。
与 apply_translations.py 的分工：
  - apply_translations.py  只往已有条目里填译文，绝不增删；
  - add_strings.py         只新增；
  - 本脚本                 只删除。

用法：
    python del_strings.py _dict\\batches\\full_23_shieldmsg.json [--dry-run]

注意：批次文件里的 "items" 的 **key** 是要删的英文原文，value 不参与判断。
      只删「批次里列了、且 strings.json 里确实存在」的条目；
      批次里列了但表里没有的，报告为「表里没有，跳过」。
"""
import argparse
import io
import json
import os
import sys
import time

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
STRINGS = os.path.join(ROOT, "_dict", "strings.json")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("batches", nargs="+", help="批次 json 文件（可多个）")
    ap.add_argument("--dry-run", action="store_true", help="只报告，不写盘")
    args = ap.parse_args()

    data = json.load(open(STRINGS, encoding="utf-8"))
    idx = {}
    for i, it in enumerate(data["strings"]):
        idx.setdefault(it["en"], []).append(i)

    want = []
    for b in args.batches:
        batch = json.load(open(b, encoding="utf-8"))
        want.extend(batch.get("items", {}).keys())

    kill = set()
    missing = []
    for en in want:
        if en in idx:
            kill.update(idx[en])
        else:
            missing.append(en)

    print("批次里列出 %d 条，表里找到 %d 条，表里没有 %d 条"
          % (len(want), len(kill), len(missing)))
    for en in missing:
        print("  [表里没有] %r" % en)

    kept = [it for i, it in enumerate(data["strings"]) if i not in kill]
    print("strings.json: %d → %d 条" % (len(data["strings"]), len(kept)))

    if args.dry_run:
        print("（--dry-run，未写盘）")
        return

    data["strings"] = kept
    data["generated"] = time.strftime("%Y-%m-%d %H:%M:%S")
    with open(STRINGS, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    print("已写回 %s" % STRINGS)


if __name__ == "__main__":
    main()
