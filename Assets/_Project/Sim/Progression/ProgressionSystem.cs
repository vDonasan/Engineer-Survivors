using System;

namespace ES.Sim
{
    public static class ProgressionSystem
    {
        /// <summary>XP compartilhado: cada jogador ativo recebe amount × balanceamento.</summary>
        public static void AddSharedXp(World w, float amount)
        {
            if (amount <= 0) return;
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                var pr = p.Progress;
                pr.Xp += amount * w.Balance.Xp;
                int next = XpCurve.ToNext(w.Rules, pr.Level);
                while (pr.Xp >= next)
                {
                    pr.Xp -= next;
                    pr.Level++;
                    pr.PendingLevels++;
                    next = XpCurve.ToNext(w.Rules, pr.Level);
                }
                EnsureOffer(w, p.Slot);
            }
        }

        /// <summary>Abre a próxima oferta se houver nível pendente e nenhuma aberta.</summary>
        public static void EnsureOffer(World w, int slot)
        {
            var p = w.Players[slot];
            var pr = p.Progress;
            if (pr.Current != null || pr.PendingLevels <= 0 || !p.IsActive) return;
            pr.PendingLevels--;
            var offer = OfferGenerator.Generate(w, slot, pr.NextOfferId);
            pr.NextOfferId = (short)(pr.NextOfferId == short.MaxValue ? 1 : pr.NextOfferId + 1);
            if (w.Config.Coop)
            {
                offer.Deadline = w.Elapsed + w.Rules.coopChoiceSeconds;
                p.InvulnUntil = Math.Max(p.InvulnUntil, w.Elapsed + w.Rules.choiceInvuln);
            }
            pr.Current = offer;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.LevelUpOffer, A = slot, B = offer.OfferId, C = pr.Level });
        }

        /// <summary>Valida e aplica a escolha da oferta aberta.</summary>
        public static bool Choose(World w, int slot, short offerId, int index)
        {
            var p = w.Players[slot];
            var offer = p?.Progress.Current;
            if (offer == null || offer.OfferId != offerId || index < 0 || index >= offer.Options.Length) return false;

            var opt = offer.Options[index];
            switch (opt.Kind)
            {
                case OptionKind.NewAbility:
                case OptionKind.Attribute:
                case OptionKind.Survival:
                    AbilitySystem.Grant(w, slot, opt.Ref);
                    break;
                case OptionKind.Upgrade:
                    foreach (var a in p.Abilities)
                        if (a.AbilityIndex == opt.Ref) { AbilitySystem.LevelUp(w, slot, a); break; }
                    break;
                case OptionKind.Evolution:
                    ApplyEvolution(w, slot, opt.Ref);
                    break;
            }

            p.Progress.Current = null;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.ChoiceApplied, A = slot, B = (int)opt.Kind, C = opt.Ref });
            SynergySystem.Evaluate(w, slot);
            EnsureOffer(w, slot);
            return true;
        }

        /// <summary>Substitui a habilidade de origem pela resultante, na mesma posição.</summary>
        public static void ApplyEvolution(World w, int slot, int evolutionIndex)
        {
            var p = w.Players[slot];
            var evo = w.Db.Evolution(evolutionIndex);
            int at = p.Abilities.FindIndex(a => a.AbilityIndex == evo.From);
            if (at < 0) return;

            var old = p.Abilities[at];
            p.Stats.RemoveSource(old.SourceId);
            AffinitySystem.OnAbilityRemoved(w, slot, old);
            p.Abilities.RemoveAt(at);

            var created = AbilitySystem.Grant(w, slot, evo.Result);
            if (created != null)
            {
                p.Abilities.Remove(created);
                p.Abilities.Insert(at, created);
            }
            p.Evolved.Add(evolutionIndex);
            w.Discovered.Add(evolutionIndex);
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.EvolutionApplied, A = slot, B = evolutionIndex, C = evo.Result });
        }

        public static void ApplyInputs(World w, PlayerInput[] inputs)
        {
            if (inputs == null) return;
            for (int slot = 0; slot < PlayerStore.MaxSlots && slot < inputs.Length; slot++)
            {
                var input = inputs[slot];
                if (input.ChoiceIndex >= 0 && w.Players.Slots[slot] != null)
                    Choose(w, slot, input.ChoiceOfferId, input.ChoiceIndex);
            }
        }

        public static void Step(World w, float dt)
        {
            if (w.PendingXp > 0)
            {
                AddSharedXp(w, w.PendingXp);
                w.PendingXp = 0;
            }
            if (!w.Config.Coop) return;
            foreach (var p in w.Players.Slots)
            {
                var offer = p?.Progress.Current;
                if (offer != null && w.Elapsed >= offer.Deadline) Choose(w, p.Slot, offer.OfferId, 0);
            }
        }
    }
}
