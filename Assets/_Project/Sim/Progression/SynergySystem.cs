namespace ES.Sim
{
    /// <summary>Sinergias: ativas enquanto os requisitos valem; não consomem nada.</summary>
    public static class SynergySystem
    {
        public static void Evaluate(World w, int slot)
        {
            var p = w.Players[slot];
            if (p == null) return;
            for (int i = 0; i < w.Db.SynergyCount; i++)
            {
                bool met = RequirementEvaluator.Met(w, slot, w.Db.Synergy(i).Requires);
                if (met == p.Synergies[i]) continue;
                p.Synergies[i] = met;
                w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.SynergyChanged, A = slot, B = i, C = met ? 1 : 0 });
            }
        }

        /// <summary>
        /// Bônus de "carga" para armas de cadeia neste disparo: parâmetro próprio da
        /// arma (ex.: Pulso Amplificado) e sinergias ativas do tipo charge_every_n.
        /// </summary>
        public static void ChainCharge(World w, int slot, AbilityInstance a, out float damageMult, out int extraChains)
        {
            damageMult = 1f;
            extraChains = 0;

            int every = (int)a.Def.Param("chargeEvery", 0f);
            if (every > 0 && a.FireCount % every == 0)
            {
                damageMult *= 1f + a.Def.Param("chargeDamageMult", 0f);
                extraChains += (int)a.Def.Param("chargeExtraChains", 0f);
            }

            var p = w.Players[slot];
            for (int i = 0; i < w.Db.SynergyCount; i++)
            {
                if (!p.Synergies[i]) continue;
                var s = w.Db.Synergy(i);
                if (s.Effect != SynergyEffect.ChargeEveryN || s.TargetAbility != a.AbilityIndex) continue;
                int n = (int)s.Def.n;
                if (n <= 0 || a.FireCount % n != 0) continue;
                damageMult *= 1f + s.Def.damageMult;
                extraChains += (int)s.Def.extraChains;
            }
        }
    }
}
