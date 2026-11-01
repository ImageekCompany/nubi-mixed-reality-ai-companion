using System;
using Jolly.Providers.MetaApi.Vision;
using Jolly.Tests.EditMode.Vision.Doubles;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Vision
{
    public sealed class SamOutputParserTests
    {
        [Test]
        public void Parse_PenConcept_ReadsInclusiveBoxConfidenceAndOneBitMask()
        {
            var records = SamOutputParser.Parse(VisionFixtures.SamOutputText(VisionFixtures.PenConcept));

            Assert.That(records, Has.Count.EqualTo(1));
            var pen = records[0];
            Assert.That(pen.Id, Is.EqualTo(0));
            Assert.That(pen.Box, Is.EqualTo(new RectInt(50, 26, 323, 372)));
            Assert.That(pen.Confidence, Is.EqualTo(0.87096774f).Within(1e-6f));
            Assert.That((pen.MaskWidth, pen.MaskHeight), Is.EqualTo((323, 372)));
            Assert.That(pen.Encoding, Is.EqualTo(SamMaskEncoding.OneBit));
            Assert.That(pen.Payload.ToString(), Does.StartWith("!!!#_Fw=L").And.EndWith("pvt!!"));
        }

        [Test]
        public void Parse_WithoutConfidence_ReportsNaN()
        {
            var records = SamOutputParser.Parse(VisionFixtures.SamOutputText(VisionFixtures.MugConcept));

            Assert.That(records[0].Box, Is.EqualTo(new RectInt(353, 122, 189, 131)));
            Assert.That(float.IsNaN(records[0].Confidence), Is.True);
        }

        [Test]
        public void Parse_MultiObjectPointAndBoxPrompt_KeepsOurIdsInOrder()
        {
            var records = SamOutputParser.Parse(VisionFixtures.Sam(VisionFixtures.PenPointBoxLossless));

            Assert.That(records, Has.Count.EqualTo(2));
            Assert.That(records[0].Id, Is.EqualTo(1));
            Assert.That(records[0].Box, Is.EqualTo(new RectInt(368, 214, 194, 174)));
            Assert.That(records[1].Id, Is.EqualTo(2));
            Assert.That(records[1].Box, Is.EqualTo(new RectInt(48, 25, 326, 374)));
            Assert.That(records[1].Encoding, Is.EqualTo(SamMaskEncoding.Lossless));
        }

        [TestCase("")]
        [TestCase("\n")]
        [TestCase(null)]
        public void Parse_EmptyOutput_IsNoMatchNotAnError(string text)
        {
            Assert.That(SamOutputParser.Parse(text), Is.Empty);
        }

        [Test]
        public void Parse_SingleRowAndColumnBox_IsOnePixel()
        {
            const string text = "<0f>7<|box;x1=5;y1=6;x2=5;y2=6;w=10;h=10|><|mask;x=0;y=0;data=1,1,!!!!!%|>\n";

            var records = SamOutputParser.Parse(text);

            Assert.That(records[0].Id, Is.EqualTo(7));
            Assert.That(records[0].Box, Is.EqualTo(new RectInt(5, 6, 1, 1)));
        }

        [Test]
        public void Parse_PayloadMayContainTheEncodingMarkers()
        {
            const string text = "<0f>0<|box;x1=0;y1=0;x2=1;y2=0;w=2;h=1|><|mask;x=0;y=0;data=1,2,!~!~|>";

            var record = SamOutputParser.Parse(text)[0];

            Assert.That(record.Encoding, Is.EqualTo(SamMaskEncoding.OneBit));
            Assert.That(record.Payload.ToString(), Is.EqualTo("~!~"));
        }

        [TestCase("<0f>0<|box;x1=0;y1=0;x2=9;y2=9;w=10;h=10|><|mask;x=0;y=0;data=9,10,!abc|>", TestName = "mask height differs from box")]
        [TestCase("<0f>0<|box;x1=0;y1=0;x2=9;y2=9;w=10;h=10|><|mask;x=0;y=0;data=10,9,!abc|>", TestName = "mask width differs from box")]
        [TestCase("<0f>0<|box;x1=0;y1=0;x2=9;y2=9;w=10;h=10|>", TestName = "missing mask")]
        [TestCase("<0f>0<|box;x1=0;y1=0;x2=9;w=10;h=10|><|mask;x=0;y=0;data=10,10,!abc|>", TestName = "missing y2")]
        [TestCase("<0f>0<|box;x1=5;y1=0;x2=4;y2=9;w=10;h=10|><|mask;x=0;y=0;data=10,0,!abc|>", TestName = "inverted box")]
        [TestCase("<0f>0<|box;x1=0;y1=0;x2=9;y2=9;w=10;h=10|><|mask;x=0;y=0;data=10,10,?abc|>", TestName = "unknown encoding")]
        [TestCase("<0f>0<|box;x1=0;y1=0;x2=9;y2=9;w=10;h=10|><|mask;x=0;y=0;data=10,10,!abc", TestName = "unterminated mask")]
        [TestCase("0<|box;x1=0;y1=0;x2=9;y2=9;w=10;h=10|><|mask;x=0;y=0;data=10,10,!abc|>", TestName = "missing frame marker")]
        [TestCase("<0f>x<|box;x1=0;y1=0;x2=9;y2=9;w=10;h=10|><|mask;x=0;y=0;data=10,10,!abc|>", TestName = "non-numeric id")]
        public void Parse_MalformedOutput_ThrowsFormatException(string text)
        {
            Assert.Throws<FormatException>(() => SamOutputParser.Parse(text));
        }
    }
}
