namespace ES.SimTests.Stats
{
    public class StatTests
    {
        static Modifier Add(StatId s, float v, int src = 1, ModScope scope = default) =>
            new Modifier { Stat = s, Op = ModOp.Add, Value = v, SourceId = src, Scope = scope };

        static Modifier Mul(StatId s, float v, int src = 1, ModScope scope = default) =>
            new Modifier { Stat = s, Op = ModOp.Mult, Value = v, SourceId = src, Scope = scope };

        [Test]
        public void StatBlock_Formula_AddThenMultiplyProduct()
        {
            var s = new StatBlock();
            s.SetBase(StatId.Damage, 10);
            s.Add(Add(StatId.Damage, 2));
            s.Add(Mul(StatId.Damage, 0.5f));
            s.Add(Mul(StatId.Damage, 0.1f));
            Assert.That(s.Get(StatId.Damage), Is.EqualTo(12 * 1.5f * 1.1f).Within(1e-4));
        }

        [Test]
        public void StatBlock_OtherStatsUnaffected()
        {
            var s = new StatBlock();
            s.SetBase(StatId.MoveSpeed, 4.5f);
            s.Add(Mul(StatId.Damage, 1f));
            Assert.That(s.Get(StatId.MoveSpeed), Is.EqualTo(4.5f));
        }

        [Test]
        public void StatBlock_RemoveSource_RemovesOnlyThatSource()
        {
            var s = new StatBlock();
            s.SetBase(StatId.MaxHp, 100);
            s.Add(Add(StatId.MaxHp, 15, src: 1));
            s.Add(Add(StatId.MaxHp, 30, src: 2));
            Assert.That(s.RemoveSource(1), Is.EqualTo(1));
            Assert.That(s.Get(StatId.MaxHp), Is.EqualTo(130));
            Assert.That(s.RemoveSource(1), Is.EqualTo(0));
        }

        [Test]
        public void StatBlock_PlayerGet_IgnoresAbilityScopedModifiers()
        {
            var s = new StatBlock();
            s.SetBase(StatId.Damage, 1);
            s.Add(Mul(StatId.Damage, 0.3f, scope: ModScope.ForTag(Tag.Mecanica)));
            Assert.That(s.Get(StatId.Damage), Is.EqualTo(1));
        }

        [Test]
        public void StatQuery_TagScope_AppliesOnlyToMatchingAbility()
        {
            var player = new StatBlock();
            player.Add(Mul(StatId.Cooldown, -0.06f, scope: ModScope.ForTag(Tag.Energia)));
            var ability = new StatBlock();
            ability.SetBase(StatId.Cooldown, 1f);

            float energia = StatQuery.Ability(player, ability, 0, TagSet.Of(Tag.Energia, Tag.Eletronica), StatId.Cooldown);
            float mecanica = StatQuery.Ability(player, ability, 0, TagSet.Of(Tag.Mecanica), StatId.Cooldown);

            Assert.That(energia, Is.EqualTo(0.94f).Within(1e-5));
            Assert.That(mecanica, Is.EqualTo(1f).Within(1e-5));
        }

        [Test]
        public void StatQuery_AbilityScope_MatchesIndexOnly()
        {
            var player = new StatBlock();
            player.Add(Mul(StatId.Damage, 0.04f, scope: ModScope.ForAbility(3)));
            var ability = new StatBlock();
            ability.SetBase(StatId.Damage, 100);
            Assert.That(StatQuery.Ability(player, ability, 3, default, StatId.Damage), Is.EqualTo(104).Within(1e-3));
            Assert.That(StatQuery.Ability(player, ability, 2, default, StatId.Damage), Is.EqualTo(100).Within(1e-3));
        }

        [Test]
        public void StatQuery_AllAbilities_CombinesWithAbilityOwnModifiers()
        {
            var player = new StatBlock();
            player.Add(Mul(StatId.Damage, 0.08f, scope: ModScope.AllAbilities));
            var ability = new StatBlock();
            ability.SetBase(StatId.Damage, 10);
            ability.Add(Add(StatId.Damage, 5));
            Assert.That(StatQuery.Ability(player, ability, 0, default, StatId.Damage), Is.EqualTo(15 * 1.08f).Within(1e-4));
        }

        [Test]
        public void TagSet_OverlapsAndCount()
        {
            var t = TagSet.Of(Tag.Energia, Tag.Termico);
            Assert.That(t.Overlaps(TagSet.Of(Tag.Termico)), Is.True);
            Assert.That(t.Overlaps(TagSet.Of(Tag.Mecanica)), Is.False);
            Assert.That(t.Has(Tag.Energia), Is.True);
            Assert.That(t.Count, Is.EqualTo(2));
        }

        [Test]
        public void Tags_TryParse_IgnoresCaseAndAccents()
        {
            Assert.That(Tags.TryParse("energia", out var t), Is.True);
            Assert.That(t, Is.EqualTo(Tag.Energia));
            Assert.That(Tags.TryParse("Eletrônica", out var e), Is.True);
            Assert.That(e, Is.EqualTo(Tag.Eletronica));
            Assert.That(Tags.TryParse("plasma", out _), Is.False);
        }
    }
}
