using System;

namespace ES.Sim
{
    /// <summary>Estado de uma arma de personagem: temporizadores, contadores e vagas de companheiros.</summary>
    public sealed class WeaponState
    {
        public readonly float[] Timers = new float[8];
        public readonly int[] Counters = new int[8];
        public readonly int[] Companions = { -1, -1, -1, -1 };
        public object Extra;
    }

    /// <summary>
    /// Base das armas dos personagens. A mesma classe atende a arma inicial e a evoluída
    /// (parâmetro "evolved" = 1): a evoluída roda o ataque inicial como no nível 5 e acrescenta o novo.
    /// Atributos: Damage, Cooldown, Range e ProjectileSpeed valem para o ataque principal; Area e
    /// Duration são multiplicadores (1 = base) de raios e durações; ataques secundários escalam pela
    /// razão entre o valor atual e o base da habilidade.
    /// </summary>
    public abstract class CharacterWeapon : IAbilityBehavior, IShotHandler
    {
        protected static readonly int[] Targets = new int[16];

        public virtual void OnAcquire(World w, int slot, AbilityInstance a) => a.State = new WeaponState();

        public void Tick(World w, int slot, AbilityInstance a, float dt)
        {
            var s = a.State as WeaponState ?? (WeaponState)(a.State = new WeaponState());
            for (int i = 0; i < s.Timers.Length; i++) if (s.Timers[i] > 0) s.Timers[i] -= dt;
            var c = new Ctx(w, slot, a, s);
            Step(ref c, dt);
        }

        protected abstract void Step(ref Ctx c, float dt);

        public virtual void OnShotHit(World w, ref Shot s, int enemy) { }
        public virtual void OnShotEnd(World w, ref Shot s) { }
        public virtual void OnStrike(World w, in Strike s) { }

        /// <summary>Contexto de um tick: atalhos para atributos, parâmetros, mira e disparo.</summary>
        protected struct Ctx
        {
            public readonly World W;
            public readonly int Slot;
            public readonly AbilityInstance A;
            public readonly PlayerState P;
            public readonly WeaponState S;
            public readonly bool Evolved;
            /// <summary>Nível do ataque inicial (5 quando evoluída).</summary>
            public readonly int Base;
            /// <summary>Nível da evolução (0 antes de evoluir).</summary>
            public readonly int Evo;

            public Ctx(World w, int slot, AbilityInstance a, WeaponState s)
            {
                W = w; Slot = slot; A = a; S = s;
                P = w.Players[slot];
                Evolved = a.Def.Param("evolved", 0) > 0;
                Base = Evolved ? 5 : a.Level;
                Evo = Evolved ? a.Level : 0;
            }

            public int Ability => A.AbilityIndex;
            public Vec2 Pos => P.Pos;
            public float Stat(StatId id) => A.Get(P, id);
            public float Param(string key, float fallback = 0) => A.Def.Param(key, fallback);

            /// <summary>Dano do ataque principal × <paramref name="mult"/>.</summary>
            public float Dmg(float mult = 1f) => Stat(StatId.Damage) * mult;
            /// <summary>Raio base × multiplicador de área.</summary>
            public float Radius(float baseRadius) => baseRadius * Scale(StatId.Area);
            public float Time(float baseSeconds) => baseSeconds * Scale(StatId.Duration);
            /// <summary>Multiplicador (Area/Duration); habilidade sem o atributo vale 1.</summary>
            float Scale(StatId id)
            {
                float v = Stat(id);
                return v > 0 ? Math.Max(0.1f, v) : 1f;
            }

            /// <summary>Período de um ataque secundário escalado pelas reduções de recarga.</summary>
            public float Period(float baseSeconds)
            {
                float b = A.Def.BaseStats.GetBase(StatId.Cooldown);
                float scale = b > 0 ? Stat(StatId.Cooldown) / b : 1f;
                return Math.Max(0.08f, baseSeconds * scale);
            }

            public float Speed(float baseSpeed)
            {
                float b = A.Def.BaseStats.GetBase(StatId.ProjectileSpeed);
                return b > 0 ? baseSpeed * Stat(StatId.ProjectileSpeed) / b : baseSpeed;
            }

            public float Range => Stat(StatId.Range);

            public bool Due(int timer) => S.Timers[timer] <= 0;
            public void Reset(int timer, float seconds) => S.Timers[timer] = seconds;
            /// <summary>Contador cíclico: true a cada <paramref name="every"/> chamadas.</summary>
            public bool Every(int counter, int every) => every > 0 && ++S.Counters[counter] % every == 0;

            public int Nearest(float range) => AbilitySystem.Nearest(W, P.Pos, range, 1, Targets, null, 0) > 0 ? Targets[0] : -1;

            public Vec2 DirTo(int enemy)
            {
                if (enemy < 0) return Facing;
                var d = (W.Enemies.Pos[enemy] - P.Pos).Normalized();
                return d.LengthSq() > 0 ? d : Facing;
            }

            public Vec2 Facing => P.Facing.LengthSq() > 1e-4f ? P.Facing.Normalized() : new Vec2(0, -1);

            public Shot NewShot(AllySprite sprite, Vec2 dir, float speed, float damage, float radius, float range, int pierce = 1)
            {
                speed = Math.Max(0.5f, speed);
                return new Shot
                {
                    Owner = (byte)Slot, Ability = (short)Ability, Sprite = sprite,
                    Pos = P.Pos, Vel = dir.Normalized() * speed, Damage = damage, Radius = radius,
                    Life = Math.Max(0.1f, range / speed), Pierce = pierce,
                };
            }

            public bool Fire(in Shot s) => W.Shots.Spawn(s) >= 0;

            /// <summary>Som de disparo: índice em "sfx" da habilidade.</summary>
            public void Sound(int variant, Vec2? at = null)
            {
                var pos = at ?? P.Pos;
                W.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.ProjectileFired, A = Slot, B = Ability, X = pos.X, Y = pos.Y, Value = variant });
            }

            public void Strike(Vec2 at, float delay, float radius, float damage, FxStyle style, byte variant = 0, float knock = 1f, float stun = 0f)
            {
                W.Strikes.Spawn(W, new Strike
                {
                    Owner = (byte)Slot, Ability = (short)Ability, Variant = variant, Pos = at, Delay = delay,
                    Radius = radius, Damage = damage, Knockback = knock, Stun = stun, Style = style,
                });
            }

            public static Vec2 Rotate(Vec2 v, float radians)
            {
                float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
                return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
            }
        }

        protected static Vec2 Rotate(Vec2 v, float radians) => Ctx.Rotate(v, radians);
        protected const float Deg = (float)(Math.PI / 180.0);

        /// <summary>Instância da habilidade dona de um tiro (para os ganchos de tiro).</summary>
        protected static AbilityInstance InstanceOf(World w, int owner, int ability)
        {
            var p = owner >= 0 && owner < PlayerStore.MaxSlots ? w.Players.Slots[owner] : null;
            if (p == null) return null;
            foreach (var a in p.Abilities) if (a.AbilityIndex == ability) return a;
            return null;
        }

        /// <summary>Tiro filho (moeda, fragmento) herdando dono e habilidade; respeita a profundidade máxima.</summary>
        protected static bool SpawnChild(World w, in Shot parent, Shot child)
        {
            if (parent.Depth + 1 > Kit.MaxDepth) return false;
            child.Owner = parent.Owner;
            child.Ability = parent.Ability;
            child.Depth = (byte)(parent.Depth + 1);
            return w.Shots.Spawn(child) >= 0;
        }
    }
}
