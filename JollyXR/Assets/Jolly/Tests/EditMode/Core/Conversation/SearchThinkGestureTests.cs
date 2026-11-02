using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Core.Tools;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class SearchThinkGestureTests
    {
        ConversationEvents _events;
        RecordingCharacterController _character;
        SearchThinkGesture _gesture;

        [SetUp]
        public void SetUp()
        {
            _events = new ConversationEvents();
            _character = new RecordingCharacterController();
            _gesture = new SearchThinkGesture(_events, _character);
        }

        [TearDown]
        public void TearDown() => _gesture.Dispose();

        [Test]
        public void WebSearchStarted_PlaysThink()
        {
            // Act
            _events.RaiseToolStarted("ws_1", ToolNames.WebSearch);

            // Assert
            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.Think }));
            Assert.That(_character.GestureTokens[0].IsCancellationRequested, Is.False);
        }

        [Test]
        public void WebSearchCompleted_StopsThink()
        {
            // Arrange
            _events.RaiseToolStarted("ws_1", ToolNames.WebSearch);

            // Act
            _events.RaiseToolCompleted("ws_1", ToolNames.WebSearch, true);

            // Assert
            Assert.That(_character.GestureTokens[0].IsCancellationRequested, Is.True);
        }

        [TestCase(ConversationState.Listening)]
        [TestCase(ConversationState.Idle)]
        public void TurnInterruptedOrOver_StopsThink(ConversationState to)
        {
            // Arrange
            _events.RaiseToolStarted("ws_1", ToolNames.WebSearch);

            // Act
            _events.RaiseStateChanged(ConversationState.Acting, to);

            // Assert
            Assert.That(_character.GestureTokens[0].IsCancellationRequested, Is.True);
        }

        [Test]
        public void SpeakingWhileSearching_KeepsThinking()
        {
            // Arrange
            _events.RaiseToolStarted("ws_1", ToolNames.WebSearch);

            // Act
            _events.RaiseStateChanged(ConversationState.Acting, ConversationState.Speaking);

            // Assert
            Assert.That(_character.GestureTokens[0].IsCancellationRequested, Is.False);
        }

        [Test]
        public void SearchEndsInActing_KeepsPlayingIntoThinkingUntilThinkingEnds()
        {
            // Arrange
            _events.RaiseStateChanged(ConversationState.Thinking, ConversationState.Acting);
            _events.RaiseToolStarted("ws_1", ToolNames.WebSearch);

            // Act
            _events.RaiseToolCompleted("ws_1", ToolNames.WebSearch, true);
            _events.RaiseStateChanged(ConversationState.Acting, ConversationState.Thinking);

            // Assert
            Assert.That(_character.GestureTokens[0].IsCancellationRequested, Is.False, "the same pose is fading in below");

            // Act
            _events.RaiseStateChanged(ConversationState.Thinking, ConversationState.Speaking);

            // Assert
            Assert.That(_character.GestureTokens[0].IsCancellationRequested, Is.True);
        }

        [Test]
        public void SearchEndsWhileSpeaking_StopsThink()
        {
            // Arrange
            _events.RaiseStateChanged(ConversationState.Thinking, ConversationState.Speaking);
            _events.RaiseToolStarted("ws_1", ToolNames.WebSearch);

            // Act
            _events.RaiseToolCompleted("ws_1", ToolNames.WebSearch, true);

            // Assert
            Assert.That(_character.GestureTokens[0].IsCancellationRequested, Is.True);
        }

        [Test]
        public void OtherTool_PlaysNothing()
        {
            // Act
            _events.RaiseToolStarted("c1", ToolNames.ComeHere);

            // Assert
            Assert.That(_character.Gestures, Is.Empty);
        }

        [Test]
        public void CharacterWithoutThink_PlaysNothing()
        {
            // Arrange
            _character.Missing.Add(GestureId.Think);

            // Act
            _events.RaiseToolStarted("ws_1", ToolNames.WebSearch);

            // Assert
            Assert.That(_character.Gestures, Is.Empty);
        }
    }
}
