#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
检查给定文本/词表里用到的字符是否都在已烘焙的字集内。

用途：
    翻译前/翻译后各跑一次，提前发现"缺字"（缺字在游戏里会显示成空白/方块）。

★ 校验的是**运行时真正能用的范围**，不是单套字集的并集：
    插件按 UIFont 的 size 取 font_<size>.fnt（16/20/28 三套），
    一个字符只要有任一字号没烘，用到那个字号就是方块 → 取三套的交集。
    默认烘焙范围（GB2312 全集 7544 字）由 bake_font.build_charset() 给出，
    与 bake_font.py 共用同一份定义，不会两边漂移。

用法：
    .venv\\Scripts\\python.exe tools\\check_charset.py 词表.tsv
    .venv\\Scripts\\python.exe tools\\check_charset.py _dict\\strings.json   # 只查译文字段
    .venv\\Scripts\\python.exe tools\\check_charset.py some_dir\\           # 目录递归
    .venv\\Scripts\\python.exe tools\\check_charset.py 词表.tsv --show-all

缺字时，把下面这行贴进终端重烘：
    .venv\\Scripts\\python.exe tools\\bake_font.py --extra "缺的字"
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DEFAULT_FONT_DIR = os.path.join(ROOT, "_font")

sys.path.insert(0, HERE)
import bake_font          # noqa: E402  ★ 字符集定义只有一份，在这里


def load_charset(font_dir, sizes, extra=""):
    """读 font_<size>.json，返回所有指定字号**都能覆盖**的字符集。"""
    if not os.path.isdir(font_dir):
        return set(), {}
    bake_font.BAKED_CHARSET.clear()
    got = bake_font.load_baked(sizes, font_dir)
    return bake_font.charset_for(sizes, extra), got


def iter_files(paths):
    for p in paths:
        if os.path.isdir(p):
            for root, _dirs, files in os.walk(p):
                for fn in files:
                    yield os.path.join(root, fn)
        elif os.path.isfile(p):
            yield p


def read_text(path):
    """尽量读成文本；二进制/无法解码的跳过。

    .json（_dict/ 下的文本规格）只取译文字段 —— 原文是英文，查了也没意义。
    """
    if path.lower().endswith(".json"):
        try:
            with open(path, encoding="utf-8") as f:
                data = json.load(f)
        except Exception:
            return None
        items = data.get("strings") if isinstance(data, dict) else data
        if not isinstance(items, list):
            return None
        parts = []
        for it in items:
            if isinstance(it, dict):
                if it.get("zh"):
                    parts.append(it["zh"])
                if it.get("note"):          # 术语表注释里也可能出现要显示的字符
                    parts.append(it["note"])
            elif isinstance(it, str):
                parts.append(it)
        return "\n".join(parts)

    try:
        with open(path, "rb") as f:
            raw = f.read()
    except Exception:
        return None
    if b"\x00" in raw[:4096]:          # 二进制
        return None
    for enc in ("utf-8-sig", "utf-8", "gb18030"):
        try:
            return raw.decode(enc)
        except Exception:
            continue
    return None


def is_han(ch):
    o = ord(ch)
    return 0x4E00 <= o <= 0x9FFF


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="+", help="词表/文本文件或目录")
    ap.add_argument("--font-dir", default=DEFAULT_FONT_DIR)
    ap.add_argument("--sizes", default=",".join(str(s) for s in bake_font.DEFAULT_SIZES),
                    help="要一起校验的字号，默认 %s（插件实际装载的）"
                         % ",".join(str(s) for s in bake_font.DEFAULT_SIZES))
    ap.add_argument("--extra", default="", help="额外认作已烘焙的字符（与 bake_font --extra 对应）")
    ap.add_argument("--show-all", action="store_true", help="也列出已覆盖的字符")
    args = ap.parse_args()

    sizes = [int(s) for s in args.sizes.split(",") if s.strip()]
    charset, got = load_charset(args.font_dir, sizes, args.extra)
    if not got:
        print("!! 字集为空：请确认 --font-dir 指向 _font 目录，且已跑过 bake_font.py")
        return 2

    used = set()
    files = 0
    skipped = 0
    for path in iter_files(args.paths):
        text = read_text(path)
        if text is None:
            skipped += 1
            continue
        files += 1
        used.update(text)

    used_han = set(c for c in used if is_han(c))
    used_ascii = set(c for c in used if 0x20 <= ord(c) <= 0x7E)
    used_other = used - used_han - used_ascii - set("\r\n\t")

    missing_han = sorted(used_han - charset)
    missing_other = sorted(used_other - charset)

    print("已烘字号       : %s" % ", ".join("%d(%d 字)" % (s, n) for s, n in sorted(got.items())))
    print("校验字号       : %s  → 交集 %d 个字符" % (",".join(str(s) for s in sizes), len(charset)))
    print("扫描文件       : %d 个（跳过二进制 %d 个）" % (files, skipped))
    print("用到字符       : %d 个（汉字 %d / ASCII %d / 其它 %d）"
          % (len(used), len(used_han), len(used_ascii), len(used_other)))
    print("")

    if missing_han:
        print("!! 缺汉字 %d 个，游戏里会显示成方块：" % len(missing_han))
        print("   " + "".join(missing_han))
        print("")
    else:
        print("✓ 汉字全部在字集内")

    if missing_other:
        print("!! 缺其它字符 %d 个：" % len(missing_other))
        print("   " + "".join(missing_other))
        print("")

    if args.show_all:
        covered = sorted(used_han & charset)
        print("已覆盖汉字 %d 个：" % len(covered))
        print("   " + "".join(covered))

    # 给出可直接执行的补烘命令
    extra = "".join(missing_han + missing_other)
    if extra:
        print("")
        print("补烘命令（缺字时必须把三个字号都重烘一遍，否则交集里仍然没有）：")
        print('  .venv\\Scripts\\python.exe tools\\bake_font.py --extra "%s"' % extra)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
