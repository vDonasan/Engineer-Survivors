using System.Linq;
using D = ES.Sim.Difficulty;

namespace ES.SimTests.Systems
{
    public class CombatTests
    {
        static World Open(D d = D.Normal)
        {
            var w = TestWorlds.CreateOpen(d);
            w.SpawningEnabled = false;
            return TestWorlds.Disarm(w);
        }

        static int Swarm(World w) => w.Db.EnemyIndex("microbo_enxame");

        [Test]
        public void Contact_RespectsInterval()
        {
            var w = Open();
            SpawnDirector.SpawnEnemy(w, Swarm(w), w.Players[0].Pos + new Vec2(0.2f, 0), false);
            for (int t = 0; t < 60; t++) w.Step(TestWorlds.NoInput());
            Assert.That(w.Players[0].Hp, Is.EqualTo(100 - 3 * 5).Within(1e-3));
        }

        [Test]
        public void Player_InvulnAfterHit()
        {
            var w = Open();
            CombatSystem.DamagePlayer(w, 0, 10);
            CombatSystem.DamagePlayer(w, 0, 10);
            Assert.That(w.Players[0].Hp, Is.EqualTo(90));
        }

        [Test]
        public void EnemyDamageScaledByBalance()
        {
            var w = Open(D.Easy);
            int e = SpawnDirector.SpawnEnemy(w, Swarm(w), new Vec2(5, 5), false);
            Assert.That(w.Enemies.ContactDamage[e], Is.EqualTo(3.25f).Within(1e-4));
        }

        [Test]
        public void Kill_SpawnsGemWithXp_AndRemovesAtEndOfTick()
        {
            var w = Open();
            int e = SpawnDirector.SpawnEnemy(w, Swarm(w), new Vec2(10, 10), false);
            bool killed = CombatSystem.DamageEnemy(w, e, 999, Vec2.Zero, 0, 0, -1);
            Assert.That(killed, Is.True);
            Assert.That(w.Events.Rule.Any(r => r.Kind == RuleEventKind.EnemyDied), Is.True);
            Assert.That(w.Events.Rule.Any(r => r.Kind == RuleEventKind.GemSpawned), Is.True);
            Assert.That(w.Gems.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(w.Gems.Value[0], Is.EqualTo(1));
            Assert.That(CombatSystem.DamageEnemy(w, e, 5, Vec2.Zero, 0, 0, -1), Is.False, "morto não toma dano");
            w.Step(TestWorlds.NoInput());
            Assert.That(w.Enemies.Count, Is.EqualTo(0));
        }

        [Test]
        public void EliteKill_GemX4()
        {
            var w = Open();
            int e = SpawnDirector.SpawnEnemy(w, Swarm(w), new Vec2(10, 10), true);
            CombatSystem.DamageEnemy(w, e, 999, Vec2.Zero, 0, 0, -1);
            Assert.That(Enumerable.Range(0, w.Gems.Count).Any(i => w.Gems.Kind[i] == PickupKind.Xp && w.Gems.Value[i] == 4), Is.True);
        }

        [Test]
        public void Gems_MergeAboveCap_PreservesXp()
        {
            var w = Open();
            var rng = new Rng(5);
            for (int k = 0; k < 200; k++)
            {
                int e = SpawnDirector.SpawnEnemy(w, Swarm(w), new Vec2(rng.Range(3f, 61f), rng.Range(3f, 61f)), false);
                CombatSystem.DamageEnemy(w, e, 999, Vec2.Zero, 0, 0, -1);
                CombatSystem.Cleanup(w);
            }
            Assert.That(w.Gems.Count, Is.LessThanOrEqualTo(150));
            float xp = Enumerable.Range(0, w.Gems.Count).Where(i => w.Gems.Kind[i] == PickupKind.Xp).Sum(i => w.Gems.Value[i]);
            Assert.That(xp, Is.EqualTo(200));
        }

        [Test]
        public void Player_DownedAtZeroHp()
        {
            var w = Open();
            CombatSystem.DamagePlayer(w, 0, 1000);
            Assert.That(w.Players[0].Status, Is.EqualTo(PlayerStatus.Downed));
            Assert.That(w.Players[0].Hp, Is.EqualTo(0));
            Assert.That(w.Events.Rule.Any(r => r.Kind == RuleEventKind.PlayerDowned && r.A == 0), Is.True);
        }

        [Test]
        public void HostileProjectile_HitsAndDespawns()
        {
            var w = Open();
            var p = w.Players[0];
            w.Hostile.Spawn(0, p.Pos + new Vec2(2, 0), new Vec2(-6, 0), 8, 0.2f, 4);
            for (int t = 0; t < 15; t++) w.Step(TestWorlds.NoInput());
            Assert.That(p.Hp, Is.EqualTo(92).Within(1e-3));
            Assert.That(w.Hostile.Count, Is.EqualTo(0));
        }

        [Test]
        public void HostileProjectile_ExpiresAtArenaEdge()
        {
            var w = Open();
            w.Hostile.Spawn(0, new Vec2(1, 1), new Vec2(-6, 0), 8, 0.2f, 4);
            for (int t = 0; t < 10; t++) w.Step(TestWorlds.NoInput());
            Assert.That(w.Hostile.Count, Is.EqualTo(0));
        }

        [Test]
        public void Pickup_CollectsWithinRadius()
        {
            var w = Open();
            var p = w.Players[0];
            w.Gems.Spawn(PickupKind.Xp, p.Pos + new Vec2(1.0f, 0), 3, 0);
            w.Gems.Spawn(PickupKind.Xp, p.Pos + new Vec2(5f, 0), 3, 0);
            w.Step(TestWorlds.NoInput());
            Assert.That(w.Gems.Count, Is.EqualTo(1));
            Assert.That(w.Events.Rule.Count(r => r.Kind == RuleEventKind.GemCollected && r.B == 0), Is.EqualTo(1));
        }

        [Test]
        public void HealPickup_RestoresFractionOfMaxHp()
        {
            var w = Open();
            var p = w.Players[0];
            p.Hp = 20;
            w.Gems.Spawn(PickupKind.Heal, p.Pos, 0.3f, 0);
            w.Step(TestWorlds.NoInput());
            Assert.That(p.Hp, Is.EqualTo(50).Within(0.1f));
        }

        [Test]
        public void Regen_HealsOverTime()
        {
            var w = Open();
            var p = w.Players[0];
            p.Hp = 50;
            p.Stats.SetBase(StatId.Regen, 1f);
            for (int t = 0; t < 30; t++) w.Step(TestWorlds.NoInput());
            Assert.That(p.Hp, Is.EqualTo(51).Within(0.01f));
        }
    }
}
