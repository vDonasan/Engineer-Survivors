using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>
    /// Badotti: três Cabazords autônomos em formação — o atirador dispara, o brigão vai até o inimigo e
    /// bate por contato, o de investida atravessa a fila. Quando os três acertam o mesmo inimigo em 2 s,
    /// ataque combinado em área. Evolução: o Cabazord Master substitui os três (soco em área, disparo de
    /// energia, investida e golpe no chão). Avançada: salto combinado até o Badotti.
    /// </summary>
    public sealed class BadottiBehavior : CharacterWeapon
    {
        // temporizadores dos robôs
        const int TShoot = 0, TBrawl = 1, TDash = 2, TDashing = 3;
        // temporizadores do Master
        const int TPunch = 0, TBolt = 1, TCharge = 2, TCharging = 3, TSlam = 4, TCombined = 5;
        const int BotShooter = 0, BotBrawler = 1, BotDasher = 2, Master = 3;
        const byte VBolt = 1, VMasterBolt = 2;
        const float Formation = 1.4f;

        static readonly int[] Hits = new int[64];

        sealed class BotState
        {
            public Vec2 DashVel;
            public readonly HashSet<ushort> DashHits = new HashSet<ushort>();
            public readonly Dictionary<ushort, (byte Mask, float At)> Combo = new Dictionary<ushort, (byte, float)>();
            public readonly List<ushort> Stale = new List<ushort>();
            public float PruneAt;
        }

        public override void OnAcquire(World w, int slot, AbilityInstance a)
        {
            var s = new WeaponState { Extra = new BotState() };
            a.State = s;
        }

        protected override void Step(ref Ctx c, float dt)
        {
            var b = c.S.Extra as BotState ?? (BotState)(c.S.Extra = new BotState());
            if (c.Evolved) MasterStep(ref c, b, dt);
            else BotsStep(ref c, b, dt);
        }

        int Ensure(ref Ctx c, int k, AllySprite sprite)
        {
            var comp = c.W.Companions;
            int id = c.S.Companions[k];
            if (id >= 0 && comp.Items[id].Active && comp.Items[id].Owner == c.Slot && comp.Items[id].Ability == c.Ability) return id;
            id = comp.Spawn(c.Slot, c.Ability, sprite, c.Pos);
            c.S.Companions[k] = id;
            return id;
        }

        static Vec2 Toward(Vec2 from, Vec2 to, float maxStep)
        {
            var d = to - from;
            float len = d.Length();
            return len <= maxStep ? to : from + d / len * maxStep;
        }

        Vec2 Slot(ref Ctx c, int k)
        {
            double ang = c.W.Elapsed * 0.6 + Math.PI / 2 + k * Math.PI * 2 / 3;
            return c.Pos + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * Formation;
        }

        int NearestFrom(ref Ctx c, Vec2 pos, float range) => AbilitySystem.Nearest(c.W, pos, range, 1, Targets, null, 0) > 0 ? Targets[0] : -1;

        // ---------- Três Cabazords ----------

        void BotsStep(ref Ctx c, BotState b, float dt)
        {
            var comp = c.W.Companions;
            var e = c.W.Enemies;
            float range = c.Range;

            // atirador: fica na formação e dispara
            int sh = Ensure(ref c, BotShooter, AllySprite.BotShooter);
            if (sh >= 0)
            {
                ref var bot = ref comp.Items[sh];
                bot.Pos = Toward(bot.Pos, Slot(ref c, 0), 8f * dt);
                int t = NearestFrom(ref c, bot.Pos, range);
                if (t >= 0) bot.Param = AllyList.Angle(e.Pos[t] - bot.Pos);
                if (t >= 0 && c.Due(TShoot))
                {
                    int n = c.Base >= 4 ? 2 : 1;
                    var dir = (e.Pos[t] - bot.Pos).Normalized();
                    for (int k = 0; k < n; k++)
                    {
                        var s = c.NewShot(AllySprite.BotBolt, Rotate(dir, (k - (n - 1) * 0.5f) * 10f * Deg), c.Stat(StatId.ProjectileSpeed), c.Dmg(), 0.22f, range + 2f);
                        s.Pos = bot.Pos;
                        s.Variant = VBolt;
                        c.Fire(s);
                    }
                    c.Sound(0, bot.Pos);
                    c.Reset(TShoot, c.Stat(StatId.Cooldown));
                }
            }

            // brigão: vai até o inimigo e bate por contato
            int br = Ensure(ref c, BotBrawler, AllySprite.BotBrawler);
            if (br >= 0)
            {
                ref var bot = ref comp.Items[br];
                int t = NearestFrom(ref c, c.Pos, range);
                var goal = t >= 0 ? e.Pos[t] : Slot(ref c, 1);
                if (Vec2.DistanceSq(bot.Pos, c.Pos) > range * range * 1.5f) goal = Slot(ref c, 1);
                bot.Param = AllyList.Angle(goal - bot.Pos);
                bot.Pos = Toward(bot.Pos, goal, 6.5f * dt);
                if (c.Due(TBrawl))
                {
                    int n = AbilitySystem.Overlapping(c.W, bot.Pos, 0.55f, Hits);
                    if (n > 0)
                    {
                        for (int h = 0; h < n; h++)
                        {
                            int k = Hits[h];
                            CombatSystem.DamageEnemy(c.W, k, c.Dmg(0.9f), (e.Pos[k] - bot.Pos).Normalized(), 1.5f, c.Slot, c.Ability);
                            Combo(ref c, b, k, 2);
                        }
                        c.Sound(1, bot.Pos);
                        c.Reset(TBrawl, 0.45f);
                    }
                }
            }

            // investida: atravessa a fila
            int da = Ensure(ref c, BotDasher, AllySprite.BotDasher);
            if (da >= 0)
            {
                ref var bot = ref comp.Items[da];
                if (!c.Due(TDashing))
                {
                    bot.Pos += b.DashVel * dt;
                    bot.Param = AllyList.Angle(b.DashVel);
                    int n = AbilitySystem.Overlapping(c.W, bot.Pos, 0.6f, Hits);
                    for (int h = 0; h < n; h++)
                    {
                        int k = Hits[h];
                        if (!b.DashHits.Add(e.Id[k])) continue;
                        CombatSystem.DamageEnemy(c.W, k, c.Dmg(1.4f), b.DashVel.Normalized(), 2f, c.Slot, c.Ability);
                        Combo(ref c, b, k, 4);
                    }
                    if (c.S.Timers[TDashing] <= dt && c.Base >= 5)
                        Area.Burst(c.W, c.Slot, c.Ability, bot.Pos, c.Radius(1.2f), c.Dmg(0.8f), 1.5f, 0f, FxStyle.Robot);
                }
                else
                {
                    var home = Slot(ref c, 2);
                    bot.Pos = Toward(bot.Pos, home, 9f * dt);
                    bot.Param = AllyList.Angle(home - bot.Pos);
                    int t = NearestFrom(ref c, bot.Pos, range);
                    if (t >= 0 && c.Due(TDash))
                    {
                        b.DashVel = (e.Pos[t] - bot.Pos).Normalized() * 16f;
                        b.DashHits.Clear();
                        c.Reset(TDashing, 0.35f);
                        c.Reset(TDash, c.Period(2.4f));
                        c.Sound(2, bot.Pos);
                    }
                }
            }
            Prune(c.W, b);
        }

        /// <summary>Marca o robô que acertou; os três no mesmo alvo em 2 s = ataque combinado.</summary>
        void Combo(ref Ctx c, BotState b, int enemy, byte bit)
        {
            var e = c.W.Enemies;
            ushort id = e.Id[enemy];
            float now = c.W.Elapsed;
            b.Combo.TryGetValue(id, out var v);
            if (now - v.At > 2f) v.Mask = 0;
            v.Mask |= bit;
            v.At = now;
            if (v.Mask == 7)
            {
                v.Mask = 0;
                Area.Burst(c.W, c.Slot, c.Ability, e.Pos[enemy], c.Radius(c.Base >= 5 ? 2.0f : 1.6f), c.Dmg(2f), 2f, 0.3f, FxStyle.Robot);
                c.Sound(3, e.Pos[enemy]);
            }
            b.Combo[id] = v;
        }

        static void Prune(World w, BotState b)
        {
            if (w.Elapsed < b.PruneAt) return;
            b.PruneAt = w.Elapsed + 1f;
            b.Stale.Clear();
            foreach (var kv in b.Combo) if (w.Elapsed - kv.Value.At > 2f) b.Stale.Add(kv.Key);
            foreach (var id in b.Stale) b.Combo.Remove(id);
        }

        public override void OnShotHit(World w, ref Shot s, int enemy)
        {
            if (s.Variant != VBolt) return;
            var a = InstanceOf(w, s.Owner, s.Ability);
            if (a?.State is WeaponState st && st.Extra is BotState b)
            {
                var c = new Ctx(w, s.Owner, a, st);
                Combo(ref c, b, enemy, 1);
            }
        }

        // ---------- Cabazord Master ----------

        void MasterStep(ref Ctx c, BotState b, float dt)
        {
            var comp = c.W.Companions;
            var e = c.W.Enemies;
            int m = Ensure(ref c, Master, AllySprite.BotMaster);
            if (m < 0) return;
            ref var bot = ref comp.Items[m];

            if (!c.Due(TCharging))
            {
                bot.Pos += b.DashVel * dt;
                int n = AbilitySystem.Overlapping(c.W, bot.Pos, 0.9f, Hits);
                for (int h = 0; h < n; h++)
                {
                    int k = Hits[h];
                    if (b.DashHits.Add(e.Id[k])) CombatSystem.DamageEnemy(c.W, k, c.Dmg(1.8f), b.DashVel.Normalized(), 3f, c.Slot, c.Ability);
                }
                return;
            }

            var behind = c.Pos - c.Facing * 1.6f + new Vec2(0.6f, 0);
            int target = NearestFrom(ref c, bot.Pos, c.Range + 1f);
            var goal = target >= 0 && Vec2.DistanceSq(e.Pos[target], c.Pos) < 25f ? e.Pos[target] : behind;
            if (Vec2.DistanceSq(bot.Pos, c.Pos) > 81f) goal = behind;
            bot.Pos = Toward(bot.Pos, goal, 7f * dt);
            bot.Param = AllyList.Angle(goal - bot.Pos);

            if (target >= 0 && c.Due(TPunch) && Vec2.DistanceSq(e.Pos[target], bot.Pos) < 2.4f * 2.4f)
            {
                Area.Cone(c.W, c.Slot, c.Ability, bot.Pos, e.Pos[target] - bot.Pos, c.Radius(2.2f), 120f * Deg, c.Dmg(1.6f), 3f, 0f, FxStyle.Robot);
                c.Sound(4, bot.Pos);
                c.Reset(TPunch, c.Period(1.2f));
            }
            if (target >= 0 && c.Due(TBolt))
            {
                var s = c.NewShot(AllySprite.MasterBolt, e.Pos[target] - bot.Pos, c.Speed(13f), c.Dmg(1.4f), 0.35f, c.Range + 3f, 4);
                s.Pos = bot.Pos;
                s.Variant = VMasterBolt;
                c.Fire(s);
                c.Sound(0, bot.Pos);
                c.Reset(TBolt, c.Period(1.6f));
            }
            if (target >= 0 && c.Due(TCharge))
            {
                b.DashVel = (e.Pos[target] - bot.Pos).Normalized() * 14f;
                b.DashHits.Clear();
                c.Reset(TCharging, 0.45f);
                c.Reset(TCharge, c.Period(4f));
                c.Sound(2, bot.Pos);
            }
            if (target >= 0 && c.Due(TSlam))
            {
                c.Strike(bot.Pos, 0.4f, c.Radius(3.5f), c.Dmg(2.5f), FxStyle.Robot, 0, 3f, 0.6f);
                c.Sound(5, bot.Pos);
                c.Reset(TSlam, c.Period(c.Evo >= 2 ? 5.5f : 7f));
            }
            if (c.Evo >= 3 && target >= 0 && c.Due(TCombined))
            {
                bot.Pos = c.Pos + new Vec2(0, 0.8f);
                c.Strike(c.Pos, 0.6f, c.Radius(5f), c.Dmg(3.5f), FxStyle.Robot, 0, 4f, 0.8f);
                c.Sound(6);
                c.Reset(TCombined, c.Period(10f));
            }
        }
    }
}
