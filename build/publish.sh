#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$ROOT/artifacts/nuget}"
export NUGET_HTTP_CACHE_PATH="${NUGET_HTTP_CACHE_PATH:-$ROOT/artifacts/nuget-http}"
dotnet test CafeMenuConverter.sln -c Release
for rid in win-x64 osx-arm64; do
  dotnet publish src/CafeMenuConverter.App -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o "artifacts/publish/$rid"
done
mkdir -p artifacts/releases
cp README.md THIRD-PARTY-NOTICES.txt artifacts/publish/win-x64/
cp -R build/licenses artifacts/publish/win-x64/
(cd artifacts/publish/win-x64 && zip -q -r -FS ../../releases/CafeMenuConverter-win-x64.zip .)
APP="$ROOT/artifacts/releases/CafeMenuConverter.app"
mkdir -p "$APP/Contents/MacOS"
cp -R artifacts/publish/osx-arm64/. "$APP/Contents/MacOS/"
mkdir -p "$APP/Contents/Resources"
cp README.md THIRD-PARTY-NOTICES.txt "$APP/Contents/Resources/"
cp -R build/licenses "$APP/Contents/Resources/"
cp build/Info.plist "$APP/Contents/Info.plist"
chmod +x "$APP/Contents/MacOS/CafeMenuConverter.App"
if [[ "$(uname -s)" == "Darwin" ]]; then
  codesign --force --deep --sign - "$APP"
  codesign --verify --deep --strict "$APP"
  ditto -c -k --sequesterRsrc --keepParent "$APP" artifacts/releases/CafeMenuConverter-osx-arm64.zip
else
  echo "The macOS app must be ad-hoc signed and archived on macOS."
fi
echo "Ready: $ROOT/artifacts/releases"
