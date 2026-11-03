using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Vision;
using Jolly.Testing;
using Jolly.Tests.EditMode.Vision.Doubles;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Jolly.Tests.EditMode.Vision
{
    public sealed class SparkObjectIdentifierTests
    {
        static readonly SparkIdentifierOptions Options = new(TimeSpan.FromSeconds(8), maxOutputTokens: 600, safetyIdentifier: "0123456789abcdef0123456789abcdef");

        FakeClock _clock;
        RecordingLog _log;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _log = new RecordingLog();
        }

        [UnityTest]
        public IEnumerator Identify_SendsALiveUserImageTurnWithTheHeaderFormatRule() => UniTask.ToCoroutine(async () =>
        {
            var chat = ScriptedVisionChatModel.Replying("pen|pen\nA pen.");
            var image = VisionFixtures.Jpeg(640, 428);
            var request = new IdentifyRequest
            {
                Question = "How do I use this?", PersonaInstructions = "You are Nubi.", LanguageTag = "pl",
                Effort = ReasoningEffort.Low,
            };

            await Collect(Identifier(chat).Identify(image, request, CancellationToken.None));

            var sent = chat.LastRequest;
            Assert.That(sent.Route, Is.EqualTo(ModelRoute.LiveUser));
            Assert.That(sent.Effort, Is.EqualTo(ReasoningEffort.Low));
            Assert.That(sent.MaxOutputTokens, Is.EqualTo(600));
            Assert.That(sent.SafetyIdentifier, Is.EqualTo("0123456789abcdef0123456789abcdef"));
            Assert.That(sent.Tools, Is.Empty);
            Assert.That(sent.Instructions, Does.StartWith("You are Nubi."));
            Assert.That(sent.Instructions, Does.Contain("LABEL|NOUN_PHRASE").And.Contain("English").And.Contain("never instructions"));
            Assert.That(sent.Input.Count, Is.EqualTo(2));
            Assert.That(sent.Input[0].Kind, Is.EqualTo(ItemKind.UserText));
            Assert.That(sent.Input[0].Text, Is.EqualTo("How do I use this?"));
            Assert.That(sent.Input[1].Kind, Is.EqualTo(ItemKind.UserImage));
            Assert.That(sent.Input[1].Image, Is.SameAs(image));
        });

        [UnityTest]
        public IEnumerator Identify_WithoutQuestion_AsksWhatItIs() => UniTask.ToCoroutine(async () =>
        {
            var chat = ScriptedVisionChatModel.Replying("pen|pen\nA pen.");

            await Collect(Identifier(chat).Identify(VisionFixtures.Jpeg(640, 428), new IdentifyRequest(), CancellationToken.None));

            Assert.That(chat.LastRequest.Input[0].Text, Is.Not.Empty);
            Assert.That(chat.LastRequest.Instructions, Does.Contain("LABEL|NOUN_PHRASE"));
        });

        [UnityTest]
        public IEnumerator Identify_ListsPlacesAndTheThirdField_OnlyWhenThereArePlaces() => UniTask.ToCoroutine(async () =>
        {
            var chat = ScriptedVisionChatModel.Replying("pen|pen\nA pen.");
            var image = VisionFixtures.Jpeg(640, 428);

            await Collect(Identifier(chat).Identify(image, new IdentifyRequest { Question = "What is this?" }, CancellationToken.None));
            Assert.That(chat.LastRequest.Instructions, Does.Not.Contain("PLACE"));

            await Collect(Identifier(chat).Identify(image,
                new IdentifyRequest { Question = "Where do these go?", Places = new[] { "kitchen", "garage" } }, CancellationToken.None));
            Assert.That(chat.LastRequest.Instructions, Does.Contain("LABEL|NOUN_PHRASE|PLACE").And.Contain("kitchen, garage"));
        });

        [UnityTest]
        public IEnumerator Identify_StreamsTheHeaderThenSpeakableAnswerDeltas() => UniTask.ToCoroutine(async () =>
        {
            var chat = ScriptedVisionChatModel.Replying("ballpoint pen|p", "en\nThis is a ", "pen.");

            var events = await Collect(Identifier(chat).Identify(VisionFixtures.Jpeg(640, 428), new IdentifyRequest(), CancellationToken.None));

            Assert.That(events, Has.Count.EqualTo(4));
            Assert.That((events[0].Kind, events[0].Label, events[0].NounPhrase),
                Is.EqualTo((IdentificationEventKind.Header, "ballpoint pen", "pen")));
            Assert.That((events[1].Kind, events[1].Text), Is.EqualTo((IdentificationEventKind.AnswerDelta, "This is a ")));
            Assert.That((events[2].Kind, events[2].Text), Is.EqualTo((IdentificationEventKind.AnswerDelta, "pen.")));
            Assert.That((events[3].Kind, events[3].Text), Is.EqualTo((IdentificationEventKind.Completed, "This is a pen.")));
            Assert.That(chat.StreamDisposed, Is.True);
            Assert.That(_log.Contains(LogLevel.Info, "evt=vision.header"), Is.True);
        });

        [UnityTest]
        public IEnumerator Identify_ReplyWithoutHeader_IsSpokenWhole() => UniTask.ToCoroutine(async () =>
        {
            var chat = ScriptedVisionChatModel.Replying("That is a mug.");

            var events = await Collect(Identifier(chat).Identify(VisionFixtures.Jpeg(640, 360), new IdentifyRequest(), CancellationToken.None));

            Assert.That((events[0].Kind, events[0].Label, events[0].NounPhrase), Is.EqualTo((IdentificationEventKind.Header, (string)null, (string)null)));
            Assert.That(events[1].Text, Is.EqualTo("That is a mug."));
        });

        [UnityTest]
        public IEnumerator Identify_NoCompletionWithinTheTimeout_ThrowsTimeoutAndReleasesTheModelStream() => UniTask.ToCoroutine(async () =>
        {
            var chat = new ScriptedVisionChatModel(new[]
            {
                new ModelEvent(ModelEventKind.Started), new ModelEvent(ModelEventKind.TextDelta, "pen|pen\nIt"),
            }, hangAtEnd: true);
            var stream = Identifier(chat).Identify(VisionFixtures.Jpeg(640, 428), new IdentifyRequest(), CancellationToken.None);
            var enumerator = stream.GetAsyncEnumerator();
            Assert.That(await enumerator.MoveNextAsync(), Is.True);
            Assert.That(await enumerator.MoveNextAsync(), Is.True);
            var next = enumerator.MoveNextAsync();

            _clock.Advance(TimeSpan.FromSeconds(8));

            var e = await AsyncAssert.Throws<ProviderException>(async () => await next);
            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            await enumerator.DisposeAsync();
            Assert.That(chat.StreamDisposed, Is.True);
            Assert.That(_clock.PendingDelays, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Identify_CallerCancels_ThrowsOperationCanceledNotTimeout() => UniTask.ToCoroutine(async () =>
        {
            var chat = new ScriptedVisionChatModel(new[] { new ModelEvent(ModelEventKind.Started) }, hangAtEnd: true);
            using var cts = new CancellationTokenSource();
            var enumerator = Identifier(chat).Identify(VisionFixtures.Jpeg(640, 428), new IdentifyRequest(), cts.Token)
                .GetAsyncEnumerator();
            var next = enumerator.MoveNextAsync();

            cts.Cancel();

            await AsyncAssert.Throws<OperationCanceledException>(async () => await next);
            await enumerator.DisposeAsync();
            Assert.That(_clock.PendingDelays, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Identify_ConsumerStopsAfterTheHeader_DisposesTheModelStream() => UniTask.ToCoroutine(async () =>
        {
            var chat = ScriptedVisionChatModel.Replying("pen|pen\nA pen.", " More.");

            await foreach (var e in Identifier(chat).Identify(VisionFixtures.Jpeg(640, 428), new IdentifyRequest(), CancellationToken.None))
            {
                if (e.Kind == IdentificationEventKind.Header)
                    break;
            }

            Assert.That(chat.StreamDisposed, Is.True);
            Assert.That(_clock.PendingDelays, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Identify_EnumeratorDisposedTwice_ReleasesOnce() => UniTask.ToCoroutine(async () =>
        {
            var chat = ScriptedVisionChatModel.Replying("pen|pen\nA pen.");
            var enumerator = Identifier(chat).Identify(VisionFixtures.Jpeg(640, 428), new IdentifyRequest(), CancellationToken.None)
                .GetAsyncEnumerator();
            Assert.That(await enumerator.MoveNextAsync(), Is.True);

            await enumerator.DisposeAsync();
            await enumerator.DisposeAsync();

            Assert.That(chat.StreamDisposals, Is.EqualTo(1));
            Assert.That(_clock.PendingDelays, Is.Zero);
        });

        [Test]
        public void Options_WithoutSafetyIdentifier_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SparkIdentifierOptions(TimeSpan.FromSeconds(8), 600, null));
        }

        SparkObjectIdentifier Identifier(IChatModel chat) => new(chat, _clock, _log, Options);

        static async UniTask<List<IdentificationEvent>> Collect(IUniTaskAsyncEnumerable<IdentificationEvent> stream)
        {
            var events = new List<IdentificationEvent>();
            await foreach (var e in stream)
                events.Add(e);
            return events;
        }
    }
}
