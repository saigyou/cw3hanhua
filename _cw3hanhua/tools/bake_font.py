#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
A1: 从系统字体烘焙 BMFont 字形图集（离线，不碰游戏文件）

产出（默认 _cw3hanhua/_font/ 下）：
    font_<size>.png    灰度图集，白字黑底，字形按 cell 网格排列
    font_<size>.json   BMFont 元数据（charSize / texWidth / texHeight / 每个 glyph 的度量）
    preview_<size>.png 左上角 320x320 预览，供目视检查清晰度

字形度量与 Unity/NGUI 的 BMFont 字段对应关系：
    glyph.x, glyph.y      图集内像素坐标（左上角）
    glyph.width/height    位图尺寸
    glyph.offsetX         相对笔位左侧的偏移（PIL bbox.left，anchor="ls"）
    glyph.offsetY         相对【行顶】的下移量 = bbox.top + ascent
    glyph.advance         前进宽度
    BMGlyph.x/y/width/height/offsetX/offsetY/advance 一一对应

默认字体是微软雅黑 **Bold**（`msyhbd.ttc`）+ 0.80 缩放 —— 这就是发布图集的参数，
不带参数直接跑即可复现。换字体用 `--font` 覆盖。

用法：
    .venv\\Scripts\\python.exe tools\\bake_font.py [--sizes 16,20,28] [--font msyhbd.ttc] [--index 0]
"""
import argparse
import json
import math
import os
import sys

from PIL import Image, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)                 # _cw3hanhua/
DEFAULT_OUT = os.path.join(ROOT, "_font")
# 发布图集用的就是雅黑 Bold + 0.80 缩放。默认值跟发布保持一致，
# 不带参数跑 `bake_font.py` 就能 1:1 复现仓库里的 _font/（已实测 sha256 相同）。
# 换成 msyh.ttc（常规体）会得到另一套字形，观感比发布版细。
DEFAULT_FONT = r"C:\Windows\Fonts\msyhbd.ttc"
DEFAULT_SCALE = 0.80

# ★ 插件实际装载的字号。check_charset / collect_ui_strings 只统计这几个字号都覆盖的字。
#   插件 SwapOneUIFont 按 UIFont 的 size 取 font_<size>.fnt：
#   Aldrich16=20 / Tempesta=16 / RadioStars22=28。改字号必须同步改这里和插件。
DEFAULT_SIZES = (16, 20, 28)

# 已烘焙字集缓存： {size: set(char)}，由 load_baked(sizes, outdir) 填。
# 只能经 load_baked() 读，避免这里反向依赖 _font 目录的扫描逻辑。
BAKED_CHARSET = {}


def load_baked(sizes=None, outdir=None):
    """读 _font/font_<size>.json，把每个字号的已烘字集填进 BAKED_CHARSET。"""
    outdir = outdir or DEFAULT_OUT
    sizes = sizes or DEFAULT_SIZES
    got = {}
    for size in sizes:
        path = os.path.join(outdir, "font_%d.json" % size)
        if not os.path.exists(path):
            continue
        with open(path, encoding="utf-8") as f:
            meta = json.load(f)
        BAKED_CHARSET[size] = set(chr(g["code"]) for g in meta["glyphs"])
        got[size] = len(BAKED_CHARSET[size])
    return got


def gb2312_chars():
    """ASCII 可见字符 + GB2312 全集（符号区 1-9 + 一级 16-55 + 二级 56-87）。

    必须包含符号区：中文标点（，。：；（）【】「」、）都在那里；
    必须包含二级：渲/阈/阱 这类常用但非一级的字。

    单独抽出来是为了让 check_charset.py / collect_ui_strings.py 复用
    （它们只算"默认烘焙范围"，不能因此依赖 PIL）。
    """
    chars = [chr(c) for c in range(0x20, 0x7F)]
    seen = set(chars)
    for hi in range(0xA1, 0xF8):          # GB2312 区位 01..87
        for lo in range(0xA1, 0xFF):
            try:
                ch = bytes([hi, lo]).decode("gb2312")
            except Exception:
                continue
            if ch not in seen:
                seen.add(ch)
                chars.append(ch)
    return chars


def build_charset():
    """= gb2312_chars() + 少量 GB2312 之外的中文常用符号。"""
    chars = gb2312_chars()
    seen = set(chars)
    # 常用但不在 GB2312 里的符号（破折号/间隔号/省略号/引号等）
    for ch in "·–—…‘’“”″′•‰":
        if ch not in seen:
            seen.add(ch)
            chars.append(ch)
    return chars


def baked_range(sizes=None):
    """默认烘焙范围（不含 --extra 补烘）：GB2312 全集。

    一个字符只要有**任一**字号没烘，用到那个字号就会显示方块，
    所以真正可用的范围是三套字号字集的交集 —— 由 charset_for() 给出。
    """
    return set(build_charset())


def charset_for(sizes=None, extra=""):
    """★ 词表校验用：给一组字号，返回**它们真正能全部覆盖**的字符集。

    取交集而不是并集 —— 一个字只要有一个字号没烘，用到那个字号的地方就是方块。
    没烘过的字号 = 直接按默认字集（GB2312 全集）算，因为重烘默认就是这个范围。
    注意：--extra 补烘只对当次烘的字号生效，所以补过字的字号要逐步列入 sizes。
    """
    sizes = sizes or DEFAULT_SIZES
    result = baked_range(sizes)
    for size in sizes:
        if size in BAKED_CHARSET:
            result = result & BAKED_CHARSET[size]
    # --extra 补烘的字也计入：只对本次烘过的字号生效，调用方负责传对 sizes
    result = result | set(ch for ch in (extra or "") if ch.strip())
    return result


def next_pow2(n):
    p = 1
    while p < n:
        p <<= 1
    return p


def measure(font, ch, ascent):
    """返回 (mask, w, h, offX, offY, adv)；空字符返回 (None, 0, 0, 0, 0, adv)。"""
    try:
        bbox = font.getbbox(ch, anchor="ls")
        adv = font.getlength(ch)          # 注意：getlength 不接受 anchor 参数
    except Exception:
        return None, 0, 0, 0, 0, 0.0
    try:
        mask = font.getmask(ch, mode="L", anchor="ls")
    except Exception:
        mask = None
    mw, mh = (mask.size if mask is not None else (0, 0))
    w = bbox[2] - bbox[0]
    h = bbox[3] - bbox[1]
    if mask is None or mw <= 0 or mh <= 0 or w <= 0 or h <= 0:
        return None, 0, 0, 0, 0, adv
    # PIL 的 mask 尺寸即墨迹尺寸；bbox 可能略大（含空白），统一以 mask 为准
    return mask, mw, mh, bbox[0], bbox[1] + ascent, adv


def bake(size, chars, outdir, font_path, font_index, scale=DEFAULT_SCALE, scale_y=None):
    # 字形像素尺寸 = size * scale；charSize 仍用 size。
    # 雅黑 Bold 的字形比 Aldrich 更"满"、笔画更粗，同 charSize 下看起来大一号，
    # 所以发布用的是 scale=0.80（实际栅格化字号 13/16/22 px）。
    #
    # scale_y 单独控制纵向：汉字天生接近正方形，而 Aldrich 的拉丁字形偏扁，
    # 等比缩放没法让两个方向同时对齐 —— 此时把 scale_y 设得比 scale 小即可"压扁"。
    # 实现方式是渲染后对 mask 做非等比缩放，并同步缩放 w/h/offX/offY/adv，
    # 这样 advance（横向）与 height/offsetY（纵向）可以各自独立。
    if scale_y is None:
        scale_y = scale
    px_size = max(6, int(round(size * max(scale, scale_y))))
    font = ImageFont.truetype(font_path, px_size, index=font_index)
    ascent, descent = font.getmetrics()
    sx = float(scale) / float(max(scale, scale_y)) if max(scale, scale_y) > 0 else 1.0
    sy = float(scale_y) / float(max(scale, scale_y)) if max(scale, scale_y) > 0 else 1.0

    # 先量一遍，收集非空字形
    items = []
    empty = 0
    max_w = 0
    max_h = 0
    for ch in chars:
        mask, w, h, ox, oy, adv = measure(font, ch, ascent)
        if mask is None:
            empty += 1
            # 空字形（空格）：advance 也要按横向比例缩放
            items.append(dict(ch=ch, mask=None, w=0, h=0, ox=0, oy=0,
                              adv=int(round(adv * sx))))
            continue

        # 非等比缩放（sx/sy 已经按最大公倍归一，通常其中一个为 1.0）
        if abs(sx - 1.0) > 1e-6 or abs(sy - 1.0) > 1e-6:
            nw = max(1, int(round(mask.size[0] * sx)))
            nh = max(1, int(round(mask.size[1] * sy)))
            if nw != mask.size[0] or nh != mask.size[1]:
                mask = mask.resize((nw, nh), Image.LANCZOS)
            w = nw
            h = nh
            ox = int(round(ox * sx))
            oy = int(round(oy * sy))
            adv = adv * sx

        items.append(dict(ch=ch, mask=mask, w=w, h=h, ox=ox, oy=oy, adv=adv))
        if w > max_w:
            max_w = w
        if h > max_h:
            max_h = h

    # cell 自适应：至少容纳最大字形 + 2px 间隙，且不小于字号
    cell = max(size, max_w, max_h) + 2

    # 选图集宽度：1024 → 2048 → 4096，取第一个高度能压进 4096 的
    def try_layout(width):
        per_row = max(1, width // cell)
        rows = int(math.ceil(len(items) / float(per_row)))
        h = next_pow2(rows * cell)
        if h > 4096:
            return None
        return per_row, width, h

    chosen = None
    for w in (1024, 2048, 4096):
        chosen = try_layout(w)
        if chosen:
            break
    if not chosen:
        raise SystemExit("!! 字号 %d 的字形太多，4096 宽也放不下，需要缩小字集" % size)
    per_row, tex_w, tex_h = chosen

    img = Image.new("L", (tex_w, tex_h), 0)
    glyphs = []
    overflow = 0
    for i, it in enumerate(items):
        col = i % per_row
        row = i // per_row
        x0 = col * cell
        y0 = row * cell
        code = ord(it["ch"])
        if it["mask"] is None:
            glyphs.append(dict(code=code, x=0, y=0, w=0, h=0,
                               offX=0, offY=0, adv=int(round(it["adv"]))))
            continue
        if it["w"] > cell or it["h"] > cell:
            overflow += 1
        m = it["mask"]
        img.paste(m, (x0, y0, x0 + m.size[0], y0 + m.size[1]))
        glyphs.append(dict(code=code, x=x0, y=y0, w=it["w"], h=it["h"],
                           offX=int(round(it["ox"])), offY=int(round(it["oy"])),
                           adv=int(round(it["adv"]))))

    png_path = os.path.join(outdir, "font_%d.png" % size)
    json_path = os.path.join(outdir, "font_%d.json" % size)
    fnt_path = os.path.join(outdir, "font_%d.fnt" % size)
    img.save(png_path, "PNG", optimize=True)

    meta = dict(size=size, texWidth=tex_w, texHeight=tex_h,
                lineHeight=ascent + descent, ascent=ascent, descent=descent,
                cell=cell, count=len(glyphs), glyphs=glyphs)
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, separators=(",", ":"))

    # 纯文本 .fnt：C# 端逐行 Split(' ') 即可，避免手写 JSON 解析
    with open(fnt_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("# CW3Hanhua BMFont\n")
        f.write("size %d\n" % size)
        f.write("texWidth %d\n" % tex_w)
        f.write("texHeight %d\n" % tex_h)
        f.write("lineHeight %d\n" % (ascent + descent))
        f.write("ascent %d\n" % ascent)
        f.write("count %d\n" % len(glyphs))
        f.write("# code x y w h offX offY adv\n")
        for g in glyphs:
            f.write("%d %d %d %d %d %d %d %d\n" % (
                g["code"], g["x"], g["y"], g["w"], g["h"],
                g["offX"], g["offY"], g["adv"]))

    # 预览：左上角 320x320（放大 2 倍便于看边缘）
    pv = img.crop((0, 0, min(320, tex_w), min(320, tex_h))).resize((640, 640), Image.NEAREST)
    pv_path = os.path.join(outdir, "preview_%d.png" % size)
    pv.save(pv_path, "PNG")

    print("  size=%-3d cell=%-3d 图集=%-5dx%-5d 字形=%-5d 空=%-3d 越界=%d 行高=%d"
          % (size, cell, tex_w, tex_h, len(glyphs), empty, overflow, ascent + descent))
    return dict(size=size, tex_w=tex_w, tex_h=tex_h, count=len(glyphs), empty=empty, overflow=overflow)


def charset_for(sizes, extra=""):
    """★ 词表校验用：给一组字号，返回**它们真正能全部覆盖**的字符集。

    取交集而不是并集 —— 一个字只要有一个字号没烘，用到那个字号的地方就是方块。
    没烘过的字号 = 直接按默认字集（GB2312 全集）算，因为重烘默认就是这个范围。
    注意：--extra 补烘只对当次烘的字号生效，所以补过字的字号要逐步列入 sizes。
    """
    base = set(build_charset())
    base.update(ch for ch in (extra or "") if ch.strip())
    result = None
    for size in sizes:
        if size not in BAKED_CHARSET:
            continue
        result = set(BAKED_CHARSET[size]) if result is None else (result & BAKED_CHARSET[size])
    return (result if result is not None else set()) | set(ch for ch in (extra or "") if ch.strip())


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    ap = argparse.ArgumentParser()
    ap.add_argument("--sizes", default="16,20,28")
    ap.add_argument("--font", default=DEFAULT_FONT,
                    help="字体文件；默认是雅黑 Bold（发布图集用的就是它）")
    ap.add_argument("--index", type=int, default=0)
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--extra", default="", help="额外要烘焙的字符（缺字时由 check_charset 给出）")
    ap.add_argument("--scale", type=float, default=DEFAULT_SCALE,
                    help="字形像素缩放（charSize 不变）；默认 0.80 与发布图集一致。"
                         "雅黑比 Aldrich 满，粗体尤其，调大前先看 preview_<size>.png")
    ap.add_argument("--scale-y", type=float, default=None,
                    help="纵向单独缩放；不给则等于 --scale。比 --scale 小可以压扁字形（横向不变）")
    args = ap.parse_args()

    sizes = [int(s) for s in args.sizes.split(",") if s.strip()]
    if not os.path.exists(args.font):
        print("!! 字体不存在: " + args.font)
        return 2
    os.makedirs(args.out, exist_ok=True)

    chars = build_charset()
    extra_added = 0
    if args.extra:
        seen = set(chars)
        for ch in args.extra:
            if ord(ch) > 0x20 and ch not in seen:
                seen.add(ch)
                chars.append(ch)
                extra_added += 1
    print("字符集: %d 个 (ASCII 95 + GB2312 一级 3755 + 额外 %d)" % (len(chars), extra_added))
    print("字体  : %s (index=%d)" % (args.font, args.index))
    print("缩放  : scale=%s%s" % (args.scale,
          "" if args.scale_y is None else " scale_y=%s" % args.scale_y))
    print("输出  : %s" % args.out)
    print("")

    results = []
    for size in sizes:
        results.append(bake(size, chars, args.out, args.font, args.index, args.scale, args.scale_y))

    print("")
    print("=== 自检 ===")
    ok = True
    for r in results:
        if r["overflow"]:
            print("  !! size=%d 有 %d 个字形超出 cell，需要加大 cell" % (r["size"], r["overflow"]))
            ok = False
        if r["tex_h"] > 4096:
            print("  !! size=%d 图集高 %d 超过 4096" % (r["size"], r["tex_h"]))
            ok = False
    if ok:
        print("  全部通过")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
