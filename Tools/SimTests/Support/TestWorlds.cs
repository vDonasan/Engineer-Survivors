using System.Linq;

namespace ES.SimTests
{
    /// <summary>Atalhos para montar mundos de teste com o conteúdo do SP1.</summary>
    public static class TestWorlds
    {
        static GameDatabase _db;
        public static GameDatabase Db => _db ??= ContentLoader.Sp1Database();

        public static World Create(ES.Sim.Difficulty difficulty = ES.Sim.Difficulty.Normal, int team = 1,
            string spec = "eletrica", ulong seed = 1, bool coop = false, GameDatabase db = null)
        {
            var roster = Enumerable.Range(0, team)
                .Select(i => new PlayerSlot { Name = "P" + (i + 1), SpecializationId = spec })
                .ToArray();
            var cfg = new MatchConfig { Seed = seed, Difficulty = difficulty, TeamSize = team, ArenaId = "laboratorio", Roster = roster, Coop = coop };
            return new World(db ?? Db, cfg);
        }

        public static PlayerInput[] NoInput(int count = 4) => new PlayerInput[count].Select(_ => new PlayerInput { ChoiceIndex = -1 }).ToArray();

        /// <summary>Remove obstáculos da arena para testes de movimento/IA em campo aberto.</summary>
        public static World CreateOpen(ES.Sim.Difficulty difficulty = ES.Sim.Difficulty.Normal, int team = 1, string spec = "eletrica", ulong seed = 1, bool coop = false)
        {
            var def = ContentLoader.LoadSp1();
            foreach (var a in def.arenas) a.obstacles = new RectDef[0];
            var db = GameDatabase.Build(def).Db;
            return Create(difficulty, team, spec, seed, coop, db);
        }
    }
}
