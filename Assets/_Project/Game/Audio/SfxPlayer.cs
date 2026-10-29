using UnityEngine;

namespace ES.Game
{
    public enum AudioBus { Music, Sfx, Ui }

    /// <summary>
    /// Efeitos, interface e música com volume por barramento (geral × música/efeitos).
    /// Barramentos em código em vez de AudioMixer: no WebGL o mixer é só parcialmente suportado.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        public const int VoicesPerSound = 4;
        const int Voices = 24;

        GameAssets _assets;
        AudioSource[] _voices;
        AudioBus[] _voiceBus;
        float[] _voiceBase;
        AudioSource _music;
        float _musicBase = 0.55f;
        readonly VoiceLimiter _limiter = new VoiceLimiter(VoicesPerSound, Voices);
        float _master = 1f, _musicVolume = 0.6f, _sfx = 0.9f;

        public static SfxPlayer Instance { get; private set; }

        public static SfxPlayer Create(GameAssets assets, Transform parent = null)
        {
            var go = new GameObject("Audio");
            if (parent != null) go.transform.SetParent(parent, false);
            else DontDestroyOnLoad(go);
            var p = go.AddComponent<SfxPlayer>();
            p._assets = assets;
            p._voices = new AudioSource[Voices];
            p._voiceBus = new AudioBus[Voices];
            p._voiceBase = new float[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                p._voices[i] = s;
            }
            p._music = go.AddComponent<AudioSource>();
            p._music.loop = true;
            p._music.playOnAwake = false;
            Instance = p;
            return p;
        }

        public void SetVolumes(float master, float music, float sfx)
        {
            _master = Mathf.Clamp01(master);
            _musicVolume = Mathf.Clamp01(music);
            _sfx = Mathf.Clamp01(sfx);
            _music.volume = _musicBase * _master * _musicVolume;
            for (int i = 0; i < _voices.Length; i++)
                if (_voices[i].isPlaying) _voices[i].volume = _voiceBase[i] * BusVolume(_voiceBus[i]);
        }

        float BusVolume(AudioBus bus) => _master * (bus == AudioBus.Music ? _musicVolume : _sfx);

        /// <summary>Toca um efeito; ignora se o som não existe ou se já há 4 vozes dele.</summary>
        public bool Play(string id, AudioBus bus = AudioBus.Sfx, float volume = 1f, float pitchJitter = 0.04f)
        {
            var clip = _assets != null ? _assets.Clip(id) : null;
            if (clip == null) return false;
            double now = Time.unscaledTimeAsDouble;
            // ainda carregando (WebGL): pula este disparo em vez de tocar um som vazio
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                return false;
            }
            if (!_limiter.TryAcquire(id, now, clip.length)) return false;
            for (int i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (v.isPlaying) continue;
                v.clip = clip;
                v.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
                _voiceBus[i] = bus;
                _voiceBase[i] = volume;
                v.volume = volume * BusVolume(bus);
                v.Play();
                return true;
            }
            return false;
        }

        public void PlayMusic(string id, float volume = 0.55f)
        {
            var clip = _assets != null ? _assets.Clip(id) : null;
            if (clip == null || (_music.isPlaying && _music.clip == clip)) return;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            _musicBase = volume;
            _music.clip = clip;
            _music.volume = _musicBase * _master * _musicVolume;
            _music.Play();
        }

        public void StopMusic() => _music.Stop();

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
