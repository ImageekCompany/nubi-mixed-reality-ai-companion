# Voice loop research: wake word / PTT, STT, LLM, TTS, lipsync, barge-in

Date: 2026-11-01. Scope: requirements.md items 14, 18, 23, 25, plus 5, 10 and 12 where they affect voice.
Sources are cited inline. "Ran:" means a command I ran. **UNVERIFIED** means I could not confirm it from a primary source.

Scratch evidence (not in the repo): Voice SDK tarball extracted at
`/private/tmp/claude-503/-Users-andrewurbanovich-Git-Projects-MetaCompetitionClaude/f031d2aa-adea-490e-8975-c2c648901da4/scratchpad/voice/pkg/package`.

---

## 0. TL;DR recommendation

| Stage | Online (demo default) | Offline / privacy fallback | Why |
|---|---|---|---|
| Activation | **Hand pinch-and-hold push-to-talk** (XR Hands, OpenXR). Wake word "Hey Jolly" is a *should* | sherpa-onnx KWS (gigaspeech 3.3M) | PTT is deterministic and avoids the echo problem. KWS is optional polish |
| VAD / endpointing | Muse realtime `ENDPOINTING` mode, or local Silero VAD | Silero VAD v5 via sherpa-onnx (2 MB) | |
| STT | **Muse Voice Transcribe realtime** (`wss://api.meta.ai/v1/asr/realtime`). Counts as Meta AI tooling, supports Polish | sherpa-onnx streaming zipformer EN (20M) | Wit STT is the second choice |
| LLM | `muse-spark-1.3(-contributor)` streaming, `reasoning_effort:"minimal"` | (another agent) | Measured below |
| TTS | **Wit.ai `/synthesize`** through Voice SDK `TTSWit` + `TTSSpeaker` (streaming, 24 kHz PCM, viseme events) | **Pre-baked clips** for req. 23 (idle and offline lines). Runtime Piper is optional | The Meta Model API has no TTS model |
| Lipsync | **uLipSync** (MIT) on the TTS `AudioSource`, driving a jaw bone or one mouth blendshape | same | Works with any TTS source, baked or runtime |
| Barge-in | Pinch during speech = instant interrupt. Voice barge-in uses a playback-gated VAD | same | Quest AEC for app capture is **UNVERIFIED** |
| Locale | Unity Localization 1.5.13 `LocalizationSettings.SelectedLocale` selects STT `languageBias`, TTS voice and baked-clip tables | | |

The <2 s budget is reachable only with streaming at every stage plus an instant pre-cached acknowledgement. See §8.

---

## 1. Meta Voice SDK (`com.meta.xr.sdk.voice`)

**Version and registry.** Ran: `curl https://npm.developer.oculus.com/com.meta.xr.sdk.voice`
- `dist-tags.latest = 85.0.1`, published 2026-07-16. Previous releases: 85.0.0 (2026-02-10) and 83.0.0 (2025-12-11).
- Tarball: `https://npm.developer.oculus.com/com.meta.xr.sdk.voice/-/com.meta.xr.sdk.voice-85.0.1.tgz`
- `package.json`: `"unity": "2022.3"`, `"unityRelease": "15f1"`, `"dependencies": {}`, display name "Meta - Voice SDK - Immersive Voice Commands".
- **Unity 6.6 (6000.6.6f1) compatibility is UNVERIFIED.** The package declares only a 2022.3.15f1 minimum. Spike: import it into the project and run an Android IL2CPP build.

**Install through a UPM scoped registry** (registry URL verified above; manifest shape is standard UPM):
```json
"scopedRegistries": [{ "name": "Meta", "url": "https://npm.developer.oculus.com", "scopes": ["com.meta.xr.sdk"] }],
"dependencies": { "com.meta.xr.sdk.voice": "85.0.1" }
```

**Dependency on Meta XR Core / OVRPlugin: none.**
- `dependencies: {}`.
- Ran: `grep -rl "OVRPlugin\|OVRManager\|Oculus.VR"` over the package. No hits.
- asmdef references are internal only (`Meta.WitAi*`, `AssistantVoiceCommandCommon`, …). See `Scripts/Runtime/VoiceSDK.Runtime.asmdef`.
- Optional Quest platform path: `AppVoiceExperience.usePlatformServices` (`Scripts/Runtime/Service/AppVoiceExperience.cs:52`) plus `Plugins/voicesdk_unity.jar`. Leave it **off** for OpenXR portability.
- **Conclusion:** the SDK works on plain OpenXR. It is plain C# with UnityWebRequest and Unity `Microphone` (`Lib/Wit.ai/Scripts/Runtime/Lib/Mic/Other/Mic.cs`).

**License.**
- `LICENSE.md`: "Meta Platform Technologies SDK License Agreement", https://developers.meta.com/horizon/licenses/oculussdk/.
- `package.json.licenseMessage` says adding the package means agreeing to it.
- This is not an OSI license. List it in THIRD_PARTY. Do not vendor the package into the repo. Reference it through the registry instead.
- Embedded third-party code also needs attribution: `Lib/Wit.ai/Lib/third-party/{NLayer,NativeWebSocket,UnityOpus}`. Check each LICENSE when writing THIRD_PARTY.

**TTS (`TTSWit` + `TTSSpeaker`). Verified in source:**
- Streaming is on by default. `TTSWit.RequestSettings.audioStream = true` and `useEvents = true` (`Lib/Wit.ai/Features/TTS/Scripts/Runtime/Integrations/TTSWit.cs:105-113`).
- Playback starts after `audioReadyDuration` = `ENDPOINT_TTS_DEFAULT_READY_LENGTH = 1.5f` seconds of buffered audio (`Lib/Wit.ai/Lib/Wit/Runtime/WitConstants.cs:87`).
  - **For latency, lower this to about 0.2–0.3 s** (field `audioReadyDuration`). The exact semantics are UNVERIFIED; test it.
- Audio types: `TTSWitAudioType { PCM, MPEG (default), WAV, OPUS }` (`Lib/Wit/Runtime/Data/TTSWitAudioType.cs`). WAV cannot stream (`WitRequestSettings.CanStreamAudio`).
  - Use **PCM**. Output is 24 kHz mono (`ENDPOINT_TTS_SAMPLE_RATE = 24_000`) and there is no MP3 decode cost.
- **Text limit is 280 characters per request** (`ENDPOINT_TTS_MAX_TEXT_LENGTH = 280`, WitConstants.cs:104). Sentence chunking is mandatory, which also suits streaming.
- Transport: HTTP by default (`DEFAULT_REQUEST_TYPE = WitRequestType.Http`). WebSocket also exists (`WIT_SOCKET_URL = "wss://api.wit.ai/composer"`).
- Voice presets: `TTSWitVoiceSettings { voice="Charlie" (default), style="default", speed 50–200 (100), pitch 25–200 (100) }` (`TTSWitVoiceSettings.cs`, WitConstants.cs:247-302). `TTSSpeaker.presetVoiceID` selects a preset.
  - The voice list is fetched in the editor from Wit `GET voices` (`WitEditorConstants.ENDPOINT_TTS_VOICES = "voices"`).
- **Visemes and phonemes for lipsync.**
  - When `useEvents=true`, the POST body includes `"viseme":"true"` (`WitTTSVRequest.EncodePostData`, `Scripts/Runtime/Requests/UnityRequests/WitTTSVRequest.cs:185-197`).
  - Event types: `TTSVisemeEvent`, `TTSPhonemeEvent`, `TTSWordEvent` (`Features/TTS/Scripts/Runtime/Data/`).
  - Viseme enum (15, Oculus set): `sil PP FF TH DD kk CH SS nn RR aa E ih oh ou` (`Data/Viseme.cs`).
  - Ready-made drivers: `VisemeBlendShapeLipSync`, `VisemeLipSyncAnimator` (`OnVisemeLerp`), `VisemeTextureFlipLipSync` (`Features/TTS/Scripts/Runtime/LipSync/`). Sample: "TTS Lip Sync".
- Speaker API: `Speak`, `SpeakQueued`, `SpeakTask(string)` returning `Task`, `Stop()`, `StopSpeaking()`, `StopLoading()`, `Pause()`.
  - Events: `OnStartSpeaking`, `OnFinishedSpeaking`, `OnCancelledSpeaking`, `OnPlaybackQueueComplete` (`Utilities/TTSSpeaker.cs`, `Events/TTSSpeakerEvents.cs`).
  - `SpeakQueued` plus `Stop()` is the barge-in primitive.

**STT (`AppVoiceExperience`, `AppDictationExperience`):**
- `Activate()`, `ActivateImmediately()`, `Deactivate()`, `DeactivateAndAbortRequest()` (`Scripts/Runtime/Service/AppVoiceExperience.cs:192-242`).
- Partial transcription: `OnPartialTranscription`, `OnFullTranscription` (`Lib/Wit.ai/Scripts/Runtime/VoiceService.cs`, `WitService.cs:583`). Continuous dictation lives in `Features/Dictation` (`AppDictationExperience`).
- Runtime config (`WitRuntimeConfiguration.cs`): `soundWakeThreshold=.0005`, `minKeepAliveVolume`, `minTranscriptionKeepAliveTimeInSeconds=1`, `maxRecordingTime=20`.
- Wire format: 16 kHz raw PCM sent chunked (`ENDPOINT_SPEECH_SAMPLE_RATE=16000`; `WitRequest.cs:430` sets `SendChunked = true` and `Content-Type: audio/raw;bits=16;rate=16k;encoding=signed-integer;endian=little`, built in `AudioEncoding.cs`).
- **Wake word: none.** `OnMinimumWakeThresholdHit` is a *volume* threshold, not a keyword spotter. Ran: grep for "wakeword/hotword" with no hits. The Meta overview mentions only "built-in activation methods" (https://developers.meta.com/horizon/documentation/unity/voice-sdk-overview/).
- Languages: "We support English voice recognition, and offer an early preview of voice recognition in other languages" (same overview page). **Polish quality is UNVERIFIED.**

**Accounts.** Wit TTS and STT need a **Wit.ai app and its token**, which is separate from `META_API_KEY`. `.env` contains only `META_API_KEY`. This is a **human action**: create a Wit app and put the client token in the local, git-ignored config. Per req. 17, note that a Wit *client* token ends up inside the APK.

## 2. Wit.ai HTTP API used directly

https://wit.ai/docs/http is a JS-rendered page. Both WebFetch and curl returned only the shell, and the browser tool did not respond. **The details below are verified from the Voice SDK client source, not from the docs page.** Re-check them against the docs page in a browser.

- Base: `https://api.wit.ai`, API version `v=20250213` (`WitConstants.API_VERSION`), header `Authorization: Bearer <wit token>`.
- **TTS:** `POST /synthesize`, `Content-Type: application/json`, body `{"q": text, "voice": ..., "style": ..., "speed": ..., "pitch": ..., "viseme": "true"}`.
  - `Accept` selects the format: `audio/raw` (PCM16 24 kHz mono), `audio/mpeg`, `audio/wav`, `audio/opus-demo` (`WitRequestSettings.GetAudioMimeType`).
  - The response is streamed, and the SDK decodes chunks as they arrive (`AudioStreamHandler`). When events are on, the decoder interleaves JSON event chunks with audio (`GetTtsAudioDecoder(FileType, onJsonDecoded)`). The exact framing is **UNVERIFIED**.
- **Voices:** `GET /voices`, used by the editor tab `WitConfigurationVoicesTab.cs`. The default is "Charlie". Meta docs also mention Rebecca, Prospector, Vampire and Cooper (https://developers.meta.com/horizon/documentation/unreal/vsdk-tts-voice-customization/).
  - **Polish TTS voices: UNVERIFIED and likely absent.** Call `GET /voices` with the Wit token to confirm.
- **STT:** `POST /speech` (intent and transcription) and `POST /dictation` (`ENDPOINT_SPEECH`, `ENDPOINT_DICTATION`), chunked raw PCM16 16 kHz. The streamed JSON responses carry `is_final` (`KEY_RESPONSE_IS_FINAL`).
- **Latency:** not measured, because there is no Wit token. **UNVERIFIED.** Measure TTFB of `/synthesize` once the token exists.
- **Portable option:** a 200-line `HttpClient`/`UnityWebRequest` + `DownloadHandlerScript` client for `/synthesize` (PCM) is portable to any XR platform and drops the 300+ file SDK. You lose the SDK's viseme decoder; uLipSync covers that (§5).
  - Recommendation: **use `TTSWit` behind our own `ITextToSpeech` interface for the hackathon** (verified streaming plus samples). Replace it with the raw client later if SDK weight or license is a problem.

## 3. Muse Voice Transcribe (STT alternative)

Docs: https://dev.meta.ai/docs/speech-to-text. Another agent tests it. Summary from the docs:
- Realtime endpoint: `wss://api.meta.ai/v1/asr/realtime`. Batch endpoint: `POST https://api.meta.ai/v1/asr/transcribe`.
- Audio: PCM16 LE mono at 16 or 24 kHz.
- 25 languages, **including Polish**, steered with `languageBias`.
- Partials: `CUMULATIVE` or `DELTA`.
- Modes: `PUSH_TO_TALK` (`final:true`), `ENDPOINTING` (`speechStart/speechEnd/speechComplete` per `turnId`), `DIARIZATION`.
- Price: $0.18/h. Sessions up to 60 min.
- Auth is sent in the first JSON frame, not in the HTTP header.
- Ran: `GET /v1/models` lists `muse-voice-transcribe-1.0`.
- **The Meta Model API has no TTS model.** Ran: `/v1/models` returns only muse-wonder, sam-3.1, muse-spark-*, muse-voice-transcribe, muse-image. `dev.meta.ai/docs/{text-to-speech,tts,speech-synthesis}` return 404.

## 4. Offline fallbacks on Quest (Android arm64)

### sherpa-onnx
Repo: https://github.com/k2-fsa/sherpa-onnx, Apache-2.0 (ran `gh api repos/k2-fsa/sherpa-onnx`).
- Latest release **v2.0.1** (2026-10-26).
  - Android asset `sherpa-onnx-v2.0.1-android.tar.bz2` is 45 MB, all ABIs. A static-onnxruntime variant is 35 MB.
  - It contains `libsherpa-onnx-c-api.so`. The C# wrapper P/Invokes `"sherpa-onnx-c-api"` (`scripts/dotnet/Dll.cs`).
- C# API (`scripts/dotnet/`): `OnlineRecognizer`, `OfflineRecognizer`, `KeywordSpotter` (`CreateStream(keywords)`, `IsReady`, `Decode`, `GetResult`), `VoiceActivityDetector` with `SileroVadModelConfig`, `OfflineTts` (`Generate`, `GenerateWithCallback`).
- Unity path: copy `arm64-v8a/*.so` to `Assets/Plugins/Android/arm64-v8a/` and the `scripts/dotnet/*.cs` sources into an asmdef. Alternatively use the community UPM wrapper `com.eitan.sherpa-onnx-unity` (Apache-2.0, 106★, OpenUPM, Android arm64-v8a, last push 2026-05-07, https://github.com/EitanWong/com.eitan.sherpa-onnx-unity).
  - The wrapper's pinned sherpa version is UNVERIFIED.
- **IL2CPP gotcha.** `OfflineTts.GenerateWithCallback` marshals a C# lambda closure as a native callback (`scripts/dotnet/OfflineTts.cs:55-80`). IL2CPP can marshal only static `[MonoPInvokeCallback]` methods, so expect a runtime failure on Quest. Use per-sentence `Generate()` (non-callback) instead. Expected behaviour; **UNVERIFIED on device**.
- **Breaking change in v2.0.0 (merged 2026-10-20):** "Remove espeak-ng and piper-phonemize from sherpa-onnx" (#4055, CHANGELOG.md).
  - Piper/VITS voices now need an external phonemizer (piper-phonemize, which is GPL) or a full `lexicon.txt` (issue #3731).
  - The last release with built-in espeak-ng is **v1.13.8**. That build links espeak-ng, which is **GPL-3.0** (ran `gh api repos/espeak-ng/espeak-ng`). Shipping it in an APK alongside the proprietary Meta SDK creates a GPL-compatibility problem.
  - **Consequence: do not ship runtime Piper TTS for the hackathon.**

### Voice models (sizes from `gh api …/releases/tags/{kws,asr,tts}-models`)
| Use | Model | Size (tar.bz2) | License |
|---|---|---|---|
| Wake word | `sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01-mobile` | 14 MB | Apache-2.0 (sherpa release). Model license UNVERIFIED (GigaSpeech-trained) |
| VAD | `silero_vad_v5.onnx` | 2 MB | MIT (Silero, https://github.com/snakers4/silero-vad). UNVERIFIED for v5 file |
| Offline STT EN | `sherpa-onnx-streaming-zipformer-en-20M-2023-02-17-mobile` | 102 MB (int8 encoder much smaller, UNVERIFIED) | Apache-2.0 |
| Offline STT EN (small) | `sherpa-onnx-streaming-zipformer-en-kroko-2025-08-06` | 54 MB | UNVERIFIED |
| Offline TTS EN | `vits-piper-en_US-joe-medium-int8` | 20 MB | dataset **CC0** (MODEL_CARD) |
| Offline TTS EN alt | `vits-piper-en_US-kristin-medium-int8` | 19 MB | dataset **public domain** (LibriVox) |
| Offline TTS PL | `vits-piper-pl_PL-gosia-medium-int8` (female) or `darkman` (male) | 20 MB | dataset **CC0** |
| Avoid | `en_US-ryan`, `hfc_male/hfc_female` | | **CC BY-NC-SA 4.0** |
| Avoid / check | `en_US-lessac` | | Blizzard 2013 license, research-oriented |

Piper MODEL_CARDs come from `https://huggingface.co/rhasspy/piper-voices/resolve/main/<lang>/<voice>/medium/MODEL_CARD` (ran). The repo card license is `mit`.

### Other options
- **whisper.unity** (MIT, https://github.com/Macoron/whisper.unity, last push 2025-04-17): whisper.cpp with Android support. It is not streaming, and `ggml-tiny.en` is about 75 MB (UNVERIFIED size). Good for batch offline STT, worse than zipformer for streaming.
- **Unity Inference Engine** `com.unity.ai.inference` 2.6.1 is bundled with the 6000.6.6f1 editor (ran: editor `PackageManager/Editor/manifest.json`). It can run Whisper or Silero ONNX on GPU or CPU, but you hand-write the pre- and post-processing. Higher effort; not recommended in 3 days.
- **Kokoro** (Apache-2.0, `kokoro-int8-en-v0_19` 98 MB) and **Kitten** (≥25 MB) also depend on espeak, so they have the same v2 problem.

### Recommendation for offline (req. 23)
- Req. 23 asks only for *predefined* greetings and idle lines.
- **Pre-render them at dev time** with Piper `en_US-joe` (CC0) and `pl_PL-gosia` (CC0), using a desktop script outside the APK so the GPL tool never ships. Import them as AudioClips in a Localization asset table, and **pre-bake uLipSync data** (§5).
- Zero runtime model cost, zero GPL exposure, deterministic demo.
- Offline STT and KWS (sherpa v2, GPL-free) are *could*-tier, needed later for the "Psychologist offline" variant.
- Expected latency on the Quest 3 XR2 Gen 2 CPU, streaming zipformer 20M: RTF well under 0.2. KWS: tens of ms per chunk. **UNVERIFIED on device; profile before committing.**

## 5. Lipsync

**Jolly asset fact.** Ran a GLB JSON parse of `Meta+Muse+Jolly+v2+Whole.glb`: 1 mesh, 1 primitive, **0 morph targets, 0 skins, 0 animations**, 2 nodes. The model has **no mouth rig at all**. Someone must add a jaw bone or one or more mouth blendshapes (e.g. in Blender) before *any* lipsync works. **Human or art task. High risk.**

| Option | License | Status | Fit |
|---|---|---|---|
| **uLipSync** v3.1.4 (https://github.com/hecomi/uLipSync, 1.7k★, last push 2026-01-06) | MIT | Active. Job System + Burst, no native plugin, so Android works | Runtime MFCC on any `AudioSource` (`OnAudioFilterRead`). Outputs A/I/U/E/O/N weights. `uLipSyncBlendShape`, `uLipSyncAnimator`, `uLipSyncTexture`. **Pre-bake to `BakedData`/AnimationClip** for the offline clips. UPM: `https://github.com/hecomi/uLipSync.git#upm` |
| Oculus/Meta LipSync (OVRLipSync) | Oculus SDK license | **End-of-life** per Meta: https://developers.meta.com/horizon/documentation/unity/audio-ovrlipsync-unity/. The replacement is Movement SDK audio-to-expression (`XR_META_face_tracking_visemes`), which is tied to Meta's OpenXR runtime and Core SDK (Unity setup UNVERIFIED) | Not portable. Avoid |
| Wit TTS viseme events | Meta SDK license | Verified in source (§1) | Exact visemes, but only for Wit audio. Offline and Muse paths get nothing |

**Recommendation.** Use **uLipSync** as the single provider-agnostic path behind `ILipSyncDriver`.
- For a stylized mascot, map the vowel weights to **one jaw-bone rotation plus optional "O/U" pucker and "I/E" wide blendshapes**. Three shapes are enough for a cartoon read.
- Calibrate the uLipSync Profile on the chosen TTS voice.
- Optional enhancement: with Wit, map `Viseme` to the same three shapes via `VisemeLipSyncAnimator.OnVisemeLerp`.
- The `AudioSource` must play at volume > 0 for analysis. The uLipSync README says that at volume 0 the data in `OnAudioFilterRead` is silent.

## 6. Barge-in, echo cancellation, VAD

- **Unity `Microphone`** exposes no AEC, noise-suppression or audio-source selection API. The Voice SDK uses plain `Microphone.Start` (`Mic.cs`).
  - Which Android `AudioSource` Unity uses internally, and whether Quest applies AEC to it: **UNVERIFIED**.
  - Meta's platform-audio page documents the default mic (48 kHz mono) and OS mic-mute, not AEC (https://developers.meta.com/horizon/documentation/unity/platform-audio/).
  - Field reports of Unity mic capture freezing on Quest 3 standalone exist (https://forum.convai.com/t/microphone-silently-fails-to-capture-on-meta-quest-3-standalone-builds/6422). Test the mic early on device.
- Native option (spike, *could*): a small Android Java plugin using `AudioRecord(MediaRecorder.AudioSource.VOICE_COMMUNICATION)` and `AcousticEchoCanceler.isAvailable()/create(sessionId)` (https://developer.android.com/reference/android/media/audiofx/AcousticEchoCanceler). Whether Horizon OS implements it: **UNVERIFIED**.
- **Design that works without AEC:**
  1. **Pinch while Jolly talks = barge-in.** `TTSSpeaker.Stop()` / `StopLoading()`, cancel the LLM `CancellationToken`, then start STT. Deterministic, and satisfies req. 12 (hand tracking).
  2. **Voice barge-in through a playback-gated VAD.** While TTS plays, run Silero VAD (sherpa `VoiceActivityDetector`) or energy VAD on the mic. Trigger only if mic RMS exceeds k × the RMS of the samples currently playing (delay-compensated, k≈2–3) for ≥250 ms. We own the playback buffer, so this reference is free.
  3. Optional: KWS "Jolly stop" / "hey Jolly" stays active during speech. Jolly's own lines should avoid saying its name.
- Spatial trick: keep Jolly's `AudioSource` spatialized at the character, not at head-locked full volume. This lowers mic pickup. Gain is UNVERIFIED.

## 7. Localization (req. 18)

- `com.unity.localization` **1.5.13** is bundled with editor 6000.6.6f1 (ran: editor manifest; also `packages.unity.com` latest = 1.5.13). It depends on Addressables 1.25.
- **String tables:** UI, persona prompts and fallback lines.
- **Asset tables:** baked AudioClips plus uLipSync `BakedData` per locale.
- **`VoiceLocaleProfile` ScriptableObject per `Locale`:**
  - STT `languageBias` (Muse), Wit app/token or locale.
  - TTS voice id: Wit "Charlie" for EN. For PL, a Wit voice is UNVERIFIED, so fall back to baked clips or PL text-only.
  - uLipSync profile.
  - Resolve it on `LocalizationSettings.SelectedLocaleChanged`.

## 8. Latency budget (end of user speech → first Jolly audio)

Measured LLM numbers. Ran `scratchpad/scripts/ttft.py`: streaming `POST https://api.meta.ai/v1/chat/completions`, short persona prompt, from this Mac.
- Without `reasoning_effort`, `muse-spark-1.3-contributor` often used the whole budget on reasoning: `content:null, finish_reason:"length", reasoning_tokens:398/400`. **Always set `reasoning_effort:"minimal"`** and `max_completion_tokens` (the docs say `"none"` returns 400: https://dev.meta.ai/docs/protocols/chat-completions).
- `minimal` results:
  - contributor: first token 1.57 / 1.81 / 1.62 s, first sentence 1.63–1.92 s.
  - `muse-spark-1.3`: first token 0.93–1.10 s.
- `low`: contributor 3.1–3.2 s. Too slow.

| Stage | Target | Basis |
|---|---|---|
| Endpointing after speech stops (PTT release = 0; VAD ≈ 300–500 ms) | 0–0.4 s | PTT removes this cost |
| STT final after release (Muse realtime) | ≤0.3 s | UNVERIFIED (other agent) |
| LLM first sentence | 1.0 s (spark-1.3) / 1.7 s (contributor) | measured |
| TTS first audio chunk (Wit PCM streaming, ready buffer ~0.25 s) | ~0.3–0.5 s | UNVERIFIED |
| **Total** | **1.6–2.0 s (spark-1.3) / 2.3–2.9 s (contributor)** | |

**Mitigations.**
- (a) On PTT release, play a **pre-cached acknowledgement** ("Hmm!", "Let me see…") plus a listening→thinking animation within 150 ms. This keeps *perceived* first audio far under 2 s.
- (b) Stream LLM → sentence splitter → `TTSSpeaker.SpeakQueued` per sentence. The first chunk should be ≤120 chars.
- (c) Lower `audioReadyDuration`.
- (d) Use `muse-spark-1.3` for the voice turn's first reply and contributor for tool and memory calls. This conflicts with req. 7, so it needs a decision and an ADR.
- (e) Keep the system prompt short and cached.

## 9. Proposed interfaces (for ADR)
- `IVoiceActivation` (PinchPushToTalk, KeywordSpotter)
- `ISpeechToText` (MuseRealtimeStt, WitStt, SherpaStt)
- `IChatModel` (streaming `IUniTaskAsyncEnumerable<string>`)
- `ITextToSpeech` (WitTts, BakedClipTts)
- `ILipSyncDriver` (uLipSync)
- `IBargeInDetector`

`VoiceLoopController` is a state machine: Idle → Listening → Thinking → Speaking → (Interrupted → Listening). One `CancellationTokenSource` per turn, cancelled on barge-in.

## 10. Risks (ranked)
1. **Jolly GLB has no rig or blendshapes.** Lipsync and walking are blocked until it is re-rigged (§5).
2. **LLM latency.** Contributor model plus reasoning puts first audio at about 2.3–2.9 s. Needs `reasoning_effort:"minimal"`, a filler clip, and possibly non-contributor for the first sentence (§8).
3. **Wit.ai account and token are missing.** TTS is blocked until a human creates a Wit app. Polish TTS voices are unconfirmed (§2).
4. **No verified AEC on Quest.** Voice barge-in may self-trigger, so pinch barge-in must be the primary path (§6).
5. **sherpa-onnx v2 dropped espeak.** Runtime Piper means either GPL v1.13.8 or a lexicon. Pre-baking avoids it (§4).
6. **Voice SDK 85.0.1 on Unity 6000.6 is unverified.** Do an import and build spike on day 1 (§1).
7. **Unity `Microphone` reliability on Quest 3 standalone.** Reports of frozen capture; test on device first (§6).

## 11. Character voice audition (2026-11-01)

Request: Nubi should sound "more like a cartoon character, but still suitable for normal conversation". It also speaks as Mentor, Guru and Listener, including sensitive talk, and should work in Polish later. Decision: ADR-0031.

**What Wit offers (live, our app).** `GET /voices` lists 30 voices: `en_US` 23, `en_CA` 2, `en_GB` 5, **no Polish**. Each lists `style`, `pitch`, `speed`, `sfx` and viseme/phoneme/word events; styles are `default`, `soft`, `formal`, `fast`, `projected` (Charlie lacks `formal`, Prospector has only `default`). `/synthesize` accepts `speed` 50–200 and `pitch` 25–200 (Voice SDK `WitConstants`). `gain` is accepted but has no effect (same PCM as without it). Effects are SSML only: `<speak><sfx character="chipmunk|monster|daemon|robot|alien" environment="reverb|room|cathedral|radio|phone|pa|helmet">…</sfx></speak>`. SSML of ~290 characters and XML entities were accepted. https://wit.ai/docs/http renders only in a browser, so these come from the live API and the Voice SDK source.

**Round 1** (4 lines: greeting, explanation, listener, excited; median F0 per line in Hz):

| Candidate | F0 median (Hz) | Range (semitones) | First byte (ms) |
|---|---|---|---|
| Charlie (current) | 154–182 | 7.3–9.6 | 277 |
| Cartoon Kid | 208–310 | 5.9–9.8 | 200 |
| Cartoon Kid, soft | 204–256 | 6.1–12.4 | 226 |
| Cartoon Baby | 256–340 | 7.1–13.9 | 213 |
| Whimsical | 249–336 | 13.9–19.8 | 308 |
| Rubie, pitch 110 | 250–280 | 12.5–14.1 | 278 |
| Railey, pitch 112, speed 105 | 309–387 | 10.6–12.6 | 240 |
| Remi, pitch 110 | 257–396 | 10.9–19.7 | 246 |
| Cody, pitch 115, speed 105 | 184–188 | 7.1–12.9 | 240 |
| Charlie, pitch 120, speed 105 | 177–212 | 8.6–12.5 | 239 |
| Charlie + chipmunk sfx | 241–315 | 7.3–10.9 | 234 |
| Rebecca, pitch 112 | 223–275 | 11.8–14.4 | 260 |
| Trendy | 196–260 | 9.1–14.9 | 268 |

Cartoon Kid soft at speed 115 was also tried. Six went to a re-check.

**Re-check** (3 new lines: casual, sensitive, a 43-word answer). First byte is the median of 5 uncached short lines on a warmed connection, alternating the voice order. Errors are word errors of `muse-voice-transcribe-1.0` (`POST /v1/asr/transcribe`, 16 calls, ~$0.009, logged in `api-spend.md`); the second figure drops repeated words ("every every"), which varied run to run on the same audio and come from the transcriber. Noise is spectral flatness (higher = noisier, a proxy for robotic or breathy artefacts). Clarity is the energy share in 1–4 kHz; it rises with any upward pitch shift, so it was not used to rank.

| Candidate | First byte (ms) | F0 Hz (casual/sensitive/long) | Range (st) | Words/s (long) | Clarity 1–4 kHz | Noise median / p90 | Clipping | Errors / without repeats |
|---|---|---|---|---|---|---|---|---|
| A Charlie (current) | 267 | 163/163/174 | 7.3–10.3 | 3.67 | 0.13–0.15 | 0.0015–0.0022 / ≤0.064 | 0 | 0% / 0% |
| **B Rubie, pitch 110** | **252** | **245/255/253** | **9.2–12.0** | **3.28** | 0.12–0.13 | **0.0010–0.0013** / ≤0.081 | 0 | **0% / 0%, 3 runs** |
| C Cartoon Kid, soft, speed 128 | 211 | 220/208/216 | 4.8–7.8 | 3.05 | 0.10–0.16 | 0.0024–0.0037 / 0.128 | 0 | long: 11.6% and 4.7% / 7.0% and 0% |
| D Charlie, pitch 120, speed 105 | 208 | 197/184/186 | 8.0–10.7 | 3.24 | 0.12–0.13 | 0.0011–0.0019 / ≤0.052 | 0 | ≤4.7% / 0% |
| E Whimsical | 203 | 376/238/311 | 12.3–17.1 | 2.61 | 0.19–0.22 | 0.0035–0.0088 / 0.18 | 0 | sensitive 13.6% then 0% / ≤9.1% |
| F Charlie + chipmunk sfx | 250 | 252/243/235 | 7.1–9.5 | 3.59 | 0.19–0.23 | 0.0044–0.0061 / 0.098 | 0 | ≤4.5% / 0% |

- **Latency** does not separate them: all are within noise of Charlie, far from a +150 ms concern.
- **Winner: B, Rubie, pitch 110, speed 100, default style, no sfx** (`{"voice":"Rubie","speed":100,"pitch":110}`). Clean transcripts on every run, the lowest noise, ~1.5× Charlie's pitch with a wider range, and its pitch barely moves on the sensitive line (245 → 255 Hz) while the range widens instead of turning excited.
- **Runner-up: C, Cartoon Kid soft at speed 128.** The most cartoon-like that still sounds like a person, at a normal pace (3.05–3.75 words/s), but monotone and the only real recognition error ("sourdough" → "Soto").
- E is breathy, slow and squeaky on casual lines; F needs SSML and shows a processing artefact; D is clean but barely more cartoon-like.
- **Nobody listened to the clips;** the pick is from measurements. Listen to the sensitive line before a demo.
- **Polish:** unchanged, no Polish Wit voice. `VoiceProfile.locales` can take a `pl` voice when one exists.
