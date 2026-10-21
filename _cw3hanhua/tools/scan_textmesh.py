#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
只读盘点：CW3 全部资产文件里有多少个 TextMesh（Unity class 102），各自文本是什么。

用途：决定「要不要走改资产」这条路 —— 需要先知道总量，而不是主界面那 14 个。
      本脚本 **不写任何游戏文件**，只读 typetree 并输出报告。

产出：
    _cw3hanhua/tools/textmesh_inventory.json   明细（含父链路径，便于定位）
    stdout                                     统计摘要

用法：
    .venv\\Scripts\\python.exe tools\\scan_textmesh.py

★ 2026-10-21 从 `_diag/` 挪进 `tools/`：19 条 TextMesh 资产已经烘成中文，
  这里的 JSON 记的是烘焙前的英文原文，重跑会覆盖成现役（中文）状态。
"""
import json
import os
import sys
import time
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)                  # _cw3hanhua
GAME = os.path.dirname(ROOT)                  # 游戏根
DATA = os.path.join(GAME, "CW3_Data")
MANAGED = os.path.join(DATA, "Managed")
OUT = os.path.join(HERE, "textmesh_inventory.json")
DICT = os.path.join(ROOT, "_dict", "zh_cn.dict")

CLASS_GAMEOBJECT = 1
CLASS_TRANSFORM = 4
CLASS_TEXTMESH = 102


def asset_files():
    names = []
    for p in sorted(os.listdir(DATA)):
        full = os.path.join(DATA, p)
        if not os.path.isfile(full):
            continue
        if p.endswith(".assets") or p.startswith("level") or p == "mainData":
            names.append(p)
    return names


def read_dict_keys():
    """读 zh_cn.dict 的 key 集合（只为回答"这条是否已有译文"）。"""
    keys = set()
    if not os.path.exists(DICT):
        return keys
    try:
        with open(DICT, "rb") as f:
            raw = f.read()
        if raw[:4] != b"CWH1":
            return keys
        n = int.from_bytes(raw[4:8], "little")
        off = 8
        for _ in range(n):
            klen = int.from_bytes(raw[off:off + 2], "little"); off += 2
            k = raw[off:off + klen].decode("utf-8"); off += klen
            zlen = int.from_bytes(raw[off:off + 2], "little"); off += 2
            off += zlen                       # 不要 z，只要 key
            keys.add(k)
    except Exception as e:
        print("!! 读词表失败: %s" % e)
    return keys


def main():
    try:
        import UnityPy
        from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    except Exception as e:
        print("!! UnityPy / TypeTreeGenerator 不可用: %s" % e)
        return 1

    print("UnityPy %s" % UnityPy.__version__)
    gen = TypeTreeGenerator("5.2.3f1")
    gen.load_local_dll_folder(MANAGED)

    dict_keys = read_dict_keys()
    print("词表 key: %d 条" % len(dict_keys))

    rows = []
    per_file = {}
    for fn in asset_files():
        t0 = time.time()
        p = os.path.join(DATA, fn)
        try:
            env = UnityPy.load(p)
            env.typetree_generator = gen
        except Exception as e:
            per_file[fn] = "load failed: %s" % e
            print("  %-24s load failed: %s" % (fn, e))
            continue

        # 先把 GameObject 名字 / Transform 父指针收齐，才能还原"这个 TextMesh 在哪"
        go_name = {}      # pid -> GameObject 名
        go_active = {}    # pid -> m_IsActive
        tr_go = {}        # transform pid -> gameobject pid
        tr_father = {}    # transform pid -> father transform pid
        go_tr = {}        # gameobject pid -> transform pid
        meshes = []       # (pid, d)

        for sf in list(env.files.values()):
            if not hasattr(sf, "objects"):
                continue
            for pid, o in list(sf.objects.items()):
                cid = int(o.class_id) if o.class_id else 0
                if cid not in (CLASS_GAMEOBJECT, CLASS_TRANSFORM, CLASS_TEXTMESH):
                    continue
                try:
                    d = o.read_typetree()
                except Exception:
                    continue
                if not isinstance(d, dict):
                    continue
                if cid == CLASS_GAMEOBJECT:
                    go_name[pid] = d.get("m_Name") or ""
                    go_active[pid] = d.get("m_IsActive")
                elif cid == CLASS_TRANSFORM:
                    g = d.get("m_GameObject") or {}
                    fa = d.get("m_Father") or {}
                    tr_go[pid] = g.get("m_PathID", 0)
                    tr_father[pid] = fa.get("m_PathID", 0)
                    if g.get("m_PathID"):
                        go_tr[g["m_PathID"]] = pid
                else:
                    meshes.append((pid, d))

        def path_of(go_pid):
            """沿 Transform 父链拼出 'A/B/C'，找不到就退回对象名。"""
            parts = []
            tr = go_tr.get(go_pid, 0)
            guard = 0
            while tr and guard < 64:
                guard += 1
                g = tr_go.get(tr, 0)
                if g:
                    parts.append(go_name.get(g, "?"))
                tr = tr_father.get(tr, 0)
            if not parts:
                return go_name.get(go_pid, "?")
            return "/".join(reversed(parts))

        n_here = 0
        for pid, d in meshes:
            text = d.get("m_Text")
            if not isinstance(text, str):
                text = ""
            g = d.get("m_GameObject") or {}
            go_pid = g.get("m_PathID", 0)
            font = d.get("m_Font") or {}
            rows.append(dict(
                file=fn, path_id=pid,
                gameobject=go_name.get(go_pid, "?"),
                go_path=path_of(go_pid),
                go_active=go_active.get(go_pid),
                text=text,
                has_text=bool(text.strip()),
                in_dict=text in dict_keys if text else False,
                font_size=d.get("m_CharacterSize"),
                font_pid=font.get("m_PathID", 0),
            ))
            n_here += 1
        per_file[fn] = dict(total=len(meshes), with_text=n_here,
                            sec=round(time.time() - t0, 1))
        print("  %-24s TextMesh %4d 个（%d 个有文本）  %.1fs"
              % (fn, len(meshes), n_here, time.time() - t0))

    with_text = [r for r in rows if r["has_text"]]
    distinct = Counter(r["text"] for r in with_text)
    translated = [t for t in distinct if t in dict_keys]
    untranslated = [t for t in distinct if t not in dict_keys]

    print()
    print("=" * 72)
    print("总计: TextMesh 对象 %d 个" % len(rows))
    print("      有文本 %d 个 / 空文本 %d 个" % (len(with_text), len(rows) - len(with_text)))
    print("      唯一文本 %d 条（其中词表已有 %d 条，缺 %d 条）"
          % (len(distinct), len(translated), len(untranslated)))
    print()
    by_file = Counter(r["file"] for r in with_text)
    print("按文件分布（有文本的）:")
    for fn, c in by_file.most_common():
        print("  %-24s %4d" % (fn, c))
    print()
    print("文本出现次数 Top 20:")
    for t, c in distinct.most_common(20):
        mark = "有" if t in dict_keys else "缺"
        print("  [%s] x%-3d %s" % (mark, c, t.replace("\n", "\\n")[:70]))

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(dict(
            generated=time.strftime("%Y-%m-%d %H:%M:%S"),
            note="只读盘点，未修改任何游戏文件",
            per_file=per_file,
            totals=dict(objects=len(rows), with_text=len(with_text),
                        distinct=len(distinct), in_dict=len(translated),
                        missing=len(untranslated)),
            items=rows,
        ), f, ensure_ascii=False, indent=1)
    print()
    print("明细已写入 %s" % OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
