using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace LegacyRuntimeConverter;

internal static class Program
{
    private static readonly JsonSerializerOptions OutputOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--self-test")
                return RunSelfTest();
            var options = Options.Parse(args);
            var inputPath = Path.GetFullPath(options.InputPath);
            var outputPath = Path.GetFullPath(options.OutputPath);
            if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
                throw new ConversionException("输入与输出必须是不同路径。");
            if (!File.Exists(inputPath))
                throw new ConversionException("输入文件不存在：" + inputPath);
            if (File.Exists(outputPath))
                throw new ConversionException("输出文件已存在，拒绝覆盖：" + outputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
                throw new ConversionException("输出目录不存在：" + outputDirectory);

            var root = JsonNode.Parse(
                File.ReadAllText(inputPath, Encoding.UTF8),
                documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })
                as JsonObject ?? throw new ConversionException("输入 JSON 根节点必须是 object。");

            var converted = options.Mode switch
            {
                "content" => ConvertContent(root, options),
                "snapshot" => ConvertSnapshot(root, options),
                _ => throw new ConversionException("模式必须是 content 或 snapshot。")
            };

            WriteNewFileAtomically(outputPath, converted.ToJsonString(OutputOptions) + Environment.NewLine);
            Console.WriteLine($"转换成功：{inputPath}");
            Console.WriteLine($"输出副本：{outputPath}");
            return 0;
        }
        catch (ConversionException ex)
        {
            Console.Error.WriteLine("转换失败：" + ex.Message);
            return 2;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine("转换失败：输入不是有效 JSON。" + ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("转换失败：" + ex.Message);
            return 1;
        }
    }

    private static JsonObject ConvertContent(JsonObject source, Options options)
    {
        RejectUnsupportedLegacyMapContent(source);
        var root = (JsonObject)source.DeepClone();
        var definitions = root["definitions"] as JsonArray
            ?? throw new ConversionException("Content 输入缺少 definitions array。");
        var convertedIds = new HashSet<string>(StringComparer.Ordinal);
        var convertedCount = 0;

        foreach (var node in definitions)
        {
            if (node is not JsonObject definition)
                throw new ConversionException("definitions 中存在非 object 项。");
            if (!string.Equals(OptionalString(definition, "type"), "formalArmy", StringComparison.Ordinal))
                continue;

            var id = RequiredString(definition, "id", "formalArmy");
            var factionId = RequiredString(definition, "factionId", id);
            var assemblySiteId = RequiredString(definition, "assemblySiteId", id);
            var members = definition["members"] as JsonArray
                ?? throw new ConversionException($"{id}: members 必须是 array。");
            ValidateContentMembers(id, members);

            var hasPosition = definition["initialSurfacePosition"] is not null;
            var hasDeployment = definition["initialSurfaceDeployment"] is not null;
            if (hasPosition && hasDeployment)
                throw new ConversionException($"{id}: initialSurfacePosition 与 initialSurfaceDeployment 不能并存。");

            JsonObject? generatedPosition = null;
            if (!hasPosition && !hasDeployment && definition["initialHex"] is JsonNode hexNode)
            {
                if (hexNode is not JsonObject hex)
                    throw new ConversionException($"{id}: initialHex 必须是 object。");
                var q = RequiredInt(hex, "q", id + ".initialHex");
                var r = RequiredInt(hex, "r", id + ".initialHex");
                RequireHexOptions(options, id);
                var (x, y) = HexToWorld(q, r, options.MovementScale!.Value);
                generatedPosition = new JsonObject
                {
                    ["surfaceId"] = options.SurfaceId,
                    ["worldX"] = x,
                    ["worldY"] = y
                };
            }

            var runtimeArmyId = OptionalString(definition, "runtimeArmyId");
            var squadId = !string.IsNullOrWhiteSpace(runtimeArmyId)
                ? "squad:migrated:" + runtimeArmyId.Replace(':', '_')
                : "squad:legacy:" + id.Replace(':', '_');

            definition["type"] = "npcSquad";
            definition["squadId"] = squadId;
            definition["factionId"] = factionId;
            definition["assemblySiteId"] = assemblySiteId;
            definition.Remove("runtimeArmyId");
            definition.Remove("runtimeStackId");
            definition.Remove("initialHex");
            if (generatedPosition is not null)
                definition["initialSurfacePosition"] = generatedPosition;
            convertedIds.Add(id);
            convertedCount++;
        }

        foreach (var node in definitions)
        {
            if (node is not JsonObject definition ||
                definition["initialFormalArmyIds"] is not JsonNode legacyNode)
                continue;
            if (legacyNode is not JsonArray legacyIds)
                throw new ConversionException(
                    $"{OptionalString(definition, "id")}: initialFormalArmyIds 必须是 array。");
            var merged = new JsonArray();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (definition["initialNpcSquadIds"] is JsonNode currentNode)
            {
                if (currentNode is not JsonArray currentIds)
                    throw new ConversionException(
                        $"{OptionalString(definition, "id")}: initialNpcSquadIds 必须是 array。");
                AppendUniqueStrings(currentIds, merged, seen, "initialNpcSquadIds");
            }
            AppendUniqueStrings(legacyIds, merged, seen, "initialFormalArmyIds");
            definition["initialNpcSquadIds"] = merged;
            definition.Remove("initialFormalArmyIds");
        }

        Console.WriteLine($"Content formalArmy 转换数：{convertedCount}");
        return root;
    }

    private static JsonObject ConvertSnapshot(JsonObject source, Options options)
    {
        ValidateCurrentSnapshotAuthority(source, allowFormalArmyInput: true);
        var root = (JsonObject)source.DeepClone();
        var strategic = root["strategic"] as JsonObject
            ?? throw new ConversionException("Snapshot 输入缺少 strategic object。");
        RejectUnsafeLegacyRelations(strategic);

        var armies = strategic["formalArmies"] as JsonArray ?? new JsonArray();
        var memberships = strategic["armyMemberships"] as JsonArray ?? new JsonArray();
        if (strategic["formalArmies"] is not null && strategic["formalArmies"] is not JsonArray)
            throw new ConversionException("strategic.formalArmies 必须是 array。");
        if (strategic["armyMemberships"] is not null && strategic["armyMemberships"] is not JsonArray)
            throw new ConversionException("strategic.armyMemberships 必须是 array。");

        var membershipByArmy = ReadMemberships(memberships);
        var squads = strategic["squads"] as JsonArray ?? new JsonArray();
        var motions = strategic["squadWorldMotions"] as JsonArray ?? new JsonArray();
        if (strategic["squads"] is not null && strategic["squads"] is not JsonArray)
            throw new ConversionException("strategic.squads 必须是 array。");
        if (strategic["squadWorldMotions"] is not null && strategic["squadWorldMotions"] is not JsonArray)
            throw new ConversionException("strategic.squadWorldMotions 必须是 array。");

        var squadsById = IndexByStringId(squads, "squadId", "strategic.squads");
        var motionsById = IndexByStringId(motions, "squadId", "strategic.squadWorldMotions");
        var convertedArmyIds = new HashSet<string>(StringComparer.Ordinal);
        var convertedCount = 0;
        foreach (var node in armies)
        {
            if (node is not JsonObject army)
                throw new ConversionException("strategic.formalArmies 中存在非 object 项。");
            var armyId = RequiredString(army, "armyId", "formalArmy snapshot");
            if (!convertedArmyIds.Add(armyId))
                throw new ConversionException("重复 legacy ArmyId：" + armyId);
            var squadId = "squad:army:" + armyId;
            var members = CollectSnapshotMembers(army, membershipByArmy.GetValueOrDefault(armyId), armyId);
            var leader = RequiredUlongString(army, "leaderCharacterId", armyId);
            if (leader == 0 || !members.Contains(leader))
                throw new ConversionException($"{armyId}: leaderCharacterId 缺失或不在成员中，拒绝猜测。");

            var hasSquad = squadsById.TryGetValue(squadId, out var existingSquad);
            var hasMotion = motionsById.TryGetValue(squadId, out var existingMotion);
            if (hasSquad != hasMotion)
                throw new ConversionException($"{armyId}: 已有 current Squad/Motion 不完整，拒绝覆盖或猜测。");
            if (hasSquad)
            {
                ValidateExistingCurrentPair(existingSquad!, existingMotion!, members, leader, armyId);
                convertedCount++;
                continue;
            }

            var factionId = RequiredString(army, "factionId", armyId);
            var squad = new JsonObject
            {
                ["squadId"] = squadId,
                ["displayName"] = string.Empty,
                ["factionId"] = factionId,
                ["leaderCharacterId"] = UlongNode(leader),
                ["legacyArmyId"] = string.Empty,
                ["commandKind"] = 3,
                ["commandRevision"] = "0",
                ["commandTargetCharacterId"] = "0",
                ["memberCharacterIds"] = ToUlongArray(members)
            };
            var motion = BuildMotion(army, squadId, options, armyId);
            squads.Add(squad);
            motions.Add(motion);
            squadsById.Add(squadId, squad);
            motionsById.Add(squadId, motion);
            convertedCount++;
        }

        foreach (var armyId in membershipByArmy.Keys)
            if (!convertedArmyIds.Contains(armyId))
                throw new ConversionException(
                    $"armyMemberships 引用了不存在的 FormalArmy：{armyId}");

        strategic["squads"] = squads;
        strategic["squadWorldMotions"] = motions;
        strategic["hasSquadSnapshotAuthority"] = true;
        strategic["hasSquadWorldMotionSnapshotAuthority"] = true;
        strategic["formalArmies"] = new JsonArray();
        strategic["armyMemberships"] = new JsonArray();
        ValidateCurrentSnapshotAuthority(root, allowFormalArmyInput: false);
        Console.WriteLine($"Snapshot FormalArmy 转换数：{convertedCount}");
        return root;
    }

    private static JsonObject BuildMotion(
        JsonObject army, string squadId, Options options, string armyId)
    {
        if (OptionalString(army, "orderTargetArmyId").Length > 0)
            throw new ConversionException($"{armyId}: orderTargetArmyId 是未安全转换的 Army 关系。");
        if (OptionalBool(army, "hasSiteDepartureState") ||
            OptionalBool(army, "isSiteDeparturePending"))
            throw new ConversionException($"{armyId}: Site departure 中间态无法安全离线转换。");

        var surfacePath = OptionalArray(army, "surfacePath", armyId);
        var hexPath = OptionalArray(army, "hexPath", armyId);
        var stateMoving = OptionalInt(army, "state") == 1;
        var isMoving = stateMoving || surfacePath.Count > 0 || hexPath.Count > 1;
        var surfaceId = OptionalString(army, "surfaceId");
        var hasExactPosition = !string.IsNullOrWhiteSpace(surfaceId) &&
                               HasFiniteNumber(army, "worldX") &&
                               HasFiniteNumber(army, "worldY");
        double worldX;
        double worldY;
        var route = new JsonArray();
        double destinationX;
        double destinationY;
        var waypointIndex = 0;

        if (hasExactPosition)
        {
            worldX = RequiredFiniteDouble(army, "worldX", armyId);
            worldY = RequiredFiniteDouble(army, "worldY", armyId);
            if (isMoving)
            {
                if (surfacePath.Count == 0)
                    throw new ConversionException(
                        $"{armyId}: moving Surface 位置缺少 surfacePath，拒绝从 Hex 路径猜测。");
                foreach (var pointNode in surfacePath)
                {
                    if (pointNode is not JsonObject point)
                        throw new ConversionException($"{armyId}: surfacePath 含非 object 项。");
                    route.Add(new JsonObject
                    {
                        ["x"] = RequiredFiniteDouble(point, "x", armyId + ".surfacePath"),
                        ["y"] = RequiredFiniteDouble(point, "y", armyId + ".surfacePath")
                    });
                }
                destinationX = RequiredFiniteDouble(army, "physicalDestinationX", armyId);
                destinationY = RequiredFiniteDouble(army, "physicalDestinationY", armyId);
                waypointIndex = OptionalInt(army, "surfaceWaypointIndex");
                if (waypointIndex < 0 || waypointIndex > route.Count)
                    throw new ConversionException($"{armyId}: surfaceWaypointIndex 越界。");
            }
            else
            {
                destinationX = worldX;
                destinationY = worldY;
            }
        }
        else
        {
            RequireHexOptions(options, armyId);
            var q = RequiredInt(army, "currentHexQ", armyId);
            var r = RequiredInt(army, "currentHexR", armyId);
            (worldX, worldY) = HexToWorld(q, r, options.MovementScale!.Value);
            surfaceId = options.SurfaceId!;

            if (isMoving)
            {
                if (hexPath.Count > 0)
                {
                    foreach (var pointNode in hexPath)
                    {
                        if (pointNode is not JsonObject point)
                            throw new ConversionException($"{armyId}: hexPath 含非 object 项。");
                        var pointQ = RequiredInt(point, "q", armyId + ".hexPath");
                        var pointR = RequiredInt(point, "r", armyId + ".hexPath");
                        var (x, y) = HexToWorld(pointQ, pointR, options.MovementScale.Value);
                        route.Add(new JsonObject { ["x"] = x, ["y"] = y });
                    }
                    waypointIndex = OptionalInt(army, "currentPathIndex");
                }
                else
                {
                    var destinationQ = RequiredInt(army, "destinationHexQ", armyId);
                    var destinationR = RequiredInt(army, "destinationHexR", armyId);
                    route.Add(new JsonObject { ["x"] = worldX, ["y"] = worldY });
                    var (x, y) = HexToWorld(destinationQ, destinationR, options.MovementScale.Value);
                    route.Add(new JsonObject { ["x"] = x, ["y"] = y });
                }
                if (waypointIndex < 0 || waypointIndex >= route.Count)
                    throw new ConversionException($"{armyId}: Hex path index 越界。");
                var destination = (JsonObject)route[route.Count - 1]!;
                destinationX = destination["x"]!.GetValue<double>();
                destinationY = destination["y"]!.GetValue<double>();
            }
            else
            {
                destinationX = worldX;
                destinationY = worldY;
            }
        }

        return new JsonObject
        {
            ["squadId"] = squadId,
            ["surfaceId"] = surfaceId,
            // Current restore canonicalizes a stationary non-empty SiteId to SiteArrival.
            // Converted exact/Hex positions are physical authority and must not be reset.
            ["siteId"] = string.Empty,
            ["worldX"] = worldX,
            ["worldY"] = worldY,
            ["isMoving"] = isMoving,
            ["destinationX"] = destinationX,
            ["destinationY"] = destinationY,
            ["waypointIndex"] = waypointIndex,
            ["segmentProgress"] = OptionalFiniteDouble(army, "segmentProgress"),
            ["sourceRevision"] = OptionalString(army, "surfaceSourceRevision"),
            ["sourceHash"] = OptionalString(army, "surfaceSourceHash"),
            ["route"] = route
        };
    }

    private static void RejectUnsupportedLegacyMapContent(JsonObject root)
    {
        if (ContainsProperty(root, "openingHexWorldId"))
            throw UnsupportedLegacyMap(
                "发现 openingHexWorldId；工具不能无损推导 openingSurfaceId。");
        if (root["definitions"] is JsonArray definitions)
            foreach (var node in definitions)
                if (node is JsonObject definition &&
                    string.Equals(OptionalString(definition, "type"), "hexWorld",
                        StringComparison.Ordinal))
                    throw UnsupportedLegacyMap(
                        $"发现 type=hexWorld（{OptionalString(definition, "id")}）；工具没有无损地图转换实现。");
    }

    private static ConversionException UnsupportedLegacyMap(string reason) =>
        new(reason +
            " 不生成输出。请使用现有 WorldComposer/SurfaceAuthoring Legacy migration 路径；" +
            "没有迁移样例时不得猜测，也不得声称已转换为 outdoorSurface。");

    private static bool ContainsProperty(JsonNode? node, string propertyName)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey(propertyName))
                return true;
            foreach (var pair in obj)
                if (ContainsProperty(pair.Value, propertyName))
                    return true;
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
                if (ContainsProperty(child, propertyName))
                    return true;
        }
        return false;
    }

    private static void ValidateCurrentSnapshotAuthority(
        JsonObject root, bool allowFormalArmyInput)
    {
        var entities = RequireArray(root, "entities", "Snapshot");
        for (var i = 0; i < entities.Count; i++)
        {
            if (entities[i] is not JsonObject entity)
                throw new ConversionException($"entities[{i}] 必须是 object。");
            if (!entity.TryGetPropertyValue("hasEntityLocation", out var locationNode) ||
                locationNode is not JsonValue locationValue ||
                !locationValue.TryGetValue<bool>(out _))
                throw new ConversionException(
                    $"entities[{i}] 缺少 current EntityLocation 字段 hasEntityLocation。");
        }

        var strategic = root["strategic"] as JsonObject
            ?? throw new ConversionException("Snapshot 输入缺少 strategic object。");
        RejectLegacySnapshotPayload(strategic, allowFormalArmyInput);

        var presences = RequireArray(
            strategic, "characterWorldPresences", "strategic current CharacterWorldPresence authority");
        var presenceIds = new HashSet<ulong>();
        for (var i = 0; i < presences.Count; i++)
        {
            if (presences[i] is not JsonObject presence)
                throw new ConversionException($"characterWorldPresences[{i}] 必须是 object。");
            var characterId = RequiredUlongString(
                presence, "characterId", $"characterWorldPresences[{i}]");
            var mode = RequiredInt(presence, "mode", $"characterWorldPresences[{i}]");
            if (characterId == 0 || !presenceIds.Add(characterId) || mode is not (3 or 4) ||
                !RequiredBool(presence, "hasWorldPosition",
                    $"characterWorldPresences[{i}]") ||
                string.IsNullOrWhiteSpace(OptionalString(presence, "personalSurfaceId")) ||
                !HasFiniteNumber(presence, "worldX") || !HasFiniteNumber(presence, "worldY"))
                throw new ConversionException(
                    $"characterWorldPresences[{i}] 不是 current exact Surface authority。");
        }

        ValidatePlayerPartyTravel(strategic);
        ValidateFactionFlags(strategic);
        ValidateRuntimeWorldSites(strategic);
        ValidateTerritoryClaims(strategic);
        ValidateSquadsAndMotions(strategic);
        ValidateCurrentEncounter(root);
    }

    private static void RejectLegacySnapshotPayload(
        JsonObject strategic, bool allowFormalArmyInput)
    {
        foreach (var name in new[]
                 {
                     "residualCharacterPresences", "territoryRegionControllers",
                     "retreatingArmies", "armyStacks", "stackMemberships"
                 })
        {
            if (strategic[name] is JsonArray values && values.Count > 0)
                throw new ConversionException($"strategic.{name} 是已退役 authority，拒绝转换。");
            if (strategic[name] is not null && strategic[name] is not JsonArray)
                throw new ConversionException($"strategic.{name} 结构无法安全识别。");
        }
        if (strategic["pendingEngagement"] is not null)
            throw new ConversionException(
                "strategic.pendingEngagement 是已退役 authority，拒绝转换。");

        if (!allowFormalArmyInput)
        {
            if (RequireArray(strategic, "formalArmies", "strategic").Count != 0 ||
                RequireArray(strategic, "armyMemberships", "strategic").Count != 0)
                throw new ConversionException("转换后仍含 FormalArmy authority。");
        }
    }

    private static void ValidatePlayerPartyTravel(JsonObject strategic)
    {
        if (strategic["playerPartyTravel"] is not JsonObject travel ||
            !RequiredBool(travel, "hasPosition", "strategic.playerPartyTravel") ||
            RequiredInt(travel, "locationKind", "strategic.playerPartyTravel") != 1 ||
            string.IsNullOrWhiteSpace(OptionalString(travel, "surfaceId")) ||
            !HasFiniteNumber(travel, "worldX") || !HasFiniteNumber(travel, "worldY"))
            throw new ConversionException(
                "strategic.playerPartyTravel 缺少 current exact Surface authority。");
        if (OptionalBool(travel, "isMoving") &&
            (!RequiredBool(travel, "hasContinuousPhysicalDestination",
                 "strategic.playerPartyTravel") ||
             !HasFiniteNumber(travel, "destinationWorldX") ||
             !HasFiniteNumber(travel, "destinationWorldY") ||
             RequiredInt(travel, "executionMode", "strategic.playerPartyTravel") != 3))
            throw new ConversionException(
                "moving playerPartyTravel 缺少 current Surface destination/executor。");
    }

    private static void ValidateFactionFlags(JsonObject strategic)
    {
        var flags = RequireArray(strategic, "factionFlags", "strategic current FactionFlags authority");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < flags.Count; i++)
        {
            if (flags[i] is not JsonObject flag ||
                RequiredInt(flag, "siteCoreFormat", $"factionFlags[{i}]") != 1 ||
                string.IsNullOrWhiteSpace(OptionalString(flag, "flagId")) ||
                !ids.Add(OptionalString(flag, "flagId")) ||
                string.IsNullOrWhiteSpace(OptionalString(flag, "surfaceId")) ||
                !RequiredBool(flag, "hasWorldPosition", $"factionFlags[{i}]") ||
                !HasFiniteNumber(flag, "worldX") || !HasFiniteNumber(flag, "worldY"))
                throw new ConversionException($"factionFlags[{i}] 不是 current SiteCore/Surface 格式。");
        }
    }

    private static void ValidateRuntimeWorldSites(JsonObject strategic)
    {
        var sites = RequireArray(
            strategic, "runtimeWorldSites", "strategic current RuntimeWorldSites authority");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < sites.Count; i++)
        {
            if (sites[i] is not JsonObject site ||
                RequiredInt(site, "coreLevelFormat", $"runtimeWorldSites[{i}]") != 1 ||
                string.IsNullOrWhiteSpace(OptionalString(site, "siteId")) ||
                !ids.Add(OptionalString(site, "siteId")) ||
                string.IsNullOrWhiteSpace(OptionalString(site, "coreAssetId")) ||
                string.IsNullOrWhiteSpace(OptionalString(site, "surfaceId")) ||
                !RequiredBool(site, "hasWorldPosition", $"runtimeWorldSites[{i}]") ||
                !HasFiniteNumber(site, "worldX") || !HasFiniteNumber(site, "worldY") ||
                RequiredInt(site, "coreLevel", $"runtimeWorldSites[{i}]") < 1)
                throw new ConversionException($"runtimeWorldSites[{i}] 不是 current exact Surface 格式。");
        }
    }

    private static void ValidateTerritoryClaims(JsonObject strategic)
    {
        if (!RequiredBool(strategic, "hasTerritoryClaimSnapshotAuthority", "strategic"))
            throw new ConversionException("strategic 缺少 current TerritoryClaim authority。");
        var claims = RequireArray(strategic, "territoryClaims", "strategic TerritoryClaims format 3");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < claims.Count; i++)
        {
            if (claims[i] is not JsonObject claim ||
                RequiredInt(claim, "formatVersion", $"territoryClaims[{i}]") != 3 ||
                string.IsNullOrWhiteSpace(OptionalString(claim, "claimId")) ||
                !ids.Add(OptionalString(claim, "claimId")) ||
                string.IsNullOrWhiteSpace(OptionalString(claim, "siteId")) ||
                string.IsNullOrWhiteSpace(OptionalString(claim, "surfaceId")) ||
                !HasFiniteNumber(claim, "centerX") || !HasFiniteNumber(claim, "centerY") ||
                !HasPositiveFiniteNumber(claim, "width") ||
                !HasPositiveFiniteNumber(claim, "height"))
                throw new ConversionException($"territoryClaims[{i}] 不是 current format 3。");
        }
    }

    private static void ValidateSquadsAndMotions(JsonObject strategic)
    {
        if (!RequiredBool(strategic, "hasSquadSnapshotAuthority", "strategic") ||
            !RequiredBool(strategic, "hasSquadWorldMotionSnapshotAuthority", "strategic"))
            throw new ConversionException("strategic 缺少 current Squads/Motions authority。");
        var squads = RequireArray(strategic, "squads", "strategic.Squads");
        var motions = RequireArray(strategic, "squadWorldMotions", "strategic.SquadWorldMotions");
        var squadIds = new HashSet<string>(StringComparer.Ordinal);
        var assignedMembers = new HashSet<ulong>();
        foreach (var node in squads)
        {
            if (node is not JsonObject squad)
                throw new ConversionException("strategic.squads 中存在非 object 项。");
            var id = RequiredString(squad, "squadId", "strategic.squads");
            var members = RequireArray(squad, "memberCharacterIds", id);
            var leader = RequiredUlongString(squad, "leaderCharacterId", id);
            var commandKind = OptionalInt(squad, "commandKind");
            if (!squadIds.Add(id) || members.Count == 0 || leader == 0 ||
                !ArrayContainsUlong(members, leader) ||
                commandKind is not (0 or 1 or 3) ||
                !string.IsNullOrEmpty(OptionalString(squad, "legacyArmyId")))
                throw new ConversionException($"{id}: current Squad authority 不完整或含旧 owner/command 值。");
            foreach (var memberNode in members)
            {
                var member = ReadUlongNode(memberNode, id + ".memberCharacterIds");
                if (member == 0 || !assignedMembers.Add(member))
                    throw new ConversionException(
                        $"{id}: current Squad member 为 0 或同时属于多个 Squad。");
            }
        }
        var motionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in motions)
        {
            if (node is not JsonObject motion)
                throw new ConversionException("strategic.squadWorldMotions 中存在非 object 项。");
            var id = RequiredString(motion, "squadId", "strategic.squadWorldMotions");
            var route = RequireArray(motion, "route", id);
            if (!squadIds.Contains(id) || !motionIds.Add(id) ||
                string.IsNullOrWhiteSpace(OptionalString(motion, "surfaceId")) ||
                !HasFiniteNumber(motion, "worldX") || !HasFiniteNumber(motion, "worldY") ||
                !HasFiniteNumber(motion, "destinationX") ||
                !HasFiniteNumber(motion, "destinationY") ||
                (OptionalBool(motion, "isMoving") && route.Count == 0))
                throw new ConversionException($"{id}: current SquadWorldMotion authority 不完整。");
            foreach (var point in route)
                if (point is not JsonObject p ||
                    !HasFiniteNumber(p, "x") || !HasFiniteNumber(p, "y"))
                    throw new ConversionException($"{id}: current motion route 不完整。");
        }
    }

    private static void ValidateCurrentEncounter(JsonObject root)
    {
        if (root["characterEncounter"] is null)
            return;
        if (root["characterEncounter"] is not JsonObject encounter ||
            RequiredInt(encounter, "version", "characterEncounter") != 4 ||
            string.IsNullOrWhiteSpace(OptionalString(encounter, "sourceSurfaceId")) ||
            !HasFiniteNumber(encounter, "centerX") || !HasFiniteNumber(encounter, "centerY") ||
            !HasPositiveFiniteNumber(encounter, "width") ||
            !HasPositiveFiniteNumber(encounter, "height"))
            throw new ConversionException("characterEncounter 不是 current format 4 exact Surface 格式。");
        ValidateEncounterPeople(
            RequireArray(encounter, "participants", "characterEncounter"), "participants");
        var candidates = RequireArray(encounter, "candidates", "characterEncounter");
        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] is not JsonObject candidate)
                throw new ConversionException($"characterEncounter.candidates[{i}] 必须是 object。");
            ValidateEncounterPeople(
                RequireArray(candidate, "members", $"characterEncounter.candidates[{i}]"),
                $"candidates[{i}].members");
        }
    }

    private static void ValidateEncounterPeople(JsonArray people, string context)
    {
        for (var i = 0; i < people.Count; i++)
        {
            if (people[i] is not JsonObject person)
                throw new ConversionException(
                    $"characterEncounter.{context}[{i}] 必须是 object。");
            var ownerKind = RequiredInt(
                person, "sourceSpatialOwnerKind", $"{context}[{i}]");
            var sourceMode = RequiredInt(person, "sourceMode", $"{context}[{i}]");
            if (!person.ContainsKey("sourceFormalArmyId") ||
                ownerKind is not (0 or 1) ||
                (ownerKind == 1 &&
                 string.IsNullOrWhiteSpace(OptionalString(person, "sourceSquadId"))) ||
                !string.IsNullOrEmpty(OptionalString(person, "sourceFormalArmyId")) ||
                sourceMode is not (3 or 4) ||
                !HasFiniteNumber(person, "originX") || !HasFiniteNumber(person, "originY") ||
                !HasFiniteNumber(person, "returnX") || !HasFiniteNumber(person, "returnY") ||
                !HasFiniteNumber(person, "tacticalX") || !HasFiniteNumber(person, "tacticalY"))
                throw new ConversionException(
                    $"characterEncounter.{context}[{i}] 含旧 owner 值或不完整 current 空间字段。");
        }
    }

    private static void RejectUnsafeLegacyRelations(JsonObject strategic)
    {
        if (strategic["pendingEngagement"] is JsonObject)
            throw new ConversionException(
                "strategic.pendingEngagement 存在；其中 Army/Stack 冻结关系无法安全离线转换。");
        foreach (var name in new[] { "armyStacks", "stackMemberships", "retreatingArmies" })
        {
            if (strategic[name] is JsonArray values && values.Count > 0)
                throw new ConversionException($"strategic.{name} 含未安全转换的 Army/Stack 关系。");
            if (strategic[name] is not null && strategic[name] is not JsonArray)
                throw new ConversionException($"strategic.{name} 结构无法安全识别。");
        }
    }

    private static Dictionary<string, List<ulong>> ReadMemberships(JsonArray memberships)
    {
        var result = new Dictionary<string, List<ulong>>(StringComparer.Ordinal);
        foreach (var node in memberships)
        {
            if (node is not JsonObject membership)
                throw new ConversionException("armyMemberships 中存在非 object 项。");
            var armyId = RequiredString(membership, "armyId", "armyMembership");
            var characterId = RequiredUlongString(membership, "characterId", armyId);
            if (characterId == 0)
                throw new ConversionException($"{armyId}: armyMembership characterId 不能为 0。");
            if (!result.TryGetValue(armyId, out var members))
                result.Add(armyId, members = []);
            if (!members.Contains(characterId))
                members.Add(characterId);
        }
        return result;
    }

    private static List<ulong> CollectSnapshotMembers(
        JsonObject army, List<ulong>? memberships, string armyId)
    {
        var result = new List<ulong>();
        if (army["memberCharacterIds"] is JsonNode memberNode)
        {
            if (memberNode is not JsonArray members)
                throw new ConversionException($"{armyId}: memberCharacterIds 必须是 array。");
            foreach (var node in members)
            {
                var id = ReadUlongNode(node, armyId + ".memberCharacterIds");
                if (id == 0)
                    throw new ConversionException($"{armyId}: memberCharacterIds 不能包含 0。");
                if (!result.Contains(id))
                    result.Add(id);
            }
        }
        if (memberships is not null)
            foreach (var id in memberships)
                if (!result.Contains(id))
                    result.Add(id);
        if (result.Count == 0)
            throw new ConversionException($"{armyId}: 无成员，禁止生成空小队。");
        return result;
    }

    private static void ValidateExistingCurrentPair(
        JsonObject squad, JsonObject motion, List<ulong> legacyMembers, ulong leader, string armyId)
    {
        var currentMembers = CollectSnapshotMembers(squad, null, armyId + " current Squad");
        foreach (var member in legacyMembers)
            if (!currentMembers.Contains(member))
                throw new ConversionException($"{armyId}: 已有 current Squad 丢失 legacy 成员 {member}。");
        if (RequiredUlongString(squad, "leaderCharacterId", armyId) != leader)
            throw new ConversionException($"{armyId}: 已有 current Squad leader 与 legacy 不一致。");
        RequiredString(motion, "surfaceId", armyId + " current motion");
        RequiredFiniteDouble(motion, "worldX", armyId + " current motion");
        RequiredFiniteDouble(motion, "worldY", armyId + " current motion");
        RequiredFiniteDouble(motion, "destinationX", armyId + " current motion");
        RequiredFiniteDouble(motion, "destinationY", armyId + " current motion");
        var route = OptionalArray(motion, "route", armyId + " current motion");
        foreach (var node in route)
        {
            if (node is not JsonObject point)
                throw new ConversionException($"{armyId}: 已有 current motion route 含非 object 项。");
            RequiredFiniteDouble(point, "x", armyId + " current motion route");
            RequiredFiniteDouble(point, "y", armyId + " current motion route");
        }
        if (OptionalBool(motion, "isMoving"))
        {
            var waypointIndex = OptionalInt(motion, "waypointIndex");
            if (route.Count == 0 || waypointIndex < 0 || waypointIndex >= route.Count)
                throw new ConversionException($"{armyId}: 已有 current moving motion route 不完整。");
        }
    }

    private static Dictionary<string, JsonObject> IndexByStringId(
        JsonArray values, string field, string context)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in values)
        {
            if (node is not JsonObject item)
                throw new ConversionException(context + " 中存在非 object 项。");
            var id = RequiredString(item, field, context);
            if (!result.TryAdd(id, item))
                throw new ConversionException($"{context} 含重复 {field}：{id}");
        }
        return result;
    }

    private static void ValidateContentMembers(string id, JsonArray members)
    {
        if (members.Count == 0)
            throw new ConversionException($"{id}: members 不能为空。");
        var leaderCount = 0;
        foreach (var node in members)
        {
            if (node is not JsonObject member)
                throw new ConversionException($"{id}: members 中存在非 object 项。");
            RequiredString(member, "characterDefinitionId", id + ".member");
            if (OptionalBool(member, "leader"))
                leaderCount++;
        }
        if (leaderCount != 1)
            throw new ConversionException($"{id}: members 必须恰有一个 leader。");
    }

    private static void AppendUniqueStrings(
        JsonArray source, JsonArray destination, HashSet<string> seen, string context)
    {
        foreach (var node in source)
        {
            if (node is not JsonValue value || !value.TryGetValue<string>(out var id) ||
                string.IsNullOrWhiteSpace(id))
                throw new ConversionException(context + " 必须只包含非空字符串。");
            if (seen.Add(id))
                destination.Add(id);
        }
    }

    private static JsonArray OptionalArray(JsonObject value, string name, string context)
    {
        if (value[name] is null)
            return new JsonArray();
        return value[name] as JsonArray
            ?? throw new ConversionException($"{context}.{name} 必须是 array。");
    }

    private static string RequiredString(JsonObject value, string name, string context)
    {
        var result = OptionalString(value, name);
        if (string.IsNullOrWhiteSpace(result))
            throw new ConversionException($"{context}: 缺少非空 {name}。");
        return result;
    }

    private static string OptionalString(JsonObject value, string name)
    {
        if (value[name] is not JsonValue scalar || !scalar.TryGetValue<string>(out var result))
            return string.Empty;
        return result ?? string.Empty;
    }

    private static bool OptionalBool(JsonObject value, string name) =>
        value[name] is JsonValue scalar && scalar.TryGetValue<bool>(out var result) && result;

    private static bool RequiredBool(JsonObject value, string name, string context)
    {
        if (value[name] is JsonValue scalar && scalar.TryGetValue<bool>(out var result))
            return result;
        throw new ConversionException($"{context}: 缺少 bool {name}。");
    }

    private static int OptionalInt(JsonObject value, string name) =>
        value[name] is JsonValue scalar && scalar.TryGetValue<int>(out var result) ? result : 0;

    private static int RequiredInt(JsonObject value, string name, string context)
    {
        if (value[name] is JsonValue scalar && scalar.TryGetValue<int>(out var result))
            return result;
        throw new ConversionException($"{context}: 缺少整数 {name}。");
    }

    private static bool HasFiniteNumber(JsonObject value, string name) =>
        value[name] is JsonValue scalar &&
        scalar.TryGetValue<double>(out var result) &&
        double.IsFinite(result);

    private static bool HasPositiveFiniteNumber(JsonObject value, string name) =>
        value[name] is JsonValue scalar &&
        scalar.TryGetValue<double>(out var result) &&
        double.IsFinite(result) && result > 0;

    private static JsonArray RequireArray(JsonObject value, string name, string context) =>
        value[name] as JsonArray
        ?? throw new ConversionException($"{context}: 缺少 array {name}。");

    private static bool ArrayContainsUlong(JsonArray values, ulong expected)
    {
        foreach (var value in values)
            if (ReadUlongNode(value, "ulong array") == expected)
                return true;
        return false;
    }

    private static double RequiredFiniteDouble(JsonObject value, string name, string context)
    {
        if (value[name] is JsonValue scalar &&
            scalar.TryGetValue<double>(out var result) &&
            double.IsFinite(result))
            return result;
        throw new ConversionException($"{context}: 缺少有限数值 {name}。");
    }

    private static double OptionalFiniteDouble(JsonObject value, string name)
    {
        if (value[name] is null)
            return 0d;
        return RequiredFiniteDouble(value, name, value.ToString());
    }

    private static ulong RequiredUlongString(JsonObject value, string name, string context)
    {
        if (value[name] is null)
            throw new ConversionException($"{context}: 缺少 {name}。");
        return ReadUlongNode(value[name], context + "." + name);
    }

    private static ulong ReadUlongNode(JsonNode? node, string context)
    {
        if (node is JsonValue scalar)
        {
            if (scalar.TryGetValue<string>(out var text) &&
                ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var stringValue))
                return stringValue;
            if (scalar.TryGetValue<ulong>(out var numberValue))
                return numberValue;
            if (scalar.TryGetValue<long>(out var signedValue) && signedValue >= 0)
                return (ulong)signedValue;
        }
        throw new ConversionException($"{context}: 必须是 ulong 十进制字符串或非负整数。");
    }

    private static JsonValue UlongNode(ulong value) =>
        JsonValue.Create(value.ToString(CultureInfo.InvariantCulture))!;

    private static JsonArray ToUlongArray(IEnumerable<ulong> values)
    {
        var result = new JsonArray();
        foreach (var value in values)
            result.Add(UlongNode(value));
        return result;
    }

    private static void RequireHexOptions(Options options, string context)
    {
        if (string.IsNullOrWhiteSpace(options.SurfaceId) || options.MovementScale is null)
            throw new ConversionException(
                $"{context}: 只有 Hex 位置；必须同时提供 --surface-id 与 --movement-scale。");
        if (!double.IsFinite(options.MovementScale.Value) || options.MovementScale.Value <= 0)
            throw new ConversionException("--movement-scale 必须是大于 0 的有限数值。");
    }

    private static (double X, double Y) HexToWorld(int q, int r, double scale) =>
        (scale * Math.Sqrt(3d) * (q + 0.5d * (r & 1)), scale * 1.5d * r);

    private static int RunSelfTest()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "LegacyRuntimeConverter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var passed = 0;
            var contentInput = Path.Combine(root, "formal-content.json");
            var contentOutput = Path.Combine(root, "formal-content-out.json");
            File.WriteAllText(contentInput, FormalArmyContentFixture(), Encoding.UTF8);
            RequireSelfTest(
                Main(["content", "--input", contentInput, "--output", contentOutput]) == 0 &&
                File.Exists(contentOutput) &&
                File.ReadAllText(contentOutput).Contains("\"type\": \"npcSquad\"",
                    StringComparison.Ordinal),
                "formalArmy content 成功");
            passed++;

            var hexInput = Path.Combine(root, "hex-content.json");
            var hexOutput = Path.Combine(root, "hex-content-out.json");
            File.WriteAllText(hexInput,
                """{"schemaVersion":1,"definitions":[{"id":"legacy:world","type":"hexWorld"}]}""",
                Encoding.UTF8);
            RequireSelfTest(
                Main(["content", "--input", hexInput, "--output", hexOutput]) == 2 &&
                !File.Exists(hexOutput),
                "hexWorld content 失败且无输出");
            passed++;

            var snapshotInput = Path.Combine(root, "hybrid-snapshot.json");
            var snapshotOutput = Path.Combine(root, "hybrid-snapshot-out.json");
            File.WriteAllText(snapshotInput, HybridSnapshotFixture(), Encoding.UTF8);
            RequireSelfTest(
                Main(["snapshot", "--input", snapshotInput, "--output", snapshotOutput]) == 0 &&
                File.Exists(snapshotOutput) &&
                File.ReadAllText(snapshotOutput).Contains(
                    "\"squadId\": \"squad:army:army-1\"", StringComparison.Ordinal),
                "完整 hybrid snapshot 成功");
            passed++;

            var incomplete = (JsonObject)JsonNode.Parse(HybridSnapshotFixture())!;
            ((JsonObject)incomplete["strategic"]!).Remove("factionFlags");
            var incompleteInput = Path.Combine(root, "incomplete-snapshot.json");
            var incompleteOutput = Path.Combine(root, "incomplete-snapshot-out.json");
            File.WriteAllText(incompleteInput, incomplete.ToJsonString(), Encoding.UTF8);
            RequireSelfTest(
                Main(["snapshot", "--input", incompleteInput, "--output", incompleteOutput]) == 2 &&
                !File.Exists(incompleteOutput),
                "缺 current authority snapshot 失败且无输出");
            passed++;

            var overwriteOutput = Path.Combine(root, "existing.json");
            File.WriteAllText(overwriteOutput, "sentinel", Encoding.UTF8);
            RequireSelfTest(
                Main(["content", "--input", contentInput, "--output", overwriteOutput]) == 2 &&
                File.ReadAllText(overwriteOutput, Encoding.UTF8) == "sentinel",
                "拒绝覆盖");
            passed++;

            Console.WriteLine($"LegacyRuntimeConverter self-test: {passed}/5 passed.");
            return 0;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { /* best-effort test cleanup */ }
        }
    }

    private static void RequireSelfTest(bool condition, string name)
    {
        if (!condition)
            throw new ConversionException("self-test failed: " + name);
        Console.WriteLine("[PASS] " + name);
    }

    private static string FormalArmyContentFixture() =>
        """
        {
          "schemaVersion": 1,
          "definitions": [
            {
              "id": "legacy:army",
              "type": "formalArmy",
              "factionId": "base:faction_test",
              "assemblySiteId": "base:site_test",
              "initialSurfacePosition": {
                "surfaceId": "base:surface_test",
                "worldX": 1,
                "worldY": 2
              },
              "members": [
                { "characterDefinitionId": "base:character_test", "leader": true }
              ]
            },
            {
              "id": "legacy:scenario",
              "type": "openingScenario",
              "initialFormalArmyIds": [ "legacy:army" ]
            }
          ]
        }
        """;

    private static string HybridSnapshotFixture() =>
        """
        {
          "schemaVersion": 6,
          "entities": [
            { "id": "1", "hasEntityLocation": false }
          ],
          "strategic": {
            "hasSquadSnapshotAuthority": true,
            "hasSquadWorldMotionSnapshotAuthority": true,
            "squads": [],
            "squadWorldMotions": [],
            "formalArmies": [
              {
                "armyId": "army-1",
                "factionId": "base:faction_test",
                "leaderCharacterId": "1",
                "memberCharacterIds": [ "1" ],
                "state": 0,
                "surfaceId": "base:surface_test",
                "worldX": 1,
                "worldY": 2
              }
            ],
            "armyMemberships": [],
            "residualCharacterPresences": [],
            "territoryRegionControllers": [],
            "retreatingArmies": [],
            "characterWorldPresences": [
              {
                "characterId": "1",
                "mode": 4,
                "siteId": "",
                "hasWorldPosition": true,
                "personalSurfaceId": "base:surface_test",
                "worldX": 1,
                "worldY": 2
              }
            ],
            "playerPartyTravel": {
              "hasPosition": true,
              "locationKind": 1,
              "surfaceId": "base:surface_test",
              "worldX": 1,
              "worldY": 2,
              "isMoving": false,
              "executionMode": 0
            },
            "factionFlags": [],
            "runtimeWorldSites": [],
            "hasTerritoryClaimSnapshotAuthority": true,
            "territoryClaims": []
          }
        }
        """;

    private static void WriteNewFileAtomically(string outputPath, string content)
    {
        var directory = Path.GetDirectoryName(outputPath)!;
        var tempPath = Path.Combine(directory, "." + Path.GetFileName(outputPath) +
                                               "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(
                       tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       64 * 1024, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
            File.Move(tempPath, outputPath, false);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private sealed record Options(
        string Mode,
        string InputPath,
        string OutputPath,
        string? SurfaceId,
        double? MovementScale)
    {
        public static Options Parse(string[] args)
        {
            if (args.Length == 0 || args[0] is "-h" or "--help")
                throw new ConversionException(
                    "用法：LegacyRuntimeConverter <content|snapshot> --input <path> --output <path> " +
                    "[--surface-id <id> --movement-scale <number>]");
            var mode = args[0];
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 1; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ConversionException("参数必须使用 --name value 成对形式。");
                if (!values.TryAdd(args[i], args[i + 1]))
                    throw new ConversionException("重复参数：" + args[i]);
            }
            if (!values.TryGetValue("--input", out var input) || string.IsNullOrWhiteSpace(input) ||
                !values.TryGetValue("--output", out var output) || string.IsNullOrWhiteSpace(output))
                throw new ConversionException("必须提供 --input 与 --output。");
            double? movementScale = null;
            if (values.TryGetValue("--movement-scale", out var scaleText))
            {
                if (!double.TryParse(
                        scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
                    throw new ConversionException("--movement-scale 必须使用 invariant 数字格式。");
                movementScale = scale;
            }
            foreach (var name in values.Keys)
                if (name is not ("--input" or "--output" or "--surface-id" or "--movement-scale"))
                    throw new ConversionException("未知参数：" + name);
            values.TryGetValue("--surface-id", out var surfaceId);
            return new Options(mode, input, output, surfaceId, movementScale);
        }
    }

    private sealed class ConversionException(string message) : Exception(message);
}
