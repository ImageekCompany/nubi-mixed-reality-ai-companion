using System;
using System.Collections.Generic;
using Jolly.Character;
using Jolly.Contracts;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using P = Jolly.Character.NubiAnimatorParameters;

namespace Jolly.Editor.Character
{
    /// <summary>
    /// Generates Nubi.controller (architecture §5.5). Base: Locomotion blend tree (Idle/Walk/Hop by Speed, playback
    /// rate from LocomotionRate) and a Sad idle for the Concerned mood. Conversation and Gesture are upper-body
    /// override layers whose Empty state writes nothing (write defaults off), so the layer below shows through.
    /// </summary>
    public static class NubiAnimatorBuilder
    {
        const float GestureFadeSeconds = 0.2f;

        public static AnimatorController Build(GameObject model, IReadOnlyDictionary<string, AnimationClip> clips,
                                               LocomotionSettings locomotion)
        {
            var mask = BuildUpperBodyMask(model);
            AssetDatabase.DeleteAsset(NubiAssetPaths.Controller);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(NubiAssetPaths.Controller);
            AddParameters(controller);
            BuildBase(controller, clips, locomotion);
            BuildConversation(AddLayer(controller, P.ConversationLayer, mask), clips);
            BuildGestures(AddLayer(controller, P.GestureLayer, mask), clips);
            EditorUtility.SetDirty(controller);
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
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Walk"], 0.25f);
            tree.AddChild(clips["Walk"], s.hopAbove);
            tree.AddChild(clips["Hop"], s.hopAbove + 0.15f);
            locomotion.speedParameter = P.LocomotionRateName;
            locomotion.speedParameterActive = true;
            locomotion.writeDefaultValues = false;
            machine.defaultState = locomotion;

            var sad = AddState(machine, "Sad", clips["Sad"]);
            var toSad = Transition(locomotion.AddTransition(sad), 0.4f);
            toSad.AddCondition(AnimatorConditionMode.Equals, (int)Mood.Concerned, P.MoodName);
            toSad.AddCondition(AnimatorConditionMode.Less, 0.05f, P.SpeedName);
            Transition(sad.AddTransition(locomotion), 0.3f).AddCondition(AnimatorConditionMode.NotEqual, (int)Mood.Concerned, P.MoodName);
            Transition(sad.AddTransition(locomotion), 0.2f).AddCondition(AnimatorConditionMode.Greater, 0.05f, P.SpeedName);
        }

        static void BuildConversation(AnimatorStateMachine machine, IReadOnlyDictionary<string, AnimationClip> clips)
        {
            machine.defaultState = AddState(machine, P.EmptyState, null);
            Pose(machine, "Talk", P.TalkingName, clips);
            Pose(machine, "Think", P.ThinkingName, clips);
            Pose(machine, "Listen", P.ListeningName, clips);
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

        static void BuildGestures(AnimatorStateMachine machine, IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var empty = AddState(machine, P.EmptyState, null);
            machine.defaultState = empty;
            foreach (GestureId gesture in Enum.GetValues(typeof(GestureId)))
            {
                if (!GestureClips.TryGetClip(gesture, out var name))
                    continue;
                var clip = clips[name];
                var state = AddState(machine, name, clip);
                state.tag = P.GestureTag;
                var enter = Transition(machine.AddAnyStateTransition(state), 0.12f);
                enter.AddCondition(AnimatorConditionMode.If, 0f, P.GestureName);
                enter.AddCondition(AnimatorConditionMode.Equals, (int)gesture, P.GestureIdName);
                var exit = Transition(state.AddTransition(empty), GestureFadeSeconds);
                exit.hasExitTime = true;
                exit.exitTime = Mathf.Max(0.5f, 1f - GestureFadeSeconds / clip.length);
            }
        }

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

        /// <summary>Spine and everything above it: gestures and talk poses leave the legs to the base layer.</summary>
        static AvatarMask BuildUpperBodyMask(GameObject model)
        {
            NubiAssetPaths.EnsureFolderFor(NubiAssetPaths.UpperBodyMask);
            AssetDatabase.DeleteAsset(NubiAssetPaths.UpperBodyMask);
            var mask = new AvatarMask { name = "NubiUpperBody" };
            mask.AddTransformPath(model.transform, true);
            for (var i = 0; i < mask.transformCount; i++)
                mask.SetTransformActive(i, mask.GetTransformPath(i).Contains("/Spine"));
            AssetDatabase.CreateAsset(mask, NubiAssetPaths.UpperBodyMask);
            return mask;
        }
    }
}
