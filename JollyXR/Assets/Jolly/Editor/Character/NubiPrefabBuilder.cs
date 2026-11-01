using System;
using System.IO;
using Jolly.Character;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Jolly.Editor.Character
{
    /// <summary>
    /// Regenerates every derived Nubi asset from Nubi.fbx: materials (remapped into the model import), the Animator
    /// controller and Nubi.prefab, the character shell every other character swaps its model into (ADR-0026). The
    /// definition's values are kept. Deterministic, so a rebuild after a Blender change is one command:
    /// <c>Unity -batchmode -quit -projectPath JollyXR -executeMethod Jolly.Editor.Character.NubiPrefabBuilder.Build</c>.
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
            var definition = UpsertDefinition(model);
            var controller = CharacterAnimatorBuilder.Build(definition, NubiAssetPaths.Controller, NubiAssetPaths.UpperBodyMask);
            SavePrefab(model, definition, controller, blob);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The definition holds Nubi's tuning, so a rebuild keeps every value in it and only re-points the generated
        /// references. A missing one is created with the contract defaults, which are Nubi's.
        /// </summary>
        static CharacterDefinition UpsertDefinition(GameObject model)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(NubiAssetPaths.Definition);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CharacterDefinition>();
                definition.id = "nubi";
                definition.displayName = "Nubi";
                AssetDatabase.CreateAsset(definition, NubiAssetPaths.Definition);
            }
            definition.model = model;
            definition.facePresets = Load<TextAsset>(NubiAssetPaths.FacePresets);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        static void SavePrefab(GameObject model, CharacterDefinition definition, RuntimeAnimatorController controller,
                               Material blobShadow)
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
                var capsule = root.AddComponent<CapsuleCollider>();
                var shadow = AddBlobShadow(root, blobShadow);
                CharacterBody.Apply(definition, instance.transform, agent, capsule, shadow.transform);
                if (!CharacterRig.TryBind(instance, definition, out _, out var error))
                    throw new InvalidOperationException($"Nubi.fbx does not bind to its definition: {error}.");
                Bind(root.AddComponent<NubiCharacterController>(), definition, instance, agent, capsule, shadow);
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
            agent.speed = 0.6f;
            agent.acceleration = 3f;
            agent.angularSpeed = 0f;
            agent.stoppingDistance = 0.15f;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.enabled = false;
            return agent;
        }

        static MeshRenderer AddBlobShadow(GameObject root, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "BlobShadow";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0.003f, 0.02f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        static void Bind(NubiCharacterController controller, CharacterDefinition definition, GameObject model,
                         NavMeshAgent agent, CapsuleCollider capsule, MeshRenderer shadow)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("_definition").objectReferenceValue = definition;
            so.FindProperty("_model").objectReferenceValue = model;
            so.FindProperty("_agent").objectReferenceValue = agent;
            so.FindProperty("_collider").objectReferenceValue = capsule;
            so.FindProperty("_contactShadow").objectReferenceValue = shadow;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"Missing asset {path}.");
            return asset;
        }
    }
}
