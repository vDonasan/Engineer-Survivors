using System;

namespace ES.Sim
{
    /// <summary>
    /// Giulia: alterna resistores (rápidos, perfurantes) e transistores (descarga que salta em cadeia).
    /// Três acertos de componentes na mesma região formam um campo elétrico temporário.
    /// Evolução: fontes de alimentação (pulso em área), osciloscópio (onda em linha) e circuito entre
    /// inimigos. Avançada: tempestade eletromagnética ao redor dela.
    /// </summary>
    public sealed class GiuliaBehavior : CharacterWeapon
    {
        const int TShot = 0, TField = 1, TSupply = 2, TScope = 3, TCircuit = 4, TStorm = 5, TStormHit = 6;
        const int CAlt = 0;
        const byte VResistor = 0, VTransistor = 1, VSupply = 2;

        sealed class Hits
        {
            public readonly Vec2[] Pos = new Vec2[8];
            public readonly float[] At = new float[8];
            public int Next;
        }

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.Due(TShot))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0)
                {
                    Components(ref c, t);
                    c.Reset(TShot, c.Stat(StatId.Cooldown));
                }
            }
            if (!c.Evolved) return;

            if (c.Due(TSupply))
            {
                int t = Area.Densest(c.W, c.Pos, c.Range, 2f);
                if (t >= 0)
                {
                    var s = c.NewShot(AllySprite.PowerSupply, c.DirTo(t), c.Speed(8f), c.Dmg(1.2f), 0.4f, c.Range);
                    s.BurstWhen = ShotBurst.OnEnd;
                    s.BurstRadius = c.Radius(2.2f);
                    s.BurstDamage = c.Dmg(1.6f);
                    s.BurstStyle = FxStyle.Electric;
                    s.Variant = VSupply;
                    c.Fire(s);
                    c.Sound(2);
                    c.Reset(TSupply, c.Period(2.4f));
                }
            }
            if (c.Due(TScope))
            {
                int t = c.Nearest(c.Range + 2f);
                if (t >= 0)
                {
                    var s = c.NewShot(AllySprite.ScopeWave, c.DirTo(t), c.Speed(11f), c.Dmg(1.4f), 0.45f, c.Range + 3f, 12);
                    s.Motion = ShotMotion.Wave;
                    s.Curve = 0.35f;
                    s.Frequency = 2f;
                    c.Fire(s);
                    c.Sound(3);
                    c.Reset(TScope, c.Period(2.8f));
                }
            }
            if (c.Due(TCircuit))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0)
                {
                    Area.Chain(c.W, c.Slot, c.Ability, c.Pos, t, c.Evo >= 2 ? 10 : 7, 4.5f, c.Dmg(1.5f), 0f, FxStyle.Circuit);
                    c.Sound(4);
                    c.Reset(TCircuit, c.Period(6f));
                }
            }
            if (c.Evo >= 3)
            {
                if (c.Due(TStorm))
                {
                    c.W.Zones.Spawn(new Zone
                    {
                        Owner = (byte)c.Slot, Ability = (short)c.Ability, Sprite = AllySprite.ZoneStorm, Pos = c.Pos,
                        Radius = c.Radius(3.5f), Life = c.Time(4f), Follow = (sbyte)c.Slot,
                    });
                    c.Reset(TStorm, c.Period(10f));
                    c.Reset(TStormHit, 0);
                    c.S.Counters[1] = (int)(c.Time(4f) / 0.25f);
                    c.Sound(5);
                }
                if (c.S.Counters[1] > 0 && c.Due(TStormHit))
                {
                    c.S.Counters[1]--;
                    c.Reset(TStormHit, 0.25f);
                    int n = AbilitySystem.Nearest(c.W, c.Pos, c.Radius(3.5f), 8, Targets, null, 0);
                    if (n > 0)
                    {
                        int k = Targets[c.W.Rng.Range(0, n)];
                        var at = c.W.Enemies.Pos[k];
                        c.W.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.ChainArc, A = c.Slot, X = at.X + 0.6f, Y = at.Y + 5f, X2 = at.X, Y2 = at.Y, Value = (float)FxStyle.Storm });
                        Area.Burst(c.W, c.Slot, c.Ability, at, 0.8f, c.Dmg(1.2f), 0.5f, 0.15f, FxStyle.Storm);
                    }
                }
            }
        }

        void Components(ref Ctx c, int target)
        {
            var dir = c.DirTo(target);
            bool transistor = c.S.Counters[CAlt]++ % 2 == 1;
            if (transistor)
            {
                var s = c.NewShot(AllySprite.Transistor, dir, c.Stat(StatId.ProjectileSpeed) * 0.65f, c.Dmg(0.8f), 0.28f, c.Range);
                s.Variant = VTransistor;
                c.Fire(s);
                c.Sound(1);
                return;
            }
            int count = Math.Max(1, (int)c.Stat(StatId.Count));
            for (int k = 0; k < count; k++)
            {
                float off = (k - (count - 1) * 0.5f) * 9f * Deg;
                var s = c.NewShot(AllySprite.Resistor, Rotate(dir, off), c.Stat(StatId.ProjectileSpeed), c.Dmg(), 0.25f, c.Range + 1f,
                    Math.Max(1, (int)c.Stat(StatId.Pierce)));
                s.Variant = VResistor;
                c.Fire(s);
            }
            c.Sound(0);
        }

        public override void OnShotHit(World w, ref Shot s, int enemy)
        {
            if (s.Variant != VResistor && s.Variant != VTransistor) return;
            var a = InstanceOf(w, s.Owner, s.Ability);
            if (a == null) return;
            var c = new Ctx(w, s.Owner, a, (WeaponState)a.State);
            var pos = w.Enemies.Pos[enemy];
            if (s.Variant == VTransistor)
                Area.Chain(w, s.Owner, s.Ability, pos, enemy, (int)c.Stat(StatId.Chains), c.Stat(StatId.ChainRange), c.Dmg(0.7f), c.Stat(StatId.ChainFalloff), FxStyle.Electric);
            Cluster(ref c, pos);
        }

        /// <summary>Três acertos a até 2 m em 1,5 s criam um campo elétrico.</summary>
        static void Cluster(ref Ctx c, Vec2 pos)
        {
            var h = c.S.Extra as Hits ?? (Hits)(c.S.Extra = new Hits());
            float now = c.W.Elapsed;
            h.Pos[h.Next] = pos;
            h.At[h.Next] = now;
            h.Next = (h.Next + 1) % h.Pos.Length;
            if (!c.Due(TField)) return;
            int near = 0;
            for (int i = 0; i < h.Pos.Length; i++)
                if (h.At[i] > 0 && now - h.At[i] <= 1.5f && Vec2.DistanceSq(h.Pos[i], pos) <= 4f) near++;
            if (near < 3) return;
            var zones = c.W.Zones;
            for (int i = 0; i < zones.Count; i++)
                if (zones.Items[i].Owner == c.Slot && zones.Items[i].Sprite == AllySprite.ZoneElectric && Vec2.DistanceSq(zones.Items[i].Pos, pos) < 4f) return;
            zones.Spawn(new Zone
            {
                Owner = (byte)c.Slot, Ability = (short)c.Ability, Sprite = AllySprite.ZoneElectric, Pos = pos,
                Radius = c.Radius(c.Base >= 5 ? 2.0f : 1.6f), Life = c.Time(2.5f), Interval = 0.4f, Damage = c.Dmg(0.35f), Slow = 0.25f, Follow = -1,
            });
            c.Reset(TField, 1.2f);
            Array.Clear(h.At, 0, h.At.Length);
        }
    }
}
