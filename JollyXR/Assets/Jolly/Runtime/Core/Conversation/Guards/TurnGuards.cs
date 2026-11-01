using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>The local, model-free reactions to a final transcript (architecture §5.1, §9).</summary>
    public sealed class TurnGuards
    {
        /// <summary>Appended to the user text after the hand-off line has been spoken, so the reply follows it up.</summary>
        public const string CrisisNote =
            "\n\n(Note: emergency and crisis-line contacts were just read out to the user. Respond with warmth, take them " +
            "seriously, and encourage them to reach out to those services or someone they trust now.)";

        readonly StopGuard _stop;
        readonly CrisisGuard _crisis;
        readonly ICharacterController _character;

        public TurnGuards(StopGuard stop, CrisisGuard crisis, ICharacterController character)
        {
            _stop = stop;
            _crisis = crisis;
            _character = character;
        }

        /// <summary>Halts locomotion on a short "stop"; the turn still goes to the model.</summary>
        public bool ApplyStop(string transcript)
        {
            if (!_stop.Matches(transcript))
                return false;
            _character.Stop();
            return true;
        }

        public bool NeedsCrisisHandoff(string transcript, PersonaProfile persona) =>
            persona != null && persona.CrisisHandoff && _crisis.Matches(transcript);
    }
}
