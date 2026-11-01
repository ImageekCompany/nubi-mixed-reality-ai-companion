# 0006. Text to speech: Wit.ai `/synthesize` over raw HTTP

- Status: accepted
- Date: 2026-11-01

## Context
- The Meta Model API has no TTS.
- The Voice SDK is under the MPT SDK License (hardware-restricted) and is untested on 6000.6 (C10).
- Android system TTS is probably absent on Quest (B1).
- **Live on 2026-11-01** with the client token: `POST https://api.wit.ai/synthesize?v=20250213`, `Accept: audio/raw`, `{"q","voice":"Charlie"}` returned 200, `Content-Type: audio/raw`, 24 kHz s16le mono. TTFB was 186 / 307 / 435 ms and total 0.27–1.18 s for about 3 s of audio. `GET /voices` lists only `en_US` (23), `en_CA` (2) and `en_GB` (5) voices for our app: no Polish.

## Decision
- `WitTextToSpeech` (`Jolly.Providers.Wit`):
  - `UnityWebRequest` with a custom `DownloadHandlerScript` built with the preallocated-buffer constructor (pooled 32 KB), since the parameterless one allocates a `byte[]` per callback.
  - `ReceiveData(byte[], int)` (verified) slices bytes into pooled `PcmChunk`s and carries an odd trailing byte over to the next chunk.
  - Cancellation aborts the request.
- The SpeechQueue keeps at most 2 in-flight requests (current + prefetch).
- Text per request is ≤ 240 characters.
- Voice and speed come from the persona and locale.
- **Canned lines:** baked at dev time through the same client into WAV assets (`CannedLineBaker`), so the offline voice matches the online one. Polish canned lines are baked with Piper `pl_PL-gosia` (CC0) outside the build.

## Consequences
- No Meta SDK is linked. The Wit service ToS applies, and the client token is provisioned like the other secrets (ADR-0010).
- There are no visemes: uLipSync analyses the audio (ADR-0007).
- Polish speech output is captions plus baked lines only, until a Polish Wit app or another provider exists.
- Whether storing Wit output as shipped assets is allowed by the Wit ToS is UNVERIFIED. If not, re-bake the English lines with Piper `en_US-libritts_r` (CC BY 4.0).

## Alternatives
- **Voice SDK `TTSWit`:** works, but brings licence and portability cost.
- **Piper/sherpa runtime:** GPL espeak issue in v1.x, phonemizer gap in v2.x (`voice.md` §4).
- **Cloud TTS from other vendors:** not Meta tooling, and needs another key.
