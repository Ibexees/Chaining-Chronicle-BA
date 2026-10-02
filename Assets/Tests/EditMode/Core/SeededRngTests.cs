using System;
using CC.Core.Util;
using NUnit.Framework;

namespace CC.Tests.Core
{
    public class SeededRngTests
    {
        [Test]
        public void SameSeedGivesSameSequence()
        {
            var a = new SeededRng(42);
            var b = new SeededRng(42);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.Next(0, 1000), b.Next(0, 1000));
        }

        [Test]
        public void DifferentSeedsGiveDifferentSequences()
        {
            var a = new SeededRng(1);
            var b = new SeededRng(2);
            bool anyDifferent = false;
            for (int i = 0; i < 20; i++)
                anyDifferent |= a.Next(0, 1000000) != b.Next(0, 1000000);
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void NextStaysInRange()
        {
            var rng = new SeededRng(7);
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.Next(-5, 5);
                Assert.GreaterOrEqual(v, -5);
                Assert.Less(v, 5);
            }
        }

        [Test]
        public void NextCoversWholeRange()
        {
            var rng = new SeededRng(3);
            var seen = new bool[6];
            for (int i = 0; i < 1000; i++) seen[rng.Next(0, 6)] = true;
            CollectionAssert.AreEqual(new[] { true, true, true, true, true, true }, seen);
        }

        [Test]
        public void NextFloatIsInUnitInterval()
        {
            var rng = new SeededRng(11);
            for (int i = 0; i < 10000; i++)
            {
                float f = rng.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
            }
        }

        [Test]
        public void ChanceExtremesAreExact()
        {
            var rng = new SeededRng(5);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(rng.Chance(0f));
                Assert.IsTrue(rng.Chance(1f));
            }
        }

        [Test]
        public void InvalidRangeThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SeededRng(1).Next(5, 5));
        }

        [Test]
        public void DeriveIsDeterministicAndIndependentOfParentState()
        {
            var parent = new SeededRng(100);
            var childBefore = parent.Derive(3);
            parent.Next(0, 10); // advancing the parent must not change derived streams
            var childAfter = parent.Derive(3);

            for (int i = 0; i < 20; i++)
                Assert.AreEqual(childBefore.Next(0, 1000), childAfter.Next(0, 1000));
        }

        [Test]
        public void DerivedStreamsWithDifferentSaltDiffer()
        {
            var parent = new SeededRng(100);
            var a = parent.Derive(1);
            var b = parent.Derive(2);
            bool anyDifferent = false;
            for (int i = 0; i < 20; i++)
                anyDifferent |= a.Next(0, 1000000) != b.Next(0, 1000000);
            Assert.IsTrue(anyDifferent);
        }
    }
}
