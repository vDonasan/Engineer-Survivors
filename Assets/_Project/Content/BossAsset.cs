using ES.Sim;
using UnityEngine;

namespace ES.Content
{
    /// <summary>Boss editável no Inspector: horário, fúria e parâmetros dos ataques (dados da Sim) + retrato.</summary>
    public sealed class BossAsset : ScriptableObject
    {
        public BossDef def = new BossDef();
        public Sprite idle;
    }
}
