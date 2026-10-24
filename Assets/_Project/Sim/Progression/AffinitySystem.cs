namespace ES.Sim
{
    /// <summary>
    /// Bônus de afinidade da especialização em armas com tags compatíveis:
    /// inicial ao adquirir, intermediário no nível N, avançado no nível máximo.
    /// </summary>
    public static class AffinitySystem
    {
        public static void OnAbilityChanged(World w, int slot, AbilityInstance a)
        {
            var p = w.Players[slot];
            var spec = w.Db.Specialization(p.Specialization);
            float maxBefore = p.Stats.Get(StatId.MaxHp);
            p.Stats.RemoveSource(a.AffinitySourceId);
            if (a.Def.Kind != AbilityKind.Weapon || !a.Def.Tags.Overlaps(spec.AffinityTags)) return;

            var scope = ModScope.ForAbility(a.AbilityIndex);
            p.Stats.Add(new Modifier { Stat = StatId.Damage, Op = ModOp.Mult, Value = w.Rules.affinityInitial, SourceId = a.AffinitySourceId, Scope = scope });
            if (a.Level >= w.Rules.affinityIntermediateLevel)
                p.Stats.Add(new Modifier { Stat = StatId.Damage, Op = ModOp.Mult, Value = w.Rules.affinityIntermediate, SourceId = a.AffinitySourceId, Scope = scope });
            if (a.Level >= a.Def.MaxLevel)
                foreach (var t in spec.AdvancedAffinity) p.Stats.Add(t.ToModifier(1f, a.AffinitySourceId, a.AbilityIndex));

            float maxAfter = p.Stats.Get(StatId.MaxHp);
            if (maxAfter > maxBefore) p.Hp += maxAfter - maxBefore;
        }

        public static void OnAbilityRemoved(World w, int slot, AbilityInstance a)
        {
            w.Players[slot].Stats.RemoveSource(a.AffinitySourceId);
        }
    }
}
