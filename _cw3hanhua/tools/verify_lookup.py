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
    # ★ v1.5.2 括号旁空格归一（与插件 Normalize 保持一致）：
    #   CrplCompiler 在分词前盲替换 "(" -> " ( " 与 ")" -> " ) "，
    #   连引号内字面量也改，所以运行时串里括号旁会多出空格。
    if "(" in r or ")" in r:
        r = paren_space(r)
    return r


PAREN_PUNCT = ".,;:!?"


def paren_space(s):
    """把 ')' 后、句读标点前、'(' 后、')' 前的空格吃掉。只动空格，不增删括号本身。"""
    out = []
    i, n = 0, len(s)
    while i < n:
        c = s[i]
        if c != " ":
            out.append(c)
            i += 1
            continue
        j = i
        while j + 1 < n and s[j + 1] == " ":
            j += 1
        if out and out[-1] == "(":
            i = j + 1
            continue
        if j + 1 < n:
            nx = s[j + 1]
            if nx == ")":
                i = j + 1
                continue
            if out and out[-1] == ")" and nx in PAREN_PUNCT:
                i = j + 1
                continue
        out.append(" ")
        i = j + 1
    return "".join(out)


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


def quoted_key_end(s, i):
    """若 s[i] 起是单引号包住的键名（'P' / 'LeftControl-P'），返回右引号下标，否则 -1。
    左引号前不能是字母，否则 don't / isn't 这类缩写会被当键名吞掉。"""
    if i < 2 or s[i - 1] != "'":
        return -1
    if is_letter(s[i - 2]):
        return -1
    limit = min(i + 24, len(s))
    for k in range(i, limit):
        c = s[k]
        if c == "'":
            return k if k > i else -1
        if not is_letter(c) and not ("0" <= c <= "9") and c not in "_-+":
            return -1
    return -1


def is_size_sep(s, i):
    """s[i] 是不是「尺寸写法」里的分隔符 x（16x9 / 101x101）。
    与插件 IsSizeSep 一致：必须是 x/X，且前后都是数字。"""
    if i <= 0 or i >= len(s) - 1:
        return False
    if s[i] not in "xX":
        return False
    return ("0" <= s[i - 1] <= "9") and ("0" <= s[i + 1] <= "9")


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
                # v1.3.2：单引号里的键名（'P' / 'LeftControl-P'）原样透传
                ke = quoted_key_end(s, i)
                if ke > i:
                    out.append(s[i:ke])
                    i = ke
                    matched += 1
                    continue
                # v1.4.1：「数字x数字」里的 x 当变量透传（16x9 / 101x101）
                if is_size_sep(s, i):
                    out.append(c)
                    i += 1
                    continue
                return None
            out.append(c)
            i += 1
        if matched == 0:
            return None
        return "".join(out)

    def match_key(self, s, i):
        # v1.3.2：左边界只在 key 自己也以字母起头时才判 —— 词表里大量碎片
        # 自带前导空格（"  [ffffff]Health:[-] "），天然有边界，不该被前一个字母卡死。
        if i > 0 and is_letter(s[i - 1]) and is_letter(s[i]):
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
    # ★ v1.3.2 单位标题：英文名 + 自带前导空格的碎片（左边界放宽的起因）
    ("单位标题-护盾钥匙", "Shield Key  [ffffff]Health:[-][00ff00] 100%[-]"),
    ("单位标题-收集器", "Collector  [ffffff]Health:[-][00ff00] 87.5%[-]"),
    ("单位标题-建造中", "Mortar  [ffffff]Building:[-][00ff00] 100/50[-]"),
    ("单位标题-弹药", "Pulse Cannon  [ffffff]Ammo:[-][00ff00] 5/60[-]"),
    # ★ v1.3.2 引号内键名透传（点击提示）
    ("点击提示-P", "Click or 'P'"),
    ("点击提示-组合键", "Click or 'LeftControl-P'"),
    ("点击提示-空", "Click or ''"),
    # ★ 2026-10-24 孢子计数 / 秒 词缀（m01235）
    ("孢子塔数量", "1 spores"),
    ("孢子计时条", "1, 1 spore(s) in 0:59, 0:59"),
    ("空降兵生成间隔", "Spawn Interval: 3 sec"),
    # 「Interval: 」+「 sec」两条词缀拼出来的合法结果（不是回归）
    ("间隔-秒", "Interval: 3 sec"),
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
    # ★ 2026-10-24：加 ' sec' 词缀后，「间隔」类碎片的右边界仍须守住。
    #   'Interval: 3 sec' 已从本表移入 CASES —— 词表里本来就有 'Interval: '→'间隔：'，
    #   拼出「间隔：3秒」是正确行为；这里只守「sec 后面还跟着字母」的情形。
    ("Interval: 3 secs", None),
    ("just a sec, ok", None),
    ("Building x", None),
    ("MultiMesh", None),
    # ★ v1.3.2 新路径的守卫：缩写里的撇号不能被当成键名引号
    ("don't panic", None),
    ("it's a trap", None),
    ("Don't 'quote' me", None),
    ("Audio item with name 'music' not found", None),
    ("The 'A' key is broken", None),
    ("Click or 'P' and then 'Q'", None),
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
