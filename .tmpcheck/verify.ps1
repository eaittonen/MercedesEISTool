$repo = 'c:\Users\Eetu Aittonen\source\repos\MercedesEISTool'
Set-Location $repo

$restoreOutput = Join-Path $repo 'restore.out'
$buildOutput = Join-Path $repo 'build.out'
$testOutput = Join-Path $repo 'test.out'

Write-Host 'Running restore...'
dotnet restore MercedesEISTool.sln *> $restoreOutput
$restoreExit = $LASTEXITCODE
Write-Host "RESTORE_EXIT=$restoreExit"

if ($restoreExit -ne 0) {
  Write-Host 'Restore failed.'
  exit $restoreExit
}

Write-Host 'Running Release build...'
dotnet build MercedesEISTool.sln -c Release --no-restore --nologo -v minimal *> $buildOutput
$buildExit = $LASTEXITCODE
Write-Host "BUILD_EXIT=$buildExit"

if ($buildExit -ne 0) {
  Write-Host 'Build failed.'
  exit $buildExit
}

Write-Host 'Running Release tests...'
dotnet test MercedesEISTool.sln -c Release --no-build --nologo --logger 'console;verbosity=minimal' *> $testOutput
$testExit = $LASTEXITCODE
Write-Host "TEST_EXIT=$testExit"

if ($testExit -ne 0) {
  Write-Host 'Tests failed.'
  exit $testExit
}

Write-Host 'Verification complete.'
