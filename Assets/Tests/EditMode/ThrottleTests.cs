using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class ThrottleTests
    {
        [Test]
        public void ThePassWaitsForTheInterval()
        {
            var t = new Throttle(0.5f);

            Assert.IsTrue(t.TryPass(10f));
            Assert.IsFalse(t.TryPass(10.2f));
            Assert.IsFalse(t.TryPass(10.49f));
            Assert.IsTrue(t.TryPass(10.5f));
        }

        [Test]
        public void ARefusedAttemptDoesNotExtendTheWait()
        {
            var t = new Throttle(1f);
            t.TryPass(0f);
            t.TryPass(0.9f);

            Assert.IsTrue(t.TryPass(1f));
        }

        [Test]
        public void TheFirstEventAlwaysPasses()
        {
            Assert.IsTrue(new Throttle(100f).TryPass(0f));
        }
    }
}
