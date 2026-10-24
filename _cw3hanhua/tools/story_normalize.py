# -*- coding: utf-8 -*-
"""统一各批次自定的译名（只改 zh，不动 en / id）。

用法：
    .venv\\Scripts\\python.exe tools\\story_normalize.py          # 只报告
    .venv\\Scripts\\python.exe tools\\story_normalize.py --write  # 写回 *.out.json

规则表见 RULES；每条规则打一行命中统计，便于复核。
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
WORK = os.path.join(ROOT, "_dict", "story_work")

# (名称, 正则, 替换) —— 顺序执行
RULES = [
    ("Ticon 统一", re.compile(r"泰康|蒂康|泰孔"), "泰肯"),
    ("Prospector 统一", re.compile(r"勘探者"), "探矿者"),
    ("Tech Artifact 统一", re.compile(r"科技圣物"), "技术圣物"),
    ("Loki 巢群->巢穴", re.compile(r"洛基巢群"), "洛基巢穴"),
    ("Loki 帝皇->统领", re.compile(r"洛基帝皇"), "洛基统领"),
    ("Digitalis 统一", re.compile(r"Digitalis"), "魔苋"),
    # 换词后两侧会残留空格（`被 Digitalis 包围` → `被 魔苋 包围`），
    # 而 NGUI 折行时会把行尾空格改写成 \n → 界面上多出莫名回车。
    # 只在**另一侧是汉字**时删（`teks006 AC是关键` 这种拉丁-拉丁词间空格要留）。
    # 中英混排的全量规则见 fix_spaces.py。
    ("专名左清空格", re.compile(r"(?<=[\u4e00-\u9fff])[ \t](?=魔苋|Creeper|Anti-Creeper|AC)"), ""),
    ("专名右清空格", re.compile(r"(魔苋|Creeper|Anti-Creeper|AC)[ \t](?=[\u4e00-\u9fff])"), r"\1"),
    ("C.E. 纪元统一(公元)", re.compile(r"公元\s*(\d+)\s*年"), r"循环历 \1 年"),
    ("C.E. 纪元统一(第X纪元)", re.compile(r"第\s*(\d+)\s*纪元"), r"循环历 \1 年"),
    ("C.E. 纪元统一(第X循环纪)", re.compile(r"第\s*(\d+)\s*循环纪[元年]"), r"循环历 \1 年"),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    args = ap.parse_args()
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    total = {name: 0 for name, _, _ in RULES}
    for p in sorted(glob.glob(os.path.join(WORK, "*.out.json"))):
        d = json.load(io.open(p, encoding="utf-8-sig"))
        changed = 0
        for it in d["items"]:
            zh = it.get("zh") or ""
            new = zh
            for name, rx, rep in RULES:
                nxt = rx.sub(rep, new)
                if nxt != new:
                    total[name] += len(rx.findall(new))
                    new = nxt
            if new != zh:
                changed += 1
                it["zh"] = new
        if changed:
            print("%-28s 改动 %d 条" % (os.path.basename(p), changed))
            if args.write:
                with io.open(p, "w", encoding="utf-8", newline="\n") as f:
                    json.dump(d, f, ensure_ascii=False, indent=1)
    print("-" * 50)
    for name, _, _ in RULES:
        print("%-28s 命中 %d" % (name, total[name]))
    if not args.write:
        print("（未写回，加 --write 生效）")


if __name__ == "__main__":
    main()
