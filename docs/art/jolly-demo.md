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
| Albedo and fur normal map (1024²) | `Textures/T_Jolly_{Albedo,Normal}.png` |
| Unity assets (step 2) | `Materials/M_Jolly_{Fur,Gloss}.mat`, `JollyCharacter.asset`, `Animator/Jolly.controller`, `Animator/JollyUpperBody.mask`, `Lines/en/*.wav` |
| `.blend`, turntable, viseme sheet, clip sheets | `tools/blender/out/jolly/` |

Before you commit anything near these paths, check them:
`git check-ignore -v JollyXR/Assets/Jolly/Local/Characters/Jolly/Jolly.fbx JollyXR/Assets/Jolly/Local.meta`
and `git status --short` must not list them. On a branch that does not have these `.gitignore` lines yet,
put the same two patterns in `.git/info/exclude`.

## 1. Blender (about 50 s with renders, 35 s without)

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
| Body, hood, face plate | The print, welded and scaled to 0.86 m. The fused arms are deflated onto the torso outline, which leaves a shallow groove for the new arm. The stub legs and coat hem are flattened into the body bottom. Then it is voxel remeshed (7 mm), Laplacian softened (4 passes) and decimated with X symmetry to about 15.6k tris. The print's dot eyes and mouth bump are sanded off the plate. |
| Face plate mask | Raw vertex density: the plate is a few large triangles and the fur is dense. The mask is transferred to the remesh and blurred, and it drives the paint. |
| Arms and mittens, legs and feet | Nubi's procedural shells, resized to the print (arms 0.33 m and nearly vertical, legs 1.35× thicker). They are separate shells, so Wave, Point and Think don't tear the body. |
| Eyes, mouth | Nubi's eye domes (small, beady, glossy) and mouth grid at 80 % size, on the plate. |
| Shape keys | Nubi's: `sil A I U E O Smile Frown Blink_L Blink_R Happy_Eyes Wide_Eyes Sad_Eyes`. |
| Rig, weights, clips | Nubi's 22 bones, placed for Jolly. The two `Sprout` bones are kept for the contract but skin nothing. The armature is still named `Nubi_Rig`, so Animator paths match Nubi's. |
| Look | Cream/ivory plush fur, a paler warm face plate, beady black eyes, soft pink cheeks and a small upturned smile. Fur strands are painted into the albedo from a 3D strand field (seamless) and baked into a tangent-space normal map from the same field. No fur shells. |

The palette comes from press descriptions ("cream-colored", "plush ivory fur", "beady black eyes",
"soft pink cheeks", "upward smile": aiweekly.co, pasqualepillitteri.it, tech-insider.org). The
face-plate tone is not described anywhere, so a paler warm skin is a guess. No reference images were
downloaded or stored.

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

24,936 tris and 12,516 verts, 22 bones, 2 materials. Max 3 influences, 0 unweighted vertices, all
normalised. All 15 clips pass the arm and foot interpenetration check: the worst is 2.7 mm in `Sad`, and
`Think` is 18.7 mm against its 32 mm limit. Locomotion matches the design speed (Walk 0.491 m/s with
0.0 mm IK error, Hop 0.900 m/s with 1.95 mm). The closed blink is 0.136 of the open eye height.

The scan body has sharp creases, and next to them the shared nearest-face test flagged points 10–30 cm
outside the body as inside. Jolly's build confirms each candidate with a ray-parity test. Nubi's check
is unchanged.

## Known gaps

- Fur reads as painted strands plus a normal map that NubiSkin does not sample. No fur shells, because
  of the Quest budget.
- The clips are Nubi's: arm rotations are deltas from Jolly's near-vertical rest pose, so `Wave` lifts
  the arm to about horizontal rather than overhead. The two `Sprout` bones animate nothing.
- The hood rim is softer than on the print, because the remesh and smoothing soften it. A small flat
  edge remains under the body where the legs were cut.
- The face-plate colour is a guess (see above). Check it against official imagery before a demo.
- Every blendshape imports at weight 100 in the editor, for Nubi as well. Runtime `FaceRenderer`
  overwrites them, but edit-mode previews show all keys mixed until you zero them.
