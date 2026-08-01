$ErrorActionPreference = 'Stop'
Set-Location 'c:\Users\Eetu Aittonen\source\repos\MercedesEISTool'
dotnet build 'MercedesEISTool.sln' -c Release --nologo -v minimal *> build-solution.log
$exit = $LASTEXITCODE
Write-Host "SOLUTION_BUILD_EXIT:$exit"
Get-Content build-solution.log -Tail 60
exit $exit
