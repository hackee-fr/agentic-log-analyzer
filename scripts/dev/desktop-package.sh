#!/usr/bin/env bash
# Builds the desktop app for RID and packages it with Velopack (installer + auto-update feed).
# Output in artifacts/packages/<rid>/:
#   Windows → AgenticLogAnalyzer-win-x64-Setup.exe, portable .zip
#   macOS   → AgenticLogAnalyzer-osx-*-Setup.pkg, portable .zip (contains the .app)
#   Linux   → AgenticLogAnalyzer-linux-x64.AppImage
#   all     → *-full.nupkg (+ *-delta.nupkg) and releases.<rid>.json: the update feed read by installed apps.
# Inputs: RID (default: this machine), VERSION (default: <Version> in Directory.Build.props),
#         PREVIOUS_RELEASES=github to download the previous release and generate delta updates.
set -euo pipefail
source "$(dirname "$0")/common.sh"

VERSION="${VERSION:-$(product_version)}"
RID="${RID:-$(detect_rid)}"
export VERSION RID
"$(dirname "$0")/desktop-publish.sh"

SRC="$ROOT/artifacts/desktop/$RID/bin"
OUT="$ROOT/artifacts/packages/$RID"
PACK_ID="AgenticLogAnalyzer"
APP_NAME="Agentic Log Analyzer"
REPO_URL="https://github.com/hackee-fr/agentic-log-analyzer"
MAIN_EXE="AgenticLogAnalyzer"
[[ "$RID" == win-* ]] && MAIN_EXE="AgenticLogAnalyzer.exe"

cd "$ROOT"
dotnet tool restore >/dev/null
mkdir -p "$OUT"

if [ "${PREVIOUS_RELEASES:-}" = "github" ]; then
  info "Downloading the previous $RID release for delta updates…"
  dotnet vpk download github --repoUrl "$REPO_URL" --channel "$RID" --outputDir "$OUT" \
    || warn "No previous Velopack release found for $RID: only a full package is created."
fi

info "Packaging $PACK_ID $VERSION for $RID with Velopack…"
PACK_ARGS=(
  --packId "$PACK_ID" --packVersion "$VERSION" --packDir "$SRC" --mainExe "$MAIN_EXE"
  --packTitle "$APP_NAME" --packAuthors "hackee-fr" --channel "$RID" --runtime "$RID" --outputDir "$OUT"
)
[ -f "$ROOT/artifacts/release-notes.md" ] && PACK_ARGS+=(--releaseNotes "$ROOT/artifacts/release-notes.md")
[[ "$RID" == osx-* ]] && PACK_ARGS+=(--bundleId "fr.hackee.agentic-log-analyzer")
if [[ "$RID" == linux-* ]]; then
  ICON="$ROOT/artifacts/desktop/icon.png"
  if command -v rsvg-convert >/dev/null 2>&1; then
    rsvg-convert -w 256 -h 256 "$ROOT/apps/dashboard/AgenticLogAnalyzer.Dashboard/web/public/favicon.svg" -o "$ICON"
    PACK_ARGS+=(--icon "$ICON")
  fi
fi

dotnet vpk pack "${PACK_ARGS[@]}"

info "Packages in $OUT:"
ls -1 "$OUT" | sed 's/^/  /'
