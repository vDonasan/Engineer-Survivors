namespace ES.Sim
{
    /// <summary>Atributos do jogador e das habilidades num único espaço de ids.</summary>
    public enum StatId
    {
        // Jogador
        MaxHp, MoveSpeed, Regen, PickupRadius, Armor, DamageTaken,
        // Habilidades
        Damage, Cooldown, Range, Area, Duration, ProjectileSpeed, Knockback, Slow, HitInterval,
        Count, Pierce, Chains, ChainRange, ChainFalloff,
    }

    public static class StatIds
    {
        public const int Count = (int)StatId.ChainFalloff + 1;

        public static bool TryParse(string text, out StatId id)
        {
            id = default;
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < Count; i++)
            {
                if (string.Equals(((StatId)i).ToString(), text, System.StringComparison.OrdinalIgnoreCase))
                {
                    id = (StatId)i;
                    return true;
                }
            }
            return false;
        }
    }
}
