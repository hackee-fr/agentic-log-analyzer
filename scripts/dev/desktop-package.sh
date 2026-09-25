#!/usr/bin/env bash
# Builds the desktop app for RID, then wraps it in the platform's distributable format.
#   macOS  → artifacts/packages/AgenticLogAnalyzer-<version>-<rid>.dmg
#   Windows→ AgenticLogAnalyzer-<version>-<rid>-setup.exe (Inno Setup, when iscc is available) and .zip
#   Linux  → AgenticLogAnalyzer-<version>-<rid>.AppImage (when appimagetool is available) and .tar.gz
# Inputs: RID (default: this machine), VERSION (default: <Version> in Directory.Build.props).
set -euo pipefail
source "$(dirname "$0")/common.sh"

VERSION="${VERSION:-$(product_version)}"
RID="${RID:-$(detect_rid)}"
export VERSION RID
"$(dirname "$0")/desktop-publish.sh"
SRC="$ROOT/artifacts/desktop/$RID"
PKG="$ROOT/artifacts/packages"
BASE="AgenticLogAnalyzer-${VERSION}-${RID}"
APP_NAME="Agentic Log Analyzer"
mkdir -p "$PKG"

case "$RID" in
  osx-*)
    require hdiutil
    info "Creating ${BASE}.dmg…"
    STAGE="$(mktemp -d)"
    cp -R "$SRC/$APP_NAME.app" "$STAGE/"
    ln -s /Applications "$STAGE/Applications"
    rm -f "$PKG/$BASE.dmg"
    hdiutil create -volname "$APP_NAME" -srcfolder "$STAGE" -ov -format UDZO "$PKG/$BASE.dmg" >/dev/null
    rm -rf "$STAGE"
    ;;

  win-*)
    info "Creating ${BASE}.zip…"
    rm -f "$PKG/$BASE.zip"
    if command -v 7z >/dev/null 2>&1; then
      (cd "$SRC/bin" && 7z a -tzip "$PKG/$BASE.zip" . >/dev/null)
    else
      (cd "$SRC/bin" && zip -qr "$PKG/$BASE.zip" .)
    fi
    ISCC="$(command -v iscc || command -v ISCC || true)"
    [ -z "$ISCC" ] && [ -x "/c/Program Files (x86)/Inno Setup 6/ISCC.exe" ] && ISCC="/c/Program Files (x86)/Inno Setup 6/ISCC.exe"
    if [ -n "$ISCC" ]; then
      info "Creating ${BASE}-setup.exe…"
      "$ISCC" -Q "-DAppVersion=$VERSION" "-DSourceDir=$(cygpath -w "$SRC/bin" 2>/dev/null || echo "$SRC/bin")" \
        "-DOutputDir=$(cygpath -w "$PKG" 2>/dev/null || echo "$PKG")" "-DOutputBaseName=$BASE-setup" \
        "$ROOT/installer/windows/AgenticLogAnalyzer.iss"
    else
      warn "Inno Setup (iscc) not found: skipping the Windows installer, the .zip is still available."
    fi
    ;;

  linux-*)
    info "Creating ${BASE}.tar.gz…"
    STAGE="$(mktemp -d)"
    cp -R "$SRC/bin" "$STAGE/$BASE"
    tar -czf "$PKG/$BASE.tar.gz" -C "$STAGE" "$BASE"
    rm -rf "$STAGE"
    if command -v appimagetool >/dev/null 2>&1; then
      info "Creating ${BASE}.AppImage…"
      APPDIR="$(mktemp -d)/AgenticLogAnalyzer.AppDir"
      mkdir -p "$APPDIR/usr/bin"
      cp -R "$SRC/bin/." "$APPDIR/usr/bin/"
      cp "$ROOT/installer/linux/AppRun" "$APPDIR/AppRun" && chmod +x "$APPDIR/AppRun"
      cp "$ROOT/installer/linux/agentic-log-analyzer.desktop" "$APPDIR/"
      if command -v rsvg-convert >/dev/null 2>&1; then
        rsvg-convert -w 256 -h 256 "$ROOT/apps/dashboard/AgenticLogAnalyzer.Dashboard/web/public/favicon.svg" -o "$APPDIR/agentic-log-analyzer.png"
      else
        fail "rsvg-convert is required to build the AppImage icon (apt install librsvg2-bin)."
      fi
      ARCH=x86_64 appimagetool "$APPDIR" "$PKG/$BASE.AppImage"
    else
      warn "appimagetool not found: skipping the AppImage, the .tar.gz is still available."
    fi
    ;;

  *) fail "Unsupported RID '$RID'." ;;
esac

info "Packages in $PKG:"
ls -1 "$PKG" | grep -F "$BASE" | sed 's/^/  /'
