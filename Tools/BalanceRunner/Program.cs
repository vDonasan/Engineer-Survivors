using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ES.Sim;
using ES.SimTests;

// Executa partidas de bots nas 12 combinações (dificuldade × equipe) para cada
// especialização e semente, e grava métricas em CSV.
// Uso: dotnet run --project Tools/BalanceRunner -- --minutes 10 --seeds 3 --out Tools/BalanceRunner/reports/x.csv

int minutes = 10, seeds = 3;
string output = Path.Combine("Tools", "BalanceRunner", "reports", "balance.csv");
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--minutes") minutes = int.Parse(args[++i], CultureInfo.InvariantCulture);
    else if (args[i] == "--seeds") seeds = int.Parse(args[++i], CultureInfo.InvariantCulture);
    else if (args[i] == "--out") output = args[++i];
}

var db = ContentLoader.Sp1Database();
var csv = new StringBuilder("difficulty,team,spec,seed,survivedSeconds,result,levelAvg,kills,damageTaken,peakEnemies,maxStepMs,avgStepMs,guirottoDiedAt,magoDiedAt,bravoCount,startDamage,totalDamage,startDamage5min\n");
var specs = Enumerable.Range(0, db.SpecializationCount).Select(i => db.Specialization(i).Def.id).ToArray();
var total = Stopwatch.StartNew();

foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
for (int team = 1; team <= 4; team++)
foreach (var spec in specs)
for (ulong seed = 1; seed <= (ulong)seeds; seed++)
{
    var roster = Enumerable.Range(0, team).Select(k => new PlayerSlot { Name = "Bot" + (k + 1), SpecializationId = spec }).ToArray();
    var w = new World(db, new MatchConfig { Seed = seed, Difficulty = d, TeamSize = team, Roster = roster, Coop = team > 1 });
    var inputs = new PlayerInput[4];
    ushort seq = 0;
    int peak = 0;
    double maxMs = 0, sumMs = 0;
    long steps = 0;
    float guirottoAt = -1, magoAt = -1, start5 = -1;
    int startAbility = db.Specialization(db.SpecializationIndex(spec)).StartingAbility;
    int bravos = 0;
    var sw = new Stopwatch();
    while (w.Phase == MatchPhase.Running && w.Elapsed < minutes * 60f)
    {
        seq++;
        for (int s = 0; s < 4; s++) inputs[s] = w.Players[s] != null ? BotBrain.Decide(w, s, seq) : PlayerInput.None;
        sw.Restart();
        w.Step(inputs);
        double ms = sw.Elapsed.TotalMilliseconds;
        maxMs = Math.Max(maxMs, ms);
        sumMs += ms;
        steps++;
        peak = Math.Max(peak, w.Enemies.Count);
        if (start5 < 0 && w.Elapsed >= 300) start5 = StartDamage(w, startAbility);
        foreach (var ev in w.Events.Rule)
        {
            if (ev.Kind == RuleEventKind.BossBravo) bravos++;
            if (ev.Kind != RuleEventKind.BossDefeated) continue;
            if (db.Boss(ev.A).Def.id == "guirotto") guirottoAt = w.Elapsed; else magoAt = w.Elapsed;
        }
    }

    var players = w.Players.Slots.Where(p => p != null).ToArray();
    string result = w.Phase == MatchPhase.Running ? "timeout" : w.Phase.ToString().ToLowerInvariant();
    csv.AppendLine(string.Join(",",
        Difficulties.Id(d), team, spec, seed,
        w.Elapsed.ToString("0.0", CultureInfo.InvariantCulture), result,
        players.Average(p => p.Progress.Level).ToString("0.0", CultureInfo.InvariantCulture),
        w.Stats.Kills,
        w.Stats.DamageTaken.Sum().ToString("0", CultureInfo.InvariantCulture),
        peak,
        maxMs.ToString("0.00", CultureInfo.InvariantCulture),
        (sumMs / Math.Max(1, steps)).ToString("0.000", CultureInfo.InvariantCulture),
        guirottoAt.ToString("0", CultureInfo.InvariantCulture), magoAt.ToString("0", CultureInfo.InvariantCulture), bravos,
        StartDamage(w, startAbility).ToString("0", CultureInfo.InvariantCulture), TotalDamage(w).ToString("0", CultureInfo.InvariantCulture), start5.ToString("0", CultureInfo.InvariantCulture)));
    Console.WriteLine($"{Difficulties.Id(d)}/{team}/{spec}/{seed}: {result} em {w.Elapsed:0}s, nível médio {players.Average(p => p.Progress.Level):0.0}, abates {w.Stats.Kills}, pico {peak}, Guirotto {guirottoAt:0}s, Mago {magoAt:0}s");
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
File.WriteAllText(output, csv.ToString());
Console.WriteLine($"CSV: {Path.GetFullPath(output)} ({total.Elapsed.TotalSeconds:0}s)");

/// <summary>Dano da arma inicial (e da sua evolução) somado entre os jogadores.</summary>
static float StartDamage(World w, int start)
{
    float d = 0;
    foreach (var p in w.Players.Slots)
    {
        if (p == null) continue;
        var row = w.Stats.DamageByAbility[p.Slot];
        d += row[start];
        for (int e = 0; e < w.Db.EvolutionCount; e++) if (w.Db.Evolution(e).From == start) d += row[w.Db.Evolution(e).Result];
    }
    return d;
}

static float TotalDamage(World w)
{
    float d = 0;
    foreach (var p in w.Players.Slots) if (p != null) foreach (var v in w.Stats.DamageByAbility[p.Slot]) d += v;
    return d;
}
