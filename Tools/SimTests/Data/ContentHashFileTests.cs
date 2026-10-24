using System.Globalization;
using System.IO;

namespace ES.SimTests.Data
{
    public class ContentHashFileTests
    {
        /// <summary>
        /// O arquivo sp1-content.hash é a referência que a Unity também confere. Ao editar o
        /// JSON, grave aqui o valor que a mensagem de falha informa.
        /// </summary>
        [Test]
        public void HashFile_MatchesJson()
        {
            string path = Path.Combine(Path.GetDirectoryName(ContentLoader.Sp1Path), "sp1-content.hash");
            string actual = ContentCanonical.Hash(ContentLoader.LoadSp1()).ToString("x16", CultureInfo.InvariantCulture);
            Assert.That(File.Exists(path), Is.True, $"crie {path} com: {actual}");
            Assert.That(File.ReadAllText(path).Trim(), Is.EqualTo(actual), $"atualize {path} para: {actual}");
        }
    }
}
