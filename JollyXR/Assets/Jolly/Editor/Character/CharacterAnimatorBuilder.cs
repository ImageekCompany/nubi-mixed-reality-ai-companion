using System;
using System.Collections.Generic;
using System.IO;
using Jolly.Character;
using Jolly.Contracts;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using P = Jolly.Character.NubiAnimatorParameters;

namespace Jolly.Editor.Character
{
    /// <summary>
    /// Generates a character's Animator controller (architecture §5.5) from its definition, with states named after the
    /// canonical clips (<see cref="CharacterClips"/>). Base: Locomotion blend tree (Idle/Walk/Hop by Speed, playback
    /// rate from LocomotionRate) and a Sad idle for the Concerned mood. Conversation and Gesture are upper-body
    /// override layers whose Empty state writes nothing (write defaults off), so the layer below shows through. Action
    /// is the same over the whole body, on top, for the optional full-body clips the definition has (ADR-0047); a
    /// looping one keeps looping while GestureHold is set.
    /// </summary>
    public static class CharacterAnimatorBuilder
    {
        const float GestureFadeSeconds = 0.2f;

        /// <summary>
        /// Rebuilds the controller of every local (git-ignored) character, e.g. after its model gained takes:
        /// <c>Unity -batchmode -quit -projectPath JollyXR -executeMethod Jolly.Editor.Character.CharacterAnimatorBuilder.BuildLocal</c>.
        /// </summary>
        [MenuItem("Jolly/Character/Rebuild Local Character Animators")]
        public static void BuildLocal()
        {
            foreach (var definition in LocalCharacters.FindAll())
            {
                Build(definition);
                Debug.Log($"[CharacterAnimatorBuilder] rebuilt {AssetDatabase.GetAssetPath(definition.animatorController)}");
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Jolly/Character/Build Animator For Selected Definition")]
        static void BuildSelected()
        {
            if (Selection.activeObject is not CharacterDefinition definition)
            {
                Debug.LogError("[CharacterAnimatorBuilder] Select a CharacterDefinition asset first.");
                return;
            }
            Build(definition);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Writes <c>Animator/&lt;Id&gt;.controller</c> and its mask next to the definition and assigns it.</summary>
        public static AnimatorController Build(CharacterDefinition definition)
        {
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(definition))?.Replace('\\', '/') + "/Animator/";
            var name = string.IsNullOrEmpty(definition.id)
                ? definition.name
                : char.ToUpperInvariant(definition.id[0]) + definition.id.Substring(1);
            return Build(definition, folder + name + ".controller", folder + name + "UpperBody.mask");
        }

        public static AnimatorController Build(CharacterDefinition definition, string controllerPath, string maskPath)
        {
            var clips = CharacterClipSet.Resolve(definition);
            var missing = CharacterClipSet.Missing(clips);
            if (missing.Count > 0)
                throw new InvalidOperationException($"'{definition.name}' lacks clips: {string.Join(", ", missing)}.");

            var mask = BuildUpperBodyMask(definition.model, definition.bones.upperBody, maskPath);
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AddParameters(controller);
            BuildBase(controller, clips, definition.tuning.locomotion);
            BuildConversation(AddLayer(controller, P.ConversationLayer, mask), clips);
            BuildGestures(AddLayer(controller, P.GestureLayer, mask), clips, fullBody: false);
            BuildGestures(AddLayer(controller, P.ActionLayer, null), clips, fullBody: true);
            EditorUtility.SetDirty(controller);
            definition.animatorController = controller;
            EditorUtility.SetDirty(definition);
            return controller;
        }

        static void AddParameters(AnimatorController c)
        {
            c.AddParameter(P.SpeedName, AnimatorControllerParameterType.Float);
            c.AddParameter(P.LocomotionRateName, AnimatorControllerParameterType.Float);
            c.AddParameter(P.ListeningName, AnimatorControllerParameterType.Bool);
            c.AddParameter(P.ThinkingName, AnimatorControllerParameterType.Bool);
            c.AddParameter(P.TalkingName, AnimatorControllerParameterType.Bool);
            c.AddParameter(P.MoodName, AnimatorControllerParameterType.Int);
            c.AddParameter(P.GestureIdName, AnimatorControllerParameterType.Int);
            c.AddParameter(P.GestureName, AnimatorControllerParameterType.Trigger);
            c.AddParameter(P.GestureHoldName, AnimatorControllerParameterType.Bool);
            var parameters = c.parameters;
            foreach (var p in parameters)
            {
                if (p.name == P.LocomotionRateName)
                    p.defaultFloat = 1f;
            }
            c.parameters = parameters;
        }

        static void BuildBase(AnimatorController c, IReadOnlyDictionary<string, AnimationClip> clips, LocomotionSettings s)
        {
            var machine = c.layers[0].stateMachine;
            var locomotion = c.CreateBlendTreeInController(P.LocomotionState, out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = P.SpeedName;
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips[CharacterClips.Idle], 0f);
            tree.AddChild(clips[CharacterClips.Walk], 0.25f);
            tree.AddChild(clips[CharacterClips.Walk], s.hopAbove);
            tree.AddChild(clips[CharacterClips.Hop], s.hopAbove + 0.15f);
            locomotion.speedParameter = P.LocomotionRateName;
            locomotion.speedParameterActive = true;
            locomotion.writeDefaultValues = false;
            machine.defaultState = locomotion;

            var sad = AddState(machine, CharacterClips.Sad, clips[CharacterClips.Sad]);
            var toSad = Transition(locomotion.AddTransition(sad), 0.4f);
            toSad.AddCondition(AnimatorConditionMode.Equals, (int)Mood.Concerned, P.MoodName);
            toSad.AddCondition(AnimatorConditionMode.Less, 0.05f, P.SpeedName);
            Transition(sad.AddTransition(locomotion), 0.3f).AddCondition(AnimatorConditionMode.NotEqual, (int)Mood.Concerned, P.MoodName);
            Transition(sad.AddTransition(locomotion), 0.2f).AddCondition(AnimatorConditionMode.Greater, 0.05f, P.SpeedName);
        }

        static void BuildConversation(AnimatorStateMachine machine, IReadOnlyDictionary<string, AnimationClip> clips)
        {
            machine.defaultState = AddState(machine, P.EmptyState, null);
            Pose(machine, CharacterClips.Talk, P.TalkingName, clips);
            Pose(machine, CharacterClips.Think, P.ThinkingName, clips);
            Pose(machine, CharacterClips.Listen, P.ListeningName, clips);
            var toEmpty = Transition(machine.AddAnyStateTransition(machine.defaultState), 0.3f);
            toEmpty.canTransitionToSelf = false;
            toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0f, P.TalkingName);
            toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0f, P.ThinkingName);
            toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0f, P.ListeningName);
        }

        static void Pose(AnimatorStateMachine machine, string clip, string flag, IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var enter = Transition(machine.AddAnyStateTransition(AddState(machine, clip, clips[clip])), 0.25f);
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, flag);
        }

        static void BuildGestures(AnimatorStateMachine machine, IReadOnlyDictionary<string, AnimationClip> clips, bool fullBody)
        {
            var empty = AddState(machine, P.EmptyState, null);
            machine.defaultState = empty;
            foreach (GestureId gesture in Enum.GetValues(typeof(GestureId)))
            {
                if (GestureClips.IsFullBody(gesture) != fullBody || !GestureClips.TryGetClip(gesture, out var name)
                    || !clips.TryGetValue(name, out var clip))
                    continue;
                var state = AddState(machine, name, clip);
                state.tag = P.GestureTag;
                var enter = Transition(machine.AddAnyStateTransition(state), 0.12f);
                enter.AddCondition(AnimatorConditionMode.If, 0f, P.GestureName);
                enter.AddCondition(AnimatorConditionMode.Equals, (int)gesture, P.GestureIdName);
                var exit = Transition(state.AddTransition(empty), GestureFadeSeconds);
                exit.hasExitTime = true;
                exit.exitTime = Mathf.Max(0.5f, 1f - GestureFadeSeconds / clip.length);
                // Below 1 the exit time is checked on every loop, so a held loop leaves at the end of the loop it is on.
                if (clip.isLooping)
                    exit.AddCondition(AnimatorConditionMode.IfNot, 0f, P.GestureHoldName);
            }
        }

        /// <summary>A null mask leaves the layer full-body.</summary>
        static AnimatorStateMachine AddLayer(AnimatorController c, string name, AvatarMask mask)
        {
            c.AddLayer(name);
            var layers = c.layers;
            var layer = layers[layers.Length - 1];
            layer.avatarMask = mask;
            layer.defaultWeight = 1f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            c.layers = layers;
            return layer.stateMachine;
        }

        static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion)
        {
            var state = machine.AddState(name);
            state.motion = motion;
            state.writeDefaultValues = false;
            return state;
        }

        static AnimatorStateTransition Transition(AnimatorStateTransition t, float seconds)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = seconds;
            return t;
        }

        /// <summary>The upper-body bone and everything under it: gestures and talk poses leave the legs to the base layer.</summary>
        static AvatarMask BuildUpperBodyMask(GameObject model, string upperBody, string path)
        {
            NubiAssetPaths.EnsureFolderFor(path);
            AssetDatabase.DeleteAsset(path);
            var mask = new AvatarMask { name = Path.GetFileNameWithoutExtension(path) };
            mask.AddTransformPath(model.transform, true);
            for (var i = 0; i < mask.transformCount; i++)
                mask.SetTransformActive(i, Array.IndexOf(mask.GetTransformPath(i).Split('/'), upperBody) >= 0);
            AssetDatabase.CreateAsset(mask, path);
            return mask;
        }
    }
}
