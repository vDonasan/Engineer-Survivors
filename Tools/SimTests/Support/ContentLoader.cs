using System;
using System.IO;
using System.Text.Json;

namespace ES.SimTests
{
    /// <summary>Lê o conteúdo canônico (o mesmo JSON que a Unity importa).</summary>
    public static class ContentLoader
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = false,
        };

        public static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project")))
                    dir = dir.Parent;
                if (dir == null) throw new DirectoryNotFoundException("raiz do repositório não encontrada");
                return dir.FullName;
            }
        }

        public static string Sp1Path => Path.Combine(RepoRoot, "Assets", "_Project", "Content", "Data", "sp1-content.json");

        public static ContentDef LoadSp1() => Parse(File.ReadAllText(Sp1Path));

        public static ContentDef Parse(string json) => JsonSerializer.Deserialize<ContentDef>(json, Options);

        public static GameDatabase Sp1Database()
        {
            var r = GameDatabase.Build(LoadSp1());
            if (r.Errors.Length > 0) throw new InvalidOperationException(string.Join("\n", r.Errors));
            return r.Db;
        }
    }
}
