# ASR fixtures

Server frames of Muse Voice Transcribe, copied from the live runs in `docs/research/meta-model-api.md` §5 (2026-11-01).

- `realtime_*.txt`: `wss://api.meta.ai/v1/asr/realtime` text frames, one JSON frame per line. Frames the research log
  elided (`…`) are left out, not invented. Error session ids are short placeholders, because the log truncated them.
- `transcribe_response.json`: the `POST /v1/asr/transcribe` JSON body (§5.1).
