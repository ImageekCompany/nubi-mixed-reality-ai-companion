using System;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Spatial;
using Jolly.XR.Interaction;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace Jolly.App.Composition
{
    /// <summary>
    /// Talk and move from one pinch (ADR-0044): the tap/hold split over the pinch, the push-to-talk the conversation
    /// sees (with the A/X buttons), the microphone that pre-rolls during a waiting pinch, the ring under the user's ray
    /// and the walk a tap or a controller trigger starts.
    /// </summary>
    public sealed class PointServices
    {
        /// <summary>What the conversation listens to: a held pinch, or A/X on a controller.</summary>
        public IVoiceActivation Voice { get; private set; }
        /// <summary>The turn's microphone; hands over the pre-roll of a pinch that became a hold.</summary>
        public IAudioCapture Microphone { get; private set; }
        /// <summary>The tap/hold split over the pinch.</summary>
        public TapHoldClassifier Pinch { get; private set; }
        public PointerTargeting Targeting { get; private set; }
        public MoveTargetResolver Resolver { get; private set; }
        public PointToMove Move { get; private set; }

        /// <param name="edges">A replaced <see cref="Edges.Voice"/> stands in for the pinch alone (no controller
        /// buttons); a replaced <see cref="Edges.Pointer"/> for the rig's rays and triggers.</param>
        public static PointServices Build(Foundation f, JollyConfig config, SceneRefs scene, Edges edges, XrServices xr,
                                          SpeechServices speech, DisposableStack disposables)
        {
            var settings = new PointMoveSettings { TapMax = TimeSpan.FromSeconds(config.tapMaxSeconds) };
            var pointer = edges.Pointer ?? disposables.Add(new XrPointer(scene.pushToTalk, xr.Hands,
                scene.origin.GetComponent<XRInputModalityManager>(), scene.nubiCollider));
            var resolver = new MoveTargetResolver(xr.World, scene.nubi, xr.User, settings, new LocomotionSettings());
            scene.moveReticle.Construct(f.Clock);
            var targeting = new PointerTargeting(pointer, resolver, scene.moveReticle);
            var pinch = disposables.Add(new TapHoldClassifier(edges.Voice ?? scene.pushToTalk, f.Clock, settings.TapMax,
                targeting.TryCapture));
            var machine = f.Machine;
            var output = speech.Output;
            var microphone = disposables.Add(new PreRollCapture(speech.Microphone, pinch, new ConversationTimings().CaptureSampleRate,
                () => !output.IsPlaying && machine.State != ConversationState.Listening));
            var voice = edges.Voice != null
                ? (IVoiceActivation)pinch
                : disposables.Add(new CompositeVoiceActivation(pinch, scene.controllerPushToTalk));
            return new PointServices
            {
                Voice = voice,
                Microphone = microphone,
                Pinch = pinch,
                Targeting = targeting,
                Resolver = resolver,
                Move = disposables.Add(new PointToMove(pinch, pointer, targeting, resolver, scene.nubi, settings, scene.moveReticle,
                    f.Clock, f.Log)),
            };
        }
    }
}
