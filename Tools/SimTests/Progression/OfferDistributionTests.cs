using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.SimTests.Progression
{
    /// <summary>
    /// Distribuição das cartas: prefere a arma inicial, os parceiros de evolução e os itens da build, sem
    /// deixar de mostrar o resto (orgânico), e nenhum item some por muito tempo.
    /// </summary>
    public class OfferDistributionTests
    {
        static World Fresh(string spec, ulong seed = 3)
        {
            var w = TestWorlds.CreateOpen(spec: spec, seed: seed);
            w.SpawningEnabled = false;
            return w;
        }

        /// <summary>Gera N ofertas seguidas (sem escolher), contando aparições por habilidade.</summary>
        static Dictionary<int, int> Count(World w, int n, out int offersWithNew, out int offersWithStartUpgrade, List<List<OfferOption>> all = null)
        {
            var counts = new Dictionary<int, int>();
            offersWithNew = 0;
            offersWithStartUpgrade = 0;
            var p = w.Players[0];
            int start = w.Db.Specialization(p.Specialization).StartingAbility;
            for (int k = 0; k < n; k++)
            {
                var offer = OfferGenerator.Generate(w, 0, (short)(k + 1));
                all?.Add(offer.Options.ToList());
                bool anyNew = false;
                foreach (var o in offer.Options)
                {
                    if (o.Kind == OptionKind.Evolution || o.Kind == OptionKind.Survival) continue;
                    counts[o.Ref] = counts.TryGetValue(o.Ref, out int c) ? c + 1 : 1;
                    if (o.Kind == OptionKind.NewAbility) anyNew = true;
                    if (o.Kind == OptionKind.Upgrade && o.Ref == start) offersWithStartUpgrade++;
                }
                if (anyNew) offersWithNew++;
            }
            return counts;
        }

        static IEnumerable<int> OfferableComponents(World w) =>
            Enumerable.Range(0, w.Db.AbilityCount).Where(i =>
            {
                var a = w.Db.Ability(i);
                return a.Kind == AbilityKind.Component && !a.Def.evolutionOnly && !a.Def.exclusive;
            });

        [Test]
        public void EveryItem_ShowsUp_NoneTooRare()
        {
            var w = Fresh("staut");
            const int n = 3000;
            var counts = Count(w, n, out _, out _);
            foreach (int i in OfferableComponents(w))
            {
                float share = counts.TryGetValue(i, out int c) ? (float)c / n : 0f;
                Assert.That(share, Is.GreaterThan(0.06f), $"{w.Db.Ability(i).Def.id} aparece em {share:P1} das ofertas");
            }
        }

        [Test]
        public void NoItem_StaysAwayForLong()
        {
            var w = Fresh("miyuki");
            var all = new List<List<OfferOption>>();
            Count(w, 600, out _, out _, all);
            foreach (int i in OfferableComponents(w))
            {
                int gap = 0, worst = 0;
                foreach (var offer in all)
                {
                    if (offer.Any(o => o.Ref == i && o.Kind == OptionKind.NewAbility)) gap = 0;
                    else worst = Math.Max(worst, ++gap);
                }
                Assert.That(worst, Is.LessThan(30), $"{w.Db.Ability(i).Def.id} ficou {worst} ofertas sem aparecer");
            }
        }

        [Test]
        public void EvolutionPartners_OfStartingWeapon_AppearMoreThanOthers()
        {
            foreach (var spec in new[] { "staut", "miyuki", "giulia", "jean" })
            {
                var w = Fresh(spec);
                int start = w.Db.Specialization(w.Players[0].Specialization).StartingAbility;
                var partners = new HashSet<int>();
                for (int e = 0; e < w.Db.EvolutionCount; e++)
                {
                    var evo = w.Db.Evolution(e);
                    if (evo.From != start) continue;
                    foreach (var r in evo.Requires)
                    {
                        if (r.Kind == RequirementKind.AbilityLevel && r.TargetIndex != start) partners.Add(r.TargetIndex);
                        if (r.Kind == RequirementKind.AnyAbilityLevel) foreach (var t in r.Targets) partners.Add(t);
                    }
                }
                partners.IntersectWith(OfferableComponents(w));
                if (partners.Count == 0) continue;
                var counts = Count(w, 3000, out _, out _);
                var others = OfferableComponents(w).Where(i => !partners.Contains(i)).ToList();
                double partnerAvg = partners.Average(i => counts.TryGetValue(i, out int c) ? c : 0);
                double otherAvg = others.Average(i => counts.TryGetValue(i, out int c) ? c : 0);
                Assert.That(partnerAvg, Is.GreaterThan(otherAvg * 1.3), $"{spec}: parceiros {partnerAvg:0} × outros {otherAvg:0}");
            }
        }

        [Test]
        public void StartingWeapon_AndOwnedItems_Preferred_ButOffersStayVaried()
        {
            var w = Fresh("staut");
            var p = w.Players[0];
            // build no meio da partida: a arma inicial e dois itens
            AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("protoboard"));
            AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("iogurte_80"));
            var counts = Count(w, 2000, out int withNew, out int withStart);
            Assert.That(withStart / 2000f, Is.GreaterThan(0.5f), "melhoria da arma inicial aparece bastante");
            Assert.That(withNew / 2000f, Is.GreaterThan(0.75f), "quase toda oferta ainda traz algo novo");
        }

        [Test]
        public void TagKinship_FavorsItemsLikeTheBuild()
        {
            // parte do item entre as cartas novas: com vários itens de Eletrônica na build, outro item de Eletrônica sobe
            static float NewShare(World w, int item)
            {
                int hits = 0, news = 0;
                for (int k = 0; k < 3000; k++)
                    foreach (var o in OfferGenerator.Generate(w, 0, (short)(k + 1)).Options)
                        if (o.Kind == OptionKind.NewAbility) { news++; if (o.Ref == item) hits++; }
                return news > 0 ? (float)hits / news : 0;
            }
            var bare = Fresh("giulia", 9);
            int target = bare.Db.AbilityIndex("campo_eletromagnetico");
            float before = NewShare(bare, target);
            var w = Fresh("giulia", 9);
            foreach (var id in new[] { "protoboard", "bateria_portatil", "cabo_hdmi" }) { int i = w.Db.AbilityIndex(id); if (i >= 0) AbilitySystem.Grant(w, 0, i); }
            Assert.That(NewShare(w, target), Is.GreaterThan(before), "parentesco de tags");
        }
    }
}
