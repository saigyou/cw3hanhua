# -*- coding: utf-8 -*-
"""临时回归：验证 collect_ui_strings.classify 对边界串的判定（尤其 RE_REGEX 改动）。"""
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
sys.path.insert(0, r"_cw3hanhua/tools")
import collect_ui_strings as C

tests = [
    "[Music Credits]",                        # ★ 资产里的显示文本，必须判 ui
    "^[a-z0-9_]+$",                           # 真正则 -> noise
    "[^ 'a-zA-Z0-9_-]+$",                     # 真正则 -> noise
    "[ffffd0]Credits[ffffff]",                # 带色标的显示文本
    "[ff0000]To Access:\n[ff0000]Complete first 5 Arc Eternal missions.",
    "View Demo Screen",
    "CREEPER WORLD",
    "[00ff00]100",
    "Alpha3",
    "Time:1:00:00.0",
    "Collector",
]
for t in tests:
    print(repr(t), "->", C.classify(t))
