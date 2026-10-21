#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
只读盘点：CW3 全部资产文件里有多少个 UILabel（NGUI，MonoBehaviour 的 mText 字段）。

用途：v0.8.5 删掉了「周期 sweep」，而 sweep 正是资产里**序列化的 UILabel.mText**
      唯一的汉化路径（这类文本不经过 set_text，prefix 拦不到）。
      删之前必须先知道这块盘子有多大，否则等于盲删。

      本脚本 **不写任何游戏文件**。

产出：
    _cw3hanhua/tools/uilabel_inventory.json
    stdout 统计摘要

用法：
    .venv\\Scripts\\python.exe tools\\scan_uilabel.py

★ 2026-10-21 从 `_diag/` 挪进 `tools/` 并入库：它不是临时中间产物，
  而是 `tools/diff_bake.py` 的对比基线。**JSON 是烘焙前的历史快照，不可再生**
  （游戏资产现在烘成了中文，重跑只会得到现役状态并覆盖基线）。要重跑就先备份 JSON。
"""
import json
import os
import sys
import time
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
GAME = os.path.dirname(ROOT)
DATA = os.path.join(GAME, "CW3_Data")
MANAGED = os.path.join(DATA, "Managed")
OUT = os.path.join(HERE, "uilabel_inventory.json")
DICT = os.path.join(ROOT, "_dict", "zh_cn.dict")


def asset_files():
    names = []
    for p in sorted(os.listdir(DATA)):
        full = os.path.join(DATA, p)
        if not os.path.isfile(full):
            continue
        if p.endswith(".assets") or p.startswith("level") or p == "mainData":
            names.append(p)
    return names


def read_dict():
    d = {}
    with open(DICT, "rb") as f:
        raw = f.read()
    if raw[:4] != b"CWH1":
        return d
    n = int.from_bytes(raw[4:8], "little")
    off = 8
    for _ in range(n):
        klen = int.from_bytes(raw[off:off + 2], "little"); off += 2
        k = raw[off:off + klen].decode("utf-8"); off += klen
        zlen = int.from_bytes(raw[off:off + 2], "little"); off += 2
        z = raw[off:off + zlen].decode("utf-8"); off += zlen
        d[k] = z
    return d


def main():
    import UnityPy
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

    gen = TypeTreeGenerator("5.2.3f1")
    gen.load_local_dll_folder(MANAGED)

    dic = read_dict()
    print("词表 %d 条" % len(dic))

    rows = []
    per_file = {}
    for fn in asset_files():
        t0 = time.time()
        try:
            env = UnityPy.load(os.path.join(DATA, fn))
            env.typetree_generator = gen
        except Exception as e:
            per_file[fn] = "load failed: %s" % e
            print("  %-26s load failed: %s" % (fn, e))
            continue

        go_name = {}
        for o in env.objects:
            if o.type.name != "GameObject":
                continue
            try:
                d = o.read_typetree()
            except Exception:
                continue
            go_name[o.path_id] = d.get("m_Name") or ""

        n_obj = 0
        n_txt = 0
        for o in env.objects:
            if o.type.name != "MonoBehaviour":
                continue
            try:
                d = o.read_typetree()
            except Exception:
                continue          # 缺脚本 dll 的 MonoBehaviour 解不出来，跳过
            if "mText" not in d:
                continue
            n_obj += 1
            t = d.get("mText")
            if not isinstance(t, str) or not t.strip():
                continue
            n_txt += 1
            g = d.get("m_GameObject") or {}
            rows.append(dict(
                file=fn, path_id=o.path_id,
                gameobject=go_name.get(g.get("m_PathID", 0), "?"),
                text=t,
                in_dict=t in dic,
                zh=dic.get(t),
            ))
        per_file[fn] = dict(objs=n_obj, with_text=n_txt,
                            sec=round(time.time() - t0, 1))
        print("  %-26s UILabel %4d 个（%d 个有文本）  %.1fs"
              % (fn, n_obj, n_txt, time.time() - t0))

    distinct = Counter(r["text"] for r in rows)
    in_d = [t for t in distinct if t in dic]
    miss = [t for t in distinct if t not in dic]

    print()
    print("=" * 72)
    print("总计: UILabel 有文本对象 %d 个" % len(rows))
    print("      唯一文本 %d 条（词表已有 %d 条，缺 %d 条）"
          % (len(distinct), len(in_d), len(miss)))
    print()
    by_file = Counter(r["file"] for r in rows)
    for fn, c in by_file.most_common():
        print("  %-26s %4d" % (fn, c))
    print()
    print("缺失 Top 30（按出现次数）:")
    for t, c in sorted(((t, distinct[t]) for t in miss), key=lambda x: -x[1])[:30]:
        print("  x%-3d %s" % (c, t.replace("\n", "\\n")[:70]))

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(dict(
            generated=time.strftime("%Y-%m-%d %H:%M:%S"),
            note="只读盘点，未修改任何游戏文件",
            per_file=per_file,
            totals=dict(objects=len(rows), distinct=len(distinct),
                        in_dict=len(in_d), missing=len(miss)),
            items=rows,
        ), f, ensure_ascii=False, indent=1)
    print()
    print("明细已写入 %s" % OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
