using System;

namespace ES.Sim
{
    /// <summary>
    /// Estado autoritativo da partida. Executado só no host; <see cref="Step"/>
    /// avança um tick fixo (1 / tickRate).
    /// </summary>
    public sealed class World
    {
        public readonly GameDatabase Db;
        public readonly RulesDef Rules;
        public readonly MatchConfig Config;
        public readonly MatchBalance Balance;
        public readonly ArenaDef Arena;
        public readonly float Dt;

        public int Tick;
        public float Elapsed;
        public MatchPhase Phase = MatchPhase.Running;
        public Rng Rng;

        public readonly PlayerStore Players = new PlayerStore();
        public readonly EnemyStore Enemies;
        public readonly ProjectileStore Hostile;
        public readonly GemStore Gems;
        public readonly EventBuffer Events;
        public readonly SpatialGrid EnemyGrid;
        public readonly PlayerProjectileStore PlayerProjectiles = new PlayerProjectileStore(256);

        /// <summary>Gerador de SourceId para modificadores concedidos.</summary>
        public int NextSourceId = 1;

        public readonly MatchStats Stats;

        /// <summary>Evoluções descobertas nesta partida (códice).</summary>
        public readonly System.Collections.Generic.HashSet<int> Discovered = new System.Collections.Generic.HashSet<int>();

        /// <summary>Solo com oferta aberta: o tempo não avança até a escolha.</summary>
        public bool PausedForChoice
        {
            get
            {
                if (Config.Coop) return false;
                foreach (var p in Players.Slots) if (p != null && p.IsActive && p.Progress.Current != null) return true;
                return false;
            }
        }

        /// <summary>XP coletado neste tick, distribuído pela progressão.</summary>
        public float PendingXp;

        public bool SpawningEnabled = true;
        public float SpawnAccumulator;
        public float NextSurgeAt;

        public World(GameDatabase db, MatchConfig cfg)
        {
            Db = db ?? throw new ArgumentNullException(nameof(db));
            Config = cfg ?? throw new ArgumentNullException(nameof(cfg));
            Rules = db.Def.rules;
            Balance = BalanceResolver.Resolve(db.Def.balance, cfg.Difficulty, cfg.TeamSize);
            Arena = db.Arena(cfg.ArenaId) ?? throw new ArgumentException("arena inexistente: " + cfg.ArenaId);
            Dt = 1f / Rules.tickRate;
            Rng = new Rng(cfg.Seed);
            NextSurgeAt = db.Def.spawn.surgeIntervalSeconds;
            Stats = new MatchStats(db.AbilityCount);

            Enemies = new EnemyStore(Rules.capEnemies);
            Hostile = new ProjectileStore(Rules.capHostileProjectiles);
            Gems = new GemStore(Rules.capGems + 64);
            Events = new EventBuffer(Rules.capCosmeticEvents);
            EnemyGrid = new SpatialGrid(Arena.width, Arena.height, 2f, Rules.capEnemies);

            SpawnPlayers();
        }

        void SpawnPlayers()
        {
            var center = new Vec2(Arena.width * 0.5f, Arena.height * 0.5f);
            int n = Math.Min(Config.Roster.Length, PlayerStore.MaxSlots);
            for (int i = 0; i < n; i++)
            {
                var slot = Config.Roster[i];
                int spec = Db.SpecializationIndex(slot.SpecializationId);
                if (spec < 0) throw new ArgumentException("especialização inexistente: " + slot.SpecializationId);

                double angle = Math.PI * 2 * i / Math.Max(1, n);
                var p = new PlayerState
                {
                    Slot = i,
                    Status = PlayerStatus.Active,
                    Name = slot.Name,
                    Specialization = spec,
                    Radius = Rules.playerRadius,
                    Pos = n == 1 ? center : center + new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 1.5f,
                    Synergies = new bool[Db.SynergyCount],
                };
                p.Stats.SetBase(StatId.MaxHp, Rules.playerMaxHp);
                p.Stats.SetBase(StatId.MoveSpeed, Rules.playerSpeed);
                p.Stats.SetBase(StatId.PickupRadius, Rules.playerPickup);
                p.Stats.SetBase(StatId.DamageTaken, 1f);
                p.Hp = p.Stats.Get(StatId.MaxHp);
                Players.Slots[i] = p;
            }

            for (int i = 0; i < n; i++)
            {
                var spec = Db.Specialization(Players.Slots[i].Specialization);
                Players.Slots[i].Special = new SpecialState { SpecialIndex = spec.Special, SourceId = NextSourceId++ };
                AbilitySystem.Grant(this, i, spec.StartingAbility);
            }
        }

        public void Step(PlayerInput[] inputs)
        {
            if (Phase != MatchPhase.Running) return;
            Events.Clear();
            if (PausedForChoice)
            {
                ProgressionSystem.ApplyInputs(this, inputs);
                return;
            }
            Tick++;
            Elapsed += Dt;

            MovementSystem.Step(this, inputs);
            SpecialSystem.ApplyInputs(this, inputs);
            SpawnDirector.Step(this, Dt);
            EnemyAISystem.Step(this, Dt);
            AbilitySystem.Step(this, Dt);
            SpecialSystem.Step(this, Dt);
            CombatSystem.Step(this, Dt);
            ProgressionSystem.ApplyInputs(this, inputs);
            ProgressionSystem.Step(this, Dt);
            MatchRules.Step(this);
        }
    }
}
