using System;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.Character
{
    public sealed class FaceLogicTests
    {
        const float Frame = 1f / 72f;
        static readonly FaceSettings Face = new();

        [Test]
        public void BlinkScheduler_BlinksAfterTheDrawnInterval_ThenOpens()
        {
            var random = new FakeRandom().EnqueueFloat(3f, 0.9f, 4f);   // interval, no double, next interval
            var blink = new BlinkScheduler(random, Face);

            var beforeDue = Run(blink, 2.9f);
            var during = Run(blink, 0.3f);
            var after = Run(blink, 0.5f);

            Assert.That(beforeDue.max, Is.Zero);
            Assert.That(during.max, Is.EqualTo(1f));
            Assert.That(after.max, Is.Zero, "next blink only after the next 4 s interval");
        }

        [Test]
        public void BlinkScheduler_DoubleBlink_SecondFollowsAfterTheGap()
        {
            var random = new FakeRandom().EnqueueFloat(2f, 0.05f, 5f);  // interval, double (< 0.15), next interval
            var blink = new BlinkScheduler(random, Face);

            Run(blink, 2f + Face.blinkCloseSeconds + Face.blinkHoldSeconds + Face.blinkOpenSeconds + Frame);
            var second = Run(blink, Face.doubleBlinkGap + 0.2f);

            Assert.That(second.max, Is.EqualTo(1f));
        }

        [Test]
        public void MouthSmoother_AttackFasterThanRelease()
        {
            var mouth = new MouthSmoother(Face);
            var open = new MouthShape(1f, 0f, 0f, 0f, 0f, 1f);

            mouth.Step(open, 0.06f);
            var afterAttack = mouth.A;
            mouth.Step(MouthShape.Closed, 0.06f);
            var droppedBy = afterAttack - mouth.A;

            Assert.That(afterAttack, Is.EqualTo(1f - Math.Exp(-1)).Within(1e-4f));
            Assert.That(droppedBy, Is.LessThan(afterAttack * (float)(1f - Math.Exp(-1))));
        }

        [Test]
        public void FacePresetTable_ParsesBlenderJson()
        {
            const string json = "{\"loopingClips\":[\"Idle\"],\"presets\":[{\"clip\":\"Happy\",\"weights\":" +
                                "[{\"shape\":\"Smile\",\"weight\":1.0},{\"shape\":\"Happy_Eyes\",\"weight\":0.5}]},{\"clip\":\"Idle\",\"weights\":[]}]}";

            var table = FacePresetTable.Parse(json);

            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(table.ForClip("Happy")[(int)FaceChannel.Smile], Is.EqualTo(1f));
            Assert.That(table.ForClip("Happy")[(int)FaceChannel.HappyEyes], Is.EqualTo(0.5f));
            Assert.That(table.ForClip("Idle"), Has.All.Zero);
            Assert.That(table.ForClip("Walk"), Is.Null);
        }

        [Test]
        public void FacePresetTable_UnknownShape_Throws()
        {
            const string json = "{\"presets\":[{\"clip\":\"Idle\",\"weights\":[{\"shape\":\"Jaw\",\"weight\":1}]}]}";

            Assert.That(() => FacePresetTable.Parse(json), Throws.InstanceOf<FormatException>());
        }

        [Test]
        public void FaceMixer_HappyEyes_SuppressBlinks()
        {
            var mixer = new FaceMixer(Face);
            var preset = new float[FaceChannels.Count];
            preset[(int)FaceChannel.HappyEyes] = 1f;

            mixer.Mix(preset, MoodFace.For(Mood.Neutral), new MouthSmoother(Face), blink: 1f, deltaTime: 1f);

            Assert.That(mixer.Output[(int)FaceChannel.BlinkLeft], Is.Zero);
            Assert.That(mixer.Output[(int)FaceChannel.HappyEyes], Is.EqualTo(1f));
        }

        [Test]
        public void FaceMixer_LipSync_ReplacesThePresetMouth()
        {
            var mixer = new FaceMixer(Face);
            var think = new float[FaceChannels.Count];
            think[(int)FaceChannel.U] = 0.6f;
            var mouth = new MouthSmoother(Face);
            mouth.Step(new MouthShape(1f, 0f, 0f, 0f, 0f, 1f), 10f);

            mixer.Mix(think, MoodFace.For(Mood.Neutral), mouth, blink: 0f, deltaTime: 1f);

            Assert.That(mixer.Output[(int)FaceChannel.A], Is.EqualTo(1f));
            Assert.That(mixer.Output[(int)FaceChannel.U], Is.Zero.Within(1e-4f));
        }

        [Test]
        public void FaceMixer_MoodAndClip_TakeTheStrongerPerChannel()
        {
            var mixer = new FaceMixer(Face);
            var wave = new float[FaceChannels.Count];
            wave[(int)FaceChannel.Smile] = 0.8f;

            mixer.Mix(wave, MoodFace.For(Mood.Concerned), new MouthSmoother(Face), blink: 0f, deltaTime: 1f);

            Assert.That(mixer.Output[(int)FaceChannel.Smile], Is.EqualTo(0.8f));
            Assert.That(mixer.Output[(int)FaceChannel.Frown], Is.EqualTo(0.6f));
        }

        static (float max, float last) Run(BlinkScheduler blink, float seconds)
        {
            var max = 0f;
            var last = 0f;
            for (var t = 0f; t < seconds; t += Frame)
            {
                last = blink.Tick(Frame);
                max = Math.Max(max, last);
            }
            return (max, last);
        }
    }
}
