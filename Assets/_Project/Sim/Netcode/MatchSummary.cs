using System;
using System.Collections.Generic;

namespace ES.Sim
{
    public struct AbilityDamage
    {
        /// <summary>Índice da habilidade; -1 = especial.</summary>
        public int Ability;
        public float Amount;
    }

    public sealed class SlotSummary
    {
        public bool Present;
        public int Level, Kills;
        public float DamageTaken;
        public readonly List<AbilityDamage> Damage = new List<AbilityDamage>(8);
    }

    /// <summary>Números da tela de resultado; o host os envia aos clientes no fim da partida.</summary>
    public sealed class MatchSummary
    {
        public MatchPhase Phase;
        public float Elapsed;
        public int TeamKills;
        public readonly SlotSummary[] Players = new SlotSummary[PlayerStore.MaxSlots];

        public MatchSummary()
        {
            for (int i = 0; i < Players.Length; i++) Players[i] = new SlotSummary();
        }

        public static MatchSummary FromWorld(World w)
        {
            var s = new MatchSummary { Phase = w.Phase, Elapsed = w.Elapsed, TeamKills = w.Stats.Kills };
            for (int slot = 0; slot < PlayerStore.MaxSlots; slot++)
            {
                var p = w.Players.Slots[slot];
                if (p == null) continue;
                var o = s.Players[slot];
                o.Present = true;
                o.Level = p.Progress.Level;
                o.Kills = w.Stats.KillsBySlot[slot];
                o.DamageTaken = w.Stats.DamageTaken[slot];
                var row = w.Stats.DamageByAbility[slot];
                for (int i = 0; i < row.Length; i++)
                    if (row[i] > 0) o.Damage.Add(new AbilityDamage { Ability = i == row.Length - 1 ? -1 : i, Amount = row[i] });
                o.Damage.Sort((a, b) => b.Amount.CompareTo(a.Amount));
            }
            return s;
        }
    }

    public static class SummaryCodec
    {
        const int MaxDamageRows = 32;

        public static void Write(ByteWriter w, MatchSummary s)
        {
            w.U8((byte)s.Phase);
            w.F32(s.Elapsed);
            w.U32((uint)Math.Max(0, s.TeamKills));
            byte mask = 0;
            for (int i = 0; i < s.Players.Length; i++) if (s.Players[i].Present) mask |= (byte)(1 << i);
            w.U8(mask);
            foreach (var p in s.Players)
            {
                if (!p.Present) continue;
                w.U8((byte)Math.Min(255, p.Level));
                w.U32((uint)Math.Max(0, p.Kills));
                w.F32(p.DamageTaken);
                int n = Math.Min(MaxDamageRows, p.Damage.Count);
                w.U8((byte)n);
                for (int i = 0; i < n; i++) { w.I16((short)p.Damage[i].Ability); w.F32(p.Damage[i].Amount); }
            }
        }

        /// <summary>Nunca lança; dados malformados retornam false.</summary>
        public static bool TryRead(byte[] data, int length, out MatchSummary s)
        {
            s = null;
            var r = new ByteReader(data, length);
            var o = new MatchSummary();
            if (!r.TryU8(out byte phase) || phase > (byte)MatchPhase.Defeat) return false;
            if (!r.TryF32(out o.Elapsed) || !r.TryU32(out uint kills) || !r.TryU8(out byte mask)) return false;
            if (float.IsNaN(o.Elapsed) || mask >= 1 << PlayerStore.MaxSlots) return false;
            o.Phase = (MatchPhase)phase;
            o.TeamKills = (int)Math.Min(kills, int.MaxValue);
            for (int i = 0; i < PlayerStore.MaxSlots; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var p = o.Players[i];
                p.Present = true;
                if (!r.TryU8(out byte level) || !r.TryU32(out uint pk) || !r.TryF32(out p.DamageTaken) || !r.TryU8(out byte n)) return false;
                if (n > MaxDamageRows) return false;
                p.Level = level;
                p.Kills = (int)Math.Min(pk, int.MaxValue);
                for (int k = 0; k < n; k++)
                {
                    if (!r.TryI16(out short ab) || !r.TryF32(out float amount)) return false;
                    p.Damage.Add(new AbilityDamage { Ability = ab, Amount = amount });
                }
            }
            if (r.Remaining != 0) return false;
            s = o;
            return true;
        }
    }
}
