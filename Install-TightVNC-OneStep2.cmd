@echo off

:: ================================
:: Auto-elevate to Administrator
:: ================================
net session >nul 2>&1
if %errorLevel% neq 0 (
    PowerShell -Command "Start-Process cmd -ArgumentList '/c ""%~f0""' -Verb RunAs -Wait -WindowStyle Hidden"
    exit /b
)

set BASEDIR=%~dp0
set MSI_NAME=tightvnc-2.8.85-gpl-setup-64bit.msi
set LOG_PATH=%TEMP%\TightVNC_Install.log

:: ===============================
:: REMOVE EXISTING VNC SOFTWARE
:: ===============================

:: --- UltraVNC ---
net stop uvnc_service >nul 2>&1
wmic product where "name like 'UltraVNC%%'" call uninstall /nointeractive >nul 2>&1
sc delete uvnc_service >nul 2>&1
rmdir /s /q "C:\Program Files\uvnc bvba" >nul 2>&1
rmdir /s /q "C:\Program Files (x86)\uvnc bvba" >nul 2>&1
del /f /q "C:\Windows\System32\winvnc.ini" >nul 2>&1
del /f /q "C:\Windows\SysWOW64\winvnc.ini" >nul 2>&1
reg delete "HKLM\Software\uvnc bvba" /f >nul 2>&1
reg delete "HKLM\Software\WOW6432Node\uvnc bvba" /f >nul 2>&1

:: --- TightVNC (old installs) ---
net stop tvnserver >nul 2>&1
net stop tvnserver64 >nul 2>&1
wmic product where "name like 'TightVNC%%'" call uninstall /nointeractive >nul 2>&1
sc delete tvnserver >nul 2>&1
sc delete tvnserver64 >nul 2>&1
rmdir /s /q "C:\Program Files\TightVNC" >nul 2>&1
rmdir /s /q "C:\Program Files (x86)\TightVNC" >nul 2>&1
reg delete "HKLM\Software\TightVNC" /f >nul 2>&1
reg delete "HKLM\Software\WOW6432Node\TightVNC" /f >nul 2>&1
rmdir /s /q "%APPDATA%\TightVNC" >nul 2>&1

:: --- RealVNC ---
net stop "RealVNC Server" >nul 2>&1
wmic product where "name like 'RealVNC%%'" call uninstall /nointeractive >nul 2>&1
sc delete "RealVNC Server" >nul 2>&1
rmdir /s /q "C:\Program Files\RealVNC" >nul 2>&1
rmdir /s /q "C:\Program Files (x86)\RealVNC" >nul 2>&1
reg delete "HKLM\Software\RealVNC" /f >nul 2>&1
reg delete "HKLM\Software\WOW6432Node\RealVNC" /f >nul 2>&1
rmdir /s /q "%APPDATA%\RealVNC" >nul 2>&1
rmdir /s /q "%PROGRAMDATA%\RealVNC" >nul 2>&1

:: ===============================
:: INSTALL TIGHTVNC (CLEAN)
:: ===============================

msiexec /i "%BASEDIR%%MSI_NAME%" /quiet /norestart /log "%LOG_PATH%" ^
ADDLOCAL=Server,Viewer ^
SET_USEVNCAUTHENTICATION=1 ^
VALUE_OF_USEVNCAUTHENTICATION=1 ^
SET_PASSWORD=1 ^
VALUE_OF_PASSWORD=Sham#123 ^
SET_ENABLEFILETRANSFER=1 ^
VALUE_OF_ENABLEFILETRANSFER=1 ^
SET_TRAYICON=1 ^
VALUE_OF_TRAYICON=0

:: Start services
net start tvnserver >nul 2>&1
net start tvnserver64 >nul 2>&1

:: Firewall rule (allow all IPs)
netsh advfirewall firewall add rule ^
name="TightVNC TCP 5900" ^
dir=in action=allow protocol=TCP localport=5900 >nul 2>&1

exit /b
