# -*- coding: utf-8 -*-
"""去掉译文里「中文与拉丁字母/数字之间」多余的半角空格。

用户 2026-10-24 决定：专有名词两侧、中英混排一律不加空格（两边都不用留）。

判定规则（只针对单个半角空格 U+0020，且两侧紧邻非空白字符）：
  类别  IDEO 汉字 / LAT 拉丁字母数字 / PUNCT 中文全角标点 / OTHER 其余
  删除条件（满足任一）：
    * 一侧 IDEO，另一侧是 LAT / IDEO / PUNCT
    * 一侧 PUNCT，另一侧是 LAT
  其余一律保留：多空格对齐（`     B：`）、行首缩进、行尾拼接空白、
  纯拉丁（`Knuckle Cracker, LLC`、`Finn M-K`、URL）、`20 - 256`。

颜色标记 `[ffffff]` / `[-]` 视为透明：判断邻字符时跳过，所以
`按 '[ffff00]C[-]' 克隆` 里的空格按 `C` 与 `克` 的关系处理。

**半角标点也视为透明**（2026-10-24 第二批，用户 m00563「也一并清掉」）：
`PUNCT_ASCII = : ' " ( ) [ ] { } < > / \\ | - _ + = * & ^ % $ # @ ! ? . , ; ~`
判邻字符时继续往外找。于是
`按 '[ffff00]C[-]' 克隆` → `按'[ffff00]C[-]'克隆`、
`麻烦列车01 - 教程` → `麻烦列车01-教程`、
`(PZ) 挖掘机是关键` → `(PZ)挖掘机是关键`。
但仍不会碰纯拉丁内部：`Knuckle Cracker, LLC`、`20 - 256`、`a, b` 里的空格
因为跳过标点后两侧都是 LAT，照旧保留。

用法：
    .venv\\Scripts\\python.exe tools\\fix_spaces.py              # 只报告
    .venv\\Scripts\\python.exe tools\\fix_spaces.py --write      # 写回
      --target strings | work | both（默认 both）
      --show N   逐条打印前 N 个样例
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

RX_IDEO = re.compile(r"[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff]")
RX_PUNCT = re.compile(r"[\u3000-\u303f\uff01-\uff60\u2014\u2026\u00b7]")
RX_LAT = re.compile(r"[A-Za-z0-9\u00c0-\u024f]")
RX_TAG = re.compile(r"\[(?:[0-9a-fA-F]{6}|-)\]")

# 判邻字符时视为透明的半角标点（2026-10-24 第二批）
PUNCT_ASCII = set(":'\"()[]{}<>/\\|-_+=*&^%$#@!?.,;~")
# 「X - Y」这种被两个空格夹住的单个符号，整体塌成 `X-Y`
RX_TRIP = re.compile(r"([^\s])[ \t]([-=+\u2014/])[ \t]([^\s])")

SP = " "

# 整条跳过（人工确认过要保留空格的条目）
SKIP_EN = set()
SKIP_ZH = set()


def cls(ch):
    if RX_IDEO.match(ch):
        return "IDEO"
    if RX_PUNCT.match(ch):
        return "PUNCT"
    if RX_LAT.match(ch):
        return "LAT"
    return "OTHER"


def prev_visible(s, i):
    """s[i] 左侧最近的可见字符（跳过颜色标记）。"""
    return s[prev_index(s, i)] if prev_index(s, i) >= 0 else ""


def next_visible(s, i):
    j = next_index(s, i)
    return s[j] if j >= 0 else ""


def prev_index(s, i):
    j = i - 1
    while j >= 0:
        if s[j] == "]":
            k = s.rfind("[", 0, j)
            if k >= 0 and RX_TAG.fullmatch(s[k:j + 1]):
                j = k - 1
                continue
            return j
        return j
    return -1


def next_index(s, i):
    j = i + 1
    n = len(s)
    while j < n:
        if s[j] == "[":
            m = RX_TAG.match(s, j)
            if m:
                j = m.end()
                continue
            return j
        return j
    return -1


def skip_left(s, i, transparent=None):
    """从 i 往左找第一个「非透明」字符下标；透明 = 颜色标记 + 半角标点。"""
    j = prev_index(s, i)
    while j >= 0:
        if transparent and s[j] in transparent:
            j = prev_index(s, j)
            continue
        return j
    return -1


def skip_right(s, i, transparent=None):
    j = next_index(s, i)
    n = len(s)
    while 0 <= j < n:
        if transparent and s[j] in transparent:
            j = next_index(s, j)
            continue
        return j
    return -1


def drop(a, b):
    if a == "IDEO" and b in ("LAT", "IDEO", "PUNCT"):
        return True
    if b == "IDEO" and a in ("LAT", "PUNCT"):
        return True
    if a == "PUNCT" and b == "LAT":
        return True
    if b == "PUNCT" and a == "LAT":
        return True
    return False


def fix_one(s, en=""):
    """返回 (新串, 删掉的空格数)。"""
    if not s:
        return s, 0
    if (en and en in SKIP_EN) or s in SKIP_ZH:
        return s, 0
    if SP not in s and "\t" not in s:
        return s, 0
    # 先塌「X - Y」这类被两空格夹住的单个符号
    n = 0
    while True:
        new, k = RX_TRIP.subn(r"\1\2\3", s)
        if not k:
            break
        s = new
        n += k * 2
    if SP not in s:
        return s, n
    out = []
    for i, ch in enumerate(s):
        if ch != SP:
            out.append(ch)
            continue
        pj = prev_index(s, i)
        qj = next_index(s, i)
        if pj < 0 or qj < 0:
            out.append(ch)
            continue
        if s[pj] in (" ", "\n", "\t", "\r") or s[qj] in (" ", "\n", "\t", "\r"):
            out.append(ch)
            continue
        # 特征字符：跳过半角标点后看到的那个字（用于判断中英关系）
        fp = skip_left(s, i, PUNCT_ASCII)
        fq = skip_right(s, i, PUNCT_ASCII)
        cp = cls(s[fp]) if fp >= 0 else ""
        cq = cls(s[fq]) if fq >= 0 else ""
        if drop(cp, cq):
            n += 1
            continue
        out.append(ch)
    return "".join(out), n


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--target", default="both", choices=["both", "strings", "work", "batches"])
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

    tot_n = 0
    tot_c = 0

    if args.target in ("both", "strings"):
        p = os.path.join(DICT, "strings.json")
        d = json.load(io.open(p, encoding="utf-8-sig"))
        cnt = sn = 0
        for it in d["strings"]:
            zh = it.get("zh") or ""
            new, n = fix_one(zh, it.get("en") or "")
            if n:
                it["zh"] = new
                cnt += 1
                sn += n
                report("strings", zh, new, " id=%s" % it.get("id"))
        print("strings.json      改动 %d 条 / 删 %d 空格" % (cnt, sn))
        tot_c += cnt
        tot_n += sn
        if args.write and cnt:
            with io.open(p, "w", encoding="utf-8", newline="\n") as f:
                json.dump(d, f, ensure_ascii=False, indent=1)

    if args.target in ("both", "work"):
        wc = wn = 0
        for q in sorted(glob.glob(os.path.join(WORK, "*.out.json"))):
            d = json.load(io.open(q, encoding="utf-8-sig"))
            c = 0
            for it in d.get("items", []):
                zh = it.get("zh") or ""
                new, n = fix_one(zh, it.get("en") or "")
                if n:
                    it["zh"] = new
                    c += 1
                    wn += n
                    report("work", zh, new, " %s id=%s" % (os.path.basename(q), it.get("id")))
            if c:
                print("%-28s 改动 %d 条" % (os.path.basename(q), c))
                wc += c
                if args.write:
                    with io.open(q, "w", encoding="utf-8", newline="\n") as f:
                        json.dump(d, f, ensure_ascii=False, indent=1)
        print("story_work        改动 %d 条 / 删 %d 空格" % (wc, wn))
        tot_c += wc
        tot_n += wn

    # batches/*.json 是 en->zh 平面映射，重跑 apply_translations.py --override
    # 会把旧译文灌回 strings.json，所以这里也要清，否则空格会复活。
    if args.target in ("both", "batches"):
        BATCH = os.path.join(DICT, "batches")
        bc = bn = 0
        for q in sorted(glob.glob(os.path.join(BATCH, "*.json"))):
            d = json.load(io.open(q, encoding="utf-8-sig"))
            items = d.get("items") if isinstance(d, dict) and "items" in d else d
            if not isinstance(items, dict):
                continue
            c = 0
            for en, zh in list(items.items()):
                if not isinstance(zh, str):
                    continue
                new, n = fix_one(zh, en)
                if n:
                    items[en] = new
                    c += 1
                    bn += n
                    report("batches", zh, new, " %s" % os.path.basename(q))
            if c:
                print("%-28s 改动 %d 条" % (os.path.basename(q), c))
                bc += c
                if args.write:
                    with io.open(q, "w", encoding="utf-8", newline="\n") as f:
                        json.dump(d, f, ensure_ascii=False, indent=1)
        print("batches           改动 %d 条 / 删 %d 空格" % (bc, bn))
        tot_c += bc
        tot_n += bn

    print("-" * 50)
    print("合计 改动 %d 条 / 删 %d 空格" % (tot_c, tot_n))
    if not args.write:
        print("（未写回，加 --write 生效）")


if __name__ == "__main__":
    main()
