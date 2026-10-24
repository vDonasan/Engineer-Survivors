namespace ES.Sim
{
    /// <summary>Entrada de um jogador num tick (8 bytes na rede).</summary>
    public struct PlayerInput
    {
        public ushort Seq;
        public sbyte MoveX;
        public sbyte MoveY;
        /// <summary>Contador de pedidos de especial; o host ativa quando aumenta.</summary>
        public byte SpecialCounter;
        public short ChoiceOfferId;
        /// <summary>-1 = nenhuma escolha neste tick.</summary>
        public sbyte ChoiceIndex;

        public static PlayerInput None => new PlayerInput { ChoiceIndex = -1 };
    }
}
