namespace ES.Sim
{
    public enum ModOp { Add, Mult }

    /// <summary>
    /// Onde um modificador vale. <see cref="ScopeKind.Player"/> afeta os atributos
    /// do próprio dono; os demais afetam habilidades (via <see cref="StatQuery"/>).
    /// </summary>
    public enum ScopeKind { Player, AllAbilities, Tag, Ability }

    public struct ModScope
    {
        public ScopeKind Kind;
        public TagSet Tags;
        public int AbilityIndex;

        public static ModScope Player => default;
        public static ModScope AllAbilities => new ModScope { Kind = ScopeKind.AllAbilities };
        public static ModScope ForTag(Tag tag) => new ModScope { Kind = ScopeKind.Tag, Tags = TagSet.Of(tag) };
        public static ModScope ForTags(TagSet tags) => new ModScope { Kind = ScopeKind.Tag, Tags = tags };
        public static ModScope ForAbility(int abilityIndex) => new ModScope { Kind = ScopeKind.Ability, AbilityIndex = abilityIndex };

        public bool AppliesTo(int abilityIndex, TagSet abilityTags)
        {
            switch (Kind)
            {
                case ScopeKind.AllAbilities: return true;
                case ScopeKind.Tag: return Tags.Overlaps(abilityTags);
                case ScopeKind.Ability: return AbilityIndex == abilityIndex;
                default: return false;
            }
        }
    }

    public struct Modifier
    {
        public StatId Stat;
        public ModOp Op;
        public float Value;
        public int SourceId;
        public ModScope Scope;
    }
}
