using System.Threading;
using Jolly.App.Adapters;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Core.Offline;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.App
{
    public sealed class InteractionLinePlayerTests
    {
        FakeInteractionSignals _signals;
        InteractionTriggers _triggers;
        RecordingOfflineResponder _responder;
        RecordingCharacterController _character;
        CancellationTokenSource _app;
        InteractionLinePlayer _player;

        [SetUp]
        public void SetUp()
        {
            _signals = new FakeInteractionSignals();
            _triggers = new InteractionTriggers(_signals, new FakeClock(), () => true);
            _responder = new RecordingOfflineResponder { Hold = true };
            _character = new RecordingCharacterController();
            _app = new CancellationTokenSource();
            _player = new InteractionLinePlayer(_triggers, _responder, new ConversationStateMachine(), _character,
                new RecordingLog(), _app.Token);
        }

        [TearDown]
        public void TearDown()
        {
            _player.Dispose();
            _triggers.Dispose();
            _app.Dispose();
        }

        [Test]
        public void LineFinished_HandsTheCharacterBackToIdle()
        {
            _signals.RaisePoke();
            _responder.Release();

            Assert.That(_character.States, Is.EqualTo(new[] { ConversationState.Speaking, ConversationState.Idle }));
        }

        [Test]
        public void AppCancelledMidLine_LeavesTheCharacterAlone()
        {
            _signals.RaisePoke();
            _app.Cancel();

            Assert.That(_character.States, Is.EqualTo(new[] { ConversationState.Speaking }));
        }
    }
}
