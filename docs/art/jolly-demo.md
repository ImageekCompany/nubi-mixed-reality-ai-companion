# Jolly demo skin (local only)

A "Jolly-like" skin for demos. It is built on your machine from a mesh you supply and drops in for Nubi:
the same 22 bones, 13 shape keys and 15 clips.

## IP constraint

Jolly is Meta's character (docs/research/character.md §3). The source print
(`Meta+Muse+Jolly+v2+Whole.glb/.stl`) has no known licence. Everything built from it is local-only and
is **never committed, pushed, published, or shipped in a build or video you distribute** without
permission from Meta. That covers the FBX, textures, materials, definition, controller, baked lines, report,
`.blend` and renders, plus their `.meta` files. Nubi stays the public default.

Only the generic pipeline is committed: `tools/blender/build_jolly.py`, this page and the `.gitignore`
entries (`Assets/Jolly/Local/`, and the older `Assets/Jolly/Art/Private/`, kept ignored). The script has no
mesh, texture or image data in it. It reads the body from `--src` at build time. Its few landmark constants (cut
heights, eye and shoulder positions) were measured on that print.

Where the outputs go (all git-ignored, under `JollyXR/Assets/Jolly/Local/Characters/Jolly/`, the folder the
character swap reads local definitions from):

| Output | Path |
|---|---|
| FBX, face presets, validation report | `Jolly.fbx`, `Jolly_FacePresets.json`, `build_report.json` |
| Albedo, smooth normal map and the shell-fur maps (§1b) | `Textures/T_Jolly_{Albedo,Normal,Fur,FurFlow}.png` (1024²), `T_Jolly_FurNoise.png` (256², tiling) |
| Unity assets (step 2) | `Materials/M_Jolly_{Fur,Gloss}.mat`, `JollyCharacter.asset`, `Animator/Jolly.controller`, `Animator/JollyUpperBody.mask`, `Lines/en/*.wav` |
| `.blend` (with the shell-fur preview rig), renders | `tools/blender/out/jolly/Jolly.blend`, `tools/blender/out/jolly/v4/` (front, three-quarter, side, back, wave pose, clip contact sheet, face sheet) |

Before you commit anything near these paths, check them:
`git check-ignore -v JollyXR/Assets/Jolly/Local/Characters/Jolly/Jolly.fbx JollyXR/Assets/Jolly/Local.meta`
and `git status --short` must not list them. On a branch that does not have these `.gitignore` lines yet,
put the same two patterns in `.git/info/exclude`.

## 1. Blender (about 70 s with renders)

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
    --python tools/blender/build_jolly.py -- --src Meta+Muse+Jolly+v2+Whole.glb   # [--no-render] [--clips Walk,Wave]
```

Built with Blender 5.1.0. The build is deterministic: two runs give byte-identical FBX and textures.
A git worktree doesn't contain ignored files, so copy the source mesh into the worktree root first.

How it works: the script reuses the `tools/blender/nubi` package unchanged. Those modules read their
constants from module globals at call time, so `apply_profile()` swaps in Jolly's values first (arm, leg,
eye, mouth and cheek placement, spine split heights, names, palette and painters). Rebuilding Nubi
afterwards gives the same bytes as before: `Nubi.fbx` and `T_Nubi_Albedo.png` match their LFS oids, and
`build_report.json` has no diff.

| Part | Source |
|---|---|
| Body, hood, face plate | The print, welded and scaled to 0.86 m, then narrowed to 0.90 in X and 0.94 in Y (`SCAN_SCALE_X/Y`): the print is squatter than the reference, whose body reads as a tall bean. Every x/y landmark constant is the print measurement times that scale. The fused arms are deflated onto the torso outline, the stub legs are cut off at 0.125 m and the cut is capped, and the torso under the new arms is carved back to a 2 cm gap below the armpit. Then it is voxel remeshed (7 mm) **together with the new arms**, Laplacian softened (10 passes), the arm/shoulder crease is smoothed into a fillet, and it is decimated with X symmetry to about 20.5k tris. The print's dot eyes and mouth bump are sanded off the plate. After the decimation, folded triangles (adjacent normals dot < -0.3; one made a dark notch under the mouth) are relaxed until none are left. After the plate is fitted, the pits left by the print's sculpted grooves (deeper than 0.4 mm; shells converge over a pit into a pale star or a dark dot) are relaxed too, except in a thin band round the plate edge (`PLATE_RIM_RP`, 0.90–1.10 of the plate superellipse, the inset rim) and in the armpit crease. Counts: `decimate_folds_repaired`, `pits_relaxed` in the report. |
| Arms | One surface with the body: a paddle (19 cm front to back at the paw, flattened 0.72 side to side) traced on the print's arm, hanging about 16° out from a pivot inside the shoulder (the print's 19° read as detached sticks; further in, `Listen` cuts into the body). Weights blend body chain → `UpperArm/LowerArm/Hand` by arm membership (distance to the arm shell vs. the torso) and fade into the chest above the pivot, max 4 influences. No thumb. |
| Face plate mask | Raw vertex density finds the plate (few large triangles vs. dense fur); a superellipse fitted to its core (percentiles, side outliers dropped) is the final mask, because the density edge is ragged. |
| Legs and feet | Nubi's procedural shells with Jolly's own leg profile: a stubby column (7.6 cm radius) that keeps its thickness down to a rounded paw barely wider than the leg, like the reference (Nubi's taper to an ankle over a flared foot read as a mushroom). Hips at x = ±0.100 (Nubi ±0.118), so the legs stand inside the narrowed body. Separate shells, so the gait IK is Nubi's. |
| Eyes, mouth | Nubi's eye domes (small, beady, glossy, one small highlight top-left) and mouth grid at 72 % size, on the plate. |
| UVs | Smart project, arms and the rest projected separately (in one projection the arm's outer side and the torso under it overlap), then averaged and packed. |
| Shape keys | Nubi's: `sil A I U E O Smile Frown Blink_L Blink_R Happy_Eyes Wide_Eyes Sad_Eyes`. |
| Rig | Nubi's 22 bones, placed for Jolly. The two `Sprout` bones are kept for the contract but skin nothing. The armature is still named `Nubi_Rig`, so Animator paths match Nubi's. |
| Clips | Same 15 names, lengths, loop flags and gait timing. Jolly's own: `Idle` (breathing stretch, weight sway, late head tilt), `Walk` (waddle roll, squash after each contact, flapping arms), `Hop` (squash on the ground, stretch in the air), `Talk` (Nubi's gestures plus a body bounce on the beats), `Wave` (**left** arm, like the reference: dip, overshoot, three forearm wags with the paw trailing), `Happy` (crouch, jump with arms high, squash on landing). The rest are Nubi's, with inward arm swings clamped (the arms hang close to the torso); `Nod` squashes harder. |
| Look | Blush-pink plush (base sRGB ≈ #F2C1B8, picked from the user's reference photo, warm peach-pink), a warm cream face plate (#E9CFC6) with a darker inset rim, glossy black bead eyes, soft cheek blush, a thin dark smile. The albedo carries no strands; the fur is the shell shader's job (§1b). |

The reference photo (local only, `tools/blender/out/jolly/reference/`, git-ignored) is the colour and
pose reference; the print is the shape reference.

## 1b. Shell fur: what the Unity shader gets

The FBX is the skin. Fur is drawn by a shell shader: the mesh again `N` times, each pass pushed out along the
normal and alpha-clipped by a strand field. All maps use **UV0**. Import the data maps with **sRGB off**;
mipmaps on; `T_Jolly_FurNoise` wrap **Repeat**, the others Clamp.

| Texture | Size | Channels |
|---|---|---|
| `T_Jolly_Albedo` | 1024², sRGB | RGB base colour (also eyes, mouth, cheeks). A = 1 |
| `T_Jolly_Fur` | 1024², linear RGBA | **R** fur length 0..1 (0 on plate, eyes, mouth; 0.12–0.4 feet; ~0.5 paws; 0.65 legs; 1.0 body/hood/arms; shorter round the face, under the body and in the armpits, and wherever a clip stretches the skin: scaled from 1 at 1.35× to 0.3 at 2.5× the rest area, worst case over every clip, `fur.clip_stretch`). **G** density 0..1. **B** tip-lightening amount 0..1 (more on top). **A** ambient occlusion (0.35..1, baked from the rest pose) |
| `T_Jolly_FurFlow` | 1024², linear RGB | **RG** comb direction in UV space, `dir = rg * 2 - 1` (x along +U, y along +V). **B** comb strength 0..1. Combed down and slightly out, radiating from a crown whorl, away from the plate around the face, down the arms, towards the toes |
| `T_Jolly_FurNoise` | 256², linear RGB, tiling | **R** strand cone height (a strand survives shell `h` where `R > h`, so strands taper). **G** soft clump field. **B** per-strand random (colour jitter) |
| `T_Jolly_Normal` | 1024², normal map | Soft clump relief on the fur, flat on the face. Optional |

Shader recipe (the Blender preview, `Jolly.blend`, does exactly this; numbers are in `build_report.json` → `fur`):

- Shells: 16 (12 at distance). Shell `i` has `h = i / N`. Skip shells where `length < 0.03` (bald: plate, eyes, mouth).
- Offset: `pos += normalWS * h * _FurLength * length`, `_FurLength = 0.024` m; plus sag
  `+ gravityWS * h² * _FurLength * length * 0.35` and comb
  `+ normalize(tangentWS * f.x + bitangentWS * f.y) * h² * _FurLength * length * strength * 0.5`,
  where `bitangentWS = cross(normalWS, tangentWS.xyz) * tangent.w` (URP's convention). Tangents: the importer's
  default **Calculate Mikktspace** must stay on.
- Clip: `noise = tex(_FurNoise, uv0 * 18.3)` (`fur.noise_tiling`, gives ~3 mm strands);
  `clip(noise.r - (h + (1 - density) * 0.6))` for `h > 0`.
- Colour: `albedo * lerp(0.70, 1.10, h) * (1 + B * 0.10 * h) * lerp(AO, 1, h) * (0.93 + 0.14 * noise.b)`. The base
  pass (`h = 0`) is opaque: `albedo * lerp(1, 0.70, length) * AO`. A soft rim/Fresnel term on the outer shells
  sells the fluff in passthrough.
- Eyes stay on `M_Jolly_Gloss` (no shells).

## 2. Unity

`Jolly.fbx` has a sibling `Jolly_FacePresets.json`, so `CharacterModelPostprocessor` imports it like Nubi:

- Generic rig, avatar created from this model, Bake Axis Conversion on, Optimize Game Objects off,
  blend shapes on, no cameras, lights or visibility, not readable, Optimal compression.
- Loop Time on the `loopingClips` from `Jolly_FacePresets.json`: Idle, Walk, Hop, Talk, Listen, Think and Sad.
- The material remaps `M_Jolly_Fur` and `M_Jolly_Gloss` point to materials on `Jolly/Character/NubiSkin`:
  - Fur: rim 0.22, spec 0.03/12.
  - Gloss: rim 0, spec 0.7/96, the same as Nubi's eyes.
  - `T_Jolly_Normal` is imported as a normal map. NubiSkin ignores it; it is there for a Lit or fur shader.

Unity assets:

- The materials were made once by hand on `Jolly/Character/NubiSkin`; the FBX's material remaps point at them.
- `JollyCharacter.asset` is a `CharacterDefinition` (docs/characters.md) copied from Nubi's (same rig, face
  channels, look-at bones, feet and tuning) with Jolly's identity, model and body:

| Field | Jolly value |
|---|---|
| id / display name / aliases | `jolly` / `Jolly` / `Jolie`, `Jollie` |
| model, face presets, face mesh | `Jolly.fbx`, `Jolly_FacePresets.json`, `Jolly_Mesh` (the 13 shape keys above) |
| animator controller | `Animator/Jolly.controller`, built by `CharacterAnimatorBuilder` from the definition |
| rig bones | Nubi's: `Chest, Neck, Head, Eye_L, Eye_R`, upper body `Spine`, feet `Foot_L`/`Foot_R` (ADR-0024) |
| height / agent radius / collider radius | 0.88 m / 0.24 m / 0.32 m |
| contact shadow radius | (0.39, 0.31) |
| locomotion, look and face tuning | Nubi's (Walk 0.49 m/s, Hop 0.90 m/s, ±16° / ±8° eyes; the pivots sit 0.30 m behind the eyes) |
| voice | `JollyVoice.asset`: Wit `Railey`, pitch 112, speed 105, default style, no sfx (brighter and higher than Nubi's Rubie, ~310–325 Hz; the only one of six auditioned voices that Wit transcribed without an error, including the name) |
| line clips | All 32 English canned lines in `Lines/en/`, the named ones saying "Jolly", baked in that voice (its voice differs from the shared library's) |

To regenerate the definition and its controller, copy the throwaway builder kept outside `Assets` at
`tools/blender/out/jolly/unity-builder/` to `JollyXR/Assets/Jolly/Local/_Setup/Editor/`, then run:

```sh
Unity -batchmode -quit -projectPath JollyXR -executeMethod Jolly.Editor.JollyCharacterSetup.Setup.Build
```

Delete `Local/_Setup` afterwards, so its assembly never reaches a test run. It keeps baked line clips. To (re)bake
them, run `CannedLineBaker.BakeCharacterBatch` as docs/characters.md §4b shows (the first request after a cold start
can time out; run it again and it bakes only what is missing).

## 3. Using it

The definition is found because it lives under `Assets/Jolly/Local/` (ADR-0026). Select it as docs/characters.md §6
describes: Play mode with **Jolly ▸ Character ▸ Play As Selected Definition**, or an APK with
`tools/device/build.sh --character jolly`, whose build log lists
`[CharacterSceneProcessor] scene=Main character=jolly local=1 paths=[Assets/Jolly/Local/Characters/Jolly/JollyCharacter.asset]`. A build without
`--character` contains nothing from `Local/`. Never add it to `JollyConfig.characters`, Addressables groups, builds
you share, or screenshots in the repo.

The character says its own name: persona prompts, greetings and canned lines fill `{name}` with "Jolly", and live ASR
mishearings `Jolie`/`Jollie` become "Jolly".

## Validation (latest local build)

26,012 tris and 13,046 verts, 22 bones, 2 materials. Max 4 influences, 0 unweighted vertices, all
normalised. All 15 clips pass the arm and foot interpenetration check: `Think` 28.0 mm against its 32 mm
limit, `Listen` 2.6 mm, `Point` 0.8 mm, everything else 0 (limit 4 mm). Locomotion matches the design speed (Walk 0.491 m/s
with 0.0 mm IK error, Hop 0.900 m/s with 1.95 mm). The closed blink is 0.136 of the open eye height.

The merged body has holes where the arms join, so arms are tested against a hidden, closed, arm-less copy of
the torso skinned to the body chain (`TorsoProxy`, not exported), with nearest-face candidates confirmed by
ray parity. Nubi's check is unchanged.

## Known gaps

- Until the Unity shell shader exists, `NubiSkin` shows the bare skin (smooth pink, no fur).
- The silhouette is still a little pot-bellied next to the reference: the belly bulges and tucks under to a
  flat bottom, and the hood meets the shoulders in a soft roll. Fixing that needs resculpting the print.
- A few faint dots remain: two on the back of the hood, a row along the back of the body's bottom rim (the
  capped leg cut), and pale specks where the shoulders meet the arms.
- Raising an arm stretches the fused armpit; the fur there is short, so `Wave` shows a small darker patch
  at the armpit base instead of streaks.
- The preview renders use the Standard view transform (AgX greyed the pink to beige); judge colour in Unity
  with Neutral or no tonemapping.
- Legs and feet are separate shells, so a seam shows from a low angle; the body bottom is flat where the
  legs were cut.
- Nubi's remaining clips (`Point`, `Think`, `Shrug`, …) were authored for an A-pose; they read fine but
  were not re-timed for Jolly.
- Every blendshape imports at weight 100 in the editor, for Nubi as well. Runtime `FaceRenderer`
  overwrites them, but edit-mode previews show all keys mixed until you zero them.
