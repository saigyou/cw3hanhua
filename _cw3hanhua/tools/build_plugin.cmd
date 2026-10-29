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

REM ---- probe that we can overwrite the target BEFORE calling csc ----
REM csc only reports "CS0016: could not write to output file -- access denied"
REM without distinguishing a locked file, a read-only attribute, or an ACL
REM permission mismatch. The probe copies the file out and back in place,
REM exercising the same write path csc needs.
set "WTEST=%TEMP%\cw3_wtest.tmp"
set "PROBE=ok"
if not exist "%PLUGDIR%\CW3Hanhua.dll" goto :probe_done
REM read-only first: copy /y also fails on it, and that is a different fix.
for %%A in ("%PLUGDIR%\CW3Hanhua.dll") do set "ATTR=%%~aA"
if not "%ATTR:R=%"=="%ATTR%" goto :probe_readonly
copy /y "%PLUGDIR%\CW3Hanhua.dll" "%WTEST%" >nul 2>&1
if errorlevel 1 set "PROBE=read"
copy /y "%WTEST%" "%PLUGDIR%\CW3Hanhua.dll" >nul 2>&1
if errorlevel 1 set "PROBE=overwrite"
del "%WTEST%" >nul 2>&1
:probe_done
if not "%PROBE%"=="ok" goto :probe_fail

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

:probe_readonly
echo.
echo [ERROR] %PLUGDIR%\CW3Hanhua.dll has the read-only attribute set.
echo.
echo   That alone makes csc fail with "CS0016 ... access denied", and the fix
echo     is one word:  attrib -r "%PLUGDIR%\CW3Hanhua.dll"
echo.
endlocal & exit /b 1

:probe_fail
echo.
echo [ERROR] cannot overwrite %PLUGDIR%\CW3Hanhua.dll -- probe says: %PROBE%
echo.
echo   Possible causes:
echo     1. CW3.exe is still running and holding the DLL open.
echo     2. Current process lacks write/modify permission on the existing file
echo        or directory ACL (e.g. created by another user or elevated session).
echo.
echo   Fix: close the game if running, or delete "%PLUGDIR%\CW3Hanhua.dll"
echo     manually and run this script again.
echo.
echo   --- Current ACL of %PLUGDIR%\CW3Hanhua.dll ---
icacls "%PLUGDIR%\CW3Hanhua.dll"
echo.
endlocal & exit /b 1

:fail
echo [ERROR] build failed
endlocal & exit /b 1
