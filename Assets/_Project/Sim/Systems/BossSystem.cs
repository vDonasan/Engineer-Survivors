using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>Estados da máquina de estados de um boss (também vão no snapshot para as animações).</summary>
    public enum BossState : byte { Entering, Chase, Windup, Attack, Recover, Rage }

    /// <summary>Ataques. 1–5 Guirotto, 6–11 Mago da Elétrica.</summary>
    public enum BossAttack : byte
    {
        None,
        Acorde, Ressonancia, ChuvaNotas, Concerto, Refrao,
        Raio, Cadeia, Tempestade, Orbes, Sobrecarga, Teleporte,
    }

    /// <summary>Tipo visual dos projéteis hostis dos bosses (acima dos tipos de inimigo).</summary>
    public static class HostileKind
    {
        public const byte Wave = 100, Note = 101, Orb = 102, Spark = 103;
        public static bool IsBoss(byte type) => type >= Wave && type <= Spark;
    }

    public enum HazardShape : byte { Circle, Line, Ring }

    /// <summary>Área perigosa sinalizada: dispara em <see cref="FireAt"/>.</summary>
    public struct BossHazard
    {
        public HazardShape Shape;
        public FxStyle Style;
        public Vec2 Pos;
        /// <summary>Círculo: raio. Linha: comprimento. Anel: raio externo.</summary>
        public float Size;
        /// <summary>Linha: ângulo (rad). Anel: raio interno. Linha também usa <see cref="Width"/>.</summary>
        public float Extra, Width;
        public float FireAt, Damage;
    }

    public sealed class BossRuntime
    {
        public int Boss;
        public ushort EnemyId;
        public BossState State;
        public BossAttack Attack, Last, Combo;
        public bool Bravo;
        /// <summary>Recorrência no Modo Infinito (0 = primeira aparição).</summary>
        public int Cycle;
        public float StateUntil, NextAttackAt, StepAt;
        public int Step, AttackCount, Target = -1;
        public Vec2 Aim, Facing = new Vec2(0, -1);
        public readonly int[] Targeted = new int[PlayerStore.MaxSlots];
    }

    /// <summary>Estado dos bosses e do Modo Infinito da partida.</summary>
    public sealed class BossDirector
    {
        public const int MaxHazards = 64;
        public readonly List<BossRuntime> Active = new List<BossRuntime>(4);
        public readonly BossHazard[] Hazards = new BossHazard[MaxHazards];
        public int HazardCount;
        /// <summary>Bosses de horário já surgidos (cada um uma única vez).</summary>
        public bool[] Spawned = new bool[0];
        public bool[] Warned = new bool[0];
        public readonly List<int> Defeated = new List<int>();
        /// <summary>Morte do boss final: a vitória é declarada neste instante (0 = não).</summary>
        public float VictoryAt;
        public bool Infinite;
        public float InfiniteSince, NextInfiniteBossAt, NextStormAt;
        public int InfiniteCycle;
        /// <summary>Próximo boss anunciado na interface (−1 nenhum; 255 dueto).</summary>
        public int NextBoss = -1;
        public float NextBossAt;
        public bool ScheduleSent;
    }

    /// <summary>
    /// Chegada, máquina de estados, ataques e fúria dos bosses; regras do Modo Infinito.
    /// Tudo roda no host; os clientes recebem estados (snapshot) e áreas sinalizadas (eventos de regra).
    /// </summary>
    public static class BossSystem
    {
        public const float WarnSeconds = 5f;
        const float MaxSpeedChase = 1f;

        static readonly List<int> Candidates = new List<int>(8);
        static readonly List<float> Weights = new List<float>(8);

        // ---------------- agenda ----------------

        public static void Step(World w, float dt)
        {
            var d = w.Bosses;
            if (d.Spawned.Length != w.Db.BossCount) { d.Spawned = new bool[w.Db.BossCount]; d.Warned = new bool[w.Db.BossCount]; }
            Schedule(w);
            for (int i = d.Active.Count - 1; i >= 0; i--)
            {
                var b = d.Active[i];
                int e = Find(w, b.EnemyId);
                if (e < 0) { d.Active.RemoveAt(i); continue; }
                Think(w, b, e, dt);
            }
            Hazards(w);
            if (d.Infinite) InfiniteEvents(w);
        }

        static void Schedule(World w)
        {
            var d = w.Bosses;
            if (!d.Infinite)
            {
                for (int k = 0; k < w.Db.BossCount; k++)
                {
                    if (d.Spawned[k]) continue;
                    float at = w.Db.Boss(k).Def.spawnSeconds;
                    if (!d.Warned[k] && w.Elapsed >= at - WarnSeconds)
                    {
                        d.Warned[k] = true;
                        w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.BossIncoming, A = k, X = at });
                    }
                    if (w.Elapsed >= at - 1e-4f)
                    {
                        d.Spawned[k] = true;
                        Spawn(w, k, 0);
                    }
                }
            }
            else if (w.Elapsed >= d.NextInfiniteBossAt - 1e-4f)
            {
                int cycle = ++d.InfiniteCycle;
                // alterna os bosses; do terceiro ciclo em diante, os dois juntos (dueto)
                if (cycle >= 3) for (int k = 0; k < w.Db.BossCount; k++) Spawn(w, k, cycle);
                else Spawn(w, (cycle - 1) % Math.Max(1, w.Db.BossCount), cycle);
                d.NextInfiniteBossAt += w.Rules.infiniteBossEvery;
                d.ScheduleSent = false;
            }
            else if (d.Infinite && !d.Warned[0] && w.Elapsed >= d.NextInfiniteBossAt - WarnSeconds && w.Db.BossCount > 0)
            {
                d.Warned[0] = true;
                int next = d.InfiniteCycle + 1;
                w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.BossIncoming, A = next >= 3 ? 255 : (next - 1) % w.Db.BossCount, X = d.NextInfiniteBossAt });
            }
            UpdateNext(w);
        }

        /// <summary>Próximo boss para a contagem regressiva da interface.</summary>
        static void UpdateNext(World w)
        {
            var d = w.Bosses;
            int next = -1;
            float at = 0;
            if (d.Infinite)
            {
                int c = d.InfiniteCycle + 1;
                if (w.Db.BossCount > 0) { next = c >= 3 ? 255 : (c - 1) % w.Db.BossCount; at = d.NextInfiniteBossAt; }
            }
            else
            {
                for (int k = 0; k < w.Db.BossCount; k++)
                {
                    if (d.Spawned[k]) continue;
                    float s = w.Db.Boss(k).Def.spawnSeconds;
                    if (next < 0 || s < at) { next = k; at = s; }
                }
            }
            if (next == d.NextBoss && Math.Abs(at - d.NextBossAt) < 1e-3f && d.ScheduleSent) return;
            d.NextBoss = next;
            d.NextBossAt = at;
            d.ScheduleSent = true;
            if (d.Infinite) d.Warned[0] = false;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.BossSchedule, A = next < 0 ? 254 : next, X = at });
        }

        /// <summary>Cria o boss perto de um jogador ativo (fora do alcance de contato). Retorna o runtime ou null.</summary>
        public static BossRuntime Spawn(World w, int boss, int cycle)
        {
            var rb = w.Db.Boss(boss);
            var def = rb.Def;
            var target = AnyActive(w);
            var center = target?.Pos ?? new Vec2(w.Arena.width * 0.5f, w.Arena.height * 0.5f);
            Vec2 pos = center;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float a = w.Rng.NextFloat() * (float)Math.PI * 2;
                var p = center + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * 6.5f;
                if (ArenaCollision.Blocked(p, 1.2f, w.Arena)) continue;
                pos = p;
                break;
            }
            int i = SpawnDirector.SpawnEnemy(w, rb.Enemy, pos, false);
            if (i < 0)
            {
                // arena cheia: abre espaço removendo o inimigo comum mais distante
                int far = -1;
                float best = -1;
                for (int k = 0; k < w.Enemies.Count; k++)
                {
                    if (w.Db.Enemy(w.Enemies.Type[k]).Behavior == EnemyBehavior.Boss) continue;
                    float dd = Vec2.DistanceSq(w.Enemies.Pos[k], center);
                    if (dd > best) { best = dd; far = k; }
                }
                if (far < 0) return null;
                w.Enemies.RemoveAt(far);
                i = SpawnDirector.SpawnEnemy(w, rb.Enemy, pos, false);
                if (i < 0) return null;
            }
            var e = w.Enemies;
            float team = 1f + def.teamHp * Math.Max(0, TeamSize(w) - 1);
            float hp = e.MaxHp[i] * team * (1f + w.Rules.infiniteBossHpPerCycle * cycle); // SpawnEnemy já aplicou a curva do Modo Infinito
            e.Hp[i] = e.MaxHp[i] = hp;
            e.Flags[i] |= EnemyFlags.Invulnerable;
            var b = new BossRuntime
            {
                Boss = boss, EnemyId = e.Id[i], Cycle = cycle, State = BossState.Entering,
                StateUntil = w.Elapsed + def.entrySeconds, NextAttackAt = w.Elapsed + def.entrySeconds + 1f,
            };
            w.Bosses.Active.Add(b);
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.BossSpawned, A = boss, B = e.Id[i], C = cycle, X = pos.X, Y = pos.Y });
            w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst, A = -1, B = (int)(def.final ? FxStyle.Storm : FxStyle.Sound), X = pos.X, Y = pos.Y, Value = 3.5f });
            return b;
        }

        static int TeamSize(World w)
        {
            int n = 0;
            foreach (var p in w.Players.Slots) if (p != null && p.Status != PlayerStatus.Inactive) n++;
            return Math.Max(1, n);
        }

        // ---------------- máquina de estados ----------------

        static void Think(World w, BossRuntime b, int i, float dt)
        {
            var e = w.Enemies;
            var rb = w.Db.Boss(b.Boss);
            var def = rb.Def;
            e.Vel[i] = Vec2.Zero;

            if (!b.Bravo && b.State != BossState.Entering && e.Hp[i] <= e.MaxHp[i] * def.bravoThreshold)
            {
                // fúria: uma única vez; não restaura vida; pausa a sequência por alguns instantes
                b.Bravo = true;
                b.State = BossState.Rage;
                b.StateUntil = w.Elapsed + def.rageSeconds;
                b.Attack = BossAttack.None;
                b.Combo = BossAttack.None;
                w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.BossBravo, A = b.Boss, B = b.EnemyId, X = e.Pos[i].X, Y = e.Pos[i].Y });
                w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst, A = -1, B = (int)(IsMago(rb) ? FxStyle.Storm : FxStyle.Sound), X = e.Pos[i].X, Y = e.Pos[i].Y, Value = 4.5f });
            }

            var target = TargetOf(w, b, e.Pos[i]);
            switch (b.State)
            {
                case BossState.Entering:
                    if (w.Elapsed < b.StateUntil) return;
                    e.Flags[i] = (byte)(e.Flags[i] & ~EnemyFlags.Invulnerable);
                    b.State = BossState.Chase;
                    return;
                case BossState.Rage:
                    if (w.Elapsed < b.StateUntil) return;
                    b.State = BossState.Chase;
                    b.NextAttackAt = w.Elapsed + 0.5f;
                    return;
                case BossState.Chase:
                    if (target == null) return;
                    Move(w, b, i, rb, target, dt);
                    if (w.Elapsed >= b.NextAttackAt) BeginWindup(w, b, i, rb, target);
                    return;
                case BossState.Windup:
                    if (target != null) Face(b, target.Pos - e.Pos[i]);
                    if (w.Elapsed < b.StateUntil) return;
                    b.State = BossState.Attack;
                    b.Step = 0;
                    b.StepAt = w.Elapsed;
                    if (target != null) b.Aim = target.Pos;
                    return;
                case BossState.Attack:
                    if (RunAttack(w, b, i, rb)) return;
                    b.State = BossState.Recover;
                    b.StateUntil = w.Elapsed + Recover(rb, b);
                    return;
                case BossState.Recover:
                    if (w.Elapsed < b.StateUntil) return;
                    b.State = BossState.Chase;
                    b.NextAttackAt = w.Elapsed + (b.Combo != BossAttack.None ? 0.25f : Cooldown(w, rb, b));
                    return;
            }
        }

        static bool IsMago(ResolvedBoss rb) => rb.Def.id == "mago";

        static float Cooldown(World w, ResolvedBoss rb, BossRuntime b)
        {
            float cd = rb.Param("cooldown", b.Bravo, 2f);
            cd *= Math.Max(0.6f, 1f - rb.Def.teamCooldown * (TeamSize(w) - 1));
            cd *= Math.Max(0.65f, (float)Math.Pow(0.92, b.Cycle));
            return cd;
        }

        static float Recover(ResolvedBoss rb, BossRuntime b)
        {
            float r = rb.Param("recover", b.Bravo, 0.7f);
            if (b.Attack == BossAttack.Sobrecarga) r += rb.Param("overloadRecover", 1.5f);
            return r;
        }

        /// <summary>Alvo da vez: o jogador ativo menos visado recentemente (empate: o mais perto).</summary>
        static PlayerState TargetOf(World w, BossRuntime b, Vec2 pos)
        {
            var cur = b.Target >= 0 ? w.Players[b.Target] : null;
            if (cur != null && cur.IsActive) return cur;
            b.Target = PickTarget(w, b, pos);
            return b.Target >= 0 ? w.Players[b.Target] : null;
        }

        static int PickTarget(World w, BossRuntime b, Vec2 pos)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                float score = (float)Math.Sqrt(Vec2.DistanceSq(p.Pos, pos)) + 8f * b.Targeted[p.Slot];
                if (score < bestScore) { bestScore = score; best = p.Slot; }
            }
            return best;
        }

        static void Move(World w, BossRuntime b, int i, ResolvedBoss rb, PlayerState target, float dt)
        {
            var e = w.Enemies;
            var to = target.Pos - e.Pos[i];
            float dist = to.Length();
            var n = dist > 1e-4f ? to / dist : Vec2.Zero;
            float keepMin = rb.Param("keepMin", 2f), keepMax = rb.Param("keepMax", 7f);
            Vec2 dir = Vec2.Zero;
            if (dist > keepMax) dir = n;
            else if (dist < keepMin) dir = -n;
            else
            {
                // posicionamento: circula o alvo
                var side = new Vec2(-n.Y, n.X);
                dir = side * ((b.EnemyId & 1) == 0 ? MaxSpeedChase : -MaxSpeedChase) * 0.6f;
            }
            float speed = e.Speed[i] * (b.Bravo ? 1.25f : 1f) * (e.SlowUntil[i] > w.Elapsed ? 1f - e.SlowAmount[i] * 0.5f : 1f);
            e.Vel[i] = dir * speed;
            e.Pos[i] = ArenaCollision.Resolve(e.Pos[i] + e.Vel[i] * dt, e.Radius[i], w.Arena);
            Face(b, dir.LengthSq() > 1e-6f ? dir : to);
        }

        static void Face(BossRuntime b, Vec2 v)
        {
            if (v.LengthSq() > 1e-6f) b.Facing = v.Normalized();
        }

        /// <summary>Direção de animação 0..7: baixo, cima, esquerda, direita, baixo-esq., baixo-dir., cima-esq., cima-dir.</summary>
        public static byte Dir8(Vec2 f)
        {
            double a = Math.Atan2(f.Y, f.X); // y cresce para cima no mundo
            int oct = (int)Math.Round(a / (Math.PI / 4)) & 7; // 0 = direita, 2 = cima, 4 = esquerda, 6 = baixo
            switch (oct)
            {
                case 0: return 3;
                case 1: return 7;
                case 2: return 1;
                case 3: return 6;
                case 4: return 2;
                case 5: return 4;
                case 6: return 0;
                default: return 5;
            }
        }

        // ---------------- escolha do ataque ----------------

        static void BeginWindup(World w, BossRuntime b, int i, ResolvedBoss rb, PlayerState target)
        {
            var e = w.Enemies;
            b.Target = PickTarget(w, b, e.Pos[i]);
            target = b.Target >= 0 ? w.Players[b.Target] : target;
            if (target == null) return;
            for (int s = 0; s < b.Targeted.Length; s++) b.Targeted[s] = Math.Max(0, b.Targeted[s] - 1);
            b.Targeted[target.Slot] += 2;

            float dist = (float)Math.Sqrt(Vec2.DistanceSq(target.Pos, e.Pos[i]));
            var atk = b.Combo != BossAttack.None ? b.Combo : IsMago(rb) ? ChooseMago(w, b, rb, dist) : ChooseGuirotto(w, b, dist);
            b.Combo = BossAttack.None;
            b.Last = b.Attack == BossAttack.None ? b.Last : b.Attack;
            b.Attack = atk;
            b.AttackCount++;
            b.Aim = target.Pos;
            b.State = BossState.Windup;
            float windup = rb.Param("windup", b.Bravo, 0.5f) * w.Balance.Telegraph;
            if (atk == BossAttack.Sobrecarga)
            {
                windup = rb.Param("overloadCharge", 2f) * w.Balance.Telegraph;
                OverloadMarks(w, b, i, rb, windup);
            }
            else if (atk == BossAttack.Teleporte) windup = 0.3f;
            b.StateUntil = w.Elapsed + windup;
            Face(b, target.Pos - e.Pos[i]);
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.BossAction, A = b.EnemyId, B = (int)atk, C = (int)BossState.Windup, X = windup });
        }

        static BossAttack ChooseGuirotto(World w, BossRuntime b, float dist)
        {
            bool special = b.AttackCount % 4 == 3;
            if (special) return b.Bravo || b.Cycle > 0 ? (b.AttackCount % 8 == 7 ? BossAttack.Concerto : BossAttack.Refrao) : BossAttack.Concerto;
            Candidates.Clear(); Weights.Clear();
            Add(BossAttack.Acorde, dist > 4.5f ? 2f : 1f);
            Add(BossAttack.Ressonancia, dist < 4.5f ? 3f : dist < 6.5f ? 1.2f : 0.3f);
            Add(BossAttack.ChuvaNotas, dist > 3.5f ? 2f : 1f);
            var pick = Pick(w, b);
            // combinação da fúria: o acorde emenda numa ressonância
            if (b.Bravo && pick == BossAttack.Acorde) b.Combo = BossAttack.Ressonancia;
            return pick;
        }

        static BossAttack ChooseMago(World w, BossRuntime b, ResolvedBoss rb, float dist)
        {
            if (dist < rb.Param("teleportBelow", 3f) && b.Last != BossAttack.Teleporte && b.Attack != BossAttack.Teleporte) return BossAttack.Teleporte;
            int every = b.Bravo ? 4 : 5;
            if (b.AttackCount % every == every - 1 && dist < rb.Param("overloadRadius", true, 7f) + 4f) return BossAttack.Sobrecarga;
            Candidates.Clear(); Weights.Clear();
            Add(BossAttack.Raio, dist > 4f ? 2f : 1f);
            Add(BossAttack.Cadeia, dist < 9f ? 1.6f : 0.4f);
            Add(BossAttack.Tempestade, 1.5f);
            Add(BossAttack.Orbes, 1.4f);
            var pick = Pick(w, b);
            if ((b.Bravo || b.Cycle > 0) && pick == BossAttack.Raio) b.Combo = BossAttack.Cadeia;
            if (b.Bravo && pick == BossAttack.Orbes) b.Combo = BossAttack.Tempestade;
            return pick;
        }

        static void Add(BossAttack a, float weight) { Candidates.Add((int)a); Weights.Add(weight); }

        /// <summary>Sorteio ponderado sem repetir o último ataque.</summary>
        static BossAttack Pick(World w, BossRuntime b)
        {
            float total = 0;
            for (int k = 0; k < Candidates.Count; k++) { if (Candidates[k] == (int)b.Attack) Weights[k] = 0; total += Weights[k]; }
            if (total <= 0) return (BossAttack)Candidates[0];
            float r = w.Rng.NextFloat() * total;
            for (int k = 0; k < Candidates.Count; k++) { r -= Weights[k]; if (r < 0) return (BossAttack)Candidates[k]; }
            return (BossAttack)Candidates[Candidates.Count - 1];
        }

        // ---------------- ataques ----------------

        /// <summary>Executa o ataque por passos; false quando terminou.</summary>
        static bool RunAttack(World w, BossRuntime b, int i, ResolvedBoss rb)
        {
            if (w.Elapsed < b.StepAt) return true;
            var e = w.Enemies;
            var pos = e.Pos[i];
            var target = b.Target >= 0 ? w.Players[b.Target] : null;
            var aim = target != null && target.IsActive ? target.Pos : b.Aim;
            bool bravo = b.Bravo;
            int extra = Extra(w) + b.Cycle;
            int step = b.Step++;
            if (step == 0) w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.BossAction, A = b.EnemyId, B = (int)b.Attack, C = (int)BossState.Attack });

            switch (b.Attack)
            {
                case BossAttack.Acorde:
                {
                    int pulses = bravo ? 3 : 2;
                    int n = (int)rb.Param("waveCount", bravo, 3) + (extra > 0 ? 1 : 0);
                    float spread = rb.Param("waveSpread", 14) * Deg;
                    float speed = rb.Param("waveSpeed", bravo, 6.5f);
                    Fan(w, pos, Angle(aim - pos), n, spread * (n - 1), speed, Dmg(w, rb, "waveDamage", 14), rb.Param("waveRadius", 0.5f), 5f, HostileKind.Wave, 0, true, 0);
                    Fx(w, pos, FxStyle.Sound, 1.4f);
                    b.StepAt = w.Elapsed + 0.3f;
                    return step + 1 < pulses;
                }
                case BossAttack.Ressonancia:
                {
                    float r = rb.Param("resonanceRadius", bravo, 3.6f);
                    float delay = rb.Param("resonanceDelay", bravo, 1.2f) * w.Balance.Telegraph;
                    if (step == 0)
                    {
                        AddHazard(w, HazardShape.Circle, FxStyle.Sound, pos, r, 0, 0, delay, Dmg(w, rb, "resonanceDamage", 26));
                        if (bravo) AddHazard(w, HazardShape.Ring, FxStyle.Sound, pos, r + 2.6f, r, 0, delay + 0.55f, Dmg(w, rb, "resonanceDamage", 26) * 0.8f);
                        b.StepAt = w.Elapsed + delay + (bravo ? 0.6f : 0.15f);
                        return true;
                    }
                    return false;
                }
                case BossAttack.ChuvaNotas:
                {
                    int n = (int)rb.Param("noteCount", bravo, 7) + extra;
                    float arc = rb.Param("noteArc", 110) * Deg;
                    float turn = rb.Param("noteTurn", 0.7f);
                    float baseA = Angle(aim - pos) + (step == 1 ? arc / (2f * Math.Max(1, n - 1)) : 0);
                    for (int k = 0; k < n; k++)
                    {
                        float a = baseA - arc * 0.5f + arc * k / Math.Max(1, n - 1);
                        int h = Shoot(w, pos, a, rb.Param("noteSpeed", 5.2f), Dmg(w, rb, "noteDamage", 10), 0.32f, 5f, HostileKind.Note);
                        if (h < 0) break;
                        w.Hostile.Turn[h] = (k & 1) == 0 ? turn : -turn;
                        if (k % 3 == 1) w.Hostile.Bounces[h] = 1;
                    }
                    Fx(w, pos, FxStyle.Sound, 1.2f);
                    b.StepAt = w.Elapsed + 0.35f;
                    return bravo && step == 0;
                }
                case BossAttack.Concerto:
                {
                    float total = rb.Param("concertSeconds", 2.4f);
                    float every = rb.Param("concertEvery", bravo, 0.3f);
                    int steps = Math.Max(1, (int)(total / every));
                    int ring = (int)rb.Param("concertRing", bravo, 8) + extra;
                    float offset = step * 0.35f;
                    for (int k = 0; k < ring; k++)
                        Shoot(w, pos, offset + (float)Math.PI * 2 * k / ring, rb.Param("noteSpeed", 5.2f) * 0.85f, Dmg(w, rb, "noteDamage", 10), 0.32f, 4.5f, HostileKind.Note);
                    if (step == 0 || step == steps / 2)
                    {
                        int marks = (int)rb.Param("concertMarks", 1);
                        foreach (var p in w.Players.Slots)
                        {
                            if (p == null || !p.IsActive) continue;
                            for (int m = 0; m < marks; m++)
                                AddHazard(w, HazardShape.Circle, FxStyle.Sound, p.Pos + Jitter(w, m == 0 ? 0 : 2f), rb.Param("concertMarkRadius", 1.8f), 0, 0,
                                    rb.Param("concertMarkDelay", 1.1f) * w.Balance.Telegraph, Dmg(w, rb, "resonanceDamage", 26) * 0.7f);
                        }
                    }
                    b.StepAt = w.Elapsed + every;
                    return step + 1 < steps;
                }
                case BossAttack.Refrao:
                {
                    if (step == 0)
                    {
                        int ring = (int)rb.Param("chorusRing", 16) + extra;
                        for (int k = 0; k < ring; k++)
                        {
                            int h = Shoot(w, pos, (float)Math.PI * 2 * k / ring, rb.Param("noteSpeed", 5.2f), Dmg(w, rb, "noteDamage", 10), 0.32f, 5f, HostileKind.Note);
                            if (h >= 0) w.Hostile.Turn[h] = 0.5f;
                        }
                        float r = rb.Param("resonanceRadius", true, 4.6f);
                        AddHazard(w, HazardShape.Circle, FxStyle.Sound, pos, r, 0, 0, 1.0f * w.Balance.Telegraph, Dmg(w, rb, "resonanceDamage", 26));
                        b.StepAt = w.Elapsed + 0.6f;
                        return true;
                    }
                    Fan(w, pos, Angle(aim - pos), 5, 60 * Deg, rb.Param("waveSpeed", true, 8.5f), Dmg(w, rb, "waveDamage", 14), rb.Param("waveRadius", 0.5f), 5f, HostileKind.Wave, 0, true, 0);
                    b.StepAt = w.Elapsed + 0.5f;
                    return step < 2;
                }
                case BossAttack.Raio:
                {
                    if (step == 0)
                    {
                        float aimT = rb.Param("boltAim", bravo, 0.7f) * w.Balance.Telegraph;
                        int n = bravo ? (int)rb.Param("boltCountBravo", 3) : 1 + (b.Cycle > 0 ? 2 : 0);
                        float fan = rb.Param("boltFan", 22) * Deg, a0 = Angle(aim - pos);
                        for (int k = 0; k < n; k++)
                        {
                            float a = a0 + (k - (n - 1) * 0.5f) * fan;
                            AddHazard(w, HazardShape.Line, FxStyle.Electric, pos, rb.Param("boltLength", 16), a, rb.Param("boltWidth", 0.9f), aimT, Dmg(w, rb, "boltDamage", 22));
                        }
                        b.StepAt = w.Elapsed + aimT + 0.15f;
                        return true;
                    }
                    return false;
                }
                case BossAttack.Cadeia:
                    Chain(w, b, pos, rb, bravo);
                    return false;
                case BossAttack.Tempestade:
                {
                    if (step > 0) return false;
                    int marks = (int)rb.Param("stormMarks", bravo, 4) + (TeamSize(w) - 1) + extra;
                    float spread = rb.Param("stormSpread", 4);
                    float delay = rb.Param("stormDelay", bravo, 1.25f) * w.Balance.Telegraph;
                    float radius = rb.Param("stormRadius", 1.7f), dmg = Dmg(w, rb, "stormDamage", 24);
                    int placed = 0;
                    foreach (var p in w.Players.Slots)
                        if (p != null && p.IsActive && placed < marks) { AddHazard(w, HazardShape.Circle, FxStyle.Storm, p.Pos, radius, 0, 0, delay, dmg); placed++; }
                    int slot = 0;
                    while (placed < marks)
                    {
                        var p = NthActive(w, slot++);
                        if (p == null) break;
                        if (bravo && placed % 3 == 0)
                        {
                            // padrão em cruz no alvo (só na fúria)
                            var c = p.Pos;
                            for (int k = 1; k <= 2 && placed < marks + 4; k++)
                            {
                                AddHazard(w, HazardShape.Circle, FxStyle.Storm, c + new Vec2(radius * 1.8f * k, 0), radius, 0, 0, delay + 0.15f * k, dmg);
                                AddHazard(w, HazardShape.Circle, FxStyle.Storm, c + new Vec2(-radius * 1.8f * k, 0), radius, 0, 0, delay + 0.15f * k, dmg);
                                placed += 2;
                            }
                        }
                        else { AddHazard(w, HazardShape.Circle, FxStyle.Storm, p.Pos + Jitter(w, spread), radius, 0, 0, delay, dmg); placed++; }
                    }
                    b.StepAt = w.Elapsed + delay * 0.6f;
                    return true;
                }
                case BossAttack.Orbes:
                {
                    if (step > 0) return false;
                    int n = (int)rb.Param("orbCount", bravo, 6) + extra;
                    int homing = (int)rb.Param("orbHoming", bravo, 2);
                    float speed = rb.Param("orbSpeed", 3.2f), turn = rb.Param("orbTurn", 0.9f);
                    float a0 = Angle(aim - pos);
                    for (int k = 0; k < n; k++)
                    {
                        int h = Shoot(w, pos, a0 + (float)Math.PI * 2 * k / n, speed, Dmg(w, rb, "orbDamage", 12), 0.4f, 6f, HostileKind.Orb);
                        if (h < 0) break;
                        w.Hostile.Turn[h] = turn;
                        if (k < homing) { w.Hostile.Homing[h] = 2.4f; w.Hostile.HomingLeft[h] = rb.Param("orbHomingSeconds", 2.2f); w.Hostile.Turn[h] = 0; }
                    }
                    Fx(w, pos, FxStyle.Electric, 1.6f);
                    b.StepAt = w.Elapsed + 0.4f;
                    return true;
                }
                case BossAttack.Sobrecarga:
                    // as áreas foram marcadas no início da carga; aqui só o clarão
                    if (step == 0) { Fx(w, pos, FxStyle.Storm, rb.Param("overloadRadius", bravo, 7f)); b.StepAt = w.Elapsed + (bravo ? rb.Param("overloadRingDelay", 1f) : 0.1f); return true; }
                    return false;
                case BossAttack.Teleporte:
                {
                    if (step > 0) return false;
                    Fx(w, pos, FxStyle.Electric, 2f);
                    var t = target ?? AnyActive(w);
                    if (t != null)
                    {
                        float keep = rb.Param("keepMax", 9f) - 1f;
                        for (int attempt = 0; attempt < 10; attempt++)
                        {
                            float a = w.Rng.NextFloat() * (float)Math.PI * 2;
                            var p = t.Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * keep;
                            if (ArenaCollision.Blocked(p, e.Radius[i] + 0.2f, w.Arena)) continue;
                            e.Pos[i] = p;
                            break;
                        }
                    }
                    int sparks = bravo ? 10 : 6;
                    for (int k = 0; k < sparks; k++)
                        Shoot(w, e.Pos[i], (float)Math.PI * 2 * k / sparks, 4.5f, Dmg(w, rb, "orbDamage", 12) * 0.8f, 0.28f, 3f, HostileKind.Spark);
                    Fx(w, e.Pos[i], FxStyle.Electric, 2f);
                    b.StepAt = w.Elapsed + 0.2f;
                    return true;
                }
            }
            return false;
        }

        static void OverloadMarks(World w, BossRuntime b, int i, ResolvedBoss rb, float charge)
        {
            var pos = w.Enemies.Pos[i];
            float dmg = Dmg(w, rb, "overloadDamage", 40);
            AddHazard(w, HazardShape.Circle, FxStyle.Storm, pos, rb.Param("overloadRadius", b.Bravo, 7f), 0, 0, charge, dmg);
            // fúria (ou recorrência): segunda onda em anel, para quem fugiu para longe
            if (b.Bravo || b.Cycle > 0)
                AddHazard(w, HazardShape.Ring, FxStyle.Storm, pos, rb.Param("overloadRingOuter", 11.5f), rb.Param("overloadRingInner", 7.5f), 0,
                    charge + rb.Param("overloadRingDelay", 1f), dmg * 0.75f);
        }

        /// <summary>Cadeia: do boss ao alvo e, a cada salto, ao jogador ainda não atingido mais perto (ou conduz por um inimigo).</summary>
        static void Chain(World w, BossRuntime b, Vec2 from, ResolvedBoss rb, bool bravo)
        {
            int jumps = (int)rb.Param("chainJumps", bravo, 3) + (b.Cycle > 0 ? 1 : 0);
            float range = rb.Param("chainRange", 6.5f), falloff = rb.Param("chainFalloff", 0.75f);
            float dmg = Dmg(w, rb, "chainDamage", 18);
            var first = b.Target >= 0 ? w.Players[b.Target] : null;
            if (first == null || !first.IsActive) first = AnyActive(w);
            if (first == null) return;
            int hitMask = 0, enemyHops = 0;
            var prev = from;
            Vec2 cur = first.Pos;
            PlayerState hit = first;
            float maxFirst = rb.Param("keepMax", 9f) + 6f;
            if (Vec2.DistanceSq(from, first.Pos) > maxFirst * maxFirst) return;
            for (int j = 0; j <= jumps; j++)
            {
                w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.ChainArc, A = -1, B = j, X = prev.X, Y = prev.Y, X2 = cur.X, Y2 = cur.Y, Value = (float)FxStyle.Storm });
                if (hit != null)
                {
                    hitMask |= 1 << hit.Slot;
                    CombatSystem.DamagePlayer(w, hit.Slot, dmg);
                    dmg *= falloff;
                }
                prev = cur;
                // próximo: jogador não atingido no alcance; senão um inimigo comum conduz (até 2 vezes)
                PlayerState next = null;
                float best = range * range;
                foreach (var p in w.Players.Slots)
                {
                    if (p == null || !p.IsActive || (hitMask & (1 << p.Slot)) != 0) continue;
                    float dd = Vec2.DistanceSq(p.Pos, prev);
                    if (dd < best) { best = dd; next = p; }
                }
                if (next != null) { hit = next; cur = next.Pos; continue; }
                if (enemyHops >= 2) break;
                int en = NearestCommonEnemy(w, prev, range * 0.6f, j);
                if (en < 0) break;
                enemyHops++;
                hit = null;
                cur = w.Enemies.Pos[en];
            }
        }

        static int NearestCommonEnemy(World w, Vec2 at, float range, int salt)
        {
            var e = w.Enemies;
            int best = -1;
            float bd = range * range;
            for (int k = (salt * 7) % Math.Max(1, e.Count), n = 0; n < e.Count; n++, k = (k + 1) % e.Count)
            {
                if ((e.Flags[k] & EnemyFlags.Dead) != 0 || w.Db.Enemy(e.Type[k]).Behavior == EnemyBehavior.Boss) continue;
                float dd = Vec2.DistanceSq(e.Pos[k], at);
                if (dd > 0.25f && dd < bd) { bd = dd; best = k; }
            }
            return best;
        }

        // ---------------- áreas sinalizadas ----------------

        public static void AddHazard(World w, HazardShape shape, FxStyle style, Vec2 pos, float size, float extra, float width, float delay, float damage)
        {
            var d = w.Bosses;
            if (d.HazardCount >= BossDirector.MaxHazards) return;
            d.Hazards[d.HazardCount++] = new BossHazard
            {
                Shape = shape, Style = style, Pos = pos, Size = size, Extra = extra, Width = width, FireAt = w.Elapsed + delay, Damage = damage,
            };
            w.Events.AddRule(new RuleEvent
            {
                Kind = RuleEventKind.BossHazard, A = (int)shape | ((int)style << 4), B = (int)Math.Round(size * 20), C = (int)Math.Round(delay * 1000),
                X = pos.X, Y = pos.Y, Z = shape == HazardShape.Line ? extra : shape == HazardShape.Ring ? extra : 0, W = width,
            });
        }

        static void Hazards(World w)
        {
            var d = w.Bosses;
            for (int k = d.HazardCount - 1; k >= 0; k--)
            {
                var h = d.Hazards[k];
                if (w.Elapsed < h.FireAt) continue;
                foreach (var p in w.Players.Slots)
                    if (p != null && p.IsActive && Inside(h, p.Pos, p.Radius)) CombatSystem.DamagePlayer(w, p.Slot, h.Damage);
                float fx = h.Shape == HazardShape.Line ? h.Width * 2f : h.Size;
                var at = h.Shape == HazardShape.Line ? h.Pos + FromAngle(h.Extra) * (h.Size * 0.5f) : h.Pos;
                Fx(w, at, h.Style, fx);
                d.Hazards[k] = d.Hazards[--d.HazardCount];
            }
        }

        public static bool Inside(in BossHazard h, Vec2 p, float r)
        {
            var to = p - h.Pos;
            switch (h.Shape)
            {
                case HazardShape.Circle: return to.LengthSq() <= (h.Size + r) * (h.Size + r);
                case HazardShape.Ring:
                {
                    float d2 = to.LengthSq();
                    return d2 <= (h.Size + r) * (h.Size + r) && d2 >= Math.Max(0, h.Extra - r) * Math.Max(0, h.Extra - r);
                }
                default:
                {
                    var dir = FromAngle(h.Extra);
                    float along = to.X * dir.X + to.Y * dir.Y;
                    if (along < -r || along > h.Size + r) return false;
                    float across = Math.Abs(to.X * -dir.Y + to.Y * dir.X);
                    return across <= h.Width * 0.5f + r;
                }
            }
        }

        // ---------------- morte, vitória e Modo Infinito ----------------

        /// <summary>Chamado por <see cref="CombatSystem"/> quando um boss morre (antes de soltar recompensas).</summary>
        public static void OnKilled(World w, int i)
        {
            var e = w.Enemies;
            int boss = w.Db.BossOfEnemy(e.Type[i]);
            if (boss < 0) return;
            var d = w.Bosses;
            d.Active.RemoveAll(b => b.EnemyId == e.Id[i]);
            d.Defeated.Add(boss);
            bool final = w.Db.Boss(boss).Def.final && !d.Infinite && d.VictoryAt <= 0;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.BossDefeated, A = boss, B = e.Id[i], C = final ? 1 : 0, X = e.Pos[i].X, Y = e.Pos[i].Y });
            if (!final) return;
            d.VictoryAt = w.Elapsed + w.Rules.bossVictoryDelay;
            // fim da luta: ataques ativos encerrados com segurança
            ClearAttacks(w);
            w.SpawningEnabled = false;
        }

        public static void ClearAttacks(World w)
        {
            var h = w.Hostile;
            for (int k = h.Count - 1; k >= 0; k--) h.RemoveAt(k);
            w.Bosses.HazardCount = 0;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.BossHazard, A = 255 });
        }

        /// <summary>Host decide continuar após a vitória: a mesma partida segue, sem vitória por tempo.</summary>
        public static bool ContinueInfinite(World w)
        {
            var d = w.Bosses;
            if (w.Phase != MatchPhase.Victory || d.Infinite) return false;
            d.Infinite = true;
            d.InfiniteSince = w.Elapsed;
            d.NextInfiniteBossAt = w.Elapsed + w.Rules.infiniteFirstBoss;
            d.NextStormAt = w.Elapsed + w.Rules.infiniteStormEvery;
            d.ScheduleSent = false;
            d.VictoryAt = 0;
            w.SpawningEnabled = true;
            w.Phase = MatchPhase.Running;
            // quem caiu na luta final volta com metade da vida: a partida continua com o grupo
            foreach (var p in w.Players.Slots)
            {
                if (p == null || p.Status != PlayerStatus.Downed) continue;
                p.Status = PlayerStatus.Active;
                p.Hp = p.Stats.Get(StatId.MaxHp) * 0.5f;
                p.InvulnUntil = w.Elapsed + 3f;
            }
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.MatchContinued, X = w.Elapsed });
            return true;
        }

        public static float InfiniteMinutes(World w) => w.Bosses.Infinite ? Math.Max(0, (w.Elapsed - w.Bosses.InfiniteSince) / 60f) : 0f;
        public static float InfiniteHp(World w) => Math.Min(w.Rules.infiniteHpMax, 1f + w.Rules.infiniteHpPerMinute * InfiniteMinutes(w) * (1f + 0.04f * InfiniteMinutes(w)));
        public static float InfiniteDamage(World w) => Math.Min(w.Rules.infiniteDamageMax, 1f + w.Rules.infiniteDamagePerMinute * InfiniteMinutes(w));
        public static float InfiniteSpawn(World w) => Math.Min(w.Rules.infiniteSpawnMax, 1f + w.Rules.infiniteSpawnPerMinute * InfiniteMinutes(w));
        public static float InfiniteElite(World w) => Math.Min(w.Rules.infiniteEliteMax, w.Rules.infiniteElitePerMinute * InfiniteMinutes(w));

        /// <summary>Evento ambiental do Modo Infinito: tempestade de raios cada vez mais densa.</summary>
        static void InfiniteEvents(World w)
        {
            var d = w.Bosses;
            if (w.Elapsed < d.NextStormAt) return;
            d.NextStormAt = w.Elapsed + w.Rules.infiniteStormEvery;
            int marks = Math.Min(w.Rules.infiniteStormMarksMax, w.Rules.infiniteStormMarks + (int)(InfiniteMinutes(w) / 2f));
            float dmg = 18f * w.Balance.EnemyDamage * InfiniteDamage(w);
            for (int k = 0; k < marks; k++)
            {
                var p = NthActive(w, k);
                if (p == null) break;
                AddHazard(w, HazardShape.Circle, FxStyle.Storm, p.Pos + Jitter(w, k < TeamSize(w) ? 0.5f : 5f), 1.6f, 0, 0, 1.4f * w.Balance.Telegraph, dmg);
            }
        }

        // ---------------- utilitários ----------------

        const float Deg = (float)(Math.PI / 180.0);

        static int Find(World w, ushort id)
        {
            var e = w.Enemies;
            for (int k = 0; k < e.Count; k++) if (e.Id[k] == id && (e.Flags[k] & EnemyFlags.Dead) == 0) return k;
            return -1;
        }

        static PlayerState AnyActive(World w)
        {
            foreach (var p in w.Players.Slots) if (p != null && p.IsActive) return p;
            return null;
        }

        static PlayerState NthActive(World w, int n)
        {
            int active = w.Players.CountWith(PlayerStatus.Active);
            if (active == 0) return null;
            n %= active;
            foreach (var p in w.Players.Slots)
                if (p != null && p.IsActive && n-- == 0) return p;
            return null;
        }

        /// <summary>Quantidade extra de projéteis/marcas pela dificuldade (fácil −1, difícil +2).</summary>
        static int Extra(World w) => w.Balance.Difficulty == Difficulty.Hard ? 2 : w.Balance.Difficulty == Difficulty.Easy ? -1 : 0;

        static float Dmg(World w, ResolvedBoss rb, string key, float fallback) => rb.Param(key, fallback) * w.Balance.EnemyDamage * InfiniteDamage(w);

        static float Angle(Vec2 v) => (float)Math.Atan2(v.Y, v.X);
        static Vec2 FromAngle(float a) => new Vec2((float)Math.Cos(a), (float)Math.Sin(a));

        static Vec2 Jitter(World w, float r)
        {
            if (r <= 0) return Vec2.Zero;
            float a = w.Rng.NextFloat() * (float)Math.PI * 2, d = r * (0.4f + 0.6f * w.Rng.NextFloat());
            return new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * d;
        }

        static void Fx(World w, Vec2 at, FxStyle style, float radius) =>
            w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst, A = -1, B = (int)style, X = at.X, Y = at.Y, Value = radius });

        static int Shoot(World w, Vec2 from, float angle, float speed, float damage, float radius, float life, byte kind)
        {
            var dir = FromAngle(angle);
            return w.Hostile.Spawn(kind, from + dir * 0.9f, dir * speed, damage, radius, life);
        }

        static void Fan(World w, Vec2 from, float center, int n, float arc, float speed, float damage, float radius, float life, byte kind, float turn, bool pierce, int bounces)
        {
            for (int k = 0; k < n; k++)
            {
                float a = n == 1 ? center : center - arc * 0.5f + arc * k / (n - 1);
                int h = Shoot(w, from, a, speed, damage, radius, life, kind);
                if (h < 0) return;
                w.Hostile.Turn[h] = turn;
                w.Hostile.Pierce[h] = pierce;
                w.Hostile.Bounces[h] = (byte)bounces;
            }
        }
    }
}
