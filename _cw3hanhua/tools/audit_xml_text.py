# -*- coding: utf-8 -*-
"""审计：TextAsset 剧情 XML 里还有哪些显示用文本不在词表/名字表。

只挑「像人话」的叶子文本（纯 ASCII 词+空格，长度 3..80，不含数字/下划线），
列出所在资产，便于判断是否还有漏译的界面文本（星系名、星取名、类型名等）。
"""
import io
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

import lzma
import UnityPy

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.dirname(PROJ)
DICT = os.path.join(PROJ, "_dict")

WORDY = re.compile(r"[A-Za-z][A-Za-z0-9 '.,!?:;\-]*")


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    S = {x["en"] for x in json.load(io.open(os.path.join(DICT, "strings.json"), encoding="utf-8"))["strings"]}
    N = {x["en"] for x in json.load(io.open(os.path.join(DICT, "story_work", "names.out.json"),
                                            encoding="utf-8-sig"))["items"]}
    C = {m["en"] for m in json.load(io.open(os.path.join(DICT, "story_candidates.json"),
                                            encoding="utf-8"))["messages"]}
    env = UnityPy.load(os.path.join(ROOT, "CW3_Data", "resources.assets"))
    miss = {}
    for o in env.objects:
        if o.type.name != "TextAsset":
            continue
        d = o.read()
        nm = getattr(d, "m_Name", "")
        raw = d.m_Script.encode("utf-8", "surrogateescape")
        try:
            x = lzma.decompress(raw, format=lzma.FORMAT_ALONE).decode("utf-8", "replace")
            root = ET.fromstring(x.lstrip("\ufeff \r\n\t"))
        except Exception:
            continue
        for el in root.iter():
            if not isinstance(el.tag, str):
                continue
            t = (el.text or "").strip()
            if not 2 < len(t) <= 80 or not WORDY.fullmatch(t):
                continue
            if any(c.isdigit() for c in t) or "_" in t:
                continue
            if t in S or t in N or t in C:
                continue
            miss.setdefault(t, set()).add(nm)
    print("未覆盖文本 %d 条" % len(miss))
    for k in sorted(miss):
        print("%-40r  %s" % (k, ",".join(sorted(miss[k])[:3])))


if __name__ == "__main__":
    main()
