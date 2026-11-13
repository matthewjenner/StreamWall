#!/usr/bin/env bash
# Generates the synthetic test clip the WebView spike plays (not committed; see .gitignore).
# 1080p30 H.264 High at ~4.5 Mbps with a quiet 220 Hz tone (-30 dB), 60 s, faststart. Needs ffmpeg.
set -euo pipefail
out="$(dirname "$0")/StreamWall.WebViewSpike/TestSite/media"
mkdir -p "$out"
ffmpeg -hide_banner -loglevel error -y \
  -f lavfi -i "testsrc2=size=1920x1080:rate=30" \
  -f lavfi -i "sine=frequency=220:sample_rate=48000" \
  -t 60 -c:v libx264 -profile:v high -pix_fmt yuv420p -b:v 4500k -maxrate 4500k -bufsize 9000k -g 60 \
  -af "volume=-30dB" -c:a aac -b:a 128k -movflags +faststart \
  "$out/test-1080p.mp4"
echo "wrote $out/test-1080p.mp4"
