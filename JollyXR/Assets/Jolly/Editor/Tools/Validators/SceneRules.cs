using System.Collections.Generic;
using Jolly.App;
using Jolly.App.SelfTest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jolly.Editor.Tools.Validators
{
    /// <summary>
    /// Main.unity: one JollyBootstrap whose config, self-test assets and every SceneRefs field are bound, and an XR rig
    /// whose input modality manager has hand objects, with pinch push-to-talk gated by those hands' interactors.
    /// </summary>
    public static class SceneRules
    {
        public static void Check(string scenePath, List<string> problems)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            var opened = !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                JollyBootstrap bootstrap = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent<JollyBootstrap>(out var found))
                        bootstrap = found;
                }
                if (bootstrap == null)
                {
                    problems.Add(scenePath + " has no root JollyBootstrap");
                    return;
                }
                CheckBindings(bootstrap, problems);
                CheckHandRig(new SerializedObject(bootstrap), problems);
                ConfigRules.CheckSelfTest(
                    new SerializedObject(bootstrap).FindProperty("selfTestAssets").objectReferenceValue as SelfTestAssets,
                    problems);
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void CheckBindings(JollyBootstrap bootstrap, List<string> problems)
        {
            var so = new SerializedObject(bootstrap);
            if (so.FindProperty("config").objectReferenceValue == null)
                problems.Add("JollyBootstrap.config is not set");
            var scene = so.FindProperty("scene");
            var end = scene.GetEndProperty();
            var p = scene.Copy();
            for (var enter = true; p.NextVisible(enter) && !SerializedProperty.EqualContents(p, end); enter = false)
            {
                // lipSync and controllerAim are optional (amplitude lip-sync, hands only).
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null
                    && p.name != "lipSync" && p.name != "controllerAim")
                    problems.Add("JollyBootstrap.scene." + p.name + " is not bound");
            }
        }

        // XRInputModalityManager deactivates a side's controller objects while that hand is tracked. With no hand
        // objects bound the user sees no hands and has no hand interactors (the Starter Assets rig; ADR-0030).
        static void CheckHandRig(SerializedObject bootstrap, List<string> problems)
        {
            var origin = bootstrap.FindProperty("scene.origin").objectReferenceValue as Component;
            var pinch = bootstrap.FindProperty("scene.pushToTalk").objectReferenceValue as Component;
            if (origin == null || pinch == null)
                return;
            var modality = origin.GetComponent("XRInputModalityManager");
            if (modality == null)
            {
                problems.Add("The XR Origin has no XRInputModalityManager");
                return;
            }
            var modes = new SerializedObject(modality);
            var rays = new SerializedObject(pinch);
            CheckHand(modes.FindProperty("m_LeftHand"), rays.FindProperty("leftHandRay"), "left", problems);
            CheckHand(modes.FindProperty("m_RightHand"), rays.FindProperty("rightHandRay"), "right", problems);
        }

        static void CheckHand(SerializedProperty hand, SerializedProperty ray, string side, List<string> problems)
        {
            var handObject = hand.objectReferenceValue as GameObject;
            if (handObject == null)
            {
                problems.Add("XRInputModalityManager has no " + side + " hand object");
                return;
            }
            var interactor = ray.objectReferenceValue as Component;
            if (interactor == null || !interactor.transform.IsChildOf(handObject.transform))
                problems.Add("PinchPushToTalk." + ray.name + " is not an interactor under the " + side + " hand object");
        }
    }
}
