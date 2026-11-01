using System.Collections.Generic;
using Jolly.Core.Speech;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Speech
{
    public sealed class SentenceSplitterTests
    {
        static List<string> TakeAll(SentenceSplitter splitter)
        {
            var result = new List<string>();
            while (splitter.TryTake(out var sentence))
                result.Add(sentence);
            return result;
        }

        [Test]
        public void TryTake_TerminatorFollowedBySpace_ReturnsSentence()
        {
            var splitter = new SentenceSplitter();

            splitter.Append("Hello there. How are");

            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { "Hello there." }));
            Assert.That(splitter.Flush(), Is.EqualTo("How are"));
        }

        [Test]
        public void TryTake_TerminatorAtEndOfBuffer_WaitsForNextDelta()
        {
            var splitter = new SentenceSplitter();

            splitter.Append("Hello.");
            var early = splitter.TryTake(out _);
            splitter.Append(" World");

            Assert.That(early, Is.False);
            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { "Hello." }));
        }

        [Test]
        public void TryTake_QuestionExclamationAndEllipsis_AreBoundaries()
        {
            var splitter = new SentenceSplitter();

            splitter.Append("Ready? Yes! Hmm… let me think. ");

            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { "Ready?", "Yes!", "Hmm…", "let me think." }));
        }

        [TestCase("Dr. Smith is here. Next", "Dr. Smith is here.")]
        [TestCase("Use a tool, e.g. a spoon. Next", "Use a tool, e.g. a spoon.")]
        [TestCase("Ask Mrs. Brown and Mr. Green. Next", "Ask Mrs. Brown and Mr. Green.")]
        [TestCase("Written by J. K. Rowling. Next", "Written by J. K. Rowling.")]
        public void TryTake_Abbreviations_DoNotSplit(string text, string expected)
        {
            var splitter = new SentenceSplitter();

            splitter.Append(text);

            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void TryTake_DecimalNumbers_DoNotSplit()
        {
            var splitter = new SentenceSplitter();

            splitter.Append("It is 21.5 degrees and costs $3.99 today. Next");

            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { "It is 21.5 degrees and costs $3.99 today." }));
        }

        [Test]
        public void TryTake_ClosingQuoteAfterTerminator_StaysWithSentence()
        {
            var splitter = new SentenceSplitter();

            splitter.Append("He said \"hi.\" Then left");

            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { "He said \"hi.\"" }));
        }

        [Test]
        public void TryTake_IdeographicFullStop_IsNormalisedAndSplits()
        {
            var splitter = new SentenceSplitter();

            splitter.Append("Hi Nubi。How are you");

            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { "Hi Nubi." }));
            Assert.That(splitter.Flush(), Is.EqualTo("How are you"));
        }

        [Test]
        public void TryTake_Newline_IsBoundaryAndBlankLinesAreSkipped()
        {
            var splitter = new SentenceSplitter();

            splitter.Append("First line\n\nSecond line\n");

            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { "First line", "Second line" }));
        }

        [Test]
        public void TryTake_FirstChunkOver60Chars_FlushesAtLastComma()
        {
            var splitter = new SentenceSplitter();

            splitter.Append("Sure, I can help you with that one, and with plenty of other things");

            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { "Sure, I can help you with that one," }));
        }

        [Test]
        public void TryTake_LaterChunkUnder120Chars_DoesNotFlushOnComma()
        {
            var splitter = new SentenceSplitter();
            splitter.Append("Okay. ");
            TakeAll(splitter);

            splitter.Append("Sure, I can help you with that one, and with plenty of other things");

            Assert.That(TakeAll(splitter), Is.Empty);
        }

        [Test]
        public void TryTake_LaterChunkOver120CharsWithoutComma_FlushesAtLastSpace()
        {
            var splitter = new SentenceSplitter();
            splitter.Append("Okay. ");
            TakeAll(splitter);
            var words = string.Join(" ", System.Linq.Enumerable.Repeat("word", 30));

            splitter.Append(words);
            var taken = TakeAll(splitter);

            Assert.That(taken, Has.Count.EqualTo(1));
            Assert.That(taken[0].Length, Is.LessThanOrEqualTo(words.Length));
            Assert.That(taken[0], Does.EndWith("word"));
            Assert.That(splitter.Flush(), Is.EqualTo("word"));
        }

        [Test]
        public void TryTake_NoBreakableCharacter_CapsAt240()
        {
            var splitter = new SentenceSplitter();
            var text = new string('x', 300);

            splitter.Append(text);
            var taken = TakeAll(splitter);

            Assert.That(taken, Is.EqualTo(new[] { new string('x', SentenceSplitter.MaxLength) }));
            Assert.That(splitter.Flush(), Is.EqualTo(new string('x', 60)));
        }

        [Test]
        public void TryTake_SentenceLongerThanCap_IsSplitAtSpaceWithinCap()
        {
            var splitter = new SentenceSplitter();
            splitter.Append("Okay. ");
            TakeAll(splitter);
            var longSentence = string.Join(" ", System.Linq.Enumerable.Repeat("abcdefghi", 40)) + ". End";

            foreach (var c in longSentence)
                splitter.Append(c.ToString());
            var taken = TakeAll(splitter);
            taken.Add(splitter.Flush());

            foreach (var piece in taken)
                Assert.That(piece.Length, Is.LessThanOrEqualTo(SentenceSplitter.MaxLength));
            Assert.That(string.Join(" ", taken), Is.EqualTo(longSentence));
        }

        [Test]
        public void Flush_ReturnsRemainderAndResetsFirstChunkRule()
        {
            var splitter = new SentenceSplitter();
            splitter.Append("Okay. Tail");
            TakeAll(splitter);

            var tail = splitter.Flush();
            splitter.Append("Sure, I can help you with that one, and with plenty of other things");

            Assert.That(tail, Is.EqualTo("Tail"));
            Assert.That(TakeAll(splitter), Is.EqualTo(new[] { "Sure, I can help you with that one," }));
        }

        [Test]
        public void Flush_WhenEmpty_ReturnsEmptyString()
        {
            var splitter = new SentenceSplitter();

            splitter.Append("   ");

            Assert.That(splitter.Flush(), Is.Empty);
        }
    }
}
