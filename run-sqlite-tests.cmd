@echo off
cd /d "%~dp0"
dotnet test MercedesEISTool.Tests\MercedesEISTool.Tests.csproj --filter "FullyQualifiedName~SqliteMigrationTests" --logger "console;verbosity=minimal" --nologo > sqlite-migration-test-output.log 2>&1
set EXITCODE=%ERRORLEVEL%
echo EXIT_CODE=%EXITCODE% > sqlite-migration-test-exitcode.txt
exit /b %EXITCODE%
