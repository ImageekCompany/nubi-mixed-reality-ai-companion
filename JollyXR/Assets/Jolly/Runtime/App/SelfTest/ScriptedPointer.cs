using System;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// The self-test's pointer (ADR-0044): aims nowhere unless a scenario sets <see cref="Aim"/>, so a headless run's
    /// phantom hand rays never put a target under the scripted pinches (see <see cref="ScriptedPushToTalk"/>).
    /// </summary>
    public sealed class ScriptedPointer : IPointerInput
    {
        public Ray? Aim { get; set; }

        public event Action<Ray> TriggerPressed
        {
            add { }
            remove { }
        }

        public bool TryGetAim(out Ray ray, out PointerSource source)
        {
            ray = Aim ?? default;
            source = PointerSource.Hand;
            return Aim.HasValue;
        }
    }
}
