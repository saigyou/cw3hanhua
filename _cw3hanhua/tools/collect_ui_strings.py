#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
静态全量收集 CW3 的玩家可见文本 —— 不跑游戏，不靠渲染回放。

为什么需要它：
    "跑一遍游戏看 set_text 打了什么"只能覆盖走过的路径（主菜单、设置页……），
    剧情、任务、教程这些要走很远才出现。要**全量量化**，只能静态扫。

两路来源，口径不同，不能混：
    路 1  #US 用户字符串堆（Assembly-CSharp.dll 等）
          → 每个字符串都可能被显示；用 IL 分析判断它**进不进 UI**：
            - 作为实参传给 set_text / TextMesh.text / 其他字符串 setter  → call_text / call_mesh
            - 出现在 "Foo: " + x 这类拼接里（会拼进 label）              → concat / format
          → 这就是静态版的"运行时 set_text 观测"，覆盖全部调用点而非走过的路径
    路 2  资产里序列化的 mText / m_Text（UILabel / TextMesh）
          → 反序列化**不经过 setter**（NOTES 3.3），必须单独收集，靠 sweep 处理

只读：不修改任何游戏文件。产物写在 _cw3hanhua/_dict/。

用法：
    .venv\\Scripts\\python.exe tools\\collect_ui_strings.py
    .venv\\Scripts\\python.exe tools\\collect_ui_strings.py --merge   # 保留已有译文
"""
import argparse
import json
import os
import re
import sys
import time
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
import bake_font          # noqa: E402  ★ 烘焙范围只从这一处来

GAME = os.path.dirname(ROOT)
DATA = os.path.join(GAME, "CW3_Data")
MANAGED = os.path.join(DATA, "Managed")
OUT_DIR = os.path.join(ROOT, "_dict")
OUT_JSON = os.path.join(OUT_DIR, "strings.json")

# 这些 DLL 的字符串堆代表了玩家实际会看到的文本
TEXT_DLLS = ["Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll"]

# ---------------------------------------------------------------- 分类
# 优先级从高到低。命中第一条即定类；所有类别都会被收集，只是统计口径不同。

RE_NOISE_SUFFIX = re.compile(r"(Exception|Stream|Inflater|Deflater|BZip2|Adler|CRC|Zip|Tar\(|GZIP|checksum|chksum)", re.I)
RE_PATH = re.compile(r"^(Hidden/|Resources/|Assets/|Exports/|Finalized/|WorldEditor/|C:/|[A-Za-z]:\\)")
RE_METHOD = re.compile(r"^[A-Za-z_][\w]*(\.[A-Za-z_<>][\w<>\[\]]*)+\s*\(.*\)$")
RE_ASSET_NAME = re.compile(r"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9]+)?$")   # 资源/对象/音频名
RE_IDENT = re.compile(r"^[_a-z][A-Za-z0-9_]*$")                       # 字段/局部变量/注释键
RE_PASCAL = re.compile(r"^[A-Z][a-z0-9]+([A-Z][a-z0-9]*)+$")           # PascalCase 标识符
RE_SHADER = re.compile(r"(Unlit/|GUI/|Particles/|Standard|Hidden\b)", re.I)
RE_NUM_ONLY = re.compile(r"^[\d\s.,:;%x*/+-]+$")
RE_ENGINE = re.compile(r"(UnityEngine|NGUI|iTween|Steamworks|can't be loaded|No AudioListener|SingletonMonoBehaviour)", re.I)
RE_URL = re.compile(r"^(https?://|www\.)", re.I)
# 调试/异常类前缀：玩家看不到
RE_DEBUG_PREFIX = re.compile(
    r"^(Could not|Cannot |Can't |Unable |Failed to|Error |WARNING|Invalid |Illegal |Unexpected |Unknown |Missing |"
    r"Attempted |Tried to|No |Null |Not |Must |Should |Expected |Use |Called |Received |Tried )", re.I)
# 富文本颜色标记：[ff5050] / [-][ffffff] / [c1c1c1]
RE_RICHTEXT = re.compile(r"^(\[[-\w]*\])+$")
# 代码片段样式：含 C# 运算符/成员访问/引号
RE_CODEISH = re.compile(r"(==|!=|\bnull\b|\.\w+\(|<\w+>|\"|\bVector[23]\b|\bAudioClip\b|\bGUIText\b|\bstring\b)", re.I)
# 路径样式：以 / 开头的资源节点路径
RE_NODE_PATH = re.compile(r"(^/|\w/[\w/]+$|^\w:/)")
RE_DASHED = re.compile(r"(--+|[=-]{3,}|_{3,})")
# 像文本其实是代码/资源的
RE_FORMAT_SPEC = re.compile(r"\{\d+(:[^}]*)?\}")          # {0:D} 占位符
RE_REGEX = re.compile(r"^[\^\[].*[\$\]]$")                # ^[a-z0-9_]+$
RE_TEMPLATE_NAME = re.compile(r"^Custom[A-Za-z0-9_]*(\d+pp|_128)$")   # Custom12pp / Custom37_128
RE_STAT_KEY = re.compile(r"^STAT_[A-Z_]+$")               # STAT_xxx 统计键
RE_RICHTEXT_PREFIX = re.compile(r"^(\[[-\w#]+\])+\s*\S")  # 颜色标记 + 正文


def classify(s, ctx=None):
    """返回 (类别, 原因)。ctx = dict(cls=set, ops=set, refs=list)。

    类别：
      noise  不翻译（标识符/资源名/引擎内部串）
      ui     确定或极可能是显示文本 → 要翻译
      other  拿不准 → 列进"待人工判定"，不要默认翻

    ★ 顺序即优先级，改之前先想清楚谁在谁前面：
       "以空格结尾"那类规则必须排在"标识符/资源名"之前，
       否则 'SHIFT-E ' / 'Score: 9999 ' 会先被当成资源名丢掉。
    """
    ctx = ctx or {}
    clsset = ctx.get("cls") or set()
    body_raw = s
    body = s.strip()
    trail_space = body_raw != body_raw.rstrip() and len(body) >= 2
    if not body:
        return "noise", "空白"
    if RE_URL.match(body):
        return "noise", "网址"
    if RE_ENGINE.search(body):
        return "noise", "引擎/插件的调试信息，玩家不可见"
    if RE_RICHTEXT.match(body):
        return "noise", "纯富文本颜色标记"
    if RE_DASHED.search(body):
        return "noise", "分隔线/调试分隔符"
    if RE_NOISE_SUFFIX.search(body) and not body.endswith((".", "!", "?")) and " " not in body[:12]:
        return "noise", "解压/网络库内部错误串"
    if any(_is_library_class(c) for c in clsset) and not any(_is_game_class(c) for c in clsset):
        return "noise", "标准库里抛的异常文本"
    if RE_METHOD.match(body):
        return "noise", "方法名/日志调用"
    if RE_NODE_PATH.search(body) and " " not in body:
        return "noise", "资源节点路径 /GameSettings/xxx"
    if RE_PATH.match(body):
        return "noise", "资源路径"
    if RE_SHADER.search(body) and " " not in body:
        return "noise", "Shader 名"
    if RE_NUM_ONLY.match(body):
        return "noise", "纯数字/符号，不需要翻译"

    # ---- 显示文本特征（必须在"标识符"判断之前）
    # 但先拦掉三类长得像文本、实际是代码的东西，否则它们会在下面被误判成 ui：
    #   '{0:D}' 格式串、'^[a-z]+$' 正则、含运算符/引号的调试消息
    if RE_FORMAT_SPEC.search(body):
        return "other", "String.Format 占位符，显示时会被替换掉 —— 需人工确认"
    if RE_REGEX.match(body):
        return "noise", "正则表达式"
    if RE_TEMPLATE_NAME.match(body):
        return "noise", "模板资源名（Custom12pp / Custom37_128pp）"
    if RE_STAT_KEY.match(body):
        return "none", "统计键名（STAT_xxx），显示前会查表换成其它文本"

    if trail_space:
        return "ui", "以空格结尾 —— 拼接前缀样式（如 \"Score: \" + 数字）"
    if re.search(r"[:!?]$", body) or body.endswith("..."):
        return "ui", "以冒号/感叹号/问号/省略号结尾（标签样式）"
    if body.endswith(".") and len(body) >= 6:
        return "ui", "以句号结尾（句子样式）"
    if re.search(r"\d\s*[xX×]\s*\d", body) or re.search(r"[:=]\s*\d", body):
        return "ui", "含数据的格式串（128x128 / Score: 9999）"
    if RE_RICHTEXT_PREFIX.match(body):
        return "ui", "带富文本颜色前缀的显示文本"

    # ---- 代码/调试特征（排在标识符前面：'\" when calling ...' 这类含空格）
    if RE_CODEISH.search(body):
        return "other", "代码片段/调试消息（含运算符/引号/成员访问）—— 需人工确认"

    # ---- 标识符特征
    if RE_IDENT.match(body):
        return "noise", "下划线小写标识符（键名/字段名）"
    if RE_PASCAL.match(body):
        return "noise", "PascalCase 标识符（类/属性名），不是显示文本"
    if re.search(r"\(Clone\)$", body):
        return "noise", "Unity 克隆对象名"

    # ---- 剩下拿不准的
    words = re.findall(r"[A-Za-z]{2,}", body)
    if len(words) >= 2:
        if RE_DEBUG_PREFIX.match(body):
            return "other", "疑似调试/异常消息，但含空格 —— 需人工确认"
        return "ui", "多词英文，几乎一定是显示文本"
    if RE_ASSET_NAME.match(body):
        return "other", "单个单词：可能是按钮文字（Yes/Creeper），也可能是资源名 —— 需人工确认"
    if len(body) < 2:
        return "noise", "单字符"
    return "other", "单个词/符号，可能是按钮文字也可能是标识符"


_LIB_CLASS = re.compile(
    r"^(Zip|Tar|AES|Deflater|Inflater|BZip2|WAVFile|GZip|LZW|JPEG|ADLER|Stream|TarBuffer|Pkzip)", re.I)
_GAME_CLASS = re.compile(
    r"(Menu|HUD|Label|Button|Panel|Space|Manager|Planet|Sector|Unit|Map|Mission|"
    r"Stats|Tutorial|Story|Info|Dialog|Score|Title|DMD|Galaxy|World|Game|Creeper|Emitter)", re.I)


def _is_library_class(c):
    return bool(_LIB_CLASS.match(c or ""))


def _is_game_class(c):
    return bool(_GAME_CLASS.search(c or ""))


# ---------------------------------------------------------------- 路 1：IL 分析
# 只需要法布尔式的粗粒度判定：这条 ldstr 后面紧跟的指令是"消费它"的 → 它进了逻辑。
# 精确的栈机模拟没必要（CW3 的文本几乎都是 ldstr 紧贴 call/concat），
# 但**要把间隔和无关指令区分开**，否则会把紧邻的 ldstr 错配到别的指令上。
CONSUME_RETURN = {"ret", "throw", "rethrow"}      # ldstr 后面直接 ret = 返回值，多半是 const 泄漏


def scan_dll_il(path):
    """返回 (rows, error)。rows: [{file, cls, method, op, text}]"""
    try:
        import dnfile
        from dncil.cil.body import CilMethodBody
        from dncil.cil.body.reader import CilMethodBodyReaderBytes
        from dncil.clr.token import Token
    except Exception as e:
        return [], "dnfile/dncil 导入失败: %s" % e

    try:
        pe = dnfile.dnPE(path)
        raw = pe.__data__
    except Exception as e:
        return [], "打不开 %s: %s" % (path, e)

    md = pe.net.mdtables
    if md is None or md.MethodDef is None:
        return [], "没有元数据表"
    us = pe.net.user_strings

    def string_of(tok):
        try:
            v = us.get(tok.rid)
        except Exception:
            return None
        if v is None:
            return None
        return getattr(v, "value", v)

    name_by_token = build_token_map(pe, md)

    rows = []
    n_methods = n_il = 0
    for mi, m in enumerate(md.MethodDef.rows):
        n_methods += 1
        rva = getattr(m, "Rva", 0)
        if not rva:
            continue
        try:
            off = pe.get_offset_from_rva(rva)
            body = CilMethodBody(CilMethodBodyReaderBytes(raw[off:]))
            instrs = body.instructions
        except Exception:
            continue
        n_il += 1
        cls, mname = name_by_token.get((0x06, mi + 1), ("?", "?"))
        pend = []
        for idx, ins in enumerate(instrs):
            op = ins.opcode.name
            if op == "ldstr" and isinstance(ins.operand, Token):
                s = string_of(ins.operand)
                if s is not None:
                    pend.append((s, idx))
                continue
            if not pend or op in ("nop",):
                continue
            # 跳过"不消费栈"的纯推入指令（除了 string 本身，基本不会紧邻出现）
            if op.startswith(("ld", "dup")) and not op.startswith("ldstr"):
                continue
            opname = op
            if op.startswith("call"):
                tok = ins.operand
                tgt = name_by_token.get((tok.table, tok.rid)) if isinstance(tok, Token) else None
                if tgt and tgt[0] == "ref":
                    opname = "call %s" % tgt[1]
            for s, _i in pend:
                rows.append(dict(file=os.path.basename(path), cls=cls,
                                 method=mname, op=opname, text=s))
            pend = []
    print("   %s: 方法 %d，其中有 IL 体 %d" % (os.path.basename(path), n_methods, n_il))
    return rows, None


def build_token_map(pe, md):
    """方法 token(表号,row) → (类型名, 方法名)；MemberRef token → ("ref", 名字)。

    dncil 的 Token 是 (table << 24 | rid)，只用来当字典键；这里直接用元组，不构造对象。
    """
    T_METHODDEF = 0x06
    T_MEMBERREF = 0x0A
    out = {}
    types = md.TypeDef
    mrows = md.MethodDef.rows
    starts = []
    for tr in types.rows:
        lst = tr.MethodList
        starts.append(lst[0].row_index if len(lst) else None)
    starts.append(None)
    for i, tr in enumerate(types.rows):
        s = starts[i]
        e = starts[i + 1]
        if s is None:
            continue
        if e is None:                      # 最后一个类型：到表尾
            e = len(mrows) + 1
        tname = str(getattr(tr, "TypeName", "?"))
        for r in range(s, min(e, len(mrows) + 1)):
            out[(T_METHODDEF, r)] = (tname, str(mrows[r - 1].Name))
    mr = md.MemberRef
    for i, r in enumerate(mr.rows if mr is not None else []):
        name = getattr(r, "Name", None)
        if name is not None:
            out[(T_MEMBERREF, i + 1)] = ("ref", str(name))
    return out


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=OUT_JSON)
    ap.add_argument("--merge", action="store_true", help="保留已有 strings.json 里的 zh 译文")
    args = ap.parse_args()

    t0 = time.time()
    bake_font.load_baked()
    baked = bake_font.charset_for(bake_font.DEFAULT_SIZES)
    print("烘焙范围（三套字号交集）: %d 个字符" % len(baked))
    print("")

    # ---------- 路 1：IL
    il_rows = []
    dll_errors = {}
    for fn in TEXT_DLLS:
        p = os.path.join(MANAGED, fn)
        if not os.path.exists(p):
            print("  跳过（不存在）: " + fn)
            continue
        rows, err = scan_dll_il(p)
        print("%-34s 字符串引用 %6d 处%s" % (fn, len(rows), ("  !! " + err) if err else ""))
        il_rows.extend(rows)
        if err:
            dll_errors[fn] = err
    print("")

    # ---------- 聚合：同一字符串可能被多处引用
    by_text = defaultdict(lambda: dict(srcs=set(), ops=set(), cls=set(), count=0,
                                       kinds=set(), refs=[]))
    for r in il_rows:
        e = by_text[r["text"]]
        e["srcs"].add(r["file"])
        e["ops"].add(r["op"])
        e["cls"].add(r["cls"])
        e["count"] += 1
        e["kinds"].add("dll")
        if len(e["refs"]) < 6:
            e["refs"].append("%s.%s" % (r["cls"], r["method"]))

    # ---------- 路 2：资产里的序列化文本
    asset_rows, asset_stats = scan_assets()

    # ---------- 分类与统计
    drop = Counter()
    out = []
    seen = set()
    for text, e in by_text.items():
        cat, reason = classify(text, e)
        if cat == "noise":
            drop[reason] += 1
            continue
        seen.add(text)
        out.append(dict(en=text, zh="", cat="dll", grade=cat,
                        ops=sorted(e["ops"]), srcs=sorted(e["srcs"]),
                        cls=sorted(e["cls"]), refs=e["refs"], hits=e["count"],
                        note=reason))
    for a in asset_rows:
        cat, reason = classify(a["text"])
        if cat == "noise":
            drop[reason] += 1
            continue
        merged = a["text"] in seen
        if merged:
            # 资产里序列化的文本反序列化不经 setter，和 DLL 里有同名字符串是两件事，
            # 但翻译只需要一份 → 在已有的那条上打标，不再重复一条。
            for r in out:
                if r["en"] == a["text"]:
                    r["cat"] = "both"
                    r["kind"] = a["kind"]
                    r["files"] = [a["file"]]
                    break
            continue
        seen.add(a["text"])
        out.append(dict(en=a["text"], zh="", cat="asset", grade=cat, kind=a["kind"],
                        files=[a["file"]], refs=[], hits=1, note=reason))
    print("去重后候选字符串: %d" % len(out))
    print("")

    save(out, args.out, args.merge)

    # ---------- 报告
    report = Counter()
    for r in out:
        report[(r["grade"], r["cat"])] += 1
    print("=== 候选字符串：可信度 × 来源 ===")
    print("  %-6s %-16s %6s" % ("可信度", "来源", "条数"))
    for k, n in sorted(report.items(), key=lambda kv: (kv[0][0], -kv[1])):
        print("  %-6s %-16s %6d" % (k[0], k[1], n))
    print("")
    print("=== 按丢弃原因 ===")
    for reason, n in drop.most_common():
        print("  %-34s %6d" % (reason, n))
    print("")
    grad = Counter(r["grade"] for r in out)
    print("结论：确定要翻 %d 条（ui），待人工判定 %d 条（other）"
          % (grad.get("ui", 0), grad.get("other", 0)))
    print("")
    print("用时 %.1fs  → %s" % (time.time() - t0, args.out))
    if dll_errors:
        print("")
        print("!! IL 分析有降级：%s" % dll_errors)
        print("   降级时只有资产文本是完整的，DLL 文本需要走 plan B")


# ---------------------------------------------------------------- 资产扫描
def scan_assets():
    """返回 (rows, per_file)。UnityPy 读 mText / m_Text。"""
    rows = []
    stats = {}
    try:
        import UnityPy
        from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    except Exception as e:
        print("!! UnityPy 不可用，跳过资产: %s" % e)
        return rows, stats

    files = sorted(
        [p for p in os.listdir(DATA) if p.endswith(".assets") and os.path.isfile(os.path.join(DATA, p))]
        + [p for p in os.listdir(DATA) if p.startswith("level") and os.path.isfile(os.path.join(DATA, p))]
        + (["mainData"] if os.path.exists(os.path.join(DATA, "mainData")) else [])
    )
    CLASS_UILabel = 114
    CLASS_TextMesh = 102
    try:
        gen = TypeTreeGenerator("5.2.3f1")
        gen.load_local_dll_folder(MANAGED)
    except Exception as e:
        print("!! TypeTreeGenerator 不可用，跳过资产: %s" % e)
        return rows, stats

    for fn in files:
        p = os.path.join(DATA, fn)
        try:
            env = UnityPy.load(p)
            env.typetree_generator = gen
        except Exception:
            stats[fn] = "load failed"
            continue
        n = 0
        for sf in list(env.files.values()):
            if not hasattr(sf, "objects"):
                continue
            for pid, o in list(sf.objects.items()):
                cid = int(o.class_id) if o.class_id else 0
                if cid not in (CLASS_UILabel, CLASS_TextMesh):
                    continue
                try:
                    d = o.read_typetree()
                except Exception:
                    continue
                if not isinstance(d, dict):
                    continue
                t = d.get("m_Text") if cid == CLASS_TextMesh else d.get("mText")
                if isinstance(t, str) and t.strip():
                    rows.append(dict(file=fn, pid=pid,
                                     kind="TextMesh" if cid == CLASS_TextMesh else "UILabel",
                                     text=t))
                    n += 1
        stats[fn] = n
    print("资产文本 %d 条，分布 %s" % (len(rows), stats))
    return rows, stats


def save(items, path, merge):
    old = {}
    if merge and os.path.exists(path):
        try:
            with open(path, encoding="utf-8") as f:
                data = json.load(f)
            for it in data.get("strings", []):
                if it.get("zh"):
                    old[it["en"]] = it["zh"]
        except Exception:
            pass
    for it in items:
        if it["en"] in old:
            it["zh"] = old[it["en"]]
    # 排序：先 ui 再 other；再按来源（both/asset 优先，它们一定会显示）
    rank_cat = {"both": 0, "asset": 1, "dll": 2}
    rank_grade = {"ui": 0, "other": 1}
    items.sort(key=lambda r: (rank_grade.get(r["grade"], 2), rank_cat.get(r["cat"], 9),
                              -len(r["en"]), r["en"]))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(dict(version=2, generated=time.strftime("%Y-%m-%d %H:%M:%S"),
                       baked_charset=len(bake_font.charset_for(bake_font.DEFAULT_SIZES)),
                       strings=items), f, ensure_ascii=False, indent=1)
    print("已写入 %s（%d 条，其中已翻译 %d 条）"
          % (path, len(items), sum(1 for r in items if r["zh"])))


if __name__ == "__main__":
    main()
