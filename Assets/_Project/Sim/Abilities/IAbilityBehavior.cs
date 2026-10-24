namespace ES.Sim
{
    /// <summary>Habilidade adquirida por um jogador.</summary>
    public sealed class AbilityInstance
    {
        public int AbilityIndex;
        public ResolvedAbility Def;
        public int Level;
        public float CooldownLeft;
        public int FireCount;
        /// <summary>Fonte dos modificadores que esta instância concede ao jogador.</summary>
        public int SourceId;
        /// <summary>Fonte dos bônus de afinidade desta instância.</summary>
        public int AffinitySourceId;
        /// <summary>Base da habilidade + melhorias de nível.</summary>
        public readonly StatBlock Stats = new StatBlock();
        /// <summary>Estado próprio do comportamento.</summary>
        public object State;

        public float Get(PlayerState owner, StatId stat) => StatQuery.Ability(owner.Stats, Stats, AbilityIndex, Def.Tags, stat);
    }

    /// <summary>Comportamento de habilidade. Implementações são sem estado (estado vai na instância).</summary>
    public interface IAbilityBehavior
    {
        void OnAcquire(World w, int slot, AbilityInstance a);
        void Tick(World w, int slot, AbilityInstance a, float dt);
    }
}
