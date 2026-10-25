using System.Collections.Generic;

namespace ES.Game
{
    /// <summary>Limita vozes simultâneas: no máximo N por som e um teto geral.</summary>
    public sealed class VoiceLimiter
    {
        readonly int _perSound, _total;
        readonly Dictionary<string, List<double>> _ends = new Dictionary<string, List<double>>();
        readonly List<double> _all = new List<double>(32);

        public VoiceLimiter(int perSound = 4, int total = 24)
        {
            _perSound = perSound;
            _total = total;
        }

        public int Active(string id, double now)
        {
            if (!_ends.TryGetValue(id, out var list)) return 0;
            Prune(list, now);
            return list.Count;
        }

        /// <summary>Reserva uma voz até <paramref name="now"/> + duração; false se o limite foi atingido.</summary>
        public bool TryAcquire(string id, double now, double duration)
        {
            if (!_ends.TryGetValue(id, out var list)) _ends[id] = list = new List<double>(_perSound);
            Prune(list, now);
            Prune(_all, now);
            if (list.Count >= _perSound || _all.Count >= _total) return false;
            list.Add(now + duration);
            _all.Add(now + duration);
            return true;
        }

        static void Prune(List<double> list, double now)
        {
            for (int i = list.Count - 1; i >= 0; i--) if (list[i] <= now) list.RemoveAt(i);
        }
    }
}
