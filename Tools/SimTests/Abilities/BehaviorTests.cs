using System.Linq;

namespace ES.SimTests.Abilities
{
    public class BehaviorTests
    {
        /// <summary>Afinidade inicial (+4%): as armas testadas são afins à especialização do jogador.</summary>
        const float Aff = 1.04f;

        static World Open(string spec = "eletrica")
        {
            var w = TestWorlds.CreateOpen(spec: spec);
            w.SpawningEnabled = false;
            w.Players[0].InvulnUntil = float.MaxValue;
            return w;
        }

        /// <summary>Prensa (70 de vida) parada no lugar.</summary>
        static int Dummy(World w, Vec2 offset)
        {
            int e = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("prensa_ambulante"), w.Players[0].Pos + offset, false);
            w.Enemies.StunUntil[e] = float.MaxValue;
            return e;
        }

        static float HpOfId(World w, ushort id)
        {
            for (int i = 0; i < w.Enemies.Count; i++) if (w.Enemies.Id[i] == id) return w.Enemies.Hp[i];
            return -1;
        }

        static AbilityInstance Find(World w, string id) =>
            w.Players[0].Abilities.First(a => a.AbilityIndex == w.Db.AbilityIndex(id));

        [Test]
        public void Eletrica_StartsWithDescarga()
        {
            var w = Open();
            Assert.That(w.Players[0].Abilities.Select(a => w.Db.Ability(a.AbilityIndex).Id), Is.EquivalentTo(new[] { "descarga_direcionada" }));
        }

        [Test]
        public void Chain_HitsNearestThenJumpsWithFalloff()
        {
            var w = Open();
            ushort a = w.Enemies.Id[Dummy(w, new Vec2(3, 0))];
            ushort b = w.Enemies.Id[Dummy(w, new Vec2(5.5f, 0))];
            ushort c = w.Enemies.Id[Dummy(w, new Vec2(8, 0))];
            w.Step(TestWorlds.NoInput());
            Assert.That(70 - HpOfId(w, a), Is.EqualTo(14f * Aff).Within(1e-3));
            Assert.That(70 - HpOfId(w, b), Is.EqualTo(11.9f * Aff).Within(1e-3));
            Assert.That(70 - HpOfId(w, c), Is.EqualTo(10.115f * Aff).Within(1e-3));
            Assert.That(w.Events.Cosmetic.Count(x => x.Kind == CosmeticKind.ChainArc), Is.EqualTo(3));
        }

        [Test]
        public void Chain_RespectsChainRange()
        {
            var w = Open();
            ushort a = w.Enemies.Id[Dummy(w, new Vec2(3, 0))];
            ushort b = w.Enemies.Id[Dummy(w, new Vec2(7, 0))];
            w.Step(TestWorlds.NoInput());
            Assert.That(HpOfId(w, a), Is.LessThan(70));
            Assert.That(HpOfId(w, b), Is.EqualTo(70));
        }

        [Test]
        public void Chain_CooldownFromStats_Bateria2()
        {
            var w = Open();
            var p = w.Players[0];
            var bat = AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("bateria_portatil"));
            AbilitySystem.LevelUp(w, 0, bat);
            var d = Find(w, "descarga_direcionada");
            float cd = StatQuery.Ability(p.Stats, d.Stats, d.AbilityIndex, w.Db.Ability(d.AbilityIndex).Tags, StatId.Cooldown);
            Assert.That(cd, Is.EqualTo(0.88f).Within(1e-4));
        }

        [Test]
        public void Orbit_HitIntervalPerTarget()
        {
            var w = Open("mecanica");
            var arm = Find(w, "braco_giratorio");
            arm.Stats.SetBase(StatId.Duration, 10000f);
            arm.Stats.SetBase(StatId.Knockback, 0f);
            ushort id = w.Enemies.Id[Dummy(w, new Vec2(1.8f, 0))];
            for (int t = 0; t < 57; t++) w.Step(TestWorlds.NoInput()); // 1,9 s
            Assert.That(70 - HpOfId(w, id), Is.EqualTo(4 * 12f * Aff).Within(1e-3));
        }

        [Test]
        public void Orbit_SweepHitsOncePerRevolution()
        {
            var w = Open("mecanica");
            Find(w, "braco_giratorio").Stats.SetBase(StatId.Knockback, 0f);
            ushort id = w.Enemies.Id[Dummy(w, new Vec2(0, 1.8f))];
            for (int t = 0; t < 84; t++) w.Step(TestWorlds.NoInput()); // 2,8 s = 2 voltas
            Assert.That(70 - HpOfId(w, id), Is.EqualTo(2 * 12f * Aff).Within(1e-3));
        }

        [Test]
        public void Orbit_ArmsIncreaseAtLevels3And5()
        {
            var w = Open("mecanica");
            var arm = Find(w, "braco_giratorio");
            AbilitySystem.LevelUp(w, 0, arm);
            AbilitySystem.LevelUp(w, 0, arm);
            Assert.That(arm.Stats.Get(StatId.Count), Is.EqualTo(2));
            AbilitySystem.LevelUp(w, 0, arm);
            AbilitySystem.LevelUp(w, 0, arm);
            Assert.That(arm.Level, Is.EqualTo(5));
            Assert.That(arm.Stats.Get(StatId.Count), Is.EqualTo(3));
        }

        [Test]
        public void Projectile_PierceCount()
        {
            var w = TestWorlds.Disarm(Open());
            AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("laser_industrial"));
            var ids = Enumerable.Range(0, 5).Select(k => w.Enemies.Id[Dummy(w, new Vec2(2 + k * 1.5f, 0))]).ToArray();
            for (int t = 0; t < 30; t++) w.Step(TestWorlds.NoInput());
            Assert.That(ids.Count(id => HpOfId(w, id) < 70), Is.EqualTo(3));
        }

        [Test]
        public void Aura_TicksAndSlows()
        {
            var w = TestWorlds.Disarm(Open());
            AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("campo_eletromagnetico"));
            int e = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("prensa_ambulante"), w.Players[0].Pos + new Vec2(1.5f, 0), false);
            ushort id = w.Enemies.Id[e];
            for (int t = 0; t < 30; t++) w.Step(TestWorlds.NoInput());
            Assert.That(70 - HpOfId(w, id), Is.EqualTo(12f * Aff).Within(1e-3));
            int i = Enumerable.Range(0, w.Enemies.Count).First(k => w.Enemies.Id[k] == id);
            Assert.That(w.Enemies.SlowAmount[i], Is.EqualTo(0.15f).Within(1e-4));
            Assert.That(w.Enemies.SlowUntil[i], Is.GreaterThan(w.Elapsed));
        }

        [Test]
        public void Passive_TurbinaAddsMoveSpeed()
        {
            var w = Open("mecanica");
            var t = AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("turbina"));
            AbilitySystem.LevelUp(w, 0, t);
            AbilitySystem.LevelUp(w, 0, t);
            Assert.That(w.Players[0].Stats.Get(StatId.MoveSpeed), Is.EqualTo(4.5f * 1.18f).Within(1e-4));
        }

        [Test]
        public void Passive_LevelUp_ReplacesModifierNotStacks()
        {
            var w = Open();
            var p = w.Players[0];
            var bat = AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("bateria_portatil"));
            AbilitySystem.LevelUp(w, 0, bat);
            int mine = Enumerable.Range(0, p.Stats.ModifierCount).Count(i => p.Stats.ModifierAt(i).SourceId == bat.SourceId);
            Assert.That(mine, Is.EqualTo(1));
        }

        [Test]
        public void Attribute_MaxHp_RaisesCurrentHpByDelta()
        {
            var w = Open();
            var p = w.Players[0];
            p.Hp = 50;
            AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("attr_vida"));
            Assert.That(p.Stats.Get(StatId.MaxHp), Is.EqualTo(115));
            Assert.That(p.Hp, Is.EqualTo(65));
        }

        [Test]
        public void Survival_Kit_HealsWithoutOccupyingSlot()
        {
            var w = Open();
            var p = w.Players[0];
            p.Hp = 10;
            var inst = AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("kit_primeiros_socorros"));
            Assert.That(inst, Is.Null);
            Assert.That(p.Hp, Is.EqualTo(40).Within(1e-3));
            Assert.That(p.Abilities.Count, Is.EqualTo(1));
        }
    }
}
