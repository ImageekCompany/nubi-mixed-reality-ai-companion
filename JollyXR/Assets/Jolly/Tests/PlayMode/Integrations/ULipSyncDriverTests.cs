using System;
using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Integrations.ULipSync;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Integrations
{
    /// <summary>ULipSyncDriver against a real uLipSync component, driven through its onLipSyncUpdate event.</summary>
    public sealed class ULipSyncDriverTests
    {
        GameObject _go;
        uLipSync.uLipSync _source;
        FakeClock _clock;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("lipsync");
            _go.SetActive(false);
            _source = _go.AddComponent<uLipSync.uLipSync>();
            _clock = new FakeClock();
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_go);

        static uLipSync.LipSyncInfo Info(float rawVolume, float volume, float a, float o) => new()
        {
            phoneme = "A", volume = volume, rawVolume = rawVolume,
            phonemeRatios = new Dictionary<string, float> { ["A"] = a, ["O"] = o },
        };

        [Test]
        public void Update_OpensTheMouthByPhonemeRatioTimesVolume()
        {
            using var driver = new ULipSyncDriver(_source, _clock);

            _source.onLipSyncUpdate.Invoke(Info(0.2f, 0.5f, 0.8f, 0.2f));

            Assert.That(driver.Current.A, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(driver.Current.O, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(driver.Current.I, Is.Zero);
            Assert.That(driver.Current.Volume, Is.EqualTo(0.5f));
        }

        [Test]
        public void FrozenRawVolume_ClosesTheMouthAfterTheSilenceWindow()
        {
            using var driver = new ULipSyncDriver(_source, _clock);
            _source.onLipSyncUpdate.Invoke(Info(0.2f, 0.5f, 1f, 0f));

            _clock.Advance(TimeSpan.FromSeconds(ULipSyncDriver.SilenceSeconds / 2));
            _source.onLipSyncUpdate.Invoke(Info(0.2f, 0.5f, 1f, 0f));
            Assert.That(driver.Current.A, Is.GreaterThan(0f), "a repeated result within the window still counts");

            _clock.Advance(TimeSpan.FromSeconds(ULipSyncDriver.SilenceSeconds));
            _source.onLipSyncUpdate.Invoke(Info(0.2f, 0.5f, 1f, 0f));
            Assert.That(driver.Current, Is.EqualTo(MouthShape.Closed));
        }

        [Test]
        public void Dispose_RemovesTheListener()
        {
            var driver = new ULipSyncDriver(_source, _clock);
            driver.Dispose();

            _source.onLipSyncUpdate.Invoke(Info(0.3f, 0.6f, 1f, 0f));

            Assert.That(driver.Current, Is.EqualTo(MouthShape.Closed));
        }
    }
}
