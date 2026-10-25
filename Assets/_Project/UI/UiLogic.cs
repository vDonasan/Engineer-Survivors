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
                    case OptionKind.NewAbility: info.Kind = Strings.Get("carta_nova"); break;
                    case OptionKind.Attribute: info.Kind = Strings.Get("carta_atributo"); break;
                    case OptionKind.Survival: info.Kind = Strings.Get("carta_sobrevivencia"); break;
                    case OptionKind.Upgrade:
                        int level = loadout?.LevelOf(o.Ref) ?? 0;
                        info.Kind = Strings.Format("carta_nivel", level, level + 1);
                        info.Change = LevelText(ability, level + 1);
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
            foreach (var r in reqs) if (r.Kind == RequirementKind.AbilityLevel && r.TargetIndex == ability) return true;
            return false;
        }
    }
}
