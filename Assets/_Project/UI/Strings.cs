using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ES.UI
{
    /// <summary>Tabela de textos por chave (pt-BR). Chave ausente → "[chave]" e erro no log.</summary>
    public static class Strings
    {
        static Dictionary<string, string> _table = new Dictionary<string, string>();
        static readonly HashSet<string> Reported = new HashSet<string>();

        public static int Count => _table.Count;

        public static void Load(string json)
        {
            _table = ParseFlat(json ?? "{}");
            Reported.Clear();
        }

        public static bool Has(string key) => key != null && _table.ContainsKey(key);

        public static string Get(string key)
        {
            if (key != null && _table.TryGetValue(key, out var s)) return s;
            if (Reported.Add(key ?? "")) Debug.LogError("[ES] Texto ausente: " + key);
            return "[" + key + "]";
        }

        public static string Format(string key, params object[] args)
        {
            try { return string.Format(Get(key), args); }
            catch (System.FormatException) { return Get(key); }
        }

        /// <summary>Lê um objeto JSON plano { "chave": "texto", ... }.</summary>
        public static Dictionary<string, string> ParseFlat(string json)
        {
            var d = new Dictionary<string, string>();
            int i = 0;
            Skip(json, ref i);
            if (i >= json.Length || json[i] != '{') return d;
            i++;
            while (true)
            {
                Skip(json, ref i);
                if (i >= json.Length || json[i] == '}') break;
                string key = ReadString(json, ref i);
                Skip(json, ref i);
                if (i >= json.Length || json[i] != ':') break;
                i++;
                Skip(json, ref i);
                string value = ReadString(json, ref i);
                if (key == null || value == null) break;
                d[key] = value;
                Skip(json, ref i);
                if (i < json.Length && json[i] == ',') i++;
            }
            return d;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) return null;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) return null;
                        sb.Append((char)System.Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            return null;
        }
    }
}
