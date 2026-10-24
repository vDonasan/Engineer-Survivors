using System.Linq;

namespace ES.SimTests.Data
{
    public class DatabaseTests
    {
        [Test]
        public void Build_Sp1Content_HasNoErrors()
        {
            var r = GameDatabase.Build(ContentLoader.LoadSp1());
            Assert.That(r.Errors, Is.Empty, string.Join("\n", r.Errors));
        }

        [Test]
        public void Build_DuplicateAbilityId_ReportsError()
        {
            var d = ContentLoader.LoadSp1();
            var dup = d.abilities.Single(a => a.id == "descarga_direcionada");
            d.abilities = d.abilities.Append(dup).ToArray();
            var r = GameDatabase.Build(d);
            Assert.That(r.Errors, Has.Some.Contains("duplicado: ability descarga_direcionada"));
            Assert.That(r.Db, Is.Null);
        }

        [Test]
        public void Build_EvolutionPointsToMissingAbility_ReportsError()
        {
            var d = ContentLoader.LoadSp1();
            d.evolutions[0].resultAbility = "inexistente";
            var r = GameDatabase.Build(d);
            Assert.That(r.Errors, Has.Some.Contains("inexistente"));
        }

        [Test]
        public void Build_UnknownTag_ReportsError()
        {
            var d = ContentLoader.LoadSp1();
            d.abilities[0].tags = new[] { "plasma" };
            var r = GameDatabase.Build(d);
            Assert.That(r.Errors, Has.Some.Contains("tag desconhecida: plasma"));
        }

        [Test]
        public void Build_UnknownStat_ReportsError()
        {
            var d = ContentLoader.LoadSp1();
            d.abilities[0].baseStats[0].stat = "Mana";
            var r = GameDatabase.Build(d);
            Assert.That(r.Errors, Has.Some.Contains("stat desconhecido: Mana"));
        }

        [Test]
        public void Build_WeaponLevelsMustMatchMaxLevel()
        {
            var d = ContentLoader.LoadSp1();
            var w = d.abilities.Single(a => a.id == "laser_industrial");
            w.levels = w.levels.Take(2).ToArray();
            var r = GameDatabase.Build(d);
            Assert.That(r.Errors, Has.Some.Contains("laser_industrial"));
        }

        [Test]
        public void Hash_IsStable_AndChangesWithValues()
        {
            var d = ContentLoader.LoadSp1();
            ulong h1 = ContentCanonical.Hash(d);
            Assert.That(ContentCanonical.Hash(ContentLoader.LoadSp1()), Is.EqualTo(h1));
            d.enemies[0].hp += 1;
            Assert.That(ContentCanonical.Hash(d), Is.Not.EqualTo(h1));
        }

        [Test]
        public void Hash_NullArrayEqualsEmptyArray()
        {
            var a = ContentLoader.LoadSp1();
            var b = ContentLoader.LoadSp1();
            a.balance.overrides = null;
            b.balance.overrides = new CellOverrideDef[0];
            Assert.That(ContentCanonical.Hash(a), Is.EqualTo(ContentCanonical.Hash(b)));
        }

        [Test]
        public void Sp1_SpecValues_Spot()
        {
            var db = ContentLoader.Sp1Database();
            Assert.That(db.Def.enemies.Single(x => x.id == "prensa_ambulante").hp, Is.EqualTo(70));
            Assert.That(db.Def.rules.matchSeconds, Is.EqualTo(600));
            Assert.That(db.Def.specials.Single(x => x.id == "apagao").cooldown, Is.EqualTo(22));
            Assert.That(db.Def.balance.teams.Single(t => t.size == 4).enemyHp, Is.EqualTo(1.75f));
        }

        [Test]
        public void Resolve_IndicesAndTags()
        {
            var db = ContentLoader.Sp1Database();
            int i = db.AbilityIndex("descarga_direcionada");
            Assert.That(i, Is.GreaterThanOrEqualTo(0));
            var a = db.Ability(i);
            Assert.That(a.Tags.Has(Tag.Energia) && a.Tags.Has(Tag.Eletronica), Is.True);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Weapon));
            Assert.That(a.BaseStats.GetBase(StatId.Damage), Is.EqualTo(14));
            Assert.That(db.AbilityIndex("nao_existe"), Is.EqualTo(-1));

            var spec = db.Specialization(db.SpecializationIndex("eletrica"));
            Assert.That(spec.StartingAbility, Is.EqualTo(i));
            Assert.That(db.Evolution(db.EvolutionIndex("evo_rotor_de_impacto")).Discoverable, Is.True);
        }
    }
}
