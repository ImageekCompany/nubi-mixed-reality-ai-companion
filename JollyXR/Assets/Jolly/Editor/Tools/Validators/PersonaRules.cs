using System.Collections.Generic;
using Jolly.App;
using Jolly.Core.Tools;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Jolly.Editor.Tools.Validators
{
    /// <summary>
    /// Persona assets (architecture §9): unique ids, the default present, known tool names, every text key filled in
    /// every locale, and the safety rules for a crisis-handoff persona (no stored memory, a safety notice, no camera tools).
    /// </summary>
    public static class PersonaRules
    {
        const string Table = "Personas";
        static readonly string[] KnownTools =
        {
            ToolNames.WalkTo, ToolNames.FollowUser, ToolNames.ComeHere, ToolNames.LookAt, ToolNames.CaptureAndIdentify,
            ToolNames.FindObject, ToolNames.HighlightPart, ToolNames.RememberPlace, ToolNames.Stop, ToolNames.PlayGesture,
            ToolNames.SetPersona, ToolNames.GoToSurface, ToolNames.RequestRoomScan,
        };
        static readonly string[] CameraTools = { ToolNames.CaptureAndIdentify, ToolNames.FindObject, ToolNames.HighlightPart };

        public static void Check(JollyConfig config, List<string> problems)
        {
            var ids = new HashSet<string>();
            var collection = LocalizationEditorSettings.GetStringTableCollection(Table);
            if (collection == null)
                problems.Add("missing string table collection " + Table);
            foreach (var persona in config.personas)
            {
                if (persona == null || string.IsNullOrWhiteSpace(persona.id))
                {
                    problems.Add("JollyConfig.personas has an empty entry or id");
                    continue;
                }
                if (!ids.Add(persona.id))
                    problems.Add($"duplicate persona id '{persona.id}'");
                CheckTools(persona, problems);
                CheckSafety(persona, problems);
                if (collection != null)
                    CheckTexts(persona, collection, problems);
            }
            if (!ids.Contains(config.defaultPersonaId))
                problems.Add($"default persona '{config.defaultPersonaId}' is not in JollyConfig.personas");
        }

        static void CheckTools(PersonaProfileAsset persona, List<string> problems)
        {
            foreach (var tool in persona.allowedTools)
            {
                if (System.Array.IndexOf(KnownTools, tool) < 0)
                    problems.Add($"persona '{persona.id}' allows unknown tool '{tool}'");
            }
        }

        static void CheckSafety(PersonaProfileAsset persona, List<string> problems)
        {
            if (!persona.crisisHandoff)
                return;
            if (persona.persistMemory)
                problems.Add($"crisis-handoff persona '{persona.id}' must not persist memory");
            if (!persona.hasSafetyNotice)
                problems.Add($"crisis-handoff persona '{persona.id}' needs a safety notice");
            if (persona.allowedTools.Length == 0)
                problems.Add($"crisis-handoff persona '{persona.id}' must list its tools (no camera tools)");
            foreach (var tool in CameraTools)
            {
                if (System.Array.IndexOf(persona.allowedTools, tool) >= 0)
                    problems.Add($"crisis-handoff persona '{persona.id}' must not use {tool}");
            }
        }

        static void CheckTexts(PersonaProfileAsset persona, StringTableCollection collection, List<string> problems)
        {
            var keys = new List<string> { persona.NameKey, persona.PromptKey, persona.GreetingKey };
            if (persona.hasSafetyNotice)
                keys.Add(persona.SafetyKey);
            foreach (var table in collection.StringTables)
            {
                foreach (var key in keys)
                {
                    if (string.IsNullOrWhiteSpace(table.GetEntry(key)?.Value))
                        problems.Add($"{table.TableCollectionName}/{table.LocaleIdentifier.Code} has no text for '{key}'");
                }
            }
        }
    }
}
