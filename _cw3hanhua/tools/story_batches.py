# -*- coding: utf-8 -*-
"""把 story_candidates.json 切成翻译批次：一个批次可含多个源文件，但绝不拆开单个文件。

输出 _cw3hanhua\\_dict\\story_work\\NN_<batch>.json 与 _index.json
"""
import io
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict")
WORK = os.path.join(DICT, "story_work")
SRC = os.path.join(DICT, "story_candidates.json")

MAX_PER_BATCH = 60


def main():
    cand = json.load(io.open(SRC, encoding="utf-8"))
    msgs = cand["messages"]
    # 每个文件 -> 条目 id 列表（保持语料顺序）
    by_file = {}
    for i, m in enumerate(msgs):
        by_file.setdefault(m["files"][0], []).append(i)
    # 一个句子出现在多个文件里时，files[0] 只是主文件；按优先级排文件
    def prio(fn):
        if fn.startswith("story_"):
            return (0, fn)
        if fn.startswith("alpha_"):
            return (2, fn)
        if fn == "credits":
            return (3, fn)
        return (1, fn)

    files = sorted(by_file, key=prio)
    batches, cur, cur_n = [], [], 0
    for fn in files:
        ids = by_file[fn]
        if cur and cur_n + len(ids) > MAX_PER_BATCH:
            batches.append(cur)
            cur, cur_n = [], 0
        cur.append(fn)
        cur_n += len(ids)
    if cur:
        batches.append(cur)

    os.makedirs(WORK, exist_ok=True)
    index = []
    for bi, files_in in enumerate(batches, 1):
        tag = files_in[0]
        tag = "story_sys" if tag.startswith("story_") else (
            "alpha" if tag.startswith("alpha_") else ("credits" if tag == "credits" else "campaign"))
        name = "%02d_%s" % (bi, tag)
        items = []
        for fn in files_in:
            for i in by_file[fn]:
                items.append({"id": i, "file": fn, "char": msgs[i]["char"], "en": msgs[i]["en"]})
        out = {"batch": name, "files": files_in, "count": len(items), "items": items}
        path = os.path.join(WORK, name + ".in.json")
        with io.open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(out, f, ensure_ascii=False, indent=1)
        index.append({"batch": name, "files": files_in, "count": len(items)})
        print("%-22s %3d 条  %s" % (name, len(items), ",".join(files_in)))

    with io.open(os.path.join(WORK, "_index.json"), "w", encoding="utf-8") as f:
        json.dump(index, f, ensure_ascii=False, indent=1)
    print("批次总数", len(index), "条目", sum(x["count"] for x in index))


if __name__ == "__main__":
    main()
