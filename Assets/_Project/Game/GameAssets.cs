using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES.Game
{
    /// <summary>
    /// Catálogo de sprites (por ID do manifesto da arte), sons (por nome do arquivo),
    /// fonte e textos. Gerado por "Engineering Survivors/Atualizar catálogo de arte e áudio".
    /// </summary>
    [CreateAssetMenu(menuName = "Engineering Survivors/Catálogo de arte e áudio")]
    public sealed class GameAssets : ScriptableObject
    {
        [Serializable] public struct SpriteEntry { public string id; public Sprite sprite; }
        [Serializable] public struct ClipEntry { public string id; public AudioClip clip; }

        public List<SpriteEntry> sprites = new List<SpriteEntry>();
        public List<ClipEntry> clips = new List<ClipEntry>();
        public Font font;
        public TextAsset strings;

        Dictionary<string, Sprite> _sprites;
        Dictionary<string, AudioClip> _clips;

        void Ensure()
        {
            if (_sprites != null) return;
            _sprites = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var e in sprites) if (e.sprite != null) _sprites[e.id] = e.sprite;
            _clips = new Dictionary<string, AudioClip>(clips.Count);
            foreach (var e in clips) if (e.clip != null) _clips[e.id] = e.clip;
        }

        /// <summary>Descarta o índice (depois de reconstruir o catálogo no editor).</summary>
        public void Invalidate() { _sprites = null; _clips = null; }

        public Sprite Sprite(string id)
        {
            Ensure();
            return id != null && _sprites.TryGetValue(id, out var s) ? s : null;
        }

        public bool HasSprite(string id) => Sprite(id) != null;

        public AudioClip Clip(string id)
        {
            Ensure();
            return id != null && _clips.TryGetValue(id, out var c) ? c : null;
        }
    }
}
