using System;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Combat;
using CC.Core.Run;
using CC.Core.Stage;
using UnityEngine;

namespace CC.Data
{
    /// <summary>
    /// Root of all design data. Picks the biome for a stage, turns data into Core parameters and is the
    /// catalog for what the run can hand out (<see cref="IPartyCatalog"/>).
    /// Rebuilt by the menu item "Chaining Chronicle/Build Content".
    /// </summary>
    [CreateAssetMenu(menuName = "Chaining Chronicle/Game Content", fileName = "GameContent")]
    public sealed class GameContent : ScriptableObject, IPartyCatalog
    {
        public BiomeDef[] biomes;
        /// <summary>The biome changes every this many stages and then cycles through the list.</summary>
        public int stagesPerBiome = 3;
        public RuntimeAnimatorController playerAnimator;
        public RuntimeAnimatorController pickupAnimator;

        [Header("Party")]
        public PartyMemberDef hero;
        public PartyMemberDef[] recruits;
        [Tooltip("Skills that a chain skill power-up can teach.")]
        public SkillDef[] chainSkills;
        [Tooltip("Predefined pool of names for found party members (GDD 3.3).")]
        public string[] namePool;

        [Header("Combat")]
        [Tooltip("Most actions in one chain, the starting action included (A12).")]
        [Min(1)] public int chainMax = 5;
        [Tooltip("GDD TODO: a character may use the same skill only once per chain.")]
        public bool sameSkillOncePerCharacter;
        [Tooltip("Chance that fleeing works. 1 = always (A11).")]
        [Range(0f, 1f)] public float fleeChance = 1f;
        [Tooltip("Other enemies on the stage join a fight when they are this many walking steps from the player (A36).")]
        [Min(0)] public int joinRadius = 5;
        [Tooltip("Most enemies in one fight, the engaged one included (A36).")]
        [Min(1)] public int maxEnemiesInCombat = 4;

        private Dictionary<string, EnemySpec> _enemySpecs;
        private List<PartyMemberSpec> _recruitSpecs;
        private List<Skill> _chainSkills;

        public BiomeDef BiomeFor(int stageNumber)
        {
            if (biomes == null || biomes.Length == 0)
                throw new InvalidOperationException("GameContent has no biomes");
            int block = (Mathf.Max(stageNumber, 1) - 1) / Mathf.Max(stagesPerBiome, 1);
            return biomes[block % biomes.Length];
        }

        public StageGenerationParams ParamsFor(int stageNumber) =>
            StageRules.Create(stageNumber, BiomeFor(stageNumber).EnemyIdsFor(stageNumber));

        public ChainConfig CreateChainConfig() => new ChainConfig(Mathf.Max(chainMax, 1), sameSkillOncePerCharacter);

        /// <summary>The hero of a new run.</summary>
        public PartyMember CreateHero(string characterName)
        {
            if (hero == null) throw new InvalidOperationException("GameContent has no hero");
            return new PartyMember(hero.ToSpec(), characterName);
        }

        /// <summary>Definition (animator, tint) of a party member template, the hero included. Null if unknown.</summary>
        public PartyMemberDef MemberDefFor(string templateId)
        {
            if (hero != null && (hero.id == templateId || hero.name == templateId)) return hero;
            return recruits?.FirstOrDefault(r => r != null && (r.id == templateId || r.name == templateId));
        }

        /// <summary>Core copy of an enemy type, by its id. Throws for unknown ids.</summary>
        public EnemySpec EnemySpecFor(string typeId)
        {
            if (_enemySpecs == null)
            {
                _enemySpecs = new Dictionary<string, EnemySpec>();
                foreach (var biome in biomes ?? new BiomeDef[0])
                    foreach (var enemy in biome.enemies ?? new EnemyDef[0])
                        if (enemy != null && !_enemySpecs.ContainsKey(enemy.id)) _enemySpecs[enemy.id] = enemy.ToSpec();
            }

            if (!_enemySpecs.TryGetValue(typeId, out var spec))
                throw new KeyNotFoundException($"No enemy definition with id '{typeId}'");
            return spec;
        }

        public IReadOnlyList<PartyMemberSpec> RecruitSpecs =>
            _recruitSpecs ??= (recruits ?? new PartyMemberDef[0]).Where(r => r != null).Select(r => r.ToSpec()).ToList();

        public IReadOnlyList<Skill> ChainSkills =>
            _chainSkills ??= (chainSkills ?? new SkillDef[0]).Where(s => s != null).Select(s => s.Core).ToList();

        public IReadOnlyList<string> Names => namePool ?? new string[0];

        private void OnValidate()
        {
            _enemySpecs = null;
            _recruitSpecs = null;
            _chainSkills = null;
        }
    }
}
