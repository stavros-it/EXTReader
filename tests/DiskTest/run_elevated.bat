@echo off
cd /d "%~dp0"
dotnet "bin\Debug\net8.0-windows\DiskTest.dll" 5 > "%TEMP%\disktest_output.txt" 2>&1
