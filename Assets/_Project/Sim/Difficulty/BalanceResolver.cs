using System;

namespace ES.Sim
{
    /// <summary>
    /// Único lugar que combina perfis de dificuldade, de equipe e ajustes por célula.
    /// Multiplicativos: valor = dificuldade × equipe × ajuste. Aditivos (+): soma.
    /// </summary>
    public static class BalanceResolver
    {
        public static readonly string[] KnownParams =
        {
            "enemyDamage", "enemyHp", "spawnBudget", "spawnRamp", "compositionShiftMinutes",
            "xp", "healDropChance", "regen", "eliteChance", "telegraph", "enemyCount", "abilityDamage",
        };

        public static bool IsKnownParam(string name) => Array.IndexOf(KnownParams, name) >= 0;

        public static MatchBalance Resolve(BalanceDef def, Difficulty difficulty, int teamSize)
        {
            if (teamSize < 1 || teamSize > 4) throw new ArgumentOutOfRangeException(nameof(teamSize), teamSize, "equipe de 1 a 4");

            var d = FindDifficulty(def, difficulty);
            var t = FindTeam(def, teamSize);

            var b = new MatchBalance
            {
                Difficulty = difficulty,
                TeamSize = teamSize,
                EnemyDamage = d.enemyDamage,
                EnemyHp = d.enemyHp * t.enemyHp,
                SpawnBudget = d.spawnBudget,
                SpawnRamp = d.spawnRamp,
                CompositionShiftMinutes = d.compositionShiftMinutes,
                Xp = d.xp * t.xpPerGem,
                HealDropChance = d.healDropChance,
                Regen = d.regen,
                EliteChance = d.eliteChance + t.eliteChance,
                Telegraph = d.telegraph,
                EnemyCount = t.enemyCount,
                AbilityDamage = d.abilityDamage * t.abilityDamage,
            };

            string id = Difficulties.Id(difficulty);
            if (def.overrides != null)
                foreach (var o in def.overrides)
                    if (o != null && o.difficulty == id && o.teamSize == teamSize) Apply(ref b, o.param, o.value);

            return b;
        }

        static void Apply(ref MatchBalance b, string param, float v)
        {
            switch (param)
            {
                case "enemyDamage": b.EnemyDamage *= v; break;
                case "enemyHp": b.EnemyHp *= v; break;
                case "spawnBudget": b.SpawnBudget *= v; break;
                case "spawnRamp": b.SpawnRamp *= v; break;
                case "compositionShiftMinutes": b.CompositionShiftMinutes += v; break;
                case "xp": b.Xp *= v; break;
                case "healDropChance": b.HealDropChance += v; break;
                case "regen": b.Regen *= v; break;
                case "eliteChance": b.EliteChance += v; break;
                case "telegraph": b.Telegraph *= v; break;
                case "enemyCount": b.EnemyCount *= v; break;
                case "abilityDamage": b.AbilityDamage *= v; break;
                default: throw new ArgumentException("parâmetro de ajuste desconhecido: " + param);
            }
        }

        static DifficultyProfileDef FindDifficulty(BalanceDef def, Difficulty difficulty)
        {
            string id = Difficulties.Id(difficulty);
            foreach (var d in def.difficulties) if (d.id == id) return d;
            throw new ArgumentException("perfil de dificuldade ausente: " + id);
        }

        static TeamProfileDef FindTeam(BalanceDef def, int size)
        {
            foreach (var t in def.teams) if (t.size == size) return t;
            throw new ArgumentException("perfil de equipe ausente: " + size);
        }
    }
}
