namespace ES.Sim
{
    public enum PlayerStatus { Empty, Active, Downed, Inactive }

    public sealed class PlayerState
    {
        public int Slot;
        public PlayerStatus Status;
        public string Name;
        public int Specialization;
        public Vec2 Pos;
        public Vec2 Vel;
        public Vec2 Facing = new Vec2(0, -1);
        public float Radius;
        public float Hp;
        public float InvulnUntil;
        /// <summary>Escudo temporário (absorve dano antes da vida).</summary>
        public float Shield;
        public readonly StatBlock Stats = new StatBlock();
        public readonly System.Collections.Generic.List<AbilityInstance> Abilities = new System.Collections.Generic.List<AbilityInstance>(12);

        /// <summary>Tempo restante de um especial com duração (ex.: Turbina).</summary>
        public float SpecialActiveLeft;
        public SpecialState Special;
        public readonly PlayerProgress Progress = new PlayerProgress();
        /// <summary>Sinergias ativas, por índice no banco (alocado pelo World).</summary>
        public bool[] Synergies = new bool[0];
        /// <summary>Evoluções aplicadas por este jogador.</summary>
        public readonly System.Collections.Generic.HashSet<int> Evolved = new System.Collections.Generic.HashSet<int>();

        public ushort LastInputSeq;
        public PlayerInput LastInput = PlayerInput.None;

        public bool IsActive => Status == PlayerStatus.Active;
    }

    /// <summary>Até 4 jogadores, indexados por slot.</summary>
    public sealed class PlayerStore
    {
        public const int MaxSlots = 4;
        public readonly PlayerState[] Slots = new PlayerState[MaxSlots];

        public PlayerState this[int slot] => Slots[slot];

        public int CountWith(PlayerStatus status)
        {
            int n = 0;
            for (int i = 0; i < MaxSlots; i++) if (Slots[i] != null && Slots[i].Status == status) n++;
            return n;
        }
    }
}
