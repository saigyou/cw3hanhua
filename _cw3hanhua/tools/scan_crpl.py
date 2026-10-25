#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""扫关卡资产里内嵌的 CRPL 脚本，抽出全部字符串字面量（只读，不改任何文件）。

为什么需要它：有些文案不在 DLL 的 `#US` 堆里、也不在 TextAsset 明文里，
而是关卡自带的 CRPL 脚本 `ShowMessage("…")` —— 脚本以 base64(UTF-16LE)
存在资产 XML 的 `<Scripts><scripts>名字.crpl;BASE64</scripts></Scripts>` 里。
静态收集器（`collect_ui_strings.py`，只认 `ldstr` 与资产文本）收不到这一类。
2026-10-23 定位 Tempus 关教学提示就是靠这条链。

用法：
    ..\\.venv\\Scripts\\python.exe tools\\scan_crpl.py
    ..\\.venv\\Scripts\\python.exe tools\\scan_crpl.py --json _dict\\crpl_lits.json
    ..\\.venv\\Scripts\\python.exe tools\\scan_crpl.py --asset Tempus --raw

输出：每个资产的 ShowMessage 调用数 + 字面量清单（`repr()` 形式，换行看得见）。
`--json` 另存结构化结果，便于喂给批次脚本。

★ 三个踩过的坑（改这个脚本前先读）：
  1. UnityPy 读 TextAsset 的 `m_Script` 返回的是 **str 不是 bytes**，
     必须 `encode("utf-8","surrogateescape")` 才拿得回原始字节，
     否则前 3 字节的判断永远不成立、LZMA 解压必然失败（曾因此 0 命中）。
  2. 抓字面量**必须整篇 `finditer`**，不能按 `split("\\n")` 逐行做 ——
     `alpha_Troublesome Trains 01` 的脚本整篇只有 1 行（内含 264 个真 CRLF），
     逐行处理会几乎全漏。
  3. ★★ **一个 `<scripts>` 块里可以有多个脚本**，形态是
     `名字1.crpl;BASE64_1,名字2.crpl;BASE64_2,...` —— 旧版按 `;` 切再逐个
     base64 解码，遇到多脚本关卡的**第一段之后全部解不出来**（`BASE64_1,名字2.crpl`
     里混进了逗号和下一个文件名，`b64decode` 要么抛异常被静默 `continue`
     跳过、要么在补齐填位后**静默解出乱码**）。结果就是 Far York / Arca /
     Farbor / credits / alpha_Electricity Quiz 等**整关的 CRPL 文案一条都收不到**，
     而且不报错 —— 2026-10-24 排查 `Mortars damage creeper that is deep.`
     才发现的。所以必须按 `,` 切条目、每条首段才是文件名。
"""
import argparse
import base64
import io
import json
import lzma
import os
import re
import sys

# 字面量：允许 \" \\ 之类的转义；re.S 让 "…" 里可以跨行
LIT = re.compile(r'"((?:[^"\\]|\\.)*)"', re.S)
SCRIPTS = re.compile(r"<scripts>(.*?)</scripts>", re.S)

# 单个脚本条目：`名字.crpl;BASE64`。多个条目之间用 ',' 分隔。
# ★ 绝不能先 split(";") —— base64 里没有分号，但**逗号是文件名字符集之外的分隔符**，
#   而 base64 字母表也不含逗号，所以按 ',' 切是安全的；切完后每条再按第一个 ';' 分。
ENTRY = re.compile(r"([^,;]*?\.crpl)\s*;\s*([A-Za-z0-9+/=\s]+)", re.S)


def parse_scripts(block):
    """把 `<scripts>` 的内容解析成 [(文件名, 源码)]。

    形态：`名字1.crpl;BASE64_1,名字2.crpl;BASE64_2,...`
    —— 必须先按 ',' 切条目，不能先按 ';' 切（会把下一个文件名并进 base64）。
    """
    out = []
    for name, b64 in ENTRY.findall(block):
        s = re.sub(r"\s", "", b64)
        if len(s) < 40:
            continue
        s += "=" * (-len(s) % 4)
        try:
            raw = base64.b64decode(s, validate=False)
        except Exception:
            continue
        if len(raw) % 2:
            continue
        try:
            src = raw.decode("utf-16-le")
        except Exception:
            src = raw.decode("utf-8", "ignore")
        if "\x00" in src:
            continue
        out.append((name.strip(), src))
    return out


def to_bytes(s):
    """UnityPy 的 m_Script 是 str；还原成原始字节。已是 bytes 就原样返回。"""
    if isinstance(s, (bytes, bytearray)):
        return bytes(s)
    return s.encode("utf-8", "surrogateescape")


def text_of(s):
    """TextAsset 正文。前 3 字节是 LZMA alone 魔数就解压，否则按 UTF-8/Latin-1 读。"""
    b = to_bytes(s)
    if b[:3] == b"\x5d\x00\x00":
        try:
            return lzma.decompress(b, format=lzma.FORMAT_ALONE).decode("utf-8", "replace")
        except Exception:
            pass
    try:
        return b.decode("utf-8")
    except Exception:
        return b.decode("latin-1")


def unescape(s):
    """CRPL 源码里的 \\n / \\t / \\r 是两字符转义，运行时才成真字符。

    这里提前还原，好让词表 key 与实际显示串一致
    （`CrplCore.ProcessCommands` 里就是 Replace("\\\\n","\\n") / ("\\\\t","\\t")）。
    """
    out = []
    i = 0
    n = len(s)
    while i < n:
        c = s[i]
        if c == "\\" and i + 1 < n:
            d = s[i + 1]
            out.append("\n" if d == "n" else "\t" if d == "t" else "\r" if d == "r" else d)
            i += 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def strip_comments(src):
    """丢掉整行 `#` 注释。CRPL 没有行内注释语法。"""
    return "\n".join(l for l in src.split("\n") if not l.lstrip().startswith("#"))


def looks_like_prose(s):
    """过滤掉代码碎片：太短、无空格、无字母、含 CRPL 的 -> / <- 赋值箭头。"""
    if len(s) < 8:
        return False
    if " " not in s:
        return False
    if not any(ch.isalpha() for ch in s):
        return False
    if "->" in s or "<-" in s:
        return False
    return True


# 会真上屏的对话/提示调用：`ShowMessage("…")`、`ShowMessageDialog("…")`、
# `AddConversationMessage(0 "…")`、`AddMessage("…")`。第一个参数可能是说话人编号
# 或颜色，所以 `[^"]*?` 放在引号前面（不跨引号）。
CALL = re.compile(
    r'\b(?:ShowMessage\w*|AddConversationMessage|AddMessage)\s*\(\s*[^"()]*?"((?:[^"\\]|\\.)*)"',
    re.S)


def call_literals_of(src):
    """只取**对话/提示调用实参**里的字面量，**不套 `looks_like_prose`**。

    ★ 为什么需要（2026-10-25，用户报 `Lies!` 没翻）：`looks_like_prose()` 要求
      `len>=8` 且含空格，而剧情对话里天然有大量短句 —— `Lies!` / `Never!` /
      `Oh...?` / `How?!?` / `Noooooooooo!!!!!` / `...'Platius'...` 全被过滤掉，
      于是 `strings.json` 里根本没有它们，插件查不到就原样显示英文。
      这些调用实参**必然上屏**，长度门槛对它们没有意义，所以这里无条件收。
    """
    seen = []
    for m in CALL.finditer(strip_comments(src)):
        s = unescape(m.group(1))
        if not s.strip():
            continue
        if "->" in s or "<-" in s:      # 赋值箭头 = 代码片段，不是文案
            continue
        if not any(ch.isalpha() for ch in s):
            continue
        if s not in seen:
            seen.append(s)
    return seen


def literals_of(src):
    body = strip_comments(src)
    seen = []
    for m in LIT.finditer(body):
        s = unescape(m.group(1))
        if looks_like_prose(s) and s not in seen:
            seen.append(s)
    for s in call_literals_of(body):
        if s not in seen:
            seen.append(s)
    return seen


def scan(assets_dir, only_asset):
    """遍历 assets 目录，返回 [{'file','asset','len','calls','scripts':[{'name','src'}]}]。"""
    results = []
    for fn in sorted(os.listdir(assets_dir)):
        if not fn.endswith(".assets"):
            continue
        path = os.path.join(assets_dir, fn)
        if not os.path.isfile(path):
            continue
        try:
            import UnityPy
            env = UnityPy.load(path)
            objs = list(env.objects)
        except Exception as e:
            print("load fail %s: %s" % (fn, e), file=sys.stderr)
            continue

        for o in objs:
            if o.type.name != "TextAsset":
                continue
            try:
                d = o.read()
                name = d.m_Name
                txt = text_of(d.m_Script)
            except Exception:
                continue
            if "<scripts>" not in txt:
                continue
            if only_asset and name != only_asset:
                continue

            scripts = []
            for m in SCRIPTS.finditer(txt):
                for sname, src in parse_scripts(m.group(1)):
                    scripts.append({"name": sname, "src": src})

            if not scripts:
                continue
            results.append({
                "file": fn,
                "asset": name,
                "len": sum(len(s["src"]) for s in scripts),
                "calls": sum(len(re.findall(r"ShowMessage\w*\(", s["src"])) for s in scripts),
                "scripts": scripts,
            })
    return results


def main():
    ap = argparse.ArgumentParser(description="扫关卡内嵌 CRPL 脚本的字面量（只读）")
    ap.add_argument("--assets", default="CW3_Data", help="资产目录（默认 CW3_Data）")
    ap.add_argument("--asset", default=None, help="只处理这个 TextAsset 名")
    ap.add_argument("--json", default=None, help="另存结构化结果到该路径")
    ap.add_argument("--raw", action="store_true", help="额外打印 CRPL 源码全文")
    args = ap.parse_args()

    if not os.path.isdir(args.assets):
        print("找不到资产目录：%s（请在游戏根目录下运行）" % args.assets, file=sys.stderr)
        return 2

    results = scan(args.assets, args.asset)
    payload = []
    total = 0
    for r in results:
        # ★ 必须遍历**全部**脚本，不能只看 scripts[0] —— 旧版只看第一个，
        #   害得 Far York（2 个脚本，目标字面量在第 2 个里）一直 lits=0。
        lits, seen = [], set()
        for sc in r["scripts"]:
            for s in literals_of(sc["src"]):
                if s not in seen:
                    seen.add(s)
                    lits.append(s)
        total += len(lits)
        print("=" * 78)
        print("ASSET: %-44s calls=%-3d scripts=%-2d lits=%d"
              % (r["asset"], r["calls"], len(r["scripts"]), len(lits)))
        for s in lits:
            print("   " + repr(s))
        if args.raw:
            for sc in r["scripts"]:
                print("-" * 78)
                print("# " + sc["name"])
                print(sc["src"])
        payload.append({
            "file": r["file"], "asset": r["asset"], "len": r["len"], "calls": r["calls"],
            "scripts": len(r["scripts"]), "strings": lits,
        })

    print("\n资产数: %d   字面量: %d" % (len(results), total))

    if args.json:
        out_dir = os.path.dirname(os.path.abspath(args.json))
        if out_dir and not os.path.isdir(out_dir):
            os.makedirs(out_dir)
        with io.open(args.json, "w", encoding="utf-8", newline="\n") as f:
            json.dump(payload, f, ensure_ascii=False, indent=1)
        print("wrote", args.json)
    return 0


if __name__ == "__main__":
    sys.exit(main())
