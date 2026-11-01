using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Jolly.Editor.Tools.Validators
{
    /// <summary>
    /// Checks the shipped content against the rules the runtime assumes but cannot check cheaply: config references,
    /// persona safety settings and texts in every locale, baked clips, self-test clip imports, the character rig
    /// contract and the scene wiring.
    /// Menu Jolly/Validate Content; batch <c>-executeMethod Jolly.Editor.Tools.Validators.ContentValidator.RunBatch</c>
    /// (tools/ci/test.sh) exits 1 on any problem.
    /// </summary>
    public static class ContentValidator
    {
        public const string ConfigPath = "Assets/Jolly/Content/Config/JollyConfig.asset";
        public const string ScenePath = "Assets/Jolly/Scenes/Main.unity";

        [MenuItem("Jolly/Validate Content")]
        static void RunFromMenu()
        {
            var problems = Validate();
            foreach (var p in problems)
                Debug.LogError("[ContentValidator] " + p);
            Debug.Log($"[ContentValidator] {problems.Count} problem(s)");
        }

        public static void RunBatch()
        {
            var problems = Validate();
            foreach (var p in problems)
                Debug.LogError("[ContentValidator] " + p);
            Debug.Log($"[ContentValidator] Result={(problems.Count == 0 ? "OK" : "FAIL")} problems={problems.Count}");
            EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
        }

        public static List<string> Validate()
        {
            var problems = new List<string>();
            var config = AssetDatabase.LoadAssetAtPath<App.JollyConfig>(ConfigPath);
            if (config == null)
            {
                problems.Add("missing " + ConfigPath);
                return problems;
            }
            ConfigRules.Check(config, problems);
            PersonaRules.Check(config, problems);
            CharacterRules.Check(config, problems);
            SceneRules.Check(ScenePath, problems);
            return problems;
        }
    }
}
