using System;
using System.Text.RegularExpressions;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Vision;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Vision
{
    public sealed class SamPromptFormatterTests
    {
        [Test]
        public void Format_PositivePoint_IsOneObjectOnFrameZero()
        {
            var prompt = SegmentPrompt.ForPoints(new[] { new PromptPoint(470, 300) });

            var text = Format(prompt, 640, 428);

            Assert.That(text, Is.EqualTo("<0f>1<|point;x=470;y=300;w=640;h=428|>"));
        }

        [Test]
        public void Format_HandNegatives_FollowThePositiveWithAMinusEach()
        {
            var prompt = SegmentPrompt.ForPoints(new[]
            {
                new PromptPoint(300, 200, positive: false),
                new PromptPoint(320, 180),
                new PromptPoint(350, 260, positive: false),
            });

            var text = Format(prompt, 640, 480);

            Assert.That(text, Is.EqualTo(
                "<0f>1<|point;x=320;y=180;w=640;h=480|>-<|point;x=300;y=200;w=640;h=480|>-<|point;x=350;y=260;w=640;h=480|>"));
        }

        [Test]
        public void Format_EveryTokenCarriesTheEncodedImageSize()
        {
            var prompt = SegmentPrompt.ForPoints(new[]
            {
                new PromptPoint(1, 2), new PromptPoint(3, 4, false), new PromptPoint(5, 6, false),
            });

            var text = Format(prompt, 637, 359);

            Assert.That(Regex.Matches(text, "<\\|point;").Count, Is.EqualTo(3));
            Assert.That(Regex.Matches(text, ";w=637;h=359\\|>").Count, Is.EqualTo(3));
        }

        [Test]
        public void Format_PositiveOutsideTheImage_IsClampedToTheLastPixel()
        {
            var prompt = SegmentPrompt.ForPoints(new[] { new PromptPoint(-5, 900) });

            var text = Format(prompt, 640, 428);

            Assert.That(text, Is.EqualTo("<0f>1<|point;x=0;y=427;w=640;h=428|>"));
        }

        [Test]
        public void Format_NegativeOutsideTheImage_IsDropped()
        {
            var prompt = SegmentPrompt.ForPoints(new[] { new PromptPoint(10, 10), new PromptPoint(640, 5, false) });

            var text = Format(prompt, 640, 428);

            Assert.That(text, Is.EqualTo("<0f>1<|point;x=10;y=10;w=640;h=428|>"));
        }

        [Test]
        public void Format_OnlyNegativePoints_Throws()
        {
            var prompt = SegmentPrompt.ForPoints(new[] { new PromptPoint(10, 10, false) });

            Assert.Throws<ArgumentException>(() => Format(prompt, 640, 428));
        }

        [Test]
        public void Format_Box_UsesInclusiveCorners()
        {
            var prompt = SegmentPrompt.ForBox(new RectInt(50, 26, 323, 372));

            var text = Format(prompt, 640, 428);

            Assert.That(text, Is.EqualTo("<0f>1<|box;x1=50;y1=26;x2=372;y2=397;w=640;h=428|>"));
        }

        [Test]
        public void Format_BoxPastTheEdge_IsClipped()
        {
            var prompt = SegmentPrompt.ForBox(new RectInt(600, 400, 100, 100));

            var text = Format(prompt, 640, 428);

            Assert.That(text, Is.EqualTo("<0f>1<|box;x1=600;y1=400;x2=639;y2=427;w=640;h=428|>"));
        }

        [TestCase("pen", "pen")]
        [TestCase("  Reading   glasses ", "Reading glasses")]
        [TestCase("mug<|box;x1=0|>", "mug box x1 0")]
        [TestCase("tv remote, please", "tv remote please")]
        [TestCase("kid's \"toy\"", "kid's toy")]
        public void Format_Text_IsSanitisedIntoASingleNounPhrase(string phrase, string expected)
        {
            Assert.That(Format(SegmentPrompt.ForText(phrase), 640, 428), Is.EqualTo(expected));
        }

        [Test]
        public void Format_TextLongerThanTheCap_IsCutAtAWordBoundary()
        {
            var text = Format(SegmentPrompt.ForText(new string('a', 30) + " " + new string('b', 40)), 640, 428);

            Assert.That(text, Is.EqualTo(new string('a', 30)));
        }

        [TestCase("")]
        [TestCase(" <|> ")]
        [TestCase("???")]
        [TestCase(null)]
        public void TryFormat_TextWithNothingLeft_IsFalseNotAnException(string phrase)
        {
            var formatted = SamPromptFormatter.TryFormat(SegmentPrompt.ForText(phrase), 640, 428, out var text);

            Assert.That(formatted, Is.False);
            Assert.That(text, Is.Empty);
        }

        static string Format(SegmentPrompt prompt, int width, int height)
        {
            Assert.That(SamPromptFormatter.TryFormat(prompt, width, height, out var text), Is.True);
            return text;
        }
    }
}
