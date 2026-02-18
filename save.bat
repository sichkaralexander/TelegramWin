@echo off
chcp 65001 > nul
cd /d "%~dp0"
set /p msg=Введите описание изменений: 
git add -A
git commit -m "%msg%"
git status
pause
