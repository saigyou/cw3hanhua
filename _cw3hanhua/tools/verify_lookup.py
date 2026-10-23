# -*- coding: utf-8 -*-
"""离线复刻插件 `TextPlugin.Lookup` 的四级查表链，用来在不启动游戏的情况下
验证词表改动 / 预测某条文本会不会被翻。

四级链（与 `_cw3hanhua/plugin/CW3HanhuaText.cs` 一一对应，改插件时这里也要改）：
    1. 精确匹配          dict[s]
    2. 换行拼接          LookupConcat   —— 在 '\\n' 处切两段，两段各自精确命中才拼接
    3. 归一化             LookupNormalized —— CRLF/连续空格/尾部空白抹平后再查
    4. 分段重组          LookupSegment  —— 碎片逐段翻、色标与变量原样透传
                                          （词边界 + 全有或全无 + 至少命中 1 段）

用法：
    python verify_lookup.py                      # 跑内置回归用例
    python verify_lookup.py "要查的串" [...]     # 查指定串
    python verify_lookup.py --idempotent         # 全量幂等自检（词表译文再喂回去不该变）
    python verify_lookup.py --regress            # strings.json 全量新旧对比
    python verify_lookup.py --missing            # 列出 strings.json 里仍查不到的条目
"""
import io
import json
import os
import re
import struct
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DICT = os.path.join(ROOT, "_dict", "zh_cn.dict")
STRINGS = os.path.join(ROOT, "_dict", "strings.json")


def load_dict(path=DICT):
    b = open(path, "rb").read()
    assert b[:4] == b"CWH1", "不是 CWH1 格式"
    n = struct.unpack("<i", b[4:8])[0]
    q, d = 8, {}
    for _ in range(n):
        kl = struct.unpack("<H", b[q:q + 2])[0]; q += 2
        k = b[q:q + kl].decode("utf-8"); q += kl
        zl = struct.unpack("<H", b[q:q + 2])[0]; q += 2
        z = b[q:q + zl].decode("utf-8"); q += zl
        d[k] = z
    return d


def normalize(s):
    if s is None:
        return ""
    out = []
    i, n = 0, len(s)
    while i < n:
        c = s[i]
        if c == "\r":
            if i + 1 < n and s[i + 1] == "\n":
                i += 1
                continue
            out.append("\n")
            i += 1
            continue
        if c == " ":
            j = i
            while j + 1 < n and s[j + 1] == " ":
                j += 1
            out.append(" ")
            i = j + 1
            continue
        out.append(c)
        i += 1
    r = "".join(out).rstrip(" \n\t")
    return r


LETTER = re.compile(r"[A-Za-z]")


def is_letter(c):
    return ("A" <= c <= "Z") or ("a" <= c <= "z")


def is_hex(c):
    return ("0" <= c <= "9") or ("a" <= c <= "f") or ("A" <= c <= "F")


def is_word_char(c):
    """「词的一部分」：字母或数字。数字也算 —— 标识符常是字母前缀 + 编号
    （Link327、Custom1_256、Mesh0），把数字当边界会拆坏它们。"""
    return is_letter(c) or ("0" <= c <= "9")


def has_cjk(s):
    """串里有没有汉字/中文标点。用来识别「这串已经是我们的译文了」。"""
    for c in s:
        o = ord(c)
        if 0x4E00 <= o <= 0x9FFF:
            return True
        if 0x3000 <= o <= 0x303F:
            return True
        if 0xFF00 <= o <= 0xFFEF:
            return True
    return False


def tag_length(s, i):
    if i >= len(s) or s[i] != "[":
        return 0
    close = -1
    limit = min(i + 24, len(s))
    for k in range(i + 1, limit):
        c = s[k]
        if c == "]":
            close = k
            break
        if c in "[ \n":
            return 0
    if close < 0:
        return 0
    n = close - i - 1
    if n == 1 and s[i + 1] == "-":
        return close - i + 1
    if n == 6 and all(is_hex(s[k]) for k in range(i + 1, close)):
        return close - i + 1
    if 1 <= n <= 20 and all(is_letter(s[k]) for k in range(i + 1, close)):
        return close - i + 1
    return 0


class Lookup(object):
    def __init__(self, d):
        self.d = d
        self.norm = {}
        for k, v in d.items():
            nk = normalize(k)
            if nk and nk not in self.norm:
                self.norm[nk] = v
        self.by_first = {}
        for k in d:
            if not k:
                continue
            c = k[0]
            if not is_letter(c) and c != " " and c != "[":
                continue
            self.by_first.setdefault(c, []).append(k)
        for lst in self.by_first.values():
            lst.sort(key=lambda x: (-len(x), x))
        self.stats = {"exact": 0, "concat": 0, "norm": 0, "seg": 0}

    # ---- 2. 换行拼接（换行归右段，与插件 LookupConcat 完全一致）
    def concat(self, s, depth):
        if depth >= 2 or len(s) < 2:
            return None
        for i in range(len(s) - 1, 0, -1):
            if s[i] != "\n":
                continue
            head, tail = s[:i], s[i:]
            h = self.d.get(head) or self.concat(head, depth + 1)
            if not h:
                continue
            t = self.d.get(tail) or self.concat(tail, depth + 1)
            if not t:
                continue
            return h + t
        return None

    # ---- 4. 分段重组
    def segment(self, s):
        if not self.by_first or not s:
            return None
        out, i, matched, L = [], 0, 0, len(s)
        while i < L:
            c = s[i]
            hit = self.match_key(s, i) if (is_letter(c) or c == " " or c == "[") else None
            if hit:
                out.append(self.d[hit])
                i += len(hit)
                matched += 1
                continue
            tl = tag_length(s, i)
            if tl:
                out.append(s[i:i + tl])
                i += tl
                continue
            if is_letter(c):
                return None
            out.append(c)
            i += 1
        if matched == 0:
            return None
        return "".join(out)

    def match_key(self, s, i):
        if i > 0 and is_letter(s[i - 1]):
            return None
        L = len(s)
        for key in self.by_first.get(s[i], ()):
            if not s.startswith(key, i):
                continue
            j = i + len(key)
            # 右边界：key 以字母结尾时，后面紧接字母或数字都算词内
            if is_letter(key[-1]) and j < L and is_word_char(s[j]):
                continue
            return key
        return None

    def __call__(self, s):
        if not s or not self.d:
            return None
        if self.d.get(s):
            self.stats["exact"] += 1
            return self.d[s]
        r = self.concat(s, 0)
        if r:
            self.stats["concat"] += 1
            return r
        nk = normalize(s)
        if nk and self.norm.get(nk):
            self.stats["norm"] += 1
            return self.norm[nk]
        # 已经是中文的串不要再翻（TextMesh 那条路会喂回来）
        if has_cjk(s):
            return None
        r = self.segment(s)
        if r:
            self.stats["seg"] += 1
            return r
        return None


PRE = "Planet is shielded. Keys needed from planet(s):\n"
G, R = "[7fff7f]", "[ff7f7f]"

CASES = [
    # 护盾提示（本次改动的起因）
    ("护盾-单名字", PRE + R + "Tempus"),
    ("护盾-单名字2", PRE + R + "Far York"),
    ("护盾-Tiplex全绿", PRE + G + " Jojo," + G + " Ormos," + G + " Seedet," + G + " Flick"),
    ("护盾-Tiplex混色", PRE + G + " Jojo," + R + " Ormos," + G + " Seedet," + R + " Flick"),
    ("护盾-Chanson", PRE + G + " Choix," + G + " Ruine," + G + " Defi"),
    ("护盾-Farbor", PRE + R + " Otrav," + R + " Meso," + R + " Krig"),
    # 资源条
    ("资源条-能量", "  [ffffff]Energy:[-][ff0000] 55/100"),
    ("资源条-升级", "  [ffffff]Upgrading to Lvl: 4"),
    ("资源条-建造完成", "  [ffffff]Fully Upgraded![-]"),
    ("资源条-建造中", "  [ffffff]Building...[-]"),
    ("资源条-生命", "  [ffffff]Health:[-][00ff00] 87.5%[-]"),
    ("资源条-矿", "  [ffffff]Ore:[-][ff0000] 12/30"),
    ("资源条-平台", "[-][ffffff]  Pad:[-][ff7f7f] 3/10[-]"),
    # 前缀 + 变量
    ("Collected", "Collected: Collector"),
    ("Collected2", "Collected: Pulse Cannon"),
    ("Amt", "Amt: 12"),
    ("Targets", "Targets: 5"),
    ("Score---", "Score: ---"),
    ("Score数字", "Score: 1234"),
    ("Time---", "Time: ---"),
    ("Time数字", "Time:1:00:00.0"),
    # 换行拼接（老路径，不能被破坏）
    ("To Access", "Arc Eternal\n[ff5050]To Access:\n[ff0000]Complete first 5 Arc Eternal missions."),
]

# 必须保持英文 / 必须整句命中（错一个就是回归）
SAFETY = [
    ("Windows", None),
    ("StartIndex: ", None),
    ("GameObject", None),
    ("RESET ALL STATS AND ACHIEVEMENTS!!!", None),
    ("SetCamera3D", None),
    ("Playlist", None),
    ("Community", None),
    ("Interval: 3 sec", None),
    ("Building x", None),
    ("MultiMesh", None),
]


def main():
    d = load_dict()
    lk = Lookup(d)
    args = sys.argv[1:]

    if not args:
        print("词表 %d 条" % len(d))
        print("\n== 目标场景 ==")
        for name, s in CASES:
            r = lk(s)
            flag = "✓" if r else "✗"
            print("  %s %-16s %r" % (flag, name, r))
        print("\n== 安全抽查（左列必须为 None）==")
        bad = 0
        for s, _ in SAFETY:
            r = lk(s)
            ok = r is None
            if not ok:
                bad += 1
            print("  %s %-38r -> %r" % ("✓" if ok else "!!", s, r))
        print("\n命中分布：" + json.dumps(lk.stats, ensure_ascii=False))
        return 0 if bad == 0 else 1

    if args[0] == "--idempotent":
        bad = [(z, lk(z)) for z in d.values() if lk(z) not in (None, z)]
        print("词表 %d 条，译文再查一遍被改写的：%d 条" % (len(d), len(bad)))
        for z, nz in bad[:30]:
            print("  %r -> %r" % (z, nz))
        return 0 if not bad else 1

    if args[0] == "--regress":
        S = json.load(open(STRINGS, encoding="utf-8"))
        diffs = []
        for it in S["strings"]:
            e = it["en"]
            old = d.get(e) or lk.concat(e, 0)
            new = lk(e)
            if old != new:
                diffs.append((e, old, new))
        print("strings.json %d 条，新旧结果不同：%d 条"
              % (len(S["strings"]), len(diffs)))
        for e, o, n in diffs[:40]:
            print("  %r\n    旧=%r\n    新=%r" % (e[:70], o, n))
        return 0

    if args[0] == "--missing":
        S = json.load(open(STRINGS, encoding="utf-8"))
        miss = [it for it in S["strings"] if not lk(it["en"])]
        print("strings.json %d 条，仍查不到：%d 条"
              % (len(S["strings"]), len(miss)))
        for it in miss[:40]:
            print("  %r" % it["en"][:70])
        return 0

    for s in args:
        print("%r\n  -> %r" % (s, lk(s)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
