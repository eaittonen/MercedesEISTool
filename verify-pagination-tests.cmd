@echo off
cd /d "%~dp0"
dotnet test MercedesEISTool.Tests\MercedesEISTool.Tests.csproj --no-restore --filter FullyQualifiedName~MyFilesPaginationTests > pagination-test.log 2>&1
echo EXIT_CODE=%ERRORLEVEL%>> pagination-test.log
type pagination-test.log
exit /b %ERRORLEVEL%
