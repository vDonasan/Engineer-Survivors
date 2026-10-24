using System.Linq;

namespace ES.SimTests.Abilities
{
    public class SpecialTests
    {
        static World Open(string spec)
        {
            var w = TestWorlds.Disarm(TestWorlds.CreateOpen(spec: spec));
            w.SpawningEnabled = false;
            w.Players[0].InvulnUntil = float.MaxValue;
            return w;
        }

        static int Still(World w, Vec2 offset)
        {
            int e = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("prensa_ambulante"), w.Players[0].Pos + offset, false);
            w.Enemies.Speed[e] = 0;
            return e;
        }

        static int IndexOf(World w, ushort id) => Enumerable.Range(0, w.Enemies.Count).First(i => w.Enemies.Id[i] == id);

        static PlayerInput[] Press(byte counter)
        {
            var inputs = TestWorlds.NoInput();
            inputs[0].SpecialCounter = counter;
            return inputs;
        }

        static bool Activated(World w) => w.Events.Rule.Any(r => r.Kind == RuleEventKind.SpecialActivated && r.A == 0);

        [Test]
        public void Blackout_DamagesAndStunsMachinesInRadius()
        {
            var w = Open("eletrica");
            ushort near = w.Enemies.Id[Still(w, new Vec2(5, 0))];
            ushort far = w.Enemies.Id[Still(w, new Vec2(7, 0))];
            w.Step(Press(1));
            Assert.That(Activated(w), Is.True);
            float t0 = w.Elapsed;
            for (int t = 0; t < 15; t++) w.Step(Press(1));
            int n = IndexOf(w, near), f = IndexOf(w, far);
            Assert.That(w.Enemies.Hp[n], Is.EqualTo(35f).Within(1e-3));
            Assert.That(w.Enemies.StunUntil[n], Is.GreaterThan(t0 + 1.9f));
            Assert.That(w.Enemies.Hp[f], Is.EqualTo(70f));
            Assert.That(w.Enemies.StunUntil[f], Is.EqualTo(0));
        }

        [Test]
        public void Blackout_HitsEachEnemyOncePerActivation()
        {
            var w = Open("eletrica");
            ushort id = w.Enemies.Id[Still(w, new Vec2(2, 0))];
            w.Step(Press(1));
            for (int t = 0; t < 30; t++) w.Step(Press(1));
            Assert.That(w.Enemies.Hp[IndexOf(w, id)], Is.EqualTo(35f).Within(1e-3));
        }

        [Test]
        public void Blackout_ClearsHostileProjectiles()
        {
            var w = Open("eletrica");
            w.Hostile.Spawn(0, w.Players[0].Pos + new Vec2(3, 0), new Vec2(0.1f, 0), 8, 0.2f, 10);
            w.Step(Press(1));
            for (int t = 0; t < 15; t++) w.Step(Press(1));
            Assert.That(w.Hostile.Count, Is.EqualTo(0));
        }

        [Test]
        public void Special_RespectsCooldown()
        {
            var w = Open("eletrica");
            w.Step(Press(1));
            Assert.That(Activated(w), Is.True);
            while (w.Elapsed < 10f) w.Step(Press(1));
            w.Step(Press(2));
            Assert.That(Activated(w), Is.False, "pedido durante a recarga é ignorado");
            while (w.Elapsed < 22.2f) { w.Step(Press(2)); Assert.That(Activated(w), Is.False); }
            w.Step(Press(3));
            Assert.That(Activated(w), Is.True);
        }

        [Test]
        public void Special_CounterRegressionIgnored()
        {
            var w = Open("eletrica");
            w.Step(Press(5));
            Assert.That(Activated(w), Is.True);
            while (w.Elapsed < 23f) w.Step(Press(5));
            w.Step(Press(3));
            Assert.That(Activated(w), Is.False);
        }

        [Test]
        public void Special_LostPacket_NextCounterStillActivatesOnce()
        {
            var w = Open("eletrica");
            w.Step(Press(1));
            while (w.Elapsed < 23f) w.Step(Press(1));
            int activations = 0;
            for (int t = 0; t < 10; t++) { w.Step(Press(3)); if (Activated(w)) activations++; }
            Assert.That(activations, Is.EqualTo(1));
        }

        [Test]
        public void Turbine_BuffsAndExpires()
        {
            var w = Open("mecanica");
            var p = w.Players[0];
            w.Step(Press(1));
            Assert.That(p.Stats.Get(StatId.MoveSpeed), Is.EqualTo(4.5f * 1.45f).Within(1e-4));
            Assert.That(p.Stats.Get(StatId.DamageTaken), Is.EqualTo(0.7f).Within(1e-4));
            Assert.That(p.SpecialActiveLeft, Is.GreaterThan(5.9f));
            while (w.Elapsed < 6.2f) w.Step(Press(1));
            Assert.That(p.Stats.Get(StatId.MoveSpeed), Is.EqualTo(4.5f).Within(1e-4));
            Assert.That(p.Stats.Get(StatId.DamageTaken), Is.EqualTo(1f).Within(1e-4));
            Assert.That(p.SpecialActiveLeft, Is.EqualTo(0));
        }

        [Test]
        public void Turbine_RamDamagesOnContact()
        {
            var w = Open("mecanica");
            ushort id = w.Enemies.Id[Still(w, new Vec2(0.5f, 0))];
            w.Step(Press(1));
            // 15 de atropelamento × 1,3 (o +30% físico da própria Turbina vale para o atropelamento).
            Assert.That(70 - w.Enemies.Hp[IndexOf(w, id)], Is.EqualTo(15f * 1.3f).Within(1e-3));
        }
    }
}
