# -*- coding: utf-8 -*-
"""去掉译文末尾「全角标点 + 半角空格」里的那个半角空格。

背景：这批条目全是拼接前缀（en 自带尾空格，如 'Collected: '），
中文里全角标点本身自带间隔，再缀一个半角空格就是 `已收集： 指挥节点`，
违反用户 2026-10-24 定的「中英混排不加空格」。

只删**末尾**的那个空格，前导空格（`  地形：`、`  [ffffff]升级至等级：`）保留。

用法：
    .venv\\Scripts\\python.exe tools\\fix_tail_space.py            # 只报告
    .venv\\Scripts\\python.exe tools\\fix_tail_space.py --write    # 写回
      --target strings | work | both | batches（默认 both）
"""
import argparse
import glob
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict")
WORK = os.path.join(DICT, "story_work")

# 末尾 = 全角标点（CJK 标点 / 全角符号 / 破折号 / 省略号）后跟一个半角空格，且到此为止
RX_TAIL = re.compile(r"([\u3000-\u303f\uff01-\uff60\u2014\u2026])[ ]+$")


def fix_one(s):
    if not s:
        return s, 0
    m = RX_TAIL.search(s)
    if not m:
        return s, 0
    return s[:m.end(1)], len(s) - m.end(1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--target", default="both",
                    choices=["both", "strings", "work", "batches"])
    ap.add_argument("--show", type=int, default=0)
    args = ap.parse_args()
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    shown = [0]

    def report(tag, old, new, meta=""):
        if shown[0] < args.show:
            print("  [%s]%s\n    - %r\n    + %r" % (tag, meta, old, new))
            shown[0] += 1

    tot_c = tot_s = 0

    if args.target in ("both", "strings"):
        p = os.path.join(DICT, "strings.json")
        d = json.load(io.open(p, encoding="utf-8-sig"))
        c = n = 0
        for it in d["strings"]:
            zh = it.get("zh") or ""
            new, k = fix_one(zh)
            if k:
                it["zh"] = new
                c += 1
                n += k
                report("strings", zh, new, " en=%r" % (it.get("en"),))
        print("strings.json      改动 %d 条 / 删 %d 空格" % (c, n))
        tot_c += c
        tot_s += n
        if args.write and c:
            with io.open(p, "w", encoding="utf-8", newline="\n") as f:
                json.dump(d, f, ensure_ascii=False, indent=1)

    if args.target in ("both", "work"):
        c = n = 0
        for q in sorted(glob.glob(os.path.join(WORK, "*.out.json"))):
            d = json.load(io.open(q, encoding="utf-8-sig"))
            cc = 0
            for it in d.get("items", []):
                zh = it.get("zh") or ""
                new, k = fix_one(zh)
                if k:
                    it["zh"] = new
                    cc += 1
                    n += k
                    report("work", zh, new, " %s id=%s" % (os.path.basename(q), it.get("id")))
            if cc:
                print("%-28s 改动 %d 条" % (os.path.basename(q), cc))
                c += cc
                if args.write:
                    with io.open(q, "w", encoding="utf-8", newline="\n") as f:
                        json.dump(d, f, ensure_ascii=False, indent=1)
        print("story_work        改动 %d 条 / 删 %d 空格" % (c, n))
        tot_c += c
        tot_s += n

    if args.target in ("both", "batches"):
        BATCH = os.path.join(DICT, "batches")
        c = n = 0
        for q in sorted(glob.glob(os.path.join(BATCH, "*.json"))):
            d = json.load(io.open(q, encoding="utf-8-sig"))
            items = d.get("items") if isinstance(d, dict) and "items" in d else d
            if not isinstance(items, dict):
                continue
            cc = 0
            for en, zh in list(items.items()):
                if not isinstance(zh, str):
                    continue
                new, k = fix_one(zh)
                if k:
                    items[en] = new
                    cc += 1
                    n += k
                    report("batches", zh, new, " %s" % os.path.basename(q))
            if cc:
                print("%-28s 改动 %d 条" % (os.path.basename(q), cc))
                c += cc
                if args.write:
                    with io.open(q, "w", encoding="utf-8", newline="\n") as f:
                        json.dump(d, f, ensure_ascii=False, indent=1)
        print("batches           改动 %d 条 / 删 %d 空格" % (c, n))
        tot_c += c
        tot_s += n

    print("-" * 50)
    print("合计 改动 %d 条 / 删 %d 空格" % (tot_c, tot_s))
    if not args.write:
        print("（未写回，加 --write 生效）")


if __name__ == "__main__":
    main()
