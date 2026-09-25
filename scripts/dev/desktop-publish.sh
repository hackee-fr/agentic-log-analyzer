#!/usr/bin/env bash
# Publishes a self-contained desktop build (no .NET install needed on the target machine).
#   RID=osx-arm64 | osx-x64 | win-x64 | linux-x64   (default: this machine)
# Output: artifacts/desktop/<rid>/ and, on macOS targets, "Agentic Log Analyzer.app".
set -euo pipefail
source "$(dirname "$0")/common.sh"

require dotnet
require pnpm

RID="${RID:-$(detect_rid)}"
OUT="$ROOT/artifacts/desktop/$RID"
APP_NAME="Agentic Log Analyzer"
VERSION="${VERSION:-$(product_version)}"

WEB="$ROOT/apps/dashboard/AgenticLogAnalyzer.Dashboard/web"
cd "$WEB"
[ -d node_modules ] || pnpm install --frozen-lockfile
info "Building the dashboard…"
pnpm build

info "Publishing for ${RID}…"
rm -rf "$OUT"
cd "$ROOT"
dotnet publish apps/desktop/AgenticLogAnalyzer.Desktop -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=false -p:DebugType=none -p:Version="$VERSION" -o "$OUT/bin"

if [[ "$RID" == osx-* ]]; then
  BUNDLE="$OUT/$APP_NAME.app"
  info "Creating $APP_NAME.app…"
  mkdir -p "$BUNDLE/Contents/MacOS" "$BUNDLE/Contents/Resources"
  cp -R "$OUT/bin/." "$BUNDLE/Contents/MacOS/"
  cat > "$BUNDLE/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$APP_NAME</string>
  <key>CFBundleDisplayName</key><string>$APP_NAME</string>
  <key>CFBundleIdentifier</key><string>fr.hackee.agentic-log-analyzer</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleExecutable</key><string>AgenticLogAnalyzer</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
  rm -rf "$OUT/bin"
  info "Done: $BUNDLE"
  warn "The app is not signed: on first launch, right-click it and choose Open (or run: xattr -dr com.apple.quarantine \"$BUNDLE\")."
else
  info "Done: $OUT/bin (run AgenticLogAnalyzer$([[ "$RID" == win-* ]] && echo .exe))"
  if [[ "$RID" == linux-* ]]; then
    warn "Linux needs WebKitGTK at runtime (e.g. sudo apt install libwebkit2gtk-4.1-0)."
  fi
fi
