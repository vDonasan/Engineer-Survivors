using ES.Sim;
using UnityEngine;

namespace ES.Content
{
    /// <summary>Definição editável no Inspector (dados da Sim + referências visuais).</summary>
    public sealed class EvolutionAsset : ScriptableObject
    {
        public EvolutionDef def = new EvolutionDef();
        
    }
}
