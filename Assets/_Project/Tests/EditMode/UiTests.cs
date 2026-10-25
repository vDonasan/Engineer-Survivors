using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ES.Content;
using ES.Sim;
using ES.UI;
using NUnit.Framework;
using UnityEditor;

namespace ES.Tests
{
    public class UiTests
    {
        const string UiDir = "Assets/_Project/UI";
        static GameDatabase _db;

        static GameDatabase Db => _db ??= AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Project/Content/ContentDatabase.asset").Build().Db;

        [OneTimeSetUp]
        public void LoadStrings() => Strings.Load(File.ReadAllText(UiDir + "/Strings/pt-BR.json"));

        static IEnumerable<UiScreen> AllScreens() =>
            typeof(UiScreen).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(UiScreen)) && !t.IsAbstract)
                .Select(t => (UiScreen)Activator.CreateInstance(t));

        static HashSet<string> UxmlButtons(string uxml)
        {
            var text = File.ReadAllText($"{UiDir}/Uxml/{uxml}.uxml");
            return new HashSet<string>(Regex.Matches(text, "<ui:Button[^>]*\\bname=\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value));
        }

        [Test]
        public void AllUxmlButtons_HaveHandlers()
        {
            var screens = AllScreens().ToList();
            Assert.That(screens.Count, Is.GreaterThanOrEqualTo(11));
            var covered = new HashSet<string>();
            foreach (var s in screens)
            {
                var inFile = UxmlButtons(s.Uxml);
                covered.Add(s.Uxml);
                Assert.That(s.Buttons, Is.EquivalentTo(inFile), $"{s.GetType().Name} ({s.Uxml}.uxml)");
            }
            foreach (var file in Directory.GetFiles(UiDir + "/Uxml", "*.uxml"))
                Assert.That(covered, Does.Contain(Path.GetFileNameWithoutExtension(file)), "UXML sem controlador: " + file);
        }

        [Test]
        public void AllStringKeysExist()
        {
            var keys = new HashSet<string>();
            foreach (var f in Directory.GetFiles(UiDir + "/Uxml", "*.uxml"))
                foreach (Match m in Regex.Matches(File.ReadAllText(f), "(?:text|label)=\"#([a-z0-9_]+)\"")) keys.Add(m.Groups[1].Value);
            foreach (var dir in new[] { UiDir, "Assets/_Project/Game", "Assets/_Project/Net" })
                foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    var text = File.ReadAllText(f);
                    foreach (Match m in Regex.Matches(text, "Strings\\.(?:Get|Format|Has)\\(\"([a-z0-9_]+)\"\\s*[,)]")) keys.Add(m.Groups[1].Value);
                    foreach (Match m in Regex.Matches(text, "\"(erro_[a-z_]+)\"")) keys.Add(m.Groups[1].Value);
                }
            foreach (var d in new[] { "easy", "normal", "hard" }) { keys.Add("dificuldade_" + d); keys.Add("dificuldade_desc_" + d); }
            for (int i = 0; i < Db.AbilityCount; i++) keys.Add("raridade_" + Db.Ability(i).Def.rarity.ToLowerInvariant());
            keys.Add("pausa_titulo");
            keys.Add("menu_coop_titulo");
            Assert.That(keys.Count, Is.GreaterThan(60));
            var missing = keys.Where(k => !Strings.Has(k)).ToList();
            Assert.That(missing, Is.Empty, string.Join(", ", missing));
        }

        [Test]
        public void ErrorMessages_MatchSpec()
        {
            Assert.That(Strings.Get("erro_codigo_invalido"), Is.EqualTo("Código inválido ou sala expirada."));
            Assert.That(Strings.Get("erro_sala_cheia"), Is.EqualTo("Sala cheia (4/4)."));
            Assert.That(Strings.Get("erro_partida_iniciada"), Is.EqualTo("A partida já começou."));
            Assert.That(Strings.Get("erro_versao"), Is.EqualTo("Versão diferente do anfitrião. Recarregue a página (Ctrl+F5)."));
            Assert.That(Strings.Get("erro_conexao_perdida"), Is.EqualTo("Conexão perdida com o anfitrião."));
            Assert.That(Strings.Get("aviso_host_minimizou"), Is.EqualTo("O anfitrião minimizou o jogo: a partida pode travar."));
        }

        [Test]
        public void Strings_MissingKey_ReturnsBracketed_AndParsesEscapes()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new Regex("Texto ausente: nao_existe"));
            Assert.That(Strings.Get("nao_existe"), Is.EqualTo("[nao_existe]"));
            var d = Strings.ParseFlat("{ \"a\": \"x\\ny\", \"b\": \"\\u00e7\\\"\" }");
            Assert.That(d["a"], Is.EqualTo("x\ny"));
            Assert.That(d["b"], Is.EqualTo("ç\""));
        }

        static LoadoutTracker Eletrica()
        {
            var t = new LoadoutTracker(Db);
            t.Reset(new MatchConfig { Roster = new[] { new PlayerSlot { Name = "A", SpecializationId = "eletrica" } }, TeamSize = 1 });
            return t;
        }

        [Test]
        public void LevelUpCard_ShowsNameRarityTagsAndSynergyHint()
        {
            var info = LevelUpCards.Describe(Db, Eletrica(), 0, new OfferOption { Kind = OptionKind.NewAbility, Ref = (ushort)Db.AbilityIndex("bateria_portatil") });
            Assert.That(info.Name, Is.EqualTo("Bateria Portátil"));
            Assert.That(info.Rarity, Is.EqualTo("Comum"));
            Assert.That(info.Tags, Does.Contain("Energia"));
            Assert.That(info.Hint, Does.Contain("Carga Acumulada"));
            Assert.That(info.Hint, Does.Contain("Pulso Amplificado"));
            Assert.That(info.IconId, Is.EqualTo("icon_bateria_portatil"));
        }

        [Test]
        public void LevelUpCard_UndiscoveredEvolution_ShowsQuestionMarks()
        {
            var info = LevelUpCards.Describe(Db, Eletrica(), 0, new OfferOption { Kind = OptionKind.NewAbility, Ref = (ushort)Db.AbilityIndex("turbina") });
            Assert.That(info.Hint, Does.Contain("???"));
            Assert.That(info.Hint, Does.Not.Contain("Rotor de Impacto"));
        }

        [Test]
        public void LevelUpCard_UpgradeShowsLevelChange()
        {
            int ab = Db.AbilityIndex("descarga_direcionada");
            var info = LevelUpCards.Describe(Db, Eletrica(), 0, new OfferOption { Kind = OptionKind.Upgrade, Ref = (ushort)ab });
            Assert.That(info.Kind, Is.EqualTo("Nível 1 → 2"));
            Assert.That(info.Change, Is.EqualTo(Db.Ability(ab).Levels[0].Text));
        }

        [Test]
        public void PortraitMood_FollowsState()
        {
            Assert.That(PortraitMood.Pick(1f, PlayerStatus.Active, MatchPhase.Victory, 9, 9, 9, false), Is.EqualTo("vitoria"));
            Assert.That(PortraitMood.Pick(0.2f, PlayerStatus.Active, MatchPhase.Running, 9, 9, 9, false), Is.EqualTo("medo"));
            Assert.That(PortraitMood.Pick(0.9f, PlayerStatus.Active, MatchPhase.Running, 9, 9, 1, false), Is.EqualTo("determinacao"));
            Assert.That(PortraitMood.Pick(0f, PlayerStatus.Downed, MatchPhase.Running, 9, 9, 9, false), Is.EqualTo("choro"));
            Assert.That(PortraitMood.Pick(0.5f, PlayerStatus.Active, MatchPhase.Running, 9, 9, 9, false), Is.EqualTo("exaustao"));
        }
    }
}
