@echo off
rem Chama o run.ps1 sem exigir mudar a política de execução de scripts do Windows.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*
