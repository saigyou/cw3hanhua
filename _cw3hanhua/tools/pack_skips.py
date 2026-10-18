#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""把 _dict/work/*.zh.json 里标 @ 的「故意不译」决策固化成 _dict/skipped_manual.json。

work/ 是一次性工作目录（src 清单 + 译稿 + @ 标记）。译稿已全部并入
_dict/batches/*.json（可用 merge_work.py 复核），但「**这条我特意跳过**」
这个决策只存在于 @ 标记里。删 work/ 前跑一次本脚本，把决策留下来。

用法：.venv\\Scripts\\python.exe tools\\pack_skips.py
"""
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
WORK = os.path.join(ROOT, "_dict", "work")
OUT = os.path.join(ROOT, "_dict", "skipped_manual.json")


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    srcs = sorted(glob.glob(os.path.join(WORK, "full_*.src.json")))
    if not srcs:
        print("没找到 %s\\full_*.src.json" % WORK)
        return 1

    rows = []
    for f in srcs:
        batch = os.path.basename(f).replace(".src.json", "")
        src = json.load(open(f, encoding="utf-8"))
        zhf = f.replace(".src.json", ".zh.json")
        if not os.path.exists(zhf):
            continue
        raw = json.load(open(zhf, encoding="utf-8")).get("zh", {})
        pairs = raw.items() if isinstance(raw, dict) else [
            (str(i), v) for i, v in enumerate(raw)]
        decided = {str(k): v for k, v in pairs}

        for it in src["items"]:
            k = str(it["i"])
            v = decided.get(k, "")
            if v and v != "@":
                continue  # 译过的不记
            rows.append({
                "en": it["en"],
                "i": it["i"],
                "cls": it.get("cls") or "",
                "batch": batch,
                "why": "@ 标记跳过（人工判定不译）" if v == "@" else "未在译稿中出现（默认跳过）",
            })

    rows.sort(key=lambda r: (r["batch"], r["i"]))
    out = {
        "_说明": "全量翻译时人工判定「不译」的条目（565 条）。规则化分流的不译桶"
                 "（标识符/文件名/哈希/星球名音节等，见 NOTES 4.5 桶表）不在此列，"
                 "可用 tools/prep_batches.py 重算。本文件记录的是**人工决策**，勿删。",
        "count": len(rows),
        "items": rows,
    }
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("写入 %d 条 → %s（%d 字节）" % (len(rows), OUT, os.path.getsize(OUT)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
