using System.Linq;

namespace ES.Sim
{
    /// <summary>
    /// Ferramenta de teste (bots, provas no navegador com ?evolve=N): leva a arma de cada personagem
    /// ao nível máximo, aplica a evolução e sobe a evoluída até <paramref name="evoLevel"/>.
    /// </summary>
    public static class DebugEvolve
    {
        public static void All(World w, int evoLevel)
        {
            for (int slot = 0; slot < PlayerStore.MaxSlots; slot++)
                if (w.Players.Slots[slot] != null) Slot(w, slot, evoLevel);
        }

        public static bool Slot(World w, int slot, int evoLevel)
        {
            var p = w.Players[slot];
            var sig = p.Abilities.FirstOrDefault(a => a.Def.Def.exclusive && !a.Def.Def.evolutionOnly);
            if (sig == null) return false;
            while (AbilitySystem.LevelUp(w, slot, sig)) { }
            int evo = Enumerable.Range(0, w.Db.EvolutionCount).FirstOrDefault(i => w.Db.Evolution(i).From == sig.AbilityIndex);
            if (w.Db.EvolutionCount == 0 || w.Db.Evolution(evo).From != sig.AbilityIndex) return false;
            foreach (var r in w.Db.Evolution(evo).Def.requires)
            {
                if (r.target == w.Db.Ability(sig.AbilityIndex).Id) continue;
                int idx = w.Db.AbilityIndex(r.target);
                var have = p.Abilities.FirstOrDefault(a => a.AbilityIndex == idx) ?? AbilitySystem.Grant(w, slot, idx);
                while (have != null && have.Level < r.value && AbilitySystem.LevelUp(w, slot, have)) { }
            }
            ProgressionSystem.ApplyEvolution(w, slot, evo);
            var evolved = p.Abilities.First(a => a.AbilityIndex == w.Db.Evolution(evo).Result);
            while (evolved.Level < evoLevel && AbilitySystem.LevelUp(w, slot, evolved)) { }
            return true;
        }
    }
}
