# Jolly demo skin (local only)

A "Jolly-like" skin for demos. It is built on your machine from a mesh you supply and drops in for Nubi:
the same 22 bones, 13 shape keys and 15 clips.

## IP constraint

Jolly is Meta's character (docs/research/character.md §3). The source print
(`Meta+Muse+Jolly+v2+Whole.glb/.stl`) has no known licence. Everything built from it is local-only and
is **never committed, pushed, published, or shipped in a build or video you distribute** without
permission from Meta. That covers the FBX, textures, materials, prefab, controller, report, `.blend` and
renders, plus their `.meta` files. Nubi stays the public default.

Only the generic pipeline is committed: `tools/blender/build_jolly.py`, this page and the `.gitignore`
entries. The script has no mesh, texture or image data in it. It reads the body from `--src` at build
time. Its few landmark constants (cut heights, eye and shoulder positions) were measured on that print.

Where the outputs go (all git-ignored):

| Output | Path |
|---|---|
| FBX, face presets, validation report | `JollyXR/Assets/Jolly/Art/Private/JollyDemo/Jolly.fbx`, `Jolly_FacePresets.json`, `build_report.json` |
| Albedo and fur normal map (1024²) | `JollyXR/Assets/Jolly/Art/Private/JollyDemo/Textures/T_Jolly_{Albedo,Normal}.png` |
| Unity assets (step 2) | `…/JollyDemo/Materials/M_Jolly_{Fur,Gloss}.mat`, `…/Animator/Jolly.overrideController`, `…/Jolly.prefab` |
| `.blend`, turntable, viseme sheet, clip sheets | `tools/blender/out/jolly/` |

Before you commit anything near these paths, check them:
`git check-ignore -v JollyXR/Assets/Jolly/Art/Private/JollyDemo/Jolly.fbx JollyXR/Assets/Jolly/Art/Private.meta`
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

There is no committed importer for Jolly, so the import settings are written once into `Jolly.fbx.meta`.
They are the same as `NubiModelPostprocessor`:

- Generic rig, avatar created from this model, Bake Axis Conversion on, Optimize Game Objects off,
  blend shapes on, no cameras, lights or visibility, not readable, Optimal compression.
- Loop Time on the `loopingClips` from `Jolly_FacePresets.json`: Idle, Walk, Hop, Talk, Listen, Think and Sad.
- The material remaps `M_Jolly_Fur` and `M_Jolly_Gloss` point to materials on `Jolly/Character/NubiSkin`:
  - Fur: rim 0.22, spec 0.03/12.
  - Gloss: rim 0, spec 0.7/96, the same as Nubi's eyes.
  - `T_Jolly_Normal` is imported as a normal map. NubiSkin ignores it; it is there for a Lit or fur shader.

Unity assets:

- `Jolly.overrideController` overrides every clip in `Nubi.controller` with the Jolly clip of the same
  name (14 overrides).
- `Jolly.prefab` mirrors `NubiPrefabBuilder`: a `Model` child with that Animator, a `NubiCharacterController`
  bound to `Jolly_Mesh`, `Jolly_FacePresets.json` and Chest/Neck/Head/Eye_L/Eye_R, a disabled NavMeshAgent
  (r 0.24, h 0.9), a capsule (r 0.32, h 0.88) and Nubi's blob-shadow material at 0.78 × 0.62.

These assets were made by a throwaway editor script, kept outside `Assets` at
`tools/blender/out/jolly/unity-builder/`. To regenerate them, copy that folder to
`JollyXR/Assets/Jolly/Art/Private/_Build/Editor/`, then run:

```sh
Unity -batchmode -quit -projectPath JollyXR -executeMethod Jolly.Editor.JollyDemoBuild.JollyDemoBuilder.Build
Unity -batchmode -quit -projectPath JollyXR -executeMethod Jolly.Editor.JollyDemoBuild.JollyDemoBuilder.Screenshots -shotDir tools/blender/out/jolly
```

Delete `_Build` afterwards, so CI never compiles it.

## 3. Swapping it in

The `feat/character-swap` branch did not exist when this was built, so nothing selects the skin yet.
The demo works today by replacing the Nubi prefab reference in `Main.unity` (`SceneRefs`) with
`Jolly.prefab` locally, without committing that change.

A `CharacterDefinition` asset in `Art/Private/JollyDemo/` needs:

| Field | Jolly value |
|---|---|
| id / display name | `jolly-demo` / "Jolly (local demo)" |
| prefab | `Jolly.prefab` (root `NubiCharacterController`, already bound) |
| animator controller | `Jolly.overrideController`. Any controller whose clips bind `Nubi_Rig/Root/…` works |
| face presets | `Jolly_FacePresets.json` (same table as Nubi's) |
| face mesh, shape-key names | `Jolly_Mesh`; the 13 names above |
| rig bones for look-at | `Chest, Neck, Head, Eye_L, Eye_R` |
| height / agent radius / capsule | 0.86 m / 0.24 m / r 0.32, h 0.88 |
| locomotion clip speeds | Walk 0.49 m/s, Hop 0.90 m/s (same gait as Nubi) |
| eye look-at limits | Nubi's (±16° yaw, ±8° pitch). The pivots sit 0.30 m behind the eyes |
| blob shadow scale | 0.78 × 0.62 |
| local-only flag | true: never in Addressables groups, builds you share, or screenshots in the repo |

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
