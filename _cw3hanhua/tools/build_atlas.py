#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
把「原版 GUI Atlas + 中文字形」合并成一张大图集，产出插件运行时要用的文件。

★ 为什么要合并：
    原版里字体是 GUI Atlas 上的精灵，文字和背景共用同一个材质 → 同一个 draw call
    → 遮挡顺序由 mesh 内按 depth 排序的三角形决定（所以设置界面能盖住悬停框）。
    我们一新建独立材质，就必然拆成两个 draw call，遮挡关系就乱了。
    把中文字形并进同一张图集，UIFont 就能继续用图集材质 → 不再拆 draw call。

★ 布局（4096x8192，实测 maxTextureSize=16384 允许）：
    (0,0)-(511,1023)      原版 GUI Atlas 1:1 贴【左上角】
                            → 101 个精灵的 outer/inner 一个都不用改
    y=1025 起，三套字号纵向排（实测值，随字集略有浮动）：
        size16  y 1025..1764
        size20  y 1766..2845
        size28  y 2847..4734
    剩余 y 4735..8191 留作将来补字

★ 坐标约定（★ 踩了四轮才定，别改）：
    sprite.outer 的 y 和 BMFont glyph.y **都是从图像顶部往下量**（顶左原点）。
    依据：NGUIMath.ConvertToTexCoords 做的 yMin = 1 - yMax/h 翻转，
    其成立前提就是 rect 本身是顶左原点；UIFont.Print 里
    zero3.y = mUVRect.yMax - num9 * glyph.y，mUVRect=(0,0,1,1) 时 yMax=1（顶部）。
    像素实证：精灵 'NGUI' outer=(0,646,255,75)，PIL 行 646..720 有 19087 个不透明像素
    （就是 NGUI logo），行 303..377（若从底量）只有 1523 个零碎像素。

    所以：原图贴 PIL (0,0)，中文字形从 y=1025 往下排，两张互不重叠。
    下面有断言，中文字形压到原图区就直接报错退出。

★ font_XX.png 是 PIL 'L' 模式（白字黑底）。**不能直接 convert("RGBA")** ——
    那会把黑底变成 alpha=255 的不透明黑块（实测整片 (0,0,0,255)）。
    正确做法：灰度值当 alpha、RGB 全 255，这样 draw call 的顶点色才能正确给字上色。

用法：
    .venv\\Scripts\\python.exe tools/build_atlas.py
    .venv\\Scripts\\python.exe tools/build_atlas.py --width 4096 --height 8192
    .venv\\Scripts\\python.exe tools/build_atlas.py --write-json    # 额外出调试 JSON
"""
import argparse
import json
import os
import struct
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
GAME = os.path.dirname(ROOT)
DATA = os.path.join(GAME, "CW3_Data")
FONT_DIR = os.path.join(ROOT, "_font")
OUT_PNG = os.path.join(FONT_DIR, "atlas_zh.png")
OUT_JSON = os.path.join(FONT_DIR, "atlas_zh.json")

DEFAULT_W, DEFAULT_H = 4096, 8192


def load_gui_atlas():
    """从资产里解出 GUI Atlas（512x1024 RGBA32 + mip）。返回 PIL Image（左上原点）。"""
    import UnityPy
    for fn in sorted(os.listdir(DATA)):
        p = os.path.join(DATA, fn)
        if not (fn.endswith(".assets") or fn.startswith("level")):
            continue
        if not os.path.isfile(p):
            continue
        try:
            env = UnityPy.load(p)
        except Exception:
            continue
        for sf in list(env.files.values()):
            if not hasattr(sf, "objects"):
                continue
            for pid, o in list(sf.objects.items()):
                if int(o.class_id or 0) != 28:
                    continue
                try:
                    d = o.read_typetree()
                except Exception:
                    continue
                if str(d.get("m_Name")) != "GUI Atlas":
                    continue
                w, h = d["m_Width"], d["m_Height"]
                fmt = d["m_TextureFormat"]
                raw = bytes(d["image data"])
                if fmt != 4:
                    raise SystemExit("!! GUI Atlas 格式不是 RGBA32(fmt=4)，实际 %d" % fmt)
                base = raw[: w * h * 4]
                img = Image.frombytes("RGBA", (w, h), base)
                # Unity 原点左下 → PIL 原点左上
                img = img.transpose(Image.FLIP_TOP_BOTTOM)
                print("  原图集: %s  %dx%d  %d 字节" % (fn, w, h, len(raw)))
                return img, fn
    raise SystemExit("!! 找不到 GUI Atlas")


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    ap = argparse.ArgumentParser()
    ap.add_argument("--width", type=int, default=DEFAULT_W)
    ap.add_argument("--height", type=int, default=DEFAULT_H)
    ap.add_argument("--out-png", default=OUT_PNG)
    ap.add_argument("--out-json", default=OUT_JSON)
    ap.add_argument("--write-json", action="store_true",
                    help="额外写出调试用 JSON（插件和工具链都不读它，默认不生成）")
    args = ap.parse_args()
    W, H = args.width, args.height

    print("=== 构建合并图集 %dx%d ===" % (W, H))

    orig, src_name = load_gui_atlas()
    ow, oh = orig.size

    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    # ★★★ 坐标约定（这里是整个方案最容易错的地方，2026-10-18 踩过三轮）：
    #
    #   NGUI 里「精灵」和「字形」的 y 方向是【相同】的 —— 都是【从顶部往下量】：
    #
    #   ① UISprite：mUVRect = NGUIMath.ConvertToTexCoords(mSprite.outer, w, h)
    #      ConvertToTexCoords 做的是 yMin = 1 - yMax/height，yMax = 1 - yMin/height。
    #      这个翻转换算成立的**前提**就是 sprite.outer 本身是**顶左原点**（TG/TexturePacker 约定）。
    #      → 原图必须贴在【左上角】，101 个 outer/inner 才能一个都不改。
    #
    #   ② UIFont.Print（反编译原文）：
    #          zero3.y = mUVRect.yMax - (uvRect.height / mFont.texHeight) * glyph.y;
    #      yMax 是 UV 空间的 1.0（=顶部），glyph.y 越大越往下
    #      → glyph.y 也是**从顶部往下量的行号**（BMFont 标准约定）。
    #
    #   ③ 所以两块区域是**上下排布**，都以 PIL 行号直接表示：
    #      PIL 行 [0, oh)                 ← 原图（sprite 的 outer 不用改）
    #      PIL 行 [oh+gap, ...)           ← 中文字形（glyph.y = PIL 行号）
    #
    #   ⚠ 踩过的坑：
    #     v1 把原图贴 (0,0) 且字形也从 y_cursor=0 开始 → 字形把原图区盖掉，
    #        **文字和背景一起消失**。
    #     v2/v3 把原图误贴到底部 (H-oh) → sprite 的 outer 全指到中文区/空白区，
    #        **背景错位、撕裂**（且中文仍不显示）。
    #   两轮都误以为「sprite 的 y 从底部量」，实际是从顶部量。
    orig_y_pil = 0
    canvas.paste(orig, (0, orig_y_pil))
    print("  原图贴到 PIL (0,%d)（顶左原点，与 sprite.outer 一致），占 %dx%d"
          % (orig_y_pil, ow, oh))

    # ---------------- 中文字形
    meta = {"version": 3, "width": W, "height": H,
            "glyph_y_from": "top", "sprite_y_from": "top",
            "original": {"texture": src_name, "width": ow, "height": oh,
                         "dest_pil": {"x": 0, "y": orig_y_pil, "pixelSize": 1}},
            "sizes": {}}

    y_cursor = oh + 1     # ★ 中文字形从原图【下方】开始排（原图占 0..oh-1）
    for size in (16, 20, 28):
        fnt_json = os.path.join(FONT_DIR, "font_%d.json" % size)
        if not os.path.exists(fnt_json):
            print("  !! 缺 %s，跳过 size=%d" % (fnt_json, size))
            continue
        with open(fnt_json, encoding="utf-8") as f:
            fm = json.load(f)
        glyph_png = os.path.join(FONT_DIR, "font_%d.png" % size)
        if not os.path.exists(glyph_png):
            print("  !! 缺 %s，跳过" % glyph_png)
            continue
        # ★ font_XX.png 是 L 模式（灰度：白字 + 黑底）。
        #   ★★ 不能直接 convert("RGBA") —— 那会把黑色背景变成 alpha=255 的不透明黑，
        #      贴进图集后中文字形区是一整片不透明黑块（2026-10-18 第四轮实测踩到）。
        #   正确做法：灰度值当 alpha，RGB 全设 255。
        #   这样 draw call 的顶点色（label.color / 富文本标记）才能正确给字上色。
        #
        # ★★★ 2026-10-21：贴图时**不能用 tile 自己当 mask**（`paste(tile, pos, tile)`）。
        #   tile 的 RGB 是纯白、alpha 是灰度，拿它当遮罩往全透明画布上合成，等于
        #   "src 与 dst 都取 tile"，结果是：
        #       alpha: a → a²        （抗锯齿中间调被平方）
        #       RGB  : 255 → 255·a   （白字被染成灰度）
        #   而 draw call 的透明混合仍按 rgb·alpha 叠 → 实际覆盖率变成 a³，
        #   0.5 的边缘像素只剩 0.125，笔画毛边/发虚。实测整字集墨量 464526 → 385399
        #   （只剩 83%），且损失集中在那圈负责"干净过渡"的浅灰像素上。
        #   tile 自带透明背景，直接整块覆盖即可。
        _g = Image.open(glyph_png).convert("L")
        gimg = Image.new("RGBA", _g.size, (255, 255, 255, 0))
        gimg.putalpha(_g)
        cell = fm["cell"]
        cols = W // cell
        glyphs = fm["glyphs"]
        n = len(glyphs)

        # 在 font_XX.png 里，字形按 cell 网格排列（bake_font.py 的布局）
        per_row_src = fm["texWidth"] // cell

        placed = []
        maxy = y_cursor
        for i, g in enumerate(glyphs):
            if g["w"] <= 0:
                # 空字形（空格）：只保留度量，不占位置
                placed.append(dict(code=g["code"], x=0, y=0, w=0, h=0,
                                   offX=g["offX"], offY=g["offY"], adv=g["adv"]))
                continue
            col = i % cols
            row = i // cols
            dx = col * cell
            dy = y_cursor + row * cell
            # 从源图对应格子取（源图同样按 cell 网格放）
            sx = (i % per_row_src) * cell
            sy = (i // per_row_src) * cell
            box = (sx, sy, sx + cell, sy + cell)
            tile = gimg.crop(box)
            # ★ 自带透明背景，直接覆盖。不要写成 paste(tile, pos, tile)（见上文的平方 bug）。
            canvas.paste(tile, (dx, dy))
            # ★ glyph.y = PIL 行号，**不换算**。
            #   UIFont.Print 里 zero3.y = yMax - (uvH/texHeight) * glyph.y，
            #   glyph.y 大 → 往下 → 正是 PIL 行号的方向（从顶部量）。
            placed.append(dict(code=g["code"], x=dx, y=dy,
                               w=g["w"], h=g["h"],
                               offX=g["offX"], offY=g["offY"], adv=g["adv"]))
            if dy + cell > maxy:
                maxy = dy + cell

        rows = (n + cols - 1) // cols
        meta["sizes"][str(size)] = {
            "cell": cell,
            "cols": cols,
            "rows": rows,
            "y_top": y_cursor,
            "count": n,
            "glyphs": placed,
        }
        print("  size=%-2d  %d 字形  cols=%d rows=%d  y %d..%d"
              % (size, n, cols, rows, y_cursor, y_cursor + rows * cell - 1))
        y_cursor = maxy + 1

    if y_cursor > H:
        print("!! 放不下：需要到 y=%d，画布只有 %d" % (y_cursor, H))
        return 1

    # ★ 关键断言：中文字形区必须从原图【下方】开始，不能压到原图（PIL 行 0..oh-1）
    if y_cursor - oh - 1 > 0:
        pass  # 初始 y_cursor = oh+1 是正常的
    first_zh_y = oh + 1
    # 检查每个字号的起始行都 > oh
    for s in meta["sizes"].values():
        if s["y_top"] < oh:
            print("!! size=%s 的 y_top=%d 压到原图区（原图占 0..%d）"
                  % (s.get("cell"), s["y_top"], oh - 1))
            return 1

    print("  中文区从 y=%d 开始，原图占 y=0..%d，结束 y=%d，余量 %d 行"
          % (first_zh_y, oh - 1, y_cursor, H - y_cursor))

    canvas.save(args.out_png, "PNG", optimize=True)
    # ★ atlas_zh.json 只是调试用副产物，插件和工具链都不读它（运行时只读 .bin）。
    #   默认不写，避免 _font 里堆一个 1.6 MB 的无用文件；要看得加 --write-json。
    if args.write_json:
        with open(args.out_json, "w", encoding="utf-8") as f:
            json.dump(meta, f, ensure_ascii=False, separators=(",", ":"))

    # ---------- 同时产出 C# 端直接读的二进制（避免在插件里解析大 JSON）
    #   magic "CWA1" | int32 W | int32 H | int32 nSizes
    #   每个字号: int32 size | int32 cell | int32 count
    #             count × (int32 code, int32 x, int32 y, int32 w, int32 h,
    #                      int32 offX, int32 offY, int32 adv)
    #   末尾 uint32 FNV-1a
    binpath = os.path.join(os.path.dirname(args.out_json), "atlas_zh.bin")
    body = bytearray()
    body += struct.pack("<iii", W, H, len(meta["sizes"]))
    for size_str in sorted(meta["sizes"], key=lambda s: int(s)):
        sz = meta["sizes"][size_str]
        gl = sz["glyphs"]
        body += struct.pack("<iii", int(size_str), sz["cell"], len(gl))
        for g in gl:
            body += struct.pack("<iiiiiiii", g["code"], g["x"], g["y"],
                                g["w"], g["h"], g["offX"], g["offY"], g["adv"])
    with open(binpath, "wb") as f:
        f.write(b"CWA1")
        f.write(body)
        f.write(struct.pack("<I", _fnv1a(bytes(body))))

    print("")
    print("→ %s  (%.1f MB 内存, 文件 %.1f MB)"
          % (args.out_png, W * H * 4.0 / 1024 / 1024,
             os.path.getsize(args.out_png) / 1024.0 / 1024.0))
    print("→ %s" % args.out_json)
    print("→ %s  (%.1f KB, 插件直读)" % (binpath, os.path.getsize(binpath) / 1024.0))
    return 0


def _fnv1a(data):
    h = 0x811C9DC5
    for b in data:
        h ^= b
        h = (h * 0x01000193) & 0xFFFFFFFF
    return h


if __name__ == "__main__":
    sys.exit(main())
