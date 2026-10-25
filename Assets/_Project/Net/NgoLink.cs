using System;
using ES.Sim;
using Unity.Collections;
using Unity.Netcode;

namespace ES.Net
{
    /// <summary>ILink sobre mensagens nomeadas do NGO ("es.&lt;tipo&gt;").</summary>
    public sealed class NgoLink : ILink
    {
        const int MaxPayload = 64 * 1024;
        static readonly MessageType[] Types = (MessageType[])Enum.GetValues(typeof(MessageType));

        readonly NetworkManager _nm;
        byte[] _rx = new byte[4096];

        /// <summary>Bytes de carga útil enviados e recebidos (painel de desempenho).</summary>
        public long BytesSent, BytesReceived;

        public NgoLink(NetworkManager nm) { _nm = nm; }

        public static string Name(MessageType t) => "es." + t;

        public void Send(ulong peer, MessageType type, byte[] data, int length, Channel channel)
        {
            if (_nm == null || !_nm.IsListening) return;
            using var writer = new FastBufferWriter(length + 8, Allocator.Temp, MaxPayload);
            writer.WriteValueSafe(length);
            writer.WriteBytesSafe(data, length);
            var delivery = channel == Channel.Reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced;
            _nm.CustomMessagingManager.SendNamedMessage(Name(type), peer, writer, delivery);
            BytesSent += length + sizeof(int);
        }

        /// <summary>Registra um tratador por tipo; dados malformados chegam como comprimento -1.</summary>
        public void Register(Action<ulong, MessageType, byte[], int> onMessage)
        {
            foreach (var t in Types)
            {
                var type = t;
                _nm.CustomMessagingManager.RegisterNamedMessageHandler(Name(type), (sender, reader) =>
                {
                    if (!reader.TryBeginRead(sizeof(int))) { onMessage(sender, type, _rx, -1); return; }
                    reader.ReadValue(out int length);
                    if (length < 0 || length > MaxPayload || !reader.TryBeginRead(length)) { onMessage(sender, type, _rx, -1); return; }
                    if (_rx.Length < length) _rx = new byte[Math.Max(length, _rx.Length * 2)];
                    reader.ReadBytes(ref _rx, length);
                    BytesReceived += length + sizeof(int);
                    onMessage(sender, type, _rx, length);
                });
            }
        }

        public void Unregister()
        {
            if (_nm == null || _nm.CustomMessagingManager == null) return;
            foreach (var t in Types) _nm.CustomMessagingManager.UnregisterNamedMessageHandler(Name(t));
        }
    }
}
