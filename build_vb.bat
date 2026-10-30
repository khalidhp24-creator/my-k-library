@echo off
echo Compiling My K Library (Program.vb)...
echo.

:: Compiling for Windows Subsystem (no command window), 32-bit for Jet MDB compatibility.
:: Explicit references ensure all required assemblies are linked correctly.
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\vbc.exe" ^
    /target:winexe ^
    /platform:x86 ^
    /win32icon:bookic2.ico ^
    /out:LibraryAppVB.exe ^
    /reference:System.dll,System.Data.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Xml.dll,System.Core.dll,Microsoft.VisualBasic.dll ^
    Program.vb

echo.
if %errorlevel% equ 0 (
    echo [SUCCESS] Compilation complete. LibraryAppVB.exe is ready.
) else (
    echo [FAILED]  Compilation failed. Check the errors above.
)
pause
