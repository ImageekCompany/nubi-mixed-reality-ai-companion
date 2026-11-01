#!/usr/bin/env bash
# Checks a recording for the edit: resolution, frame rate, duration and audio, plus evenly spaced frames, each judged
# for passthrough. The app clears to black, so without passthrough most of the frame is black and only the character
# and the UI are lit: an adb screenrecord of the app (no passthrough) measured mean luma 26 but a 10th-percentile luma
# of 16 (video black), while system recordings with passthrough measured 10th percentiles of 24-35. A frame counts as
# passthrough when its mean and its 10th-percentile luma both clear their floors.
# Usage: tools/demo/check-frame.sh <video> [--frames N] [--min-luma L] [--min-p10 P] [--out dir]
#   --frames N    frames to grab, spread over the clip (default 5)
#   --min-luma L  mean luma (0-255) a frame needs (default 25)
#   --min-p10 P   10th-percentile luma a frame needs, i.e. 90 % of it above black (default 20)
#   --out dir     where the PNGs go (default artifacts/demo/frames/<video name>)
# Exits 1 when any frame has no passthrough.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VIDEO="${1:-}"
[[ -n "$VIDEO" && -f "$VIDEO" ]] || { echo "usage: $0 <video> [--frames N] [--min-luma L] [--out dir]" >&2; exit 2; }
shift
FRAMES=5
MIN_LUMA=25
MIN_P10=20
OUT="$ROOT/artifacts/demo/frames/$(basename "${VIDEO%.*}")"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --frames) FRAMES="$2"; shift ;;
    --min-luma) MIN_LUMA="$2"; shift ;;
    --min-p10) MIN_P10="$2"; shift ;;
    --out) OUT="$2"; shift ;;
    *) echo "unknown argument $1" >&2; exit 2 ;;
  esac
  shift
done
command -v ffprobe >/dev/null && command -v ffmpeg >/dev/null || { echo "needs ffmpeg and ffprobe (brew install ffmpeg)" >&2; exit 2; }
mkdir -p "$OUT"

probe() { ffprobe -v error -select_streams "$1" -show_entries "$2" -of default=noprint_wrappers=1:nokey=1 "$VIDEO" | head -n 1; }
width="$(probe v:0 stream=width)"
height="$(probe v:0 stream=height)"
codec="$(probe v:0 stream=codec_name)"
rate="$(probe v:0 stream=avg_frame_rate)"
fps="$(awk -F/ '{ printf "%.2f", ($2 > 0 ? $1 / $2 : 0) }' <<<"$rate")"
duration="$(ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 "$VIDEO")"
audio="$(ffprobe -v error -select_streams a -show_entries stream=codec_name,sample_rate,channels -of csv=p=0 "$VIDEO" | tr '\n' ' ')"
echo "[check] $VIDEO"
echo "[check] video ${width}x${height} ${codec} ${fps} fps, ${duration} s; audio: ${audio:-none}"

sum=0
dark=0
for i in $(seq 1 "$FRAMES"); do
  t="$(awk -v d="$duration" -v i="$i" -v n="$FRAMES" 'BEGIN { printf "%.2f", d * (i - 0.5) / n }')"
  png="$OUT/frame_${i}_${t}s.png"
  ffmpeg -v error -y -ss "$t" -i "$VIDEO" -frames:v 1 "$png"
  stats="$(ffmpeg -v error -i "$png" -vf "signalstats,metadata=print:file=-" -f null - | grep -E "YAVG|YLOW" | tr '\n' ' ')"
  avg="$(grep -oE "YAVG=[0-9.]+" <<<"$stats" | cut -d= -f2)"
  low="$(grep -oE "YLOW=[0-9.]+" <<<"$stats" | cut -d= -f2)"
  verdict="passthrough"
  if awk -v a="$avg" -v m="$MIN_LUMA" -v l="$low" -v p="$MIN_P10" 'BEGIN { exit !(a < m || l < p) }'; then
    verdict="BLACK"
    dark=$((dark + 1))
  fi
  printf "[check] t=%6ss mean_luma=%6.1f p10_luma=%5.1f %s  %s\n" "$t" "$avg" "$low" "$verdict" "$png"
  sum="$(awk -v s="$sum" -v a="$avg" 'BEGIN { print s + a }')"
done
mean="$(awk -v s="$sum" -v n="$FRAMES" 'BEGIN { printf "%.1f", s / n }')"
echo "[check] mean luma over $FRAMES frames: $mean; $dark of $FRAMES frame(s) without passthrough"
if [[ $dark -gt 0 ]]; then
  echo "[check] FAIL: black background in $dark frame(s): no passthrough in the recording" >&2
  exit 1
fi
echo "[check] OK"
