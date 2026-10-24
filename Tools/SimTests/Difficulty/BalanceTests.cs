using System;
using System.Linq;
using static ES.Sim.Difficulty;

namespace ES.SimTests.Difficulty
{
    public class BalanceTests
    {
        static BalanceDef Def => ContentLoader.LoadSp1().balance;
        static MatchBalance R(ES.Sim.Difficulty d, int n) => BalanceResolver.Resolve(Def, d, n);
        static MatchBalance R(BalanceDef def, ES.Sim.Difficulty d, int n) => BalanceResolver.Resolve(def, d, n);

        [Test]
        public void Resolve_All12Cells_Exist()
        {
            foreach (ES.Sim.Difficulty d in Enum.GetValues(typeof(ES.Sim.Difficulty)))
                for (int n = 1; n <= 4; n++)
                {
                    var b = R(d, n);
                    Assert.That(b.Difficulty, Is.EqualTo(d));
                    Assert.That(b.TeamSize, Is.EqualTo(n));
                }
        }

        [Test]
        public void EnemyDamage_HardGtNormalGtEasy()
        {
            for (int n = 1; n <= 4; n++)
            {
                float e = R(Easy, n).EnemyDamage, m = R(Normal, n).EnemyDamage, h = R(Hard, n).EnemyDamage;
                Assert.That(h > m && m > e, $"equipe {n}: {e} {m} {h}");
            }
        }

        [Test]
        public void EnemyHpPerPlayer_IsSublinear()
        {
            foreach (ES.Sim.Difficulty d in Enum.GetValues(typeof(ES.Sim.Difficulty)))
                for (int n = 2; n <= 4; n++)
                    Assert.That(R(d, n).EnemyHp / n, Is.LessThan(R(d, n - 1).EnemyHp / (n - 1)));
        }

        [Test]
        public void Spec_Normal4_Values()
        {
            var b = R(Normal, 4);
            Assert.That(b.EnemyCount, Is.EqualTo(2.2f).Within(1e-5));
            Assert.That(b.EnemyHp, Is.EqualTo(1.75f).Within(1e-5));
            Assert.That(b.EliteChance, Is.EqualTo(0.025f + 0.03f).Within(1e-5));
            Assert.That(b.Xp, Is.EqualTo(0.55f).Within(1e-5));
        }

        [Test]
        public void Spec_Hard1_Values()
        {
            var b = R(Hard, 1);
            Assert.That(b.EnemyDamage, Is.EqualTo(1.35f).Within(1e-5));
            Assert.That(b.EnemyHp, Is.EqualTo(1.1f).Within(1e-5));
            Assert.That(b.CompositionShiftMinutes, Is.EqualTo(1.5f).Within(1e-5));
            Assert.That(b.HealDropChance, Is.EqualTo(0.005f).Within(1e-6));
            Assert.That(b.Telegraph, Is.EqualTo(0.8f).Within(1e-5));
        }

        [Test]
        public void CellOverride_MultAndAdd()
        {
            var def = Def;
            var before = R(def, Hard, 3);
            def.overrides = new[]
            {
                new CellOverrideDef { difficulty = "hard", teamSize = 3, param = "enemyHp", value = 1.1f },
                new CellOverrideDef { difficulty = "hard", teamSize = 3, param = "eliteChance", value = 0.01f },
            };
            var after = R(def, Hard, 3);
            Assert.That(after.EnemyHp, Is.EqualTo(before.EnemyHp * 1.1f).Within(1e-5));
            Assert.That(after.EliteChance, Is.EqualTo(before.EliteChance + 0.01f).Within(1e-6));
            Assert.That(R(def, Hard, 2).EnemyHp, Is.EqualTo(R(Hard, 2).EnemyHp), "outras células intactas");
        }

        [Test]
        public void TeamSizeOutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => R(Normal, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => R(Normal, 0));
        }

        [Test]
        public void Build_UnknownOverrideParam_ReportsError()
        {
            var d = ContentLoader.LoadSp1();
            d.balance.overrides = new[] { new CellOverrideDef { difficulty = "hard", teamSize = 2, param = "bossRage", value = 2 } };
            var r = GameDatabase.Build(d);
            Assert.That(r.Errors, Has.Some.Contains("bossRage"));
        }
    }
}
