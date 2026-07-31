using System;
using System.Collections.Generic;
using System.IO;
using XianXia.Core.Domain;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Data.Serialization;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Loads explicitly listed ContentPackage directories. Never scans Mods/.
    /// </summary>
    public sealed class ContentPackageLoader
    {
        public Result<LoadedContent> Load(IReadOnlyList<string> packageDirectories)
        {
            if (packageDirectories == null || packageDirectories.Count == 0)
                return Result.Fail<LoadedContent>(ErrorCode.ContentLoadFailed, "No package directories specified.");

            var report = new ValidationReport();
            var registry = new DefinitionRegistry();
            var manifests = new List<ContentManifest>();

            foreach (var dir in packageDirectories)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    report.Add(ErrorCode.ContentLoadFailed, "Package directory missing.", dir);
                    continue;
                }

                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    report.Add(ErrorCode.MissingRequiredField, "manifest.json missing.", dir);
                    continue;
                }

                ContentManifest manifest;
                try
                {
                    manifest = ReadManifest(File.ReadAllText(manifestPath), dir, report);
                }
                catch (Exception ex)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "Failed to parse manifest.json.", ex.Message);
                    continue;
                }

                if (manifest == null)
                    continue;

                manifests.Add(manifest);

                var dataRoot = Path.Combine(dir, "Data");
                if (!Directory.Exists(dataRoot))
                    continue;

                foreach (var file in Directory.GetFiles(dataRoot, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        LoadDefinitionsFile(File.ReadAllText(file), registry, report);
                    }
                    catch (Exception ex)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "Failed to parse definitions.", file + ": " + ex.Message);
                    }
                }
            }

            if (!report.IsValid)
                return report.ToResult<LoadedContent>(null);

            return Result.Ok(new LoadedContent(manifests, registry));
        }

        static ContentManifest ReadManifest(string json, string dir, ValidationReport report)
        {
            var root = SimpleJson.Parse(json);
            var modId = root.GetString("modId");
            var ns = root.GetString("namespace");
            var versionText = root.GetString("version");
            var compatible = root.GetString("compatibleGameVersion");

            if (string.IsNullOrEmpty(modId))
                report.Add(ErrorCode.MissingRequiredField, "modId required.", dir);
            if (string.IsNullOrEmpty(ns))
                report.Add(ErrorCode.MissingRequiredField, "namespace required.", dir);
            if (string.IsNullOrEmpty(versionText))
                report.Add(ErrorCode.MissingRequiredField, "version required.", dir);

            if (!report.IsValid && (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(versionText)))
                return null;

            var manifest = new ContentManifest
            {
                ModId = modId,
                Namespace = ns,
                Version = new DataVersion(versionText),
                CompatibleGameVersion = compatible ?? string.Empty,
                PackageDirectory = dir
            };

            if (root.TryGetProperty("contentFolders", out var folders) && folders.Kind == JsonValueKind.Array)
            {
                foreach (var f in folders.Array)
                {
                    if (f.Kind == JsonValueKind.String)
                        manifest.ContentFolders.Add(f.String);
                }
            }

            return manifest;
        }

        static void LoadDefinitionsFile(string json, DefinitionRegistry registry, ValidationReport report)
        {
            var root = SimpleJson.Parse(json);
            if (!root.TryGetProperty("definitions", out var defs) || defs.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "definitions array required.");
                return;
            }

            foreach (var item in defs.Array)
            {
                var idText = item.GetString("id");
                var type = item.GetString("type");
                if (string.IsNullOrEmpty(idText))
                {
                    report.Add(ErrorCode.MissingRequiredField, "definition.id required.");
                    continue;
                }

                var parsed = DefinitionId.Parse(idText);
                if (parsed.IsFailure)
                {
                    report.Add(parsed.Error);
                    continue;
                }

                if (!string.Equals(type, "character", StringComparison.Ordinal))
                    continue;

                var character = new CharacterDefinition
                {
                    Id = parsed.Value,
                    DisplayNameKey = item.GetString("displayNameKey", string.Empty)
                };

                if (item.TryGetProperty("baseAttributes", out var attrs) && attrs.Kind == JsonValueKind.Object)
                {
                    foreach (var kv in attrs.Object)
                        character.BaseAttributes[kv.Key] = (int)kv.Value.Number;
                }

                var reg = registry.RegisterCharacter(character);
                if (reg.IsFailure)
                    report.Add(reg.Error);
            }
        }
    }

    public sealed class LoadedContent
    {
        public LoadedContent(IReadOnlyList<ContentManifest> manifests, DefinitionRegistry registry)
        {
            Manifests = manifests;
            Registry = registry;
        }

        public IReadOnlyList<ContentManifest> Manifests { get; }

        public DefinitionRegistry Registry { get; }
    }
}
