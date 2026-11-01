using System;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Localization;
using Jolly.XR.Occlusion;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jolly.App
{
    /// <summary>
    /// Every tunable of the app in one asset (architecture §3, §11): model ids, budget, timeouts and content references.
    /// Holds no secrets. Model ids live here and never in code (ADR-0004).
    /// </summary>
    [CreateAssetMenu(menuName = "Jolly/Config", fileName = "JollyConfig")]
    public sealed class JollyConfig : ScriptableObject
    {
        [Header("Models (ADR-0004)")]
        public string chatModel = "muse-spark-1.3";
        [Tooltip("Synthetic inputs only (CI smoke tests, dev tools). Never receives user data.")]
        public string syntheticChatModel = "muse-spark-1.3-contributor";
        public string samModel = "sam-3.1";
        public string asrModel = "muse-voice-transcribe-1.0";

        [Header("Budget")]
        [Tooltip("Per-device hard cap in USD, a sub-budget of the project's $140.")]
        public float hardCapUsd = 20f;
        public float chatReservationUsd = 0.02f;
        public ModelPriceEntry[] prices =
        {
            new() { model = "muse-spark-1.3", inputPerMillion = 1.25f, cachedInputPerMillion = 0.15f, outputPerMillion = 4.25f },
            new() { model = "muse-spark-1.3-contributor", inputPerMillion = 0.10f, cachedInputPerMillion = 0.002f, outputPerMillion = 0.20f },
            new() { model = "sam-3.1", perImage = 0.0025f },
            new() { model = "muse-voice-transcribe-1.0", perAudioHour = 0.18f },
        };

        [Header("Timeouts, seconds (architecture §11)")]
        [Tooltip("Live voice turns and vision. Request → first event was 0.3–1.4 s in 52 device turns; 4 s × 2 attempts " +
                 "also leaves the vision retry room inside visionSeconds.")]
        public float voiceFirstEventSeconds = 4f;
        public int voiceAttempts = 2;
        [Tooltip("A model round silent this long gets a canned hold line (\"One moment.\"); 0 turns it off.")]
        public float holdLineSeconds = 2f;
        [Tooltip("Then once more after this long while still waiting (the retry); 0: only once.")]
        public float holdLineRepeatSeconds = 4f;
        public float backgroundFirstEventSeconds = 10f;
        public int backgroundAttempts = 3;
        public float llmIdleSeconds = 10f;
        public float samSeconds = 5f;
        public int samAttempts = 2;
        public float visionSeconds = 8f;
        public int visionMaxOutputTokens = 1024;
        public float witFirstChunkSeconds = 3f;
        public float witIdleSeconds = 3f;
        public float healthProbeSeconds = 3f;

        [Header("Personas and content")]
        public string defaultPersonaId = "companion";
        public PersonaProfileAsset[] personas = Array.Empty<PersonaProfileAsset>();
        public LocaleVoiceMapAsset localeVoices;
        public LineClipLibrary lineClips;
        [Tooltip("String tables preloaded at startup; ILocalizer.Get is synchronous afterwards.")]
        public string[] preloadTables = { "UI", "Lines", "Personas", "Crisis" };
        [Tooltip("Editor and Link only: the still image served as the camera frame (meta-openxr gives no CPU image there).")]
        public TextAsset editorCameraStill;

        [Header("Character (docs/characters.md)")]
        [Tooltip("Shown unless persistentDataPath/character.txt or build.sh --character names another; also the fallback.")]
        public CharacterDefinition defaultCharacter;
        [Tooltip("Committed characters selectable by id. Local, git-ignored ones are added to the processed scene at " +
                 "build or Play time and are never listed here.")]
        public CharacterDefinition[] characters = Array.Empty<CharacterDefinition>();

        [Header("Rendering")]
        [Tooltip("Pipelines warmed at boot; recorded by a development-player self-test (docs/perf/README.md §7).")]
        public GraphicsStateCollection graphicsStates;
        [Tooltip("How Nubi is hidden behind real objects (ADR-0024).")]
        public OcclusionSettings occlusion = new();

        [Header("Room (ADR-0025)")]
        [Tooltip("Seconds after the scene permission is granted without a floor plane before the room counts as not scanned.")]
        public float roomScanTimeoutSeconds = 8f;
        [Tooltip("Without Space Setup, Nubi walks on a small disc around the user instead of not at all.")]
        public bool fallbackFloor = true;
        public float fallbackFloorRadius = 1.5f;
        [Tooltip("Speak the Space Setup offer once per session when the room is not scanned.")]
        public bool offerRoomScan = true;

        [Header("Behaviour")]
        public bool acknowledgeOnRelease = true;
        [Tooltip("Seconds the OpenXR head pose is predicted ahead of LateUpdate (R1 marker test).")]
        public float headPoseLeadSeconds;
        public LogLevel minimumLogLevel = LogLevel.Info;
    }

    [Serializable]
    public sealed class ModelPriceEntry
    {
        public string model;
        public float inputPerMillion;
        public float cachedInputPerMillion;
        public float outputPerMillion;
        public float perImage;
        public float perAudioHour;
    }
}
