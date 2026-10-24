using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>Tipos de mensagem do jogo (um nome de mensagem NGO por tipo).</summary>
    public enum MessageType : byte { Welcome = 1, Lobby, Pick, Start, Input, Snapshot, Events, HostBackground }

    public enum Channel : byte { Reliable, Unreliable }

    /// <summary>Envio de bytes para um par. No cliente, o único par é o host.</summary>
    public interface ILink
    {
        void Send(ulong peer, MessageType type, byte[] data, int length, Channel channel);
    }

    public static class Peers
    {
        public const ulong Host = 0;
    }

    public sealed class LobbyEntry
    {
        public ulong Peer;
        public int Slot;
        public string Name;
        public string SpecializationId;
    }

    public sealed class LobbyState
    {
        public readonly List<LobbyEntry> Players = new List<LobbyEntry>(PlayerStore.MaxSlots);
        public Difficulty Difficulty = Difficulty.Normal;
        public bool Started;
    }

    /// <summary>Código de convite digitado pelo jogador: sem espaços, maiúsculo, só A–Z e 0–9.</summary>
    public static class JoinCode
    {
        public const int MinLength = 6, MaxLength = 8;

        public static bool TryNormalize(string raw, out string code)
        {
            code = null;
            if (string.IsNullOrEmpty(raw)) return false;
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char ch in raw)
            {
                if (char.IsWhiteSpace(ch)) continue;
                char c = char.ToUpperInvariant(ch);
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
                sb.Append(c);
            }
            if (sb.Length < MinLength || sb.Length > MaxLength) return false;
            code = sb.ToString();
            return true;
        }
    }
}
