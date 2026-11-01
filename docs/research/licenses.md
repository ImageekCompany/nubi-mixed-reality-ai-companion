# License audit for THIRD_PARTY.md

Audit date: 2026-11-01. I wrote this as an engineer, not a lawyer. Every row cites the URL, file or command it came from, and anything I could not read from a primary source is marked **UNVERIFIED**.

Raw license files were downloaded to a scratch dir with `curl` from `raw.githubusercontent.com/<repo>/<branch>/LICENSE`. Unity packages were pulled as tarballs from `https://download.packages.unity.com/<pkg>/-/<pkg>-<ver>.tgz`, and Meta packages from `https://npm.developer.oculus.com/<pkg>/-/<pkg>-<ver>.tgz`. I read `package/LICENSE.md` and `package/Third Party Notices.md` from each tarball.

Column meanings:
- **Redistribute in a public OSS repo** asks whether the dependency's own files may be committed. Referencing a package by name in `Packages/manifest.json` is always fine.
- **OSI** means OSI-approved (https://opensource.org/licenses).

---

## 1. Unity

| Item | License (as stated in package) | SPDX | License text | Commit sources to repo? | Attribution | OSI |
|---|---|---|---|---|---|---|
| Unity Editor 6000.6.6f1 | Unity Terms of Service / Editor Software Terms | none | https://unity.com/legal | N/A (never redistributed) | none | No |
| `com.unity.xr.openxr` 1.19.0 | Source: **Unity Companion License (UCL)**; otherwise: **Unity Package Distribution License (UPDL)** (LICENSE.md in tarball) | none | https://unity.com/legal/licenses/unity-companion-license · https://unity.com/legal/licenses/unity-package-distribution-license | **No.** Reference via UPM | UCL §5: keep notice in copies | No |
| `com.unity.xr.meta-openxr` 2.7.0 | UCL (source) + UPDL (other). Third Party Notices list **Oculus OpenXR Mobile SDK** and **AudioPluginOculusSpatializer** under *Oculus SDK License Agreement*, plus OpenXR-SDK-Source under Apache-2.0 | none | as above | **No.** Reference via UPM | as above | No |
| `com.unity.xr.arfoundation` 6.7.1 | UCL "for Unity-dependent projects" | none | UCL URL | Technically allowed under UCL, but don't. UPM | UCL §5 | No |
| `com.unity.xr.hands` 1.10.0 | **UPDL only** (LICENSE.md). Bundles Oculus OpenXR Mobile SDK (Oculus SDK License) | none | UPDL URL | **No.** UPDL permits distribution only "as binaries integrated with your Project Content" | none | No |
| `com.unity.xr.interaction.toolkit` 3.7.0 | UCL | none | UCL URL | UPM | UCL §5 | No |
| `com.unity.localization` 1.5.13 | UCL | none | UCL URL | UPM | UCL §5 | No |
| `com.unity.xr.management` 4.6.1, `com.unity.xr.core-utils` 2.6.0, `com.unity.test-framework` | UCL | none | UCL URL | UPM | UCL §5 | No |
| `com.unity.ext.nunit` 2.1.0 (built-in) | UPDL. Bundled NUnit 3.5 is MIT (Third Party Notices.md) | none / MIT | UPDL URL | UPM | none | No / Yes |
| **Inference Engine** `com.unity.ai.inference` 2.6.1 (LICENSE header still says "Sentis") | **Unity Terms of Service**, not UCL ("This software is subject to, and made available under, the Unity Terms of Service") | none | https://unity.com/legal | **No.** UPM | Third-party: Google.Protobuf BSD-3-Clause | No |
| Built-in `com.unity.sentis` 2.2.0 (in Editor `BuiltInPackages`) | no LICENSE.md file in the built-in folder: **UNVERIFIED** | — | — | UPM | — | — |

Key license terms:
- **UCL v1.4 (2024-10-29).** The work may be used only "in connection with the authoring and/or distribution of applications, software, or other content" under a Unity Engine License. §5 requires the license and copyright notice in "all substantial portions".
- **UPDL v2.1 (2023-07-21).** Distribution is "limited to distribution of the Work as binaries integrated with your Project Content". It gives no right to redistribute the package on its own.
- **Conclusion.** Committing `Packages/manifest.json` and `packages-lock.json` is fine. Never commit `Library/PackageCache`, and never embed or copy any `com.unity.*` package into `Packages/` or `Assets/`. `.gitignore` already ignores `JollyXR/Library/`.

## 2. Meta XR SDKs, Voice SDK, Wit.ai

| Item | License | SPDX | Text | Commit to repo? | Attribution | OSI |
|---|---|---|---|---|---|---|
| Meta XR Core SDK `com.meta.xr.sdk.core` 207.0.1 | "Licensed under the **Oculus SDK License Agreement**" (LICENSE.md). NOTICE.txt: OpenVR (BSD-3), SimpleJSON | none | https://developers.meta.com/horizon/licenses/oculussdk/ | **No** | see §1.3 below | No |
| Meta Voice SDK `com.meta.xr.sdk.voice` 85.0.1 | **Meta Platform Technologies SDK License Agreement** (LICENSE.md) | none | same URL | **No** | §1.3 | No |
| `Lib/Wit.ai` inside Voice SDK (also https://github.com/wit-ai/wit-unity) | Custom permissive grant to "use, copy, modify, and distribute … **for use in connection with the web services and APIs provided by Wit.ai**". Subject to Wit.ai ToS | none (GitHub reports `NOASSERTION`) | `package/Lib/Wit.ai/LICENSE`; https://wit.ai/terms | Allowed only with the notice and only for Wit.ai use | Keep copyright notice | No (field-of-use restriction) |
| Wit.ai Terms of Service | https://wit.ai/terms, read 2026-11-01: Meta owns the platform and "other output data generated by the Platform" (§3.4); developers get a limited, non-transferable, non-sublicensable, revocable licence to use the platform with their application (§3.2) and may not "sell, rent, lend, transfer, or sublicense the Platform or Platform Content"; Cloned Voice may not be used outside the application (§4.2.1, not used here). Baked TTS clips may ship inside Nubi but are not MIT-licensable | — | — | — | — | — |

The Meta Platform Technologies SDK License Agreement (formerly the Oculus SDK License; effective 2022-10-25, fetched from the URL above) says:
- **§1.1.** The license is "limited, royalty-free, non-exclusive, non-transferrable" and lets you "use and reproduce the SDK solely to develop, test, and/or distribute your Application". The Application must be built for **"MPT approved hardware and software products"** unless the docs expressly allow otherwise. That matters for the Pico and Android XR plans.
- **§1.2.1.** No modifications or derivative works, except sample code.
- **§1.2.8.** You may not redistribute the SDK "in any manner that would cause the SDK to become subject to an open source license". So **vendoring the SDK into an MIT or Apache repo is prohibited.**
- **§1.3.** Redistributed sample code must carry "Copyright © Meta Platform Technologies, LLC and its affiliates. All rights reserved."
- **§3.4.** No use of Meta brand features and no implied endorsement without written permission.

**Portability note.** `com.unity.xr.meta-openxr` and `com.unity.xr.hands` *already* contain Oculus-SDK-licensed binaries, so a Quest build always pulls some non-OSI code through UPM. That is acceptable as long as it is not committed.

## 3. Meta Model API and Muse outputs

Source: **Meta Model API Terms of Service**, last updated 2026-10-09, https://dev.meta.ai/legal/terms-of-service (the old `llama.developer.meta.com/legal/terms-of-service` redirects here). The Acceptable Use Policy is at https://dev.meta.ai/legal/acceptable-use-policy.

Ownership and use of outputs:
- **§4.1.** You "own all rights, title, and interests in and to your Inputs and Outputs". Muse outputs (text, images, audio) can therefore go into the repo and the demo.
- **§10.1(ix).** You may not use Outputs to train or distill a model "that competes with Meta's models or services". Evaluation and benchmarking are allowed.
- **§7.** "No trademark licenses are granted."

API keys:
- **§3.1.** You may not share your API key with any third party. That means **no shipped key in the APK or the repo**: each user supplies their own key or uses a proxy.

Contributor vs Standard models:
- **§5.1 (Standard).** Content is not used for training.
- **§6.1 (Contributor).** Meta may use content to "train, develop, evaluate, and improve" its models.
- **§6.2 (Contributor).** You "**must not submit sensitive, confidential, or personal information** to the Contributor Services."
- **Pricing page.** https://dev.meta.ai/docs/pricing-rate-limits confirms that Contributor mode trades prompts and responses for a discount.

AUP points that apply to Jolly:
- No "unauthorized or unlicensed practice of any profession". This matters for the **Psychologist persona**.
- No relying on Outputs for health decisions without qualified review.
- You must not "conceal an agent's … automated nature", must not represent Outputs as human-generated, and must disclose known limitations.

Jolly implication: **use `muse-spark-1.3-contributor` only for synthetic or dev traffic.**
- Passthrough camera frames of a user's home and therapy-style conversations are personal data. They must go to Standard models (or to local models).
- The Psychologist persona needs an "AI, not a licensed professional" disclaimer and crisis-resource fallback text.

## 4. SAM 3 / SAM 3.1

| Item | License | SPDX | Text | Redistribute | Attribution | OSI |
|---|---|---|---|---|---|---|
| SAM 3 code + SAM 3.1 checkpoints | **SAM License** (Last Updated 2025-11-19) | none | https://github.com/facebookresearch/sam3/blob/main/LICENSE (README: "licensed under the SAM License"). Weights: https://huggingface.co/facebook/sam3.1 (gated, `license: other`) | Yes, but only under this Agreement, and a copy must be included (§1.b.i) | Acknowledge in publications (§1.b.ii); include the license copy | No |
| SAM 3.1 via Meta Model API (`sam-3.1`) | Meta Model API ToS (§3 above). https://dev.meta.ai/docs/sam/overview states no separate license | — | — | N/A: we only call the API | none | — |

Other SAM License terms:
- Royalty-free and permits derivatives.
- Prohibits ITAR, military and weapons uses (§1.b.v).
- Includes a reverse-engineering clause (§1.b.iv).
- Meta may change the terms unilaterally (§8).

**Jolly calls SAM via the API, so nothing is redistributed. Do not commit SAM weights.**

## 5. Open-weight Muse models and Llama

| Item | License | SPDX | Text | Redistribute | Attribution | OSI |
|---|---|---|---|---|---|---|
| **Muse Glimmer** 30B | **Apache License 2.0** ("Muse Glimmer weights are released under the Apache License 2.0", https://dev.meta.ai/docs/muse-glimmer) | Apache-2.0 | HF `meta-models/Muse-Glimmer-30B` LICENSE (verified: HF API `license: apache-2.0`, `gated: false`) | Yes | Keep LICENSE and NOTICE | Yes (the license; OSI does not certify weights) |
| **Muse Spark Open** 816B MoE | Custom `LICENSE.md`, accepted on https://dev.meta.ai/platform/models/download/muse-spark-open. **Name and terms UNVERIFIED** (click-through, not fetched) | ? | ? | ? | ? | assume No |
| Llama 4 (only if used offline) | **Llama 4 Community License** (effective 2025-04-05) | none | https://github.com/meta-llama/llama-models/blob/main/models/llama4/LICENSE | Yes, but you must ship a copy of the license, show **"Built with Llama"** prominently, and add a NOTICE line; 700M MAU cap; AUP https://www.llama.com/llama4/use-policy | yes | No |

Recommendation: Llama is **not needed**. If an offline LLM is added later, Glimmer (Apache-2.0) is the cleanest Meta option. Never commit the weights, because they are large; download them at runtime.

## 6. Runtime OSS (code)

| Item | License | SPDX | Text | Redistribute | Attribution | OSI |
|---|---|---|---|---|---|---|
| UniTask (Cysharp) | MIT | MIT | https://github.com/Cysharp/UniTask/blob/master/LICENSE | Yes | Copyright + license | Yes |
| R3 (Cysharp) | MIT | MIT | https://github.com/Cysharp/R3/blob/main/LICENSE | Yes | same | Yes |
| VContainer (hadashiA) | MIT | MIT | https://github.com/hadashiA/VContainer/blob/master/LICENSE | Yes | same | Yes |
| uLipSync (hecomi) | MIT | MIT | https://github.com/hecomi/uLipSync/blob/main/LICENSE.md | Yes | same | Yes |
| sherpa-onnx (k2-fsa) | Apache-2.0 | Apache-2.0 | https://github.com/k2-fsa/sherpa-onnx/blob/master/LICENSE | Yes | LICENSE + NOTICE, state changes | Yes |
| Silero VAD (code + bundled model) | MIT | MIT | https://github.com/snakers4/silero-vad/blob/master/LICENSE | Yes | same | Yes |
| OpenAI Whisper (code + weights) | MIT | MIT | https://github.com/openai/whisper/blob/main/LICENSE | Yes | same | Yes |
| whisper.cpp / ggml | MIT | MIT | https://github.com/ggml-org/whisper.cpp/blob/master/LICENSE | Yes | same | Yes |
| whisper.unity (Macoron) | MIT (GitHub API `spdx_id: MIT`) | MIT | https://github.com/Macoron/whisper.unity/blob/master/LICENSE.MD | Yes | same | Yes |
| Piper (rhasspy/piper, archived) | MIT | MIT | https://github.com/rhasspy/piper/blob/master/LICENSE.md | Yes | same | Yes |
| piper1-gpl (OHF-Voice, the successor) | **GPL-3.0** | GPL-3.0-only (COPYING) | https://github.com/OHF-Voice/piper1-gpl/blob/main/COPYING | Yes, but copyleft | full GPL | Yes. **Avoid**: it would force GPL on the app |
| espeak-ng (phonemizer used by Piper/VITS voices) | **GPL-3.0** | GPL-3.0 | https://github.com/espeak-ng/espeak-ng (GitHub API) | copyleft | — | Yes. **Risk**, see below |

**sherpa-onnx and espeak-ng.** Current sherpa-onnx `master` `CMakeLists.txt` and `cmake/` contain no espeak references (checked with curl and grep). Earlier releases linked espeak-ng for Piper/VITS TTS. Whether the prebuilt Android `.so` / AAR we pick contains GPL espeak-ng code is **UNVERIFIED**. Before shipping, check the exact release's `cmake/` and binary symbols. If it is present, either use non-espeak TTS models or accept GPL-3.0 for the TTS module (the app is still OSS, but every committed binary then needs to be GPL-compatible).

### Piper voices (https://huggingface.co/rhasspy/piper-voices, repo tag `mit`; each voice's `MODEL_CARD` names its dataset license)

| Voice | Dataset license | Use? |
|---|---|---|
| en_US-lessac-* | Blizzard 2013 Lessac **research licence** (https://www.cstr.ed.ac.uk/projects/blizzard/2013/lessac_blizzard2013/license.html; registration-only, research licence) | **Avoid** |
| en_US-ryan-* | CC BY-NC-SA 4.0 | **Avoid** (NC + SA) |
| en_US-libritts_r-* | CC BY 4.0 | OK with attribution |
| en_US-amy, en_GB-alan | "See URL" (Mycroft mimic3-voices): **UNVERIFIED** | Avoid until checked |
| pl_PL-gosia / darkman / mc_speech | CC0 | OK |

## 7. Animation and character assets

| Item | License | SPDX | Redistribute raw files | Attribution | OSI |
|---|---|---|---|---|---|
| Quaternius (e.g. Universal Animation Library) | "Free to use in personal, educational and commercial projects. (CC0 License)" (https://quaternius.com/packs/universalanimationlibrary.html). The Standard/Pro tiers are free; the paid "Source" tier (.blend, engine projects) is via Patreon | CC0-1.0 | Yes (free tier) | not required (credit is courteous) | No (CC0 is not OSI-approved; FSF-free) |
| Mixamo (Adobe) | Adobe General Terms plus the Mixamo FAQ. The official FAQ (https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) returned **403**. Secondary sources (Adobe community: https://community.adobe.com/questions-696/mixamo-faq-licensing-royalties-ownership-eula-and-tos-589400) say royalty-free in projects, **no distribution of raw character/animation files**. **UNVERIFIED** | none | **No** (assume) | none | No |
| **Jolly mesh** (`Meta+Muse+Jolly+v2+Whole.glb/.stl`) | **No license.** Jolly is Meta's Muse avatar (Axios 2026-09-25: https://axios.com/2026/09/25/ai-doom-meta-muse-mascot). The GLB generator is `THREE.GLTFExporter r186`, the STL header reads `MW 1.0 3441016 US` (MakerWorld ID? the page returned 403), and the file's `com.apple.quarantine` says it arrived via Telegram. **Provenance UNVERIFIED** | none | **No** | — | — |

## 8. Test tooling

| Item | License | SPDX | Text | OSI | Verdict |
|---|---|---|---|---|---|
| NSubstitute | BSD-3-Clause (`PackageLicenseExpression` in `NSubstitute.csproj`) | BSD-3-Clause | https://github.com/nsubstitute/NSubstitute/blob/main/LICENSE.txt | Yes | OK |
| Castle.Core 5.1.1 (NSubstitute dependency) | Apache-2.0 | Apache-2.0 | https://github.com/castleproject/Core/blob/master/LICENSE | Yes | OK |
| **FluentAssertions v8.x** (`main`; tags 8.0.0–8.11.0) | **Xceed Community License Agreement (Non-Commercial Use)**. "Any use outside of these parameters requires a paid Commercial License from Xceed", and Xceed may revoke it "at its sole discretion". OSS use is explicitly listed as non-commercial | none (GitHub: `NOASSERTION`) | https://github.com/fluentassertions/fluentassertions/blob/main/LICENSE | **No** | **Verified: do not use v8** |
| FluentAssertions v7.x (`support-7.0` branch; tags 7.0.0–7.2.2) | Apache-2.0 | Apache-2.0 | https://github.com/fluentassertions/fluentassertions/blob/support-7.0/LICENSE | Yes | OK, pinned `7.2.x` |
| NUnit (via `com.unity.ext.nunit`) | MIT (NUnit 3.5) inside a UPDL Unity package | MIT | Third Party Notices.md | Yes | OK, preferred |

Note: the `writing-unity-tests` convention assumes FluentAssertions. Pin **7.2.x** or use plain NUnit `Assert.That`. A v8 upgrade by NuGetForUnity would silently change the license.

## 9. Hackathon rules and trademarks

**Official Rules are not publicly reachable. UNVERIFIED.**
- https://cerebralvalley.ai/e/meta-ai-developer-hackathon says the Official Rules "will be published before the Hackathon begins on October 26, 2026" and must be accepted by selected participants.
- `/rules` and `/official-rules` return 404. `/details` and `/waiver` render client-side and show no text without login.
- **Human action:** download the accepted Official Rules from the participant dashboard. Check the IP-ownership, open-source, Meta-marks and demo-video clauses before submission.
- The public page confirms only that allowed tooling includes "Muse Spark series, Muse Wonder, Muse Image and SAM 3.1 through Meta Model API, run Meta open weights models."

**Meta trademarks** (https://www.meta.com/brand/resources/meta/our-trademarks/):
- "You may only use our trademarks with our permission."
- You may not use Meta marks in a "trademark, company name, trade name, username, or domain".
- "Muse" and "Jolly" are not listed in the glossary, but the page says silence "does not mean that we have waived our rights".
- Also cited above: Model API ToS §7 (no trademark licence) and the MPT SDK License §3.4 (no brand features or implied endorsement).

**Jolly risk assessment.** Jolly is a Meta-owned character design. That means copyright in the design, regardless of whether there is a trademark registration (UNVERIFIED). Publishing the mesh or a faithful recreation in a public repo under MIT/Apache would grant licences we do not have.

speculation: a Meta-sponsored hackathon demo featuring Jolly would likely be tolerated, but "tolerated" is not a licence. Requirement #9 already allows a redo.

**Recommendation:**
1. Ship an **original character** in the repo, e.g. a CC0 Quaternius-rigged or self-made model with a different silhouette and name.
2. Keep the Jolly asset local-only (git-ignored) for private experiments.
3. Don't use "Meta" or "Muse" in the product, repo or package name. "Jolly" as a project codename is a common word, but because it collides with Meta's mascot name, renaming is the safer choice. That is a human decision.
4. Write "Uses Meta Model API" (a descriptive, nominative use), not "Meta Jolly".

---

## 10. Recommended dependency list (publishable)

Repo license suggestion: **MIT** or **Apache-2.0** for our code. Everything below is either referenced through a registry or is compatible.

| Layer | Dependency | How it enters the repo |
|---|---|---|
| XR | `com.unity.xr.openxr`, `com.unity.xr.meta-openxr`, `com.unity.xr.arfoundation`, `com.unity.xr.hands`, `com.unity.xr.interaction.toolkit`, `com.unity.xr.management` | `Packages/manifest.json` only |
| Strings | `com.unity.localization` | manifest only |
| On-device ML (optional) | `com.unity.ai.inference` (Unity ToS) | manifest only |
| Async/DI/Rx | UniTask (MIT), VContainer (MIT), R3 (MIT) | OpenUPM / git URL in manifest (R3 needs NuGet core, per its README: UNVERIFIED) |
| Lipsync | uLipSync (MIT) | git URL in manifest |
| VAD/STT/TTS offline | sherpa-onnx (Apache-2.0) + Silero VAD (MIT) + Whisper (MIT) | downloaded at build/first run; commit only the C# wrapper if MIT/Apache |
| TTS voices | Piper `pl_PL-*` (CC0), `en_US-libritts_r` (CC BY 4.0) | download script, not committed |
| Cloud AI | Meta Model API (Muse Spark/Wonder/Image/Voice, SAM 3.1) | HTTP; key from user-local config |
| Animations | Quaternius CC0 | can be committed (LFS) |
| Tests | NUnit (Unity built-in), NSubstitute (BSD-3) + Castle.Core (Apache-2.0), FluentAssertions **7.2.x** (Apache-2.0) or none | NuGetForUnity / committed DLLs OK |
| Meta XR SDK / Voice SDK | **Avoid if possible.** OpenXR + meta-openxr covers passthrough, hands and camera. If needed, add via `https://npm.developer.oculus.com` scoped registry | manifest only, never vendored |

## 11. Must NOT be committed

1. `Meta+Muse+Jolly+v2+Whole.glb` / `.stl`, and any Jolly-derived mesh, texture or render. Add them to `.gitignore`. The current `.gitattributes` routes `*.glb`/`*.stl` to LFS, which would publish them.
2. Any `com.meta.*` package source (MPT SDK §1.2.8), including copies under `Assets/Oculus`, `Assets/MetaXR`, `Packages/com.meta.*`.
3. Any `com.unity.*` package source (UPDL is binary-in-project only), and `Library/PackageCache`.
4. SAM 3/3.1 weights, Muse Spark Open weights (licence unknown), Llama weights.
5. Mixamo FBX files (raw-file redistribution not permitted, UNVERIFIED).
6. Piper voices from non-commercial or research datasets: `lessac`, `ryan`, and `amy`/`alan` until checked.
7. FluentAssertions ≥ 8.0 DLLs.
8. `.env`, API keys, `StreamingAssets/secrets*` (already ignored). ToS §3.1 forbids sharing keys.
9. Any GPL-3.0 binary (espeak-ng, piper1-gpl) unless we deliberately choose GPL for that module.
10. Passthrough captures or recordings of real homes or people in test fixtures (privacy; AUP).

## Open items (human action)

- Get the **Official Rules PDF** from the hackathon dashboard and check the IP, open-source and Meta-marks clauses.
- Decide on the **mascot**: an original character (recommended) vs Jolly kept local-only.
- Confirm Mixamo terms from a logged-in Adobe session, or just use Quaternius.
- Check the sherpa-onnx release chosen for espeak-ng linkage.
- Read the Muse Spark Open `LICENSE.md` only if offline Spark is ever planned.
