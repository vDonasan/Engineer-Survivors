using System.Collections.Generic;
using ES.Sim;
using UnityEngine;

namespace ES.Content
{
    /// <summary>
    /// Todo o conteúdo do jogo como ScriptableObjects. Fonte para o GameDatabase em
    /// tempo de execução; sincronizado com sp1-content.json pelo menu Conteúdo.
    /// </summary>
    public sealed class ContentDatabaseAsset : ScriptableObject
    {
        public RulesDef rules = new RulesDef();
        public BalanceDef balance = new BalanceDef();
        public SpawnDef spawn = new SpawnDef();
        public List<ArenaAsset> arenas = new List<ArenaAsset>();
        public List<SpecializationAsset> specializations = new List<SpecializationAsset>();
        public List<SpecialAsset> specials = new List<SpecialAsset>();
        public List<AbilityAsset> abilities = new List<AbilityAsset>();
        public List<EnemyAsset> enemies = new List<EnemyAsset>();
        public List<SynergyAsset> synergies = new List<SynergyAsset>();
        public List<EvolutionAsset> evolutions = new List<EvolutionAsset>();

        public ContentDef ToContentDef()
        {
            return new ContentDef
            {
                rules = rules,
                balance = balance,
                spawn = spawn,
                arenas = arenas.ConvertAll(a => a.def).ToArray(),
                specializations = specializations.ConvertAll(a => a.def).ToArray(),
                specials = specials.ConvertAll(a => a.def).ToArray(),
                abilities = abilities.ConvertAll(a => a.def).ToArray(),
                enemies = enemies.ConvertAll(a => a.def).ToArray(),
                synergies = synergies.ConvertAll(a => a.def).ToArray(),
                evolutions = evolutions.ConvertAll(a => a.def).ToArray(),
            };
        }

        public BuildResult Build() => GameDatabase.Build(ToContentDef());

        public AbilityAsset Ability(string id) => abilities.Find(a => a.def.id == id);
        public SpecialAsset Special(string id) => specials.Find(a => a.def.id == id);
        public SpecializationAsset Specialization(string id) => specializations.Find(a => a.def.id == id);
    }
}
