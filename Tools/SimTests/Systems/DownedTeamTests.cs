using System.Linq;

namespace ES.SimTests.Systems
{
    /// <summary>Coop com alguém caído: a horda acompanha quem está ativo (desempenho de quem sobra).</summary>
    public class DownedTeamTests
    {
        [Test]
        public void SpawnBudget_FollowsActivePlayers()
        {
            var w = TestWorlds.Disarm(TestWorlds.CreateOpen(team: 2, coop: true));
            float both = SpawnDirector.TeamFactor(w);
            CombatSystem.DamagePlayer(w, 0, 1e6f);
            float one = SpawnDirector.TeamFactor(w);
            Assert.That(both, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(one, Is.EqualTo(1.0f / 1.5f).Within(1e-3f), "perfil de 1 jogador sobre o de 2");
        }

        [Test]
        public void FarEnemies_AreRecycled_BossesNever()
        {
            var w = TestWorlds.Disarm(TestWorlds.CreateOpen(team: 2, coop: true));
            w.SpawningEnabled = false;
            w.Players[0].Pos = new Vec2(4, 4);
            w.Players[1].Pos = new Vec2(60, 60);
            int near = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("microbo_enxame"), new Vec2(8, 8), false);
            int far = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("microbo_enxame"), new Vec2(6, 58), false);
            var boss = BossSystem.Spawn(w, 0, 0);
            ushort farId = w.Enemies.Id[far], nearId = w.Enemies.Id[near];
            // o jogador 1 cai: o inimigo perto dele fica longe de todos os ativos
            CombatSystem.DamagePlayer(w, 1, 1e6f);
            int bossIdx = Enumerable.Range(0, w.Enemies.Count).First(i => w.Enemies.Id[i] == boss.EnemyId);
            w.Enemies.Pos[bossIdx] = new Vec2(60, 4);
            int kills = w.Stats.Kills;
            for (int k = 0; k < 30 * 3; k++)
            {
                w.Players[0].InvulnUntil = float.MaxValue;
                w.Step(TestWorlds.NoInput());
            }
            var ids = Enumerable.Range(0, w.Enemies.Count).Select(i => w.Enemies.Id[i]).ToList();
            Assert.That(ids, Does.Contain(nearId));
            Assert.That(ids, Does.Contain(boss.EnemyId), "boss nunca some");
            Assert.That(w.Stats.Kills, Is.EqualTo(kills), "reciclar não conta abate nem dá XP");
            // o microbo do canto longe: ou foi reciclado ou andou para perto
            int fi = ids.IndexOf(farId);
            Assert.That(fi < 0 || Vec2.DistanceSq(w.Enemies.Pos[fi], w.Players[0].Pos) < 40f * 40f, Is.True);
        }

        [Test]
        public void Recycle_RemovesOnlyBeyondDistance()
        {
            var w = TestWorlds.Disarm(TestWorlds.CreateOpen(team: 1));
            w.SpawningEnabled = false;
            w.Players[0].Pos = new Vec2(4, 4);
            int a = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("prensa_ambulante"), new Vec2(60, 60), false);
            int b = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("prensa_ambulante"), new Vec2(20, 20), false);
            ushort farId = w.Enemies.Id[a];
            int removed = SpawnDirector.Recycle(w);
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(Enumerable.Range(0, w.Enemies.Count).Any(i => w.Enemies.Id[i] == farId), Is.False);
            Assert.That(w.Events.Rule.Any(e => e.Kind == RuleEventKind.EnemyRemoved), Is.True, "clientes removem o inimigo (sem contar abate)");
        }
    }
}
