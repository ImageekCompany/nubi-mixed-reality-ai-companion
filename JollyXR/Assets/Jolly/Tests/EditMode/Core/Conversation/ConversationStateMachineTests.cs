using System.Collections.Generic;
using System.Linq;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Testing;
using NUnit.Framework;
using S = Jolly.Contracts.ConversationState;
using T = Jolly.Core.Conversation.ConversationTrigger;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class ConversationStateMachineTests
    {
        /// <summary>Shortest trigger path from Idle to each state, with every latched flag clear.</summary>
        static readonly Dictionary<S, T[]> PathTo = new()
        {
            [S.Idle] = new T[0],
            [S.Listening] = new[] { T.PttPressed },
            [S.Thinking] = new[] { T.PttPressed, T.FinalText },
            [S.Acting] = new[] { T.PttPressed, T.FinalText, T.ToolStarted },
            [S.Speaking] = new[] { T.PttPressed, T.FinalText, T.FirstAudio },
        };

        /// <summary>Triggers that leave the state machine in a different state, or the same state on a taken self-loop,
        /// from the canonical path above. Everything else is ignored (latches only).</summary>
        static readonly Dictionary<S, T[]> Handled = new()
        {
            [S.Idle] = new[] { T.PttPressed, T.OfflinePttPressed, T.Announce },
            [S.Listening] = new[] { T.FinalText, T.FinalEmpty, T.SttFailed, T.Watchdog },
            [S.Thinking] = new[] { T.PttPressed, T.OfflinePttPressed, T.FirstAudio, T.ToolStarted, T.TurnFailed, T.Watchdog, T.TurnCompleted },
            [S.Acting] = new[] { T.PttPressed, T.OfflinePttPressed, T.FirstAudio, T.ToolsDone, T.TurnFailed, T.Watchdog },
            [S.Speaking] = new[] { T.PttPressed, T.OfflinePttPressed, T.SpeechDrained, T.TurnFailed, T.Watchdog },
        };

        // Architecture §6 transitions, then the rows WP2 adds to close the table (see the class remarks).
        [TestCase(new[] { T.PttPressed }, S.Listening)]
        [TestCase(new[] { T.OfflinePttPressed }, S.Speaking)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio, T.PttPressed }, S.Listening)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.PttPressed }, S.Listening)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.ToolStarted, T.PttPressed }, S.Listening)]
        [TestCase(new[] { T.PttPressed, T.FinalEmpty }, S.Idle)]
        [TestCase(new[] { T.PttPressed, T.SttFailed }, S.Idle)]
        [TestCase(new[] { T.PttPressed, T.Watchdog }, S.Idle)]
        [TestCase(new[] { T.PttPressed, T.FinalText }, S.Thinking)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio }, S.Speaking)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.ToolStarted }, S.Acting)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.ToolStarted, T.FirstAudio }, S.Speaking)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio, T.ToolStarted, T.SpeechDrained }, S.Acting)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.ToolStarted, T.ToolsDone }, S.Thinking)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio, T.TurnCompleted, T.SpeechDrained }, S.Idle)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio, T.ToolStarted, T.TurnCompleted, T.SpeechDrained }, S.Acting)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.ToolStarted, T.TurnCompleted, T.ToolsDone }, S.Idle)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.ToolStarted, T.SpeechQueued, T.TurnCompleted, T.ToolsDone }, S.Acting)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.TurnFailed }, S.Speaking)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.ToolStarted, T.TurnFailed }, S.Speaking)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio, T.TurnFailed }, S.Speaking)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio, T.TurnFailed, T.SpeechDrained }, S.Idle)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.Watchdog }, S.Idle)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.ToolStarted, T.Watchdog }, S.Idle)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio, T.Watchdog }, S.Idle)]
        // Added: speech drained mid-turn waits for the next round in Thinking, not in Speaking.
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio, T.SpeechDrained }, S.Thinking)]
        // Added: a turn that ends with nothing left to say or run goes Idle from Thinking.
        [TestCase(new[] { T.PttPressed, T.FinalText, T.TurnCompleted }, S.Idle)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.SpeechQueued, T.TurnCompleted }, S.Thinking)]
        [TestCase(new[] { T.PttPressed, T.FinalText, T.SpeechQueued, T.TurnCompleted, T.SpeechDrained }, S.Idle)]
        // Added: offline press while busy barges into a canned line; announcements (persona greeting) speak from Idle.
        [TestCase(new[] { T.PttPressed, T.FinalText, T.FirstAudio, T.OfflinePttPressed }, S.Speaking)]
        [TestCase(new[] { T.Announce }, S.Speaking)]
        [TestCase(new[] { T.Announce, T.TurnCompleted, T.SpeechDrained }, S.Idle)]
        public void Fire_FollowsTheTransitionTable(T[] path, S expected)
        {
            var machine = new ConversationStateMachine();

            foreach (var trigger in path)
                machine.Fire(trigger);

            Assert.That(machine.State, Is.EqualTo(expected));
        }

        [Test]
        public void Fire_UnhandledTriggers_AreIgnoredInEveryState([Values] S state)
        {
            foreach (var trigger in System.Enum.GetValues(typeof(T)).Cast<T>().Except(Handled[state]))
            {
                var log = new RecordingLog();
                var machine = Reach(state, log);

                var taken = machine.Fire(trigger);

                Assert.That(taken, Is.False, $"{state} + {trigger}");
                Assert.That(machine.State, Is.EqualTo(state), $"{state} + {trigger}");
            }
        }

        [Test]
        public void Fire_HandledTriggers_AreTakenInEveryState([Values] S state)
        {
            foreach (var trigger in Handled[state])
            {
                var machine = Reach(state, new RecordingLog());

                Assert.That(machine.Fire(trigger), Is.True, $"{state} + {trigger}");
            }
        }

        [Test]
        public void LatchedFlags_SetByTheirTriggersAndResetOnPress()
        {
            var machine = Reach(S.Speaking, new RecordingLog());
            machine.Fire(T.ToolStarted);
            machine.Fire(T.TurnCompleted);

            Assert.That(machine.ToolsRunning && machine.SpeechPending && machine.TurnEnded, Is.True);

            machine.Fire(T.PttPressed);

            Assert.That(machine.ToolsRunning || machine.SpeechPending || machine.TurnEnded, Is.False);
        }

        [Test]
        public void Changed_RaisedOnlyOnActualStateChange()
        {
            var machine = new ConversationStateMachine();
            var changes = new List<(S, S)>();
            machine.Changed += (from, to) => changes.Add((from, to));

            machine.Fire(T.PttPressed);
            machine.Fire(T.FinalText);
            machine.Fire(T.TurnFailed);
            machine.Fire(T.TurnFailed);

            Assert.That(changes, Is.EqualTo(new[] { (S.Idle, S.Listening), (S.Listening, S.Thinking), (S.Thinking, S.Speaking) }));
        }

        [Test]
        public void Fire_Ignored_LogsAtDebug()
        {
            var log = new RecordingLog();
            var machine = new ConversationStateMachine(log);

            machine.Fire(T.FirstAudio);

            Assert.That(log.Contains(LogLevel.Debug, "evt=state.ignored state=Idle trigger=FirstAudio"), Is.True);
        }

        static ConversationStateMachine Reach(S state, ILog log)
        {
            var machine = new ConversationStateMachine(log);
            foreach (var trigger in PathTo[state])
                machine.Fire(trigger);
            Assert.That(machine.State, Is.EqualTo(state));
            return machine;
        }
    }
}
