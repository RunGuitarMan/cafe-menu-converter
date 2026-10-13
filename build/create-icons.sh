#!/usr/bin/env bash
# Regenerate every icon size directly from the vector source on macOS.
# Development prerequisites: Node.js, npm ci --prefix build (sharp), and iconutil.
# Normal publishing uses the committed assets and needs none of these tools.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
node "$ROOT/build/render-icons.cjs"
iconutil -c icns "$ROOT/artifacts/icons/AppIcon.iconset" -o "$ROOT/build/AppIcon.icns"
