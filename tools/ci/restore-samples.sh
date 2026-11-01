#!/usr/bin/env bash
# Restores the package samples that may not be committed (ADR-0030): XR Hands "HandVisualizer" is under the Unity
# Package Distribution License, so it is git-ignored and copied from the package cache, .meta files (GUIDs) included,
# to the path the Package Manager uses. The scene's hand rig references it by GUID.
# Run before any editor session; a no-op when the sample is present. On a clone without Library/ (the cache does not
# exist yet), Jolly.Editor.Build.PackageSampleRestore does the same copy when the editor first loads.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="$ROOT/JollyXR"

restore() {
  local package="$1" sample="$2" display="$3"
  local version source dest
  version="$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["dependencies"][sys.argv[2]])' \
    "$PROJECT/Packages/manifest.json" "$package")"
  dest="$PROJECT/Assets/Samples/$display/$version/$sample"
  [[ -d "$dest" ]] && return 0
  source=""
  for dir in "$PROJECT"/Library/PackageCache/"$package"@*; do
    [[ -f "$dir/package.json" ]] || continue
    if python3 -I -c 'import json,sys; sys.exit(json.load(open(sys.argv[1]))["version"] != sys.argv[2])' \
        "$dir/package.json" "$version"; then
      source="$dir/Samples~/$sample"
    fi
  done
  if [[ -z "$source" || ! -d "$source" ]]; then
    echo "[samples] $package@$version not in Library/PackageCache yet; the editor restores $sample on first load"
    return 0
  fi
  mkdir -p "$(dirname "$dest")"
  cp -R "$source" "$dest"
  chmod -R u+w "$dest"
  echo "[samples] restored $sample from $package@$version"
}

restore com.unity.xr.hands HandVisualizer "XR Hands"
