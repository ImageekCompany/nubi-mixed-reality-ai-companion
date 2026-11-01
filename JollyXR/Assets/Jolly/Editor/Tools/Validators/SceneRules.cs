using System.Collections.Generic;
using Jolly.App;
using Jolly.App.SelfTest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Jolly.Editor.Tools.Validators
{
    /// <summary>Main.unity: one JollyBootstrap whose config, self-test assets and every SceneRefs field are bound.</summary>
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
    }
}
