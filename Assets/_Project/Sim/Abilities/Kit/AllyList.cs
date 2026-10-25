using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>Tiros, zonas e companheiros a desenhar (host) e a enviar no snapshot.</summary>
    public static class AllyList
    {
        /// <summary>Tiros além desta distância de todos os jogadores ficam de fora quando falta espaço.</summary>
        const float FarDistance = 18f;

        public static byte Angle(Vec2 v)
        {
            if (v.LengthSq() < 1e-6f) return 0;
            double a = Math.Atan2(v.Y, v.X);
            if (a < 0) a += Math.PI * 2;
            return (byte)((int)Math.Round(a / (Math.PI * 2) * 256) & 0xFF);
        }

        public static void Collect(World w, List<AllySnap> into, int cap)
        {
            into.Clear();
            foreach (var c in w.Companions.Items)
            {
                if (!c.Active || into.Count >= cap) continue;
                var owner = w.Players.Slots[c.Owner];
                if (owner == null || owner.Status == PlayerStatus.Inactive) continue;
                into.Add(new AllySnap { Id = c.Id, Pos = c.Pos, Sprite = c.Sprite, Param = c.Param, Tick = w.Tick });
            }
            for (int i = 0; i < w.Zones.Count && into.Count < cap; i++)
            {
                ref var z = ref w.Zones.Items[i];
                into.Add(new AllySnap { Id = z.Id, Pos = z.Pos, Sprite = z.Sprite, Param = (byte)Math.Min(255, (int)Math.Round(z.Radius * 16f)), Tick = w.Tick });
            }

            var shots = w.Shots;
            bool crowded = into.Count + shots.Count > cap;
            float far2 = FarDistance * FarDistance;
            for (int i = 0; i < shots.Count && into.Count < cap; i++)
            {
                ref var s = ref shots.Items[i];
                if (crowded && !NearAnyPlayer(w, s.Pos, far2)) continue;
                into.Add(new AllySnap { Id = s.Id, Pos = s.Pos, Sprite = s.Sprite, Param = Angle(s.Vel), Tick = w.Tick });
            }
        }

        static bool NearAnyPlayer(World w, Vec2 pos, float far2)
        {
            foreach (var p in w.Players.Slots)
                if (p != null && p.Status != PlayerStatus.Inactive && Vec2.DistanceSq(p.Pos, pos) < far2) return true;
            return false;
        }
    }
}
