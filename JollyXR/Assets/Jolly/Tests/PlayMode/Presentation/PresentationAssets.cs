using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.Presentation
{
    /// <summary>Loads the shipped prefabs and assets, so the tests also cover their wiring. Editor-only (CI runs PlayMode in the Editor).</summary>
    static class PresentationAssets
    {
        public const string PrefabFolder = "Assets/Jolly/Runtime/Presentation/Prefabs/";
        public const string LocaleVoiceMap = "Assets/Jolly/Content/Config/LocaleVoiceMap.asset";

        public static T Instantiate<T>(string prefabName) where T : Component
        {
            var prefab = Load<GameObject>(PrefabFolder + prefabName + ".prefab");
            // The canvases LazyFollow Camera.main (the XR Origin camera on device), resolved in OnEnable.
            var head = new GameObject("Head", typeof(Camera)) { tag = "MainCamera" };
            var instance = Object.Instantiate(prefab);
            head.transform.SetParent(instance.transform, false);
            var component = instance.GetComponentInChildren<T>(true);
            Assert.That(component != null, Is.True, $"{prefabName} has a {typeof(T).Name}");
            return component;
        }

        /// <summary>
        /// Resumes after MonoBehaviour.Update has run at least once more. Plain NextFrame is not enough: UniTask's Update
        /// runner sits before ScriptRunBehaviourUpdate, so it resumes before the views' Update of that frame.
        /// </summary>
        public static UniTask AfterUpdate() => UniTask.NextFrame(PlayerLoopTiming.PostLateUpdate);

        public static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset != null, Is.True, path);
            return asset;
#else
            Assert.Ignore("Presentation tests load assets through the AssetDatabase (Editor only).");
            return null;
#endif
        }
    }
}
