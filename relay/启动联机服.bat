@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ============================================
echo   SFM Online Relay - One-Click Start
echo   Windows relay + admin panel
echo ============================================
where python >nul 2>nul
if errorlevel 1 (
  echo [ERROR] Python not found. Install Python 3.10+ and tick "Add to PATH".
  pause
  exit /b 1
)
python -m pip install -r requirements.txt -q
set "SFM_CFG=%~dp0config.json"
set "SFM_STATE=%~dp0state.json"
set "SFM_CMD=%~dp0commands.json"
set "SFM_LOG=%~dp0relay.log"
set "SFM_ADMIN_PORT=7001"
echo Starting relay on port 7000 ...
start "SFM Relay" cmd /k "python relay.py"
timeout /t 1 /nobreak >nul
echo Starting admin panel on http://127.0.0.1:7001 ...
start "SFM Admin" cmd /k "python admin.py"
echo Done. Keep both windows open.
pause
