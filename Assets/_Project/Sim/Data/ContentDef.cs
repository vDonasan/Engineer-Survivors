using System;

// Definições autorais de conteúdo. Campos públicos em camelCase, arrays em vez de
// listas: compatível com JsonUtility (Unity), System.Text.Json (IncludeFields) e
// com a serialização de ScriptableObjects. Strings são resolvidas para índices e
// enums por GameDatabase.Build.
namespace ES.Sim
{
    [Serializable]
    public class ContentDef
    {
        public RulesDef rules = new RulesDef();
        public BalanceDef balance = new BalanceDef();
        public SpawnDef spawn = new SpawnDef();
        public ArenaDef[] arenas = new ArenaDef[0];
        public SpecializationDef[] specializations = new SpecializationDef[0];
        public SpecialDef[] specials = new SpecialDef[0];
        public AbilityDef[] abilities = new AbilityDef[0];
        public EnemyDef[] enemies = new EnemyDef[0];
        public SynergyDef[] synergies = new SynergyDef[0];
        public EvolutionDef[] evolutions = new EvolutionDef[0];
    }

    [Serializable]
    public class RulesDef
    {
        public float matchSeconds = 600, tickRate = 30;
        public float playerMaxHp = 100, playerSpeed = 4.5f, playerPickup = 1.6f, playerRadius = 0.4f, hitInvuln = 0.5f;
        public float xpA = 5, xpB = 5, xpC = 0.6f;
        public int maxWeapons = 6, maxComponents = 6;
        public float coopChoiceSeconds = 12, choiceInvuln = 2;
        public float affinityInitial = 0.04f, affinityIntermediate = 0.04f;
        public int affinityIntermediateLevel = 4;
        public int capEnemies = 400, capGems = 150, capHostileProjectiles = 200, capCosmeticEvents = 48;
        public float survivalHpThreshold = 0.5f;
        public float eliteHp = 3, eliteSize = 1.3f, eliteDamage = 1.25f, eliteXp = 4, eliteHealChance = 0.25f;
        public float healAmountFraction = 0.3f;
        public float farEnemyDistance = 30;
        public int farEnemyUpdateEvery = 3;
        public float cameraHalfDiagonal = 12;
    }

    [Serializable]
    public class BalanceDef
    {
        public DifficultyProfileDef[] difficulties = new DifficultyProfileDef[0];
        public TeamProfileDef[] teams = new TeamProfileDef[0];
        public CellOverrideDef[] overrides = new CellOverrideDef[0];
    }

    [Serializable]
    public class DifficultyProfileDef
    {
        public string id;
        public float enemyDamage = 1, enemyHp = 1, spawnBudget = 1, spawnRamp = 1;
        /// <summary>Antecipação da composição em minutos; positivo adianta. Somado.</summary>
        public float compositionShiftMinutes;
        public float xp = 1;
        /// <summary>Chance por abate. Somada.</summary>
        public float healDropChance;
        public float regen = 1;
        /// <summary>Somada.</summary>
        public float eliteChance;
        public float telegraph = 1;
        /// <summary>Dano das armas dos jogadores.</summary>
        public float abilityDamage = 1;
    }

    [Serializable]
    public class TeamProfileDef
    {
        public int size;
        public float enemyCount = 1, enemyHp = 1;
        /// <summary>Somada à da dificuldade.</summary>
        public float eliteChance;
        public float xpPerGem = 1;
        public float abilityDamage = 1;
    }

    [Serializable]
    public class CellOverrideDef
    {
        public string difficulty;
        public int teamSize;
        public string param;
        public float value;
    }

    [Serializable]
    public class SpawnDef
    {
        public float baseBudget = 1.2f, rampPerMinute = 0.55f, surgeIntervalSeconds = 60;
        public int surgeCount = 24;
        public float spawnPadding = 2;
        public BracketDef[] brackets = new BracketDef[0];
    }

    [Serializable]
    public class BracketDef
    {
        public float fromMinute;
        public EnemyWeightDef[] weights = new EnemyWeightDef[0];
    }

    [Serializable]
    public class EnemyWeightDef
    {
        public string enemyId;
        public float weight;
    }

    [Serializable]
    public class ArenaDef
    {
        public string id, name;
        public float width, height;
        public RectDef[] obstacles = new RectDef[0];
    }

    [Serializable]
    public class RectDef
    {
        public float x, y, w, h;
    }

    [Serializable]
    public class SpecializationDef
    {
        public string id, name, startingAbility, special, portrait;
        /// <summary>Engenharia exibida no cartão do personagem.</summary>
        public string engineering;
        /// <summary>Fora da tela de escolha (especializações do SP1, usadas em testes e bots).</summary>
        public bool hidden;
        public string[] affinityTags = new string[0];
        public ModifierDef[] advancedAffinity = new ModifierDef[0];
        public string advancedAffinityText;
    }

    [Serializable]
    public class SpecialDef
    {
        public string id, name, kind, description;
        public float cooldown, radius, expandSeconds, damage, stunSeconds, duration;
        public float moveBonus, damageBonus, damageTakenMult = 1, ramDamage, ramKnockback;
        /// <summary>Recarga das armas enquanto ativo (multiplicativo, ex.: −0,4).</summary>
        public float cooldownBonus;
        /// <summary>Tag que recebe o damageBonus (vazio = Mecânica).</summary>
        public string boostTag;
        public string stunTag;
    }

    [Serializable]
    public class AbilityDef
    {
        public string id, name, description, kind, rarity, behavior;
        public string[] tags = new string[0];
        public int maxLevel;
        public bool evolutionOnly;
        /// <summary>Arma de personagem: nunca é oferecida a quem não a tem.</summary>
        public bool exclusive;
        /// <summary>Sons de disparo por variante (evento ProjectileFired, Value = índice).</summary>
        public string[] sfx = new string[0];
        public StatValueDef[] baseStats = new StatValueDef[0];
        /// <summary>levels[i] = mudanças ao chegar no nível i + 2.</summary>
        public LevelDef[] levels = new LevelDef[0];
        /// <summary>Componentes/atributos: modificadores com valor × nível.</summary>
        public ModifierDef[] grants = new ModifierDef[0];
        public ParamDef[] parameters = new ParamDef[0];
    }

    [Serializable]
    public class StatValueDef
    {
        public string stat;
        public float value;
    }

    [Serializable]
    public class LevelDef
    {
        public StatValueDef[] add = new StatValueDef[0];
        public StatValueDef[] mult = new StatValueDef[0];
        public string text;
    }

    [Serializable]
    public class ModifierDef
    {
        public string stat;
        /// <summary>add | mult</summary>
        public string op;
        /// <summary>player | all | self | tag:&lt;Tag&gt; | ability:&lt;id&gt;</summary>
        public string scope;
        public float value;
    }

    [Serializable]
    public class ParamDef
    {
        public string key;
        public float value;
    }

    [Serializable]
    public class EnemyDef
    {
        public string id, name, role, behavior;
        public string[] tags = new string[0];
        public float hp, speed, contactDamage, contactInterval, radius, xp, cost, knockbackResist;
        public float keepMin, keepMax, fireInterval, telegraph, projectileSpeed, projectileDamage;
    }

    [Serializable]
    public class SynergyDef
    {
        public string id, name, description, effect, targetAbility;
        public RequirementDef[] requires = new RequirementDef[0];
        public float n, damageMult, extraChains;
    }

    [Serializable]
    public class EvolutionDef
    {
        public string id, name, description, fromAbility, resultAbility;
        public RequirementDef[] requires = new RequirementDef[0];
        public bool discoverable;
    }

    [Serializable]
    public class RequirementDef
    {
        /// <summary>ability_level | tag_count | specialization | stat_at_least | branch</summary>
        public string kind;
        public string target;
        public float value;
    }
}
