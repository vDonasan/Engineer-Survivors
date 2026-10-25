using System;
using System.Collections.Generic;
using ES.Sim;

namespace ES.UI
{
    /// <summary>Emoção do retrato a partir do estado da partida (arquivos portrait_&lt;spec&gt;_&lt;emoção&gt;).</summary>
    public static class PortraitMood
    {
        public static string Pick(float hpFraction, PlayerStatus status, MatchPhase phase, float sinceHurt, float sinceHeal, float sinceEvolution, bool allyDowned)
        {
            if (phase == MatchPhase.Victory) return "vitoria";
            if (phase == MatchPhase.Defeat) return "derrota";
            if (status == PlayerStatus.Downed) return "choro";
            if (sinceEvolution < 3f) return "determinacao";
            if (sinceHeal < 1.5f) return "alivio";
            if (hpFraction < 0.3f) return "medo";
            if (allyDowned) return "desespero";
            if (sinceHurt < 0.8f) return "raiva";
            if (hpFraction < 0.6f) return "exaustao";
            return "determinacao";
        }
    }

    /// <summary>O que uma carta de subida de nível mostra.</summary>
    public struct CardInfo
    {
        public string Kind, Name, Rarity, RarityClass, Description, Change, Tags, Hint, IconId;
    }

    public static class LevelUpCards
    {
        public static CardInfo Describe(GameDatabase db, LoadoutTracker loadouts, int slot, OfferOption o)
        {
            var info = new CardInfo();
            var loadout = loadouts != null && slot >= 0 && slot < loadouts.Slots.Length ? loadouts.Slots[slot] : null;
            ResolvedAbility ability = null;
            if (o.Kind == OptionKind.Evolution)
            {
                if (o.Ref < 0 || o.Ref >= db.EvolutionCount) return Unknown();
                var evo = db.Evolution(o.Ref);
                ability = db.Ability(evo.Result);
                info.Kind = Strings.Get("carta_evolucao");
                info.Name = evo.Def.name;
                info.Description = ability.Def.description;
                info.Change = evo.Def.description;
            }
            else
            {
                if (o.Ref < 0 || o.Ref >= db.AbilityCount) return Unknown();
                ability = db.Ability(o.Ref);
                info.Name = ability.Def.name;
                info.Description = ability.Def.description;
                switch (o.Kind)
                {
                    case OptionKind.NewAbility:
                        info.Kind = Strings.Get("carta_nova");
                        info.Change = StatText.Lines(ability, 0, 1);
                        break;
                    case OptionKind.Attribute:
                    {
                        int owned = loadout?.LevelOf(o.Ref) ?? 0;
                        info.Kind = owned > 0 ? Strings.Format("carta_nivel", owned, owned + 1) : Strings.Get("carta_atributo");
                        info.Change = StatText.Lines(ability, owned, owned + 1);
                        break;
                    }
                    case OptionKind.Survival: info.Kind = Strings.Get("carta_sobrevivencia"); break;
                    case OptionKind.Upgrade:
                        int level = loadout?.LevelOf(o.Ref) ?? 0;
                        info.Kind = Strings.Format("carta_nivel", level, level + 1);
                        info.Change = Join(LevelText(ability, level + 1), StatText.Lines(ability, level, level + 1));
                        break;
                }
            }
            info.IconId = "icon_" + ability.Def.id;
            string rarity = (ability.Def.rarity ?? "comum").ToLowerInvariant();
            info.Rarity = Strings.Get("raridade_" + rarity);
            info.RarityClass = "rarity-" + rarity;
            info.Tags = TagList(ability.Def.tags);
            info.Hint = Hints(db, loadouts, o.Kind == OptionKind.Evolution ? -1 : o.Ref);
            info.Change = info.Change ?? "";
            return info;
        }

        static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + "\n" + b;

        static CardInfo Unknown() => new CardInfo { Kind = "", Name = "?", Rarity = "", RarityClass = "rarity-comum", Description = "", Change = "", Tags = "", Hint = "", IconId = null };

        static string LevelText(ResolvedAbility a, int nextLevel)
        {
            int i = nextLevel - 2;
            return i >= 0 && i < a.Levels.Length ? a.Levels[i].Text ?? "" : "";
        }

        public static string TagList(string[] tags)
        {
            if (tags == null || tags.Length == 0) return "";
            var names = new List<string>(tags.Length);
            foreach (var t in tags) names.Add(Enum.TryParse(t, out Tag tag) ? UiContext.TagName(tag) : t);
            return string.Join(" · ", names);
        }

        /// <summary>Sinergias e evoluções que envolvem a habilidade; evolução descobrível ainda oculta aparece como "???".</summary>
        public static string Hints(GameDatabase db, LoadoutTracker loadouts, int ability)
        {
            if (ability < 0) return "";
            var parts = new List<string>();
            for (int i = 0; i < db.SynergyCount; i++)
                if (Mentions(db.Synergy(i).Requires, ability)) parts.Add(Strings.Format("carta_dica_sinergia", db.Synergy(i).Def.name));
            for (int i = 0; i < db.EvolutionCount; i++)
            {
                var e = db.Evolution(i);
                if (!Mentions(e.Requires, ability)) continue;
                bool hidden = e.Discoverable && (loadouts == null || !loadouts.Discovered.Contains(i));
                parts.Add(Strings.Format("carta_dica_evolucao", hidden ? Strings.Get("hud_evolucao_oculta") : e.Def.name));
            }
            return string.Join("\n", parts);
        }

        static bool Mentions(ResolvedRequirement[] reqs, int ability)
        {
            foreach (var r in reqs)
            {
                if (r.Kind == RequirementKind.AbilityLevel && r.TargetIndex == ability) return true;
                if (r.Kind == RequirementKind.AnyAbilityLevel && Array.IndexOf(r.Targets, ability) >= 0) return true;
            }
            return false;
        }
    }
}

namespace ES.UI
{
    /// <summary>Texto das mudanças de atributo por nível: "Dano 9 → 12 (+33%)", "Velocidade de movimento +8% → +11%".</summary>
    public static class StatText
    {
        static readonly System.Globalization.CultureInfo Br = new System.Globalization.CultureInfo("pt-BR");

        public static string Name(StatId s)
        {
            switch (s)
            {
                case StatId.Damage: return "Dano";
                case StatId.Cooldown: return "Recarga";
                case StatId.Range: return "Alcance";
                case StatId.Area: return "Área";
                case StatId.Duration: return "Duração";
                case StatId.ProjectileSpeed: return "Velocidade dos projéteis";
                case StatId.Knockback: return "Empurrão";
                case StatId.Slow: return "Lentidão";
                case StatId.HitInterval: return "Intervalo entre golpes";
                case StatId.Count: return "Quantidade";
                case StatId.Pierce: return "Perfuração";
                case StatId.Chains: return "Saltos";
                case StatId.ChainRange: return "Alcance do salto";
                case StatId.ChainFalloff: return "Perda por salto";
                case StatId.MaxHp: return "Vida máxima";
                case StatId.MoveSpeed: return "Velocidade de movimento";
                case StatId.Regen: return "Regeneração";
                case StatId.PickupRadius: return "Raio de coleta";
                case StatId.Armor: return "Armadura";
                case StatId.DamageTaken: return "Dano recebido";
                case StatId.XpGain: return "Experiência";
                case StatId.Dodge: return "Esquiva";
                default: return s.ToString();
            }
        }

        static string Num(float v) => Math.Round(v, 2).ToString("0.##", Br);
        static string Pct(float v) => (v >= 0 ? "+" : "−") + Math.Round(Math.Abs(v) * 100f, 1).ToString("0.#", Br) + "%";

        static string Scope(StatChange c)
        {
            if (c.Scope == TemplateScope.All) return " (todas as armas)";
            if (c.Scope != TemplateScope.Tag) return "";
            var names = new List<string>();
            for (int t = 0; t < Tags.Count; t++) if (c.Tags.Has((Tag)t)) names.Add(UiContext.TagName((Tag)t));
            return " (armas de " + string.Join("/", names) + ")";
        }

        public static string Line(StatChange c)
        {
            string name = Name(c.Stat);
            if (c.Grant)
            {
                bool percent = c.Op == ModOp.Mult || c.Stat == StatId.XpGain || c.Stat == StatId.Dodge;
                string f = percent ? Pct(c.From) : (c.From >= 0 ? "+" : "") + Num(c.From);
                string t = percent ? Pct(c.To) : (c.To >= 0 ? "+" : "") + Num(c.To);
                string extra = Scope(c);
                return c.From == 0 ? $"{name} {t}{extra}" : $"{name} {f} → {t}{extra}";
            }
            string unit = c.Stat == StatId.Cooldown || c.Stat == StatId.HitInterval ? " s" : "";
            string a = (c.Stat == StatId.Area ? "×" : "") + Num(c.From) + unit, b = (c.Stat == StatId.Area ? "×" : "") + Num(c.To) + unit;
            if (c.From == 0) return $"{name} {b}";
            float rel = (c.To - c.From) / Math.Abs(c.From);
            return $"{name} {a} → {b} ({Pct(rel)})";
        }

        /// <summary>Linhas de mudança entre dois níveis (vazio se nada muda em números).</summary>
        public static string Lines(ResolvedAbility a, int from, int to)
        {
            if (a == null) return "";
            var lines = new List<string>();
            foreach (var c in AbilityPreview.Changes(a, from, to)) lines.Add(Line(c));
            return string.Join("\n", lines);
        }
    }
}
