using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Architecture
{
    /// <summary>The asmdef reference graph of architecture.md §2. A reference missing from this table is a review failure.</summary>
    public sealed class AssemblyGraphTests
    {
        static readonly string[] UniTask = { "UniTask", "UniTask.Linq" };
        static readonly string[] XrPackages =
        {
            "Unity.XR.ARFoundation", "Unity.XR.ARSubsystems", "Unity.XR.CoreUtils", "Unity.XR.Hands",
            "Unity.XR.Interaction.Toolkit", "Unity.InputSystem", "Unity.AI.Navigation",
        };
        static readonly string[] JollyRuntime =
        {
            "Jolly.Contracts", "Jolly.Core", "Jolly.Net", "Jolly.Providers.MetaApi", "Jolly.Providers.Wit", "Jolly.Audio",
            "Jolly.Integrations.ULipSync", "Jolly.XR", "Jolly.XR.MetaQuest", "Jolly.Character", "Jolly.Localization",
            "Jolly.Presentation",
        };
        static readonly string[] EditModeTests =
        {
            "Jolly.Contracts", "Jolly.Core", "Jolly.Net", "Jolly.Providers.MetaApi", "Jolly.Providers.Wit", "Jolly.Audio",
            "Jolly.Testing", "UniTask", "UniTask.Linq", "UnityEngine.TestRunner", "UnityEditor.TestRunner",
        };

        static readonly Dictionary<string, string[]> Allowed = new()
        {
            ["Jolly.Contracts"] = new[] { "UniTask" },
            ["Jolly.Core"] = With("Jolly.Contracts", UniTask),
            ["Jolly.Net"] = With("Jolly.Contracts", UniTask),
            ["Jolly.Providers.MetaApi"] = With("Jolly.Contracts", "Jolly.Net", UniTask),
            ["Jolly.Providers.Wit"] = With("Jolly.Contracts", "Jolly.Net", UniTask),
            ["Jolly.Audio"] = With("Jolly.Contracts", UniTask),
            ["Jolly.Integrations.ULipSync"] = new[] { "Jolly.Contracts", "uLipSync.Runtime" },
            ["Jolly.XR"] = With("Jolly.Contracts", UniTask, XrPackages),
            ["Jolly.XR.MetaQuest"] = With("Jolly.Contracts", "Jolly.XR", UniTask,
                "Unity.XR.MetaOpenXR", "Unity.XR.ARFoundation", "Unity.XR.ARSubsystems"),
            ["Jolly.Character"] = With("Jolly.Contracts", UniTask),
            ["Jolly.Localization"] = With("Jolly.Contracts", UniTask,
                "UniTask.Addressables", "Unity.ResourceManager", "Unity.Localization"),
            ["Jolly.Presentation"] = With("Jolly.Contracts", UniTask, "Unity.TextMeshPro", "UnityEngine.UI", "Unity.Localization"),
            ["Jolly.App"] = With(JollyRuntime, UniTask),
            ["Jolly.Editor.Build"] = new[]
            {
                "Unity.XR.Management", "Unity.XR.Management.Editor", "Unity.XR.OpenXR", "Unity.XR.OpenXR.Editor",
                "Unity.XR.OpenXR.Features.MetaQuestSupport", "Unity.XR.MetaOpenXR", "Unity.XR.Hands",
                "Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime",
            },
            ["Jolly.Editor.Character"] = new[] { "Jolly.Character", "Jolly.Contracts" },
            ["Jolly.Editor"] = With("Jolly.App", "Jolly.Core", "Jolly.Contracts", "Jolly.Providers.Wit", UniTask),
            ["Jolly.Testing"] = With("Jolly.Contracts", "Jolly.Core", "Jolly.Net", UniTask),
            ["Jolly.Tests.EditMode"] = EditModeTests,
            ["Jolly.Tests.PlayMode"] = With(EditModeTests, "Jolly.App", "Jolly.Character", "Jolly.Editor.Character", "Jolly.Presentation", "Jolly.Localization", "Jolly.XR",
                XrPackages, "Unity.Localization", "Unity.TextMeshPro", "UnityEngine.UI"),
        };

        static readonly HashSet<string> RuntimeAssemblies = new(JollyRuntime.Append("Jolly.App"));

        public sealed class Asmdef
        {
            public Asmdef(string path)
            {
                Json = JObject.Parse(File.ReadAllText(path));
                Name = (string)Json["name"];
                Directory = Path.GetDirectoryName(path);
            }

            public string Name { get; }
            public string Directory { get; }
            public JObject Json { get; }
            public override string ToString() => Name;
        }

        static readonly Asmdef[] All = System.IO.Directory
            .GetFiles(Path.Combine(Application.dataPath, "Jolly"), "*.asmdef", SearchOption.AllDirectories)
            .Select(path => new Asmdef(path))
            .OrderBy(a => a.Name)
            .ToArray();

        static IEnumerable<Asmdef> RuntimeAsmdefs => All.Where(a => RuntimeAssemblies.Contains(a.Name));
        static IEnumerable<Asmdef> TestAsmdefs => All.Where(a => a.Name.StartsWith("Jolly.Test"));

        [Test]
        public void EveryAssemblyInTheTableExists()
        {
            Assert.That(All.Select(a => a.Name), Is.EquivalentTo(Allowed.Keys));
        }

        [TestCaseSource(nameof(All))]
        public void References_AreOnlyTheAllowedOnes(Asmdef asmdef)
        {
            var references = asmdef.Json["references"]?.Values<string>().ToArray() ?? Array.Empty<string>();

            Assert.That(references, Has.None.StartsWith("GUID:"), "reference asmdefs by name so this test can read them");
            Assert.That(references, Is.SubsetOf(Allowed[asmdef.Name]));
        }

        [TestCaseSource(nameof(All))]
        public void HasCscRspTreatingWarningsAsErrors(Asmdef asmdef)
        {
            var rsp = Path.Combine(asmdef.Directory, "csc.rsp");

            Assert.That(File.Exists(rsp), Is.True, rsp);
            Assert.That(File.ReadAllText(rsp), Does.Contain("-warnaserror+"));
        }

        [TestCaseSource(nameof(RuntimeAsmdefs))]
        public void RuntimeAssemblies_AreNotAutoReferenced(Asmdef asmdef)
        {
            Assert.That((bool?)asmdef.Json["autoReferenced"], Is.False);
        }

        [TestCaseSource(nameof(TestAsmdefs))]
        public void TestAssemblies_CompileOnlyWithTestsIncluded(Asmdef asmdef)
        {
            Assert.That(asmdef.Json["defineConstraints"]?.Values<string>(), Does.Contain("UNITY_INCLUDE_TESTS"));
        }

        static string[] With(params object[] parts) =>
            parts.SelectMany(p => p is string s ? new[] { s } : (IEnumerable<string>)p).Distinct().ToArray();
    }
}
