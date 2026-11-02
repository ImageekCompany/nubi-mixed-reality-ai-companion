#if UNITY_EDITOR
using System.Collections;
using Jolly.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Character
{
    public sealed class ShellFurLodTests
    {
        [TestCase(0.5f, -1, 0)]
        [TestCase(2f, -1, 1)]
        [TestCase(4f, -1, 2)]
        [TestCase(1.7f, 0, 0)]
        [TestCase(1.8f, 0, 1)]
        [TestCase(3.5f, 0, 2)]
        [TestCase(1.5f, 1, 1)]
        [TestCase(1.4f, 1, 0)]
        [TestCase(3.1f, 2, 2)]
        [TestCase(2.9f, 2, 1)]
        [TestCase(0.2f, 2, 0)]
        public void Select_ChangesLevelOnlyPastTheBand(float distance, int current, int expected)
        {
            Assert.That(ShellFurLod.Select(distance, current, 1.6f, 3.2f, 0.15f), Is.EqualTo(expected));
        }

        [Test]
        public void Height_SpreadsShellsUpToTheTips()
        {
            Assert.That(ShellFurLod.Height(0, 4), Is.EqualTo(0.25f));
            Assert.That(ShellFurLod.Height(3, 4), Is.EqualTo(1f));
        }
    }

    public sealed class FurInertiaTests
    {
        [Test]
        public void Step_FirstCallOnlyPlacesTheBody()
        {
            var inertia = new FurInertia(2.5f, 0.45f, 0.03f);

            Assert.That(inertia.Step(new Vector3(5f, 0f, 0f), 0.1f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Step_TipsTrailAMoveThenSettleBack()
        {
            var inertia = new FurInertia(2.5f, 0.45f, 0.03f);
            inertia.Step(Vector3.zero, 0f);

            var lag = inertia.Step(new Vector3(0.01f, 0f, 0f), 1f / 72f);
            for (var i = 0; i < 72 * 4; i++)
                inertia.Step(new Vector3(0.01f, 0f, 0f), 1f / 72f);

            Assert.That(lag.x, Is.LessThan(0f));
            Assert.That(inertia.Offset.magnitude, Is.LessThan(1e-4f));
        }

        [Test]
        public void Step_ATeleportIsClampedToTheLongestLag()
        {
            var inertia = new FurInertia(2.5f, 0.45f, 0.03f);
            inertia.Step(Vector3.zero, 0f);

            var lag = inertia.Step(new Vector3(0f, 0f, 3f), 1f / 72f);

            Assert.That(lag.magnitude, Is.EqualTo(0.03f).Within(1e-5f));
            Assert.That(lag.z, Is.LessThan(0f));
        }

        [Test]
        public void Step_ALongHitchStaysStable()
        {
            var inertia = new FurInertia(2.5f, 0.45f, 0.03f);
            inertia.Step(Vector3.zero, 0f);
            inertia.Step(new Vector3(0.02f, 0f, 0f), 1f / 72f);

            inertia.Step(new Vector3(0.02f, 0f, 0f), 2f);

            Assert.That(inertia.Offset.magnitude, Is.LessThan(1e-3f));
        }
    }

    /// <summary>Shell materials on a plain two-submesh MeshRenderer: the same path a SkinnedMeshRenderer takes.</summary>
    public sealed class ShellFurTests
    {
        const string ShaderPath = "Assets/Jolly/Content/Rendering/Shaders/ShellFur.shader";

        GameObject _go;
        MeshRenderer _renderer;
        Mesh _mesh;
        Material _fur;
        Material _other;

        [SetUp]
        public void SetUp()
        {
            _fur = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath));
            _fur.SetFloat("_ShellCount", 4);
            _fur.SetFloat("_ShellCountMid", 2);
            _fur.SetFloat("_ShellCountFar", 0);
            _fur.SetFloat("_ShellDistanceMid", 1.6f);
            _fur.SetFloat("_ShellDistanceFar", 3.2f);
            _other = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one } };
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            _mesh.SetTriangles(new[] { 1, 3, 2 }, 1);
            _go = new GameObject("Furry");
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _go.AddComponent<MeshRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_mesh);
            Object.DestroyImmediate(_fur);
            Object.DestroyImmediate(_other);
        }

        [Test]
        public void TryAttach_FurOnTheLastSubmesh_AddsTheNearShellsOutermostFirst()
        {
            _renderer.sharedMaterials = new[] { _other, _fur };

            Assert.That(ShellFur.TryAttach(_renderer, out var fur, out _), Is.True);
            fur.Tick(_go.transform.position + Vector3.forward * 0.5f, 0f);

            var materials = _renderer.sharedMaterials;
            Assert.That(fur.Level, Is.Zero);
            Assert.That(fur.ShellCount, Is.EqualTo(4));
            Assert.That(materials, Has.Length.EqualTo(6));
            Assert.That(materials[0], Is.SameAs(_other));
            Assert.That(materials[1], Is.SameAs(_fur));
            Assert.That(materials[2].GetFloat("_ShellHeight"), Is.EqualTo(1f));
            Assert.That(materials[5].GetFloat("_ShellHeight"), Is.EqualTo(0.25f));
            Assert.That(materials[2].IsKeywordEnabled(ShellFur.ShellKeyword), Is.True);
            Assert.That(materials[2].renderQueue, Is.LessThan(materials[5].renderQueue));
            Assert.That(materials[2].renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.AlphaTest));
            fur.Dispose();
        }

        [Test]
        public void Tick_FartherAway_DrawsFewerShells()
        {
            _renderer.sharedMaterials = new[] { _other, _fur };
            ShellFur.TryAttach(_renderer, out var fur, out _);

            fur.Tick(_go.transform.position + Vector3.forward * 2f, 0f);
            Assert.That(_renderer.sharedMaterials, Has.Length.EqualTo(4));
            fur.Tick(_go.transform.position + Vector3.forward * 5f, 0f);
            Assert.That(_renderer.sharedMaterials, Has.Length.EqualTo(2));
            Assert.That(fur.ShellCount, Is.Zero);
            fur.Dispose();
        }

        [UnityTest]
        public IEnumerator Dispose_RestoresTheRendererAndDestroysTheShells()
        {
            _renderer.sharedMaterials = new[] { _other, _fur };
            ShellFur.TryAttach(_renderer, out var fur, out _);
            fur.Tick(_go.transform.position, 0f);
            var shell = _renderer.sharedMaterials[2];

            fur.Dispose();
            yield return null;

            Assert.That(_renderer.sharedMaterials, Is.EqualTo(new[] { _other, _fur }));
            Assert.That(shell == null, Is.True);
        }

        [Test]
        public void TryAttach_FurNotOnTheLastSubmesh_Refuses()
        {
            _renderer.sharedMaterials = new[] { _fur, _other };

            Assert.That(ShellFur.TryAttach(_renderer, out var fur, out var reason), Is.False);
            Assert.That(fur, Is.Null);
            Assert.That(reason, Does.Contain("slot 0"));
        }

        [Test]
        public void TryAttach_NoFur_IsNotAnError()
        {
            _renderer.sharedMaterials = new[] { _other, _other };

            Assert.That(ShellFur.TryAttach(_renderer, out _, out var reason), Is.False);
            Assert.That(reason, Is.Null);
        }
    }
}
#endif
