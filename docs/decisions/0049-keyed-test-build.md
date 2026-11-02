# 0049. A keyed test build, opt-in and private

- Status: accepted
- Date: 2026-11-02

## Context
- ADR-0010 keeps keys out of every APK: they are pushed with adb as `secrets.json`. A tester without adb and the owner's `.env` gets only the offline lines.
- The owner asked for an APK a friend can install and test fully, with the keys inside.
- Meta's authentication docs advise a unique key per app and keeping keys out of client code, because client code is visible to its users.

## Decision
- `tools/device/build.sh --embed-secrets` writes `META_API_KEY`, `WIT_AI_CLIENT_TOKEN` and `WIT_AI_APP_ID` from `ENV_FILE` (default `.env`) to a temporary file, deleted when the script exits. `CharacterSceneProcessor` copies it into `JollyBootstrap.embeddedSecretsJson` in the processed copy of the scene. The saved `Main.unity` stays empty, and the Wit server token is never included.
- The default output is `Builds/Android/Nubi-keyed.apk`, so it cannot be mistaken for the normal build (`*.apk` is git-ignored).
- On the device a pushed `secrets.json` wins over the embedded JSON, so a revoked key can be replaced without a rebuild.

## Consequences
- Anyone holding a keyed APK can extract the key and spend on it. The app's `hardCapUsd` ($20 per install) limits the app, not someone using the extracted key; only deleting the key in the Model API dashboard stops that.
- Keyed APKs are for named, trusted testers only. Never upload one, and never submit one to the hackathon. Build them with a dedicated key (`ENV_FILE=...`) where possible.
- Unity's git-ignored build caches under `JollyXR/Library` can hold the embedded values.
- Everything else stays as in ADR-0010: without the flag the field is empty.

## Alternatives
- **A small proxy holding the key.** The right design for a public release, as Meta's docs advise, but it needs hosting and is out of scope for testing before Nov 4.
- **Tester pushes `secrets.json`.** Hands over the same key, and needs adb.
