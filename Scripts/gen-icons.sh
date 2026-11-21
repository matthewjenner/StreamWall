#!/usr/bin/env bash
#
# Regenerate the app icons from one source image (the RingView script, adapted).
#
# The artwork is trimmed to its visible shape and centered in a square exactly as large as its
# longest side: edge to edge, no added margin, so the icon is as large as its neighbors on the
# taskbar. Near-invisible haze (alpha under 2%) is cleared. The .ico carries every size Windows
# asks for (20, 24 and 40 are what it picks at 125% and 150% scaling); 256 is required for the
# installer and Explorer. Requires ImageMagick (`magick`).
#
# Usage:
#   ./Scripts/gen-icons.sh path/to/logo.png [output folder]   (default: Src/StreamWall.App/Assets)
#
set -euo pipefail

SRC="${1:-}"
if [[ -z "$SRC" || ! -f "$SRC" ]]; then
    echo "usage: $0 <source-image> [output-folder]   (PNG with a transparent background)" >&2
    exit 1
fi

if ! command -v magick >/dev/null 2>&1; then
    echo "ImageMagick not found (need the 'magick' command on PATH)." >&2
    echo "Install: winget install ImageMagick.ImageMagick" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="${2:-$SCRIPT_DIR/../Src/StreamWall.App/Assets}"
mkdir -p "$OUT"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Clear the haze, then trim to the visible artwork.
magick "$SRC" -channel A -fx "u<0.02 ? 0 : u" +channel -trim +repage "$WORK/trimmed.png"
# (magick is a native Windows program here: strip its carriage returns.)
W=$(magick "$WORK/trimmed.png" -format "%w" info: | tr -d '\r\n')
H=$(magick "$WORK/trimmed.png" -format "%h" info: | tr -d '\r\n')
SIDE=$(( W > H ? W : H ))

# Centered in a square as large as the longest side (edge to edge), then a 1024 master.
magick "$WORK/trimmed.png" -background none -gravity center -extent "${SIDE}x${SIDE}" \
    -resize 1024x1024 "$OUT/icon.png"

# Multi-size .ico: the exe, windows, taskbar, installer and Start menu.
magick "$OUT/icon.png" -define icon:auto-resize=256,128,64,48,40,32,24,20,16 "$OUT/icon.ico"

echo "Wrote $OUT/icon.png (1024x1024) and $OUT/icon.ico (256..16) from $SRC (artwork ${W}x${H})"
