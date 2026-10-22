# -*- coding: utf-8 -*-
"""从 CW3_Data\\resources.assets 的 TextAsset 里抽取剧情/任务文本。

TextAsset 内容是 LZMA(FORMAT_ALONE) 压缩的 XML（System 或 Game 根节点）。
剧情台词在 <Messages><Message><c>角色</c><m>台词</m>，任务里也有同样的块。

输出：
  _cw3hanhua\\_dict\\story_candidates.json   —— 供翻译的候选串（按文件/标签分组）
  stdout 统计：条数、去重后条数、已在 strings.json 里的条数、缺失条数
"""
import io
import json
import lzma
import os
import re
import sys
import xml.etree.ElementTree as ET

import UnityPy

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "CW3_Data", "resources.assets")
DICT_JSON = os.path.join(ROOT, "_cw3hanhua", "_dict", "strings.json")
OUT_JSON = os.path.join(ROOT, "_cw3hanhua", "_dict", "story_candidates.json")


def iter_assets():
    env = UnityPy.load(ASSETS)
    for o in env.objects:
        if o.type.name != "TextAsset":
            continue
        d = o.read()
        name = d.m_Name
        s = d.m_Script
        raw = s.encode("utf-8", "surrogateescape") if isinstance(s, str) else bytes(s)
        try:
            xml = lzma.decompress(raw, format=lzma.FORMAT_ALONE)
        except Exception:
            continue
        try:
            root = ET.fromstring(xml.decode("utf-8-sig", "replace"))
        except Exception:
            continue
        yield name, root


def load_known():
    with io.open(DICT_JSON, encoding="utf-8") as f:
        data = json.load(f)
    known = {}
    for it in data["strings"]:
        en = it.get("en") or ""
        if en:
            known[en] = it
    return data, known


def main():
    data, known = load_known()
    msgs = []          # (file, char, text)
    names = []         # (file, tag, text)  星系/行星名
    per_file = {}
    for name, root in iter_assets():
        n_msg = 0
        for msg in root.iter("Message"):
            m = msg.findtext("m")
            c = msg.findtext("c")
            if m is None:
                continue
            t = m.strip()
            if not t:
                continue
            msgs.append((name, (c or "").strip(), t))
            n_msg += 1
        # 星系/行星名：System 里的 <n>/<g>，以及 Game 顶层的 <n>
        if root.tag == "System":
            for el in root.iter():
                if el.tag in ("n", "g") and el.text and el.text.strip():
                    names.append((name, el.tag, el.text.strip()))
        elif root.tag == "Game":
            top = root.findtext("n")
            if top and top.strip():
                names.append((name, "n", top.strip()))
        per_file[name] = n_msg

    # 去重（同一句可能在多个文件里出现）
    seen = {}
    order = []
    for fn, c, t in msgs:
        if t in seen:
            seen[t][1].add(fn)
            continue
        seen[t] = (c, {fn})
        order.append(t)

    missing = [t for t in order if t not in known]
    have = [t for t in order if t in known]

    print("TextAsset 解析成功数:", len(per_file))
    print("Message 总数:", len(msgs), " 去重:", len(order))
    print("已在词表:", len(have), " 缺失:", len(missing))
    print("名字条目:", len(names), " 去重:", len({x[2] for x in names}))
    miss_names = sorted({x[2] for x in names if x[2] not in known})
    print("名字缺失:", len(miss_names), miss_names[:20])
    print("缺失最长:", max((len(t) for t in missing), default=0), " 缺失总字符:",
          sum(len(t) for t in missing))

    out = {
        "generated": __import__("datetime").datetime.now().isoformat(timespec="seconds"),
        "messages": [
            {"en": t, "char": seen[t][0], "files": sorted(seen[t][1]),
             "translated": t in known}
            for t in order
        ],
        "names": sorted({(x[1], x[2]) for x in names}),
        "per_file": per_file,
    }
    with io.open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print("写出:", OUT_JSON)


if __name__ == "__main__":
    sys.exit(main())
