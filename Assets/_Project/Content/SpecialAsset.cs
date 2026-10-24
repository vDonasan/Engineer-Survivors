using ES.Sim;
using UnityEngine;

namespace ES.Content
{
    /// <summary>Definição editável no Inspector (dados da Sim + referências visuais).</summary>
    public sealed class SpecialAsset : ScriptableObject
    {
        public SpecialDef def = new SpecialDef();
        public Sprite icon;
    }
}
