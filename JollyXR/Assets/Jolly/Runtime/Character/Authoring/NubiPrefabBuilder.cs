#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Jolly.Character.Authoring
{
    /// <summary>
    /// Regenerates every derived Nubi asset from Nubi.fbx: materials (remapped into the model import), the Animator
    /// controller and Nubi.prefab. Deterministic, so a rebuild after a Blender change is one command:
    /// <c>Unity -batchmode -quit -projectPath JollyXR -executeMethod Jolly.Character.Authoring.NubiPrefabBuilder.Build</c>.
    /// </summary>
    public static class NubiPrefabBuilder
    {
        [MenuItem("Jolly/Character/Rebuild Nubi Assets")]
        public static void Build()
        {
            var skin = UpsertMaterial(NubiAssetPaths.SkinMaterial, NubiAssetPaths.SkinShader, m =>
            {
                m.SetTexture("_BaseMap", Load<Texture2D>(NubiAssetPaths.Albedo));
                m.SetFloat("_RimStrength", 0.3f);
                m.SetFloat("_SpecStrength", 0.06f);
                m.SetFloat("_SpecPower", 16f);
            });
            var gloss = UpsertMaterial(NubiAssetPaths.GlossMaterial, NubiAssetPaths.SkinShader, m =>
            {
                m.SetTexture("_BaseMap", Load<Texture2D>(NubiAssetPaths.Albedo));
                m.SetFloat("_RimStrength", 0f);
                m.SetFloat("_SpecStrength", 0.7f);
                m.SetFloat("_SpecPower", 96f);
            });
            var blob = UpsertMaterial(NubiAssetPaths.BlobShadowMaterial, NubiAssetPaths.BlobShadowShader, _ => { });
            RemapModelMaterials(("M_Nubi_Skin", skin), ("M_Nubi_Gloss", gloss));

            var model = Load<GameObject>(NubiAssetPaths.Model);
            var settings = new NubiCharacterSettings();
            var controller = NubiAnimatorBuilder.Build(model, LoadClips(), settings.locomotion);
            SavePrefab(model, controller, blob);
            AssetDatabase.SaveAssets();
        }

        static void SavePrefab(GameObject model, RuntimeAnimatorController controller, Material blobShadow)
        {
            var root = new GameObject("Nubi");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Model";
                var animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var face = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                face.shadowCastingMode = ShadowCastingMode.Off;
                face.receiveShadows = false;
                face.skinnedMotionVectors = false;

                var agent = AddAgent(root);
                AddCollider(root);
                AddBlobShadow(root, blobShadow);
                Bind(root.AddComponent<NubiCharacterController>(), animator, agent, face, instance.transform);
                PrefabUtility.SaveAsPrefabAsset(root, NubiAssetPaths.Prefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Disabled until a floor NavMesh exists: AgentLocomotion enables it on the first move.</summary>
        static NavMeshAgent AddAgent(GameObject root)
        {
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.22f;
            agent.height = 0.9f;
            agent.speed = 0.6f;
            agent.acceleration = 3f;
            agent.angularSpeed = 0f;
            agent.stoppingDistance = 0.15f;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.enabled = false;
            return agent;
        }

        static void AddCollider(GameObject root)
        {
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.45f, 0f);
            capsule.radius = 0.3f;
            capsule.height = 0.9f;
        }

        static void AddBlobShadow(GameObject root, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "BlobShadow";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0.003f, 0.02f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(0.62f, 0.56f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void Bind(NubiCharacterController controller, Animator animator, NavMeshAgent agent,
                         SkinnedMeshRenderer face, Transform model)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("_animator").objectReferenceValue = animator;
            so.FindProperty("_agent").objectReferenceValue = agent;
            so.FindProperty("_faceMesh").objectReferenceValue = face;
            so.FindProperty("_facePresets").objectReferenceValue = Load<TextAsset>(NubiAssetPaths.FacePresets);
            so.FindProperty("_bones.chest").objectReferenceValue = Find(model, "Chest");
            so.FindProperty("_bones.neck").objectReferenceValue = Find(model, "Neck");
            so.FindProperty("_bones.head").objectReferenceValue = Find(model, "Head");
            so.FindProperty("_bones.eyeLeft").objectReferenceValue = Find(model, "Eye_L");
            so.FindProperty("_bones.eyeRight").objectReferenceValue = Find(model, "Eye_R");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Find(Transform root, string name)
        {
            var bone = FindOrNull(root, name);
            if (bone == null)
                throw new InvalidOperationException($"Nubi.fbx has no '{name}' bone.");
            return bone;
        }

        static Transform FindOrNull(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                var found = FindOrNull(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        static Material UpsertMaterial(string path, string shaderName, Action<Material> configure)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Shader '{shaderName}' not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                NubiAssetPaths.EnsureFolderFor(path);
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.enableInstancing = true;
            configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void RemapModelMaterials(params (string source, Material target)[] remaps)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(NubiAssetPaths.Model);
            foreach (var (source, target) in remaps)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), target);
            importer.SaveAndReimport();
        }

        static Dictionary<string, AnimationClip> LoadClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(NubiAssetPaths.Model))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    clips[clip.name] = clip;
            }
            return clips;
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"Missing asset {path}.");
            return asset;
        }
    }
}
#endif
