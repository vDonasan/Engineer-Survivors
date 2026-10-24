using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>
    /// Valores-base + modificadores. Final = (base + ΣAdd) × Π(1 + Mult).
    /// <see cref="Get"/> considera só modificadores de escopo Player.
    /// </summary>
    public sealed class StatBlock
    {
        readonly float[] _base = new float[StatIds.Count];
        readonly List<Modifier> _mods = new List<Modifier>(16);

        public int ModifierCount => _mods.Count;
        public Modifier ModifierAt(int i) => _mods[i];

        public void SetBase(StatId stat, float value) => _base[(int)stat] = value;
        public float GetBase(StatId stat) => _base[(int)stat];

        public void Add(in Modifier mod) => _mods.Add(mod);

        public int RemoveSource(int sourceId)
        {
            int removed = 0;
            for (int i = _mods.Count - 1; i >= 0; i--)
            {
                if (_mods[i].SourceId != sourceId) continue;
                _mods.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        public float Get(StatId stat)
        {
            float add = 0f, mult = 1f;
            for (int i = 0; i < _mods.Count; i++)
            {
                var m = _mods[i];
                if (m.Stat != stat || m.Scope.Kind != ScopeKind.Player) continue;
                if (m.Op == ModOp.Add) add += m.Value; else mult *= 1f + m.Value;
            }
            return (_base[(int)stat] + add) * mult;
        }

        public void CopyBaseFrom(StatBlock other)
        {
            for (int i = 0; i < _base.Length; i++) _base[i] = other._base[i];
        }
    }
}
