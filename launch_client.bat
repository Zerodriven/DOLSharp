@echo off
cd /d "C:\DAoC"
echo Launching Dark Age of Camelot connecting to 127.0.0.1:10300...
connect.exe "C:\DAoC\game.dll" 127.0.0.1:10300 testaccount testpassword
