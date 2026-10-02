using System;
using System.Linq;
using CC.Data;
using NUnit.Framework;
using UnityEngine;

namespace CC.Tests.Data
{
    public class GameContentTests
    {
        private GameContent _content;
        private BiomeDef _meadow, _snow;
        private EnemyDef _mouse, _walrus;

        [SetUp]
        public void SetUp()
        {
            _mouse = Enemy("mouse", firstStage: 1);
            _walrus = Enemy("walrus", firstStage: 4);
            _meadow = Biome("meadow", _mouse, _walrus);
            _snow = Biome("snow", _walrus);
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.biomes = new[] { _meadow, _snow };
            _content.stagesPerBiome = 3;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in new UnityEngine.Object[] { _content, _meadow, _snow, _mouse, _walrus })
                UnityEngine.Object.DestroyImmediate(o);
        }

        private static EnemyDef Enemy(string id, int firstStage)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDef>();
            enemy.id = id;
            enemy.firstStage = firstStage;
            return enemy;
        }

        private static BiomeDef Biome(string id, params EnemyDef[] enemies)
        {
            var biome = ScriptableObject.CreateInstance<BiomeDef>();
            biome.id = id;
            biome.enemies = enemies;
            return biome;
        }

        [TestCase(1, "meadow")]
        [TestCase(3, "meadow")]
        [TestCase(4, "snow")]
        [TestCase(6, "snow")]
        [TestCase(7, "meadow")] // cycles
        [TestCase(0, "meadow")] // defensive: stages start at 1
        public void BiomeChangesEveryThreeStagesAndCycles(int stage, string expected)
        {
            Assert.AreEqual(expected, _content.BiomeFor(stage).id);
        }

        [Test]
        public void EnemiesAppearOnlyFromTheirFirstStage()
        {
            CollectionAssert.AreEquivalent(new[] { "mouse" }, _meadow.EnemyIdsFor(1));
            CollectionAssert.AreEquivalent(new[] { "mouse", "walrus" }, _meadow.EnemyIdsFor(4));
        }

        [Test]
        public void ParamsUseTheEnemyPoolOfTheStagesBiome()
        {
            var p = _content.ParamsFor(5); // snow, walrus unlocked
            CollectionAssert.AreEquivalent(new[] { "walrus" }, p.EnemyTypeIds.ToArray());
            Assert.AreEqual(5, p.EnemyLevel);
        }

        [Test]
        public void FindEnemyReturnsNullForUnknownId()
        {
            Assert.AreSame(_mouse, _meadow.FindEnemy("mouse"));
            Assert.IsNull(_meadow.FindEnemy("dragon"));
        }

        [Test]
        public void EmptyContentThrowsClearError()
        {
            _content.biomes = Array.Empty<BiomeDef>();
            Assert.Throws<InvalidOperationException>(() => _content.BiomeFor(1));
        }
    }
}
