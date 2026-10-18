#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
校验一份**按序号对齐**的译稿。

源清单：_dict/work/full_01.src.json
    {"batch": "full_01", "count": N, "items": [{"i":0,"cls":"...","en":"..."}, ...]}
译稿  ：_dict/work/full_01.zh.json
    {"batch": "full_01", "zh": ["译文0", "译文1", ...]}
    - 数组长度必须等于 count
    - 某一项写 "@" 表示"跳过不译"（不填入词表）

检查项（任一不过就打印并返回 1）：
    1. 数组长度 == count
    2. 译文非空（跳过项除外）
    3. 首尾空白原样保留（"PACKET SPEED.  " 的尾空格是拼接前缀的一部分）
    4. 换行符个数一致
    5. [RRGGBB] / [颜色名] 方括号标记原样保留且顺序一致
    6. 占位符 {0} %s <b> 等保留
    7. 与原文完全相同的条目列出来（提示是不是漏翻了）
返回 0 表示通过。
"""
import json
import os
import re
import sys

TAG = re.compile(r"\[[^\[\]\n]{1,20}\]")
PH = re.compile(r"\{[^{}\n]{1,12}\}|%[sdf]|<[a-zA-Z/][^<>\n]{0,12}>")


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    src_path, zh_path = sys.argv[1], sys.argv[2]
    with open(src_path, encoding="utf-8") as f:
        src = json.load(f)
    with open(zh_path, encoding="utf-8") as f:
        zh = json.load(f)

    items = src["items"]
    raw = zh.get("zh", [])
    n = len(items)
    if isinstance(raw, dict):
        # 序号字典形式：{"3": "译文"}，缺的默认跳过。★ 推荐用这种，不会整体错位
        arr = [raw.get(str(i), raw.get(i, "@")) for i in range(n)]
        bad_key = [k for k in raw if not str(k).isdigit() or not (0 <= int(k) < n)]
        if bad_key:
            print("!! 序号越界：%r" % (bad_key[:20],))
            return 1
    else:
        arr = raw
        if len(arr) != n:
            print("!! 条数不符：源 %d，译稿 %d" % (n, len(arr)))
            return 1

    problems = []
    skipped = []
    same = []
    bad_ws, bad_nl, bad_tag, bad_ph, empty = [], [], [], [], []
    for it, z in zip(items, arr):
        e = it["en"]
        if z == "@":
            skipped.append(e)
            continue
        if not isinstance(z, str) or z.strip() == "":
            empty.append(e)
            continue
        if z == e:
            same.append(e)
            continue
        le = len(e) - len(e.lstrip())
        te = len(e) - len(e.rstrip())
        if z[:le] != e[:le] or (te and z[-te:] != e[-te:]):
            bad_ws.append((e, z))
        if e.count("\n") != z.count("\n"):
            bad_nl.append((e, z))
        if TAG.findall(e) != TAG.findall(z):
            bad_tag.append((e, z))
        if sorted(PH.findall(e)) != sorted(PH.findall(z)):
            bad_ph.append((e, z))

    for name, lst in (("译文为空", empty), ("换行数不一致", bad_nl),
                      ("首尾空白不一致", bad_ws), ("[标记] 不一致", bad_tag),
                      ("占位符不一致", bad_ph)):
        if lst:
            problems.append(("%s %d 条" % (name, len(lst)), lst[:15]))

    print("%s：%d 条；跳过 %d 条；与原文相同 %d 条"
          % (os.path.basename(src_path), n, len(skipped), len(same)))
    if skipped:
        for e in skipped[:60]:
            print("  @ %r" % e)
        if len(skipped) > 60:
            print("  … 还有 %d 条" % (len(skipped) - 60))
    if same:
        for e in same[:60]:
            print("  = %r" % e)

    if problems:
        print("")
        print("!! %d 类问题：" % len(problems))
        for name, lst in problems:
            print("  - %s" % name)
            for x in lst:
                print("      %r" % (x,))
        return 1
    print("✓ 校验通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
