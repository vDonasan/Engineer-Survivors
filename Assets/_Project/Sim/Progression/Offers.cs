namespace ES.Sim
{
    public enum OptionKind { NewAbility, Upgrade, Evolution, Attribute, Survival }

    /// <summary>Uma carta de subida de nível. <see cref="Ref"/> é índice de habilidade ou de evolução.</summary>
    public struct OfferOption
    {
        public OptionKind Kind;
        public int Ref;

        public override string ToString() => Kind + ":" + Ref;
    }

    public sealed class LevelUpOffer
    {
        public short OfferId;
        public OfferOption[] Options;
        /// <summary>Coop: instante em que vale a primeira opção. Solo: infinito.</summary>
        public float Deadline = float.MaxValue;
    }

    public sealed class PlayerProgress
    {
        public int Level = 1;
        public float Xp;
        /// <summary>Níveis ganhos que ainda não viraram oferta.</summary>
        public int PendingLevels;
        /// <summary>Oferta aberta (no máximo uma por vez).</summary>
        public LevelUpOffer Current;
        public short NextOfferId = 1;
    }
}
