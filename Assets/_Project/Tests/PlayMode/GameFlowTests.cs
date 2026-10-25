using System.Collections;
using System.Linq;
using ES.Content;
using ES.Game;
using ES.Net;
using ES.Sim;
using ES.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES.Tests
{
    /// <summary>O jogo inteiro (cena Main montada em código): menu → solo → HUD → subida de nível.</summary>
    public class GameFlowTests
    {
        GameBootstrap _boot;
        string _savedPrefs;

        UiRouter Router => _boot.Router;
        NetGame Net => _boot.Net;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savedPrefs = PlayerPrefs.GetString(SettingsStore.Key, null);
            PlayerPrefs.DeleteKey(SettingsStore.Key);
            new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            var go = new GameObject("Game");
            go.SetActive(false);
            _boot = go.AddComponent<GameBootstrap>();
#if UNITY_EDITOR
            _boot.content = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Project/Content/ContentDatabase.asset");
            _boot.assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Game/GameAssets.asset");
            _boot.ui = AssetDatabase.LoadAssetAtPath<UiAssets>("Assets/_Project/UI/UiAssets.asset");
#endif
            go.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(_boot.FatalError, Is.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Net != null)
            {
                var leave = Net.LeaveAsync();
                while (!leave.IsCompleted) yield return null;
            }
            foreach (var name in new[] { "Game", "NetGame", "NetworkManager", "Audio", "Main Camera", "Match" })
            {
                var g = GameObject.Find(name);
                while (g != null)
                {
                    Object.DestroyImmediate(g);
                    g = GameObject.Find(name);
                }
            }
            if (_savedPrefs == null) PlayerPrefs.DeleteKey(SettingsStore.Key);
            else PlayerPrefs.SetString(SettingsStore.Key, _savedPrefs);
            yield return null;
        }

        void Click(string name)
        {
            var b = Router.RootElement.Q<Button>(name);
            Assert.That(b, Is.Not.Null, name);
            Assert.That(b.enabledInHierarchy, Is.True, name + " desabilitado");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Espera pela condição em tempo real (no batchmode a Unity roda a centenas de FPS).</summary>
        static IEnumerator Until(System.Func<bool> condition, float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < seconds) yield return null;
        }

        IEnumerator StartSoloFromMenu()
        {
            Assert.That(Router.Menu.Visible, Is.True);
            Click("btn-solo");
            yield return null;
            Assert.That(Router.Solo.Visible, Is.True);
            Click("btn-solo-start");
            yield return Until(() => Router.Hud.Visible && Net.Feed != null && Net.Feed.Tick > 0, 3f);
        }

        [UnityTest]
        public IEnumerator EveryScreen_BindsAllItsButtons()
        {
            foreach (var s in Router.Screens) Assert.That(s.Bound, Is.SupersetOf(s.Buttons), s.Uxml);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Menu_Solo_Start_ReachesHud()
        {
            yield return StartSoloFromMenu();
            Assert.That(Net.Mode, Is.EqualTo(NetMode.Solo));
            Assert.That(Net.Started, Is.True);
            Assert.That(Router.Hud.Visible, Is.True);
            Assert.That(_boot.Match.Active, Is.True);
            Assert.That(Router.RootElement.Q<Label>("label-time").text, Does.Contain(":"));
        }

        [UnityTest]
        public IEnumerator Solo_StartsWithoutSession_NoNetworkListening()
        {
            yield return StartSoloFromMenu();
            Assert.That(Net.RoomCode, Is.Null.Or.Empty);
            var nm = Unity.Netcode.NetworkManager.Singleton;
            Assert.That(nm == null || !nm.IsListening, Is.True, "o solo não abre transporte");
            float t0 = Net.Feed.Elapsed;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(Net.Feed.Elapsed, Is.GreaterThan(t0 + 0.3f), "a simulação avança no solo");
        }

        IEnumerator LevelUp()
        {
            yield return StartSoloFromMenu();
            ProgressionSystem.AddSharedXp(Net.Host.World, 10);
            yield return Until(() => Router.LevelUp.Visible, 3f);
            Assert.That(Router.LevelUp.Visible, Is.True, "painel de subida de nível");
        }

        [UnityTest]
        public IEnumerator Solo_LevelUp_PausesSimulation()
        {
            yield return LevelUp();
            int tick = Net.Feed.Tick;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(Net.Feed.Tick, Is.EqualTo(tick), "o solo pausa durante a escolha");
        }

        [UnityTest]
        public IEnumerator LevelUp_CardsShowNameRarityTags()
        {
            yield return LevelUp();
            var card = Router.RootElement.Q<Button>("card-0");
            Assert.That(card.Q<Label>("card-name").text, Is.Not.Empty);
            Assert.That(new[] { "Comum", "Incomum", "Raro" }, Does.Contain(card.Q<Label>("card-rarity").text));
            Assert.That(card.Q<Label>("card-kind").text, Does.StartWith("[1]"));
        }

        [UnityTest]
        public IEnumerator Solo_ChoiceUpdatesHud()
        {
            yield return LevelUp();
            var offer = Net.Feed.CurrentOffer;
            int pick = System.Array.FindIndex(offer.Options, o => o.Kind != OptionKind.Survival);
            Assume.That(pick, Is.GreaterThanOrEqualTo(0));
            var before = Net.Feed.Loadouts.Slots[0].Abilities.Select(a => (a.Ability, a.Level)).ToList();
            Click("card-" + pick);
            yield return Until(() => !Router.LevelUp.Visible, 3f);
            yield return Frames(2);
            Assert.That(Router.LevelUp.Visible, Is.False);
            var after = Net.Feed.Loadouts.Slots[0].Abilities.Select(a => (a.Ability, a.Level)).ToList();
            Assert.That(after, Is.Not.EqualTo(before));
            Assert.That(Router.RootElement.Q<VisualElement>("list-abilities").childCount, Is.EqualTo(after.Count));
        }

        [UnityTest]
        public IEnumerator Pause_FreezesSolo_AndQuitReturnsToMenu()
        {
            yield return StartSoloFromMenu();
            Click("btn-hud-menu");
            yield return null;
            Assert.That(Router.Pause.Visible, Is.True);
            int tick = Net.Feed.Tick;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(Net.Feed.Tick, Is.EqualTo(tick));
            Click("btn-quit");
            yield return Until(() => Router.Menu.Visible, 3f);
            Assert.That(Router.Menu.Visible, Is.True);
            Assert.That(Net.Mode, Is.EqualTo(NetMode.None));
            yield return Frames(2);
            Assert.That(_boot.Match.Active, Is.False);
        }

        [UnityTest]
        public IEnumerator MatchEnd_ShowsResultWithDamage()
        {
            yield return StartSoloFromMenu();
            var w = Net.Host.World;
            yield return new WaitForSecondsRealtime(1.5f);
            w.Players[0].InvulnUntil = 0;
            CombatSystem.DamagePlayer(w, 0, 100000);
            yield return Until(() => Router.Result.Visible, 8f);
            Assert.That(Router.Result.Visible, Is.True);
            Assert.That(Router.RootElement.Q<Label>("label-result-title").text, Does.Contain("DERROTA"));
        }

        [UnityTest]
        public IEnumerator Perf_400Enemies_RecordsFrameTime()
        {
            yield return StartSoloFromMenu();
            Net.Host.World.Players[0].InvulnUntil = float.MaxValue;
            int spawned = 0;
            for (int i = 0; i < 8; i++) spawned += _boot.Match.DebugSpawn(50);
            yield return Frames(10);
            float t0 = Time.realtimeSinceStartup;
            int frames = 0;
            while (Time.realtimeSinceStartup - t0 < 2f) { frames++; yield return null; }
            float fps = frames / (Time.realtimeSinceStartup - t0);
            Debug.Log($"[ES] Perf editor (batchmode): {Net.Feed.Tracks.EnemyCount} inimigos, {fps:0} FPS (aviso apenas; a medição oficial é no navegador)");
            if (fps < 45) Debug.LogWarning($"[ES] Abaixo de 45 FPS no editor: {fps:0}");
            Assert.That(spawned, Is.GreaterThan(300));
            Assert.That(Net.Feed.Tracks.EnemyCount, Is.GreaterThan(300));
        }
    }
}
