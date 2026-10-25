using UnityEngine;

namespace ES.Game
{
    /// <summary>Segue o alvo, limita à arena e treme (× configuração de tremor).</summary>
    public sealed class CameraRig
    {
        readonly Camera _cam;
        Vector2 _pos;
        float _trauma;
        bool _has;
        public float ShakeScale = 1f;
        public Vector2 ArenaSize = new Vector2(64, 64);

        public CameraRig(Camera cam) { _cam = cam; }

        public void AddShake(float amount) => _trauma = Mathf.Min(1f, _trauma + amount);

        public void Follow(Vector2 target, float dt)
        {
            if (!_has) { _pos = target; _has = true; }
            _pos = Vector2.Lerp(_pos, target, 1f - Mathf.Exp(-dt * 12f));
            float halfH = _cam.orthographicSize, halfW = halfH * _cam.aspect;
            var p = _pos;
            p.x = ArenaSize.x > halfW * 2 - 2 ? Mathf.Clamp(p.x, halfW - 1.5f, ArenaSize.x - halfW + 1.5f) : ArenaSize.x / 2;
            p.y = ArenaSize.y > halfH * 2 - 2 ? Mathf.Clamp(p.y, halfH - 1.5f, ArenaSize.y - halfH + 1.5f) : ArenaSize.y / 2;
            var shake = Vector2.zero;
            if (_trauma > 0 && ShakeScale > 0)
            {
                float s = _trauma * _trauma * ShakeScale * 0.35f;
                shake = new Vector2(Random.Range(-s, s), Random.Range(-s, s));
                _trauma = Mathf.Max(0f, _trauma - dt * 2.2f);
            }
            _cam.transform.position = new Vector3(p.x + shake.x, p.y + shake.y, -10f);
        }

        public void Reset() { _has = false; _trauma = 0; }
    }
}
