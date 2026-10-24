using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>
    /// Linha do tempo de snapshots recebidos. Desenha o mundo um pouco no passado
    /// (100–200 ms) e aumenta o atraso quando as chegadas oscilam (WebSocket é TCP).
    /// </summary>
    public sealed class InterpBuffer
    {
        const int MaxTicks = 32;
        const double OffsetCreep = 0.0005;
        const double JitterDecay = 0.97;

        readonly double _min, _max, _tickSeconds;
        readonly List<int> _ticks = new List<int>(MaxTicks + 1);
        double _offset = double.MaxValue;
        double _jitter;

        public InterpBuffer(double minDelay = 0.1, double maxDelay = 0.2, double tickSeconds = 1.0 / 30.0)
        {
            _min = minDelay;
            _max = maxDelay;
            _tickSeconds = tickSeconds;
        }

        public double Delay => Math.Max(_min, Math.Min(_max, _min + _jitter));
        public int LatestTick => _ticks.Count == 0 ? -1 : _ticks[_ticks.Count - 1];

        public void Push(int tick, double arrivalTime)
        {
            double sample = arrivalTime - tick * _tickSeconds;
            _offset = Math.Min(sample, _offset == double.MaxValue ? sample : _offset + OffsetCreep);
            double dev = sample - _offset;
            _jitter = dev > _jitter ? dev : _jitter * JitterDecay + dev * (1 - JitterDecay);

            if (_ticks.Count > 0 && tick <= LatestTick) return;
            _ticks.Add(tick);
            if (_ticks.Count > MaxTicks) _ticks.RemoveAt(0);
        }

        /// <summary>Par de ticks que cerca o instante de desenho e a fração entre eles.</summary>
        public bool TrySample(double now, out int fromTick, out int toTick, out float t)
        {
            fromTick = toTick = -1;
            t = 0;
            if (_ticks.Count == 0) return false;

            double render = (now - _offset - Delay) / _tickSeconds;
            int latest = _ticks[_ticks.Count - 1];
            if (render >= latest) { fromTick = toTick = latest; return true; }
            if (render <= _ticks[0]) { fromTick = toTick = _ticks[0]; return true; }

            for (int i = _ticks.Count - 1; i > 0; i--)
            {
                int a = _ticks[i - 1], b = _ticks[i];
                if (render < a) continue;
                fromTick = a;
                toTick = b;
                t = (float)((render - a) / (b - a));
                return true;
            }
            fromTick = toTick = _ticks[0];
            return true;
        }

        public void Clear()
        {
            _ticks.Clear();
            _offset = double.MaxValue;
            _jitter = 0;
        }
    }
}
