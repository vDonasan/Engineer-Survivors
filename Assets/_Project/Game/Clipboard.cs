using System.Runtime.InteropServices;
using UnityEngine;

namespace ES.Game
{
    /// <summary>Copia texto: no navegador via navigator.clipboard (.jslib), no editor via a área de transferência do sistema.</summary>
    public static class Clipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ES_CopyToClipboard(string text);
#endif

        public static void Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            ES_CopyToClipboard(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }
    }
}
