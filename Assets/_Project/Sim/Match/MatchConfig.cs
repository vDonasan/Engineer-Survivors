namespace ES.Sim
{
    public struct PlayerSlot
    {
        public string Name;
        public string SpecializationId;
    }

    /// <summary>Configuração fixada no início da partida (host → clientes).</summary>
    public sealed class MatchConfig
    {
        public ulong Seed;
        public Difficulty Difficulty = Difficulty.Normal;
        public int TeamSize = 1;
        public string ArenaId = "laboratorio";
        public PlayerSlot[] Roster = new PlayerSlot[0];
        /// <summary>Coop: escolha de nível não pausa (prazo + invulnerabilidade).</summary>
        public bool Coop;
    }

    public enum MatchPhase { Running, Victory, Defeat }
}
