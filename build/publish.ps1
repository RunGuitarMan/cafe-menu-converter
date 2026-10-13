$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet test CafeMenuConverter.sln -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
$output = Join-Path $PWD 'artifacts/publish/win-x64'
if (Test-Path $output) { Remove-Item $output -Recurse -Force }
dotnet publish src/CafeMenuConverter.App -c Release -r win-x64 -p:StandaloneRelease=true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
python build/verify-package.py win-x64 $output
if ($LASTEXITCODE -ne 0) { throw 'Package verification failed' }
New-Item artifacts/releases -ItemType Directory -Force | Out-Null
Copy-Item "$output/CafeMenuConverter.App.exe" artifacts/releases/CafeMenuConverter-win-x64.exe -Force
python build/package-notices.py
if ($LASTEXITCODE -ne 0) { throw 'Notices packaging failed' }
