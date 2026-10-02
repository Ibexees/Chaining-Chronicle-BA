using CC.Core.Run;
using NUnit.Framework;

namespace CC.Tests.Core.Run
{
    public class RunStateTests
    {
        [Test]
        public void StartsAtStageOne()
        {
            var run = new RunState(123, "Ada");
            Assert.AreEqual(1, run.StageNumber);
            Assert.AreEqual(123, run.Seed);
            Assert.AreEqual("Ada", run.CharacterName);
        }

        [Test]
        public void AdvanceStageCountsUp()
        {
            var run = new RunState(1, "Ada");
            run.AdvanceStage();
            run.AdvanceStage();
            Assert.AreEqual(3, run.StageNumber);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void EmptyNameFallsBackToHero(string name)
        {
            Assert.AreEqual("Hero", new RunState(1, name).CharacterName);
        }

        [Test]
        public void NameIsTrimmed()
        {
            Assert.AreEqual("Ada", new RunState(1, "  Ada ").CharacterName);
        }
    }
}
