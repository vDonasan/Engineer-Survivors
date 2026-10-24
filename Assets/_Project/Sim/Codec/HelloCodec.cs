using System.Text;

namespace ES.Sim
{
    /// <summary>Primeira mensagem do cliente ao entrar na sala.</summary>
    public struct Hello
    {
        public string BuildVersion;
        public ulong ContentHash;
        public string Name;
        public string SpecializationId;
    }

    public static class HelloCodec
    {
        public const int MaxNameChars = 16;
        public const string DefaultName = "Engenheiro(a)";

        public static void Write(ByteWriter w, in Hello h)
        {
            w.Str(h.BuildVersion);
            w.U64(h.ContentHash);
            w.Str(SanitizeName(h.Name));
            w.Str(h.SpecializationId);
        }

        public static bool TryRead(byte[] data, int length, out Hello h)
        {
            h = default;
            var r = new ByteReader(data, length);
            if (!r.TryStr(32, out string version) || !r.TryU64(out ulong hash) || !r.TryStr(64, out string name) || !r.TryStr(32, out string spec))
                return false;
            h = new Hello { BuildVersion = version, ContentHash = hash, Name = SanitizeName(name), SpecializationId = spec };
            return r.Remaining == 0;
        }

        /// <summary>Remove caracteres de controle, apara e corta em 16; vazio vira o nome padrão.</summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return DefaultName;
            var sb = new StringBuilder(name.Length);
            foreach (char c in name) if (!char.IsControl(c) && !char.IsSurrogate(c)) sb.Append(c);
            string s = sb.ToString().Trim();
            if (s.Length > MaxNameChars) s = s.Substring(0, MaxNameChars).TrimEnd();
            return s.Length == 0 ? DefaultName : s;
        }
    }
}
