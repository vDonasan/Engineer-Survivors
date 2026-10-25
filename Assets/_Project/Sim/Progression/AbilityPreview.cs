using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>Uma mudança de atributo entre dois níveis (para as cartas e a ficha).</summary>
    public struct StatChange
    {
        public StatId Stat;
        /// <summary>true: bônus concedido ao jogador/armas (ex.: +8% de velocidade); false: atributo da própria arma.</summary>
        public bool Grant;
        public ModOp Op;
        public TemplateScope Scope;
        public TagSet Tags;
        public float From, To;
    }

    /// <summary>
    /// Calcula os atributos de uma habilidade em cada nível, sem modificadores do jogador, para mostrar
    /// "Dano 9 → 12 (+33%)" e "Velocidade +8% → +11%" a partir só do conteúdo.
    /// </summary>
    public static class AbilityPreview
    {
        /// <summary>Valor do atributo da arma no nível (base + níveis; 0 = não tem).</summary>
        public static float StatAt(ResolvedAbility a, int level, StatId stat)
        {
            float add = 0f, mult = 1f;
            for (int i = 0; i < a.Levels.Length && i + 2 <= level; i++)
            {
                foreach (var v in a.Levels[i].Add) if (v.Stat == stat) add += v.Value;
                foreach (var v in a.Levels[i].Mult) if (v.Stat == stat) mult *= 1f + v.Value;
            }
            return (a.BaseStats.GetBase(stat) + add) * mult;
        }

        public static float GrantAt(ModifierTemplate g, int level)
        {
            if (level <= 0) return 0f;
            if (g.PerLevel != null && g.PerLevel.Length > 0) return g.PerLevel[Math.Min(level, g.PerLevel.Length) - 1];
            return g.Value * level;
        }

        /// <summary>O que muda de <paramref name="from"/> para <paramref name="to"/> (0 = ainda não tem).</summary>
        public static List<StatChange> Changes(ResolvedAbility a, int from, int to)
        {
            var list = new List<StatChange>();
            if (a.Kind == AbilityKind.Weapon || from > 0 || a.Grants.Length == 0)
            {
                for (int s = 0; s < StatIds.Count; s++)
                {
                    var id = (StatId)s;
                    float x = from > 0 ? StatAt(a, from, id) : 0f, y = StatAt(a, to, id);
                    if (Math.Abs(y - x) < 1e-4f || (from <= 0 && y == 0)) continue;
                    if (from <= 0 && a.Kind != AbilityKind.Weapon) continue;
                    list.Add(new StatChange { Stat = id, From = x, To = y });
                }
            }
            foreach (var g in a.Grants)
            {
                float x = GrantAt(g, from), y = GrantAt(g, to);
                if (Math.Abs(y - x) < 1e-5f) continue;
                list.Add(new StatChange { Stat = g.Stat, Grant = true, Op = g.Op, Scope = g.Scope, Tags = g.Tags, From = x, To = y });
            }
            return list;
        }
    }
}
