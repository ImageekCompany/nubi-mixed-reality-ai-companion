# 0010. Secrets provisioning: no keys in the build

- Status: accepted
- Date: 2026-11-01

## Context
Requirement 17 says secrets stay local-only. ToS §3.1 forbids sharing the key, and a key embedded in an APK can be extracted (critic B8). No ephemeral token mechanism is documented.

## Decision
- `ISecretsProvider`:
  - **Editor:** `DotEnvSecretsProvider(<repo>/.env)`.
  - **Device:** `JsonFileSecretsProvider(Application.persistentDataPath + "/secrets.json")`.
- Device keys: `META_API_KEY` and `WIT_AI_CLIENT_TOKEN`. Pushed by the developer with:
  ```
  adb push secrets.json /sdcard/Android/data/com.nubi.companion/files/secrets.json
  ```
  Here `persistentDataPath` = `/storage/emulated/0/Android/data/com.nubi.companion/files`.
- Nothing secret is ever in assets, scenes, `StreamingAssets`, ScriptableObjects or logs. `RedactingLog` masks key patterns as defence in depth.
- A missing key means the app runs in offline mode with a visible "API key missing" status.
- The README documents a server-side proxy as the production path.

## Consequences
- The judges' build needs the key pushed separately. The README gives the exact command and a `tools/push-secrets.sh`.
- The existing `.gitignore` already excludes `.env` and `StreamingAssets/secrets*`.

## Alternatives
- **Key in the APK:** extractable and against ToS.
- **An in-headset entry panel:** *could* tier.
- **A proxy server:** correct for production, too much for the hackathon.
