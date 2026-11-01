using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Presentation;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Presentation
{
    public sealed class CaptionViewTests
    {
        FakeConversationEvents _events;
        FakeUserPreferences _preferences;
        FakeClock _clock;
        CaptionView _view;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _events = new FakeConversationEvents();
            _preferences = new FakeUserPreferences();
            _clock = new FakeClock();
            _view = PresentationAssets.Instantiate<CaptionView>("Captions");
            _root = _view.transform.root.gameObject;
            _view.Construct(_events, _preferences, _clock, new EchoLocalizer());
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        [UnityTest]
        public IEnumerator NubiCaption_AppearsAtItsFirstSample_AndHidesAfterItsReadingTime() => UniTask.ToCoroutine(async () =>
        {
            const string sentence = "The red button on top turns it on.";   // 34 chars → 2.5 s + 34 × 0.06 s = 4.54 s

            _events.RaiseCaption(1, sentence);
            Assert.That(_view.NubiText, Is.EqualTo(sentence));

            _clock.Advance(TimeSpan.FromSeconds(4.4));
            await PresentationAssets.AfterUpdate();
            Assert.That(_view.NubiText, Is.EqualTo(sentence), "still within the reading time");

            _clock.Advance(TimeSpan.FromSeconds(0.2));
            await PresentationAssets.AfterUpdate();
            Assert.That(_view.NubiText, Is.Empty);
        });

        [UnityTest]
        public IEnumerator NextSentence_ReplacesTheLine_AndRestartsItsHold() => UniTask.ToCoroutine(async () =>
        {
            _events.RaiseCaption(1, "First.");
            _clock.Advance(TimeSpan.FromSeconds(2));
            _events.RaiseCaption(2, "Second sentence.");
            _clock.Advance(TimeSpan.FromSeconds(2));
            await PresentationAssets.AfterUpdate();

            Assert.That(_view.NubiText, Is.EqualTo("Second sentence."));
        });

        [UnityTest]
        public IEnumerator UserPartials_StayUntilTheFinal_ThenTheFinalHidesAfterItsHold() => UniTask.ToCoroutine(async () =>
        {
            _events.RaiseState(ConversationState.Idle, ConversationState.Listening);
            _events.RaisePartial("Hi");
            _events.RaisePartial("Hi Nubi come");
            _clock.Advance(TimeSpan.FromSeconds(30));
            await PresentationAssets.AfterUpdate();
            Assert.That(_view.UserText, Is.EqualTo("Hi Nubi come"), "partials never expire while listening");

            _events.RaiseFinal("Hi Nubi, come here.");
            _clock.Advance(TimeSpan.FromSeconds(3.9));
            await PresentationAssets.AfterUpdate();
            Assert.That(_view.UserText, Is.EqualTo("Hi Nubi, come here."));

            _clock.Advance(TimeSpan.FromSeconds(0.2));
            await PresentationAssets.AfterUpdate();
            Assert.That(_view.UserText, Is.Empty);
        });

        [Test]
        public void TalkStart_ShowsListening_ThenTheFirstPartialReplacesIt()
        {
            _events.RaiseFinal("What is this?");

            _events.RaiseState(ConversationState.Idle, ConversationState.Listening);
            Assert.That(_view.UserText, Is.EqualTo("[en] caption.listening"), "the last turn's words give way at once");

            _events.RaisePartial("Which");

            Assert.That(_view.UserText, Is.EqualTo("Which"));
        }

        [TestCase(ConversationState.Idle)]
        [TestCase(ConversationState.Speaking)]
        public void NothingHeard_TheListeningCueGoesWithTheListening(ConversationState next)
        {
            _events.RaiseState(ConversationState.Idle, ConversationState.Listening);

            _events.RaiseState(ConversationState.Listening, next);

            Assert.That(_view.UserText, Is.Empty);
        }

        [Test]
        public void LeavingListening_KeepsWhatWasHeard()
        {
            _events.RaiseState(ConversationState.Idle, ConversationState.Listening);
            _events.RaiseFinal("Come here.");

            _events.RaiseState(ConversationState.Listening, ConversationState.Thinking);

            Assert.That(_view.UserText, Is.EqualTo("Come here."));
        }

        [Test]
        public void NewTurn_ClearsTheInterruptedNubiLine()
        {
            _events.RaiseCaption(1, "A long answer that gets interrupted");

            _events.RaiseState(ConversationState.Speaking, ConversationState.Listening);

            Assert.That(_view.NubiText, Is.Empty);
        }

        [Test]
        public void CaptionSize_FollowsThePreference()
        {
            var small = _view.FontSize;

            _preferences.Set(new UserPreferencesSnapshot { CaptionSize = 2 });

            Assert.That(_view.FontSize, Is.GreaterThan(small));
        }

        [UnityTest]
        public IEnumerator Destroy_UnsubscribesFromEveryEvent() => UniTask.ToCoroutine(async () =>
        {
            Assert.That(_events.SubscriberCount, Is.GreaterThan(0));

            Object.Destroy(_root);
            await UniTask.NextFrame();

            Assert.That(_events.SubscriberCount, Is.Zero);
            Assert.That(_preferences.SubscriberCount, Is.Zero);
        });
    }
}
