using System;

namespace ES.Sim
{
    /// <summary>
    /// Converte o tempo de quadro em ticks fixos. Quadros longos (ex.: aba que
    /// volta do segundo plano) rodam no máximo <c>maxTicksPerFrame</c> ticks e
    /// descartam o resto, evitando a "espiral da morte".
    /// </summary>
    public sealed class TickClock
    {
        readonly double _step;
        readonly int _maxTicks;
        double _acc;

        public TickClock(double tickRate = 30, int maxTicksPerFrame = 5)
        {
            _step = 1.0 / tickRate;
            _maxTicks = maxTicksPerFrame;
        }

        /// <summary>Fração do próximo tick já decorrida (para interpolação visual).</summary>
        public double Alpha => _acc / _step;

        public int Advance(double frameSeconds)
        {
            if (frameSeconds < 0) frameSeconds = 0;
            _acc += frameSeconds;
            int ticks = (int)Math.Floor(_acc / _step + 1e-9);
            if (ticks > _maxTicks)
            {
                _acc = 0;
                return _maxTicks;
            }
            _acc -= ticks * _step;
            if (_acc < 0) _acc = 0;
            return ticks;
        }

        public void Reset() => _acc = 0;
    }
}
