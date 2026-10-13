#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$ROOT/artifacts/nuget}"
export NUGET_HTTP_CACHE_PATH="${NUGET_HTTP_CACHE_PATH:-$ROOT/artifacts/nuget-http}"
targets=("$@")
if [[ ${#targets[@]} -eq 0 ]]; then targets=(win-x64 osx-arm64 osx-x64); fi
for rid in "${targets[@]}"; do
  case "$rid" in win-x64|osx-arm64|osx-x64) ;; *) echo "Unsupported target: $rid" >&2; exit 1 ;; esac
  if [[ "$rid" == osx-* && "$(uname -s)" != Darwin ]]; then
    echo "macOS packaging and signing must run on macOS." >&2; exit 1
  fi
done
if [[ -n "${MACOS_SIGN_IDENTITY:-}" && -z "${MACOS_NOTARY_PROFILE:-}" ]]; then
  echo "Set MACOS_NOTARY_PROFILE for a notarized Developer ID release." >&2; exit 1
fi
dotnet test CafeMenuConverter.sln -c Release
mkdir -p artifacts/releases
for rid in "${targets[@]}"; do
  output="$ROOT/artifacts/publish/$rid"
  rm -rf "$output"
  dotnet publish src/CafeMenuConverter.App -c Release -r "$rid" \
    -p:StandaloneRelease=true -o "$output"
  python3 build/verify-package.py "$rid" "$output"
  if [[ "$rid" == win-x64 ]]; then
    cp "$output/CafeMenuConverter.App.exe" artifacts/releases/CafeMenuConverter-win-x64.exe
    continue
  fi
  staging="$ROOT/artifacts/staging/$rid"
  app="$staging/CafeMenuConverter.app"
  rm -rf "$staging"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  cp -R "$output/." "$app/Contents/MacOS/"
  cp build/AppIcon.icns README.md THIRD-PARTY-NOTICES.txt "$app/Contents/Resources/"
  cp -R build/licenses "$app/Contents/Resources/"
  cp build/Info.plist "$app/Contents/Info.plist"
  chmod +x "$app/Contents/MacOS/CafeMenuConverter.App"
  signing=(--force --sign "${MACOS_SIGN_IDENTITY:--}")
  if [[ -n "${MACOS_SIGN_IDENTITY:-}" ]]; then signing+=(--options runtime --timestamp); fi
  # Sign nested native code first; --deep is for verification, not signing.
  for library in "$app/Contents/MacOS/"*.dylib; do
    codesign "${signing[@]}" "$library"
  done
  codesign "${signing[@]}" --entitlements build/entitlements.plist "$app"
  codesign --verify --deep --strict "$app"
  if [[ -n "${MACOS_SIGN_IDENTITY:-}" ]]; then
    submission="$staging/notarization.zip"
    ditto -c -k --keepParent "$app" "$submission"
    xcrun notarytool submit "$submission" --keychain-profile "$MACOS_NOTARY_PROFILE" --wait
    xcrun stapler staple "$app"
    xcrun stapler validate "$app"
    spctl --assess --type execute --verbose "$app"
  else
    echo "$rid: ad-hoc signed; NOT notarized (no Developer ID configured)."
  fi
  archive="$ROOT/artifacts/releases/CafeMenuConverter-$rid.zip"
  rm -f "$archive"
  ditto -c -k --norsrc --keepParent "$app" "$archive"
  if [[ "$rid" == osx-arm64 ]]; then
    rm -rf artifacts/releases/CafeMenuConverter.app
    ditto "$app" artifacts/releases/CafeMenuConverter.app
  fi
done
python3 build/package-notices.py
echo "Ready: $ROOT/artifacts/releases"
