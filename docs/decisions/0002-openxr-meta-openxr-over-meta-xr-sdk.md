# 0002. OpenXR + Unity OpenXR: Meta + AR Foundation instead of the Meta XR SDK

- Status: accepted
- Date: 2026-11-01

## Context
Quest 3 is the demo device. Meta VR glasses, Android XR and Pico are the roadmap (requirement 25). The project is open source. The Meta XR Core SDK and MRUK are under the Oculus/MPT SDK License: not OSI, limited to "MPT approved hardware", and they must not be redistributed (`licenses.md` §2). Requirement 19 says to prefer default Unity solutions.

## Decision
- Use the editor-bundled package set for 6000.6.6f1, pinned per `xr-stack.md` §1 and critic C1:

| Package | Version |
|---|---|
| `com.unity.xr.openxr` | 1.18.0 |
| `com.unity.xr.meta-openxr` | 2.6.1 |
| `com.unity.xr.arfoundation` | 6.6.2 |
| `com.unity.xr.hands` | 1.9.0 |
| `com.unity.xr.interaction.toolkit` | 3.6.1 |
| `com.unity.xr.management` | 4.7.0 |
| `com.unity.xr.core-utils` | 2.6.0 |
| `com.unity.xr.compositionlayers` | 2.6.0 |
| `com.unity.ai.navigation` | 2.0.15 |
| `com.unity.localization` | 1.5.13 |
| `com.unity.inputsystem` | 1.20.0 |
| `com.unity.animation.rigging` | 6.6.0 (built-in) |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 |

- Upgrade the whole set together or not at all (1.19 / 2.7 / 6.7.1).
- Meta-specific code lives only in `Jolly.XR.MetaQuest`.
- Hands are the primary input: XR Hands plus the Hand Interaction Profile. The Oculus Touch profile is the fallback.

## Consequences
- The Quest build still pulls Oculus-licensed binaries through UPM (inside meta-openxr and hands). That is acceptable because they are not committed. THIRD_PARTY says so.
- Editor and Link get no PCA CPU images (`xr-stack.md` §2.2), so editor work needs fake frame sources.
- There are no Meta Interaction SDK niceties; we build with XRI.

## Alternatives
- **Meta XR SDK + MRUK:** faster PCA iteration over Link, but the licence is non-OSI and the code is not portable.
- **Mixing both stacks:** coexistence is UNVERIFIED, and it would mean two XR stacks.
