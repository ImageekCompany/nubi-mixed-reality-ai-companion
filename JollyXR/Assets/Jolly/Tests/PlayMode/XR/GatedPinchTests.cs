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

            Assert.That(pinch.Update(Open, gateOpen: true, 0), Is.False);
            Assert.That(pinch.Update(Closed, gateOpen: true, 0), Is.True);
        }

        [Test]
        public void Update_BetweenThresholds_KeepsThePreviousState()
        {
            var pinch = new GatedPinch();

            Assert.That(pinch.Update(Between, true, 0), Is.False, "not closed enough to start");
            pinch.Update(Closed, true, 0);
            Assert.That(pinch.Update(Between, true, 0), Is.True, "not open enough to stop");
            Assert.That(pinch.Update(Open, true, 0), Is.False);
        }

        [Test]
        public void Update_PinchStartedWhileGated_StaysSuppressedUntilReleased()
        {
            var pinch = new GatedPinch();

            Assert.That(pinch.Update(Closed, gateOpen: false, 0), Is.False);
            Assert.That(pinch.Update(Closed, gateOpen: true, 0), Is.False, "the gate opening mid-pinch does not start talking");
            pinch.Update(Open, true, 0);
            Assert.That(pinch.Update(Closed, true, 0), Is.True);
        }

        [Test]
        public void Update_GateClosingMidPinch_KeepsHolding()
        {
            var pinch = new GatedPinch();
            pinch.Update(Closed, true, 0);

            Assert.That(pinch.Update(Closed, gateOpen: false, 0), Is.True, "hovering UI later must not cut the user off");
        }

        [Test]
        public void Update_TrackingDropoutShorterThanTheGrace_KeepsHolding()
        {
            var pinch = new GatedPinch();
            pinch.Update(Closed, true, 0);

            Assert.That(pinch.Update(null, true, 0.01), Is.True);
            Assert.That(pinch.Update(null, true, 0.01 + GatedPinch.DropoutGraceSeconds * 0.5), Is.True);
            Assert.That(pinch.Update(Closed, true, 0.3), Is.True);
            Assert.That(pinch.Update(null, true, 0.31 + GatedPinch.DropoutGraceSeconds * 0.5), Is.True,
                "tracking coming back restarts the grace");
        }

        [Test]
        public void Update_TrackingLostLongerThanTheGrace_Releases()
        {
            var pinch = new GatedPinch();
            pinch.Update(Closed, true, 0);
            pinch.Update(null, true, 0.01);

            Assert.That(pinch.Update(null, true, 0.02 + GatedPinch.DropoutGraceSeconds), Is.False);
        }

        [Test]
        public void Update_TrackingLostWhileNotHeld_Opens()
        {
            var pinch = new GatedPinch();
            pinch.Update(Closed, gateOpen: false, 0);
            pinch.Update(null, true, 0.01);

            Assert.That(pinch.Update(Closed, true, 0.02), Is.True, "a fresh pinch after a dropout is gated anew");
        }
    }
}
