using Jolly.Core.Speech;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Speech
{
    public sealed class SpokenTextTests
    {
        [TestCase("It's sunny today, about 12 degrees.")]
        [TestCase("A well-known café in Kraków, open 9 to 5.")]
        [TestCase("It's -2 degrees and 40 km/h winds.")]
        public void Clean_PlainSentence_IsUnchanged(string sentence)
        {
            // Act
            var clean = SpokenText.Clean(sentence);

            // Assert
            Assert.That(clean, Is.EqualTo(sentence));
        }

        [TestCase("**Muse Spark** was announced on **Wednesday**.", "Muse Spark was announced on Wednesday.")]
        [TestCase("It is __really__ `cold` today.", "It is really cold today.")]
        [TestCase("## Weather today", "Weather today")]
        [TestCase("- Highs around 12 degrees", "Highs around 12 degrees")]
        [TestCase("2. Bring an umbrella.", "Bring an umbrella.")]
        [TestCase("1.", "")]
        [TestCase("> Light rain later.", "Light rain later.")]
        public void Clean_Markdown_IsRemoved(string sentence, string expected)
        {
            // Act
            var clean = SpokenText.Clean(sentence);

            // Assert
            Assert.That(clean, Is.EqualTo(expected));
        }

        [TestCase("Rain is likely ([bbc.co.uk](https://www.bbc.co.uk/weather/2643743)).", "Rain is likely.")]
        [TestCase("See [the forecast](https://metoffice.gov.uk) for details.", "See the forecast for details.")]
        [TestCase("It opens at nine, see https://example.com/hours. ", "It opens at nine, see.")]
        [TestCase("Details are on www.example.com today.", "Details are on today.")]
        [TestCase("It won 2–1 [1][2].", "It won 2–1.")]
        [TestCase("It won 2–1 【3†source】.", "It won 2–1.")]
        [TestCase("Highs of 12 degrees (metoffice.gov.uk).", "Highs of 12 degrees.")]
        public void Clean_LinksUrlsAndCitations_AreNotRead(string sentence, string expected)
        {
            // Act
            var clean = SpokenText.Clean(sentence);

            // Assert
            Assert.That(clean, Is.EqualTo(expected));
        }

        [TestCase("| Day | High |", "Day, High")]
        [TestCase("|---|---|", "")]
        public void Clean_TableRows_BecomeAListOrNothing(string sentence, string expected)
        {
            // Act
            var clean = SpokenText.Clean(sentence);

            // Assert
            Assert.That(clean, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Clean_Empty_IsReturnedAsIs(string sentence)
        {
            // Act
            var clean = SpokenText.Clean(sentence);

            // Assert
            Assert.That(clean, Is.EqualTo(sentence));
        }
    }
}
