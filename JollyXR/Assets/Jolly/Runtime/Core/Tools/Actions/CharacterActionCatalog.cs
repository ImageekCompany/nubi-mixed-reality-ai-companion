using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Everything the active character's body can do on request, as tools in the "actions" namespace (ADR-0047): the
    /// model reads the namespace, picks the action the user asked for and calls it, like a tool on an MCP server. An
    /// action is offered only when the character can play every gesture it needs (<see cref="ICharacterController.CanPlay"/>).
    /// The namespace description names no action: a request may offer fewer than the catalog holds (seated mode, a
    /// persona's list), so each tool's own description carries what it does. Built once at composition, after the
    /// character is chosen.
    /// </summary>
    public sealed class CharacterActionCatalog
    {
        public const string Description =
            "Things your body can do, one tool each. When the user asks you to do one, or asks whether you can, call it in " +
            "this turn to show them; never say you did it without calling it. When they ask you to surprise them, show " +
            "off or do a trick, pick one or two of these yourself and call them.";

        readonly List<ITool> _tools = new();

        CharacterActionCatalog() { }

        public IReadOnlyList<ITool> Tools => _tools;

        /// <summary>Null when the character can do none of them.</summary>
        public ToolNamespace Namespace => _tools.Count == 0 ? null : new ToolNamespace(ToolNames.Actions.Namespace, Description);

        public static CharacterActionCatalog For(ICharacterController character, IUserPoseProvider user, IWorldQuery world,
                                                 LocomotionSettings settings, ILog log)
        {
            var catalog = new CharacterActionCatalog();
            GestureActionTool Clip(string name, string description, bool wholeBody, params GestureId[] sequence) =>
                new(name, description, character, log, wholeBody, sequence);

            catalog.Offer(character, Clip(ToolNames.Actions.Jump,
                "Jump on the spot, e.g. 'jump!', 'can you jump?'.", true, GestureId.Jump));
            catalog.Offer(character, new DanceTool(character, log), GestureId.Dance);
            catalog.Offer(character, Clip(ToolNames.Actions.Bow,
                "Take a bow, e.g. 'take a bow', or when the user applauds you.", true, GestureId.Bow));
            catalog.Offer(character, new SpinTool(character, log), GestureId.HappyBounce);
            catalog.Offer(character, Clip(ToolNames.Actions.Celebrate,
                "Jump for joy when the user asks you to celebrate, e.g. 'let's celebrate!'.", true,
                GestureId.Jump, GestureId.HappyBounce));
            catalog.Offer(character, new MoveRelativeTool(character, user, world, settings, log));
            catalog.Offer(character, Clip(ToolNames.Actions.Wave,
                "Wave at the user: hello, goodbye, or when they ask you to wave.", false, GestureId.Wave));
            catalog.Offer(character, Clip(ToolNames.Actions.Nod, "Nod: yes, or agreeing.", false, GestureId.Nod));
            catalog.Offer(character, Clip(ToolNames.Actions.ShakeHead,
                "Shake your head: no, or disagreeing.", false, GestureId.ShakeHead));
            catalog.Offer(character, Clip(ToolNames.Actions.Shrug, "Shrug: when you do not know.", false, GestureId.Shrug));
            catalog.Offer(character, Clip(ToolNames.Actions.Think,
                "Strike a thinking pose, e.g. 'have a think', or over a hard question.", false, GestureId.Think));
            catalog.Offer(character, Clip(ToolNames.Actions.Bounce,
                "Bounce happily, e.g. at good news.", false, GestureId.HappyBounce));
            catalog.Offer(character, Clip(ToolNames.Actions.Stretch,
                "Have a good stretch, e.g. 'stretch!', 'are you tired?'.", false, GestureId.Stretch));
            catalog.Offer(character, Clip(ToolNames.Actions.LookAround,
                "Look around the room, e.g. 'look around'.", false, GestureId.LookAround));
            return catalog;
        }

        /// <summary>Registers the namespace, then its tools; nothing when no action is offered.</summary>
        public void RegisterInto(IToolRegistry registry)
        {
            if (_tools.Count == 0)
                return;
            registry.RegisterNamespace(Namespace);
            foreach (var tool in _tools)
                registry.Register(tool);
        }

        /// <summary>Offers the tool when the character can play every gesture in <paramref name="requires"/>; a
        /// <see cref="GestureActionTool"/> needs its own sequence.</summary>
        void Offer(ICharacterController character, ITool tool, params GestureId[] requires)
        {
            foreach (var gesture in requires)
            {
                if (!character.CanPlay(gesture))
                    return;
            }
            if (tool is GestureActionTool clip && !clip.CanPlay())
                return;
            _tools.Add(tool);
        }
    }
}
