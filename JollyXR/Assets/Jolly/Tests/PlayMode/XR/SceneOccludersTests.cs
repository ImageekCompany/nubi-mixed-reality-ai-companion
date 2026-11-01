#if UNITY_EDITOR
using System.Collections;
using Jolly.XR.Occlusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class SceneOccludersTests
    {
        const string OccluderMaterial = "Assets/Jolly/Content/Rendering/Materials/M_SceneOccluder.mat";
        static readonly TrackableId Table = new(1, 1);

        GameObject _go;
        GameObject _table;
        SceneOccluders _occluders;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("occluders");
            _occluders = _go.AddComponent<SceneOccluders>();
            var so = new SerializedObject(_occluders);
            so.FindProperty("occluderMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(OccluderMaterial);
            so.ApplyModifiedPropertiesWithoutUndo();
            _table = new GameObject("table box");
            _table.transform.SetPositionAndRotation(new Vector3(1f, 0.25f, 2f), Quaternion.Euler(0f, 30f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_go);
            Object.Destroy(_table);
        }

        [Test]
        public void Upsert_FillsTheBoxVolume_AndFollowsTheTrackable()
        {
            _occluders.Upsert(Table, _table.transform, new Vector3(0.8f, 0.5f, 0.6f));

            var renderer = _table.GetComponentInChildren<MeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.transform.parent, Is.SameAs(_table.transform));
            Assert.That(renderer.transform.position, Is.EqualTo(_table.transform.position).Using(Vector3EqualityComparer.Instance));
            Assert.That(renderer.transform.lossyScale, Is.EqualTo(new Vector3(0.8f, 0.5f, 0.6f)).Using(Vector3EqualityComparer.Instance));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void Upsert_Again_ResizesInPlace()
        {
            _occluders.Upsert(Table, _table.transform, Vector3.one);
            _occluders.Upsert(Table, _table.transform, new Vector3(2f, 1f, 1f));

            Assert.That(_occluders.Count, Is.EqualTo(1));
            Assert.That(_table.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(1));
            Assert.That(_table.GetComponentInChildren<MeshRenderer>(true).transform.localScale.x, Is.EqualTo(2f));
        }

        [Test]
        public void Occluders_AreHiddenUntilMadeVisible_IncludingLaterOnes()
        {
            _occluders.Upsert(Table, _table.transform, Vector3.one);
            Assert.That(_table.GetComponentInChildren<MeshRenderer>(true).enabled, Is.False);

            _occluders.Visible = true;
            var couch = new GameObject("couch");
            try
            {
                _occluders.Upsert(new TrackableId(2, 2), couch.transform, Vector3.one);

                Assert.That(_table.GetComponentInChildren<MeshRenderer>(true).enabled, Is.True);
                Assert.That(couch.GetComponentInChildren<MeshRenderer>(true).enabled, Is.True);
            }
            finally
            {
                Object.Destroy(couch);
            }
        }

        [UnityTest]
        public IEnumerator Remove_DestroysTheOccluder()
        {
            _occluders.Upsert(Table, _table.transform, Vector3.one);

            _occluders.Remove(Table);
            yield return null;

            Assert.That(_occluders.Count, Is.Zero);
            Assert.That(_table.GetComponentInChildren<MeshRenderer>(true), Is.Null);
        }

        [Test]
        public void UnitCube_IsAClosedMetreCubeAroundTheOrigin()
        {
            var cube = UnitCube.Create();
            try
            {
                Assert.That(cube.vertexCount, Is.EqualTo(8));
                Assert.That(cube.triangles, Has.Length.EqualTo(36));
                Assert.That(cube.bounds.center, Is.EqualTo(Vector3.zero));
                Assert.That(cube.bounds.size, Is.EqualTo(Vector3.one));
                cube.RecalculateNormals();
                // Outward winding: each vertex normal points away from the centre.
                foreach (var (v, n) in Zip(cube.vertices, cube.normals))
                    Assert.That(Vector3.Dot(v, n), Is.GreaterThan(0f), v.ToString());
            }
            finally
            {
                Object.Destroy(cube);
            }
        }

        [TestCase("Jolly/XR/DepthOccluder")]
        public void Shader_CompilesWithoutMessages(string name)
        {
            var shader = Shader.Find(name);

            Assert.That(shader, Is.Not.Null, name);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, name);
            Assert.That(ShaderUtil.GetShaderMessageCount(shader), Is.Zero, name);
            Assert.That(shader.renderQueue, Is.LessThan(2000), "drawn before Nubi's Geometry queue");
        }

        static (Vector3, Vector3)[] Zip(Vector3[] a, Vector3[] b)
        {
            var pairs = new (Vector3, Vector3)[a.Length];
            for (var i = 0; i < a.Length; i++)
                pairs[i] = (a[i], b[i]);
            return pairs;
        }
    }
}
#endif
