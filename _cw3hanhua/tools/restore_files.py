# -*- coding: utf-8 -*-
"""从备份里还原**指定的**资产文件（bake_textmesh_assets.py --restore 是整批还原）。

用途：二分定位「哪个文件的烘焙导致了问题」。
  --list                 只列每个文件现在是「原件」还是「已修改」
  level1                 只还原 level1
  level1 resources.assets 只还原这两个

★ 必须用系统 python 跑（venv 里的解释器是 Low 完整性，写不了游戏资产）：
  $env:PYTHONPATH = "<根>\\_cw3hanhua\\.venv\\Lib\\site-packages"
  & "<系统 python 的路径（本机是 python314）>" _cw3hanhua\\tools\\restore_files.py level1
"""
import hashlib
import json
import os
import shutil
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATA = os.path.join(ROOT, "CW3_Data")
BACKUP_DIR = os.path.join(ROOT, "_cw3hanhua", "_assets_orig")
MANIFEST = os.path.join(BACKUP_DIR, "manifest.json")


def sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def load_manifest():
    with open(MANIFEST, "r", encoding="utf-8") as f:
        return json.load(f)


def game_running():
    try:
        out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq CW3.exe"],
                             capture_output=True, text=True).stdout
        return "CW3.exe" in out
    except Exception:
        return False


def main():
    args = [a for a in sys.argv[1:]]
    man = load_manifest()
    files = man["files"]

    if not args or args[0] in ("--list", "-l"):
        print("%-26s %-10s %s" % ("文件", "状态", "现役大小"))
        for name in sorted(files):
            cur = os.path.join(DATA, name)
            if not os.path.exists(cur):
                print("%-26s %-10s %s" % (name, "缺失", "-"))
                continue
            same = sha(cur) == files[name]["sha256"]
            print("%-26s %-10s %d" % (name, "原件" if same else "已修改",
                                      os.path.getsize(cur)))
        return 0

    if game_running():
        print("[拒绝] CW3.exe 正在运行，先退出游戏（游戏会内存映射资产文件）")
        return 1

    for name in args:
        if name not in files:
            print("[跳过] %s 不在备份清单里（可选: %s）"
                  % (name, ", ".join(sorted(files))))
            continue
        bak = os.path.join(BACKUP_DIR, name)
        cur = os.path.join(DATA, name)
        if not os.path.exists(bak):
            print("[失败] 备份缺失: %s" % bak)
            return 1
        want = files[name]["sha256"]
        got = sha(bak)
        if got != want:
            print("[失败] 备份 sha256 不符，拒绝写回: %s" % name)
            return 1
        if os.path.exists(cur) and sha(cur) == want:
            print("[跳过] %s 已是原件" % name)
            continue
        try:
            shutil.copy2(bak, cur)
        except PermissionError as e:
            print("[失败] %s: %s" % (name, e))
            print("       ↳ 用系统 python 跑（venv 的解释器是 Low 完整性，写不了游戏资产）")
            return 1
        print("[还原] %-26s sha256=%s" % (name, want[:10]))

    print("\n当前状态：")
    for name in sorted(files):
        cur = os.path.join(DATA, name)
        same = os.path.exists(cur) and sha(cur) == files[name]["sha256"]
        print("  %-26s %s" % (name, "原件（英文）" if same else "已烘焙（中文）"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
