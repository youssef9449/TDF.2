@echo off
echo Cleaning TDFMAUI solution...

echo Stopping any running instances of TDFMAUI...
taskkill /f /im TDFMAUI.exe 2>nul

echo Cleaning build directories...
rmdir /s /q TDFMAUI\bin 2>nul
rmdir /s /q TDFMAUI\obj 2>nul

echo Cleaning NuGet packages...
dotnet nuget locals all --clear

echo Cleaning Visual Studio temporary files...
rmdir /s /q .vs 2>nul

echo Cleaning complete. Please restart Visual Studio and rebuild the solution.
