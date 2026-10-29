using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>Blocos de um snapshot; buffers reutilizados entre snapshots.</summary>
    public sealed class ChunkSet
    {
        public int Count;
        public byte[][] Buffers = new byte[8][];
        public int[] Lengths = new int[8];

        internal byte[] Get(int index, int size)
        {
            if (index >= Buffers.Length)
            {
                Array.Resize(ref Buffers, Buffers.Length * 2);
                Array.Resize(ref Lengths, Lengths.Length * 2);
            }
            if (Buffers[index] == null || Buffers[index].Length < size) Buffers[index] = new byte[size];
            return Buffers[index];
        }
    }

    public struct PlayerSnap
    {
        public bool Present;
        public PlayerStatus Status;
        public Vec2 Pos, Vel, Facing;
        public float Hp, MaxHp;
        public byte Flags;
        public ushort LastSeq;
        public float SpecialCooldown, SpecialActiveLeft;
        public int Level;
        public float XpProgress;
        public float MoveSpeed;
        public int Tick;

        public const byte FlagInvulnerable = 1, FlagSpecialActive = 2, FlagHasOffer = 4, FlagShield = 8;
        /// <summary>Aparência temporária (<see cref="PlayerLook"/>).</summary>
        public byte Look;
    }

    public struct EnemySnap
    {
        public Vec2 Pos;
        public byte Type;
        /// <summary>EnemyFlags.Elite / Stunned / HitFlash.</summary>
        public byte Flags;
        public float HpFraction;
        /// <summary><see cref="EnemyStatus"/> (preso, marcado, colado, lento).</summary>
        public byte Status;
        public int Tick;
    }

    /// <summary>Tiro, zona ou companheiro de jogador.</summary>
    public struct AllySnap
    {
        public ushort Id;
        public Vec2 Pos;
        public AllySprite Sprite;
        /// <summary>Tiro: ângulo (256 = volta); zona: raio × 16; companheiro: estado.</summary>
        public byte Param;
        public int Tick;
    }

    /// <summary>Estado de animação de um boss (a vida vem no registro do inimigo).</summary>
    public struct BossSnap
    {
        public BossState State;
        public BossAttack Attack;
        /// <summary>1 = BRAVO, 2 = invulnerável (entrada).</summary>
        public byte Flags;
        /// <summary>Direção de animação 0..7 (<see cref="BossSystem.Dir8"/>).</summary>
        public byte Dir;
        /// <summary>Fração da vida (a barra do boss; o registro do inimigo não leva vida).</summary>
        public float Hp;
        public int Tick;

        public const byte FlagBravo = 1, FlagInvulnerable = 2;
    }

    public struct HostileSnap
    {
        public Vec2 Pos;
        public byte Type;
        public int Tick;
    }

    /// <summary>Estado recebido pelo cliente, acumulado bloco a bloco.</summary>
    public sealed class SnapshotView
    {
        public int Tick = -1;
        public readonly PlayerSnap[] Players = new PlayerSnap[PlayerStore.MaxSlots];
        public readonly Dictionary<ushort, EnemySnap> Enemies = new Dictionary<ushort, EnemySnap>(512);
        public readonly Dictionary<ushort, HostileSnap> Hostile = new Dictionary<ushort, HostileSnap>(256);
        public readonly Dictionary<ushort, AllySnap> Allies = new Dictionary<ushort, AllySnap>(256);
        public readonly Dictionary<ushort, BossSnap> Bosses = new Dictionary<ushort, BossSnap>(4);
        public readonly List<CosmeticEvent> Cosmetic = new List<CosmeticEvent>(64);
    }

    /// <summary>
    /// Snapshot em blocos autocontidos: cabeçalho (8 B) + sequências
    /// [tipo, quantidade, registros]. Inimigo = 7 B (+1 se elite, +1 se tem estado); aliado = 8 B.
    /// </summary>
    public static class SnapshotCodec
    {
        const byte Magic = 0xE5;
        const byte Version = 4;
        const int HeaderSize = 8;
        const int RunHeaderSize = 2;

        const byte RunPlayers = 1, RunEnemies = 2, RunHostile = 3, RunCosmetic = 4, RunAllies = 5, RunBoss = 6;
        // Jogador: slot, status, x, y, vx, vy, fx, fy, hp, maxHp, flags, seq, recarga, ativo, nível, xp, velocidade.
        const int PlayerSize = 23, EnemySize = 7, HostileSize = 7, CosmeticSize = 20, AllySize = 8, BossSize = 8;
        const byte TypeMask = 0x0F, BitStatus = 1 << 4, BitElite = 1 << 5, BitStunned = 1 << 6, BitFlash = 1 << 7;
        /// <summary>Aliados por snapshot (os mais próximos dos jogadores primeiro).</summary>
        public const int MaxAllies = 200;

        static readonly List<AllySnap> AllyScratch = new List<AllySnap>(MaxAllies);

        // Estado do escritor (só o host escreve, numa thread).
        static ChunkSet _set;
        static byte[] _buf;
        static int _pos, _max, _runType, _runCountPos, _runCount, _tick;

        public static int Write(World w, List<CosmeticEvent> cosmetics, ChunkSet into, int maxChunkBytes = 1100)
        {
            _set = into;
            _max = maxChunkBytes;
            _tick = w.Tick;
            into.Count = 0;
            NewChunk();

            float aw = w.Arena.width, ah = w.Arena.height;

            foreach (var p in w.Players.Slots)
            {
                if (p == null) continue;
                Record(RunPlayers, PlayerSize);
                float max = p.Stats.Get(StatId.MaxHp);
                byte flags = 0;
                if (p.InvulnUntil > w.Elapsed) flags |= PlayerSnap.FlagInvulnerable;
                if (p.SpecialActiveLeft > 0) flags |= PlayerSnap.FlagSpecialActive;
                if (p.Progress.Current != null) flags |= PlayerSnap.FlagHasOffer;
                if (p.Shield > 0.5f) flags |= PlayerSnap.FlagShield;
                PutU8((byte)p.Slot);
                PutU8((byte)p.Status);
                PutU16(Quant.Pos(p.Pos.X, 0, aw));
                PutU16(Quant.Pos(p.Pos.Y, 0, ah));
                PutU8((byte)ClampSByte(p.Vel.X * 10f));
                PutU8((byte)ClampSByte(p.Vel.Y * 10f));
                PutU8((byte)Quant.Axis(p.Facing.X));
                PutU8((byte)Quant.Axis(p.Facing.Y));
                PutU16((ushort)Math.Min(ushort.MaxValue, Math.Max(0, Math.Round(p.Hp * 10f))));
                PutU16((ushort)Math.Min(ushort.MaxValue, Math.Round(max * 10f)));
                PutU8(flags);
                PutU16(p.LastInputSeq);
                PutU8(Tenths(p.Special?.CooldownLeft ?? 0));
                PutU8(Tenths(p.SpecialActiveLeft));
                PutU8((byte)Math.Min(255, p.Progress.Level));
                PutU8((byte)Math.Min(255, Math.Round(255f * p.Progress.Xp / Math.Max(1, XpCurve.ToNext(w.Rules, p.Progress.Level)))));
                PutU8((byte)Math.Min(255, Math.Round(p.Stats.Get(StatId.MoveSpeed) * 10f)));
                PutU8(EventSystem.Look(w, p));
            }

            foreach (var b in w.Bosses.Active)
            {
                Record(RunBoss, BossSize);
                PutU16(b.EnemyId);
                PutU8((byte)b.State);
                PutU8((byte)b.Attack);
                PutU8(BossFlags(w, b));
                PutU8(BossSystem.Dir8(b.Facing));
                PutU16(BossHp(w, b));
            }

            var h = w.Hostile;
            for (int i = 0; i < h.Count; i++)
            {
                Record(RunHostile, HostileSize);
                PutU16(h.Id[i]);
                PutU16(Quant.Pos(h.Pos[i].X, 0, aw));
                PutU16(Quant.Pos(h.Pos[i].Y, 0, ah));
                PutU8(h.Type[i]);
            }

            AllyList.Collect(w, AllyScratch, MaxAllies);
            foreach (var a in AllyScratch)
            {
                Record(RunAllies, AllySize);
                PutU16(a.Id);
                PutU16(Quant.Pos(a.Pos.X, 0, aw));
                PutU16(Quant.Pos(a.Pos.Y, 0, ah));
                PutU8((byte)a.Sprite);
                PutU8(a.Param);
            }

            var e = w.Enemies;
            for (int i = 0; i < e.Count; i++)
            {
                if ((e.Flags[i] & EnemyFlags.Dead) != 0) continue;
                bool elite = (e.Flags[i] & EnemyFlags.Elite) != 0;
                byte status = EnemyStatus.Bits(e, i, w.Elapsed);
                Record(RunEnemies, EnemySize + (elite ? 1 : 0) + (status != 0 ? 1 : 0));
                byte tf = (byte)(e.Type[i] & TypeMask);
                if (status != 0) tf |= BitStatus;
                if (elite) tf |= BitElite;
                if ((e.Flags[i] & EnemyFlags.Stunned) != 0) tf |= BitStunned;
                if ((e.Flags[i] & EnemyFlags.HitFlash) != 0) tf |= BitFlash;
                PutU16(e.Id[i]);
                PutU16(Quant.Pos(e.Pos[i].X, 0, aw));
                PutU16(Quant.Pos(e.Pos[i].Y, 0, ah));
                PutU8(tf);
                if (elite) PutU8((byte)Math.Round(255f * Math.Max(0, e.Hp[i]) / Math.Max(1e-3f, e.MaxHp[i])));
                if (status != 0) PutU8(status);
            }

            if (cosmetics != null)
            {
                foreach (var c in cosmetics)
                {
                    Record(RunCosmetic, CosmeticSize);
                    PutU8((byte)c.Kind);
                    PutU16((ushort)Math.Max(0, Math.Min(ushort.MaxValue, c.A)));
                    PutU8((byte)(sbyte)Math.Max(-128, Math.Min(127, c.B)));
                    PutU16(Quant.Pos(c.X, 0, aw));
                    PutU16(Quant.Pos(c.Y, 0, ah));
                    PutF32(c.X2);
                    PutF32(c.Y2);
                    PutF32(c.Value);
                }
            }

            CloseChunk();
            int total = 0;
            for (int k = 0; k < into.Count; k++)
            {
                into.Buffers[k][7] = (byte)into.Count;
                total += into.Lengths[k];
            }
            _set = null;
            _buf = null;
            return total;
        }

        static void NewChunk()
        {
            int idx = _set.Count++;
            _buf = _set.Get(idx, _max);
            _pos = 0;
            PutU8(Magic);
            PutU8(Version);
            PutU32((uint)_tick);
            PutU8((byte)idx);
            PutU8(0); // quantidade de blocos, corrigida no fim
            _runType = 0;
            _runCount = 0;
        }

        static void CloseChunk() => _set.Lengths[_set.Count - 1] = _pos;

        /// <summary>Garante espaço para um registro, abrindo sequência ou bloco novo.</summary>
        static void Record(byte type, int size)
        {
            bool sameRun = _runType == type && _runCount < 255;
            int needed = size + (sameRun ? 0 : RunHeaderSize);
            if (_pos + needed > _max)
            {
                CloseChunk();
                NewChunk();
                sameRun = false;
            }
            if (!sameRun)
            {
                PutU8(type);
                _runCountPos = _pos;
                PutU8(0);
                _runType = type;
                _runCount = 0;
            }
            _runCount++;
            _buf[_runCountPos] = (byte)_runCount;
        }

        /// <summary>Vida do boss em 0..65535 (fração da vida máxima).</summary>
        public static ushort BossHp(World w, BossRuntime b)
        {
            var e = w.Enemies;
            for (int i = 0; i < e.Count; i++)
                if (e.Id[i] == b.EnemyId) return (ushort)Math.Round(65535f * Math.Max(0f, Math.Min(1f, e.Hp[i] / Math.Max(1e-3f, e.MaxHp[i]))));
            return 0;
        }

        public static byte BossFlags(World w, BossRuntime b)
        {
            byte f = b.Bravo ? BossSnap.FlagBravo : (byte)0;
            if (b.State == BossState.Entering) f |= BossSnap.FlagInvulnerable;
            return f;
        }

        static void PutU8(byte v) => _buf[_pos++] = v;
        static void PutU16(ushort v) { _buf[_pos++] = (byte)v; _buf[_pos++] = (byte)(v >> 8); }
        static void PutU32(uint v) { for (int i = 0; i < 4; i++) _buf[_pos++] = (byte)(v >> (8 * i)); }
        static void PutF32(float v) => PutU32((uint)BitConverter.SingleToInt32Bits(v));
        static sbyte ClampSByte(float v) => (sbyte)Math.Max(-127, Math.Min(127, Math.Round(v)));
        static byte Tenths(float seconds) => (byte)Math.Max(0, Math.Min(255, Math.Ceiling(seconds * 10f)));

        /// <summary>Lê um bloco e aplica ao <paramref name="into"/>. Nunca lança; false se malformado.</summary>
        public static bool TryRead(byte[] chunk, int length, float arenaW, float arenaH, SnapshotView into)
        {
            if (chunk == null || length < HeaderSize || length > chunk.Length) return false;
            if (!Validate(chunk, length, out int tick)) return false;
            if (tick < into.Tick) return true; // bloco atrasado: posições velhas, ignorado

            into.Tick = tick;
            var r = new ByteReader(chunk, length) { Position = HeaderSize };
            while (r.Remaining > 0)
            {
                r.TryU8(out byte type);
                r.TryU8(out byte count);
                for (int k = 0; k < count; k++)
                {
                    switch (type)
                    {
                        case RunPlayers: ReadPlayer(ref r, arenaW, arenaH, tick, into); break;
                        case RunEnemies: ReadEnemy(ref r, arenaW, arenaH, tick, into); break;
                        case RunHostile: ReadHostile(ref r, arenaW, arenaH, tick, into); break;
                        case RunCosmetic: ReadCosmetic(ref r, arenaW, arenaH, into); break;
                        case RunAllies: ReadAlly(ref r, arenaW, arenaH, tick, into); break;
                        case RunBoss:
                            r.TryU16(out ushort id); r.TryU8(out byte st); r.TryU8(out byte at); r.TryU8(out byte fl); r.TryU8(out byte dir); r.TryU16(out ushort bhp);
                            into.Bosses[id] = new BossSnap { State = (BossState)st, Attack = (BossAttack)at, Flags = fl, Dir = dir, Hp = bhp / 65535f, Tick = tick };
                            break;
                    }
                }
            }
            return true;
        }

        /// <summary>Primeira passada: estrutura completa e consistente, sem aplicar nada.</summary>
        static bool Validate(byte[] chunk, int length, out int tick)
        {
            tick = 0;
            var r = new ByteReader(chunk, length);
            r.TryU8(out byte magic);
            r.TryU8(out byte version);
            r.TryU32(out uint t);
            r.TryU8(out byte index);
            r.TryU8(out byte count);
            if (magic != Magic || version != Version || count == 0 || index >= count || t > int.MaxValue) return false;
            tick = (int)t;

            while (r.Remaining > 0)
            {
                if (!r.TryU8(out byte type) || !r.TryU8(out byte n) || n == 0) return false;
                for (int k = 0; k < n; k++)
                {
                    int size;
                    switch (type)
                    {
                        case RunPlayers:
                            if (r.Remaining < PlayerSize) return false;
                            if (chunk[r.Position] >= PlayerStore.MaxSlots || chunk[r.Position + 1] > (byte)PlayerStatus.Inactive) return false;
                            size = PlayerSize;
                            break;
                        case RunEnemies:
                            if (r.Remaining < EnemySize) return false;
                            size = EnemySize + ((chunk[r.Position + 6] & BitElite) != 0 ? 1 : 0) + ((chunk[r.Position + 6] & BitStatus) != 0 ? 1 : 0);
                            break;
                        case RunAllies:
                            if (r.Remaining < AllySize) return false;
                            size = AllySize;
                            break;
                        case RunHostile:
                            size = HostileSize;
                            break;
                        case RunBoss:
                            if (r.Remaining < BossSize || chunk[r.Position + 2] > (byte)BossState.Rage || chunk[r.Position + 3] > (byte)BossAttack.Teleporte || chunk[r.Position + 5] > 7) return false;
                            size = BossSize;
                            break;
                        case RunCosmetic:
                            if (r.Remaining < CosmeticSize || chunk[r.Position] > (byte)CosmeticKind.Swing) return false;
                            for (int f = 0; f < 3; f++)
                            {
                                float v = BitConverter.Int32BitsToSingle(BitConverter.ToInt32(chunk, r.Position + 8 + f * 4));
                                if (float.IsNaN(v) || float.IsInfinity(v)) return false;
                            }
                            size = CosmeticSize;
                            break;
                        default:
                            return false;
                    }
                    if (r.Remaining < size) return false;
                    r.Position += size;
                }
            }
            return true;
        }

        static void ReadPlayer(ref ByteReader r, float aw, float ah, int tick, SnapshotView into)
        {
            r.TryU8(out byte slot);
            r.TryU8(out byte status);
            r.TryU16(out ushort x); r.TryU16(out ushort y);
            r.TryI8(out sbyte vx); r.TryI8(out sbyte vy);
            r.TryI8(out sbyte fx); r.TryI8(out sbyte fy);
            r.TryU16(out ushort hp); r.TryU16(out ushort maxHp);
            r.TryU8(out byte flags);
            r.TryU16(out ushort seq);
            r.TryU8(out byte cd); r.TryU8(out byte active);
            r.TryU8(out byte level); r.TryU8(out byte xp); r.TryU8(out byte speed); r.TryU8(out byte look);
            into.Players[slot] = new PlayerSnap
            {
                Present = true,
                Status = (PlayerStatus)status,
                Pos = new Vec2(Quant.UnPos(x, 0, aw), Quant.UnPos(y, 0, ah)),
                Vel = new Vec2(vx / 10f, vy / 10f),
                Facing = new Vec2(Quant.UnAxis(fx), Quant.UnAxis(fy)),
                Hp = hp / 10f,
                MaxHp = maxHp / 10f,
                Flags = flags,
                LastSeq = seq,
                SpecialCooldown = cd / 10f,
                SpecialActiveLeft = active / 10f,
                Level = level,
                XpProgress = xp / 255f,
                MoveSpeed = speed / 10f,
                Look = look,
                Tick = tick,
            };
        }

        static void ReadEnemy(ref ByteReader r, float aw, float ah, int tick, SnapshotView into)
        {
            r.TryU16(out ushort id);
            r.TryU16(out ushort x); r.TryU16(out ushort y);
            r.TryU8(out byte tf);
            float hp = 1f;
            byte status = 0;
            if ((tf & BitElite) != 0) { r.TryU8(out byte h); hp = h / 255f; }
            if ((tf & BitStatus) != 0) r.TryU8(out status);
            byte flags = 0;
            if ((tf & BitElite) != 0) flags |= EnemyFlags.Elite;
            if ((tf & BitStunned) != 0) flags |= EnemyFlags.Stunned;
            if ((tf & BitFlash) != 0) flags |= EnemyFlags.HitFlash;
            into.Enemies[id] = new EnemySnap
            {
                Pos = new Vec2(Quant.UnPos(x, 0, aw), Quant.UnPos(y, 0, ah)),
                Type = (byte)(tf & TypeMask), Flags = flags, HpFraction = hp, Status = status, Tick = tick,
            };
        }

        static void ReadAlly(ref ByteReader r, float aw, float ah, int tick, SnapshotView into)
        {
            r.TryU16(out ushort id);
            r.TryU16(out ushort x); r.TryU16(out ushort y);
            r.TryU8(out byte sprite);
            r.TryU8(out byte param);
            into.Allies[id] = new AllySnap
            {
                Id = id, Pos = new Vec2(Quant.UnPos(x, 0, aw), Quant.UnPos(y, 0, ah)), Sprite = (AllySprite)sprite, Param = param, Tick = tick,
            };
        }

        static void ReadHostile(ref ByteReader r, float aw, float ah, int tick, SnapshotView into)
        {
            r.TryU16(out ushort id);
            r.TryU16(out ushort x); r.TryU16(out ushort y);
            r.TryU8(out byte type);
            into.Hostile[id] = new HostileSnap { Pos = new Vec2(Quant.UnPos(x, 0, aw), Quant.UnPos(y, 0, ah)), Type = type, Tick = tick };
        }

        static void ReadCosmetic(ref ByteReader r, float aw, float ah, SnapshotView into)
        {
            r.TryU8(out byte kind);
            r.TryU16(out ushort a);
            r.TryI8(out sbyte b);
            r.TryU16(out ushort x); r.TryU16(out ushort y);
            r.TryF32(out float x2); r.TryF32(out float y2); r.TryF32(out float value);
            into.Cosmetic.Add(new CosmeticEvent
            {
                Kind = (CosmeticKind)kind, A = a, B = b,
                X = Quant.UnPos(x, 0, aw), Y = Quant.UnPos(y, 0, ah), X2 = x2, Y2 = y2, Value = value,
            });
        }
    }
}
