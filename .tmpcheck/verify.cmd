@echo off
setlocal
set REPO=%~dp0..
cd /d "%REPO%"

echo Running restore...
dotnet restore MercedesEISTool.sln > restore.out 2>&1
set RESTORE_EXIT=%ERRORLEVEL%
echo RESTORE_EXIT=%RESTORE_EXIT%
if not "%RESTORE_EXIT%"=="0" exit /b %RESTORE_EXIT%

echo Running Release build...
dotnet build MercedesEISTool.sln -c Release --no-restore --nologo -v minimal > build.out 2>&1
set BUILD_EXIT=%ERRORLEVEL%
echo BUILD_EXIT=%BUILD_EXIT%
if not "%BUILD_EXIT%"=="0" exit /b %BUILD_EXIT%

echo Running Release tests...
dotnet test MercedesEISTool.sln -c Release --no-build --nologo --logger "console;verbosity=minimal" > test.out 2>&1
set TEST_EXIT=%ERRORLEVEL%
echo TEST_EXIT=%TEST_EXIT%
if not "%TEST_EXIT%"=="0" exit /b %TEST_EXIT%

echo Verification complete.
