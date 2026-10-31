# Jolly character asset: inspection, readiness, pipeline

Date: 2026-11-01. Tools: Blender 5.1.0 (`/Applications/Blender.app/Contents/MacOS/Blender --version`, build 2026-03-17, hash adfe2921d5f3), Unity 6000.6.6f1.
Scripts used (scratch, not in repo): `inspect.py`, `render.py`, `apicheck.py`, `proto.py` in
`/private/tmp/claude-503/-Users-andrewurbanovich-Git-Projects-MetaCompetitionClaude/f031d2aa-adea-490e-8975-c2c648901da4/scratchpad/`.

**Verdict:** the files are a **static 3D-print sculpt**, not a game asset. They have no rig, no UVs, no textures, no mouth and no shape keys. One flat grey material. 197,590 triangles. The asset is also **legally unusable as-is in an open-source repo** (see §3). The geometry is clean (watertight after welding), so auto-rigging is technically easy. Licensing, not tech, is the blocker.

---

## 1. What is in the files

### 1.1 GLB (`Meta+Muse+Jolly+v2+Whole.glb`, 14,227,632 B)

From the glTF JSON chunk (python dump of the header) and the Blender import (`inspect.py`):

| Property | Value | Source |
|---|---|---|
| Generator | `THREE.GLTFExporter r186` | GLB `asset.generator` |
| Node tree | `millimetres_z_up` (matrix: scale 0.001 + Z-up to Y-up rotation, `extras.convert3dSourceSpace = "millimetre-z-up"`) → `empty_2` (mesh 0) | GLB `nodes` |
| Interpretation | An online STL→GLB converter wrapped a mm/Z-up STL. The GLB is **derived from the STL**, not authored separately. | speculation, based on the `extras` key and the generator |
| Meshes | 1 mesh, 1 primitive, attributes `POSITION`, `NORMAL` only | GLB `meshes` |
| Vertices (raw) | 592,770 (unindexed triangle soup: 3 verts per tri) | accessor 0 count |
| Vertices (welded) | **98,600** (`bmesh.ops.remove_doubles`, dist 1e-4 mm) | `render.py` |
| Triangles | **197,590** | Blender `loop_triangles`; STL header count 0x0303D6 = 197,590 |
| Connectivity after weld | **1 island, 0 non-manifold edges** (watertight, one piece) | `render.py` |
| UVs / vertex colors | **none** | `inspect.py` (`uv []`, `colattr []`) |
| Materials | 1 (`Material_0`): baseColor 0.604 grey, metallic 0, roughness 1 | GLB `materials` |
| Textures / images | **none** | `bpy.data.images` empty, no `images` in the GLB |
| Armature / skin / joints | **none** | no `skins` in the GLB, `bpy.data.armatures` empty |
| Shape keys / morph targets | **none** | `shape_keys None` |
| Animations | **none** | `bpy.data.actions` empty |
| Size | 127.6 × 86.4 × 150.3 mm (W × D × H): a ~15 cm figurine | accessor min/max (mm) |
| Units / up axis | Source: millimetres, Z-up. GLB: metres, Y-up via the parent matrix. | node matrix |
| Pivot | At the origin. Model min Z = 1.0 mm, so the pivot sits about 1 mm below the feet, centred in X/Y. Effectively a **feet-centred pivot**. | accessor min |

### 1.2 STL (`Meta+Muse+Jolly+v2+Whole.stl`, 9,879,584 B)

- Binary STL. The 80-byte header starts with `MW 1.0 3441016 US` (`xxd`). Triangle count is 197,590, the same mesh as the GLB.
- speculation: `MW` = **MakerWorld** (Bambu Lab's model site) and `3441016` is a MakerWorld model or design ID. I could not verify this: `https://makerworld.com/en/models/3441016` returned HTTP 403 to WebFetch.
- macOS metadata: `com.apple.quarantine: 0081;…;Telegram;` (`xattr -l`). Both files were **received via Telegram**, and the upstream URL is not recorded (`kMDItemWhereFroms = (null)`).

### 1.3 What it looks like

Renders (Workbench, ortho): `docs/research/samples/jolly_front.png`, `jolly_34.png`, `jolly_side.png`, `jolly_back.png`, plus `jolly_30k_decimated.png` (after remesh and decimation to 30k triangles, see §2).

Design:
- A chubby, pear/bean-shaped **furry body with a hood**. Fur is sculpted as displacement-like geometry over the whole surface, and that accounts for most of the triangle count.
- A smooth, recessed oval **face plate** in the hood opening with **two small spherical dot eyes**.
- **No mouth, no nose, no ears**. No eyebrows or eyelids, so blinking has to be added.
- **Arms:** two short, stubby, mitten-like arms hanging at the sides. They are fused to the body at the shoulders (one watertight island) and separated from the torso by a narrow gap down most of their length.
- **Legs:** two short, stubby legs/feet. They are about 30 % of the height and separated from each other by a gap.
- Proportions are chibi: head and body are one mass, with no neck.
- Press coverage describes the real Jolly as "fuzzy, rosy-cheeked" with "soft pink cheeks" ([aiweekly](https://aiweekly.co/alerts/metas-muse-ships-an-18-ai-agent-behind-a-kids-toy-mascot), [pasqualepillitteri.it](https://pasqualepillitteri.it/en/news/20242/meta-openai-cute-mascots-ai-agents-jolly-dots)). The print has no colour, so cheeks and other colour zones would have to be painted in.

---

## 2. Readiness assessment

| Question | Answer |
|---|---|
| Rigged? | **No.** No armature, skin or weights. |
| Can it walk? | Not as delivered. Topologically yes: separate legs and a watertight mesh. Auto-weights succeeded in a test (below). |
| Can it talk? | **No mouth geometry at all.** A mouth must be added, either as a mesh, a decal or a texture. |
| Quest budget (≤30–50k tris, ≤2 materials, 2048 textures) | 197k tris is **4–6× over**. 1 material passes. No textures, so it needs a UV unwrap and a bake if we want the fur detail back. |

### 2.1 Prototype I ran (`proto.py`, headless, under 2 s total)

1. Unparent and apply transforms. Weld. Scale to **1.0 m tall** (×6.653). Blender's bone-heat solver printed `Bone Heat Weighting: failed to find solution for one or more bones` on the **native 15 cm scale**, and the warning went away at 1 m. That is a known small-scale failure of bone heat; set the real game scale only after weighting.
2. `object.voxel_remesh` with `remesh_voxel_size = 0.006` gave 169,052 tris in 0.2 s and guarantees clean manifold topology.
3. `DECIMATE` modifier (collapse), ratio 30000/169052, gave **30,000 tris** in 0.5 s. The silhouette, face plate and eyes hold up and the fur reads as noise (`jolly_30k_decimated.png`).
4. I hand-placed a 7-bone armature (hips, spine, head, upperarm.L/R, leg.L/R) and ran `parent_set(type='ARMATURE_AUTO')`. Result: **FINISHED, 0 of 15,002 vertices unweighted**. Every bone got weights (head 8,107 verts, arms ~5.4k each, legs ~7k each). Posing moved vertices up to 0.157 m in the evaluated depsgraph, so deformation works. I did not judge quality: the arms are fused at the shoulder, so expect some arm/torso bleed that needs `vertex_group_smooth` or manual cleanup.

### 2.2 Recommended game-ready pipeline (open-source safe)

Principle: **everything is produced by a committed Blender Python script from a source mesh we are allowed to ship**. Per requirement 9 in `requirements.md`, that source should be our own "Jolly-like" re-sculpt or an original character. The script then yields a reproducible `jolly.glb`.

1. **Clean:** import, unparent, apply transforms, weld, scale to target height (decision needed: ~0.6–1.0 m companion size reads well in MR), feet pivot at the origin, face −Y in Blender (glTF exporter `export_yup` turns this into Unity +Z forward).
2. **Retopo/decimate:** voxel remesh, then Decimate to about **20–30k tris** (verified to work). For cleaner deformation, `object.quadriflow_remesh` exists in 5.1 and gives quads, but I did not run it on this mesh.
3. **Material split (2 materials max):** `M_Fur` (body) and `M_Face` (face plate, eyes, mouth). Select the face plate by region (it is a smooth, recessed oval, so a bounding-sphere or normal/curvature selection works) and assign material index 1. Eyes go in M_Face with vertex colour, or as separate tiny eye meshes, which makes blink easy.
4. **UV + bake (optional, visual polish):** `uv.smart_project`, then `object.bake(type='NORMAL', use_selected_to_active=True)` from the 197k original onto the 30k mesh. The result is a 2048 (or 1024) normal map that restores the fur relief. Cycles is required for baking: `bake` type enum includes `NORMAL` and `AO`.
5. **Armature (own script, not Rigify-generated):** about 15–20 bones. Root → Hips → Spine → Chest → Head. Per side: Shoulder → UpperArm → LowerArm → Hand, and UpperLeg → LowerLeg → Foot (→ Toe). Add Eye.L/R bones or use shape keys. Naming should match Unity **Humanoid** required bones so Mecanim retargeting works. UNVERIFIED: the exact required-bone list in the Unity manual (Avatar setup). Rigify loads in 5.1 (`addon_utils.enable("rigify")`, after which `pose.rigify_generate` and `object.armature_basic_human_metarig_add` exist). Its generated control rig is overkill for export, though, and leaks ORG/MCH/DEF bones into glTF unless filtered with `export_def_bones`. A simple custom armature is fewer moving parts.
6. **Weights:** `parent_set(type='ARMATURE_AUTO')` at 1 m scale, then `vertex_group_smooth`, `vertex_group_limit_total(limit=4)` (Quest skinning budget), `vertex_group_normalize_all`.
7. **Mouth:** none exists, so add a **separate mouth mesh** (about 100–300 tris, dark inner-mouth material in M_Face) placed on the face plate and weighted 100 % to Head. Author **shape keys** with `Object.shape_key_add(name=…, from_mix=False)` and offset the vertices in Python:
   - Full Oculus/OVR set (15): `sil, PP, FF, TH, DD, kk, CH, SS, nn, RR, aa, E, I, O, U`.
   - **Recommended reduced set (6–8)** for a dot-eyed mascot: `sil, PP(closed/pressed), FF, aa, E, I, O, U`, with the rest mapped to the nearest one in code (DD/nn/TH→E, kk/CH/SS/RR→I).
   - A jaw bone is a poor fit because the hood/face plate is rigid and has no jaw.
   - Cheaper alternative: a **texture/sprite-swap mouth** on a quad (viseme atlas). It fits the minimal 2D-ish face, but needs a texture.
8. **Blink and expressions:** shape keys on the eye meshes: `blink` (scale Z → 0.1), `happy` (squash eyes), `look_*` (handled better by eye bones and Unity Multi-Aim). Optional `cheeks` blush via an emission vertex colour or a material property.
9. **Animations:**
   - **(A) Blender Python, keyed:** idle-breathe, wave, point, talk-gesture ×2, happy-bounce, as short loops. Insert keys with `pose_bone.keyframe_insert("rotation_quaternion", frame=…)`. Blender 5.1 actions are layered/slotted (`Action.layers` and `Action.slots` exist). Export with `export_animation_mode` (property exists) set to one clip per action.
   - **(B) Unity procedural, recommended for walk and look-at:** Animation Rigging **6.6.0** is built into this editor (`…/BuiltInPackages/com.unity.animation.rigging/package.json`). Constraints available: `TwoBoneIKConstraint`, `MultiAimConstraint`, `DampedTransform`, `ChainIKConstraint`, `MultiParentConstraint`, `OverrideTransform`, `TwistCorrection` and others (`Runtime/AnimationRig/Constraints/`). Plan:
     - Walk: a procedural waddle with foot targets on a gait cycle + TwoBoneIK legs, plus hip bob/sway. Stubby legs make a canned human walk look wrong anyway.
     - Look-at: MultiAim on Head (and Eyes) toward the user's head or a pointed object.
     - Point: TwoBoneIK on the arm toward the target.
     - Secondary motion: DampedTransform on ears/hood if bones exist.
   - **(C) Retargeted CC0 clips:** see 2.3. Use them only for gesture/emote clips where proportions matter less.
10. **Export:** `export_scene.gltf(export_format='GLB', export_skins=True, export_morph=True, export_morph_normal=False, export_animations=True, export_yup=True, export_texcoords=True, export_normals=True)`. All of these property names are verified to exist in 5.1. Do **not** use Draco (`export_draco_mesh_compression_enable`): Unity's importer support for it varies (UNVERIFIED for glTFast in this project).

### 2.3 Animation sources and licences

| Source | Licence | Usable in OSS repo? | Evidence |
|---|---|---|---|
| **Adobe Mixamo** | Royalty-free use in games/films, but **raw character and animation files may not be distributed**, and use for training ML models is prohibited. | **No**, not as standalone FBX/clips committed to a public repo. Baked into a shipped binary build is OK per the FAQ's wording. | Adobe community FAQ by an Adobe staffer, 2022-09-29: "really the only thing you can't do is distribute the raw character and animation files" ([link](https://community.adobe.com/t5/mixamo-discussions/mixamo-faq-licensing-royalties-ownership-eula-and-tos/td-p/13234775)). The official helpx FAQ (`helpx.adobe.com/creative-cloud/faq/mixamo-faq.html`) returned **403**, so the current official wording is UNVERIFIED. |
| **Quaternius Universal Animation Library** (Standard, free) | **CC0**, "Free to use in personal, educational and commercial projects" | Yes | [quaternius.com pack page](https://quaternius.com/packs/universalanimationlibrary.html), [itch.io](https://quaternius.itch.io/universal-animation-library). 120+ anims total, free Standard tier ≈45. Universal humanoid rig, FBX/glTF. |
| Quaternius Universal Animation Library 2 | CC0 (per listing) | Yes | [OpenGameArt](https://opengameart.org/content/universal-animation-library-2). Page 403'd for me; licence from the search snippet, so UNVERIFIED. |
| **Mesh2Motion** (web auto-rig + animation library) | Code **MIT**, art assets (models, rigs, animations) **CC0** | Yes | `gh api repos/Mesh2Motion/mesh2motion-app`: `LICENSE-CC0.MD`, `LICENSE-MIT.MD`. README "Licenses" section. Assets repo: github.com/Mesh2Motion/mesh2motion-assets |
| uLipSync (audio→viseme, Unity) | **MIT** | Yes | `gh api repos/hecomi/uLipSync` → `MIT`, last push 2026-01-06 |
| Unity Animation Rigging 6.6.0 | Unity Companion License | Yes for Unity-dependent projects; it is a package dependency, not vendored | `…/com.unity.animation.rigging/LICENSE.md` |

Retargeting caveat: Jolly's proportions (legs about 30 % of height, no neck, arms fused to the body) are far from a human rig. Quaternius walk/run clips retargeted through Unity Humanoid will clip and look wrong. **Use procedural walk (B)** and keep retargeted CC0 clips for upper-body emotes only, with a check in the editor first.

---

## 3. Origin and licensing of "Meta Muse Jolly"

What is known:
- **Jolly is Meta's mascot / default avatar for its Muse AI assistant**, unveiled at Meta Connect on 2026-09-23 ([tech-insider](https://tech-insider.org/what-exactly-did-meta-launch-with-jolly/), [axios 2026-09-25](https://axios.com/2026/09/25/ai-doom-meta-muse-mascot), [foro3d](https://foro3d.com/2026/octubre/muse-y-jolly-avatares-de-ia-como-nueva-interfaz-de-producto.html)). Secondary sources say it was designed by Muse product design lead Alex Cornell, per Alexandr Wang on X ([pasqualepillitteri.it](https://pasqualepillitteri.it/en/news/20242/meta-openai-cute-mascots-ai-agents-jolly-dots)). UNVERIFIED at the primary source.
- **No official Meta 3D asset or download, and no brand licence for Jolly, was found** in web searches ("Meta Muse Jolly 3D model", "makerworld Meta Jolly mascot"). The hackathon brief given to me does not mention a Jolly asset kit (UNVERIFIED: hackathon rules page not checked in this task).
- These files: the STL header `MW 1.0 3441016 US` points to a **MakerWorld** download (speculation), most likely a **fan-made printable figurine**. They arrived via **Telegram** (quarantine xattr). Designer, licence and page are **unknown**: MakerWorld returned 403.

Implications (honest assessment):
- Even if the MakerWorld uploader used a permissive licence, the **character design is Meta's IP** (copyright in the character, likely trademarks). A fan's licence cannot grant rights to Meta's character. MakerWorld's default licences are typically non-commercial / no-redistribution (UNVERIFIED for this model).
- **Do not commit `Meta+Muse+Jolly+v2+Whole.glb/.stl` to the public repo.** Right now they are untracked and **not gitignored** (`git check-ignore` → not ignored, `git status` → `??`), and `.gitattributes` routes `*.glb`/`*.stl` to LFS. An accidental `git add .` would publish them. Add both to `.gitignore`.
- Options:
  - (a) Keep the GLB local only, as a "bring your own model" demo asset loaded at runtime or stripped from the repo, and ship the repo with an **original, script-generated placeholder character**.
  - (b) Re-sculpt an **original** "Jolly-inspired" companion (furry hooded blob with a face plate is generic enough if not traced from this mesh) and license it CC0/CC-BY in the repo.
  - (c) Ask Meta / the hackathon organisers whether Jolly may be used in submissions. That is the only way to legitimately show "Jolly" in the demo video.
  - Requirement 9 in `requirements.md` already anticipates (a)/(b). Record the decision in THIRD_PARTY.

---

## 4. Blender Python pipeline: script plan (`tools/blender/build_jolly.py`)

Run: `Blender -b --factory-startup --python tools/blender/build_jolly.py -- --src <mesh> --out JollyXR/Assets/Art/Jolly/jolly.glb --height 0.8 --tris 25000`

Every API below was checked in Blender 5.1.0 (`apicheck.py`/`proto.py`: operator `get_rna_type()` lookups, RNA property enums, or actual execution, marked **ran**).

| Step | API (verified) |
|---|---|
| 0 Reset | `bpy.ops.wm.read_factory_settings(use_empty=True)` **ran** |
| 1 Import | `bpy.ops.import_scene.gltf(filepath=…)` **ran**; `bpy.ops.wm.stl_import` exists |
| 2 Flatten hierarchy | `bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')`, `bpy.ops.object.transform_apply(location, rotation, scale)` **ran** |
| 3 Weld | `bmesh.ops.remove_doubles(bm, verts=…, dist=…)` **ran** (592,770 → 98,600 verts) |
| 4 Scale to 1 m for weighting | `obj.scale = …` + `transform_apply` **ran** |
| 5 Remesh | `mesh.remesh_voxel_size = 0.006; bpy.ops.object.voxel_remesh()` **ran**; alt `bpy.ops.object.quadriflow_remesh`, `RemeshModifier.mode ∈ {BLOCKS, SMOOTH, SHARP, VOXEL}` |
| 6 Decimate | `obj.modifiers.new(type='DECIMATE')`, `.ratio`, `.use_symmetry`, `.symmetry_axis`; `bpy.ops.object.modifier_apply(modifier=…)` **ran** |
| 7 Material zones | `bpy.data.materials.new`, `mesh.materials.append`, `polygon.material_index` (standard RNA). Face-plate selection: custom bmesh region test |
| 8 UV + bake | `bpy.ops.uv.smart_project`; `bpy.ops.object.bake(type='NORMAL', use_selected_to_active=True, …)`. Type enum includes NORMAL/AO. Needs `scene.render.engine='CYCLES'` (not run) |
| 9 Armature | `bpy.data.armatures.new`, `bpy.data.objects.new`, `bpy.ops.object.mode_set(mode='EDIT')`, `arm.edit_bones.new()` with `.head/.tail/.roll/.parent/.use_connect/.use_deform` **ran** |
| 10 Skin | `bpy.ops.object.parent_set(type='ARMATURE_AUTO')` **ran**; then `object.vertex_group_smooth`, `object.vertex_group_limit_total`, `object.vertex_group_normalize_all`, `object.vertex_group_quantize` (exist) |
| 11 Mouth + eyes | `bmesh.ops.create_circle` / `create_uvsphere` (standard bmesh, not separately checked); `Object.shape_key_add(name, from_mix)` (function exists on `bpy.types.Object`), then edit `key_block.data[i].co` |
| 12 Animation | `pose_bone.keyframe_insert(data_path, frame)` (`bpy_struct.keyframe_insert` exists); `Action.layers` / `Action.slots` exist (layered actions); `AnimData.nla_tracks` exists |
| 13 Optional Rigify | `addon_utils.enable("rigify")` **ran**, then `object.armature_basic_human_metarig_add`, `pose.rigify_generate` exist |
| 14 Export | `bpy.ops.export_scene.gltf(export_format='GLB', export_skins, export_morph, export_morph_normal, export_animations, export_animation_mode, export_force_sampling, export_def_bones, export_yup, export_apply, export_texcoords, export_normals, export_image_format)`. All property names exist |
| 15 Preview render | `scene.render.engine='BLENDER_WORKBENCH'`, `bpy.ops.render.render(write_still=True)` **ran** |

Notes for the script author:
- Weight at 1 m scale, then scale the armature object for the final size and apply. Alternatively keep 1 m and scale in Unity.
- `Material.use_nodes` prints a DeprecationWarning in 5.1 ("expected to be removed in Blender 6.0"). New materials have nodes by default, so don't set it.
- Make the script deterministic (fixed bone coordinates as fractions of the bbox) so CI or a teammate can regenerate the asset.

---

## Top risks

1. **IP:** Jolly is Meta's character, and the file is an unattributed fan print, probably from MakerWorld via Telegram. It cannot be published in an OSS repo, and showing it in the demo is a judgement call for the team or the organisers.
2. The GLB/STL are **not gitignored**, so an accidental commit would push them via LFS.
3. **No mouth, no rig, no UVs, no colours.** Lipsync and expressions are entirely new authoring work.
4. **197k tris** is 4–6× the Quest budget. Remesh plus decimate to 30k is verified and fast, but fur detail needs a normal-map bake to look good.
5. Bone-heat auto-weights **fail at the native 15 cm scale**. They work at 1 m. Fused arms will need weight cleanup.
6. Mixamo clips cannot be committed. CC0 Quaternius/Mesh2Motion clips exist but retarget poorly onto chibi proportions, so plan for a procedural walk with Animation Rigging.
