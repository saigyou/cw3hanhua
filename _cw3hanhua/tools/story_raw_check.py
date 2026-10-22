#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
核对「XML 里的原始文本」和「我们词表里的 key」差在哪。

背景：extract_story_text.py 抽 <m> 时做了 m.strip()，但游戏的
Message.message 是**原样**赋给 UILabel 的（MessageDialogManager.set_currentMessage
IL 0126-0132: messageText.text = msg.message），所以只要 XML 里 <m> 前后有换行/空格，
词表的 key 就和运行时字符串差几个不可见字符 → 精确匹配必然落空。

本脚本只做只读核对：
  - 列出 raw != strip 的消息条数、差的是哪几个字符
  - 把 raw 文本写成 _dict/story_raw.json，供 fix_story_keys.py 用
"""
import io
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from extract_story_text import iter_assets, DICT_JSON  # noqa: E402

OUT = os.path.join(os.path.dirname(DICT_JSON), "story_raw.json")


def esc(s):
    return s.replace("\r", "\\r").replace("\n", "\\n").replace("\t", "\\t")


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    raws = []        # (file, raw)
    name_raws = []
    for name, root in iter_assets():
        for msg in root.iter("Message"):
            m = msg.findtext("m")
            if m is None:
                continue
            if not m.strip():
                continue
            raws.append((name, m))
        if root.tag == "System":
            for el in root.iter():
                if el.tag in ("n", "g") and el.text and el.text.strip():
                    name_raws.append((name, el.text))
        elif root.tag == "Game":
            top = root.findtext("n")
            if top and top.strip():
                name_raws.append((name, top))

    diff = [(f, r) for f, r in raws if r != r.strip()]
    print("消息总数:", len(raws), " 去重:", len({r for _, r in raws}))
    print("raw != strip 的条数:", len(diff))

    kinds = {}
    for _, r in diff:
        pre = len(r) - len(r.lstrip())
        post = len(r) - len(r.rstrip())
        k = "lead=%s tail=%s" % (esc(r[:pre]), esc(r[len(r) - post:]) if post else "")
        kinds[k] = kinds.get(k, 0) + 1
    for k, v in sorted(kinds.items(), key=lambda kv: -kv[1])[:10]:
        print("   %-40s ×%d" % (k, v))

    ndiff = [(f, r) for f, r in name_raws if r != r.strip()]
    print("名字 raw != strip:", len(ndiff), " / ", len(name_raws))

    out = {
        "messages": [{"file": f, "raw": r} for f, r in raws],
        "names": [{"file": f, "raw": r} for f, r in name_raws],
    }
    with io.open(OUT, "w", encoding="utf-8", newline="\n") as fp:
        json.dump(out, fp, ensure_ascii=False, indent=1)
    print("已写", OUT)


if __name__ == "__main__":
    main()
