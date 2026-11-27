using System.IO;

namespace Relead.CrossPromo.Tests
{
    static class TestUtil
    {
        public const string FixtureDir = "Packages/com.relead.crosspromo/Tests/Editor/Fixtures/";

        public static string Fixture(string name) => File.ReadAllText(Path.GetFullPath(FixtureDir + name));

        public static string NewTempDir()
        {
            var d = Path.Combine(Path.GetTempPath(), "relead-crosspromo-tests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }
    }
}
