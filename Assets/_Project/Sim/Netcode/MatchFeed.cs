using System.Collections.Generic;

namespace ES.Sim
{
    public struct Track
    {
        public Vec2 Prev, Cur;
        public int PrevTick, CurTick;
        public byte Type, Flags, Status;
        public float Hp;

        public Vec2 At(float renderTick)
        {
            if (CurTick <= PrevTick || renderTick >= CurTick) return Cur;
            if (renderTick <= PrevTick) return Prev;
            float t = (renderTick - PrevTick) / (CurTick - PrevTick);
            return Prev + (Cur - Prev) * t;
        }
    }

    public struct EnemyRender { public ushort Id; public Vec2 Pos; public byte Type, Flags, Status; public float Hp; }
    public struct AllyRender { public ushort Id; public Vec2 Pos; public AllySprite Sprite; public byte Param; }
    public struct HostileRender { public ushort Id; public Vec2 Pos; public byte Type; }

    /// <summary>O que a apresentação desenha neste quadro.</summary>
    public sealed class RenderFrame
    {
        public readonly List<EnemyRender> Enemies = new List<EnemyRender>(512);
        public readonly List<HostileRender> Hostile = new List<HostileRender>(256);
        public readonly List<AllyRender> Allies = new List<AllyRender>(256);
        public readonly Vec2[] PlayerPositions = new Vec2[PlayerStore.MaxSlots];
    }

    /// <summary>Posições anterior/atual por entidade, para desenhar entre ticks.</summary>
    public sealed class RenderTracks
    {
        readonly Dictionary<ushort, Track> _enemies = new Dictionary<ushort, Track>(512);
        readonly Dictionary<ushort, Track> _hostile = new Dictionary<ushort, Track>(256);
        readonly Dictionary<ushort, Track> _allies = new Dictionary<ushort, Track>(256);
        readonly Track[] _players = new Track[PlayerStore.MaxSlots];
        readonly List<ushort> _stale = new List<ushort>(64);
        // inimigos mortos -> tick da morte: snapshots atrasados não os ressuscitam
        readonly Dictionary<ushort, int> _deadAt = new Dictionary<ushort, int>(256);
        const int DeadMemoryTicks = 300;

        public int EnemyCount => _enemies.Count;
        public int AllyCount => _allies.Count;
        public bool TryGetEnemy(ushort id, out Track t) => _enemies.TryGetValue(id, out t);

        static void Push(ref Track t, Vec2 pos, int tick, bool isNew)
        {
            if (isNew) { t.Prev = t.Cur = pos; t.PrevTick = t.CurTick = tick; return; }
            if (tick <= t.CurTick) { t.Cur = pos; return; }
            t.Prev = t.Cur;
            t.PrevTick = t.CurTick;
            t.Cur = pos;
            t.CurTick = tick;
        }

        public void SetEnemy(ushort id, Vec2 pos, byte type, byte flags, float hp, int tick, byte status = 0)
        {
            if (_deadAt.TryGetValue(id, out int died))
            {
                if (tick <= died) return;
                if (tick - died < DeadMemoryTicks) return; // ID só é reutilizado bem depois
                _deadAt.Remove(id);
            }
            bool isNew = !_enemies.TryGetValue(id, out var t);
            Push(ref t, pos, tick, isNew);
            t.Type = type; t.Flags = flags; t.Hp = hp; t.Status = status;
            _enemies[id] = t;
        }

        public void RemoveEnemy(ushort id, int tick = int.MaxValue)
        {
            _enemies.Remove(id);
            if (tick != int.MaxValue) _deadAt[id] = tick;
        }

        public void SetHostile(ushort id, Vec2 pos, byte type, int tick)
        {
            bool isNew = !_hostile.TryGetValue(id, out var t);
            Push(ref t, pos, tick, isNew);
            t.Type = type;
            _hostile[id] = t;
        }

        /// <summary>Projéteis hostis não têm evento de remoção: somem quando param de chegar.</summary>
        public void PruneHostile(int minTick) => Prune(_hostile, minTick);

        /// <summary>Tiros, zonas e companheiros: um id novo com outro sprite recomeça a trajetória.</summary>
        public void SetAlly(ushort id, Vec2 pos, AllySprite sprite, byte param, int tick)
        {
            bool isNew = !_allies.TryGetValue(id, out var t) || t.Type != (byte)sprite;
            Push(ref t, pos, tick, isNew);
            t.Type = (byte)sprite;
            t.Flags = param;
            _allies[id] = t;
        }

        public void PruneAllies(int minTick) => Prune(_allies, minTick);

        void Prune(Dictionary<ushort, Track> d, int minTick)
        {
            _stale.Clear();
            foreach (var kv in d) if (kv.Value.CurTick < minTick) _stale.Add(kv.Key);
            foreach (var id in _stale) d.Remove(id);
        }

        public void SetPlayer(int slot, Vec2 pos, int tick, bool isNew = false) => Push(ref _players[slot], pos, tick, isNew || _players[slot].CurTick == 0);

        public void Clear()
        {
            _enemies.Clear();
            _deadAt.Clear();
            _hostile.Clear();
            _allies.Clear();
            for (int i = 0; i < _players.Length; i++) _players[i] = default;
        }

        public void Fill(float renderTick, RenderFrame frame)
        {
            frame.Enemies.Clear();
            foreach (var kv in _enemies)
                frame.Enemies.Add(new EnemyRender { Id = kv.Key, Pos = kv.Value.At(renderTick), Type = kv.Value.Type, Flags = kv.Value.Flags, Status = kv.Value.Status, Hp = kv.Value.Hp });
            frame.Hostile.Clear();
            foreach (var kv in _hostile)
                frame.Hostile.Add(new HostileRender { Id = kv.Key, Pos = kv.Value.At(renderTick), Type = kv.Value.Type });
            frame.Allies.Clear();
            foreach (var kv in _allies)
                frame.Allies.Add(new AllyRender { Id = kv.Key, Pos = kv.Value.At(renderTick), Sprite = (AllySprite)kv.Value.Type, Param = kv.Value.Flags });
            for (int i = 0; i < _players.Length; i++) frame.PlayerPositions[i] = _players[i].At(renderTick);
        }
    }

    public struct GemInfo
    {
        public Vec2 Pos;
        public PickupKind Kind;
        public float Value;
    }

    /// <summary>
    /// Estado que a apresentação consome, igual no host (lido do World) e no cliente
    /// (montado a partir de snapshots e eventos).
    /// </summary>
    public sealed class MatchFeed
    {
        public readonly RenderTracks Tracks = new RenderTracks();
        public readonly PlayerSnap[] Players = new PlayerSnap[PlayerStore.MaxSlots];
        public readonly Dictionary<ushort, GemInfo> Gems = new Dictionary<ushort, GemInfo>(256);
        /// <summary>Eventos ainda não consumidos pela apresentação (ela limpa a lista).</summary>
        public readonly List<DecodedRuleEvent> RuleEvents = new List<DecodedRuleEvent>(64);
        public readonly List<CosmeticEvent> Cosmetics = new List<CosmeticEvent>(128);

        public int LocalSlot;
        public int Tick;
        public float Elapsed;
        public float RenderTick;
        public MatchPhase Phase = MatchPhase.Running;
        public bool HostBackgrounded;
        /// <summary>Oferta de nível aberta para o jogador local.</summary>
        public LevelUpOffer CurrentOffer;
        /// <summary>Habilidades de cada jogador (criado no início da partida).</summary>
        public LoadoutTracker Loadouts;
        /// <summary>Resultado enviado pelo host no fim da partida.</summary>
        public MatchSummary Summary;
        /// <summary>Abates da equipe contados pelos eventos EnemyDied.</summary>
        public int TeamKills;

        /// <summary>Bosses vivos por id de inimigo (estado de animação do último snapshot).</summary>
        public readonly Dictionary<ushort, BossInfo> Bosses = new Dictionary<ushort, BossInfo>(4);
        /// <summary>Áreas sinalizadas ainda por explodir (a apresentação remove as vencidas).</summary>
        public readonly List<HazardInfo> Hazards = new List<HazardInfo>(64);
        /// <summary>Próximo boss da contagem: −1 nenhum, 255 dueto.</summary>
        public int NextBoss = -1;
        public float NextBossAt;
        public bool Infinite;
        public int BossesDefeated;
        /// <summary>Eventos aleatórios em andamento (por id) e trajetórias de moto.</summary>
        public readonly Dictionary<int, EventInfo> Events = new Dictionary<int, EventInfo>(8);
        public readonly List<MotoInfo> Motos = new List<MotoInfo>(4);

        public void SetBoss(ushort enemyId, in BossSnap s)
        {
            if (!Bosses.TryGetValue(enemyId, out var b)) return;
            b.State = s.State;
            b.Attack = s.Attack;
            b.Bravo |= (s.Flags & BossSnap.FlagBravo) != 0;
            b.Invulnerable = (s.Flags & BossSnap.FlagInvulnerable) != 0;
            b.Dir = s.Dir;
        }

        public void ApplyRuleEvent(in DecodedRuleEvent d)
        {
            var e = d.Event;
            switch (e.Kind)
            {
                case RuleEventKind.EnemyDied: Tracks.RemoveEnemy((ushort)e.A, e.C); TeamKills++; break;
                case RuleEventKind.GemSpawned:
                    Gems[(ushort)e.A] = new GemInfo { Pos = new Vec2(e.X, e.Y), Kind = (PickupKind)e.B, Value = e.C };
                    break;
                case RuleEventKind.GemCollected: Gems.Remove((ushort)e.A); break;
                case RuleEventKind.LevelUpOffer:
                    if (e.A == LocalSlot && d.Options != null && d.Options.Length > 0)
                        CurrentOffer = new LevelUpOffer { OfferId = (short)e.B, Options = d.Options, RerollsLeft = d.Rerolls, BansLeft = d.Bans, SkipsLeft = d.Skips };
                    break;
                case RuleEventKind.ChoiceApplied:
                    if (e.A == LocalSlot) CurrentOffer = null;
                    break;
                case RuleEventKind.MatchEnded: Phase = (MatchPhase)e.A; break;
                case RuleEventKind.BossSpawned:
                    Bosses[(ushort)e.B] = new BossInfo { Boss = e.A, EnemyId = (ushort)e.B, Cycle = e.C, State = BossState.Entering, Invulnerable = true, SpawnedAt = Elapsed };
                    break;
                case RuleEventKind.BossBravo:
                    if (Bosses.TryGetValue((ushort)e.B, out var bb)) { bb.Bravo = true; bb.State = BossState.Rage; bb.BravoAt = Elapsed; }
                    break;
                case RuleEventKind.BossAction:
                    if (Bosses.TryGetValue((ushort)e.A, out var ba)) { ba.Attack = (BossAttack)e.B; ba.State = (BossState)e.C; ba.ActionAt = Elapsed; }
                    break;
                case RuleEventKind.BossDefeated:
                    Bosses.Remove((ushort)e.B);
                    BossesDefeated++;
                    break;
                case RuleEventKind.BossSchedule:
                    NextBoss = e.A == 254 ? -1 : e.A;
                    NextBossAt = e.X;
                    break;
                case RuleEventKind.BossHazard:
                    if (e.A == 255) { Hazards.Clear(); break; }
                    Hazards.Add(new HazardInfo
                    {
                        Shape = (HazardShape)(e.A & 15), Style = (FxStyle)(e.A >> 4), Pos = new Vec2(e.X, e.Y), Size = e.B / 20f,
                        Extra = e.Z, Width = e.W, CreatedAt = Elapsed, FireAt = Elapsed + e.C / 1000f,
                    });
                    break;
                case RuleEventKind.EventStarted:
                    Events[e.C] = new EventInfo
                    {
                        Id = e.C, Event = e.A, Target = e.B == 255 ? -1 : e.B, StartedAt = Elapsed, StartsAt = Elapsed + e.Y, EndsAt = Elapsed + e.X,
                    };
                    break;
                case RuleEventKind.EventProgress:
                    if (Events.TryGetValue(e.A, out var ep)) { ep.Progress = e.B; ep.Goal = e.C; }
                    break;
                case RuleEventKind.EventEnded:
                    Events.Remove(e.A);
                    break;
                case RuleEventKind.EventMoto:
                    Motos.Add(new MotoInfo { Id = e.A, Start = new Vec2(e.X, e.Y), Angle = e.Z, Speed = e.W, Length = e.B / 10f, WarnAt = Elapsed, GoAt = Elapsed + e.C / 1000f });
                    break;
                case RuleEventKind.MatchContinued:
                    Phase = MatchPhase.Running;
                    Infinite = true;
                    Summary = null;
                    break;
            }
            Loadouts?.Apply(e);
            RuleEvents.Add(d);
        }
    }

    public sealed class BossInfo
    {
        public int Boss, Cycle;
        public ushort EnemyId;
        public BossState State;
        public BossAttack Attack;
        public bool Bravo, Invulnerable;
        public byte Dir;
        public float SpawnedAt, BravoAt = -1, ActionAt = -1;
    }

    public struct HazardInfo
    {
        public HazardShape Shape;
        public FxStyle Style;
        public Vec2 Pos;
        public float Size, Extra, Width, CreatedAt, FireAt;
    }

    public sealed class EventInfo
    {
        public int Id, Event, Target, Progress, Goal;
        public float StartedAt, StartsAt, EndsAt;
    }

    public struct MotoInfo
    {
        public int Id;
        public Vec2 Start;
        public float Angle, Speed, Length, WarnAt, GoAt;
    }
}
