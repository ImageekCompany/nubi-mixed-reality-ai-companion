# 0046. The model is turned to face the root's +z by a definition yaw, and the validator measures the face

- Status: accepted
- Date: 2026-11-02

## Context
- The user, wearing the headset: Jolly "always rotated to me with butt", and Nubi did too.
- Every facing rule takes the character root's +z as where the face points:
  - body turns toward the user and the walk heading (`NubiCharacterController`);
  - the look-at bearing (`LookAtDriver`);
  - `CharacterRig.HeadForward`;
  - the `look.user` telemetry and self-test, and `AttentionTests`.
- Both Blender exports (`tools/blender/nubi/export.py`, shared by `build_jolly.py`) face Unity **-z**:
  - In Blender the character faces -Y; `axis_forward="-Z"` writes that to FBX -Z, and Unity keeps z.
  - The left foot and arm come in at +x, which is the character's left only when it faces -z.
  - The export docstring said "+Z", and no test looked at the mesh.
- So the code turned the root precisely toward the user and showed the back.
  - The device logged `body_yaw_off` 1–6° on every turn of the user's session.
  - With the old setup, the vertices skinned to the eyes point 173–179° from the user in every `AttentionTests` case.
- The eye bones are pivots near the head's centre. Their position says nothing about which way the face points, so a check on the bones passes on the broken model.

## Decision
- `CharacterDefinition.modelYaw` (degrees about up) turns the model instance under the root.
  - `CharacterBody.Apply` sets it, beside `scale`. That covers the prefab builder and the runtime swap.
  - Nubi and Jolly use 180. The default is 0, Unity's convention, for models that already face +z.
- `CharacterRig.HeadForward` is the root's +z in head-bone space through the same yaw, so the head-yaw measure stays the face's.
- The validator measures the face itself:
  - It bakes an unanimated instance of the model (bind pose; the import keeps meshes unreadable).
  - It takes the centroid of the vertices mostly skinned to the eye bones, relative to the head bone.
  - It turns that by `modelYaw` and errors when the result points more than 30° off +z.
  - It runs on every definition, committed or local (`tools/ci/test.sh` content validation, `CharacterDefinitionTests`).
- `AttentionTests.AssertFacesTheUser` adds the same measure on the live, animated character: the rendered face, not the root, must point within 25° of the user.

## Consequences
- The bones, clips and FBX files are unchanged, and the art needs no rebuild.
- A character from another pipeline sets one number, and the validator says which.
- `look.user` telemetry from builds before this change is not evidence the character faced the user.

## Alternatives
- **Re-export facing +z** (`axis_forward="Z"` or a 180° turn before export). This fixes the source, but it re-exports the committed Nubi FBX (LFS) and every take, and it moves the bone axes the jaw's `openRotation` and the clips were authored against. It would also need the art checks re-run three days before submission. It is still possible later: set `modelYaw` back to 0, and the validator will confirm.
- **Rotate the prefab's Model child by hand.** The runtime swap instantiates the model fresh and would lose it, and nothing would check it.
