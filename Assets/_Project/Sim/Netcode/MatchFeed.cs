using System.Collections.Generic;

namespace ES.Sim
{
    public struct Track
    {
        public Vec2 Prev, Cur;
        public int PrevTick, CurTick;
        public byte Type, Flags;
        public float Hp;

        public Vec2 At(float renderTick)
        {
            if (CurTick <= PrevTick || renderTick >= CurTick) return Cur;
            if (renderTick <= PrevTick) return Prev;
            float t = (renderTick - PrevTick) / (CurTick - PrevTick);
            return Prev + (Cur - Prev) * t;
        }
    }

    public struct EnemyRender { public ushort Id; public Vec2 Pos; public byte Type, Flags; public float Hp; }
    public struct HostileRender { public ushort Id; public Vec2 Pos; public byte Type; }

    /// <summary>O que a apresentação desenha neste quadro.</summary>
    public sealed class RenderFrame
    {
        public readonly List<EnemyRender> Enemies = new List<EnemyRender>(512);
        public readonly List<HostileRender> Hostile = new List<HostileRender>(256);
        public readonly Vec2[] PlayerPositions = new Vec2[PlayerStore.MaxSlots];
    }

    /// <summary>Posições anterior/atual por entidade, para desenhar entre ticks.</summary>
    public sealed class RenderTracks
    {
        readonly Dictionary<ushort, Track> _enemies = new Dictionary<ushort, Track>(512);
        readonly Dictionary<ushort, Track> _hostile = new Dictionary<ushort, Track>(256);
        readonly Track[] _players = new Track[PlayerStore.MaxSlots];
        readonly List<ushort> _stale = new List<ushort>(64);
        // inimigos mortos -> tick da morte: snapshots atrasados não os ressuscitam
        readonly Dictionary<ushort, int> _deadAt = new Dictionary<ushort, int>(256);
        const int DeadMemoryTicks = 300;

        public int EnemyCount => _enemies.Count;
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

        public void SetEnemy(ushort id, Vec2 pos, byte type, byte flags, float hp, int tick)
        {
            if (_deadAt.TryGetValue(id, out int died))
            {
                if (tick <= died) return;
                if (tick - died < DeadMemoryTicks) return; // ID só é reutilizado bem depois
                _deadAt.Remove(id);
            }
            bool isNew = !_enemies.TryGetValue(id, out var t);
            Push(ref t, pos, tick, isNew);
            t.Type = type; t.Flags = flags; t.Hp = hp;
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
        public void PruneHostile(int minTick)
        {
            _stale.Clear();
            foreach (var kv in _hostile) if (kv.Value.CurTick < minTick) _stale.Add(kv.Key);
            foreach (var id in _stale) _hostile.Remove(id);
        }

        public void SetPlayer(int slot, Vec2 pos, int tick, bool isNew = false) => Push(ref _players[slot], pos, tick, isNew || _players[slot].CurTick == 0);

        public void Clear()
        {
            _enemies.Clear();
            _deadAt.Clear();
            _hostile.Clear();
            for (int i = 0; i < _players.Length; i++) _players[i] = default;
        }

        public void Fill(float renderTick, RenderFrame frame)
        {
            frame.Enemies.Clear();
            foreach (var kv in _enemies)
                frame.Enemies.Add(new EnemyRender { Id = kv.Key, Pos = kv.Value.At(renderTick), Type = kv.Value.Type, Flags = kv.Value.Flags, Hp = kv.Value.Hp });
            frame.Hostile.Clear();
            foreach (var kv in _hostile)
                frame.Hostile.Add(new HostileRender { Id = kv.Key, Pos = kv.Value.At(renderTick), Type = kv.Value.Type });
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

        public void ApplyRuleEvent(in DecodedRuleEvent d)
        {
            var e = d.Event;
            switch (e.Kind)
            {
                case RuleEventKind.EnemyDied: Tracks.RemoveEnemy((ushort)e.A, e.C); break;
                case RuleEventKind.GemSpawned:
                    Gems[(ushort)e.A] = new GemInfo { Pos = new Vec2(e.X, e.Y), Kind = (PickupKind)e.B, Value = e.C };
                    break;
                case RuleEventKind.GemCollected: Gems.Remove((ushort)e.A); break;
                case RuleEventKind.LevelUpOffer:
                    if (e.A == LocalSlot && d.Options != null && d.Options.Length > 0)
                        CurrentOffer = new LevelUpOffer { OfferId = (short)e.B, Options = d.Options };
                    break;
                case RuleEventKind.ChoiceApplied:
                    if (e.A == LocalSlot) CurrentOffer = null;
                    break;
                case RuleEventKind.MatchEnded: Phase = (MatchPhase)e.A; break;
            }
            RuleEvents.Add(d);
        }
    }
}
