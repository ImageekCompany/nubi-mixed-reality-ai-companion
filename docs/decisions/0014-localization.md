# 0014. Localization

- Status: accepted
- Date: 2026-11-01

## Context
Requirement 18 says English first, with Polish possible, covering strings, STT and TTS. Muse ASR Polish was verified live. The Wit app exposes no Polish voices (live `/voices`, 2026-11-01).

## Decision
- **Unity Localization 1.5.13:**
  - String tables `UI`, `Lines`, `Personas` and `Crisis` (crisis and stop keywords).
  - Asset table `LineClips`.
  - Locales `en` (default) and `pl`.
- `LocaleVoiceMapAsset` maps each locale to a `LocaleVoiceProfile` (ASR `languageBias`, TTS voice, `TtsAvailable`, reply language).
- `ILocalizer` wraps `LocalizationSettings.SelectedLocaleChanged`, awaits `StringDatabase.PreloadTables` at startup and on locale change (via `UniTask.Addressables`; `Jolly.Localization` references `UniTask.Addressables` and `Unity.ResourceManager`), then serves `Get` with the synchronous `GetLocalizedString` (verified, `LocalizedStringDatabase.cs:146`).
- Tables are Addressables content: the Addressables settings use `BuildWithPlayer`, and `QuestBuild.BuildApk` calls `AddressableAssetSettings.BuildPlayerContent()` first, because the default `PreferencesValue` depends on per-machine EditorPrefs (verified, Addressables 2.11.2).
- One string table per owning work package: `UI`, `Lines` (+ asset table `LineClips`), `Personas`, `Crisis`.
- Model-facing tool schemas stay in English. The persona `instructions` get "Reply in {language}."
- Polish TTS: captions only for live replies, and Piper-baked CC0 clips for canned lines.

## Consequences
- Adding a locale means adding a table column plus a map entry. No code changes.
- Polish is *could* tier for the demo.

## Alternatives
- A custom JSON i18n: rejected by requirement 19 (prefer default Unity solutions).
