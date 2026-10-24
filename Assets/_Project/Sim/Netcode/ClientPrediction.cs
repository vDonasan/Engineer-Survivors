namespace ES.Sim
{
    /// <summary>
    /// Previsão do jogador local: aplica as próprias entradas na hora e, ao receber
    /// a posição do host, reaplica as entradas ainda não confirmadas. Correções
    /// grandes são suavizadas em 100 ms para o desenho não saltar.
    /// </summary>
    public sealed class ClientPrediction
    {
        const int Capacity = 128;
        const float IgnoreError = 0.05f;
        const float SmoothSeconds = 0.1f;

        readonly ArenaDef _arena;
        readonly float _dt;
        readonly float _radius;
        readonly PlayerInput[] _inputs = new PlayerInput[Capacity];
        int _start, _count;

        Vec2 _offsetStart;
        float _offsetLeft;

        public Vec2 Predicted { get; private set; }
        public Vec2 RenderOffset { get; private set; }
        public Vec2 RenderPosition => Predicted + RenderOffset;
        public int PendingCount => _count;

        public ClientPrediction(RulesDef rules, ArenaDef arena)
        {
            _arena = arena;
            _dt = 1f / rules.tickRate;
            _radius = rules.playerRadius;
        }

        public void Reset(Vec2 pos)
        {
            Predicted = pos;
            RenderOffset = Vec2.Zero;
            _offsetLeft = 0;
            _start = _count = 0;
        }

        public void Record(in PlayerInput input, float speed)
        {
            if (_count == Capacity) { _start = (_start + 1) % Capacity; _count--; }
            _inputs[(_start + _count) % Capacity] = input;
            _count++;
            Predicted = MovementSystem.Integrate(Predicted, input.MoveX, input.MoveY, speed, _dt, _arena, _radius);
        }

        public void Reconcile(Vec2 serverPos, ushort lastProcessedSeq, float speed)
        {
            while (_count > 0 && !IsNewer(_inputs[_start].Seq, lastProcessedSeq))
            {
                _start = (_start + 1) % Capacity;
                _count--;
            }

            var p = serverPos;
            for (int i = 0; i < _count; i++)
            {
                var input = _inputs[(_start + i) % Capacity];
                p = MovementSystem.Integrate(p, input.MoveX, input.MoveY, speed, _dt, _arena, _radius);
            }

            if ((p - Predicted).Length() < IgnoreError) return;
            _offsetStart = RenderOffset + (Predicted - p);
            _offsetLeft = SmoothSeconds;
            RenderOffset = _offsetStart;
            Predicted = p;
        }

        /// <summary>Avança a suavização da última correção (chamar a cada quadro).</summary>
        public void Tick(float dt)
        {
            if (_offsetLeft <= 0) { RenderOffset = Vec2.Zero; return; }
            _offsetLeft -= dt;
            RenderOffset = _offsetLeft <= 0 ? Vec2.Zero : _offsetStart * (_offsetLeft / SmoothSeconds);
        }

        static bool IsNewer(ushort seq, ushort than)
        {
            ushort d = (ushort)(seq - than);
            return d != 0 && d < 32768;
        }
    }
}
