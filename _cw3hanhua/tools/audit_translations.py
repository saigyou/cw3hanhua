#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""译文一致性审计：逐条比对 en / zh 的 [颜色标记]、换行数、占位符、首尾空白，
外加**按组**的引号配对校验（跨条目，见 NOTES 4.17）。

用法：
    .venv\\Scripts\\python.exe tools\\audit_translations.py
    .venv\\Scripts\\python.exe tools\\audit_translations.py --max 30   # 每类最多列 30 条

五项全为 0 才算过。改完译文（尤其手工补的）都该跑一次；
`apply_translations.py` 只保证"原文能匹配上"，不保证标记/空白没被译丢。

前四项是**逐条**的；第五项「引号配对」必须是**跨条目**的 —— 引擎把一句台词
拆成多条独立字符串（`PreludeManager/EpilogueManager/DemoScreenManager.ShowText`），
引号于是开在 A 条、闭在 B 条，单条看永远"合法"，只有按原顺序累计奇偶才发现漏配。
"""
import argparse
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
P = os.path.join(ROOT, "_dict", "strings.json")

# [ffff00] 这类富文本色标；{0} %s <b> 这类占位符
TAG = re.compile(r"\[[^\[\]\n]{1,20}\]")
PH = re.compile(r"\{[^{}\n]{1,12}\}|%[sdf]|<[a-zA-Z/][^<>\n]{0,12}>")

# 汉字 / 中文标点 / 全角符号：zh 以这些开头时，前面的半角空格属于
# 「中文与拉丁/数字之间」的空隙，按 4.13 该删（`' spores'` → `'枚孢子'`）。
CJK_HEAD = re.compile(r"[\u4e00-\u9fff\u3000-\u303f\uff01-\uff60]")

# 引号配对校验（第五项）的组前缀：note 以此开头的一组条目是被引擎
# 拆条显示的同一段台词，引号只能在**整组**里配对（NOTES 4.17）。
QUOTE_GROUP = "\u8fc7\u573a\u53d9\u4e8b\u53f0\u8bcd"


def lead_ok(en, zh, le):
    """前导空白校验。

    默认：en 的前导空白必须逐字保留（`'  地形：'`、`'  [ffffff]升级至等级：'`
    靠它缩进对齐）。唯一放行：en 前导是**纯空格**、而 zh 以中文开头 ——
    这类是拼接词缀（`' spores'` → `'枚孢子'`，拼出 `1枚孢子`），
    留空格就成了「1 枚孢子」，违反 4.13 定的「中英混排不加空格」。
    """
    if not le:
        return True
    head = en[:le]
    if zh[:le] == head:
        return True
    return head.strip(" ") == "" and bool(CJK_HEAD.match(zh))


def quote_parity_bad(S):
    """按组校验引号配对（跨条目，见 NOTES 4.17）。

    引擎会把**一句台词拆成多条独立字符串**（`ShowText` 的实参逐条进词表），
    于是 `"` 开在 A 条、闭在 B 条 —— 单条看两边都"合法"，永远查不出来。
    只有按 `strings.json` 的原始顺序累计奇偶，才暴露漏配的那一条：
    `pq` 累计 en 的引号数、`pz` 累计 zh 的「」净数，任何一步不等即有漏。

    组键取 `note`：同一批被拆条的台词共用一条 note
    （「过场叙事台词：Prelude/Epilogue/Demo 的 ShowText 实参」）。
    """
    groups = {}
    for i, it in enumerate(S):
        n = it.get("note") or ""
        if n.startswith(QUOTE_GROUP):
            groups.setdefault(n, []).append((i, it))

    bad = []
    for _n, items in groups.items():
        pq = pz = 0
        ok = True
        for i, it in items:
            en, zh = it["en"], it.get("zh") or ""
            pe = en.count('"') + en.count("\u201c") + en.count("\u201d")
            if not zh:          # 未译 ⇒ 屏幕上还是英文，奇偶照原样走
                pq = (pq + pe) % 2
                pz = (pz + pe) % 2
            else:
                pq = (pq + pe) % 2
                # zh 的「」成对；ASCII " 是键位名/占位符字形（'"SHIFT-T"'），
                # 本身成对出现，计进去不影响奇偶。
                pz = (pz + zh.count("\u300c") - zh.count("\u300d")
                      + zh.count('"')) % 2
            now_ok = pq == pz
            # 只报**失衡起点**：一旦累计奇偶错开，后面每条都会「错」，
            # 全列出来只是噪音；起点那一条才是要补括号的地方。
            if ok and not now_ok:
                bad.append((i, en, zh))
            ok = now_ok
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--max", type=int, default=15, help="每类最多列几条")
    a = ap.parse_args()

    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    d = json.load(open(P, encoding="utf-8"))
    S = d["strings"]

    tag_bad, nl_bad, ph_bad, sp_bad = [], [], [], []
    for i, it in enumerate(S):
        en, zh = it["en"], it.get("zh") or ""
        if not zh:
            continue
        if TAG.findall(en) != TAG.findall(zh):
            tag_bad.append((i, en, zh))
        if en.count("\n") != zh.count("\n"):
            nl_bad.append((i, en, zh))
        if PH.findall(en) != PH.findall(zh):
            ph_bad.append((i, en, zh))
        # 前导空白必须逐字保留：靠它缩进对齐（'  地形：'）
        # 尾随空白：en 尾部若只是空格（拼接前缀，如 'Collected: '），
        # zh 允许整段删掉——中文全角标点自带间隔，无需再缀半角空格（见 tools/fix_tail_space.py）。
        # 但 en 尾部若含换行/制表符，仍必须逐字保留。
        le = len(en) - len(en.lstrip())
        te = len(en) - len(en.rstrip())
        tail_en = en[len(en) - te:] if te else ""
        bad = not lead_ok(en, zh, le)
        if not bad and te:
            if tail_en.strip(" ") == "":      # en 尾部纯空格 → zh 可留可删
                bad = not (zh[len(zh) - te:] == tail_en or not zh.endswith(" "))
            else:                              # 含 \n \t → 必须一字不差
                bad = zh[len(zh) - te:] != tail_en
        if bad:
            sp_bad.append((i, en, zh))

    q_bad = quote_parity_bad(S)

    total = 0
    for name, arr in (("标记", tag_bad), ("换行", nl_bad),
                      ("占位符", ph_bad), ("首尾空白", sp_bad),
                      ("引号配对", q_bad)):
        total += len(arr)
        print("== %s: %d" % (name, len(arr)))
        for i, en, zh in arr[:a.max]:
            print("  %d  %r -> %r" % (i, en, zh))
    print("\n%s" % ("✓ 五项全过" if total == 0 else "✗ 共 %d 处问题" % total))
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main())
