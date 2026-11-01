using System;

namespace Jolly.Core.Personas
{
    /// <summary>What the controller needs from the persona store beyond the frozen <c>IPersonaProvider</c>.</summary>
    public interface IPersonaDirectory
    {
        /// <summary>The persona's localized greeting, or null when it has none.</summary>
        string GreetingFor(string id);
        /// <summary>Raised by an accepted RequestSwitch, so a switch chosen in Settings while Idle applies at once.</summary>
        event Action SwitchRequested;
    }
}
