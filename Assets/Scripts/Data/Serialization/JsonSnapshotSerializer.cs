using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Persistence;
using XianXia.Core.Results;
using XianXia.Data.Serialization;

namespace XianXia.Data.Serialization
{
    public sealed class JsonSnapshotSerializer : ISnapshotSerializer
    {
        public Result<string> Serialize(WorldSnapshot snapshot)
        {
            if (snapshot == null)
                return Result.Fail<string>(ErrorCode.SnapshotInvalid, "Snapshot is null.");

            var root = new Dictionary<string, JsonValue>(System.StringComparer.Ordinal)
            {
                ["schemaVersion"] = JsonValue.FromNumber(snapshot.SchemaVersion),
                ["snapshotId"] = U(snapshot.SnapshotId),
                ["worldTick"] = U(snapshot.WorldTick),
                ["regionId"] = U(snapshot.RegionId),
                ["enabledPackageId"] = JsonValue.FromString(snapshot.EnabledPackageId ?? string.Empty),
                ["enabledPackageVersion"] = JsonValue.FromString(snapshot.EnabledPackageVersion ?? string.Empty),
                ["randomS0"] = U(snapshot.RandomS0),
                ["randomS1"] = U(snapshot.RandomS1),
                ["randomStreamId"] = U(snapshot.RandomStreamId),
                ["eventCursor"] = JsonValue.FromNumber(snapshot.EventCursor),
                ["nextEventId"] = U(snapshot.NextEventId),
                ["nextEntityId"] = U(snapshot.NextEntityId),
                ["nextOrderId"] = U(snapshot.NextOrderId),
                ["nextActionId"] = U(snapshot.NextActionId),
                ["nextModifierId"] = U(snapshot.NextModifierId),
                ["entities"] = JsonValue.FromArray(SerializeEntities(snapshot.Entities)),
                ["activeActions"] = JsonValue.FromArray(SerializeActions(snapshot.ActiveActions)),
                ["orders"] = JsonValue.FromArray(SerializeOrders(snapshot.Orders)),
                ["schedules"] = JsonValue.FromArray(SerializeSchedules(snapshot.Schedules))
            };

            return Result.Ok(SimpleJson.Stringify(JsonValue.FromObject(root)));
        }

        public Result<WorldSnapshot> Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return Result.Fail<WorldSnapshot>(ErrorCode.SnapshotInvalid, "JSON is empty.");

            try
            {
                var root = SimpleJson.Parse(json);
                var snapshot = new WorldSnapshot
                {
                    SchemaVersion = (int)root.GetNumber("schemaVersion"),
                    SnapshotId = ReadU(root, "snapshotId"),
                    WorldTick = ReadU(root, "worldTick"),
                    RegionId = ReadU(root, "regionId"),
                    EnabledPackageId = root.GetString("enabledPackageId", string.Empty),
                    EnabledPackageVersion = root.GetString("enabledPackageVersion", string.Empty),
                    RandomS0 = ReadU(root, "randomS0"),
                    RandomS1 = ReadU(root, "randomS1"),
                    RandomStreamId = ReadU(root, "randomStreamId"),
                    EventCursor = (int)root.GetNumber("eventCursor"),
                    NextEventId = ReadU(root, "nextEventId"),
                    NextEntityId = ReadU(root, "nextEntityId"),
                    NextOrderId = ReadU(root, "nextOrderId"),
                    NextActionId = ReadU(root, "nextActionId"),
                    NextModifierId = ReadU(root, "nextModifierId")
                };

                if (root.TryGetProperty("entities", out var entities) && entities.Kind == JsonValueKind.Array)
                {
                    foreach (var e in entities.Array)
                        snapshot.Entities.Add(ReadEntity(e));
                }

                if (root.TryGetProperty("activeActions", out var actions) && actions.Kind == JsonValueKind.Array)
                {
                    foreach (var a in actions.Array)
                        snapshot.ActiveActions.Add(ReadAction(a));
                }

                if (root.TryGetProperty("orders", out var orders) && orders.Kind == JsonValueKind.Array)
                {
                    foreach (var o in orders.Array)
                        snapshot.Orders.Add(ReadOrder(o));
                }

                if (root.TryGetProperty("schedules", out var schedules) && schedules.Kind == JsonValueKind.Array)
                {
                    foreach (var s in schedules.Array)
                        snapshot.Schedules.Add(ReadSchedule(s));
                }

                return Result.Ok(snapshot);
            }
            catch (System.Exception ex)
            {
                return Result.Fail<WorldSnapshot>(ErrorCode.SnapshotInvalid, "JSON parse failed.", ex.Message);
            }
        }

        static List<JsonValue> SerializeEntities(List<EntitySnapshotDto> entities)
        {
            var list = new List<JsonValue>();
            foreach (var e in entities)
            {
                var bases = new List<JsonValue>();
                foreach (var b in e.Bases)
                {
                    bases.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["attributeId"] = JsonValue.FromNumber(b.AttributeId),
                        ["value"] = JsonValue.FromNumber(b.Value)
                    }));
                }

                var mods = new List<JsonValue>();
                foreach (var m in e.Modifiers)
                {
                    mods.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["id"] = JsonValue.FromNumber(m.Id),
                        ["attributeId"] = JsonValue.FromNumber(m.AttributeId),
                        ["operation"] = JsonValue.FromNumber(m.Operation),
                        ["value"] = JsonValue.FromNumber(m.Value),
                        ["sourceKind"] = JsonValue.FromNumber(m.SourceKind),
                        ["sourceDefinitionId"] = JsonValue.FromString(m.SourceDefinitionId ?? string.Empty),
                        ["sourceEntityId"] = JsonValue.FromNumber(m.SourceEntityId),
                        ["hasSourceEntity"] = JsonValue.FromBool(m.HasSourceEntity),
                        ["sourceModifierId"] = JsonValue.FromNumber(m.SourceModifierId),
                        ["hasSourceModifier"] = JsonValue.FromBool(m.HasSourceModifier)
                    }));
                }

                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["id"] = JsonValue.FromNumber(e.Id),
                    ["definitionId"] = JsonValue.FromString(e.DefinitionId ?? string.Empty),
                    ["displayName"] = JsonValue.FromString(e.DisplayName ?? string.Empty),
                    ["tags"] = JsonValue.FromNumber(e.Tags),
                    ["lifecycle"] = JsonValue.FromNumber(e.Lifecycle),
                    ["bases"] = JsonValue.FromArray(bases),
                    ["modifiers"] = JsonValue.FromArray(mods),
                    ["activeActionId"] = JsonValue.FromNumber(e.ActiveActionId),
                    ["activeTotalTicks"] = JsonValue.FromNumber(e.ActiveTotalTicks),
                    ["activeRemainingTicks"] = JsonValue.FromNumber(e.ActiveRemainingTicks),
                    ["hasActiveClock"] = JsonValue.FromBool(e.HasActiveClock),
                    ["hasCultivation"] = JsonValue.FromBool(e.HasCultivation),
                    ["realm"] = JsonValue.FromNumber(e.Realm),
                    ["cultivationProgress"] = JsonValue.FromNumber(e.CultivationProgress),
                    ["breakthroughProgressRequired"] = JsonValue.FromNumber(e.BreakthroughProgressRequired),
                    ["cultivationSpeed"] = JsonValue.FromNumber(e.CultivationSpeed),
                    ["learnedManualId"] = JsonValue.FromString(e.LearnedManualId ?? string.Empty),
                    ["requiredRealmName"] = JsonValue.FromString(e.RequiredRealmName ?? string.Empty),
                    ["hasDailyTask"] = JsonValue.FromBool(e.HasDailyTask),
                    ["laborProgress"] = JsonValue.FromNumber(e.LaborProgress),
                    ["laborQuota"] = JsonValue.FromNumber(e.LaborQuota),
                    ["hasSchedule"] = JsonValue.FromBool(e.HasSchedule),
                    ["scheduleDefinitionId"] = JsonValue.FromString(e.ScheduleDefinitionId ?? string.Empty)
                }));
            }
            return list;
        }

        static List<JsonValue> SerializeSchedules(List<ScheduleDefinitionSnapshotDto> schedules)
        {
            var list = new List<JsonValue>();
            if (schedules == null)
                return list;
            foreach (var s in schedules)
            {
                var blocks = new List<JsonValue>();
                if (s.Blocks != null)
                {
                    foreach (var b in s.Blocks)
                    {
                        blocks.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                        {
                            ["startTickInDay"] = JsonValue.FromNumber(b.StartTickInDay),
                            ["endTickInDay"] = JsonValue.FromNumber(b.EndTickInDay),
                            ["activity"] = JsonValue.FromNumber(b.Activity),
                            ["orderDurationTicks"] = JsonValue.FromNumber(b.OrderDurationTicks)
                        }));
                    }
                }

                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["id"] = JsonValue.FromString(s.Id ?? string.Empty),
                    ["blocks"] = JsonValue.FromArray(blocks)
                }));
            }

            return list;
        }

        static List<JsonValue> SerializeActions(List<ActiveActionSnapshotDto> actions)
        {
            var list = new List<JsonValue>();
            foreach (var a in actions)
            {
                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["id"] = JsonValue.FromNumber(a.Id),
                    ["subjectId"] = JsonValue.FromNumber(a.SubjectId),
                    ["sourceOrderId"] = JsonValue.FromNumber(a.SourceOrderId),
                    ["kind"] = JsonValue.FromString(a.Kind ?? "Wait"),
                    ["status"] = JsonValue.FromNumber(a.Status),
                    ["totalTicks"] = JsonValue.FromNumber(a.TotalTicks),
                    ["remainingTicks"] = JsonValue.FromNumber(a.RemainingTicks)
                }));
            }
            return list;
        }

        static List<JsonValue> SerializeOrders(List<OrderSnapshotDto> orders)
        {
            var list = new List<JsonValue>();
            foreach (var o in orders)
            {
                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["id"] = JsonValue.FromNumber(o.Id),
                    ["subjectId"] = JsonValue.FromNumber(o.SubjectId),
                    ["type"] = JsonValue.FromNumber(o.Type),
                    ["source"] = JsonValue.FromNumber(o.Source),
                    ["waitTicks"] = JsonValue.FromNumber(o.WaitTicks)
                }));
            }
            return list;
        }

        static EntitySnapshotDto ReadEntity(JsonValue e)
        {
            var dto = new EntitySnapshotDto
            {
                Id = (ulong)e.GetNumber("id"),
                DefinitionId = e.GetString("definitionId", string.Empty),
                DisplayName = e.GetString("displayName", string.Empty),
                Tags = (int)e.GetNumber("tags"),
                Lifecycle = (int)e.GetNumber("lifecycle"),
                ActiveActionId = (ulong)e.GetNumber("activeActionId"),
                ActiveTotalTicks = (ulong)e.GetNumber("activeTotalTicks"),
                ActiveRemainingTicks = (ulong)e.GetNumber("activeRemainingTicks"),
                HasActiveClock = e.TryGetProperty("hasActiveClock", out var hac) && hac.Kind == JsonValueKind.Boolean && hac.Bool,
                HasCultivation = e.TryGetProperty("hasCultivation", out var hc) && hc.Kind == JsonValueKind.Boolean && hc.Bool,
                Realm = (int)e.GetNumber("realm"),
                CultivationProgress = (int)e.GetNumber("cultivationProgress"),
                BreakthroughProgressRequired = (int)e.GetNumber("breakthroughProgressRequired"),
                CultivationSpeed = (int)e.GetNumber("cultivationSpeed"),
                LearnedManualId = e.GetString("learnedManualId", string.Empty),
                RequiredRealmName = e.GetString("requiredRealmName", string.Empty),
                HasDailyTask = e.TryGetProperty("hasDailyTask", out var hdt) && hdt.Kind == JsonValueKind.Boolean && hdt.Bool,
                LaborProgress = (int)e.GetNumber("laborProgress"),
                LaborQuota = (int)e.GetNumber("laborQuota"),
                HasSchedule = e.TryGetProperty("hasSchedule", out var hs) && hs.Kind == JsonValueKind.Boolean && hs.Bool,
                ScheduleDefinitionId = e.GetString("scheduleDefinitionId", string.Empty)
            };

            if (e.TryGetProperty("bases", out var bases) && bases.Kind == JsonValueKind.Array)
            {
                foreach (var b in bases.Array)
                {
                    dto.Bases.Add(new AttrBaseDto
                    {
                        AttributeId = (int)b.GetNumber("attributeId"),
                        Value = (int)b.GetNumber("value")
                    });
                }
            }

            if (e.TryGetProperty("modifiers", out var mods) && mods.Kind == JsonValueKind.Array)
            {
                foreach (var m in mods.Array)
                {
                    dto.Modifiers.Add(new ModifierSnapshotDto
                    {
                        Id = (ulong)m.GetNumber("id"),
                        AttributeId = (int)m.GetNumber("attributeId"),
                        Operation = (int)m.GetNumber("operation"),
                        Value = m.GetNumber("value"),
                        SourceKind = (int)m.GetNumber("sourceKind"),
                        SourceDefinitionId = m.GetString("sourceDefinitionId", string.Empty),
                        SourceEntityId = (ulong)m.GetNumber("sourceEntityId"),
                        HasSourceEntity = m.TryGetProperty("hasSourceEntity", out var hse) && hse.Kind == JsonValueKind.Boolean && hse.Bool,
                        SourceModifierId = (ulong)m.GetNumber("sourceModifierId"),
                        HasSourceModifier = m.TryGetProperty("hasSourceModifier", out var hsm) && hsm.Kind == JsonValueKind.Boolean && hsm.Bool
                    });
                }
            }

            return dto;
        }

        static ActiveActionSnapshotDto ReadAction(JsonValue a) => new ActiveActionSnapshotDto
        {
            Id = (ulong)a.GetNumber("id"),
            SubjectId = (ulong)a.GetNumber("subjectId"),
            SourceOrderId = (ulong)a.GetNumber("sourceOrderId"),
            Kind = a.GetString("kind", "Wait"),
            Status = (int)a.GetNumber("status"),
            TotalTicks = (ulong)a.GetNumber("totalTicks"),
            RemainingTicks = (ulong)a.GetNumber("remainingTicks")
        };

        static JsonValue U(ulong value) => JsonValue.FromString(value.ToString(CultureInfo.InvariantCulture));

        static ulong ReadU(JsonValue root, string name)
        {
            if (!root.TryGetProperty(name, out var v)) return 0;
            if (v.Kind == JsonValueKind.String && ulong.TryParse(v.String, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            if (v.Kind == JsonValueKind.Number)
                return (ulong)v.Number;
            return 0;
        }

        static OrderSnapshotDto ReadOrder(JsonValue o) => new OrderSnapshotDto
        {
            Id = (ulong)o.GetNumber("id"),
            SubjectId = (ulong)o.GetNumber("subjectId"),
            Type = (int)o.GetNumber("type"),
            Source = (int)o.GetNumber("source"),
            WaitTicks = (ulong)o.GetNumber("waitTicks")
        };

        static ScheduleDefinitionSnapshotDto ReadSchedule(JsonValue s)
        {
            var dto = new ScheduleDefinitionSnapshotDto
            {
                Id = s.GetString("id", string.Empty)
            };
            if (s.TryGetProperty("blocks", out var blocks) && blocks.Kind == JsonValueKind.Array)
            {
                foreach (var b in blocks.Array)
                {
                    dto.Blocks.Add(new ScheduleBlockSnapshotDto
                    {
                        StartTickInDay = (int)b.GetNumber("startTickInDay"),
                        EndTickInDay = (int)b.GetNumber("endTickInDay"),
                        Activity = (int)b.GetNumber("activity"),
                        OrderDurationTicks = (ulong)b.GetNumber("orderDurationTicks")
                    });
                }
            }

            return dto;
        }
    }
}

