using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Presentation;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Presentation
{
    public sealed class SettingsPanelTests
    {
        EchoLocalizer _localizer;
        FakePersonaProvider _personas;
        FakeUsageMeter _usage;
        InMemoryPreferencesStore _store;
        FakeClock _clock;
        SettingsPanel _panel;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _localizer = new EchoLocalizer();
            _personas = new FakePersonaProvider(FakePersonaProvider.Profile("companion"), FakePersonaProvider.Profile("mentor"),
                FakePersonaProvider.Profile("guru"), FakePersonaProvider.Profile("psychologist"));
            _usage = new FakeUsageMeter(0.53m, 5m);
            _store = new InMemoryPreferencesStore { Stored = new UserPreferencesSnapshot { CaptionSize = 1 } };
            _clock = new FakeClock();
            _panel = PresentationAssets.Instantiate<SettingsPanel>("SettingsPanel");
            _root = _panel.transform.root.gameObject;
            _panel.Construct(_localizer, _personas, _usage, _store, _clock);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        SettingRow Row(string name) => _panel.GetComponentsInChildren<SettingRow>(true).Single(r => r.name == name);
        ChoiceGroup Group(string name) => _panel.GetComponentsInChildren<ChoiceGroup>(true).Single(g => g.name == name);

        [Test]
        public void Construct_LoadsStoredPreferences()
        {
            Assert.That(_panel.Current.CaptionSize, Is.EqualTo(1));
            Assert.That(Row("CaptionSize").Value, Is.EqualTo("[en] settings.size.medium"));
        }

        [Test]
        public void Construct_BringsCorruptStoredValuesBackIntoRange()
        {
            _store.Stored = new UserPreferencesSnapshot { CaptionSize = 7, SpeechRate = 5f, Volume = float.NaN };

            _panel.Construct(_localizer, _personas, _usage, _store, _clock);

            Assert.That(_panel.Current.CaptionSize, Is.EqualTo(SettingsModel.MaxCaptionSize));
            Assert.That(_panel.Current.SpeechRate, Is.EqualTo(SettingsModel.MaxRate));
            Assert.That(_panel.Current.Volume, Is.EqualTo(1f));
        }

        [Test]
        public void Model_Sanitize_SnapsOffGridValues()
        {
            var p = SettingsModel.Sanitize(new UserPreferencesSnapshot { CaptionSize = -1, SpeechRate = 0f, Volume = 0.33f });

            Assert.That(p.CaptionSize, Is.Zero);
            Assert.That(p.SpeechRate, Is.EqualTo(SettingsModel.MinRate));
            Assert.That(p.Volume, Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void Stepping_RaisesChangedWithANewSnapshot_AndPersistsIt()
        {
            var before = _panel.Current;
            UserPreferencesSnapshot raised = null;
            _panel.Changed += p => raised = p;

            Row("SpeechRate").Next.onClick.Invoke();

            Assert.That(raised, Is.Not.Null.And.Not.SameAs(before));
            Assert.That(raised.SpeechRate, Is.EqualTo(1.25f));
            Assert.That(before.SpeechRate, Is.EqualTo(1f), "the previous snapshot is never mutated");
            Assert.That(_store.Stored.SpeechRate, Is.EqualTo(1.25f));
            Assert.That(Row("SpeechRate").Value, Is.EqualTo("1.25×"));
        }

        [Test]
        public void SeatedAndPttHand_ToggleOnEitherButton()
        {
            Row("Seated").Previous.onClick.Invoke();
            Row("PttHand").Next.onClick.Invoke();

            Assert.That(_panel.Current.Seated, Is.True);
            Assert.That(_panel.Current.PttEitherHand, Is.False);
            Assert.That(Row("Seated").Value, Is.EqualTo("[en] settings.on"));
            Assert.That(Row("PttHand").Value, Is.EqualTo("[en] settings.ptt.dominant"));
        }

        [Test]
        public void PersonaButton_RequestsThatPersona_AndTheCurrentOneIsMarked()
        {
            var group = Group("Personas");
            string requested = null;
            _panel.PersonaSelected += id => requested = id;

            Assert.That(group.Buttons.Select(b => b.interactable), Is.EqualTo(new[] { false, true, true, true }), "companion is current");
            group.Buttons[2].onClick.Invoke();

            Assert.That(requested, Is.EqualTo("guru"));
        }

        [Test]
        public void LanguageButton_RequestsThatLanguage()
        {
            string requested = null;
            _panel.LanguageSelected += tag => requested = tag;

            Group("Languages").Buttons[1].onClick.Invoke();

            Assert.That(requested, Is.EqualTo("pl"));
        }

        [Test]
        public void ClearMemory_NeedsASecondTapWithinTheConfirmWindow()
        {
            var cleared = 0;
            _panel.ClearMemoryRequested += () => cleared++;
            var button = _panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "ClearMemory");

            button.onClick.Invoke();
            Assert.That(cleared, Is.Zero);
            Assert.That(_panel.IsConfirmingClear, Is.True);

            _clock.Advance(TimeSpan.FromSeconds(3.5));
            button.onClick.Invoke();
            Assert.That(cleared, Is.Zero, "the first tap expired, so this one only arms again");

            button.onClick.Invoke();
            Assert.That(cleared, Is.EqualTo(1));
        }

        [Test]
        public void Usage_ShowsSpendAgainstTheCap_AndFollowsSpendChanges()
        {
            var usage = _panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t => t.name == "Usage");
            Assert.That(usage.text, Is.EqualTo("[en] settings.usage: $0.53 / $5.00"));

            _usage.Spend(0.25m);

            Assert.That(usage.text, Is.EqualTo("[en] settings.usage: $0.78 / $5.00"));
        }

        [Test]
        public void LanguageChange_RelocalizesEveryLabel()
        {
            _localizer.SetLanguage("pl", default).Forget();

            Assert.That(Row("CaptionSize").Value, Is.EqualTo("[pl] settings.size.medium"));
            Assert.That(Group("Languages").Buttons.Select(b => b.interactable), Is.EqualTo(new[] { true, false }));
        }

        [TestCase(Setting.CaptionSize, 1, 5, 2)]
        [TestCase(Setting.CaptionSize, -1, 5, 0)]
        public void Model_CaptionSizeClamps(Setting setting, int direction, int steps, int expected)
        {
            var p = new UserPreferencesSnapshot();
            for (var i = 0; i < steps; i++)
                p = SettingsModel.Step(p, setting, direction);
            Assert.That(p.CaptionSize, Is.EqualTo(expected));
        }

        [Test]
        public void Model_VolumeSnapsToTenths_AndClamps()
        {
            var values = new List<float>();
            var p = new UserPreferencesSnapshot { Volume = 1f };
            for (var i = 0; i < 12; i++)
            {
                p = SettingsModel.Step(p, Setting.Volume, -1);
                values.Add(p.Volume);
            }
            Assert.That(values[2], Is.EqualTo(0.7f).Within(1e-6f));
            Assert.That(values.Last(), Is.EqualTo(0f));
        }

        [Test]
        public void Model_SpeechRateStaysWithinHalfToDouble()
        {
            var p = new UserPreferencesSnapshot();
            for (var i = 0; i < 10; i++)
                p = SettingsModel.Step(p, Setting.SpeechRate, 1);
            Assert.That(p.SpeechRate, Is.EqualTo(2f));
            for (var i = 0; i < 10; i++)
                p = SettingsModel.Step(p, Setting.SpeechRate, -1);
            Assert.That(p.SpeechRate, Is.EqualTo(0.5f));
        }
    }
}
