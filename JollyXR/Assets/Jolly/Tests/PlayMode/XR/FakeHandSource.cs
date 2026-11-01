using System.Collections.Generic;
using Jolly.XR.Hands;
using UnityEngine.XR.Hands;

namespace Jolly.Tests.PlayMode.XR
{
    sealed class FakeHandSource : IHandSource
    {
        readonly Dictionary<Handedness, HandSnapshot> _hands = new();

        public void Set(Handedness handedness, HandSnapshot snapshot) => _hands[handedness] = snapshot;
        public void Lose(Handedness handedness) => _hands.Remove(handedness);

        public bool TryGetHand(Handedness handedness, out HandSnapshot snapshot) => _hands.TryGetValue(handedness, out snapshot);
    }
}
