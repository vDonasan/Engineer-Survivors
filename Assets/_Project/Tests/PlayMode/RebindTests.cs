using System.Collections;
using ES.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace ES.Tests
{
    public class RebindTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator Settings_SpecialRebind_Persists()
        {
            string saved = PlayerPrefs.GetString(SettingsStore.Key, null);
            var kb = InputSystem.AddDevice<Keyboard>();
            try
            {
                var settings = new SettingsStore();
                settings.Load();
                string got = null;
                using (var input = new GameInput(settings.SpecialBinding))
                {
                    input.RebindSpecial(path => { got = path; settings.SpecialBinding = path; settings.Save(); });
                    Assert.That(input.Rebinding, Is.True);
                    yield return null;
                    Press(kb.fKey);
                    currentTime += 0.2;
                    InputSystem.Update();
                    yield return null;
                    Release(kb.fKey);
                    yield return null;
                    Assert.That(got, Is.EqualTo("<Keyboard>/f"));
                    Assert.That(input.Rebinding, Is.False);
                }
                var reloaded = new SettingsStore();
                reloaded.Load();
                Assert.That(reloaded.SpecialBinding, Is.EqualTo("<Keyboard>/f"));
                using (var again = new GameInput(reloaded.SpecialBinding))
                {
                    Assert.That(again.SpecialPath, Is.EqualTo("<Keyboard>/f"));
                    Assert.That(again.Special.controls, Does.Contain(kb.fKey), "a ação do especial passa a ouvir a tecla F");
                }
            }
            finally
            {
                if (saved == null) PlayerPrefs.DeleteKey(SettingsStore.Key);
                else PlayerPrefs.SetString(SettingsStore.Key, saved);
            }
        }
    }
}
