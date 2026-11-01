using System;
using Jolly.Contracts;

namespace Jolly.Testing
{
    public sealed class FakeInteractionSignals : IInteractionSignals
    {
        public event Action GazeDwell;
        public event Action OpenPalm;
        public event Action Poke;

        public bool HasSubscribers => GazeDwell != null || OpenPalm != null || Poke != null;

        public void RaiseGazeDwell() => GazeDwell?.Invoke();
        public void RaiseOpenPalm() => OpenPalm?.Invoke();
        public void RaisePoke() => Poke?.Invoke();
    }
}
