using System;
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
        const float AffinityBoost = 1.3f;
        /// <summary>Melhorias da arma inicial (e da sua evolução).</summary>
        const float StartBoost = 2.5f;
        /// <summary>Itens que evoluem uma arma do jogador (da arma inicial: mais).</summary>
        const float PartnerBoost = 1.6f, StartPartnerBoost = 2.0f;
        /// <summary>Por habilidade do jogador que divide uma tag com a candidata (teto).</summary>
        const float KinshipStep = 0.12f, KinshipMax = 1.5f;
        /// <summary>Anti-seca: cresce com o quadrado das ofertas sem aparecer (quanto mais tempo, mais rápido volta), com teto.</summary>
        const float DroughtStep = 0.02f, DroughtMax = 8f;
        /// <summary>Garantia: o item há mais ofertas sem aparecer, a partir deste número, entra numa das cartas.</summary>
        const int DroughtGuarantee = 18;
        /// <summary>Parte das cartas que são melhorias: cresce com o que dá para melhorar (teto); atributos têm parte fixa.</summary>
        const float UpgradeShareBase = 0.25f, UpgradeSharePerItem = 0.08f, UpgradeShareMax = 0.5f, AttributeShare = 0.1f;
        /// <summary>Teto do multiplicador de preferência: ninguém domina as cartas.</summary>
        const float MaxBoost = 3.5f;
        /// <summary>Cada melhoria sorteada reduz as outras melhorias nesta oferta (variedade).</summary>
        const float UpgradeDamp = 0.6f;

        struct Candidate
        {
            public OfferOption Option;
            public float Weight;
        }

        static readonly List<Candidate> Pool = new List<Candidate>(64);
        static readonly List<OfferOption> Picked = new List<OfferOption>(MaxOptions);
        static readonly HashSet<int> Core = new HashSet<int>(), Partners = new HashSet<int>(), StartPartners = new HashSet<int>();

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

            Preferences(w, p);
            var since = p.Progress.SinceSeen ??= new int[db.AbilityCount];

            int weapons = 0, components = 0;
            foreach (var a in p.Abilities)
            {
                if (a.Def.Kind == AbilityKind.Weapon) weapons++;
                else if (a.Def.Kind == AbilityKind.Component) components++;
                if (a.Level >= a.Def.MaxLevel || p.Progress.Banned.Contains(a.AbilityIndex)) continue;
                // a arma inicial cresce mais; quem é parceiro de evolução também
                float boost = Core.Contains(a.AbilityIndex) ? StartBoost : Partners.Contains(a.AbilityIndex) ? 1.25f : 1f;
                Pool.Add(new Candidate { Option = new OfferOption { Kind = OptionKind.Upgrade, Ref = a.AbilityIndex }, Weight = WeightUpgrade * boost });
            }

            for (int i = 0; i < db.AbilityCount; i++)
            {
                var ab = db.Ability(i);
                if (ab.Def.evolutionOnly || ab.Def.exclusive || ab.Kind == AbilityKind.Survival || p.Progress.Banned.Contains(i) || RequirementEvaluator.LevelOf(p, i) > 0 || EvolvedAway(w, p, i)) continue;
                float boost = Preference(p, ab, i, spec, since) * RarityWeight(ab.Rarity, w.Elapsed < p.Progress.LuckUntil);
                if (ab.Kind == AbilityKind.Weapon && weapons < w.Rules.maxWeapons)
                    Pool.Add(new Candidate { Option = new OfferOption { Kind = OptionKind.NewAbility, Ref = i }, Weight = WeightNew * boost });
                else if (ab.Kind == AbilityKind.Component && components < w.Rules.maxComponents)
                    Pool.Add(new Candidate { Option = new OfferOption { Kind = OptionKind.NewAbility, Ref = i }, Weight = WeightNew * boost });
                else if (ab.Kind == AbilityKind.Attribute)
                    Pool.Add(new Candidate { Option = new OfferOption { Kind = OptionKind.Attribute, Ref = i }, Weight = WeightAttribute });
            }

            // garantia contra seca: o item mais esquecido (18+ ofertas) ocupa uma das cartas
            int forgotten = -1;
            for (int i = 0; i < Pool.Count; i++)
            {
                if (Pool[i].Option.Kind != OptionKind.NewAbility || since[Pool[i].Option.Ref] < DroughtGuarantee) continue;
                if (forgotten < 0 || since[Pool[i].Option.Ref] > since[Pool[forgotten].Option.Ref]) forgotten = i;
            }
            if (forgotten >= 0 && Picked.Count < MaxOptions)
            {
                Picked.Add(Pool[forgotten].Option);
                Pool.RemoveAt(forgotten);
            }

            // sorteio em dois passos: primeiro o tipo de carta (melhoria, novidade, atributo), depois a carta
            int upgradable = 0;
            foreach (var c in Pool) if (c.Option.Kind == OptionKind.Upgrade) upgradable++;
            float upShare = Math.Min(UpgradeShareMax, UpgradeShareBase + UpgradeSharePerItem * upgradable);
            while (Picked.Count < MaxOptions && Pool.Count > 0)
            {
                float up = 0, fresh = 0, attr = 0;
                foreach (var c in Pool)
                {
                    if (c.Option.Kind == OptionKind.Upgrade) up += c.Weight;
                    else if (c.Option.Kind == OptionKind.Attribute) attr += c.Weight;
                    else fresh += c.Weight;
                }
                float wUp = up > 0 ? upShare : 0, wAttr = attr > 0 ? AttributeShare : 0, wNew = fresh > 0 ? Math.Max(0.1f, 1f - upShare - AttributeShare) : 0;
                float roll = w.Rng.NextFloat() * (wUp + wAttr + wNew);
                OptionKind kind = roll < wUp ? OptionKind.Upgrade : roll < wUp + wAttr ? OptionKind.Attribute : OptionKind.NewAbility;
                float total = kind == OptionKind.Upgrade ? up : kind == OptionKind.Attribute ? attr : fresh;
                float r = w.Rng.NextFloat() * total;
                int chosen = -1;
                for (int i = 0; i < Pool.Count; i++)
                {
                    if (Pool[i].Option.Kind != kind) continue;
                    chosen = i;
                    r -= Pool[i].Weight;
                    if (r < 0) break;
                }
                if (chosen < 0) break;
                Picked.Add(Pool[chosen].Option);
                Pool.RemoveAt(chosen);
                // variedade: cada melhoria sorteada diminui a chance de outra nesta oferta
                if (kind == OptionKind.Upgrade) upShare *= UpgradeDamp;
            }

            // anti-seca: quem podia aparecer e não apareceu ganha peso na próxima; quem apareceu zera
            for (int i = 0; i < since.Length; i++)
            {
                var ab = db.Ability(i);
                if (ab.Kind != AbilityKind.Component && ab.Kind != AbilityKind.Weapon) continue;
                bool shown = false;
                foreach (var o in Picked) if (o.Ref == i && (o.Kind == OptionKind.NewAbility || o.Kind == OptionKind.Upgrade)) shown = true;
                since[i] = shown ? 0 : since[i] + 1;
            }

            if (Picked.Count == 0 && kit >= 0) Picked.Add(new OfferOption { Kind = OptionKind.Survival, Ref = kit });
            else if (Picked.Count < MaxOptions && kit >= 0 && !Picked.Contains(new OfferOption { Kind = OptionKind.Survival, Ref = kit }))
                Picked.Add(new OfferOption { Kind = OptionKind.Survival, Ref = kit });

            return new LevelUpOffer { OfferId = offerId, Options = Picked.ToArray() };
        }

        /// <summary>A raridade controla a chance de aparecer (não o poder).</summary>
        static float RarityWeight(Rarity r, bool lucky) => lucky
            ? (r == Rarity.Comum ? 0.8f : r == Rarity.Incomum ? 1f : r == Rarity.Raro ? 0.95f : 0.85f)
            : (r == Rarity.Comum ? 1f : r == Rarity.Incomum ? 0.85f : r == Rarity.Raro ? 0.7f : 0.55f);

        /// <summary>
        /// Núcleo (arma inicial e sua evolução) e parceiros de evolução das armas do jogador. Um parceiro é
        /// qualquer habilidade pedida por uma evolução cuja arma de origem o jogador tem.
        /// </summary>
        static void Preferences(World w, PlayerState p)
        {
            var db = w.Db;
            Core.Clear(); Partners.Clear(); StartPartners.Clear();
            int start = db.Specialization(p.Specialization).StartingAbility;
            Core.Add(start);
            foreach (int e in p.Evolved) if (db.Evolution(e).From == start) Core.Add(db.Evolution(e).Result);
            for (int e = 0; e < db.EvolutionCount; e++)
            {
                var evo = db.Evolution(e);
                if (p.Evolved.Contains(e) || RequirementEvaluator.LevelOf(p, evo.From) == 0) continue;
                bool fromStart = evo.From == start;
                foreach (var r in evo.Requires)
                {
                    if (r.Kind == RequirementKind.AbilityLevel && r.TargetIndex != evo.From) Add(r.TargetIndex, fromStart);
                    else if (r.Kind == RequirementKind.AnyAbilityLevel) foreach (int t in r.Targets) Add(t, fromStart);
                }
            }
        }

        static void Add(int ability, bool fromStart)
        {
            if (ability < 0) return;
            Partners.Add(ability);
            if (fromStart) StartPartners.Add(ability);
        }

        /// <summary>Preferência orgânica por uma habilidade nova: afinidade, parceria de evolução, parentesco de tags e anti-seca, com teto.</summary>
        static float Preference(PlayerState p, ResolvedAbility ab, int index, ResolvedSpecialization spec, int[] since)
        {
            float m = ab.Tags.Overlaps(spec.AffinityTags) ? AffinityBoost : 1f;
            if (StartPartners.Contains(index)) m *= StartPartnerBoost;
            else if (Partners.Contains(index)) m *= PartnerBoost;
            int kin = 0;
            foreach (var a in p.Abilities) if (a.Def.Tags.Overlaps(ab.Tags)) kin++;
            m *= Math.Min(KinshipMax, 1f + KinshipStep * kin);
            m = Math.Min(MaxBoost, m);
            // a seca não tem teto do preferido: até o item menos ligado à build volta a aparecer
            int k = since[index];
            return m * Math.Min(DroughtMax, 1f + DroughtStep * k * k + 0.1f * k);
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
