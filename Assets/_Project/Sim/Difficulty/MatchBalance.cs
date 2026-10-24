namespace ES.Sim
{
    /// <summary>
    /// Balanceamento resolvido de uma partida (dificuldade × tamanho da equipe).
    /// Fixado no início e enviado aos clientes; sistemas leem só daqui.
    /// </summary>
    public struct MatchBalance
    {
        public Difficulty Difficulty;
        public int TeamSize;
        public float EnemyDamage;
        public float EnemyHp;
        public float SpawnBudget;
        public float SpawnRamp;
        public float CompositionShiftMinutes;
        public float Xp;
        public float HealDropChance;
        public float Regen;
        public float EliteChance;
        public float Telegraph;
        public float EnemyCount;
    }
}
