using Jolly.XR.Hands;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class TrackingDebounceTests
    {
        // Times are exact in binary, so the boundary cases test the rule and not rounding.
        const double Hold = 0.25;

        [Test]
        public void FirstSample_IsTakenAtOnce()
        {
            var debounce = new TrackingDebounce(Hold);

            var changed = debounce.Update(true, 5.0);

            Assert.That(changed, Is.True);
            Assert.That(debounce.Stable, Is.True);
        }

        [Test]
        public void Acquired_ShorterThanTheHold_IsIgnored()
        {
            // The 6179cfc headless runs: the subsystem reported a hand for a moment about once a second.
            var debounce = new TrackingDebounce(Hold);
            debounce.Update(false, 0.0);

            var changes = debounce.Update(true, 1.0) | debounce.Update(true, 1.125) | debounce.Update(false, 1.1875);

            Assert.That(changes, Is.False);
            Assert.That(debounce.Stable, Is.False);
        }

        [Test]
        public void Acquired_HeldForTheHold_Changes()
        {
            var debounce = new TrackingDebounce(Hold);
            debounce.Update(false, 0.0);
            debounce.Update(true, 1.0);

            var changed = debounce.Update(true, 1.25);

            Assert.That(changed, Is.True);
            Assert.That(debounce.Stable, Is.True);
        }

        [Test]
        public void Lost_ShorterThanTheHold_KeepsTheHand()
        {
            var debounce = new TrackingDebounce(Hold);
            debounce.Update(true, 0.0);

            var changes = debounce.Update(false, 1.0) | debounce.Update(false, 1.125) | debounce.Update(true, 1.1875);

            Assert.That(changes, Is.False);
            Assert.That(debounce.Stable, Is.True);
        }

        [Test]
        public void Flicker_RestartsTheHold()
        {
            var debounce = new TrackingDebounce(Hold);
            debounce.Update(false, 0.0);
            debounce.Update(true, 1.0);
            debounce.Update(false, 1.125);
            debounce.Update(true, 1.1875);

            var early = debounce.Update(true, 1.375);
            var held = debounce.Update(true, 1.4375);

            Assert.That(early, Is.False, "0.1875 s since the last flip");
            Assert.That(held, Is.True);
        }

        [Test]
        public void Change_IsReportedOnce()
        {
            var debounce = new TrackingDebounce(Hold);
            debounce.Update(false, 0.0);
            debounce.Update(true, 1.0);
            debounce.Update(true, 1.25);

            var again = debounce.Update(true, 1.5);

            Assert.That(again, Is.False);
            Assert.That(debounce.Stable, Is.True);
        }
    }
}
