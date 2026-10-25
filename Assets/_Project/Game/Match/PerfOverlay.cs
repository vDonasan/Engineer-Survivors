using ES.Net;
using UnityEngine;

namespace ES.Game
{
    /// <summary>Painel F3: FPS, tempo de quadro, entidades, bytes/s e latência.</summary>
    public sealed class PerfOverlay : MonoBehaviour
    {
        public MatchView Match;
        public bool Visible;
        float _fps = 60f, _ms = 16f, _worstMs;
        float _worstReset;
        GUIStyle _style;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0) return;
            _fps = Mathf.Lerp(_fps, 1f / dt, 0.05f);
            _ms = Mathf.Lerp(_ms, dt * 1000f, 0.05f);
            _worstMs = Mathf.Max(_worstMs, dt * 1000f);
            if (Time.unscaledTime > _worstReset) { _worstReset = Time.unscaledTime + 2f; _worstMs = dt * 1000f; }
        }

        /// <summary>Média recente de FPS (também usada nas medições automáticas).</summary>
        public float Fps => _fps;

        void OnGUI()
        {
            if (!Visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
                _style.normal.textColor = Color.white;
            }
            var net = NetGame.Instance;
            string text = $"FPS {_fps:0}  quadro {_ms:0.0} ms (pior {_worstMs:0.0})\n";
            if (Match != null && Match.Active)
                text += $"inimigos {Match.EnemyCount}  hostis {Match.HostileCount}  gemas {Match.GemCount}  carimbos {Match.StampCount}\n";
            if (net != null)
                text += $"{net.Mode}  enviados {net.SentPerSecond / 1024f:0.0} KB/s  recebidos {net.ReceivedPerSecond / 1024f:0.0} KB/s  latência {net.RttMs:0} ms";
            GUI.Box(new Rect(8, 8, 520, 64), text, _style);
        }
    }
}
