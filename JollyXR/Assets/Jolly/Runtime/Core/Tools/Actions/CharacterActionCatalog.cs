using System.Collections.Generic;
using System.Text;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// What the active character's body can do, as tools in the "actions" namespace (ADR-0047): the model reads the
    /// namespace, picks the action the user asked for and calls it, like a tool on an MCP server. An action is offered
    /// only when the character can play every gesture it needs (<see cref="ICharacterController.CanPlay"/>), and the
    /// namespace description lists exactly the offered ones. Built once at composition, after the character is chosen.
    /// </summary>
    public sealed class CharacterActionCatalog
    {
        const string DescriptionTail =
            ". When the user asks you to do one, or asks whether you can, call it in this turn to show them; " +
            "never say you did it without calling it.";

        readonly List<ITool> _tools = new();
        readonly List<string> _phrases = new();

        CharacterActionCatalog() { }

        public IReadOnlyList<ITool> Tools => _tools;

        /// <summary>Null when the character can do none of them.</summary>
        public ToolNamespace Namespace => _tools.Count == 0
            ? null
            : new ToolNamespace(ToolNames.Actions.Namespace, Describe(_phrases));

        public static CharacterActionCatalog For(ICharacterController character, IUserPoseProvider user, IWorldQuery world,
                                                 LocomotionSettings settings, ILog log)
        {
            var catalog = new CharacterActionCatalog();
            catalog.Offer(character, "jump", new GestureActionTool(ToolNames.Actions.Jump,
                "Jump on the spot, e.g. 'jump!', 'can you jump?'.", character, log, GestureId.Jump), GestureId.Jump);
            catalog.Offer(character, "dance", new DanceTool(character, log), GestureId.Dance);
            catalog.Offer(character, "take a bow", new GestureActionTool(ToolNames.Actions.Bow,
                "Take a bow, e.g. 'take a bow', or when the user applauds you.", character, log, GestureId.Bow), GestureId.Bow);
            catalog.Offer(character, "spin around", new SpinTool(character, log), GestureId.HappyBounce);
            catalog.Offer(character, "celebrate", new GestureActionTool(ToolNames.Actions.Celebrate,
                "Jump for joy when the user asks you to celebrate, e.g. 'let's celebrate!'.", character, log,
                GestureId.Jump, GestureId.HappyBounce), GestureId.Jump, GestureId.HappyBounce);
            catalog.Offer(character, "move relative to the user (in front of them, behind them, to their left or right, closer, away)",
                new MoveRelativeTool(character, user, world, settings, log));
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

        /// <summary>Offers the tool when the character can play every gesture in <paramref name="requires"/>.</summary>
        void Offer(ICharacterController character, string phrase, ITool tool, params GestureId[] requires)
        {
            foreach (var gesture in requires)
            {
                if (!character.CanPlay(gesture))
                    return;
            }
            _tools.Add(tool);
            _phrases.Add(phrase);
        }

        static string Describe(List<string> phrases)
        {
            var sb = new StringBuilder("Things your body can do: ");
            for (var i = 0; i < phrases.Count; i++)
            {
                if (i > 0)
                    sb.Append(i == phrases.Count - 1 ? " and " : ", ");
                sb.Append(phrases[i]);
            }
            return sb.Append(DescriptionTail).ToString();
        }
    }
}
