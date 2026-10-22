# -*- coding: utf-8 -*-
"""合并 + 校验剧情翻译批次。

用法：
    .venv\\Scripts\\python.exe tools\\story_merge.py [--write]

校验项：
  * 覆盖率：579 条台词是否全部有译文（一个不多一个不少）
  * 换行：译文的 '\\n' 个数必须与原文一致（对话里空行的排版靠它）
  * 残留英文：除白名单（Creeper/Anti-Creeper/AC/CAIN/Odin/数字/按键/拉丁语）外不应有 ASCII 字母
  * 长度：中文字数 / 英文字符 比例过低说明漏句
  * 句数：原文句末标点数 vs 译文句末标点数

--write 时把译文写回 _dict/strings.json（新增 grade=other, cat=asset, kind=Dialog 的条目）。
"""
import argparse
import glob
import io
import json
import os
import re
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict")
WORK = os.path.join(DICT, "story_work")
CAND = os.path.join(DICT, "story_candidates.json")
STRINGS = os.path.join(DICT, "strings.json")

ALLOW_EN = re.compile(
    r"Creeper|Anti-Creeper|\bAC\b|\bCAIN\b|\bOdin\b|Fortuna favet fatuis|"
    r"\bWASD\b|\bSPACE\b|\bTAB\b|\bSHIFT\b|\bESC\b|\bCTRL\b|\bALT\b|\bDMD\b|\bCN\b|"
    r"\bCPU\b|\bAI\b|\bEULA\b|\bURL\b|\bOK\b")
ZH_PUNCT = "，。？！；：、（）「」《》…—·"
HAN = re.compile(r"[\u4e00-\u9fff]")


def load_out():
    """收集所有 .out.json（names.out.json 结构不同，单独处理）。"""
    zh_by_id = {}
    dup = []
    for p in sorted(glob.glob(os.path.join(WORK, "*.out.json"))):
        if os.path.basename(p) in ("names.out.json", "12_extra.out.json"):
            continue
        d = json.load(io.open(p, encoding="utf-8-sig"))
        for it in d["items"]:
            i = it["id"]
            if i in zh_by_id:
                dup.append(i)
            zh_by_id[i] = (it.get("zh") or "").strip()
    return zh_by_id, dup


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--sync", action="store_true",
                    help="把词表里 kind=Dialog 的条目的 zh 用 out 文件覆盖（用于统一译名后同步）")
    args = ap.parse_args()
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    cand = json.load(io.open(CAND, encoding="utf-8"))
    msgs = cand["messages"]
    zh_by_id, dup = load_out()

    errs, warns, ok = [], [], 0
    for i, m in enumerate(msgs):
        en = m["en"]
        if i not in zh_by_id:
            errs.append("缺译 #%d: %r" % (i, en[:60]))
            continue
        zh = zh_by_id[i]
        if not zh:
            errs.append("空译文 #%d: %r" % (i, en[:60]))
            continue
        if zh.count("\n") != en.count("\n"):
            errs.append("换行数不符 #%d en=%d zh=%d: %r -> %r"
                        % (i, en.count("\n"), zh.count("\n"), en[:50], zh[:50]))
            continue
        if zh == en and not HAN.search(en):
            # 原样搬运的乱码串（brief 规则 8），允许
            ok += 1
            continue
        if not HAN.search(zh) and re.search(r"[A-Za-z]", en):
            errs.append("无中文 #%d: %r -> %r" % (i, en[:50], zh[:50]))
            continue
        # 残留英文
        stripped = ALLOW_EN.sub("", zh)
        leftover = re.findall(r"[A-Za-z]{2,}", stripped)
        if leftover:
            warns.append("残留英文 #%d %s: %s" % (i, leftover[:6], zh[:70]))
        # 长度
        ratio = len(HAN.findall(zh)) / max(1, len(re.findall(r"[A-Za-z]", en)) + len(re.findall(r"[A-Za-z]", en)) * 0.0 + 1)
        en_words = len(re.findall(r"[A-Za-z']+", en))
        zh_chars = len(HAN.findall(zh))
        if en_words >= 6 and zh_chars / en_words < 0.35:
            warns.append("疑似漏句 #%d en_words=%d zh_chars=%d: %r -> %r"
                         % (i, en_words, zh_chars, en[:60], zh[:60]))
        # 句数
        en_s = len(re.findall(r"[.!?](?=\s|$)", en))
        zh_s = len(re.findall(r"[。！？]", zh))
        if en_s >= 3 and zh_s < en_s - 1:
            warns.append("句数偏少 #%d en=%d zh=%d: %r" % (i, en_s, zh_s, zh[:60]))
        ok += 1

    extra = [i for i in zh_by_id if i >= len(msgs)]
    print("=" * 70)
    print("台词条目: %d  有译文: %d  通过: %d  错误: %d  警告: %d"
          % (len(msgs), len(zh_by_id), ok, len(errs), len(warns)))
    if dup:
        print("重复 id:", dup[:20])
    if extra:
        print("多余 id:", extra[:20])
    for e in errs[:40]:
        print("  ERR ", e)
    for w in warns[:40]:
        print("  WARN", w)
    if len(warns) > 40:
        print("  ... 其余警告 %d 条" % (len(warns) - 40))

    # 名字
    names_path = os.path.join(WORK, "names.out.json")
    n_err = 0
    if os.path.exists(names_path):
        nd = json.load(io.open(names_path, encoding="utf-8-sig"))
        nm = {it["en"]: it["zh"] for it in nd["items"]}
        want = {n for _, n in cand["names"]}
        miss = [n for n in want if n not in nm]
        print("名字: 输入 %d  输出 %d  缺 %d" % (len(want), len(nm), len(miss)))
        if miss:
            print("  缺:", miss[:15])
        for k, v in nm.items():
            if not HAN.search(v or ""):
                print("  名字无中文:", k, "->", v)
                n_err += 1
    else:
        print("名字: 还没有 names.out.json")

    if args.write and not errs:
        S = json.load(io.open(STRINGS, encoding="utf-8"))
        # ★ 词表里已存在但 zh 为空的条目（采集到却没译）必须**补写**，不能跳过
        index = {x["en"]: x for x in S["strings"]}
        added, filled, synced, conflict = 0, 0, 0, []

        def put(en, zh, kind, note, files=None):
            nonlocal added, filled, synced
            zh = (zh or "").strip()
            if not zh:
                return
            cur = index.get(en)
            if cur is None:
                cur = {"en": en, "zh": zh, "cat": "asset", "grade": "other",
                       "kind": kind, "files": files or [], "refs": [], "hits": 1,
                       "note": note}
                S["strings"].append(cur)
                index[en] = cur
                added += 1
            elif not (cur.get("zh") or "").strip():
                cur["zh"] = zh
                if kind and not (cur.get("kind") or "").strip():
                    cur["kind"] = kind
                if note:
                    cur["note"] = (cur.get("note") or "") + "；" + note
                filled += 1
            elif args.sync and cur.get("kind") == "Dialog" and cur["zh"] != zh:
                cur["zh"] = zh
                synced += 1
            elif cur["zh"] != zh:
                conflict.append((en, cur["zh"], zh))

        for i, m in enumerate(msgs):
            put(m["en"], zh_by_id[i], "Dialog",
                "剧情台词（resources.assets TextAsset / LZMA XML <m>）", m["files"])
        if os.path.exists(names_path):
            nd = json.load(io.open(names_path, encoding="utf-8-sig"))
            for it in nd["items"]:
                put(it["en"], it.get("zh"), it.get("kind") or "Name",
                    "星球/星系/任务名（TextAsset XML <n>/<g>）")
        extra_path = os.path.join(WORK, "12_extra.out.json")
        if os.path.exists(extra_path):
            ed = json.load(io.open(extra_path, encoding="utf-8-sig"))
            for it in ed["items"]:
                put(it["en"], it.get("zh"), it.get("kind") or "Name",
                    "星系图/地图物件名（story_sector、ShieldKey）")
        S["generated"] = time.strftime("%Y-%m-%d %H:%M:%S")
        with io.open(STRINGS, "w", encoding="utf-8", newline="\n") as f:
            json.dump(S, f, ensure_ascii=False, indent=1)
        print("写回 strings.json：新增 %d 条，补空译 %d 条，同步 %d 条，总计 %d 条"
              % (added, filled, synced, len(S["strings"])))
        if conflict:
            print("已有不同译文 %d 条（保留原译，人工复核）:" % len(conflict))
            for en, old, new in conflict[:40]:
                print("  %r: 原 %r / 新 %r" % (en, old, new))
    elif args.write:
        print("有错误，未写回 strings.json")


if __name__ == "__main__":
    main()
