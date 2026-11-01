# 0011. Original character "Nubi" instead of Meta's Jolly

- Status: accepted
- Date: 2026-11-01

## Context
Jolly is Meta's Muse mascot. The local GLB/STL is an unlicensed fan print with no rig, mouth or UVs (`character.md`, `licenses.md` §7, §9). The hackathon rules' warranties cover trademarks.

## Decision
- Ship an original, CC0 character, **Nubi**: a chubby hooded blob with a face plate, dot eyes, stubby limbs and a different silhouette and colour scheme. It is built deterministically by `tools/blender/build_nubi.py` (Blender 5.1):
  - about 25k triangles and 2 materials
  - a custom 15-bone rig
  - a separate mouth mesh with shape keys `sil, aa, E, I, O, U`
  - eye blink keys
  - export as GLB without Draco, imported with glTFast 6.14.1, or as FBX if glTFast import fails
- The `Meta+Muse+Jolly*` files stay git-ignored, and nothing derived from them is committed.
- Meta names and logos are never used as character branding. "Uses Meta Model API" is the only nominative use.

## Consequences
- The demo shows our own character. There is no IP risk at Stage 1.
- The project codename "Jolly" survives only in folder, asmdef and namespace names (`Jolly.*`). Renaming it is a mechanical follow-up if organisers object (low risk; recorded here).

## Alternatives
- **Use the Jolly asset:** IP and licence risk, and it would still need full rigging.
- **A Quaternius CC0 humanoid:** not a companion mascot, and weaker for the demo.
