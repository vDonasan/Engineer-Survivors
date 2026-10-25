using System.Globalization;
using System.Text;

namespace ES.Sim
{
    /// <summary>Tags de engenharia (habilidades) e de natureza (inimigos).</summary>
    public enum Tag
    {
        Energia, Eletronica, Automacao, Software, Estruturas, Mecanica, Biomecanica, Quimica,
        Sensores, Robotica, Defesa, Suporte, Termico, Reacao, Drones,
        Maquina, Blindado, Organico,
        // categorias de jogo (requisitos de evolução dos itens)
        Area, Projetil, Mobilidade, Cura,
    }

    public static class Tags
    {
        public const int Count = (int)Tag.Cura + 1;

        static readonly string[] Keys = BuildKeys();

        static string[] BuildKeys()
        {
            var keys = new string[Count];
            for (int i = 0; i < Count; i++) keys[i] = ((Tag)i).ToString().ToLowerInvariant();
            return keys;
        }

        /// <summary>Aceita o nome com ou sem acento, sem diferenciar maiúsculas.</summary>
        public static bool TryParse(string text, out Tag tag)
        {
            tag = default;
            if (string.IsNullOrEmpty(text)) return false;
            string key = StripAccents(text.Trim()).ToLowerInvariant();
            for (int i = 0; i < Count; i++)
            {
                if (Keys[i] == key) { tag = (Tag)i; return true; }
            }
            return false;
        }

        static string StripAccents(string s)
        {
            string d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString();
        }
    }
}
