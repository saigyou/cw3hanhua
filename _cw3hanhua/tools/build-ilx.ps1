# 重新编译 ilx.exe（改过 ilx\ilx.cs 之后跑一次）
#
# 为什么用 3.5 的 csc：ilx 自己不是被注入的插件，跑在桌面 CLR 上，
# 用哪个版本都行；这里沿用项目统一的 3.5 csc，避免再多一条构建路径。
$ErrorActionPreference = 'Stop'

$game = 'C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$cecil = Join-Path $game 'BepInEx\core\Mono.Cecil.dll'

if (-not (Test-Path $cecil)) { throw "找不到 Mono.Cecil: $cecil" }

& 'C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe' /nologo /target:exe `
    /out:"$here\ilx.exe" `
    /reference:"$cecil" `
    "$here\ilx\ilx.cs"

if ($LASTEXITCODE -ne 0) { throw "csc 失败，退出码 $LASTEXITCODE" }

# 把 Cecil 复制到 exe 旁边，保证目录可以整体搬走
Copy-Item $cecil (Join-Path $here 'Mono.Cecil.dll') -Force
Write-Host "OK -> $(Join-Path $here 'ilx.exe')"
