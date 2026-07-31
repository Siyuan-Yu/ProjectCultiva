using System.IO;
using NUnit.Framework;
using XianXia.Core.Results;
using XianXia.Data.Content;
using XianXia.Data.Import;

namespace XianXia.Tests
{
    public sealed class CsvDefinitionImporterTests
    {
        static string AuthoringCsvPath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame", "Authoring", "Csv"));

        [Test]
        public void Convert_BaseGameAuthoringCsv_WritesJsonAndLoads()
        {
            var outDir = Path.Combine(Path.GetTempPath(), "xianxia_csv_out_" + Path.GetRandomFileName());
            try
            {
                var result = new CsvDefinitionImporter().ConvertDirectory(AuthoringCsvPath, outDir);
                Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
                Assert.IsTrue(File.Exists(Path.Combine(outDir, "characters.json")));
                Assert.IsTrue(File.Exists(Path.Combine(outDir, "cultivation.json")));
                Assert.IsTrue(File.Exists(Path.Combine(outDir, "items.json")));

                var package = CreateTempPackageFromData(outDir);
                try
                {
                    var loaded = new ContentPackageLoader().Load(new[] { package });
                    Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
                    Assert.AreEqual(1, loaded.Value.Registry.Characters.Count);
                    Assert.AreEqual(1, loaded.Value.Registry.Cultivations.Count);
                    Assert.AreEqual(1, loaded.Value.Registry.Items.Count);
                }
                finally
                {
                    Directory.Delete(package, true);
                }
            }
            finally
            {
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, true);
            }
        }

        [Test]
        public void Convert_DuplicateId_BlocksOutput()
        {
            var csvDir = CreateCsvDir(
                "id,name,MaxHp\nbase:character_a,甲,1\nbase:character_a,乙,2\n",
                "id,name,requiredRealm,targetAttribute,operation,value\nbase:cultivation_a,功,凡人,MaxHp,Fixed,1\n",
                "id,name,maxStack\nbase:item_a,物,1\n");
            var outDir = Path.Combine(Path.GetTempPath(), "xianxia_csv_dup_" + Path.GetRandomFileName());
            try
            {
                var result = new CsvDefinitionImporter().ConvertDirectory(csvDir, outDir);
                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual(ErrorCode.ValidationFailed, result.Error.Code);
                Assert.IsFalse(Directory.Exists(outDir) && File.Exists(Path.Combine(outDir, "characters.json")));
            }
            finally
            {
                Directory.Delete(csvDir, true);
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, true);
            }
        }

        [Test]
        public void Convert_InvalidDefinitionId_BlocksOutput()
        {
            var csvDir = CreateCsvDir(
                "id,name,MaxHp\nbad_id,甲,1\n",
                "id,name,requiredRealm,targetAttribute,operation,value\nbase:cultivation_a,功,凡人,MaxHp,Fixed,1\n",
                "id,name,maxStack\nbase:item_a,物,1\n");
            var outDir = Path.Combine(Path.GetTempPath(), "xianxia_csv_badid_" + Path.GetRandomFileName());
            try
            {
                var result = new CsvDefinitionImporter().ConvertDirectory(csvDir, outDir);
                Assert.IsTrue(result.IsFailure);
                Assert.IsFalse(File.Exists(Path.Combine(outDir, "characters.json")));
            }
            finally
            {
                Directory.Delete(csvDir, true);
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, true);
            }
        }

        [Test]
        public void Convert_MissingRequiredField_BlocksOutput()
        {
            var csvDir = CreateCsvDir(
                "id,MaxHp\nbase:character_a,1\n",
                "id,name,requiredRealm,targetAttribute,operation,value\nbase:cultivation_a,功,凡人,MaxHp,Fixed,1\n",
                "id,name,maxStack\nbase:item_a,物,1\n");
            var outDir = Path.Combine(Path.GetTempPath(), "xianxia_csv_miss_" + Path.GetRandomFileName());
            try
            {
                var result = new CsvDefinitionImporter().ConvertDirectory(csvDir, outDir);
                Assert.IsTrue(result.IsFailure);
                Assert.IsFalse(File.Exists(Path.Combine(outDir, "characters.json")));
            }
            finally
            {
                Directory.Delete(csvDir, true);
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, true);
            }
        }

        [Test]
        public void Convert_MissingDefinitionRef_BlocksOutput()
        {
            var csvDir = CreateCsvDir(
                "id,name,MaxHp\nbase:character_a,甲,1\n",
                "id,name,requiredRealm,targetAttribute,operation,value\nbase:cultivation_a,功,base:missing_realm,MaxHp,Fixed,1\n",
                "id,name,maxStack\nbase:item_a,物,1\n");
            var outDir = Path.Combine(Path.GetTempPath(), "xianxia_csv_ref_" + Path.GetRandomFileName());
            try
            {
                var result = new CsvDefinitionImporter().ConvertDirectory(csvDir, outDir);
                Assert.IsTrue(result.IsFailure);
                Assert.IsFalse(File.Exists(Path.Combine(outDir, "characters.json")));
            }
            finally
            {
                Directory.Delete(csvDir, true);
                if (Directory.Exists(outDir))
                    Directory.Delete(outDir, true);
            }
        }

        static string CreateCsvDir(string characters, string cultivation, string items)
        {
            var root = Path.Combine(Path.GetTempPath(), "xianxia_csv_src_" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "characters.csv"), characters);
            File.WriteAllText(Path.Combine(root, "cultivation.csv"), cultivation);
            File.WriteAllText(Path.Combine(root, "items.csv"), items);
            return root;
        }

        static string CreateTempPackageFromData(string dataDir)
        {
            var root = Path.Combine(Path.GetTempPath(), "xianxia_csv_pkg_" + Path.GetRandomFileName());
            var dest = Path.Combine(root, "Data");
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(dataDir, "*.json"))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
            File.WriteAllText(
                Path.Combine(root, "manifest.json"),
                "{ \"modId\":\"test\", \"namespace\":\"base\", \"version\":\"1.0.0\", \"compatibleGameVersion\":\"0.1.0\", \"contentFolders\":[\"Data\"] }");
            return root;
        }
    }
}
