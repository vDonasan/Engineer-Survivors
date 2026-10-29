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
                AddXp(w, p, amount, true);
            }
        }

        /// <summary>
        /// XP de um jogador. <paramref name="scaled"/>: aplica balanceamento e bônus de experiência (como uma gema);
        /// senão é XP direto (frações da barra dos eventos). Nunca reduz o nível.
        /// </summary>
        public static void AddXp(World w, PlayerState p, float amount, bool scaled)
        {
            if (p == null || amount <= 0) return;
            {
                var pr = p.Progress;
                pr.Xp += scaled ? amount * w.Balance.Xp * (1f + Math.Max(0f, p.Stats.Get(StatId.XpGain))) : amount;
                ItemHooks.OnXpGained(w, p.Slot);
                int next = XpCurve.ToNext(w.Rules, pr.Level);
                while (pr.Xp >= next)
                {
                    pr.Xp -= next;
                    pr.Level++;
                    pr.PendingLevels++;
                    ItemHooks.OnLevelUp(w, p.Slot);
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
            OpenOffer(w, p, float.NaN);
        }

        /// <summary>Gera e publica uma oferta nova (também ao atualizar ou banir; o prazo do coop é mantido).</summary>
        static void OpenOffer(World w, PlayerState p, float keepDeadline)
        {
            var pr = p.Progress;
            var offer = OfferGenerator.Generate(w, p.Slot, pr.NextOfferId);
            pr.NextOfferId = (short)(pr.NextOfferId == short.MaxValue ? 1 : pr.NextOfferId + 1);
            if (w.Config.Coop)
            {
                offer.Deadline = float.IsNaN(keepDeadline) ? w.ChoiceClock + w.Rules.coopChoiceSeconds : keepDeadline;
                p.InvulnUntil = Math.Max(p.InvulnUntil, w.Elapsed + w.Rules.choiceInvuln);
            }
            offer.RerollsLeft = (byte)Math.Max(0, pr.RerollsLeft);
            offer.BansLeft = (byte)Math.Max(0, pr.BansLeft);
            offer.SkipsLeft = (byte)Math.Max(0, pr.SkipsLeft);
            pr.Current = offer;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.LevelUpOffer, A = p.Slot, B = offer.OfferId, C = pr.Level });
        }

        /// <summary>Códigos de ChoiceIndex além das cartas 0..n.</summary>
        public const sbyte ActionReroll = -2, ActionSkip = -3, ActionBanFirst = -10;
        /// <summary>ChoiceApplied.B quando a escolha foi pulada.</summary>
        public const int SkippedKind = 250;

        static bool Action(World w, PlayerState p, LevelUpOffer offer, int index)
        {
            var pr = p.Progress;
            if (index == ActionReroll)
            {
                if (pr.RerollsLeft <= 0) return false;
                pr.RerollsLeft--;
                OpenOffer(w, p, offer.Deadline);
                return true;
            }
            if (index == ActionSkip)
            {
                if (pr.SkipsLeft <= 0) return false;
                pr.SkipsLeft--;
                pr.Current = null;
                w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.ChoiceApplied, A = p.Slot, B = SkippedKind, C = 0 });
                EnsureOffer(w, p.Slot);
                return true;
            }
            int k = ActionBanFirst - index;
            if (k < 0 || k >= offer.Options.Length || pr.BansLeft <= 0) return false;
            var opt = offer.Options[k];
            if (opt.Kind == OptionKind.Evolution || opt.Kind == OptionKind.Survival) return false;
            pr.BansLeft--;
            pr.Banned.Add(opt.Ref);
            OpenOffer(w, p, offer.Deadline);
            return true;
        }

        /// <summary>Valida e aplica a escolha da oferta aberta.</summary>
        public static bool Choose(World w, int slot, short offerId, int index)
        {
            var p = w.Players[slot];
            var offer = p?.Progress.Current;
            if (offer == null || offer.OfferId != offerId) return false;
            if (index <= ActionReroll) return Action(w, p, offer, index);
            if (index < 0 || index >= offer.Options.Length) return false;

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
            w.Companions.RemoveOwned(slot, old.AbilityIndex);

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
                if (input.ChoiceIndex != -1 && w.Players.Slots[slot] != null)
                    Choose(w, slot, input.ChoiceOfferId, input.ChoiceIndex);
            }
        }

        /// <summary>Coop: quem não escolheu no prazo (tempo de pausa) leva a primeira carta, para ninguém travar o grupo.</summary>
        public static void ExpireOffers(World w)
        {
            foreach (var p in w.Players.Slots)
            {
                var offer = p?.Progress.Current;
                if (offer != null && p.IsActive && w.ChoiceClock >= offer.Deadline) Choose(w, p.Slot, offer.OfferId, 0);
            }
        }

        public static void Step(World w, float dt)
        {
            if (w.PendingXp > 0)
            {
                AddSharedXp(w, w.PendingXp);
                w.PendingXp = 0;
            }
        }
    }
}
