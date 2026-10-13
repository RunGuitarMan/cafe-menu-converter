$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet test CafeMenuConverter.sln -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
dotnet publish src/CafeMenuConverter.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish/win-x64
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
New-Item artifacts/releases -ItemType Directory -Force | Out-Null
Copy-Item README.md, THIRD-PARTY-NOTICES.txt artifacts/publish/win-x64/
Copy-Item build/licenses artifacts/publish/win-x64/ -Recurse -Force
Compress-Archive artifacts/publish/win-x64/* artifacts/releases/CafeMenuConverter-win-x64.zip -Force
