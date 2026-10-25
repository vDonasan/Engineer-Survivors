using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>Monta as cartas de subida de nível (host).</summary>
    public static class OfferGenerator
    {
        const int MaxOptions = 3;
        const float WeightUpgrade = 1.0f;
        const float WeightNew = 0.8f;
        const float WeightAttribute = 0.6f;
        const float AffinityBoost = 1.5f;

        struct Candidate
        {
            public OfferOption Option;
            public float Weight;
        }

        static readonly List<Candidate> Pool = new List<Candidate>(64);
        static readonly List<OfferOption> Picked = new List<OfferOption>(MaxOptions);

        public static LevelUpOffer Generate(World w, int slot, short offerId)
        {
            var p = w.Players[slot];
            var db = w.Db;
            var spec = db.Specialization(p.Specialization);
            Picked.Clear();
            Pool.Clear();

            // Evoluções elegíveis entram sempre.
            for (int i = 0; i < db.EvolutionCount && Picked.Count < MaxOptions; i++)
            {
                var evo = db.Evolution(i);
                if (p.Evolved.Contains(i) || RequirementEvaluator.LevelOf(p, evo.From) == 0) continue;
                if (RequirementEvaluator.Met(w, slot, evo.Requires)) Picked.Add(new OfferOption { Kind = OptionKind.Evolution, Ref = i });
            }

            bool lowHp = p.Hp < p.Stats.Get(StatId.MaxHp) * w.Rules.survivalHpThreshold;
            int kit = KitIndex(db);
            if (lowHp && kit >= 0 && Picked.Count < MaxOptions) Picked.Add(new OfferOption { Kind = OptionKind.Survival, Ref = kit });

            int weapons = 0, components = 0;
            foreach (var a in p.Abilities)
            {
                if (a.Def.Kind == AbilityKind.Weapon) weapons++;
                else if (a.Def.Kind == AbilityKind.Component) components++;
                if (a.Level < a.Def.MaxLevel) Pool.Add(new Candidate { Option = new OfferOption { Kind = OptionKind.Upgrade, Ref = a.AbilityIndex }, Weight = WeightUpgrade });
            }

            for (int i = 0; i < db.AbilityCount; i++)
            {
                var ab = db.Ability(i);
                if (ab.Def.evolutionOnly || ab.Def.exclusive || ab.Kind == AbilityKind.Survival || RequirementEvaluator.LevelOf(p, i) > 0 || EvolvedAway(w, p, i)) continue;
                float boost = ab.Tags.Overlaps(spec.AffinityTags) ? AffinityBoost : 1f;
                if (ab.Kind == AbilityKind.Weapon && weapons < w.Rules.maxWeapons)
                    Pool.Add(new Candidate { Option = new OfferOption { Kind = OptionKind.NewAbility, Ref = i }, Weight = WeightNew * boost });
                else if (ab.Kind == AbilityKind.Component && components < w.Rules.maxComponents)
                    Pool.Add(new Candidate { Option = new OfferOption { Kind = OptionKind.NewAbility, Ref = i }, Weight = WeightNew * boost });
                else if (ab.Kind == AbilityKind.Attribute)
                    Pool.Add(new Candidate { Option = new OfferOption { Kind = OptionKind.Attribute, Ref = i }, Weight = WeightAttribute });
            }

            while (Picked.Count < MaxOptions && Pool.Count > 0)
            {
                float total = 0;
                foreach (var c in Pool) total += c.Weight;
                float r = w.Rng.NextFloat() * total;
                int chosen = Pool.Count - 1;
                for (int i = 0; i < Pool.Count; i++)
                {
                    r -= Pool[i].Weight;
                    if (r < 0) { chosen = i; break; }
                }
                Picked.Add(Pool[chosen].Option);
                Pool.RemoveAt(chosen);
            }

            if (Picked.Count == 0 && kit >= 0) Picked.Add(new OfferOption { Kind = OptionKind.Survival, Ref = kit });
            else if (Picked.Count < MaxOptions && kit >= 0 && !Picked.Contains(new OfferOption { Kind = OptionKind.Survival, Ref = kit }))
                Picked.Add(new OfferOption { Kind = OptionKind.Survival, Ref = kit });

            return new LevelUpOffer { OfferId = offerId, Options = Picked.ToArray() };
        }

        static bool EvolvedAway(World w, PlayerState p, int abilityIndex)
        {
            foreach (int e in p.Evolved) if (w.Db.Evolution(e).From == abilityIndex) return true;
            return false;
        }

        static int KitIndex(GameDatabase db)
        {
            for (int i = 0; i < db.AbilityCount; i++) if (db.Ability(i).Kind == AbilityKind.Survival) return i;
            return -1;
        }
    }
}
