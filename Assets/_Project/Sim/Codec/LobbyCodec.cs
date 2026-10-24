namespace ES.Sim
{
    public enum RejectReason : byte { VersionMismatch, Full, AlreadyStarted, Malformed, Kicked }

    /// <summary>Mensagens de sala: boas-vindas, recusa e início de partida.</summary>
    public static class LobbyCodec
    {
        public static void WriteWelcome(ByteWriter w, int slot) => w.U8((byte)slot);

        public static bool TryReadWelcome(byte[] data, int length, out int slot)
        {
            var r = new ByteReader(data, length);
            bool ok = r.TryU8(out byte s);
            slot = s;
            return ok && s < PlayerStore.MaxSlots && r.Remaining == 0;
        }

        public static void WriteReject(ByteWriter w, RejectReason reason) => w.U8((byte)reason);

        public static bool TryReadReject(byte[] data, int length, out RejectReason reason)
        {
            var r = new ByteReader(data, length);
            bool ok = r.TryU8(out byte b);
            reason = (RejectReason)b;
            return ok && b <= (byte)RejectReason.Kicked && r.Remaining == 0;
        }

        public static void WriteMatchStart(ByteWriter w, MatchConfig cfg, ulong contentHash)
        {
            w.U64(cfg.Seed);
            w.U8((byte)cfg.Difficulty);
            w.U8((byte)cfg.TeamSize);
            w.Bool(cfg.Coop);
            w.Str(cfg.ArenaId);
            w.U64(contentHash);
            w.U8((byte)cfg.Roster.Length);
            foreach (var slot in cfg.Roster)
            {
                w.Str(HelloCodec.SanitizeName(slot.Name));
                w.Str(slot.SpecializationId);
            }
        }

        public static bool TryReadMatchStart(byte[] data, int length, out MatchConfig cfg, out ulong contentHash)
        {
            cfg = null;
            contentHash = 0;
            var r = new ByteReader(data, length);
            if (!r.TryU64(out ulong seed) || !r.TryU8(out byte diff) || !r.TryU8(out byte team) || !r.TryBool(out bool coop)
                || !r.TryStr(32, out string arena) || !r.TryU64(out contentHash) || !r.TryU8(out byte n))
                return false;
            if (diff > (byte)Difficulty.Hard || team < 1 || team > PlayerStore.MaxSlots || n != team) return false;

            var roster = new PlayerSlot[n];
            for (int i = 0; i < n; i++)
            {
                if (!r.TryStr(64, out string name) || !r.TryStr(32, out string spec)) return false;
                roster[i] = new PlayerSlot { Name = HelloCodec.SanitizeName(name), SpecializationId = spec };
            }
            if (r.Remaining != 0) return false;
            cfg = new MatchConfig { Seed = seed, Difficulty = (Difficulty)diff, TeamSize = team, Coop = coop, ArenaId = arena, Roster = roster };
            return true;
        }
    }
}
