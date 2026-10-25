using System;
using System.Collections.Generic;
using ES.Sim;
using UnityEngine;

namespace ES.Game
{
    /// <summary>
    /// Tiros, zonas e companheiros dos jogadores (sequência "aliados" do snapshot) e os estados
    /// visuais dos inimigos (preso, marcado, colado). Rastro de pneus e fumaça do carro em drift.
    /// </summary>
    public sealed class AllyViews
    {
        static readonly HashSet<AllySprite> Rotating = new HashSet<AllySprite>
        {
            AllySprite.Laser, AllySprite.Briefcase, AllySprite.Expression, AllySprite.Sashimi, AllySprite.Resistor, AllySprite.Transistor,
            AllySprite.ScopeWave, AllySprite.Dorito, AllySprite.Pill, AllySprite.Syringe, AllySprite.SyringeSpecial, AllySprite.Phone,
            AllySprite.Insulin, AllySprite.SoundWave, AllySprite.Bottle, AllySprite.Jabuti, AllySprite.JabutiWalk, AllySprite.MasterBolt,
            AllySprite.Computer, AllySprite.Carrot, AllySprite.ScrapPart,
        };

        static readonly HashSet<AllySprite> Companions = new HashSet<AllySprite>
        {
            AllySprite.BotShooter, AllySprite.BotBrawler, AllySprite.BotDasher, AllySprite.BotMaster, AllySprite.Car, AllySprite.TireOrbit,
            AllySprite.HotDog, AllySprite.HotDogTurbo, AllySprite.Papaya,
        };

        struct Mark { public Vector2 Pos; public float Born; public bool Smoke; }

        readonly StampLayer _stamps;
        readonly Dictionary<AllySprite, Sprite[]> _frames = new Dictionary<AllySprite, Sprite[]>();
        readonly Sprite _px, _trap, _mark, _sticky, _skid, _smoke;
        readonly List<Mark> _marks = new List<Mark>(256);
        readonly Dictionary<ushort, Vector2> _lastCar = new Dictionary<ushort, Vector2>();
        float _lastSmoke;

        public int Count => _stamps.Count;

        public AllyViews(Transform parent, GameAssets assets)
        {
            _stamps = new StampLayer(parent, "Allies", 1500);
            foreach (AllySprite s in Enum.GetValues(typeof(AllySprite)))
            {
                string name = s == AllySprite.Laser ? null : "ally_" + s.ToString().ToLowerInvariant() + "_";
                var list = new List<Sprite>();
                if (name == null) list.Add(assets.Sprite("fx_laser"));
                else for (int i = 0; i < 8; i++) { var sp = assets.Sprite(name + i); if (sp == null) break; list.Add(sp); }
                _frames[s] = list.ToArray();
            }
            _px = assets.Sprite("fx_px");
            _trap = assets.Sprite("fx_trap");
            _mark = assets.Sprite("fx_mark");
            _sticky = assets.Sprite("fx_sticky");
            _skid = assets.Sprite("fx_skid");
            _smoke = assets.Sprite("fx_smoke");
        }

        public static float AngleDeg(byte param) => param * 360f / 256f;

        public void Tick(RenderFrame frame, float now, EffectsLevel effects)
        {
            _stamps.Begin();
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                var m = _marks[i];
                float age = now - m.Born, life = m.Smoke ? 0.5f : 1.4f;
                if (age > life) { _marks.RemoveAt(i); continue; }
                float a = 1f - age / life;
                if (m.Smoke) _stamps.Add(_smoke, Layers.Snap(m.Pos + new Vector2(0, age * 0.6f)), new Color(1, 1, 1, 0.45f * a), Layers.Fx - 1);
                else _stamps.Add(_skid, Layers.Snap(m.Pos), new Color(1, 1, 1, 0.55f * a), Layers.Decal + 1);
            }

            foreach (var a in frame.Allies)
            {
                var pos = Layers.V(a.Pos);
                var frames = _frames[a.Sprite];
                switch (a.Sprite)
                {
                    case AllySprite.ZoneElectric:
                    case AllySprite.ZoneStorm:
                        Zone(pos, a.Param / 16f, a.Sprite == AllySprite.ZoneStorm, now, effects);
                        continue;
                    case AllySprite.ZoneStink:
                    case AllySprite.ZoneHeat:
                    case AllySprite.ZoneHeal:
                    case AllySprite.Afterimage:
                        Cloud(a.Sprite, pos, a.Param / 16f, now, effects, frames);
                        continue;
                }
                if (frames.Length == 0) continue;
                int f = frames.Length == 1 ? 0 : (int)(now * 8 + a.Id) % frames.Length;
                float rot = 0f;
                bool flip = false;
                if (a.Sprite == AllySprite.Car)
                {
                    // 8 direções desenhadas no sentido horário a partir da direita
                    f = ((Mathf.RoundToInt(-AngleDeg(a.Param) / 45f) % 8) + 8) % 8;
                    Skids(a.Id, pos, now, effects);
                }
                else if (Rotating.Contains(a.Sprite)) rot = AngleDeg(a.Param);
                else if ((a.Sprite >= AllySprite.BotShooter && a.Sprite <= AllySprite.BotMaster) || a.Sprite == AllySprite.HotDog || a.Sprite == AllySprite.HotDogTurbo)
                {
                    float deg = AngleDeg(a.Param);
                    flip = deg > 90f && deg < 270f;
                }
                int order = Companions.Contains(a.Sprite) ? Layers.Actor(pos.y) : Layers.Fx + 1;
                if (a.Sprite == AllySprite.Coin || a.Sprite == AllySprite.Digit) f = (int)(now * 10 + a.Id) % frames.Length;
                _stamps.Add(frames[f], Layers.Snap(pos), Color.white, order, rot, flip);
            }

            foreach (var e in frame.Enemies)
            {
                if (e.Status == 0) continue;
                var pos = Layers.V(e.Pos);
                if ((e.Status & EnemyStatus.Trapped) != 0) _stamps.Add(_trap, Layers.Snap(pos + new Vector2(0, 0.2f)), new Color(1, 1, 1, 0.85f), Layers.Fx + 2);
                if ((e.Status & EnemyStatus.Marked) != 0) _stamps.Add(_mark, Layers.Snap(pos + new Vector2(0, 0.75f)), Color.white, Layers.Fx + 3);
                if ((e.Status & EnemyStatus.Sticky) != 0) _stamps.Add(_sticky, Layers.Snap(pos + new Vector2(0.25f, 0.1f)), Color.white, Layers.Fx + 3);
            }
            _stamps.End();
        }

        void Zone(Vector2 center, float radius, bool storm, float now, EffectsLevel effects)
        {
            var col = storm ? new Color(0.7f, 0.55f, 1f, 0.75f) : new Color(0.55f, 0.95f, 1f, 0.7f);
            float circumference = Mathf.PI * 2f * radius * 32f;
            int n = Mathf.Clamp(Mathf.RoundToInt(circumference / 4f), 8, 300);
            float phase = effects == EffectsLevel.Low ? 0 : now * (storm ? 1.2f : 2.5f);
            for (int i = 0; i < n; i++)
            {
                if (((i + (int)(now * 12)) & 3) == 3) continue; // tracejado que gira
                float ang = phase + Mathf.PI * 2f * i / n;
                _stamps.Add(_px, Layers.Snap(center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius), col, Layers.Shadow + 3);
            }
            if (effects == EffectsLevel.Low) return;
            var rng = new System.Random((int)(now * 20) ^ (int)(center.x * 100));
            int sparks = storm ? 4 : 3;
            for (int i = 0; i < sparks; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = (float)rng.NextDouble() * radius;
                _stamps.Add(_px, Layers.Snap(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d), storm ? new Color(1f, 0.9f, 0.5f) : Color.white, Layers.Fx);
            }
        }

        /// <summary>Nuvem de mau cheiro, rastro de calor, campo de cura e imagem residual: anel do tamanho real + sprite.</summary>
        void Cloud(AllySprite kind, Vector2 center, float radius, float now, EffectsLevel effects, Sprite[] frames)
        {
            var col = kind == AllySprite.ZoneStink ? new Color(0.6f, 0.82f, 0.25f, 0.65f)
                : kind == AllySprite.ZoneHeat ? new Color(1f, 0.48f, 0.1f, 0.6f)
                : kind == AllySprite.ZoneHeal ? new Color(0.3f, 1f, 0.53f, 0.45f)
                : new Color(0.78f, 0.94f, 1f, 0.55f);
            if (kind != AllySprite.ZoneHeat)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.PI * 2f * radius * 32f / 5f), 8, 240);
                float phase = effects == EffectsLevel.Low ? 0 : now * 0.8f;
                for (int i = 0; i < n; i++)
                {
                    float ang = phase + Mathf.PI * 2f * i / n;
                    _stamps.Add(_px, Layers.Snap(center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius), col, Layers.Shadow + 3);
                }
            }
            if (frames.Length == 0) return;
            int f = (int)(now * 6) % frames.Length;
            float bob = kind == AllySprite.ZoneStink ? Mathf.Sin(now * 3f) * 0.08f : 0f;
            _stamps.Add(frames[f], Layers.Snap(center + new Vector2(0, bob)), kind == AllySprite.Afterimage ? new Color(1, 1, 1, 0.6f) : Color.white, kind == AllySprite.ZoneHeat ? Layers.Shadow + 4 : Layers.Fx);
            if (kind == AllySprite.ZoneStink && effects == EffectsLevel.High)
                for (int k = 0; k < 2; k++)
                {
                    float a = now * 1.3f + k * 3.1f;
                    _stamps.Add(frames[(f + 1) % frames.Length], Layers.Snap(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * radius * 0.55f), new Color(1, 1, 1, 0.7f), Layers.Fx);
                }
        }

        void Skids(ushort id, Vector2 pos, float now, EffectsLevel effects)
        {
            if (_lastCar.TryGetValue(id, out var last) && (pos - last).sqrMagnitude > 0.04f && _marks.Count < 240)
            {
                var dir = (pos - last).normalized;
                var side = new Vector2(-dir.y, dir.x) * 0.22f;
                _marks.Add(new Mark { Pos = pos - side, Born = now });
                _marks.Add(new Mark { Pos = pos + side, Born = now });
                if (effects != EffectsLevel.Low && now - _lastSmoke > 0.12f)
                {
                    _lastSmoke = now;
                    _marks.Add(new Mark { Pos = pos - dir * 0.4f, Born = now, Smoke = true });
                }
            }
            _lastCar[id] = pos;
        }
    }
}
