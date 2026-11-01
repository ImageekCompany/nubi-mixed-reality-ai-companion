# Third-party software, assets and services

Our own code is under the MIT License ([`LICENSE`](LICENSE)). This file lists everything else the project uses, which licence applies, and how it gets into the repository or the APK.

- Versions are the ones resolved in `JollyXR/Packages/manifest.json` and `packages-lock.json`.
- Licence texts were read from each package's `LICENSE.md` and `Third Party Notices.md` in `JollyXR/Library/PackageCache` (2026-11-02).
- The method and citations are in [`docs/research/licenses.md`](docs/research/licenses.md).
- This was written by engineers, not lawyers.
- **OSI** means an OSI-approved licence. Several dependencies are *not* open source in that sense, and they are marked "No".

**How dependencies enter the repo.** Unity packages are **referenced by name and version** in `Packages/manifest.json` and fetched by the Unity Package Manager. Their sources are never committed: `Library/` is git-ignored, and the Unity Package Distribution License only allows distribution as binaries inside a built app. The only third-party files committed to this repository are the ones listed in [§4](#4-third-party-files-committed-to-this-repository).

## 1. Open-source packages

| Package | Version | Licence | OSI | How it enters | Used for |
|---|---|---|---|---|---|
| [UniTask](https://github.com/Cysharp/UniTask) (`com.cysharp.unitask`) | 2.5.11 | MIT, © Cysharp, Inc. (`package.json`: `"license": "MIT"`) | Yes | OpenUPM scoped registry | All async code (ADR-0008) |
| [uLipSync](https://github.com/hecomi/uLipSync) (`com.hecomi.ulipsync`) | 3.1.5, git commit `ece3272762f2148dd8d3e658ec04fcbd79d4d97e` | MIT, © 2021 hecomi | Yes | Git URL pinned to the commit | Mouth shapes from played audio (ADR-0007) |
| [Newtonsoft.Json](https://www.newtonsoft.com/json) via `com.unity.nuget.newtonsoft-json` | 3.2.2 | Newtonsoft.Json: MIT, © 2007 James Newton-King. Unity wrapper package: Unity Companion License | Yes / No | UPM | JSON reading and writing (ADR-0015) |
| [Unity glTFast](https://github.com/Unity-Technologies/com.unity.cloud.gltfast) (`com.unity.cloud.gltfast`) | 6.14.1 | Apache-2.0, © 2023 Unity Technologies and the glTFast authors | Yes | UPM | Installed for model import; no `Jolly.*` assembly references it |
| JetBrains Rider Editor (`com.unity.ide.rider`) | 3.0.38 | MIT, © 2019 Unity Technologies, JetBrains s.r.o. | Yes | UPM | Editor-only IDE integration |
| NUnit 3.5 (inside `com.unity.ext.nunit` 2.1.0) | 2.1.0 | NUnit: MIT. Unity package: Unity Package Distribution License | Yes / No | UPM (transitive) | Tests |

Licences bundled inside Unity packages, from their `Third Party Notices.md`:
- SmartFormat (MIT, in Localization)
- Mono.Cecil (MIT)
- Perfolizer (MIT, in the performance test framework)
- Khronos OpenXR-SDK-Source (Apache-2.0, in OpenXR, meta-openxr and XR Hands)
- Google ArmModel and gesture code (Apache-2.0, in XRI and legacy input helpers)
- NVIDIA FXAA (in URP)

## 2. Unity packages (not OSI)

Licences:
- **UCL** is the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license). It allows use only together with a Unity Engine licence, and it requires keeping the notice.
- **UPDL** is the [Unity Package Distribution License](https://unity.com/legal/licenses/unity-package-distribution-license). It allows distribution only as binaries integrated with your project content.
- Packages marked "UCL source + UPDL" put their source code under UCL and everything else under UPDL.

| Package | Version | Licence |
|---|---|---|
| Unity Editor | 6000.6.6f1 | Unity Terms of Service (never redistributed) |
| `com.unity.xr.openxr` | 1.18.0 | UCL source + UPDL |
| `com.unity.xr.meta-openxr` | 2.6.1 | UCL source + UPDL; bundles Meta binaries, see §3 |
| `com.unity.xr.hands` | 1.9.0 | **UPDL only**; bundles Meta binaries, see §3 |
| `com.unity.xr.arfoundation` | 6.6.2 | UCL |
| `com.unity.xr.interaction.toolkit` | 3.6.1 | UCL |
| `com.unity.xr.management` | 4.7.0 | UCL |
| `com.unity.xr.core-utils` (transitive) | 2.6.0 | UCL |
| `com.unity.xr.compositionlayers` (transitive) | 2.6.0 | UCL source + UPDL |
| `com.unity.xr.legacyinputhelpers` (transitive) | 3.0.1 | UCL |
| `com.unity.ai.navigation` | 2.0.15 | UCL |
| `com.unity.localization` | 1.5.13 | UCL |
| `com.unity.addressables` (transitive) | 2.11.2 | UCL |
| `com.unity.scriptablebuildpipeline` (transitive) | 3.0.3 | UCL |
| `com.unity.inputsystem` | 1.20.0 | UCL |
| `com.unity.render-pipelines.universal` / `.core` / `-config` | 17.6.0 | UCL |
| `com.unity.shadergraph` (transitive) | 17.6.0 | UCL |
| `com.unity.ugui` (includes TextMesh Pro) | 2.6.0 | UCL |
| `com.unity.animation.rigging` | 6.6.0 | UCL. Installed, but no `Jolly.*` assembly references it any more (ADR-0017) |
| `com.unity.timeline` | 6.6.0 | UCL. Installed because uLipSync uses it without declaring the dependency |
| `com.unity.test-framework` | 1.8.0 | UCL |
| `com.unity.test-framework.performance` (transitive) | 6.6.0 | UCL |
| `com.unity.testtools.codecoverage` | 1.3.0 | UCL (`LICENSE.md` in the package). Editor-only, never in the player; it bundles ReportGenerator (Apache 2.0) and OpenCover's model (MIT) per its `Third Party Notices.md`. The 6000.6.6f1 editor ships this exact version (`PackageManager/Editor/com.unity.testtools.codecoverage-1.3.0.tgz`) |
| `com.unity.settings-manager` (transitive, from code coverage) | 2.1.1 | UCL |
| `com.unity.burst`, `com.unity.collections`, `com.unity.mathematics`, `com.unity.editorcoroutines`, `com.unity.profiling.core`, `com.unity.nuget.mono-cecil`, `com.unity.searcher`, `com.unity.graph-authoring` (all transitive) | per `packages-lock.json` | UCL, or Unity Companion Package License for `searcher`; `burst` and `graph-authoring` ship no licence file in the cache |
| `com.unity.modules.*` | 1.0.0 | Built-in engine modules, under the Editor terms |

## 3. Meta components (not OSI)

| Component | How it reaches us | Licence | Notes |
|---|---|---|---|
| **Oculus OpenXR Mobile SDK** | Bundled inside `com.unity.xr.meta-openxr` 2.6.1 and `com.unity.xr.hands` 1.9.0 (their `Third Party Notices.md`) | Oculus SDK License Agreement, now the [Meta Platform Technologies SDK License](https://developers.meta.com/horizon/licenses/oculussdk/) | Every Quest build includes these binaries through UPM. The licence limits use to Meta-approved hardware (§1.1), forbids redistributing the SDK under an open-source licence (§1.2.8), and grants no brand rights (§3.4). We never commit them. |
| **AudioPluginOculusSpatializer** | Listed in the `com.unity.xr.meta-openxr` notices | Oculus SDK License Agreement | Not used by our code. |
| **Meta XR Core SDK / Meta Voice SDK** | **Not used.** We chose OpenXR + meta-openxr (ADR-0002) and Wit over raw HTTP (ADR-0006). | — | No `com.meta.*` package is in the manifest. |

## 4. Third-party files committed to this repository

| Path | Origin | Licence |
|---|---|---|
| `JollyXR/Assets/Jolly/Runtime/Providers/MetaApi/Vision/OneBitMaskDecoder.cs` | A C# port of the `one_bit` mask decoder in `meta_sam_parser/_mask_codec.py` (`meta-sam-parser` 0.1.0), Copyright © Meta Platforms, Inc. and affiliates | **SAM License** (last updated 2025-11-19). The full text is committed beside it as `LICENSE-SAM.txt`, as its §1.b.i requires. **Not OSI**: it adds use restrictions (no military, weapons or ITAR uses), and Meta may change the terms. This one file is under the SAM License, not MIT. |
| `JollyXR/Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/**` | XRI 3.6.1 package sample (XR Origin rig, input actions, demo models) | UCL (as part of `com.unity.xr.interaction.toolkit`) |
| `JollyXR/Assets/Samples/XR Interaction Toolkit/3.6.1/Hands Interaction Demo/**` | XRI 3.6.1 package sample *Hands Interaction Demo*: the `XR Origin Hands (XR Rig)` variant the scene uses, hand visual prefabs, hand materials and shaders, affordance themes, sample scripts. Imported without `HandsDemoScene`, `DemoAssets` and `Prefabs/InteractionAffordance.prefab` (ADR-0030) | UCL (as part of `com.unity.xr.interaction.toolkit`; its `LICENSE.md`) |
| `JollyXR/Assets/TextMesh Pro/**` (shaders, settings, SDF assets) | TMP Essential Resources from `com.unity.ugui` 2.6.0 | UCL |
| `JollyXR/Assets/TextMesh Pro/Fonts/LiberationSans.ttf` | Liberation Sans, © 2012 Red Hat, Inc. (digitized data © 2010 Google) | **SIL Open Font License 1.1**; the licence text is committed beside it |
| `JollyXR/Assets/Jolly/Tests/Fixtures/images/pen_640.jpg` | "04-BICcristal2008-03-26.jpg" by **Trounce**, [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:04-BICcristal2008-03-26.jpg), downscaled to 640×428 | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |
| `JollyXR/Assets/Jolly/Tests/Fixtures/images/mug_640.jpg` | "Coffee Mug (Unsplash).jpg" by **Ryan Holloway**, [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Coffee_Mug_(Unsplash).jpg), downscaled to 640×360 | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| `JollyXR/Assets/Jolly/Tests/Fixtures/{sse,sam,asr}/**` | Responses recorded from live Meta Model API calls on synthetic prompts, or on the two images above | Meta Model API ToS §4.1: the customer owns inputs and outputs. They contain no personal data. Provenance: `Tests/Fixtures/ATTRIBUTION.md` |
| `JollyXR/Assets/Jolly/Content/Audio/Lines/en/*.wav` | 31 canned English lines synthesised by Wit.ai in Nubi's voice ("Rubie", pitch 110, from `Content/Character/Nubi/NubiVoice.asset`, ADR-0031) with `CannedLineBaker` | We generated them through the Wit.ai service. Under the Wit.ai terms (read 2026-11-01, not legal advice) Meta owns output data the platform generates (§3.4), and we hold a non-sublicensable licence to use the platform with our application (§3.2). So the clips ship inside Nubi under that licence, **not** under this repository's MIT licence; anyone redistributing them separately should re-bake their own. They can be regenerated with `Jolly/Bake English Canned Lines` using your own Wit app. |
| `JollyXR/Assets/Jolly/Content/SelfTest/still_mug.bytes` | The `mug_640.jpg` image above, shipped in every APK as the device self-test's camera fallback | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| `JollyXR/Assets/Jolly/Content/SelfTest/*.wav` | Four short user utterances synthesised by Wit.ai (voice "Charlie") as the self-test's voice input; they ship in every APK through `JollyBootstrap.selfTestAssets`. Provenance: `Content/SelfTest/ATTRIBUTION.md` | Same as the canned lines: Wit.ai output, used inside the app under the Wit.ai terms §3.2/§3.4 and not covered by the MIT licence. They replaced macOS `say` clips, whose licence permits personal, non-commercial use only. |

**Restored, never committed: the XR Hands *HandVisualizer* sample.** The hand rig (ADR-0030) needs its `HandVisualizer` script and rigged hand models (`LeftHand.fbx`, `RightHand.fbx`). XR Hands' `LICENSE.md` puts the whole package, `Samples~` included, under the UPDL, so `JollyXR/Assets/Samples/XR Hands/` is in `.gitignore`. `tools/ci/restore-samples.sh` (run by the CI and device build scripts) and the Editor's `PackageSampleRestore` copy it, `.meta` files included, from the resolved package at `Library/PackageCache/com.unity.xr.hands@*/Samples~/HandVisualizer`. It reaches users only inside the built APK.

## 5. Services (terms of use, not software licences)

| Service | Terms | What applies to this project |
|---|---|---|
| **Meta Model API**: Muse Spark 1.3, SAM 3.1, Muse Voice Transcribe 1.0 | [Terms of Service](https://dev.meta.ai/legal/terms-of-service) (updated 2026-10-09), [Acceptable Use Policy](https://dev.meta.ai/legal/acceptable-use-policy) | §3.1: an API key may not be shared, so **no key ships in the APK or the repo**, and each user brings their own. §4.1: you own your inputs and outputs. §5.1: Standard content is not used for training. §6.1–6.2: Contributor content may be used for training, and "you must not submit sensitive, confidential, or personal information", so contributor models carry only synthetic traffic (ADR-0004, enforced by `MetaModelResolver`). §7: no trademark licence. The AUP requires disclosing that Nubi is an AI and prohibits unlicensed professional practice, which is why the Listener persona (id `psychologist`) is not demoed and is not called a psychologist in the app. |
| **SAM 3.1 via the API** | Covered by the Meta Model API ToS | We call SAM over HTTP. **No SAM weights or model code are in this repository.** |
| **Wit.ai** (`/synthesize`) | [wit.ai/terms](https://wit.ai/terms) (read 2026-11-01: Meta owns generated output, §3.4; non-sublicensable use licence, §3.2; voice cloning, §4.2, is not used) | Called over raw HTTP with the user's own client token. No Wit SDK code is included. |

## 6. Our own assets

| Asset | Licence |
|---|---|
| **Nubi**: `JollyXR/Assets/Jolly/Art/Character/**` (FBX, albedo texture, face presets), `docs/art/nubi/*` (preview renders, build report), generated by `tools/blender/build_nubi.py` | **CC0 1.0** (ADR-0011). The generator script is under the repository's MIT licence. It uses no external assets: mesh, UVs, texture, rig, shape keys and animations are all procedural. |
| Everything else not listed in this file | MIT ([`LICENSE`](LICENSE)) |

## 7. Deliberately not included

- **Meta's "Jolly" mascot.** The `Meta+Muse+Jolly+v2+Whole.glb` / `.stl` files that inspired the project have **no licence**, and we could not establish their provenance. Jolly is a Meta-owned character design, and the hackathon rules require entrants to warrant that a submission violates no copyright or trademark. The files are git-ignored. Nothing traced or derived from them is committed, and Meta's name and logo are not used as branding. Nubi replaces Jolly, and its design was steered *away* from Jolly (see `docs/art/nubi/README.md`).
- **SAM, Llama and Muse model weights.** None are used locally, and none are committed.
- **Piper voices, sherpa-onnx, Whisper.** These were evaluated for offline speech (`docs/research/licenses.md` §6) but are **not shipped**. If they are added later, use only voices with clear dataset licences (for example `pl_PL-gosia`, CC0) and avoid the GPL-3.0 espeak-ng / piper1-gpl builds.
- **FluentAssertions ≥ 8** (Xceed non-commercial licence) and NSubstitute. The tests use only NUnit and hand-written fakes.
- **Mixamo or other assets whose raw files may not be redistributed.**
