namespace ES.Sim
{
    public static class MovementSystem
    {
        /// <summary>
        /// Integra um tick de movimento. Mesmo código no host e na previsão do cliente.
        /// </summary>
        public static Vec2 Integrate(Vec2 pos, sbyte moveX, sbyte moveY, float speed, float dt, ArenaDef arena, float radius)
        {
            var dir = Vec2.ClampLength(new Vec2(Quant.UnAxis(moveX), Quant.UnAxis(moveY)), 1f);
            return ArenaCollision.Resolve(pos + dir * (speed * dt), radius, arena);
        }

        public static void Step(World w, PlayerInput[] inputs)
        {
            for (int slot = 0; slot < PlayerStore.MaxSlots; slot++)
            {
                var p = w.Players.Slots[slot];
                if (p == null) continue;
                var input = inputs != null && slot < inputs.Length ? inputs[slot] : PlayerInput.None;
                p.LastInput = input;
                p.LastInputSeq = input.Seq;
                if (!p.IsActive) { p.Vel = Vec2.Zero; continue; }

                var before = p.Pos;
                p.Pos = Integrate(p.Pos, input.MoveX, input.MoveY, p.Stats.Get(StatId.MoveSpeed), w.Dt, w.Arena, p.Radius);
                p.Vel = (p.Pos - before) / w.Dt;
                if (input.MoveX != 0 || input.MoveY != 0)
                    p.Facing = new Vec2(Quant.UnAxis(input.MoveX), Quant.UnAxis(input.MoveY)).Normalized();
            }
        }
    }
}
