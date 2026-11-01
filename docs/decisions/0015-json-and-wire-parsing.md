# 0015. JSON and wire parsing

- Status: accepted
- Date: 2026-11-01

## Context
SSE events are polymorphic (`type` field). `JsonUtility` cannot read them. IL2CPP strips reflection-only types.

## Decision
- `com.unity.nuget.newtonsoft-json` 3.2.2, which is editor-bundled and marked `mustBeBundled`.
- Hot paths (SSE events, ASR events, SAM output) use `JsonTextReader` / `JObject.Parse` on a single event, with no reflection-based deserialization.
- Request bodies are written with `JsonTextWriter` into a reused `StringBuilder`.
- Cold paths (memory, usage and secrets files) use the same reader/writer style over Core persistence DTOs with public float fields, mapped by hand. `JsonConvert` is not used on contract types: `Vector3`/`Pose` fail with "Self referencing loop detected for property 'normalized'" (the package ships no Unity converters), and `private set` properties deserialize silently as defaults. Each store has a round-trip EditMode test. Any reflection-reached type is listed in a `link.xml` beside its asmdef.

## Consequences
- Parsers are deterministic and fixture-testable.
- One JSON library everywhere.

## Alternatives
- **`JsonUtility`:** no polymorphism and no dictionaries.
- **`System.Text.Json`:** not shipped with Unity.
