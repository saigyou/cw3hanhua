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

★ 两个踩过的坑（改这个脚本前先读）：
  1. UnityPy 读 TextAsset 的 `m_Script` 返回的是 **str 不是 bytes**，
     必须 `encode("utf-8","surrogateescape")` 才拿得回原始字节，
     否则前 3 字节的判断永远不成立、LZMA 解压必然失败（曾因此 0 命中）。
  2. 抓字面量**必须整篇 `finditer`**，不能按 `split("\\n")` 逐行做 ——
     `alpha_Troublesome Trains 01` 的脚本整篇只有 1 行（内含 264 个真 CRLF），
     逐行处理会几乎全漏。
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


def literals_of(src):
    body = strip_comments(src)
    seen = []
    for m in LIT.finditer(body):
        s = unescape(m.group(1))
        if looks_like_prose(s) and s not in seen:
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
                # 形态：名字.crpl;BASE64   （按 ';' 切，第一段是文件名）
                for part in m.group(1).split(";"):
                    part = part.strip()
                    if len(part) < 40:
                        continue
                    try:
                        src = base64.b64decode(part).decode("utf-16-le")
                    except Exception:
                        continue
                    if "ShowMessage" not in src:
                        continue
                    scripts.append({"name": "", "src": src})

            if not scripts:
                continue
            results.append({
                "file": fn,
                "asset": name,
                "len": len(scripts[0]["src"]),
                "calls": len(re.findall(r"ShowMessage\w*\(", scripts[0]["src"])),
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
        lits = literals_of(r["scripts"][0]["src"])
        total += len(lits)
        print("=" * 78)
        print("ASSET: %-44s calls=%-3d lits=%d" % (r["asset"], r["calls"], len(lits)))
        for s in lits:
            print("   " + repr(s))
        if args.raw:
            print("-" * 78)
            print(r["scripts"][0]["src"])
        payload.append({
            "file": r["file"], "asset": r["asset"],
            "len": r["len"], "calls": r["calls"], "strings": lits,
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
