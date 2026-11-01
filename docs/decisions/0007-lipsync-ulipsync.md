# 0007. Lipsync: uLipSync behind `ILipSyncDriver`, with an amplitude fallback

- Status: accepted
- Date: 2026-11-01

## Context
- TTS gives no visemes (ADR-0006). OVRLipSync is end-of-life and Meta-only.
- The Nubi mouth is a separate mesh with vowel shape keys (`character.md` §2.2.7, C6).
- uLipSync is MIT and uses Job + Burst MFCC with A/I/U/E/O/N output. It works on any `AudioSource` (`OnAudioFilterRead`) or directly through `OnDataReceived(float[], int)` (verified in source, branch `upm`, `package.json` 3.1.5, commit `ece3272`).

## Decision
- Use `com.hecomi.ulipsync` via the git URL `https://github.com/hecomi/uLipSync.git#ece3272762f2148dd8d3e658ec04fcbd79d4d97e`, wrapped by `ULipSyncDriver` in `Jolly.Integrations.ULipSync`.
- Its `LipSyncInfo.phonemeRatios` + `volume` become `MouthShape`.
- Calibrate the profile on the chosen Wit voice.
- The `uLipSync` component sits after `StreamingSpeechOutput` on the same GameObject. Speech is generated in `OnAudioFilterRead` (not a streamed `AudioClip`, whose read-ahead defeats a 150 ms flush), so uLipSync's own filter callback sees exactly the played samples. Filter ordering with uLipSync is UNVERIFIED until spike R3.
- `AmplitudeLipSyncDriver` (in `Jolly.Audio`) reads RMS from the speech ring and maps it to `A`. It is used in tests and if uLipSync fails on IL2CPP/Burst.

## Consequences
- One lipsync path for online TTS, baked lines and any future offline TTS.
- Burst and Mathematics come in as dependencies. Burst is already resolved by the editor (2.0.0).
- The analysed `AudioSource` must play at volume > 0. Ducking is done in the mixer after analysis, if needed.

## Alternatives
- **Own formant driver:** more work, worse result.
- **Wit viseme events:** only through the Voice SDK.
- **Meta Movement SDK audio-to-expression:** Meta-only and Core-SDK-bound.
