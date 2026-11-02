using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Character
{
    /// <summary>
    /// Canonical clip names: the Animator state names every character's controller uses, whatever its takes are called.
    /// Face presets are keyed by them too.
    /// </summary>
    public static class CharacterClips
    {
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Hop = "Hop";
        public const string Sad = "Sad";
        public const string Talk = "Talk";
        public const string Listen = "Listen";
        public const string Think = "Think";

        /// <summary>Every clip the animator builder must wire: locomotion, the Concerned idle, the talk poses and each
        /// upper-body gesture.</summary>
        public static readonly IReadOnlyList<string> Required = Build(required: true);

        /// <summary>The full-body action clips, wired when present (ADR-0047), and Surprised, in the Blender contract
        /// but not played by anything yet.</summary>
        public static readonly IReadOnlyList<string> Optional = Build(required: false);

        static string[] Build(bool required)
        {
            var names = required ? new List<string> { Idle, Walk, Hop, Sad, Talk, Listen, Think } : new List<string> { "Surprised" };
            foreach (GestureId gesture in Enum.GetValues(typeof(GestureId)))
            {
                if (GestureClips.TryGetClip(gesture, out var clip) && GestureClips.IsFullBody(gesture) != required
                    && !names.Contains(clip))
                    names.Add(clip);
            }
            return names.ToArray();
        }
    }
}
