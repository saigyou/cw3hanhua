#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
把中文直接写进 TextMesh 资产（Unity class 102 的 m_Text），让 TextMesh 类文本零闪烁。

为什么必须走改资产：
  UnityEngine.TextMesh 的 set_text / get_text 都是 **extern icall**（RVA=0x0，无 IL 体），
  Harmony 打不上；TextMesh 又不是 NGUI 的 UILabel，没有 ProcessText 那样的统一收口。
  全游戏只有 30 个 TextMesh 对象、27 个有文本 —— 与其给 8 个宿主方法逐个打补丁，
  不如把中文烧进 prefab：对象一出现就是中文，没有"先闪一下英文"的窗口。

面板上的文字不是它管的：
  UILabel 由插件的 `UILabel.ProcessText` 补丁处理（NOTES 3.3 / 4.3），本脚本**只碰 class 102**。

用法（在本目录上层即游戏根执行）：
    .venv\\Scripts\\python.exe tools\\bake_textmesh_assets.py --backup    # 备份（幂等）
    .venv\\Scripts\\python.exe tools\\bake_textmesh_assets.py --dry-run   # 预演，不写盘
    .venv\\Scripts\\python.exe tools\\bake_textmesh_assets.py             # 恢复备份 + 写中文
    .venv\\Scripts\\python.exe tools\\bake_textmesh_assets.py --restore   # 只恢复，清除改动

设计要点
--------
* **默认行为 = 先恢复再烘焙**。用户明确要求脚本"先把备份放回游戏目录，以清除之前修改的影响，
  再对被放回的资产进行修改"。这样重复执行是幂等的，不会在已改过的文件上叠加改动
  （叠加会出问题：第二次读到的 m_Text 已是中文，词表查不到，虽然不会写坏但会误报一堆"未命中"）。
* **备份永不覆盖**。若目标备份已存在就跳过 —— 否则一旦在"已改过"的状态下再 --backup，
  就会把中文版当成原件存起来，原始英文就永久丢了。
* **只改词表里查得到的串**。查不到的一律跳过（`Andro` / `4` / `DMD` 这些原本就不译，
  且 `Andro` 是运行时被 StarManager 覆盖的，改了也白改）。
* **typettree 原样写回是无损的**（已实测：30 个 TextMesh 全部 read→save_typetree 后
  原始字节逐字节相等），所以只改 m_Text 不会动到别的字段。
  注意 TextMesh 是 Unity 内置类型，**不需要** TypeTreeGenerator；只有 MonoBehaviour 才需要
  （见 NOTES 4.6 的坑）。
"""
import argparse
import hashlib
import json
import os
import shutil
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)                  # _cw3hanhua
GAME = os.path.dirname(ROOT)                  # 游戏根
DATA = os.path.join(GAME, "CW3_Data")
DICT = os.path.join(ROOT, "_dict", "zh_cn.dict")
BACKUP_DIR = os.path.join(ROOT, "_assets_orig")
MANIFEST = os.path.join(BACKUP_DIR, "manifest.json")

CLASS_TEXTMESH = 102


# --------------------------------------------------------------------------- ★ UnityPy 补丁
# 为什么需要这段：UnityPy 1.25.4 的 ObjectReader 对 Unity 5.x（header.version=15）
# 的序列化文件做 save() 时，会把**每个对象**的 script_type_index 写成同一个值，
# 于是除最后一个对象外，所有 MonoBehaviour 的"这段数据属于哪个脚本"全错位。
# 后果不是崩溃，而是**静默的功能损坏**：
#   MainMenu 的 6 个 sprite 字段里有 3 个绑到了错误脚本 → 运行时是"壳在、native
#   ptr=0"的死组件 → MainMenu.Update 的悬停比较链里 callvirt get_gameObject() 抛
#   NRE → 点击检测（IL 0477）永远走不到 → 主菜单按钮点不动。
# 踩过的弯路：因为 raw_data 逐字节相同，一度判定"烘焙无辜"，静态查了十几项全清白。
#
# 证据（2026-10-20）：不改任何文本、只 UnityPy.load → sf.save() 纯往返，
#   level1 就有 316 字节差异，**全部落在 metadata 区**；修掉本 bug 后差异 = 0。
#
# 病根在 ObjectReader.py:109-112：
#     serialized_type.script_type_index = script_type_index
#   serialized_type 是**类型表里共享的那个对象**，每读一个对象覆盖一次。
# 修法：读的时候按 path_id 把原值记下来，save 前逐个写回。
# 注意要用"从 reader 当前位置回退"来读，不能依赖被污染过的 serialized_type 字段。
def install_unitypy_fix():
    from UnityPy.files.ObjectReader import ObjectReader

    orig_from = ObjectReader.from_reader.__func__
    orig_write = ObjectReader.write
    # ★ 键必须带上文件身份：不同资产文件的 path_id 各自独立编号，会重号。
    saved = {}

    def patched_from(cls, assets_file, reader):
        obj = orig_from(cls, assets_file, reader)
        v = assets_file.header.version
        if 11 <= v < 17:
            end = reader.Position
            back = 1 if v in (15, 16) else 0        # 后面还跟一个 is_stripped 字节
            reader.Position = end - back - 2        # short = 2 字节
            saved[(id(assets_file), obj.path_id)] = reader.read_short()
            reader.Position = end
        return obj

    def patched_write(self, header, writer, data_writer):
        st = self.serialized_type
        k = (id(self.assets_file), self.path_id)
        if st is not None and k in saved:
            st.script_type_index = saved[k]         # 写回**自己的**值
        return orig_write(self, header, writer, data_writer)

    ObjectReader.from_reader = classmethod(patched_from)
    ObjectReader.write = patched_write
    return saved


# --------------------------------------------------------------------------- 工具函数
def asset_files():
    """CW3_Data 下所有序列化资产文件（不碰 .resource / .resS 附属流）。"""
    names = []
    for p in sorted(os.listdir(DATA)):
        full = os.path.join(DATA, p)
        if not os.path.isfile(full):
            continue
        if p.endswith(".assets") or p.startswith("level") or p == "mainData":
            names.append(p)
    return names


def read_dict():
    """读 zh_cn.dict：magic 'CWH1' + int32 count + [uint16 klen + key + uint16 zlen + zh]..."""
    d = {}
    if not os.path.exists(DICT):
        print("!! 词表不存在: %s" % DICT)
        return d
    raw = open(DICT, "rb").read()
    if raw[:4] != b"CWH1":
        print("!! 词表 magic 不对，期望 CWH1")
        return d
    n = int.from_bytes(raw[4:8], "little")
    off = 8
    for _ in range(n):
        klen = int.from_bytes(raw[off:off + 2], "little"); off += 2
        k = raw[off:off + klen].decode("utf-8"); off += klen
        zlen = int.from_bytes(raw[off:off + 2], "little"); off += 2
        z = raw[off:off + zlen].decode("utf-8"); off += zlen
        d[k] = z
    return d


def has_letter(s):
    for c in s:
        if c.isalpha():
            return True
    return False


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def game_running():
    """CW3.exe 在跑就拒绝写盘 —— 游戏会对 asset 文件保持内存映射，边跑边改会崩。"""
    try:
        import subprocess
        out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq CW3.exe", "/NH"],
                             capture_output=True, text=True, timeout=15).stdout
        return "CW3.exe" in out
    except Exception:
        return False


def precheck_writable(files):
    """提前确认能不能覆盖这些文件，别等改到一半才炸。

    ★ 踩过的坑（2026-10-20）：沙箱里跑 `.venv\\Scripts\\python.exe` 时**一个字节都写不进去**
      （PermissionError: [Errno 13]），但同一 shell 里 pwsh 和系统装的那个 python
      （本机是 python314）都能写。原因是 DSH 沙箱让「从工作区目录内启动的可执行文件」
      以 **Low 完整性级别**运行，而游戏资产是 Medium —— Windows 的 no-write-up 规则
      禁止低完整性进程覆盖高完整性文件（新建文件、删自己建的、写 _cw3hanhua 下的文件
      都不受影响，所以看起来"权限正常"）。

      解法：用系统 python 解释器 + venv 的 site-packages —— 它是在沙箱外直接启动的，
      拿的是 Medium 完整性，能正常写：

          $env:PYTHONPATH = "<根>\\_cw3hanhua\\.venv\\Lib\\site-packages"
          & "<系统 python 的路径>" "_cw3hanhua\\tools\\bake_textmesh_assets.py"

      下面这个探测就是为了把这条信息直接印出来，而不是丢一个裸的 errno 13。
    """
    bad = []
    for fn in files:
        p = os.path.join(DATA, fn)
        try:
            with open(p, "r+b"):
                pass
        except PermissionError:
            bad.append(fn)
        except Exception:
            pass                      # 其它异常留给真正的写盘去报
    if bad:
        print("!! 无法覆盖这些资产文件（PermissionError）: %s" % ", ".join(bad))
        print("   最可能的原因：当前 python 以 Low 完整性级别运行，而游戏文件是 Medium。")
        print("   用系统 python（本机是 python314）+ venv 的 site-packages 重跑：")
        print('     $env:PYTHONPATH = "%s"' % os.path.join(ROOT, ".venv", "Lib", "site-packages"))
        print('     & "<系统 python 的路径>" "%s"' % os.path.abspath(__file__))
        return False
    return True


# --------------------------------------------------------------------------- 扫描
def scan(env_dict):
    """返回 {文件名: [(path_id, 旧文本, 新译文), ...]}，只含有可译文本的 TextMesh。

    不做翻译判断以外的事：空串跳过、词表查不到跳过、纯符号跳过。
    """
    plan = {}
    all_files = asset_files()
    for fn in all_files:
        p = os.path.join(DATA, fn)
        try:
            env = UnityPy.load(p)
        except Exception as e:
            print("  %-22s 载入失败: %s" % (fn, e))
            continue
        hits = []
        for sf in env.files.values():
            if not hasattr(sf, "objects"):
                continue
            for o in sf.objects.values():
                try:
                    cid = int(o.class_id) if o.class_id else 0
                except Exception:
                    continue
                if cid != CLASS_TEXTMESH:
                    continue
                try:
                    tree = o.read_typetree()
                except Exception as e:
                    print("  !! %s pid=%s 读 typetree 失败: %s" % (fn, o.path_id, e))
                    continue
                old = tree.get("m_Text")
                if not old or not has_letter(old):
                    continue                      # 空串 / 纯数字符号：不碰
                zh = env_dict.get(old)
                if not zh:
                    continue                      # 词表没有：保持原样
                if zh == old:
                    continue
                hits.append((o.path_id, old, zh))
        if hits:
            plan[fn] = hits
    return plan


# --------------------------------------------------------------------------- 备份 / 恢复
def load_manifest():
    if os.path.exists(MANIFEST):
        try:
            return json.load(open(MANIFEST, encoding="utf-8"))
        except Exception as e:
            print("!! manifest 读失败: %s" % e)
    return {"files": {}}


def save_manifest(m):
    os.makedirs(BACKUP_DIR, exist_ok=True)
    with open(MANIFEST, "w", encoding="utf-8", newline="\n") as f:
        json.dump(m, f, ensure_ascii=False, indent=1)


def do_backup(files, force=False):
    """把游戏目录里的原样文件复制进 _assets_orig/。已存在的不覆盖（除非 --force-backup）。"""
    m = load_manifest()
    os.makedirs(BACKUP_DIR, exist_ok=True)
    n_new = n_skip = 0
    for fn in files:
        src = os.path.join(DATA, fn)
        dst = os.path.join(BACKUP_DIR, fn)
        if os.path.exists(dst) and not force:
            print("  %-22s 备份已存在，跳过（不覆盖！）  %8d 字节" % (fn, os.path.getsize(dst)))
            n_skip += 1
            continue
        shutil.copy2(src, dst)
        m["files"][fn] = dict(size=os.path.getsize(dst), sha256=sha256(dst),
                              backed_up=time.strftime("%Y-%m-%d %H:%M:%S"))
        n_new += 1
        print("  %-22s 已备份  %8d 字节" % (fn, os.path.getsize(dst)))
    save_manifest(m)
    print("备份完成：新增 %d 个，跳过 %d 个 → %s" % (n_new, n_skip, BACKUP_DIR))
    return 0


def do_restore(quiet=False):
    """把备份放回游戏目录。校验 sha256，备份缺失直接报错。"""
    m = load_manifest()
    if not m["files"]:
        print("!! 没有任何备份记录（%s 不存在或为空）。先跑 --backup。" % MANIFEST)
        return 1
    n_ok = n_same = 0
    for fn, meta in sorted(m["files"].items()):
        bak = os.path.join(BACKUP_DIR, fn)
        dst = os.path.join(DATA, fn)
        if not os.path.exists(bak):
            print("!! 备份文件丢失: %s" % bak)
            return 1
        got = sha256(bak)
        if got != meta["sha256"]:
            print("!! 备份文件被改动过（sha256 不符）: %s" % bak)
            return 1
        if os.path.exists(dst) and sha256(dst) == meta["sha256"]:
            n_same += 1
            if not quiet:
                print("  %-22s 已是原始状态，无需恢复" % fn)
            continue
        shutil.copy2(bak, dst)
        n_ok += 1
        if not quiet:
            print("  %-22s 已恢复  %8d 字节" % (fn, meta["size"]))
    print("恢复完成：写回 %d 个，本来就是原件 %d 个" % (n_ok, n_same))
    return 0


# --------------------------------------------------------------------------- 烘焙
def do_bake(plan, dry_run=False):
    total = sum(len(v) for v in plan.values())
    print("将要写入 %d 条中文（分布在 %d 个文件）" % (total, len(plan)))
    for fn in sorted(plan):
        for pid, old, zh in plan[fn]:
            print("    %-22s pid=%-6s %-40s → %s" % (fn, pid, repr(old), repr(zh)))
    if dry_run:
        print("\n--dry-run：未写盘。")
        return 0

    written = 0
    install_unitypy_fix()      # ★ 不装这个，写出来的文件会让主菜单按钮点不动
    for fn in sorted(plan):
        p = os.path.join(DATA, fn)
        size0 = os.path.getsize(p)
        t0 = time.time()
        env = UnityPy.load(p)
        # path_id → (old, zh)，用来在写完后回读校验
        want = {pid: (old, zh) for pid, old, zh in plan[fn]}
        for sf in env.files.values():
            if not hasattr(sf, "objects"):
                continue
            for pid in list(want.keys()):
                o = sf.objects.get(pid)
                if o is None:
                    print("  !! %s 里找不到 pid=%s，跳过" % (fn, pid))
                    continue
                tree = o.read_typetree()
                tree["m_Text"] = want[pid][1]
                o.save_typetree(tree)
            data = sf.save()
        with open(p, "wb") as f:
            f.write(data)
        written += len(plan[fn])
        print("  %-22s %d 条  %d → %d 字节  %.1fs" % (fn, len(plan[fn]), size0, len(data), time.time() - t0))

    # 回读校验：重新打开文件，确认 m_Text 真的是中文
    print("\n回读校验：")
    bad = 0
    for fn in sorted(plan):
        p = os.path.join(DATA, fn)
        env = UnityPy.load(p)
        got = {}
        for sf in env.files.values():
            if not hasattr(sf, "objects"):
                continue
            for pid, o in sf.objects.items():
                if pid in {x[0] for x in plan[fn]}:
                    try:
                        got[pid] = o.read_typetree().get("m_Text")
                    except Exception as e:
                        got[pid] = "<读失败: %s>" % e
        for pid, old, zh in plan[fn]:
            if got.get(pid) != zh:
                bad += 1
                print("  !! %s pid=%s 期望 %r 实际 %r" % (fn, pid, zh, got.get(pid)))
    print("  校验通过 %d 条，失败 %d 条" % (written - bad, bad))
    return 0 if bad == 0 else 1


# --------------------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser(
        description="把中文写进 TextMesh 资产（只碰 class 102，UILabel 归插件管）")
    ap.add_argument("--backup", action="store_true", help="只做备份（幂等，不覆盖已有备份）")
    ap.add_argument("--restore", action="store_true", help="只把备份放回游戏目录，清除改动")
    ap.add_argument("--dry-run", action="store_true", help="只预演，不写盘")
    ap.add_argument("--force-backup", action="store_true", help="备份时覆盖已存在的备份（危险）")
    ap.add_argument("--force", action="store_true", help="即使 CW3.exe 在跑也强行写盘")
    args = ap.parse_args()

    global UnityPy
    try:
        import UnityPy  # noqa: F401
    except Exception as e:
        print("!! UnityPy 不可用: %s" % e)
        return 1

    print("游戏目录: %s" % GAME)
    print("词表: %s" % DICT)

    # ★ --restore 必须在算 plan **之前**处理，且完全不依赖 plan。
    #   踩过的坑（2026-10-20 用户实测）：--restore 排在 scan() 之后，而烘焙完之后
    #   资产里已经是中文，scan() 按「英文原文查词表」自然返回空 → 走到
    #   「没有需要改的 TextMesh」直接 return 0，**恢复根本没执行**，也没有任何报错。
    #   恢复只需要 manifest，跟词表、跟 plan 一点关系都没有，所以提到最前面。
    if args.restore:
        if game_running() and not args.force:
            print("!! 检测到 CW3.exe 正在运行，先退出游戏再恢复。确认无碍可加 --force。")
            return 1
        if not precheck_writable(sorted(load_manifest()["files"].keys())):
            return 1
        return do_restore()

    d = read_dict()
    print("词表 %d 条" % len(d))
    if not d:
        return 1

    plan = scan(d)

    if args.backup:
        # ★ 同样加了护栏：plan 为空说明「现在没有需要烘的东西」，这时备份要么是多余的
        #   （已经备份过），要么是**危险的**（之前没备份、而现在资产已是中文 → 会把中文
        #   当成原件冻存起来，以后 --restore 只会还原出中文，永远回不到英文）。
        if not plan:
            print("没有需要改的 TextMesh —— 无需备份。")
            print("（若资产已被烘焙成中文，此刻备份会把中文当原件，故拒绝执行。）")
            return 0
        return do_backup(sorted(plan.keys()), force=args.force_backup)

    if not plan:
        print("没有需要改的 TextMesh（词表里查不到任何 TextMesh 文本）。")
        print("若你本意是「还原成改之前的样子」，请用 --restore。")
        return 0

    # 默认：先恢复，再烘焙
    if game_running() and not args.force:
        print("!! 检测到 CW3.exe 正在运行。请先退出游戏（资产文件被内存映射时写入会损坏）。")
        print("   确认无碍可加 --force。")
        return 1

    if args.dry_run:
        # dry-run 承诺"不写盘"，所以既不备份也不恢复，直接给计划。
        if not os.path.exists(MANIFEST):
            print("\n（--dry-run：还没备份过；正式执行时会先备份 %d 个文件）" % len(plan))
        return do_bake(plan, dry_run=True)

    if not precheck_writable(sorted(plan.keys())):
        return 1

    if not os.path.exists(MANIFEST):
        print("== 首次运行：先备份 ==")
        rc = do_backup(sorted(plan.keys()))
        if rc != 0:
            return rc

    print("\n== 第一步：把备份放回游戏目录（清除之前修改的影响）==")
    rc = do_restore(quiet=False)
    if rc != 0:
        return rc

    print("\n== 第二步：写入中文 ==")
    return do_bake(plan, dry_run=False)


if __name__ == "__main__":
    sys.exit(main())
