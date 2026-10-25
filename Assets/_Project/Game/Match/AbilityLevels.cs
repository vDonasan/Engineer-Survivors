using ES.Sim;

namespace ES.Game
{
    /// <summary>Valor de um atributo da habilidade num nível, só pelos dados (para visuais e cartas).</summary>
    public static class AbilityLevels
    {
        public static float Value(ResolvedAbility a, int level, StatId stat)
        {
            float add = a.BaseStats.GetBase(stat), mult = 1f;
            for (int i = 0; i < level - 1 && i < a.Levels.Length; i++)
            {
                var l = a.Levels[i];
                if (l.Add != null) foreach (var v in l.Add) if (v.Stat == stat) add += v.Value;
                if (l.Mult != null) foreach (var v in l.Mult) if (v.Stat == stat) mult *= 1f + v.Value;
            }
            return add * mult;
        }

        /// <summary>Texto do que muda ao subir para <paramref name="nextLevel"/> (vazio se não houver).</summary>
        public static string LevelText(ResolvedAbility a, int nextLevel)
        {
            int i = nextLevel - 2;
            return i >= 0 && i < a.Levels.Length ? a.Levels[i].Text ?? "" : "";
        }
    }
}
