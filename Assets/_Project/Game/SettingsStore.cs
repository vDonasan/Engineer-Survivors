using System;
using UnityEngine;

namespace ES.Game
{
    public enum EffectsLevel { Low, Medium, High }

    /// <summary>Configurações do jogador, salvas no navegador (PlayerPrefs → IndexedDB no WebGL).</summary>
    [Serializable]
    public sealed class SettingsStore
    {
        public const string Key = "es.settings.v1";
        public const string DefaultSpecialBinding = "<Keyboard>/space";

        public float Master = 1f, Music = 0.6f, Sfx = 0.9f, Shake = 0.7f, UiScale = 1f;
        public EffectsLevel Effects = EffectsLevel.High;
        public string SpecialBinding = DefaultSpecialBinding;
        public bool Fullscreen;
        public string PlayerName = "";

        public event Action Changed;

        public void Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, this); }
                catch (Exception e) { Debug.LogWarning("[ES] Configurações inválidas, usando padrões: " + e.Message); }
            }
            Clamp();
        }

        public void Save()
        {
            Clamp();
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        void Clamp()
        {
            Master = Mathf.Clamp01(Master);
            Music = Mathf.Clamp01(Music);
            Sfx = Mathf.Clamp01(Sfx);
            Shake = Mathf.Clamp01(Shake);
            UiScale = Mathf.Clamp(UiScale, 0.8f, 1.3f);
            if (!Enum.IsDefined(typeof(EffectsLevel), Effects)) Effects = EffectsLevel.High;
            if (string.IsNullOrEmpty(SpecialBinding) || !SpecialBinding.StartsWith("<")) SpecialBinding = DefaultSpecialBinding;
            if (PlayerName == null) PlayerName = "";
            if (PlayerName.Length > 16) PlayerName = PlayerName.Substring(0, 16);
        }
    }
}
