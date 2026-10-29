using System;

namespace ES.Sim
{
    /// <summary>Surgimento de inimigos por orçamento, composição por minuto e surtos.</summary>
    public static class SpawnDirector
    {
        const int MaxSpawnsPerTick = 24;
        const float MaxBacklog = 12f;

        /// <summary>Pontos de custo por segundo.</summary>
        public static float Budget(SpawnDef s, in MatchBalance b, float elapsedSeconds)
        {
            float minutes = elapsedSeconds / 60f;
            return (s.baseBudget + s.rampPerMinute * b.SpawnRamp * minutes) * b.SpawnBudget * b.EnemyCount;
        }

        /// <summary>
        /// Coop com alguém caído: a horda segue o perfil de equipe dos jogadores ativos (senão o orçamento de
        /// 2–4 jogadores cai inteiro na tela de quem sobrou). 1 com todos ativos.
        /// </summary>
        public static float TeamFactor(World w)
        {
            int team = w.Players.CountWith(PlayerStatus.Active) + w.Players.CountWith(PlayerStatus.Downed);
            int active = w.Players.CountWith(PlayerStatus.Active);
            if (active <= 0 || active >= team) return 1f;
            float full = TeamEnemyCount(w, team), now = TeamEnemyCount(w, active);
            return full > 0 ? now / full : 1f;
        }

        static float TeamEnemyCount(World w, int size)
        {
            foreach (var t in w.Db.Def.balance.teams) if (t.size == size) return t.enemyCount;
            return size;
        }

        /// <summary>Distância (a todos os jogadores ativos) a partir da qual um inimigo comum é reciclado.</summary>
        public const float RecycleDistance = 40f;
        const float RecycleEvery = 1f;

        /// <summary>
        /// Remove inimigos comuns longe de todo jogador ativo (ex.: os que cercavam quem caiu): não são abate nem dão XP;
        /// o orçamento recria a horda perto de quem está jogando. Bosses nunca. Retorna quantos saíram.
        /// </summary>
        public static int Recycle(World w)
        {
            var e = w.Enemies;
            int removed = 0;
            float r2 = RecycleDistance * RecycleDistance;
            for (int i = e.Count - 1; i >= 0; i--)
            {
                if ((e.Flags[i] & EnemyFlags.Dead) != 0 || w.Db.Enemy(e.Type[i]).Behavior == EnemyBehavior.Boss) continue;
                bool near = false;
                foreach (var p in w.Players.Slots)
                    if (p != null && p.IsActive && Vec2.DistanceSq(p.Pos, e.Pos[i]) < r2) { near = true; break; }
                if (near) continue;
                w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.EnemyRemoved, A = e.Id[i], C = w.Tick });
                e.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        /// <summary>Durante uma luta de boss (fora do Modo Infinito) a horda diminui para o boss ser o foco.</summary>
        const float BossFightBudget = 0.6f;

        /// <summary>Bracket vigente; <paramref name="shiftMinutes"/> positivo antecipa.</summary>
        public static int BracketIndexAt(ResolvedBracket[] brackets, float elapsedMinutes, float shiftMinutes)
        {
            float t = elapsedMinutes + shiftMinutes;
            int idx = 0;
            for (int i = 0; i < brackets.Length; i++) if (brackets[i].FromMinute <= t) idx = i;
            return idx;
        }

        public static void Step(World w, float dt)
        {
            // reciclagem vale mesmo sem surgimento (fim da luta final, testes): é desempenho, não regra
            if (w.Tick % (int)Math.Max(1, Math.Round(RecycleEvery / w.Dt)) == 0 && w.Players.CountWith(PlayerStatus.Active) > 0) Recycle(w);
            if (!w.SpawningEnabled) return;
            var s = w.Db.Def.spawn;

            if (w.Elapsed >= w.NextSurgeAt)
            {
                w.NextSurgeAt += s.surgeIntervalSeconds;
                Surge(w, (int)Math.Round(s.surgeCount * w.Balance.EnemyCount * TeamFactor(w) * (w.Bosses.Infinite ? BossSystem.InfiniteSpawn(w) : 1f)));
            }

            float mult = TeamFactor(w) * (w.Bosses.Infinite ? BossSystem.InfiniteSpawn(w) : w.Bosses.Active.Count > 0 ? BossFightBudget : 1f) * w.EventDir.SpawnMult;
            w.SpawnAccumulator = Math.Min(w.SpawnAccumulator + Budget(s, w.Balance, w.Elapsed) * mult * dt, MaxBacklog * mult);
            var bracket = w.Db.Brackets[BracketIndexAt(w.Db.Brackets, w.Elapsed / 60f, w.Balance.CompositionShiftMinutes)];

            for (int n = 0; n < MaxSpawnsPerTick; n++)
            {
                if (w.Enemies.Count >= w.Enemies.Capacity) { w.SpawnAccumulator = 0; return; }
                int type = Pick(ref w.Rng, bracket);
                float cost = w.Db.Enemy(type).Def.cost;
                if (w.SpawnAccumulator < cost) return;
                if (!TryRingPoint(w, out var pos, out _)) return;
                bool elite = w.Rng.NextFloat() < w.Balance.EliteChance + BossSystem.InfiniteElite(w);
                SpawnEnemy(w, type, pos, elite);
                w.SpawnAccumulator -= cost;
            }
        }

        static int Pick(ref Rng rng, ResolvedBracket b)
        {
            float r = rng.NextFloat() * b.TotalWeight;
            for (int i = 0; i < b.Weights.Length; i++)
            {
                r -= b.Weights[i];
                if (r < 0) return b.Enemies[i];
            }
            return b.Enemies[b.Enemies.Length - 1];
        }

        public static void Surge(World w, int count)
        {
            int swarm = -1;
            for (int i = 0; i < w.Db.EnemyCount; i++) if (w.Db.Enemy(i).Def.role == "swarm") { swarm = i; break; }
            if (swarm < 0 || !RandomActivePlayer(w, out var target)) return;

            float radius = w.Rules.cameraHalfDiagonal + w.Db.Def.spawn.spawnPadding;
            float r = w.Db.Enemy(swarm).Def.radius;
            float offset = w.Rng.NextFloat() * (float)Math.PI * 2;
            for (int i = 0; i < count && w.Enemies.Count < w.Enemies.Capacity; i++)
            {
                double a = offset + Math.PI * 2 * i / count;
                var pos = target.Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * radius;
                if (ArenaCollision.Blocked(pos, r, w.Arena)) continue;
                SpawnEnemy(w, swarm, pos, false);
            }
        }

        static bool RandomActivePlayer(World w, out PlayerState player)
        {
            player = null;
            int active = w.Players.CountWith(PlayerStatus.Active);
            if (active == 0) return false;
            int pick = w.Rng.Range(0, active);
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                if (pick-- == 0) { player = p; return true; }
            }
            return false;
        }

        /// <summary>Ponto num anel fora da visão da câmera, dentro da arena e livre.</summary>
        static bool TryRingPoint(World w, out Vec2 pos, out PlayerState target)
        {
            pos = default;
            if (!RandomActivePlayer(w, out target)) return false;
            float min = w.Rules.cameraHalfDiagonal + w.Db.Def.spawn.spawnPadding;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float a = w.Rng.NextFloat() * (float)Math.PI * 2;
                float r = min + w.Rng.NextFloat() * 2f;
                var p = target.Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * r;
                if (ArenaCollision.Blocked(p, 0.8f, w.Arena)) continue;
                pos = p;
                return true;
            }
            return false;
        }

        /// <summary>Cria um inimigo já balanceado. Retorna o índice ou -1 se cheio.</summary>
        public static int SpawnEnemy(World w, int type, Vec2 pos, bool elite)
        {
            int i = w.Enemies.Spawn(type, pos);
            if (i < 0) return -1;
            var d = w.Db.Enemy(type).Def;
            var r = w.Rules;
            var e = w.Enemies;
            float hp = d.hp * w.Balance.EnemyHp * (elite ? r.eliteHp : 1f) * BossSystem.InfiniteHp(w);
            e.Hp[i] = e.MaxHp[i] = hp;
            e.Radius[i] = d.radius * (elite ? r.eliteSize : 1f);
            e.Speed[i] = d.speed;
            e.ContactDamage[i] = d.contactDamage * w.Balance.EnemyDamage * (elite ? r.eliteDamage : 1f) * BossSystem.InfiniteDamage(w);
            e.XpValue[i] = d.xp * (elite ? r.eliteXp : 1f);
            e.NextFire[i] = w.Elapsed + d.fireInterval * (0.5f + 0.5f * w.Rng.NextFloat());
            if (elite) e.Flags[i] |= EnemyFlags.Elite;
            return i;
        }
    }
}
