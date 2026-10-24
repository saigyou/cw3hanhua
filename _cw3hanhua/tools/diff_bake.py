# -*- coding: utf-8 -*-
"""只读回归检查：确认烘焙只动了 TextMesh 的 m_Text，没有波及任何 UILabel。

★ 为什么不去解析 `_cw3hanhua/_assets_orig/` 里的备份来做对比 —— 试过，行不通：
  备份文件离开了 `CW3_Data/` 就解析不了 MonoBehaviour 的 typetree，UnityPy 会报
  `FileNotFoundError: File sharedassets0.assets not found in ...\\_assets_orig`
  （这些 MonoBehaviour 的 MonoScript 要跨文件引用 sharedassets0，而备份目录里没有它）。
  实测：备份 0/1827 成功、现役 1807/1827 成功。**备份目录只能拿来整体拷回，不能就地读。**

★ 所以改用「基线快照对比」：`_cw3hanhua/tools/uilabel_inventory.json` 是烘焙**之前**
  扫原件留下的 1029 条 UILabel 记录（烘焙脚本压根不碰 UILabel，见 bake_textmesh_assets.py
  的 apply 只写 TextMesh）。现在重扫现役资产，逐条对比基线 —— 有任何差异都说明波及了 UILabel。
  基线由同目录的 scan_uilabel.py 生成；**它是不可再生的**（游戏资产已被烘成中文，
  重扫拿到的是现役状态，不是烘焙前），只能当历史快照用，别覆盖。

★ 基线口径：`uilabel_inventory.json` 只记**有文本**的 label（1029 条 / 503 个唯一串），
  全场实际有 1206 个 UILabel，多出的 177 个 `mText` 是空串。所以对比前必须把空串滤掉，
  否则会把那 177 个空 label 误报成「新增」。实测：滤空后 1206→1029，与基线正好对齐。

用法（★ 必须用系统 python，见 NOTES 4.6 的 Low 完整性坑）：
  $env:PYTHONPATH = "<根>\\_cw3hanhua\\.venv\\Lib\\site-packages"
  & "<系统 python 的路径（本机是 python314）>" _cw3hanhua\\tools\\diff_bake.py
"""
import json
import os
import sys

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATA = os.path.join(ROOT, "CW3_Data")
MANAGED = os.path.join(DATA, "Managed")
BASELINE = os.path.join(ROOT, "_cw3hanhua", "tools", "uilabel_inventory.json")

FILES = ["level1", "level2", "level3", "level4", "level5", "level6", "level7",
         "mainData", "resources.assets", "sharedassets4.assets", "sharedassets6.assets"]


def scan_labels(gen):
    """返回 {(file, pid): text}，只读现役资产。"""
    out = {}
    for fn in FILES:
        p = os.path.join(DATA, fn)
        if not os.path.exists(p):
            continue
        try:
            env = UnityPy.load(p)
            env.typetree_generator = gen
        except Exception as e:
            print("  !! %s 打不开: %s" % (fn, e))
            continue
        for o in env.objects:
            if o.type.name != "MonoBehaviour":
                continue
            try:
                d = o.read_typetree()
            except Exception:
                continue
            if isinstance(d, dict) and "mText" in d:
                t = d.get("mText") or ""
                if t:                       # ★ 基线只记非空，这里也要滤空（见文件头）
                    out[(fn, o.path_id)] = t
        print("  %-22s %d 个 UILabel" % (fn, sum(1 for k in out if k[0] == fn)))
    return out


def main():
    if not os.path.exists(BASELINE):
        print("!! 基线不存在: %s" % BASELINE)
        return 2
    base = json.load(open(BASELINE, encoding="utf-8"))
    old = {(it["file"], it["path_id"]): it["text"] for it in base["items"]}
    print("基线（烘焙前扫的原件）: %d 条 UILabel，generated=%s"
          % (len(old), base.get("generated")))

    gen = TypeTreeGenerator("5.2.3f1")
    gen.load_local_dll_folder(MANAGED)
    print("\n重扫现役资产:")
    cur = scan_labels(gen)

    changed = [(k, old[k], cur[k]) for k in old if k in cur and old[k] != cur[k]]
    lost = [k for k in old if k not in cur]
    added = [k for k in cur if k not in old]

    print("\n" + "=" * 56)
    print("UILabel 共 %d 条（基线 %d）" % (len(cur), len(old)))
    print("  文本变了  : %d 条（应 = 0）" % len(changed))
    print("  消失了    : %d 条（应 = 0）" % len(lost))
    print("  新增了    : %d 条（应 = 0）" % len(added))
    for k, a, b in changed[:30]:
        print("  !! %s pid=%s  %r -> %r" % (k[0], k[1], a, b))
    for k in lost[:30]:
        print("  !! 丢失 %s pid=%s  %r" % (k[0], k[1], old[k]))
    for k in added[:30]:
        print("  ?? 新增 %s pid=%s  %r" % (k[0], k[1], cur[k]))

    if changed or lost or added:
        print("\n!! 有 UILabel 被波及 —— 烘焙脚本不该碰这些")
        return 1
    print("\nOK：1029 条 UILabel 与烘焙前的基线逐条一致，完全未被触碰")
    return 0


if __name__ == "__main__":
    sys.exit(main())
