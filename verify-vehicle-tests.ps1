$ErrorActionPreference = 'Stop'
Set-Location 'c:\Users\Eetu Aittonen\source\repos\MercedesEISTool'
dotnet test 'MercedesEISTool.Tests/MercedesEISTool.Tests.csproj' -c Release --filter FullyQualifiedName~VehicleLookupTests --logger 'console;verbosity=minimal' *> test-vehicle.log
$exit = $LASTEXITCODE
Write-Host "VEHICLE_TEST_EXIT:$exit"
Get-Content test-vehicle.log -Tail 80
