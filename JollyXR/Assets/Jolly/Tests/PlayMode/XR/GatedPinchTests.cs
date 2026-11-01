using Jolly.XR.Voice;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class GatedPinchTests
    {
        const float Closed = 0.01f;
        const float Between = 0.028f;
        const float Open = 0.06f;

        [Test]
        public void Update_PinchClosesWithTheGateOpen_Holds()
        {
            var pinch = new GatedPinch();

            Assert.That(pinch.Update(Open, gateOpen: true), Is.False);
            Assert.That(pinch.Update(Closed, gateOpen: true), Is.True);
        }

        [Test]
        public void Update_BetweenThresholds_KeepsThePreviousState()
        {
            var pinch = new GatedPinch();

            Assert.That(pinch.Update(Between, true), Is.False, "not closed enough to start");
            pinch.Update(Closed, true);
            Assert.That(pinch.Update(Between, true), Is.True, "not open enough to stop");
            Assert.That(pinch.Update(Open, true), Is.False);
        }

        [Test]
        public void Update_PinchStartedWhileGated_StaysSuppressedUntilReleased()
        {
            var pinch = new GatedPinch();

            Assert.That(pinch.Update(Closed, gateOpen: false), Is.False);
            Assert.That(pinch.Update(Closed, gateOpen: true), Is.False, "the gate opening mid-pinch does not start talking");
            pinch.Update(Open, true);
            Assert.That(pinch.Update(Closed, true), Is.True);
        }

        [Test]
        public void Update_GateClosingMidPinch_KeepsHolding()
        {
            var pinch = new GatedPinch();
            pinch.Update(Closed, true);

            Assert.That(pinch.Update(Closed, gateOpen: false), Is.True, "hovering UI later must not cut the user off");
        }

        [Test]
        public void Update_TrackingLost_Releases()
        {
            var pinch = new GatedPinch();
            pinch.Update(Closed, true);

            Assert.That(pinch.Update(null, true), Is.False);
        }
    }
}
