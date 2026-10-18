#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
把 strings.json 编译成插件运行时直接读的二进制词表。

★ 为什么用二进制而不是 TSV：
   插件是 C# 3.5 / Mono 2.x，手写 TSV 解析要处理转义、引号、BOM、换行，
    还要在启动时把 1000+ 条字符串全读进内存做 Dictionary。
    这里离线把 原文 → 译文 压成一个顺序文件：

        magic  "CWH1"                      4 字节
        count  int32                       条数
        重复 count 次：
            keyLen uint16, key  (UTF-8)    英文原文
            zhLen  uint16, zh   (UTF-8)    译文
        末尾 4 字节 uint32 = (key+zh) 区的 FNV-1a 校验（★ 无符号，C# 端用 ReadUInt32）

    插件端只需 BinaryReader 顺序读，零转义问题。

★ 只导出 zh 非空的条目。空译文 = 还没翻，不导出（插件查不到就保持原文）。

用法：
    .venv\\Scripts\\python.exe tools\\build_dict.py
    .venv\\Scripts\\python.exe tools\\build_dict.py --include-other   # other 档已有的译文也导出
"""
import argparse
import json
import os
import struct
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict")
STRINGS = os.path.join(DICT, "strings.json")
OUT = os.path.join(DICT, "zh_cn.dict")

MAGIC = b"CWH1"


def fnv1a(data):
    h = 0x811C9DC5
    for b in data:
        h ^= b
        h = (h * 0x01000193) & 0xFFFFFFFF
    return h


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    ap = argparse.ArgumentParser()
    ap.add_argument("--include-other", action="store_true",
                    help="把 grade=other 的已译条目也导出（默认只导出 ui）")
    ap.add_argument("--out", default=OUT)
    args = ap.parse_args()

    with open(STRINGS, encoding="utf-8") as f:
        items = json.load(f)["strings"]

    rows = []
    skipped = 0
    for it in items:
        if not it.get("zh"):
            continue
        if it.get("grade") != "ui" and not args.include_other:
            skipped += 1
            continue
        rows.append((it["en"], it["zh"]))

    # 原文按 UTF-8 字节序排序：插件可二分，也可直接线性读（条数少）
    rows.sort(key=lambda kv: kv[0].encode("utf-8"))

    body = bytearray()
    for en, zh in rows:
        k = en.encode("utf-8")
        z = zh.encode("utf-8")
        if len(k) > 0xFFFF or len(z) > 0xFFFF:
            print("!! 超长条目跳过：%s" % en[:40])
            continue
        body += struct.pack("<H", len(k)) + k
        body += struct.pack("<H", len(z)) + z

    blob = bytearray(MAGIC)
    blob += struct.pack("<i", len(rows))
    blob += body
    blob += struct.pack("<I", fnv1a(bytes(body)))

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "wb") as f:
        f.write(blob)

    print("已编译 %d 条 → %s（%d 字节）" % (len(rows), args.out, len(blob)))
    if skipped:
        print("  跳过 grade=other 的已译条目 %d 条（加 --include-other 可导出）" % skipped)
    print("  生成时间 %s" % time.strftime("%Y-%m-%d %H:%M:%S"))


if __name__ == "__main__":
    main()
