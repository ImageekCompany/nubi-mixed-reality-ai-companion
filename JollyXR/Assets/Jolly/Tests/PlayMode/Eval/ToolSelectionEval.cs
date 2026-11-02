#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.App;
using Jolly.App.Platform;
using Jolly.Contracts;
using Jolly.Core.Agent;
using Jolly.Core.Config;
using Jolly.Core.Memory;
using Jolly.Core.Personas;
using Jolly.Core.Secrets;
using Jolly.Core.Tools;
using Jolly.Core.Usage;
using Jolly.Localization;
using Jolly.Net.Http;
using Jolly.Net.Retry;
using Jolly.Providers.MetaApi.Chat;
using Jolly.Providers.MetaApi.Common;
using Jolly.Testing;
using Jolly.Testing.Offline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.Eval
{
    /// <summary>
    /// Live tool-selection eval (docs/perf/tool-selection-eval.md): each <see cref="ToolSelectionCase"/> as one text turn
    /// to the Responses API with the app's instructions and tool definitions for the default persona, recording the
    /// tools of the first round. Never part of tools/ci/test.sh; run with tools/eval/tool-selection.sh, which reads
    /// META_API_KEY from the repository .env. About $0.01 per run, metered with the app's price table.
    /// JOLLY_EVAL_EFFORT (minimal | low), JOLLY_EVAL_TAGS (comma-separated <see cref="ToolSelectionCase.Tag"/>s, empty for
    /// all), JOLLY_EVAL_COMMIT and JOLLY_EVAL_OUT (the markdown file) come from the script.
    /// </summary>
    [Explicit("Live Meta API calls; run tools/eval/tool-selection.sh")]
    [Category("LiveEval")]
    public sealed class ToolSelectionEval
    {
        const string ConfigPath = "Assets/Jolly/Content/Config/JollyConfig.asset";

        [UnityTest]
        public IEnumerator EveryCase_FirstRoundTools() => UniTask.ToCoroutine(async () =>
        {
            var ct = CancellationToken.None;
            var effort = (ReasoningEffort)Enum.Parse(typeof(ReasoningEffort), Env("JOLLY_EVAL_EFFORT", "minimal"), true);
            var config = AssetDatabase.LoadAssetAtPath<JollyConfig>(ConfigPath);
            var secrets = new DotEnvSecretsProvider(AppPaths.EditorDotEnv);
            if (!secrets.TryGet(SecretKeys.MetaApiKey, out _))
                Assert.Ignore("META_API_KEY is not in the repository .env");

            var log = new RecordingLog();
            var localizer = new UnityLocalizer(config.localeVoices, config.preloadTables, log);
            await localizer.Initialize(ct);
            await localizer.SetLanguage("en", ct);
            var character = config.defaultCharacter;
            var text = new CharacterNamedLocalizer(localizer,
                string.IsNullOrWhiteSpace(character.displayName) ? character.id : character.displayName, character.nameKey);
            var personas = new PersonaRepository(config.personas.Select(p => p.ToDefinition()).ToArray(),
                config.defaultPersonaId, text, character.voice);
            var preferences = new FakeUserPreferences();
            var registry = Catalog(personas);
            var tools = registry.DefinitionsFor(personas.Current, preferences);
            var namespaces = registry.NamespacesOf(tools);

            var clock = new UnityClock();
            var usage = new ApiUsageMeter(Prices(config),
                Path.Combine(Path.GetTempPath(), "jolly-eval-usage-" + Guid.NewGuid().ToString("N") + ".json"), 1m, clock, log);
            var liveModel = MetaModelResolver.Resolve(ModelRoute.LiveUser, config.chatModel);
            var model = new ResponsesChatModel(new MetaApiEndpoint(secrets),
                new MetaSseClient(new HttpTransport(), new RetryExecutor(clock, new SystemRandom(1), log), clock), clock, usage,
                ResponsesChatOptions.VoiceTurn(liveModel, config.syntheticChatModel, (decimal)config.chatReservationUsd), log);

            var tags = Env("JOLLY_EVAL_TAGS", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var cases = ToolSelectionCase.Select(tags);
            long inputTokens = 0, cachedTokens = 0, outputTokens = 0;
            var report = new ToolSelectionReport(Env("JOLLY_EVAL_COMMIT", "unknown"), effort.ToString().ToLowerInvariant(), liveModel);
            try
            {
                foreach (var c in cases)
                {
                    var scene = new SceneMemory("unused", new FakeSpatialAnchors(), new FakeClock(), new MemoryLimits(), log);
                    if (c.SeenLabel != null)
                        scene.AddObject(c.SeenLabel, new Vector3(0.6f, 0.8f, 1.2f), $"a {c.SeenLabel} on the table");
                    var memory = new ConversationMemory(Path.GetTempPath(), personas, new MemoryLimits(), log);
                    var prompt = new PromptBuilder(text, preferences, memory, scene, null, new AgentSettings());
                    var input = new List<ConversationItem>(2);
                    if (prompt.ContextItem() is { } context)
                        input.Add(context);
                    input.Add(ConversationItem.User(c.Utterance));
                    var request = new ModelRequest
                    {
                        Route = ModelRoute.LiveUser,
                        Instructions = prompt.Instructions(personas.Current,
                            bodyActions: namespaces.Any(n => n.Name == ToolNames.Actions.Namespace)),
                        Input = input,
                        Tools = tools,
                        Namespaces = namespaces,
                        Effort = effort,
                    };

                    var called = new List<string>();
                    var watch = Stopwatch.StartNew();
                    await model.Stream(request, ct).ForEachAsync(e =>
                    {
                        if (e.Kind == ModelEventKind.ToolCall)
                            called.Add(e.ToolCall.Name);
                        else if (e.Kind == ModelEventKind.Completed)
                        {
                            inputTokens += e.Result.Usage.InputTokens;
                            cachedTokens += e.Result.Usage.CachedInputTokens;
                            outputTokens += e.Result.Usage.OutputTokens;
                        }
                    }, ct);
                    report.Add(c, called, watch.ElapsedMilliseconds);
                }
            }
            finally
            {
                var markdown = report.ToMarkdown(usage.SpentUsd) +
                               $"Tokens: input {inputTokens} (cached {cachedTokens}), output {outputTokens}.\n";
                var path = Env("JOLLY_EVAL_OUT", Path.Combine(Application.dataPath, "..", "..", "artifacts",
                    "tool-selection-" + effort.ToString().ToLowerInvariant() + ".md"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, markdown);
                UnityEngine.Debug.Log($"[ToolSelectionEval] {report.Passed}/{report.Total} usd={usage.SpentUsd} in={inputTokens} cached={cachedTokens} out={outputTokens} -> {path}");
                usage.Dispose();
                personas.Dispose();
                localizer.Dispose();
            }
            Assert.That(report.Total, Is.EqualTo(cases.Count));
        });

        /// <summary>The definitions and namespaces of ToolCatalog.Build, in its order. A definition depends only on the tool's type (and
        /// on the persona ids for set_persona), so the adapters it needs to run are left out.</summary>
        static ToolRegistry Catalog(IPersonaProvider personas)
        {
            var locomotion = new LocomotionSettings();
            var registry = new ToolRegistry();
            registry.Register(new WalkToTool(null, null, null, locomotion));
            registry.Register(new FollowUserTool(null, locomotion));
            registry.Register(new ComeHereTool(null, null, null, locomotion));
            registry.Register(new LookAtTool(null, null, null, null, locomotion));
            registry.Register(new CaptureAndIdentifyTool(null, null, null, null, null));
            registry.Register(new FindObjectTool(null, null, null, null));
            registry.Register(new HighlightPartTool(null, null, null));
            registry.Register(new RememberPlaceTool(null, null, null, null, null, null));
            registry.Register(new StopTool(null));
            registry.Register(new PlayGestureTool(null));
            registry.Register(new SetPersonaTool(personas));
            registry.Register(new GoToSurfaceTool(null, null, null, locomotion));
            registry.Register(new RequestRoomScanTool(null, null));
            registry.Register(new SetHomeLocationTool());
            // A character that can play every gesture, so every action is offered, as for Nubi and Jolly.
            CharacterActionCatalog.For(new FakeCharacter(), null, null, locomotion, null).RegisterInto(registry);
            return registry;
        }

        static PriceTable Prices(JollyConfig config) => new(config.prices.ToDictionary(p => p.model,
            p => new ModelPrice((decimal)p.inputPerMillion, (decimal)p.cachedInputPerMillion, (decimal)p.outputPerMillion,
                (decimal)p.perImage, (decimal)p.perAudioHour, (decimal)p.perWebSearch), StringComparer.Ordinal));

        static string Env(string name, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
#endif
