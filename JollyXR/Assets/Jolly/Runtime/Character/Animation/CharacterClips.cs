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

        /// <summary>Every clip the animator builder wires: locomotion, the Concerned idle, the talk poses and each gesture.</summary>
        public static readonly IReadOnlyList<string> Required = BuildRequired();

        /// <summary>In the Blender contract but not played by anything yet.</summary>
        public static readonly IReadOnlyList<string> Optional = new[] { "Surprised" };

        static string[] BuildRequired()
        {
            var names = new List<string> { Idle, Walk, Hop, Sad, Talk, Listen, Think };
            foreach (GestureId gesture in Enum.GetValues(typeof(GestureId)))
            {
                if (GestureClips.TryGetClip(gesture, out var clip) && !names.Contains(clip))
                    names.Add(clip);
            }
            return names.ToArray();
        }
    }
}
