using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Jolly.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using P = Jolly.Character.NubiAnimatorParameters;

namespace Jolly.Editor.Character
{
    public enum CharacterIssueLevel { Warning, Error }

    public readonly struct CharacterIssue
    {
        public CharacterIssue(CharacterIssueLevel level, string message) { Level = level; Message = message; }
        public CharacterIssueLevel Level { get; }
        public string Message { get; }
        public override string ToString() => $"{Level}: {Message}";
    }

    /// <summary>
    /// The rig contract every character must meet (docs/characters.md): required clips, every canonical face channel
    /// bound to a blendshape the face mesh has (or the jaw), the look-at and upper-body bones, a face that points the
    /// root's +z, at most four bone influences, usable body and tuning values, and a triangle budget (a warning). Run
    /// by the content validator (tools/ci/test.sh) on every definition in the project, and from Jolly/Character/Validate
    /// Characters.
    /// </summary>
    public static class CharacterDefinitionValidator
    {
        /// <summary>Architecture §11 Performance: under 30k triangles.</summary>
        public const int TriangleBudget = 30000;
        public const int MaxBoneInfluences = 4;
        /// <summary>Degrees the face may point off the root's +z: generous, a wrong model faces 90° or 180° off.</summary>
        public const float FacingTolerance = 30f;

        static readonly Regex IdPattern = new("^[a-z0-9_-]+$");
        static readonly string[] Parameters =
        {
            P.SpeedName, P.LocomotionRateName, P.ListeningName, P.ThinkingName, P.TalkingName, P.MoodName,
            P.GestureIdName, P.GestureName,
        };

        [MenuItem("Jolly/Character/Validate Characters")]
        static void ValidateFromMenu()
        {
            foreach (var path in FindAll())
            {
                var issues = Validate(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path));
                foreach (var issue in issues)
                {
                    if (issue.Level == CharacterIssueLevel.Error)
                        Debug.LogError($"[CharacterValidator] {path}: {issue.Message}");
                    else
                        Debug.LogWarning($"[CharacterValidator] {path}: {issue.Message}");
                }
                Debug.Log($"[CharacterValidator] {path}: {issues.Count} issue(s)");
            }
        }

        /// <summary>Asset paths of every CharacterDefinition in the project, committed or local.</summary>
        public static List<string> FindAll()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CharacterDefinition)))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        public static List<CharacterIssue> Validate(CharacterDefinition d)
        {
            var issues = new List<CharacterIssue>();
            if (d == null)
            {
                Error(issues, "not a CharacterDefinition");
                return issues;
            }
            CheckIdentity(d, issues);
            CheckBody(d, issues);
            if (d.model == null)
            {
                Error(issues, "model is not set");
                return issues;
            }
            CheckClips(d, issues);
            CheckFacePresets(d, issues);
            CheckRig(d, issues);
            CheckMeshes(d.model, issues);
            return issues;
        }

        static void CheckIdentity(CharacterDefinition d, List<CharacterIssue> issues)
        {
            if (string.IsNullOrEmpty(d.id) || !IdPattern.IsMatch(d.id))
                Error(issues, $"id '{d.id}' must be lowercase letters, digits, '-' or '_'");
            if (string.IsNullOrWhiteSpace(d.displayName))
                Error(issues, "displayName is empty");
            if (d.voice == null)
                Error(issues, "voice is not set (a VoiceProfile, ADR-0031)");
            else if (string.IsNullOrWhiteSpace(d.voice.VoiceFor("en").VoiceId))
                Error(issues, "voice has no English Wit voice");
        }

        static void CheckBody(CharacterDefinition d, List<CharacterIssue> issues)
        {
            if (d.scale <= 0f || d.height <= 0f || d.agentRadius <= 0f || d.colliderRadius <= 0f
                || d.contactShadowRadius.x < 0f || d.contactShadowRadius.y < 0f)
                Error(issues, "scale, height and radii must be positive");
            var t = d.tuning;
            if (t?.locomotion == null || t.look == null || t.face == null)
            {
                Error(issues, "tuning is incomplete");
                return;
            }
            var l = t.locomotion;
            if (l.walkSpeed <= 0f || l.walkClipSpeed <= 0f || l.hopClipSpeed <= 0f || l.hopAbove <= 0f
                || l.minPlaybackRate <= 0f || l.maxPlaybackRate < l.minPlaybackRate)
                Error(issues, "locomotion speeds and playback rates must be positive (max ≥ min)");
        }

        static void CheckClips(CharacterDefinition d, List<CharacterIssue> issues)
        {
            var clips = CharacterClipSet.Resolve(d);
            foreach (var name in CharacterClipSet.Missing(clips))
                Error(issues, $"required clip '{name}' is neither bound nor a take of the model");
            foreach (var binding in d.clips ?? Array.Empty<ClipBinding>())
            {
                if (binding == null || binding.clip == null || !IsCanonicalClip(binding.name))
                    Error(issues, $"clip binding '{binding?.name}' is not a canonical clip with a clip set");
            }

            if (d.animatorController is not AnimatorController controller)
            {
                Error(issues, "animatorController is not set (Jolly/Character/Build Animator For Selected Definition)");
                return;
            }
            foreach (var parameter in Parameters)
            {
                if (Array.FindIndex(controller.parameters, p => p.name == parameter) < 0)
                    Error(issues, $"animator controller has no '{parameter}' parameter");
            }
            foreach (var layer in new[] { P.ConversationLayer, P.GestureLayer })
            {
                if (Array.FindIndex(controller.layers, l => l.name == layer) < 0)
                    Error(issues, $"animator controller has no '{layer}' layer");
            }
            var used = controller.animationClips;
            foreach (var name in CharacterClips.Required)
            {
                if (clips.TryGetValue(name, out var clip) && Array.IndexOf(used, clip) < 0)
                    Error(issues, $"animator controller does not play the '{name}' clip; rebuild it");
            }
        }

        static void CheckFacePresets(CharacterDefinition d, List<CharacterIssue> issues)
        {
            if (d.facePresets == null)
            {
                Error(issues, "facePresets is not set");
                return;
            }
            try
            {
                FacePresetTable.Parse(d.facePresets.text);
            }
            catch (Exception e) when (e is FormatException or ArgumentException)
            {
                Error(issues, "facePresets: " + e.Message);
            }
        }

        static void CheckRig(CharacterDefinition d, List<CharacterIssue> issues)
        {
            var root = d.model.transform;
            var names = d.bones ?? new RigBoneNames();
            foreach (var (role, bone) in new[]
                     {
                         ("chest", names.chest), ("neck", names.neck), ("head", names.head), ("eyeLeft", names.eyeLeft),
                         ("eyeRight", names.eyeRight), ("upperBody", names.upperBody),
                     })
            {
                if (CharacterRig.Find(root, bone) == null)
                    Error(issues, $"bones.{role} '{bone}' is not in the model");
            }
            if (CharacterRig.Find(root, names.footLeft) == null || CharacterRig.Find(root, names.footRight) == null)
                Warn(issues, $"bones.footLeft '{names.footLeft}' or bones.footRight '{names.footRight}' is not in the " +
                             "model: the contact shadow will not react to hops");
            var hasJaw = !string.IsNullOrEmpty(d.jaw?.bone);
            if (hasJaw && CharacterRig.Find(root, d.jaw.bone) == null)
                Error(issues, $"jaw bone '{d.jaw.bone}' is not in the model");

            var face = CharacterRig.FindFace(d.model, d.faceMesh);
            if (face == null)
            {
                Error(issues, string.IsNullOrEmpty(d.faceMesh)
                    ? "the model has no SkinnedMeshRenderer with blendshapes"
                    : $"faceMesh '{d.faceMesh}' is not a SkinnedMeshRenderer in the model");
                return;
            }
            CheckFace(d, face.sharedMesh, hasJaw, issues);
            CheckFacing(d, issues);
        }

        /// <summary>
        /// Everything that turns the character takes the root's +z as where the face points (ADR-0046). The face is
        /// where the vertices skinned to the eyes sit; the eye bones themselves are pivots inside the head, so their
        /// position says nothing about the facing.
        /// </summary>
        static void CheckFacing(CharacterDefinition d, List<CharacterIssue> issues)
        {
            // An unanimated instance stands in its bind pose; baking it reads vertices the import keeps unreadable.
            var model = UnityEngine.Object.Instantiate(d.model);
            var baked = new Mesh();
            try
            {
                var root = model.transform;
                var head = CharacterRig.Find(root, d.bones.head);
                var eyes = new[] { CharacterRig.Find(root, d.bones.eyeLeft), CharacterRig.Find(root, d.bones.eyeRight) };
                var face = CharacterRig.FindFace(model, d.faceMesh);
                if (head == null || eyes[0] == null || eyes[1] == null || face == null)
                    return;
                face.BakeMesh(baked, true);
                if (!TryEyeCentroid(face, baked.vertices, eyes, out var centroid))
                {
                    Warn(issues, $"no vertex of '{face.name}' is skinned to the eye bones: the facing is unchecked");
                    return;
                }
                var toFace = Vector3.ProjectOnPlane(Quaternion.Euler(0f, d.modelYaw, 0f) * root.InverseTransformVector(centroid - head.position),
                                                    Vector3.up);
                var off = Vector3.SignedAngle(Vector3.forward, toFace, Vector3.up);
                if (toFace.sqrMagnitude < 1e-8f || Mathf.Abs(off) > FacingTolerance)
                    Error(issues, $"with modelYaw {d.modelYaw:0.#} the face points {off:0}° from the root's +z; set modelYaw so " +
                                  "it faces +z (ADR-0046)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(model);
            }
        }

        /// <summary>World centroid of the baked vertices mostly skinned to the eye bones.</summary>
        static bool TryEyeCentroid(SkinnedMeshRenderer face, Vector3[] vertices, Transform[] eyes, out Vector3 centroid)
        {
            centroid = default;
            var bones = face.bones;
            var weights = face.sharedMesh.boneWeights;
            if (weights.Length != vertices.Length)
                return false;
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                if (w.weight0 < 0.5f || w.boneIndex0 >= bones.Length || Array.IndexOf(eyes, bones[w.boneIndex0]) < 0)
                    continue;
                sum += face.transform.TransformPoint(vertices[i]);
                count++;
            }
            if (count == 0)
                return false;
            centroid = sum / count;
            return true;
        }

        static void CheckFace(CharacterDefinition d, Mesh mesh, bool hasJaw, List<CharacterIssue> issues)
        {
            var seen = new bool[FaceChannels.Count];
            foreach (var binding in d.face ?? Array.Empty<FaceChannelBinding>())
            {
                if (binding == null || !FaceChannels.TryParse(binding.channel, out var channel))
                {
                    Error(issues, $"face binding '{binding?.channel}' is not a canonical channel");
                    continue;
                }
                if (seen[(int)channel])
                    Error(issues, $"face channel '{binding.channel}' is bound twice");
                seen[(int)channel] = true;

                var shapes = 0;
                foreach (var target in binding.blendShapes ?? Array.Empty<BlendShapeTarget>())
                {
                    if (target == null || mesh.GetBlendShapeIndex(target.name) < 0)
                        Error(issues, $"face channel '{binding.channel}' targets blendshape '{target?.name}', which '{mesh.name}' lacks");
                    else if (target.weight <= 0f)
                        Error(issues, $"face channel '{binding.channel}' gives '{target.name}' no weight");
                    else
                        shapes++;
                }
                if (binding.jawOpen > 0f && !hasJaw)
                    Error(issues, $"face channel '{binding.channel}' opens the jaw, but jaw.bone is not set");
                // sil is the rest mouth: a jaw-bone mouth shows it by closing.
                var shown = shapes > 0 || (hasJaw && binding.jawOpen > 0f) || (channel == FaceChannel.Silence && hasJaw);
                if (!shown)
                    Error(issues, $"face channel '{binding.channel}' drives nothing");
            }
            for (var c = 0; c < seen.Length; c++)
            {
                if (!seen[c])
                    Error(issues, $"face channel '{FaceChannels.ShapeNames[c]}' is not bound");
            }
        }

        static void CheckMeshes(GameObject model, List<CharacterIssue> issues)
        {
            long triangles = 0;
            foreach (var mesh in Meshes(model))
            {
                for (var s = 0; s < mesh.subMeshCount; s++)
                {
                    if (mesh.GetTopology(s) == MeshTopology.Triangles)
                        triangles += mesh.GetIndexCount(s) / 3;
                }
                var influences = 0;
                foreach (var count in mesh.GetBonesPerVertex())
                    influences = Math.Max(influences, count);
                if (influences > MaxBoneInfluences)
                    Error(issues, $"mesh '{mesh.name}' has vertices with {influences} bone influences (max {MaxBoneInfluences})");
            }
            if (triangles > TriangleBudget)
                Warn(issues, $"{triangles} triangles is over the {TriangleBudget} budget");
        }

        static IEnumerable<Mesh> Meshes(GameObject model)
        {
            foreach (var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh != null)
                    yield return skinned.sharedMesh;
            }
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                    yield return filter.sharedMesh;
            }
        }

        static bool IsCanonicalClip(string name)
        {
            foreach (var clip in CharacterClips.Required)
            {
                if (clip == name)
                    return true;
            }
            foreach (var clip in CharacterClips.Optional)
            {
                if (clip == name)
                    return true;
            }
            return false;
        }

        static void Error(List<CharacterIssue> issues, string message) => issues.Add(new CharacterIssue(CharacterIssueLevel.Error, message));
        static void Warn(List<CharacterIssue> issues, string message) => issues.Add(new CharacterIssue(CharacterIssueLevel.Warning, message));
    }
}
