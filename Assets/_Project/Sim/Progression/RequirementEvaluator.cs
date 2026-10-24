namespace ES.Sim
{
    public static class RequirementEvaluator
    {
        /// <summary>Verdadeiro se todos os requisitos são atendidos pelo jogador.</summary>
        public static bool Met(World w, int slot, ResolvedRequirement[] reqs)
        {
            var p = w.Players[slot];
            if (p == null) return false;
            foreach (var r in reqs)
            {
                switch (r.Kind)
                {
                    case RequirementKind.AbilityLevel:
                        if (LevelOf(p, r.TargetIndex) < r.Value) return false;
                        break;
                    case RequirementKind.TagCount:
                        int count = 0;
                        foreach (var a in p.Abilities) if (a.Def.Tags.Has(r.Tag)) count++;
                        if (count < r.Value) return false;
                        break;
                    case RequirementKind.Specialization:
                        if (p.Specialization != r.TargetIndex) return false;
                        break;
                    case RequirementKind.StatAtLeast:
                        if (p.Stats.Get(r.Stat) < r.Value) return false;
                        break;
                    case RequirementKind.Branch:
                        // Ramos chegam no SP2.
                        return false;
                }
            }
            return true;
        }

        public static int LevelOf(PlayerState p, int abilityIndex)
        {
            foreach (var a in p.Abilities) if (a.AbilityIndex == abilityIndex) return a.Level;
            return 0;
        }
    }
}
