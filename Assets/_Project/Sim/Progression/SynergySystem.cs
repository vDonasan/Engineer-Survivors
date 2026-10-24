namespace ES.Sim
{
    /// <summary>Sinergias entre habilidades (implementado na Task 12).</summary>
    public static class SynergySystem
    {
        /// <summary>Bônus de "carga" para armas de cadeia neste disparo.</summary>
        public static void ChainCharge(World w, int slot, AbilityInstance a, out float damageMult, out int extraChains)
        {
            damageMult = 1f;
            extraChains = 0;
        }
    }
}
