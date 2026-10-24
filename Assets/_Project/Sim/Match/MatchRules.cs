namespace ES.Sim
{
    public static class MatchRules
    {
        /// <summary>Vitória ao fim do tempo com alguém ativo; derrota quando ninguém está ativo.</summary>
        public static void Step(World w)
        {
            if (w.Phase != MatchPhase.Running) return;
            int active = w.Players.CountWith(PlayerStatus.Active);
            if (active == 0) End(w, MatchPhase.Defeat);
            else if (w.Elapsed >= w.Rules.matchSeconds - 1e-4f) End(w, MatchPhase.Victory);
        }

        static void End(World w, MatchPhase result)
        {
            w.Phase = result;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.MatchEnded, A = (int)result, X = w.Elapsed });
        }

        /// <summary>
        /// Jogador desconectado: deixa de agir e de contar para a derrota; ofertas
        /// pendentes são descartadas. O balanceamento da partida não muda.
        /// </summary>
        public static void SetInactive(World w, int slot)
        {
            var p = w.Players[slot];
            if (p == null || p.Status == PlayerStatus.Inactive) return;
            p.Status = PlayerStatus.Inactive;
            p.Progress.Current = null;
            p.Progress.PendingLevels = 0;
            p.Vel = Vec2.Zero;
            if (p.Special != null)
            {
                p.SpecialActiveLeft = 0;
                p.Stats.RemoveSource(p.Special.SourceId);
            }
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.PlayerInactive, A = slot });
        }
    }
}
