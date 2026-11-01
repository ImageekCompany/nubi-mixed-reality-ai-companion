# 0005. Speech to text: Muse Voice Transcribe realtime, pinch push-to-talk

- Status: accepted (pending IL2CPP WebSocket spike R4)
- Date: 2026-11-01

## Context
- `wss://api.meta.ai/v1/asr/realtime` was verified live: auth goes in the first frame, input is PCM s16le at 16 or 24 kHz, the final arrives about 145 ms after `endStream`, and Polish works (`meta-model-api.md` §5).
- Keyword biasing duplicated "Jolly", so ASR biasing must not be used as a wake word (C5).
- Quest AEC is UNVERIFIED (B4).

## Decision
- `MuseRealtimeSpeechToText` uses `System.Net.WebSockets.ClientWebSocket` with `mode:"PUSH_TO_TALK"`, `PCM_24KHZ` and `partialMode:"CUMULATIVE"`.
  - It connects on pinch press and buffers mic audio during the handshake.
  - It paces 80 ms frames in real time.
  - On release it sends `{"type":"endStream"}` and reads until close 1000.
- **Activation:** hold a pinch on either hand. Pinching while Nubi is speaking is barge-in. Voice barge-in and a wake word are *could* tier.
- **IL2CPP fallback (B2):** `POST /v1/asr/transcribe` with `Accept: text/event-stream` on PTT release, sending the buffered WAV. It costs latency but is verified.

## Consequences
- Turn-taking is deterministic and needs no VAD on the main path. It is accessible one-handed.
- ASR costs $0.18/h, which is negligible.
- `languageBias` comes from `LocaleVoiceProfile`.

## Alternatives
- **Wit STT:** a second Meta service, but weaker Polish and less "Meta AI tooling" credit.
- **ENDPOINTING mode (hands-free):** kept as a config option for *could* tier.
- **sherpa-onnx offline:** roadmap for an offline Psychologist.
