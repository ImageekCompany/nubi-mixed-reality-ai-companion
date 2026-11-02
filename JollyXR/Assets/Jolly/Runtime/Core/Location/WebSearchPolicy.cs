using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.Core.Location
{
    /// <summary>
    /// Whether a live turn offers the provider's web search (ADR-0048): it is enabled, the persona allows "web_search" (a
    /// null AllowedTools allows it) and the link is Online. Degraded already means slow or failing requests, and a search adds
    /// a second model pass, so it is left out there.
    /// </summary>
    public sealed class WebSearchPolicy
    {
        readonly IConnectivityMonitor _connectivity;
        readonly bool _enabled;

        /// <param name="enabled">False keeps the location for the context block but never offers search.</param>
        public WebSearchPolicy(UserLocation location, IConnectivityMonitor connectivity, bool enabled = true)
        {
            Location = location;
            _connectivity = connectivity;
            _enabled = enabled;
        }

        public UserLocation Location { get; }

        /// <summary>Search is on and the persona may use it, whatever the link: the persona may also be told the home city.</summary>
        public bool Allows(PersonaProfile persona) =>
            _enabled && ToolNames.IsAllowed(persona?.AllowedTools, ToolNames.WebSearch);

        /// <summary>Null: no web search this turn.</summary>
        public WebSearchOptions For(PersonaProfile persona) =>
            Allows(persona) && _connectivity.State == Contracts.Connectivity.Online ? Location.SearchOptions() : null;
    }
}
