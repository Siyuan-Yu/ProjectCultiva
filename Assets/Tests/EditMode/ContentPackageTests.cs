using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class ContentPackageTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Load_BaseGame_RegistersSampleCharacter()
        {
            var loader = new ContentPackageLoader();
            var result = loader.Load(new[] { BaseGamePath });
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            Assert.IsTrue(result.Value.Registry.TryGetCharacter(
                new DefinitionId("base", "character_labor_disciple"), out var def));
            Assert.AreEqual(100, def.BaseAttributes["MaxHp"]);
            Assert.AreEqual("base", result.Value.Manifests[0].ModId);
        }

        [Test]
        public void Load_DuplicateDefinitionId_Fails()
        {
            var temp = CreateTempPackage("dup", @"
{
  ""definitions"": [
    { ""id"": ""base:character_labor_disciple"", ""type"": ""character"", ""displayNameKey"": ""a"" },
    { ""id"": ""base:character_labor_disciple"", ""type"": ""character"", ""displayNameKey"": ""b"" }
  ]
}");
            try
            {
                var loader = new ContentPackageLoader();
                var result = loader.Load(new[] { temp });
                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual(ErrorCode.ValidationFailed, result.Error.Code);
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        [Test]
        public void Load_InvalidDefinitionId_Fails()
        {
            var temp = CreateTempPackage("badid", @"
{
  ""definitions"": [
    { ""id"": ""not_namespaced"", ""type"": ""character"", ""displayNameKey"": ""a"" }
  ]
}");
            try
            {
                var loader = new ContentPackageLoader();
                var result = loader.Load(new[] { temp });
                Assert.IsTrue(result.IsFailure);
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        [Test]
        public void Load_MissingModId_AddsValidationError()
        {
            var root = Path.Combine(Path.GetTempPath(), "xianxia_content_" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "manifest.json"), "{ \"namespace\":\"base\", \"version\":\"1.0.0\" }");
            try
            {
                var result = new ContentPackageLoader().Load(new[] { root });
                Assert.IsTrue(result.IsFailure);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        static string CreateTempPackage(string name, string definitionsJson)
        {
            var root = Path.Combine(Path.GetTempPath(), "xianxia_content_" + name + "_" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(root, "Data"));
            File.WriteAllText(Path.Combine(root, "manifest.json"),
                "{ \"modId\":\"test\", \"namespace\":\"base\", \"version\":\"1.0.0\", \"compatibleGameVersion\":\"0.1.0\", \"contentFolders\":[\"Data\"] }");
            File.WriteAllText(Path.Combine(root, "Data", "defs.json"), definitionsJson);
            return root;
        }
    }
}
