using System.Collections.Generic;
using UnityEngine;

namespace ES.Game
{
    /// <summary>Quadros de um personagem (manifesto: char_&lt;spec&gt;_&lt;anim&gt;_&lt;dir&gt;_&lt;n&gt;).</summary>
    public sealed class CharacterSprites
    {
        public readonly Sprite[][] Idle = new Sprite[4][];
        public readonly Sprite[][] Walk = new Sprite[4][];
        public Sprite[] Attack, Special, Hurt, Stun, Downed, Revive, Victory;

        static readonly Dictionary<string, CharacterSprites> Cache = new Dictionary<string, CharacterSprites>();

        public static CharacterSprites For(GameAssets assets, string spec)
        {
            string key = assets.GetInstanceID() + ":" + spec;
            if (Cache.TryGetValue(key, out var c)) return c;
            c = new CharacterSprites();
            for (int d = 0; d < 4; d++)
            {
                c.Idle[d] = Frames(assets, spec, "idle", Layers.DirNames[d], 2);
                c.Walk[d] = Frames(assets, spec, "walk", Layers.DirNames[d], 4);
            }
            c.Attack = Frames(assets, spec, "attack", "down", 3);
            c.Special = Frames(assets, spec, "special", "down", 3);
            c.Hurt = Frames(assets, spec, "hurt", "down", 2);
            c.Stun = Frames(assets, spec, "stun", "down", 2);
            c.Downed = Frames(assets, spec, "downed", "down", 3);
            c.Revive = Frames(assets, spec, "revive", "down", 2);
            c.Victory = Frames(assets, spec, "victory", "down", 2);
            Cache[key] = c;
            return c;
        }

        static Sprite[] Frames(GameAssets a, string spec, string anim, string dir, int n)
        {
            var f = new Sprite[n];
            for (int i = 0; i < n; i++) f[i] = a.Sprite($"char_{spec}_{anim}_{dir}_{i}");
            return f;
        }

        public static Sprite Loop(Sprite[] frames, float t, float fps)
        {
            if (frames == null || frames.Length == 0) return null;
            int i = (int)(t * fps) % frames.Length;
            return frames[i < 0 ? 0 : i];
        }

        public static Sprite Once(Sprite[] frames, float t, float fps)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[Mathf.Clamp((int)(t * fps), 0, frames.Length - 1)];
        }
    }
}
