@echo off
REM ============================================================
REM Build the CW3 Hanhua plugin (ONE DLL: font layer + text layer).
REM
REM Constraints learned the hard way -- do not change:
REM   /platform:x86    game is a 32-bit process; AnyCPU DLLs fail to load
REM   /codepage:65001  sources are UTF-8 without BOM; needed for CJK comments
REM   csc 3.5          match the game Mono runtime, avoid newer BCL APIs
REM
REM Note: the game assembly defines a global Path class that shadows
REM       System.IO.Path -- sources must spell out System.IO.Path.
REM
REM This file is ASCII + CRLF on purpose (cmd.exe reads OEM codepage).
REM ============================================================
setlocal
cd /d "%~dp0..\.."

set "CSC=C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe"
set "PLUGDIR=BepInEx\plugins"

if not exist "%CSC%" (
  echo [ERROR] csc 3.5 not found: %CSC%
  exit /b 1
)

echo == CW3Hanhua.dll (font layer + text layer, one assembly) ==
"%CSC%" /nologo /target:library /platform:x86 /codepage:65001 ^
  /reference:BepInEx\core\BepInEx.dll ^
  /reference:BepInEx\core\0Harmony.dll ^
  /reference:CW3_Data\Managed\UnityEngine.dll ^
  /reference:CW3_Data\Managed\Assembly-CSharp.dll ^
  /out:%PLUGDIR%\CW3Hanhua.dll ^
  _cw3hanhua\plugin\CW3Hanhua.cs ^
  _cw3hanhua\plugin\CW3HanhuaText.cs
if errorlevel 1 goto :fail

REM ---- v1.0.0: the two layers are ONE plugin now. The old second DLL would be a
REM      duplicate BepInPlugin id (same "cw3hanhua.text" GUID) -- remove it.
if exist "%PLUGDIR%\CW3HanhuaText.dll" (
  echo == removing obsolete CW3HanhuaText.dll ==
  del "%PLUGDIR%\CW3HanhuaText.dll"
)

REM ---- optional: temporary diagnostic layer - delete the .cs and it disappears ----
REM WARNING: no unescaped ')' may appear anywhere inside the block below, not even
REM          inside an echo's text. cmd closes the if-block at the FIRST unbalanced
REM          ')' it sees, so a stray one in a message leaks the whole block out and
REM          csc then runs unconditionally with a source file that may not exist.
if exist "_cw3hanhua\plugin\CW3HanhuaDiag.cs" (
  echo == CW3HanhuaDiag.dll - TEMP diag, delete after use ==
  "%CSC%" /nologo /target:library /platform:x86 /codepage:65001 ^
    /reference:BepInEx\core\BepInEx.dll ^
    /reference:BepInEx\core\0Harmony.dll ^
    /reference:CW3_Data\Managed\UnityEngine.dll ^
    /reference:CW3_Data\Managed\Assembly-CSharp.dll ^
    /out:%PLUGDIR%\CW3HanhuaDiag.dll ^
    _cw3hanhua\plugin\CW3HanhuaDiag.cs
  if errorlevel 1 goto :fail
)

echo.
echo == OK ==
dir /b %PLUGDIR%\CW3Hanhua*.dll
endlocal & exit /b 0

:fail
echo [ERROR] build failed
endlocal & exit /b 1
