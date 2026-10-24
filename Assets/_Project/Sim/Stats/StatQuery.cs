namespace ES.Sim
{
    public static class StatQuery
    {
        /// <summary>
        /// Valor efetivo de um atributo de habilidade: base da habilidade, todos os
        /// modificadores da própria habilidade e os modificadores do jogador cujo
        /// escopo casa com a habilidade. Mesma fórmula de <see cref="StatBlock"/>.
        /// </summary>
        public static float Ability(StatBlock player, StatBlock ability, int abilityIndex, TagSet abilityTags, StatId stat)
        {
            float add = 0f, mult = 1f;

            for (int i = 0; i < ability.ModifierCount; i++)
            {
                var m = ability.ModifierAt(i);
                if (m.Stat != stat) continue;
                if (m.Op == ModOp.Add) add += m.Value; else mult *= 1f + m.Value;
            }

            for (int i = 0; i < player.ModifierCount; i++)
            {
                var m = player.ModifierAt(i);
                if (m.Stat != stat || !m.Scope.AppliesTo(abilityIndex, abilityTags)) continue;
                if (m.Op == ModOp.Add) add += m.Value; else mult *= 1f + m.Value;
            }

            return (ability.GetBase(stat) + add) * mult;
        }
    }
}
