using System;
using System.Collections.Generic;

namespace ES.Sim
{
    public struct DecodedRuleEvent
    {
        public RuleEvent Event;
        /// <summary>Só para LevelUpOffer; vazio se a oferta já foi resolvida no mesmo tick.</summary>
        public OfferOption[] Options;
        /// <summary>Só para LevelUpOffer: atualizações, banimentos e pulos restantes.</summary>
        public byte Rerolls, Bans, Skips;
    }

    /// <summary>Eventos de regra em formato compacto por tipo (entrega confiável).</summary>
    public static class RuleEventCodec
    {
        static readonly OfferOption[] NoOptions = new OfferOption[0];

        public static void Write(ByteWriter w, List<RuleEvent> events, World world)
        {
            float aw = world.Arena.width, ah = world.Arena.height;
            w.U16((ushort)Math.Min(ushort.MaxValue, events.Count));
            for (int i = 0; i < events.Count && i < ushort.MaxValue; i++)
            {
                var e = events[i];
                w.U8((byte)e.Kind);
                switch (e.Kind)
                {
                    case RuleEventKind.EnemyDied:
                        w.U16((ushort)e.A); w.U8((byte)e.B); w.U32((uint)Math.Max(0, e.C)); Pos(w, e, aw, ah);
                        break;
                    case RuleEventKind.GemSpawned:
                        w.U16((ushort)e.A); w.U8((byte)e.B); w.U16((ushort)Math.Min(ushort.MaxValue, e.C)); Pos(w, e, aw, ah);
                        break;
                    case RuleEventKind.GemCollected:
                        w.U16((ushort)e.A); w.I8((sbyte)e.B);
                        break;
                    case RuleEventKind.LevelUpOffer:
                    {
                        w.U8((byte)e.A); w.I16((short)e.B); w.U8((byte)Math.Min(255, e.C));
                        var cur = world.Players[e.A]?.Progress.Current;
                        var opts = cur != null && cur.OfferId == e.B ? cur.Options : NoOptions;
                        w.U8((byte)opts.Length);
                        foreach (var o in opts) { w.U8((byte)o.Kind); w.U16((ushort)o.Ref); }
                        w.U8(cur?.RerollsLeft ?? 0); w.U8(cur?.BansLeft ?? 0); w.U8(cur?.SkipsLeft ?? 0);
                        break;
                    }
                    case RuleEventKind.ChoiceApplied:
                        w.U8((byte)e.A); w.U8((byte)e.B); w.U16((ushort)e.C);
                        break;
                    case RuleEventKind.SynergyChanged:
                        w.U8((byte)e.A); w.U16((ushort)e.B); w.U8((byte)e.C);
                        break;
                    case RuleEventKind.EvolutionApplied:
                        w.U8((byte)e.A); w.U16((ushort)e.B); w.U16((ushort)e.C);
                        break;
                    case RuleEventKind.SpecialActivated:
                        w.U8((byte)e.A); w.U16((ushort)e.B); Pos(w, e, aw, ah);
                        break;
                    case RuleEventKind.PlayerDowned:
                        w.U8((byte)e.A); Pos(w, e, aw, ah);
                        break;
                    case RuleEventKind.PlayerInactive:
                        w.U8((byte)e.A);
                        break;
                    case RuleEventKind.MatchEnded:
                        w.U8((byte)e.A); w.F32(e.X);
                        break;
                }
            }
        }

        static void Pos(ByteWriter w, RuleEvent e, float aw, float ah)
        {
            w.U16(Quant.Pos(e.X, 0, aw));
            w.U16(Quant.Pos(e.Y, 0, ah));
        }

        /// <summary>Nunca lança. Em dados malformados retorna false e não adiciona nada.</summary>
        public static bool TryRead(byte[] data, int length, float arenaW, float arenaH, List<DecodedRuleEvent> into)
        {
            var r = new ByteReader(data, length);
            if (!r.TryU16(out ushort count)) return false;
            var parsed = new List<DecodedRuleEvent>(count);
            for (int i = 0; i < count; i++)
            {
                if (!r.TryU8(out byte kind) || kind > (byte)RuleEventKind.MatchEnded) return false;
                var e = new RuleEvent { Kind = (RuleEventKind)kind };
                var options = NoOptions;
                var actions = new byte[3];
                bool ok;
                switch (e.Kind)
                {
                    case RuleEventKind.EnemyDied:
                        ok = r.TryU16(out ushort a1) & r.TryU8(out byte b1) & r.TryU32(out uint c1) & ReadPos(ref r, arenaW, arenaH, ref e);
                        e.A = a1; e.B = b1; e.C = (int)Math.Min(c1, int.MaxValue);
                        break;
                    case RuleEventKind.GemSpawned:
                        ok = r.TryU16(out ushort a2) & r.TryU8(out byte b2) & r.TryU16(out ushort c2) & ReadPos(ref r, arenaW, arenaH, ref e);
                        e.A = a2; e.B = b2; e.C = c2;
                        break;
                    case RuleEventKind.GemCollected:
                        ok = r.TryU16(out ushort a3) & r.TryI8(out sbyte b3);
                        e.A = a3; e.B = b3;
                        break;
                    case RuleEventKind.LevelUpOffer:
                    {
                        ok = r.TryU8(out byte a4) & r.TryI16(out short b4) & r.TryU8(out byte c4) & r.TryU8(out byte n);
                        e.A = a4; e.B = b4; e.C = c4;
                        if (!ok || a4 >= PlayerStore.MaxSlots || n > 8) return false;
                        options = new OfferOption[n];
                        for (int k = 0; k < n; k++)
                        {
                            if (!r.TryU8(out byte ok2) || ok2 > (byte)OptionKind.Survival || !r.TryU16(out ushort rf)) return false;
                            options[k] = new OfferOption { Kind = (OptionKind)ok2, Ref = rf };
                        }
                        if (!r.TryU8(out actions[0]) || !r.TryU8(out actions[1]) || !r.TryU8(out actions[2])) return false;
                        break;
                    }
                    case RuleEventKind.ChoiceApplied:
                        ok = r.TryU8(out byte a5) & r.TryU8(out byte b5) & r.TryU16(out ushort c5);
                        e.A = a5; e.B = b5; e.C = c5;
                        break;
                    case RuleEventKind.SynergyChanged:
                        ok = r.TryU8(out byte a6) & r.TryU16(out ushort b6) & r.TryU8(out byte c6);
                        e.A = a6; e.B = b6; e.C = c6;
                        break;
                    case RuleEventKind.EvolutionApplied:
                        ok = r.TryU8(out byte a7) & r.TryU16(out ushort b7) & r.TryU16(out ushort c7);
                        e.A = a7; e.B = b7; e.C = c7;
                        break;
                    case RuleEventKind.SpecialActivated:
                        ok = r.TryU8(out byte a8) & r.TryU16(out ushort b8) & ReadPos(ref r, arenaW, arenaH, ref e);
                        e.A = a8; e.B = b8;
                        break;
                    case RuleEventKind.PlayerDowned:
                        ok = r.TryU8(out byte a9) & ReadPos(ref r, arenaW, arenaH, ref e);
                        e.A = a9;
                        break;
                    case RuleEventKind.PlayerInactive:
                        ok = r.TryU8(out byte a10);
                        e.A = a10;
                        break;
                    case RuleEventKind.MatchEnded:
                        ok = r.TryU8(out byte a11) & r.TryF32(out float x11);
                        e.A = a11; e.X = x11;
                        ok &= a11 <= (byte)MatchPhase.Defeat;
                        break;
                    default:
                        return false;
                }
                if (!ok) return false;
                if (IsSlotEvent(e.Kind) && e.A >= PlayerStore.MaxSlots) return false;
                parsed.Add(new DecodedRuleEvent { Event = e, Options = options, Rerolls = actions[0], Bans = actions[1], Skips = actions[2] });
            }
            if (r.Remaining != 0) return false;
            into.AddRange(parsed);
            return true;
        }

        static bool IsSlotEvent(RuleEventKind k) =>
            k == RuleEventKind.ChoiceApplied || k == RuleEventKind.SynergyChanged || k == RuleEventKind.EvolutionApplied ||
            k == RuleEventKind.SpecialActivated || k == RuleEventKind.PlayerDowned || k == RuleEventKind.PlayerInactive;

        static bool ReadPos(ref ByteReader r, float aw, float ah, ref RuleEvent e)
        {
            if (!r.TryU16(out ushort x) || !r.TryU16(out ushort y)) return false;
            e.X = Quant.UnPos(x, 0, aw);
            e.Y = Quant.UnPos(y, 0, ah);
            return true;
        }
    }
}
