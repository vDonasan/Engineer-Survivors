using System;

namespace ES.Sim
{
    public static class ArenaCollision
    {
        /// <summary>Empurra o círculo para fora dos obstáculos e para dentro das bordas.</summary>
        public static Vec2 Resolve(Vec2 pos, float radius, ArenaDef arena)
        {
            var obstacles = arena.obstacles;
            if (obstacles != null)
            {
                for (int i = 0; i < obstacles.Length; i++)
                {
                    var o = obstacles[i];
                    float cx = Clamp(pos.X, o.x, o.x + o.w);
                    float cy = Clamp(pos.Y, o.y, o.y + o.h);
                    float dx = pos.X - cx, dy = pos.Y - cy;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= radius * radius) continue;

                    if (d2 > 1e-8f)
                    {
                        float d = (float)Math.Sqrt(d2);
                        float push = radius - d;
                        pos = new Vec2(pos.X + dx / d * push, pos.Y + dy / d * push);
                    }
                    else
                    {
                        // Centro dentro do retângulo: sai pelo lado mais próximo.
                        float left = pos.X - o.x, right = o.x + o.w - pos.X;
                        float down = pos.Y - o.y, up = o.y + o.h - pos.Y;
                        float min = Math.Min(Math.Min(left, right), Math.Min(down, up));
                        if (min == left) pos.X = o.x - radius;
                        else if (min == right) pos.X = o.x + o.w + radius;
                        else if (min == down) pos.Y = o.y - radius;
                        else pos.Y = o.y + o.h + radius;
                    }
                }
            }

            pos.X = Clamp(pos.X, radius, arena.width - radius);
            pos.Y = Clamp(pos.Y, radius, arena.height - radius);
            return pos;
        }

        /// <summary>Verdadeiro se o círculo sobrepõe algum obstáculo ou sai da arena.</summary>
        public static bool Blocked(Vec2 pos, float radius, ArenaDef arena)
        {
            if (pos.X < radius || pos.Y < radius || pos.X > arena.width - radius || pos.Y > arena.height - radius) return true;
            var obstacles = arena.obstacles;
            if (obstacles == null) return false;
            for (int i = 0; i < obstacles.Length; i++)
            {
                var o = obstacles[i];
                float cx = Clamp(pos.X, o.x, o.x + o.w), cy = Clamp(pos.Y, o.y, o.y + o.h);
                float dx = pos.X - cx, dy = pos.Y - cy;
                if (dx * dx + dy * dy < radius * radius) return true;
            }
            return false;
        }

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
