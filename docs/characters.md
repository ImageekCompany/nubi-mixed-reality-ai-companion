# Characters: how to add one

The companion's body is data. Everything character-specific sits in one `CharacterDefinition` asset, and the
runtime reads the character only through it (ADR-0026). Nubi is the default and the only committed character;
anything else is a new model, a face-preset file and a definition, with no code changes.

```
CharacterDefinition ──► model (FBX)          instantiated under the Nubi.prefab shell
                    ├─► animatorController   states named after the canonical clips
                    ├─► facePresets (JSON)   per-clip face weights, canonical names
                    ├─► face / jaw           canonical channel → blendshapes or jaw bone
                    ├─► bones                look-at chain + upper-body mask root
                    └─► body, tuning         agent/collider/shadow size, speeds, look and blink timing
```

## 1. The Blender contract

Build the model so it meets what Nubi's build meets (`docs/art/nubi/build_report.json`). The validator in step 5
checks every point marked ✔.

| | Contract |
|---|---|
| Export | FBX, 1 unit = 1 m (UnitScaleFactor 100), Y up, facing +Z in Unity, no leaf bones, no cameras or lights |
| Rig | One armature, **Generic** in Unity. The root bone is never animated (root motion off) |
| Bones ✔ | A look-at chain and an upper-body root. Nubi's names (`Chest`, `Neck`, `Head`, `Eye_L`, `Eye_R`, `Spine`) are the defaults; other names are mapped in the definition. A character without movable eyes adds two unweighted eye bones under the head |
| Skinning ✔ | At most **4** bone influences per vertex, all normalised, no unweighted vertices |
| Triangles ✔ | Warning above **30,000** (architecture §11); Nubi is 25,248 |
| Materials | 1–2, Base Color texture only, so they map to URP Lit or the project's skin shader |
| Clips ✔ | One take per canonical clip (below), in place, 30 fps. Takes may be named differently; then map them in `clips` |
| Takes | Bone curves only. Remove shape-key curves so the Animator never overwrites lip-sync and blinks |
| Shape keys ✔ | One or more per canonical face channel (below), or a jaw bone for the visemes |

**Canonical clips** (`CharacterClips`). Required: `Idle`, `Walk`, `Hop`, `Sad`, `Talk`, `Listen`, `Think`, `Wave`,
`Nod`, `ShakeHead`, `Shrug`, `Point`, `Happy`, `Stretch`. Optional: `Surprised`. Loops: `Idle`, `Walk`, `Hop`, `Talk`,
`Listen`, `Think`, `Sad`. `Walk` and `Hop` are in place; measure the ground speed each one matches at 1× and put it in
`tuning.locomotion.walkClipSpeed` / `hopClipSpeed`.

**Canonical face channels** (`FaceChannels.ShapeNames`): visemes `sil`, `A`, `I`, `U`, `E`, `O`; expressions `Smile`,
`Frown`; eyes `Blink_L`, `Blink_R`, `Happy_Eyes`, `Wide_Eyes`, `Sad_Eyes`. Nubi's shape keys use exactly these names.

**Face presets.** Write `<Model>_FacePresets.json` next to the FBX, in the shape of `Nubi_FacePresets.json`:
`loopingClips` lists the **take** names to loop, and each `presets[].clip` is a **canonical** clip name whose `weights`
use canonical channel names (0..1). The face layers read only this file for a clip's expression.

## 2. Import

Put the FBX and its `_FacePresets.json` in the same folder; add the JSON first (or reimport the FBX after). A model
with a sibling `<Model>_FacePresets.json` is a character model, and `CharacterModelPostprocessor` sets its import in
code: Generic rig with an avatar from the model, Bake Axis Conversion, bones kept as GameObjects (Optimize Game Objects
off, needed by the look-at), blendshapes imported, Read/Write off, and Loop Time on exactly the `loopingClips`. Do not
change these in the Inspector; the postprocessor overwrites them.

Materials: remap the FBX's materials to project materials in the model importer's Materials tab.

## 3. Create the definition

Assets ▸ Create ▸ Jolly ▸ Character Definition. A new asset starts with Nubi's contract defaults and the identity
face mapping; fill in:

| Field | What |
|---|---|
| `id` | Selection key: `a-z`, `0-9`, `-`, `_` (e.g. `jolly`) |
| `displayName` | The name the character calls itself. Persona prompts, greetings, canned lines and stop fillers say it wherever their text has `{name}` |
| `nameKey` | Optional `UI` table key of the name in other locales; empty or missing in a locale uses `displayName` |
| `aliases` | Words live ASR hears instead of the name (e.g. `Jolie`, `Jollie`); rewritten to `displayName` before captions and the model |
| `voice` | Its `VoiceProfile` (Assets ▸ Create ▸ Jolly ▸ Voice Profile, ADR-0031): Wit voice, style, speed, pitch and SSML sfx, with per-language replacements. Required: personas only scale it, and a definition without one is not used |
| `lineClips` | Baked canned lines in this character's voice, per language. Filled by step 4b |
| `model` | The FBX |
| `animatorController` | Filled by step 4 |
| `avatar` | Leave empty to keep the import's avatar |
| `clips` | Only for takes whose name is not the canonical one: `{ name: "Wave", clip: <take> }` |
| `facePresets` | The `_FacePresets.json` |
| `faceMesh` | Name of the SkinnedMeshRenderer with the face shape keys; empty picks the first with any |
| `face` | One entry per canonical channel: blendshape name(s) and weight(s) (0..1), and `jawOpen` (0..1) for visemes on a jaw. A shape several channels drive takes the strongest (e.g. one `Blink` shape for both eyes) |
| `jaw` | `bone` and `openRotation` (local Euler degrees at fully open) for a jaw-bone mouth; leave `bone` empty otherwise. Clips must not key the jaw |
| `bones` | `chest`, `neck`, `head`, `eyeLeft`, `eyeRight`, `upperBody` (this bone and its children play gestures and talk poses over the walk), and `footLeft`/`footRight` (optional: the contact shadow shrinks and fades as the lower foot rises above its bind-pose height, ADR-0024; without them it stays at standing size and the validator warns) |
| `scale` | Uniform scale of the model instance |
| `modelYaw` | Degrees about up that turn the model to face the root's +z, where look-at, body turns and walking point the face. 180 for our Blender exports, which face -z; the validator measures the face (the vertices skinned to the eyes) and errors when it points elsewhere (ADR-0046) |
| `height`, `agentRadius`, `colliderRadius` | NavMeshAgent and capsule collider (also what the poke and gaze triggers hit), metres |
| `contactShadowRadius` | Half-extents of the blob shadow on the floor: x across, y front to back |
| `tuning` | Walk/catch-up speeds, clip ground speeds, Walk→Hop threshold, playback-rate clamp, turn rate; look-at limits (keep eye yaw/pitch where the eyes stay in the head); blink and mouth timing |

Where to put it:

- **Committed character:** `Assets/Jolly/Content/Character/<Name>/`, and add it to `JollyConfig.characters`.
- **Local character** (a demo skin whose source may not be redistributed, such as the Jolly-like mascot):
  `Assets/Jolly/Local/Characters/<Name>/` (git-ignored, with its `.meta` files; renders go to `docs/art/local/`,
  also ignored). Do **not** add it to `JollyConfig`; the validator fails a config that references `Assets/Jolly/Local/`.
  The local Jolly-like skin is expected at `Assets/Jolly/Local/Characters/Jolly/JollyCharacter.asset` with id `jolly`.

## 4. Build its Animator

Select the definition, then **Jolly ▸ Character ▸ Build Animator For Selected Definition**. It writes
`Animator/<Id>.controller` and `Animator/<Id>UpperBody.mask` next to the definition and assigns the controller. States
carry the canonical names, so gestures, conversation poses and face presets work unchanged. Re-run it after changing
clips, `bones.upperBody` or `tuning.locomotion.hopAbove`.

### 4b. Bake its named lines

Canned lines whose text says `{name}` (`greeting_01`, `whoami_01..03`) are baked per character, because the clip speaks
the name. Select the definition, then **Jolly ▸ Character ▸ Bake Named Lines For Selected Definition**, or in batch
(Wit is free; the client token comes from `WIT_AI_CLIENT_TOKEN` or the repo `.env`, and is never logged):

```sh
Unity -batchmode -projectPath JollyXR -executeMethod Jolly.Editor.Tools.CannedLineBaker.BakeCharacterBatch \
      -jollyCharacter Assets/Jolly/Local/Characters/Jolly/JollyCharacter.asset   # [-jollyForce]; exits by itself
```

It bakes in the definition's `voice`, writes `Lines/en/<key>.wav` next to the definition and assigns them to
`lineClips`. When that voice is not the default character's (in which the shared library is baked), it bakes every
canned line, not only the named ones, so no offline line speaks in another character's voice. A named line without a
clip is shown as a caption only; it never plays the shared clip, which says another name. Every other line plays the
shared `JollyConfig.lineClips` unless the character has its own.

Nubi's assets are rebuilt with **Jolly ▸ Character ▸ Rebuild Nubi Assets** (or
`-executeMethod Jolly.Editor.Character.NubiPrefabBuilder.Build`); it keeps every value in `NubiCharacter.asset`.

## 5. Validate it

**Jolly ▸ Character ▸ Validate Characters** checks every definition in the project: required clips present and played
by the controller, its parameters and layers, the preset file, every canonical channel bound to a blendshape the face
mesh has (or the jaw), every bone present, ≤ 4 influences, positive sizes and speeds, and a triangle-budget warning.

`tools/ci/test.sh` runs the same check through the content validator and `CharacterDefinitionTests` (PlayMode). A
local definition is validated whenever it is present, so a broken local skin fails the gate on the machine that has
it; on CI and fresh clones the local Jolly test case is skipped with "is a local, git-ignored character that is not
present here".

## 6. Select it

The first that binds wins; an unknown, unusable or unbindable choice is logged and the next one is tried, ending at
Nubi (a choice that already failed to bind is not tried again):

1. `persistentDataPath/character.txt` with the id.
   - Quest: `echo jolly > character.txt && adb push character.txt /sdcard/Android/data/com.nubi.companion/files/` then
     restart the app. `adb shell rm /sdcard/Android/data/com.nubi.companion/files/character.txt` returns to the default.
   - Editor: select the definition, **Jolly ▸ Character ▸ Play As Selected Definition**; **Play As Default** undoes it.
     Tests use their own data root, so this never affects them.
2. The id baked in by `tools/device/build.sh --character <id>`.
3. `JollyConfig.defaultCharacter` (Nubi).

A local character reaches Play mode automatically, but an APK only when named at build time:
`tools/device/build.sh --character jolly`. A build without `--character` contains no local character, even on a machine
that has one, so a submission APK cannot ship a demo skin by accident. An id that matches nothing fails the build.

Logcat tags (`character`): `evt=character.selected id=…`, `evt=character.unknown source=override|build id=…`,
`evt=character.unusable …`, `evt=character.bind_failed id=… reason=…`.

## Known limits

- The name is fixed at launch, like the character; a locale change re-reads `nameKey`. Tool descriptions and the
  memory summariser never name the character.
- The voice belongs to the character (`voice`); personas scale it and `LocaleVoiceMap` only says whether a locale can
  be spoken. The launch Wit warm-up speaks in the selected character's voice.
- The swap happens once at launch, before the graph is built; there is no live switch.
- The shell (`Nubi.prefab`) keeps Nubi's agent, collider, interactable, voice and shadow objects and only resizes them;
  a character needing a different collider shape needs a code change.
