using System;

namespace ES.Sim
{
    /// <summary>
    /// Jogador automático para testes e balanceamento: foge do perigo, coleta
    /// gemas, usa o especial quando cercado e escolhe cartas por heurística.
    /// Sem estado próprio: tudo vem do World.
    /// </summary>
    public static class BotBrain
    {
        const float FleeRadius = 6f;
        const float CrowdRadius = 4f;
        const int CrowdForSpecial = 8;
        const float WallMargin = 4f;

        static readonly int[] Near = new int[256];

        public static PlayerInput Decide(World w, int slot, ushort seq)
        {
            var input = new PlayerInput { Seq = seq, ChoiceIndex = -1 };
            var p = w.Players[slot];
            if (p == null || !p.IsActive) return input;

            var offer = p.Progress.Current;
            if (offer != null)
            {
                input.ChoiceOfferId = offer.OfferId;
                input.ChoiceIndex = (sbyte)BestOption(w, p, offer);
            }

            var e = w.Enemies;
            Vec2 dir = Vec2.Zero;
            int n = w.EnemyGrid.Query(p.Pos, FleeRadius, Near);
            int crowd = 0;
            for (int k = 0; k < n; k++)
            {
                int i = Near[k];
                if (i >= e.Count || (e.Flags[i] & EnemyFlags.Dead) != 0) continue;
                var away = p.Pos - e.Pos[i];
                float d = away.Length();
                if (d < CrowdRadius) crowd++;
                float weight = 1f - d / FleeRadius;
                dir += away.Normalized() * (weight * weight * 3f);
            }
            for (int i = 0; i < w.Hostile.Count; i++)
            {
                var away = p.Pos - w.Hostile.Pos[i];
                float d = away.Length();
                if (d < 3f) dir += away.Normalized() * (1f - d / 3f) * 2f;
            }

            float hpFrac = p.Hp / Math.Max(1f, p.Stats.Get(StatId.MaxHp));
            int gem = NearestPickup(w, p.Pos, hpFrac < 0.6f);
            if (gem >= 0) dir += (w.Gems.Pos[gem] - p.Pos).Normalized() * (w.Gems.Kind[gem] == PickupKind.Heal ? 1.5f : 0.7f);

            var a = w.Arena;
            if (p.Pos.X < WallMargin) dir.X += (WallMargin - p.Pos.X) / WallMargin * 2f;
            if (p.Pos.X > a.width - WallMargin) dir.X -= (p.Pos.X - (a.width - WallMargin)) / WallMargin * 2f;
            if (p.Pos.Y < WallMargin) dir.Y += (WallMargin - p.Pos.Y) / WallMargin * 2f;
            if (p.Pos.Y > a.height - WallMargin) dir.Y -= (p.Pos.Y - (a.height - WallMargin)) / WallMargin * 2f;

            if (dir.LengthSq() < 1e-4f) dir = (new Vec2(a.width * 0.5f, a.height * 0.5f) - p.Pos) * 0.05f;
            dir = Vec2.ClampLength(dir, 1f);
            input.MoveX = Quant.Axis(dir.X);
            input.MoveY = Quant.Axis(dir.Y);

            var s = p.Special;
            input.SpecialCounter = s != null && crowd >= CrowdForSpecial && s.CooldownLeft <= 0 ? (byte)(s.LastCounter + 1) : s?.LastCounter ?? 0;
            return input;
        }

        static int NearestPickup(World w, Vec2 pos, bool wantHeal)
        {
            var g = w.Gems;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < g.Count; i++)
            {
                float d = Vec2.DistanceSq(pos, g.Pos[i]);
                if (g.Kind[i] == PickupKind.Heal && wantHeal) d *= 0.25f;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>Evolução &gt; nova arma afim &gt; melhoria de arma &gt; componente &gt; atributo; Kit se a vida estiver baixa.</summary>
        static int BestOption(World w, PlayerState p, LevelUpOffer offer)
        {
            var spec = w.Db.Specialization(p.Specialization);
            float hpFrac = p.Hp / Math.Max(1f, p.Stats.Get(StatId.MaxHp));
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < offer.Options.Length; i++)
            {
                var o = offer.Options[i];
                float score;
                switch (o.Kind)
                {
                    case OptionKind.Evolution: score = 100; break;
                    case OptionKind.Survival: score = hpFrac < 0.35f ? 90 : 1; break;
                    default:
                    {
                        var ab = w.Db.Ability(o.Ref);
                        bool affine = ab.Tags.Overlaps(spec.AffinityTags);
                        if (o.Kind == OptionKind.Attribute) score = 30 + (hpFrac < 0.5f && ab.Grants.Length > 0 && (ab.Grants[0].Stat == StatId.MaxHp || ab.Grants[0].Stat == StatId.Regen) ? 20 : 0);
                        else if (ab.Kind == AbilityKind.Weapon) score = (o.Kind == OptionKind.NewAbility ? 60 : 50) + (affine ? 20 : 0);
                        else score = (o.Kind == OptionKind.NewAbility ? 40 : 35) + (affine ? 10 : 0);
                        break;
                    }
                }
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
    }
}
