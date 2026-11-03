# 0051. Jolly remembers a place the user shows, leads the way to it and points

- Status: accepted
- Date: 2026-11-03

## Context
- The user wants this beat in the demo: name the kitchen, show a mess on the sofa, ask "where can I put these things?", and have Jolly walk to the kitchen and point: "There!"
- `remember_place` saved only the user's own floor position, so a kitchen named from the living room was remembered as the living room.
- `capture_and_identify` always ended the turn after its answer, and nothing walked somewhere and then pointed. `GestureId.Point` existed, but no tool played it.
- Live checks against `muse-spark-1.3` on 2026-11-03 (effort minimal):
  - "That over there is the kitchen." got words and no `remember_place` in both runs that reached it, one before a stronger tool description and one after. The user hears a reply and nothing is saved.
  - "Where is the kitchen?" called `show_place` in 4 of 5 complete runs.
  - "Where can I put these things?" and "Where do these go?" called `capture_and_identify` in 4 of 6 with a note appended to the turn telling the model to.

## Decision
- **`remember_place` takes `where`:** `there` (the pointing ray, else the head's forward ray, up to 10 m, else 3 m along it) or `here` (the old behaviour). With `there`, the character looks at the spot, which is the acknowledgement the user sees. The position is where the ray landed, often up a wall, so `walk_to` samples the floor below it.
- **`PointToPlace`:**
  - Walks along the straight line and stops 1 m short. If that spot is unreachable it tries 7/8, 6/8 … 1/8 of the way, because another room is usually past the scanned floor. With nothing reachable it points from where it stands.
  - Turns so the arm rather than the belly is on the target, then starts `Point`. The clip keeps playing after the tool returns, so the line lands while the arm is out.
  - Measured on the played clip (`PointToPlaceTests`), the arm is 21° right of the body and starts 0.23 m right of its axis. `POINT_DIR_R` alone implies 12.5°, which missed by about 20° at the stand-off. With the correction, Jolly's arm is 3.2° off at 1.1 m and 3.9° off at 3 m.
  - A stop or a newer move during the walk skips the point. Barge-in cancels the tool but not the walk, as for every walk.
- **`show_place`** (Exclusive): resolves a name like `walk_to` (taught place, furniture, seen object), runs `PointToPlace`, says "There!" and ends the turn. It is not withheld when seated: the user stays put and the character does the walking.
- **`capture_and_identify` can lead:**
  - When places are remembered, the vision request lists them and the header gains a third field, `LABEL|NOUN_PHRASE|PLACE`. With no places the prompt is unchanged, so the hero beat's verified prompt stays as it was.
  - A named place is checked against memory, then the tool runs `PointToPlace` and adds "There!". An invented name is ignored.
  - The tool's timeout now covers the walk (12 s + 20 s). The vision part keeps its own 12 s and reports `timeout` itself.
  - Live on `mug_640.jpg`: "Where can I put these things?" and "Where does this go?" gave `kitchen` in 3 of 3 with other places listed, and "What is this?" gave `none`.
- **`PlaceGuard`, no model round:**
  - Naming a room from a closed list with a showing phrase ("this is", "that's", "over there") runs `remember_place` with `there` and says "Got it, the kitchen!".
  - Asking after a remembered place ("where is", "show me", "take me to") runs `show_place`.
  - "Where do these go?" with a place remembered runs `capture_and_identify` directly. Which place fits stays with the vision model. A failure with no line of its own (too dark, no camera) goes to the model to explain, and with no place remembered the turn is an ordinary model turn.
  - Other phrasings stay with the model, whose tool descriptions cover them.

## Consequences
- The demo line is deterministic up to the vision model's choice of place, and costs one vision call and one SAM call.
- A place reaches past the scanned floor only by pointing. Jolly walks to the furthest reachable spot on the line.
- The turn holds Acting during the walk, up to 20 s. A longer walk fails with `timeout` and the walk still finishes.
- A place is anchored when the platform persists anchors, as before. `tools/demo/reset.sh` clears memory, so each take teaches the kitchen again.
- If the answer is still being spoken on arrival, the arm goes out before "There!", which queues after the answer.
- Rooms in the guard's closed list are English and Polish singular nominatives only. "W kuchni" and any room not listed go to the model.
- Unverified with a wearer when written: the walk on a real floor, the gaze ray landing on a real doorway, and how arm and line feel together. Tests use fakes and the real character on a baked test floor.

## Alternatives
- **Let `capture_and_identify` return and have the model call `show_place` next.** One more round of 1.5–2 s, and the model decides again whether to move.
- **A separate "where does it go" tool for the model.** The model chose among tools unreliably here; a guard is what makes the line hold.
- **`tool_choice`.** The chat request has no such field in this codebase.
- **A note appended to the user's turn telling the model to look.** Tried: 4 of 6.
- **`here` as the only meaning.** The kitchen would be wherever the user stood when naming it.
