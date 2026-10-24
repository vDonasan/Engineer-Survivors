namespace ES.Sim
{
    /// <summary>Números da partida para a tela de resultado e para os bots de balanceamento.</summary>
    public sealed class MatchStats
    {
        public int Kills;
        public readonly float[] DamageTaken = new float[PlayerStore.MaxSlots];
        /// <summary>[slot][índice da habilidade]; o último índice é o especial.</summary>
        public readonly float[][] DamageByAbility = new float[PlayerStore.MaxSlots][];
        public readonly int[] KillsBySlot = new int[PlayerStore.MaxSlots];

        public MatchStats(int abilityCount)
        {
            for (int i = 0; i < PlayerStore.MaxSlots; i++) DamageByAbility[i] = new float[abilityCount + 1];
        }

        public void AddDamage(int slot, int abilityIndex, float amount)
        {
            if (slot < 0 || slot >= PlayerStore.MaxSlots) return;
            var row = DamageByAbility[slot];
            int i = abilityIndex >= 0 && abilityIndex < row.Length - 1 ? abilityIndex : row.Length - 1;
            row[i] += amount;
        }
    }
}
