using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Vision;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Vision
{
    public sealed class IdentificationStreamParserTests
    {
        [Test]
        public void Header_ThenAnswerDeltas_ThenCompletedWithTheWholeAnswer()
        {
            var events = Run("ballpoint pen|pen\nThis is a pen. ", "Click the top to write.");

            Assert.That(events, Is.EqualTo(new[]
            {
                "Header ballpoint pen|pen", "AnswerDelta This is a pen. ", "AnswerDelta Click the top to write.",
                "Completed This is a pen. Click the top to write.",
            }));
        }

        [Test]
        public void Header_SplitAcrossDeltas_IsEmittedOnceTheNewlineArrives()
        {
            var events = Run("TV re", "mote", "|", "remote", "\r", "\nPress the red button.");

            Assert.That(events, Is.EqualTo(new[]
            {
                "Header TV remote|remote", "AnswerDelta Press the red button.", "Completed Press the red button.",
            }));
        }

        [Test]
        public void Header_NotEmittedBeforeTheNewline()
        {
            var parser = new IdentificationStreamParser();

            parser.Append("moka pot|moka pot");

            Assert.That(parser.TryTake(out _), Is.False);
        }

        [Test]
        public void MissingBar_YieldsAnEmptyHeaderAndTheWholeTextAsTheAnswer()
        {
            var events = Run("It is a pen.", "\nUse it to write.");

            Assert.That(events, Is.EqualTo(new[]
            {
                "Header <null>|<null>", "AnswerDelta It is a pen.\nUse it to write.", "Completed It is a pen.\nUse it to write.",
            }));
        }

        [Test]
        public void StreamEndingInsideTheHeaderLine_StillEmitsTheHeader()
        {
            var events = Run("mug|mug");

            Assert.That(events, Is.EqualTo(new[] { "Header mug|mug", "Completed " }));
        }

        [Test]
        public void StreamEndingWithoutBarOrNewline_IsAllAnswer()
        {
            var events = Run("A white mug.");

            Assert.That(events, Is.EqualTo(new[] { "Header <null>|<null>", "AnswerDelta A white mug.", "Completed A white mug." }));
        }

        [Test]
        public void LeadingBlankLines_AreSkippedBeforeTheHeader()
        {
            var events = Run("\n \n", "pen|pen\nHi.");

            Assert.That(events[0], Is.EqualTo("Header pen|pen"));
        }

        [Test]
        public void MarkdownAndQuotesAroundTheHeaderFields_AreStripped()
        {
            var events = Run("**Thermostat** | \"thermostat\"\nTurn the dial.");

            Assert.That(events[0], Is.EqualTo("Header Thermostat|thermostat"));
        }

        [Test]
        public void EmptyHeaderField_IsNull()
        {
            var events = Run("|remote\nOk.");

            Assert.That(events[0], Is.EqualTo("Header <null>|remote"));
        }

        [Test]
        public void OverlongFirstLine_IsTreatedAsAnswerWithoutWaitingForTheNewline()
        {
            var parser = new IdentificationStreamParser();
            var sentence = new string('x', 130);

            parser.Append(sentence);

            Assert.That(Drain(parser), Is.EqualTo(new[] { "Header <null>|<null>", "AnswerDelta " + sentence }));
        }

        [Test]
        public void Complete_IsIdempotent()
        {
            var parser = new IdentificationStreamParser();
            parser.Append("pen|pen\nHi.");
            parser.Complete();
            Drain(parser);

            parser.Complete();

            Assert.That(parser.TryTake(out _), Is.False);
        }

        static List<string> Run(params string[] deltas)
        {
            var parser = new IdentificationStreamParser();
            var events = new List<string>();
            foreach (var d in deltas)
            {
                parser.Append(d);
                events.AddRange(Drain(parser));
            }
            parser.Complete();
            events.AddRange(Drain(parser));
            return events;
        }

        static List<string> Drain(IdentificationStreamParser parser)
        {
            var events = new List<string>();
            while (parser.TryTake(out var e))
            {
                events.Add(e.Kind switch
                {
                    IdentificationEventKind.Header => $"Header {e.Label ?? "<null>"}|{e.NounPhrase ?? "<null>"}",
                    _ => $"{e.Kind} {e.Text}",
                });
            }
            return events;
        }
    }
}
