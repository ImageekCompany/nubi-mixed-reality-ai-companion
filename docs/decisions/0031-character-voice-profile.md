# 0031. Character voice as data: `VoiceProfile`, persona delivery, Nubi speaks as Wit "Rubie"

- Status: accepted
- Date: 2026-11-01

## Context
- The user asked for Nubi to sound "more like a cartoon character, but still suitable for normal conversation". Nubi also speaks as Mentor, Guru and Listener, which includes sensitive conversations, so a squeaky gimmick voice is not acceptable. Polish should work later.
- Until now the Wit voice was spread over three places: each persona asset held absolute per-language voices (all `Charlie`), `LocaleVoiceMap` held a fallback voice per locale, and `CannedLineBaker` hard-coded `Charlie`. Changing the voice meant editing all of them, and a second character (the parallel character-swap work, ADR-0026 there) could not have its own voice.
- Live checks with our Wit app (2026-11-01):
  - `GET /voices` lists 30 voices in `en_US`, `en_CA` and `en_GB`, none Polish. Every voice lists `style`, `pitch`, `speed` and `sfx` as supported features; `gain` is not listed.
  - `gain` in the `/synthesize` body is accepted but ignored: the same text with `gain` 25, `gain` 200, no `gain`, or an unknown key gave byte-identical or within-variance PCM.
  - Effects exist only as SSML `<speak><sfx character="…" environment="…">…</sfx></speak>` (Voice SDK sample `TTSSpeakerEffectSelect`: characters chipmunk, monster, daemon, robot, alien; environments reverb, room, cathedral, radio, phone, pa, helmet). A 240-character sentence wrapped in SSML (~290 characters) and XML entities (`&amp;`, `&lt;`) were accepted.
- The audition (`docs/research/voice.md` §11) compared 14 configurations, then re-checked six on fresh lines for latency, pitch, clarity, artefacts and machine-transcription errors.

## Decision
- **`VoiceProfile` (ScriptableObject, `Jolly.Character`)** holds a character's voice: `defaultVoice {voiceId, style, speed, pitch, sfxCharacter, sfxEnvironment}` and `locales[]` that replace the whole voice for a language tag (exact tag first, then the primary language: `pl` matches `pl-PL`). It implements the new contract `ICharacterVoice.VoiceFor(languageTag)`. There is no gain field, because Wit ignores it.
- **`CharacterDefinition.voice`** (ADR-0026) owns the profile; Nubi's is `Content/Character/Nubi/NubiVoice.asset`. The composition root passes the selected character's profile to `PersonaRepository`, so persona voices, the launch Wit warm-up and live speech all follow the character. The field is required: `IsUsable` refuses a definition without a voice ("no voice", so selection falls back to the default character) and `CharacterDefinitionValidator` fails one without a voice or without an English Wit voice. There is no `JollyConfig` fallback.
  - Amended after the merge with character swapping: the first version put the profile on `JollyConfig.characterVoice` because `CharacterDefinition` did not exist yet on this branch. A config-level fallback was dropped: it could only make a swapped character speak in another character's voice, which the selection fallback already avoids by using the default character as a whole.
- **Personas no longer pick a voice.** `PersonaProfileAsset` has `voiceStyle` (empty keeps the character's), `voiceSpeedPercent` and `voicePitchPercent`, mapped to `VoiceDelivery` (Core), which scales the character's voice and clamps to Wit's ranges. The old absolute numbers carried over as percentages: Companion and Mentor 100/100, Guru speed 90 % and pitch 92 % (Rubie: speed 90, pitch 101), Listener speed 92 %.
- `LocaleVoiceMap` keeps only ASR bias, reply language and `ttsAvailable`; `LocaleVoiceProfile.TtsVoice` is removed. Whether a locale can be spoken is a property of the Wit app, which voice speaks it is a property of the character.
- **`WitProtocol.SynthesizeBody`** sends `q`, `voice`, `style` (only when set), `speed` and `pitch`. With an sfx effect it XML-escapes the text and wraps it in SSML; without one the text is sent as is. Each case has an EditMode test.
- **Nubi's voice is the audition winner: `{"voice":"Rubie","speed":100,"pitch":110}`**, default style, no sfx. It was the only candidate with no transcription errors on every run, had the lowest noise (spectral flatness), is ~1.5× Charlie's pitch (245–255 Hz vs 163–174 Hz) with a wider range (9.2–12 semitones), and added no latency (first byte 252 ms vs 267 ms). Its pitch barely moves on the sensitive line, which suits Listener.
- **The canned lines are re-baked** by `CannedLineBaker`, which reads the English voice of a definition: the shared library in the default character's voice, each character's named lines in its own. A character whose voice differs from the default character's gets every canned line baked into its own `lineClips`, so offline lines keep matching live speech.
- **Lipsync:** `VoiceLipSyncTests` (PlayMode, editor) runs every baked line through the uLipSync profile `Main.unity` uses, the way uLipSync's BakedData editor does, at the output sample rate, and requires that no phoneme takes more than half the voiced frames and that each of A, I, U, E and O is the main phoneme in more than 5 % of them.
  - Result with the scene's profile (still uLipSync's stock `uLipSync-Profile-Sample`; it was never calibrated on Charlie either), 48 kHz, ~4,950 voiced frames of the 31 lines:

    | Voice | A | E | I | O | U | S |
    |---|---|---|---|---|---|---|
    | Charlie (old clips) | 0.282 | 0.154 | **0.038** | 0.176 | 0.173 | 0.177 |
    | Rubie, pitch 110 | 0.285 | 0.135 | 0.107 | 0.189 | 0.131 | 0.152 |

  - The brighter voice does not collapse the profile; it spreads better than Charlie did (I rises from 3.8 % to 10.7 %, the old clips would fail the test). **No retune:** the stock profile stays. The test guards the next voice change.

## Consequences
- A new character gets its own voice by adding one asset; personas and baked lines follow it.
- Speech sounds brighter and younger. Nobody has listened to the clips yet (the audition ran on numbers); listen to the sensitive line before the demo.
- Polish is still captions plus baked lines: no Polish Wit voice exists for our app. When one does, add a `pl` entry to `NubiVoice.locales`, set `LocaleVoiceMap` `pl.ttsAvailable` and widen `WitTextToSpeech.Supports`.
- The character is chosen once at launch, before `PersonaRepository` is built with its voice; a live character switch would have to re-resolve the persona voice (`PersonaRepository` takes one `ICharacterVoice` at construction).
- The self-test's user-utterance clips (`Content/SelfTest/*.wav`) stay in Charlie: they play the user, not Nubi.

## Alternatives
- **Runner-up: Cartoon Kid, `style:"soft"`, speed 128.** The most cartoon-like voice that still sounds like speech, but monotone (4.8–7.8 semitones), the only real recognition error ("sourdough" → "Soto"), and the most noise on the long line.
- **Charlie, pitch 120, speed 105:** the smallest change and clean, but barely more cartoon-like (+2.5–3.5 semitones).
- **Whimsical:** breathiest (4× Charlie's flatness), slow (2.6 words/s) and squeaky on casual lines (376 Hz).
- **Charlie + chipmunk sfx:** 2–3× Charlie's noise, which fits a processing artefact.
- **Keeping voices on personas:** every persona would have to be edited per character, and a character swap would not change the voice.
