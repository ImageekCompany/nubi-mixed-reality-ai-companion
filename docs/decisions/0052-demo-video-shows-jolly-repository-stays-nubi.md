# 0052. The demo video shows the local Jolly skin; the repository and public builds stay Nubi

- Status: accepted
- Date: 2026-11-03

## Context
- The project began with Meta's Muse mascot, Jolly. We found no licence for the model files, so the repository ships Nubi, an original CC0 character (ADR-0011), and a look-alike skin stays local and git-ignored (ADR-0026).
- The hackathon rules ask entrants to warrant that a submission violates no copyright or trademark (`docs/research/hackathon.md` §1). No licence or written permission for the mascot is on record in this repository.
- The team recorded the submitted demo video with the Jolly skin. Nubi does not appear in it. Our earlier plan recommended a Nubi cut unless the organizers permitted Jolly; the team chose Jolly for recognition in a Meta event.

## Decision
- **The video shows Jolly, and only the video does.** The repository, the default build and every public artefact are Nubi.
- **Say so first.** The README's first screen says who is in the video, why the repository differs and that it is the same code. A reader must not have to discover it.
- **The mesh, textures, materials, renders and baked lines stay out**, in the tree and in history: `docs/research/samples/jolly_*.png`, committed by mistake and removed in `e50732d`, were rewritten out of history before the first push.
- **What stays committed about Jolly is what reproducing the video needs and contains no art:** `tools/blender/build_jolly.py` (reads the body from a file you supply; a few landmark constants were measured on it), `docs/art/jolly-demo.md`, the `--character` build flag, and one self-test voice clip saying the name. These are disclosed in `THIRD_PARTY.md` §7.
- **Video working files left the tree after recording** (plan, shot checklist, voice-over, cards, captions). They remain in git history.

## Consequences
- The risk that the skin breaches the rules' warranty is the team's, and it is accepted knowingly. If the organizers object, `tools/device/build.sh` already builds Nubi with the same code, but the video would have to be re-recorded.
- The ADRs from 0041 on name Jolly because the live sessions behind them ran the Jolly build. The same code and clips drive both characters, and `PointToPlaceTests` checks each.
- Device numbers in the docs say which character they ran with.

## Alternatives
- **Record the video with Nubi.** Removes the risk. Not what the team recorded.
- **Commit the skin.** Would distribute a mascot derivative with no licence. Rejected.
- **Say nothing and let the two names stand.** A judge comparing the video and the repository sees a contradiction, and hiding it is worse than the risk. Rejected.
