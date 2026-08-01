$ErrorActionPreference = 'Stop'
Set-Location 'c:\Users\Eetu Aittonen\source\repos\MercedesEISTool'
dotnet build 'MercedesEISTool.Server/MercedesEISTool.Server.csproj' -c Release --nologo -v minimal *> build-server.log
$exit = $LASTEXITCODE
Write-Host "SERVER_BUILD_EXIT:$exit"
Get-Content build-server.log -Tail 40
