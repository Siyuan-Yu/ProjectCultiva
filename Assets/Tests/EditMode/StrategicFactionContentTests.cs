using System;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>
    /// Strategic Faction Content authority（factions.json = 全局唯一 faction 真源）。
    /// 断言基于：ContentPackageLoader.Load 成功 → StrategicFactionContentInstaller.Install
    /// → StrategicFactionCatalog content-first（未安装时 fallback hardcoded 表）。
    /// </summary>
    public sealed class StrategicFactionContentTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static readonly string[] AllEightFactionIds =
        {
            "base:faction_player",
            "base:sect_huangcun_labor",
            "base:faction_fisher_village",
            "base:faction_nan_yan",
            "base:faction_shuofeng",
            "base:faction_donglin",
            "base:faction_xijin",
            "base:faction_bandits"
        };

        [Test]
        public void A_Load_BaseGame_Factions_Loadable()
        {
            var result = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            Assert.AreEqual(8, result.Value.Registry.StrategicFactions.Count);
            Assert.IsTrue(result.Value.Registry.TryGetStrategicFaction(
                new DefinitionId("base", "faction_nan_yan"), out var nanYan));
            Assert.AreEqual("南堰庄盟", nanYan.Name);
        }

        [Test]
        public void B_Load_BaseGame_AllEightFactions_Present()
        {
            var result = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            foreach (var idText in AllEightFactionIds)
            {
                Assert.IsTrue(
                    DefinitionId.TryParse(idText, out var id) &&
                    result.Value.Registry.TryGetStrategicFaction(id, out _),
                    "missing faction: " + idText);
            }
        }

        [Test]
        public void C_DisplayName_ComesFrom_Content_AfterLoad()
        {
            var result = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            Assert.IsTrue(StrategicFactionCatalog.HasInstalledContent);
            Assert.AreEqual("南堰庄盟", StrategicFactionCatalog.DisplayName("base:faction_nan_yan"));
            Assert.AreEqual("朔风堡", StrategicFactionCatalog.DisplayName("base:faction_shuofeng"));
        }

        [Test]
        public void D_MapTint_Matches_Hardcoded_ByteLevel()
        {
            var result = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            // (hardcode float RGB, expected mapColor hex)。hex 由 round(255 * oldFloat) 换算。
            AssertHexMatches(0.35f, 0.72f, 0.42f, "#59B86B", "player");
            AssertHexMatches(0.78f, 0.32f, 0.28f, "#C75247", "huangcun_labor");
            AssertHexMatches(0.32f, 0.52f, 0.82f, "#5285D1", "fisher_village");
            AssertHexMatches(0.82f, 0.62f, 0.28f, "#D19E47", "nan_yan");
            AssertHexMatches(0.55f, 0.68f, 0.78f, "#8CADC7", "shuofeng");
            AssertHexMatches(0.28f, 0.62f, 0.38f, "#479E61", "donglin");
            AssertHexMatches(0.58f, 0.45f, 0.72f, "#9473B8", "xijin");
            AssertHexMatches(0.62f, 0.28f, 0.62f, "#9E479E", "bandits");
        }

        [Test]
        public void E_Load_InvalidMapColor_Fails()
        {
            var temp = CreateTempPackage("badcolor", @"
{
  ""definitions"": [
    { ""id"": ""base:faction_x"", ""type"": ""strategicFaction"", ""name"": ""测试"", ""mapColor"": ""purple"" }
  ]
}");
            try
            {
                var result = new ContentPackageLoader().Load(new[] { temp });
                Assert.IsTrue(result.IsFailure);
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        [Test]
        public void F_Load_DuplicateFactionId_Fails()
        {
            var temp = CreateTempPackage("dup_faction", @"
{
  ""definitions"": [
    { ""id"": ""base:faction_dup"", ""type"": ""strategicFaction"", ""name"": ""甲"", ""mapColor"": ""#123456"" },
    { ""id"": ""base:faction_dup"", ""type"": ""strategicFaction"", ""name"": ""乙"", ""mapColor"": ""#654321"" }
  ]
}");
            try
            {
                var result = new ContentPackageLoader().Load(new[] { temp });
                Assert.IsTrue(result.IsFailure);
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        [Test]
        public void G_Load_UnknownFactionReference_Fails()
        {
            var temp = CreateTempPackage("unknown_faction_ref", @"
{
  ""definitions"": [
    { ""id"": ""base:faction_known"", ""type"": ""strategicFaction"", ""name"": ""已知"", ""mapColor"": ""#123456"" },
    { ""id"": ""base:char_member"", ""type"": ""character"", ""displayNameKey"": ""member"" },
    {
      ""id"": ""base:army_with_bad_faction"",
      ""type"": ""formalArmy"",
      ""name"": ""测试军团"",
      ""runtimeArmyId"": ""army:test_bad_faction"",
      ""runtimeStackId"": ""army:test_bad_faction_stack"",
      ""factionId"": ""base:faction_missing"",
      ""assemblySiteId"": ""base:site_huangcun"",
      ""members"": [
        { ""characterDefinitionId"": ""base:char_member"", ""displayName"": ""Member"", ""leader"": true }
      ]
    }
  ]
}");
            try
            {
                var result = new ContentPackageLoader().Load(new[] { temp });
                Assert.IsTrue(result.IsFailure);
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        [Test]
        public void H_Fallback_WithoutContent_StillWorks()
        {
            StrategicFactionCatalog.ResetInstall();
            Assert.IsFalse(StrategicFactionCatalog.HasInstalledContent);
            Assert.AreEqual("南堰庄盟", StrategicFactionCatalog.DisplayName("base:faction_nan_yan"));
            StrategicFactionCatalog.MapTint("base:faction_nan_yan", out var r, out var g, out var b);
            AssertByte(0.82f, r, "R");
            AssertByte(0.62f, g, "G");
            AssertByte(0.28f, b, "B");
            Assert.AreEqual("无归属", StrategicFactionCatalog.DisplayName(null));
        }

        static void AssertHexMatches(float hardR, float hardG, float hardB, string hex, string ctx)
        {
            Assert.IsTrue(StrategicFactionCatalog.TryParseMapColor(hex, out var r, out var g, out var b), ctx + " hex parse");
            AssertByte(hardR, r, ctx + " R");
            AssertByte(hardG, g, ctx + " G");
            AssertByte(hardB, b, ctx + " B");
        }

        static void AssertByte(float hard, float content, string ctx)
        {
            var expected = (int)Math.Round(hard * 255f);
            var actual = (int)Math.Round(content * 255f);
            Assert.AreEqual(expected, actual, ctx + " byte-level mismatch");
        }

        static string CreateTempPackage(string name, string definitionsJson)
        {
            var root = Path.Combine(Path.GetTempPath(), "xianxia_faction_" + name + "_" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(root, "Data"));
            File.WriteAllText(Path.Combine(root, "manifest.json"),
                "{ \"modId\":\"test\", \"namespace\":\"base\", \"version\":\"1.0.0\", \"compatibleGameVersion\":\"0.1.0\", \"contentFolders\":[\"Data\"] }");
            File.WriteAllText(Path.Combine(root, "Data", "defs.json"), definitionsJson);
            return root;
        }
    }
}
