@echo off
cd /d D:\AI\tran
"C:\Program Files\Git\cmd\git.exe" add .
"C:\Program Files\Git\cmd\git.exe" commit -m "Auto sync: %date% %time%"
"C:\Program Files\Git\cmd\git.exe" push origin main
echo Sync completed at %date% %time% >> D:\AI\tran\auto_sync.log
