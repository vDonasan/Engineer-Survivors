using System.Collections.Generic;
using System;

namespace ES.Sim
{
    public enum AbilityKind { Weapon, Component, Attribute, Survival }
    public enum Rarity { Comum, Incomum, Raro, Reliquia }
    public enum SpecialKind { Blackout, Turbine, Overclock }
    public enum EnemyBehavior { Chase, KiteShoot, Boss }
    public enum RequirementKind { AbilityLevel, TagCount, Specialization, StatAtLeast, Branch, AnyAbilityLevel }
    public enum SynergyEffect { ChargeEveryN }
    public enum TemplateScope { Player, All, Self, Tag, Ability }

    public struct StatValue
    {
        public StatId Stat;
        public float Value;
    }

    public sealed class ResolvedLevel
    {
        public StatValue[] Add;
        public StatValue[] Mult;
        public string Text;
    }

    /// <summary>Modificador ainda sem dono: "self" é resolvido na aplicação.</summary>
    public struct ModifierTemplate
    {
        public StatId Stat;
        public ModOp Op;
        public float Value;
        public TemplateScope Scope;
        public TagSet Tags;
        public int AbilityIndex;
        /// <summary>Valor acumulado por nível (nível 1 = [0]); vazio = Value × nível.</summary>
        public float[] PerLevel;

        public Modifier AtLevel(int level, int sourceId, int selfAbilityIndex)
        {
            if (PerLevel == null || PerLevel.Length == 0) return ToModifier(level, sourceId, selfAbilityIndex);
            var m = ToModifier(1f, sourceId, selfAbilityIndex);
            m.Value = PerLevel[Math.Max(0, Math.Min(level, PerLevel.Length) - 1)];
            return m;
        }

        public Modifier ToModifier(float valueScale, int sourceId, int selfAbilityIndex)
        {
            ModScope scope;
            switch (Scope)
            {
                case TemplateScope.All: scope = ModScope.AllAbilities; break;
                case TemplateScope.Self: scope = ModScope.ForAbility(selfAbilityIndex); break;
                case TemplateScope.Tag: scope = ModScope.ForTags(Tags); break;
                case TemplateScope.Ability: scope = ModScope.ForAbility(AbilityIndex); break;
                default: scope = ModScope.Player; break;
            }
            return new Modifier { Stat = Stat, Op = Op, Value = Value * valueScale, SourceId = sourceId, Scope = scope };
        }
    }

    public sealed class ResolvedAbility
    {
        public int Index;
        public AbilityDef Def;
        public AbilityKind Kind;
        public Rarity Rarity;
        public TagSet Tags;
        public StatBlock BaseStats;
        public ResolvedLevel[] Levels;
        public ModifierTemplate[] Grants;
        public string[] ParamKeys;
        public float[] ParamValues;

        public string Id => Def.id;
        public int MaxLevel => Def.maxLevel;

        public float Param(string key, float fallback)
        {
            for (int i = 0; i < ParamKeys.Length; i++) if (ParamKeys[i] == key) return ParamValues[i];
            return fallback;
        }
    }

    public sealed class ResolvedSpecialization
    {
        public int Index;
        public SpecializationDef Def;
        public int StartingAbility;
        public int Special;
        public TagSet AffinityTags;
        public ModifierTemplate[] AdvancedAffinity;
    }

    public sealed class ResolvedSpecial
    {
        public int Index;
        public SpecialDef Def;
        public SpecialKind Kind;
        public TagSet StunTags;
        /// <summary>Tag que recebe o bônus de dano (Turbina/Overclock).</summary>
        public Tag BoostTag;
    }

    public sealed class ResolvedBoss
    {
        public int Index;
        public BossDef Def;
        public int Enemy;
        readonly Dictionary<string, float> _params = new Dictionary<string, float>();

        public ResolvedBoss(BossDef def)
        {
            Def = def;
            foreach (var p in def.parameters ?? new ParamDef[0]) if (p?.key != null) _params[p.key] = p.value;
        }

        public float Param(string key, float fallback = 0) => _params.TryGetValue(key, out float v) ? v : fallback;

        /// <summary>Na fase BRAVO usa "&lt;chave&gt;Bravo" quando existir.</summary>
        public float Param(string key, bool bravo, float fallback = 0) => bravo && _params.TryGetValue(key + "Bravo", out float b) ? b : Param(key, fallback);
    }

    public sealed class ResolvedEnemy
    {
        public int Index;
        public EnemyDef Def;
        public TagSet Tags;
        public EnemyBehavior Behavior;
    }

    public struct ResolvedRequirement
    {
        public RequirementKind Kind;
        public int TargetIndex;
        /// <summary>AnyAbilityLevel: basta uma destas habilidades no nível pedido.</summary>
        public int[] Targets;
        public Tag Tag;
        public StatId Stat;
        public string Branch;
        public float Value;
    }

    public sealed class ResolvedSynergy
    {
        public int Index;
        public SynergyDef Def;
        public SynergyEffect Effect;
        public int TargetAbility;
        public ResolvedRequirement[] Requires;
    }

    public sealed class ResolvedEvolution
    {
        public int Index;
        public EvolutionDef Def;
        public int From;
        public int Result;
        public ResolvedRequirement[] Requires;
        public bool Discoverable => Def.discoverable;
    }

    public sealed class ResolvedBracket
    {
        public float FromMinute;
        public int[] Enemies;
        public float[] Weights;
        public float TotalWeight;
    }
}
