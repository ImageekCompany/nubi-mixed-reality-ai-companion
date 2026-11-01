#if UNITY_EDITOR
using System;
using System.Collections;
using Jolly.Contracts;
using Jolly.Testing;
using Jolly.XR.Occlusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    /// <summary>
    /// The occlusion components as Main.unity wires them, with no XR loader: no occlusion subsystem, so no depth frame
    /// ever arrives, which is the Editor, Link without depth and a denied USE_SCENE alike.
    /// </summary>
    public sealed class RealWorldOcclusionTests
    {
        const string OccluderMaterial = "Assets/Jolly/Content/Rendering/Materials/M_SceneOccluder.mat";

        GameObject _camera;
        GameObject _xr;
        GameObject _box;
        AROcclusionManager _manager;
        ARShaderOcclusion _shaderOcclusion;
        SceneOccluders _occluders;
        RealWorldOcclusion _occlusion;
        readonly FakeClock _clock = new();
        readonly RecordingLog _log = new();

        [SetUp]
        public void SetUp()
        {
            // Built inactive so the shipped "disabled until USE_SCENE" state holds before any OnEnable runs.
            _camera = new GameObject("camera");
            _camera.SetActive(false);
            _camera.AddComponent<Camera>();
            _manager = _camera.AddComponent<AROcclusionManager>();
            _shaderOcclusion = _camera.AddComponent<ARShaderOcclusion>();
            _manager.enabled = false;
            _shaderOcclusion.enabled = false;
            _camera.SetActive(true);

            _xr = new GameObject("xr");
            _xr.SetActive(false);
            _occluders = _xr.AddComponent<SceneOccluders>();
            _occlusion = _xr.AddComponent<RealWorldOcclusion>();
            Bind(_occluders, ("occluderMaterial", AssetDatabase.LoadAssetAtPath<Material>(OccluderMaterial)));
            Bind(_occlusion, ("occlusionManager", _manager), ("shaderOcclusion", _shaderOcclusion), ("sceneOccluders", _occluders));
            _xr.SetActive(true);

            _box = new GameObject("table box");
            _occluders.Upsert(new TrackableId(1, 1), _box.transform, new Vector3(0.8f, 0.5f, 0.8f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_xr);
            Object.Destroy(_camera);
            Object.Destroy(_box);
        }

        static void Bind(Object target, params (string Field, Object Value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields)
                so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        MeshRenderer Occluder => _box.GetComponentInChildren<MeshRenderer>(true);

        [Test]
        public void DepthPreferred_WithoutDepthFrames_FallsBackToSceneMeshes()
        {
            _occlusion.Construct(_clock, _log, new OcclusionSettings());

            Assert.That(_occlusion.Mode, Is.EqualTo(OcclusionMode.SceneMeshes));
            Assert.That(_shaderOcclusion.enabled, Is.False, "shader occlusion stays off without depth");
            Assert.That(Occluder.enabled, Is.True);
            Assert.That(_log.Contains(LogLevel.Info, "evt=occlusion.mode mode=scene_meshes"), Is.True);
        }

        [Test]
        public void DepthPreferred_HandsTheManagerToThePermissionGate_AndLeavesItDisabled()
        {
            _occlusion.Construct(_clock, _log, new OcclusionSettings());

            Assert.That(_occlusion.DepthManager, Is.SameAs(_manager));
            Assert.That(_manager.enabled, Is.False, "only PermissionGate starts it, after USE_SCENE");
        }

        [TestCase(OcclusionPreference.Off)]
        [TestCase(OcclusionPreference.SceneMeshes)]
        public void WithoutDepthPreference_TheManagerIsNeverStarted(OcclusionPreference preference)
        {
            _occlusion.Construct(_clock, _log, new OcclusionSettings { preference = preference });

            Assert.That(_occlusion.DepthManager, Is.Null);
        }

        [Test]
        public void Off_HidesNothing()
        {
            _occlusion.Construct(_clock, _log, new OcclusionSettings { preference = OcclusionPreference.Off });

            Assert.That(_occlusion.Mode, Is.EqualTo(OcclusionMode.None));
            Assert.That(_shaderOcclusion.enabled, Is.False);
            Assert.That(Occluder.enabled, Is.False);
        }

        [Test]
        public void FallbackDisabled_WithoutDepth_HidesNothing()
        {
            _occlusion.Construct(_clock, _log, new OcclusionSettings { sceneMeshFallback = false });

            Assert.That(_occlusion.Mode, Is.EqualTo(OcclusionMode.None));
            Assert.That(Occluder.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator SceneMeshes_StayOnAcrossFrames_AndCoverBoxesAddedLater()
        {
            _occlusion.Construct(_clock, _log, new OcclusionSettings { preference = OcclusionPreference.SceneMeshes });
            var couch = new GameObject("couch box");
            try
            {
                _occluders.Upsert(new TrackableId(2, 2), couch.transform, new Vector3(2f, 0.9f, 0.9f));
                _clock.Advance(TimeSpan.FromSeconds(1));
                yield return null;

                Assert.That(_occlusion.Mode, Is.EqualTo(OcclusionMode.SceneMeshes));
                Assert.That(couch.GetComponentInChildren<MeshRenderer>().enabled, Is.True);
                Assert.That(_occluders.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.Destroy(couch);
            }
        }

        [Test]
        public void Construct_Twice_Throws()
        {
            _occlusion.Construct(_clock, _log, new OcclusionSettings());

            Assert.That(() => _occlusion.Construct(_clock, _log, new OcclusionSettings()),
                Throws.InvalidOperationException);
        }
    }
}
#endif
