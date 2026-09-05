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
                ["schedules"] = JsonValue.FromArray(SerializeSchedules(snapshot.Schedules)),
                ["opportunitySites"] = JsonValue.FromArray(SerializeOpportunitySites(snapshot.OpportunitySites)),
                ["manuals"] = JsonValue.FromArray(SerializeManuals(snapshot.Manuals)),
                ["observationDiscoverChancePercent"] = JsonValue.FromNumber(snapshot.ObservationDiscoverChancePercent),
                ["partyInventorySlots"] = JsonValue.FromArray(SerializePartyInventorySlots(snapshot.PartyInventorySlots)),
                ["relationshipEvents"] = JsonValue.FromArray(SerializeRelationshipEvents(snapshot.RelationshipEvents)),
                ["strategic"] = SerializeStrategic(snapshot.Strategic)
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

                if (root.TryGetProperty("opportunitySites", out var sites) && sites.Kind == JsonValueKind.Array)
                {
                    foreach (var s in sites.Array)
                        snapshot.OpportunitySites.Add(ReadOpportunitySite(s));
                }

                if (root.TryGetProperty("manuals", out var manuals) && manuals.Kind == JsonValueKind.Array)
                {
                    foreach (var m in manuals.Array)
                        snapshot.Manuals.Add(ReadManual(m));
                }

                if (root.TryGetProperty("observationDiscoverChancePercent", out var chance) &&
                    chance.Kind == JsonValueKind.Number)
                {
                    snapshot.ObservationDiscoverChancePercent = (int)chance.Number;
                }

                if (root.TryGetProperty("partyInventorySlots", out var invSlots) &&
                    invSlots.Kind == JsonValueKind.Array)
                {
                    foreach (var slot in invSlots.Array)
                    {
                        if (slot.Kind != JsonValueKind.Object)
                            continue;
                        var itemId = slot.GetString("itemId", string.Empty);
                        var count = (int)slot.GetNumber("count");
                        if (string.IsNullOrEmpty(itemId) || count <= 0)
                            continue;
                        snapshot.PartyInventorySlots.Add(new PartyInventorySlotSnapshotDto
                        {
                            ItemId = itemId,
                            Count = count
                        });
                    }
                }

                if (root.TryGetProperty("relationshipEvents", out var relEvents) &&
                    relEvents.Kind == JsonValueKind.Array)
                {
                    foreach (var ev in relEvents.Array)
                    {
                        if (ev.Kind != JsonValueKind.Object)
                            continue;
                        snapshot.RelationshipEvents.Add(new RelationshipEventSnapshotDto
                        {
                            Tick = ReadU(ev, "tick"),
                            FromEntityId = ReadU(ev, "fromEntityId"),
                            ToEntityId = ReadU(ev, "toEntityId"),
                            Delta = (int)ev.GetNumber("delta"),
                            ReasonTag = ev.GetString("reasonTag", string.Empty),
                            CauseEventId = ReadU(ev, "causeEventId"),
                            HasCauseEventId = ev.TryGetProperty("hasCauseEventId", out var hce) &&
                                              hce.Kind == JsonValueKind.Boolean &&
                                              hce.Bool
                        });
                    }
                }

                if (root.TryGetProperty("strategic", out var strategic) &&
                    strategic.Kind == JsonValueKind.Object)
                {
                    snapshot.Strategic = ReadStrategic(strategic);
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
                    ["cultivationMinorStage"] = JsonValue.FromNumber(e.CultivationMinorStage),
                    ["cultivationProgress"] = JsonValue.FromNumber(e.CultivationProgress),
                    ["breakthroughProgressRequired"] = JsonValue.FromNumber(e.BreakthroughProgressRequired),
                    ["cultivationSpeed"] = JsonValue.FromNumber(e.CultivationSpeed),
                    ["learnedManualId"] = JsonValue.FromString(e.LearnedManualId ?? string.Empty),
                    ["hasManualMastery"] = JsonValue.FromBool(e.HasManualMastery),
                    ["manualMasteryTier"] = JsonValue.FromNumber(e.ManualMasteryTier),
                    ["manualMasteryProgress"] = JsonValue.FromNumber(e.ManualMasteryProgress),
                    ["manualMasteryProgressRequired"] = JsonValue.FromNumber(e.ManualMasteryProgressRequired),
                    ["combatArtsLearned"] = JsonValue.FromArray(SerializeStringList(e.CombatArtsLearned)),
                    ["combatArtsEquipped"] = JsonValue.FromArray(SerializeStringList(e.CombatArtsEquipped)),
                    ["combatArtMastery"] = JsonValue.FromArray(SerializeArtMastery(e.CombatArtMastery)),
                    ["requiredRealmName"] = JsonValue.FromString(e.RequiredRealmName ?? string.Empty),
                    ["hasDailyTask"] = JsonValue.FromBool(e.HasDailyTask),
                    ["laborProgress"] = JsonValue.FromNumber(e.LaborProgress),
                    ["laborQuota"] = JsonValue.FromNumber(e.LaborQuota),
                    ["requiredAmount"] = JsonValue.FromNumber(e.RequiredAmount),
                    ["completedAmount"] = JsonValue.FromNumber(e.CompletedAmount),
                    ["deviation"] = JsonValue.FromNumber(e.Deviation),
                    ["pendingReprimand"] = JsonValue.FromBool(e.PendingReprimand),
                    ["lastSettledDeviation"] = JsonValue.FromNumber(e.LastSettledDeviation),
                    ["hasSchedule"] = JsonValue.FromBool(e.HasSchedule),
                    ["scheduleDefinitionId"] = JsonValue.FromString(e.ScheduleDefinitionId ?? string.Empty),
                    ["activeOrderSource"] = JsonValue.FromNumber(e.ActiveOrderSource),
                    ["knownSiteIds"] = JsonValue.FromArray(SerializeStringList(e.KnownSiteIds)),
                    ["personalConcealmentRisk"] = JsonValue.FromNumber(e.PersonalConcealmentRisk),
                    ["hasEntityLocation"] = JsonValue.FromBool(e.HasEntityLocation),
                    ["locationId"] = JsonValue.FromString(e.LocationId ?? string.Empty),
                    ["hasPresentationOverride"] = JsonValue.FromBool(e.HasPresentationOverride),
                    ["presentationOverrideX"] = JsonValue.FromNumber(e.PresentationOverrideX),
                    ["presentationOverrideZ"] = JsonValue.FromNumber(e.PresentationOverrideZ),
                    ["factionId"] = JsonValue.FromString(e.FactionId ?? string.Empty),
                    ["factionRole"] = JsonValue.FromNumber(e.FactionRole),
                    ["hasCombatVitals"] = JsonValue.FromBool(e.HasCombatVitals),
                    ["currentHp"] = JsonValue.FromNumber(e.CurrentHp),
                    ["currentSpiritPower"] = JsonValue.FromNumber(e.CurrentSpiritPower),
                    ["vitalsPoolsInitialized"] = JsonValue.FromBool(e.VitalsPoolsInitialized),
                    ["bleedOutAfterTick"] = U(e.BleedOutAfterTick),
                    ["hasCorpse"] = JsonValue.FromBool(e.HasCorpse),
                    ["corpseRemoveAfterTick"] = U(e.CorpseRemoveAfterTick),
                    ["personalityTags"] = JsonValue.FromArray(SerializeStringList(e.PersonalityTags))
                }));
            }
            return list;
        }

        static List<JsonValue> SerializePartyInventorySlots(List<PartyInventorySlotSnapshotDto> slots)
        {
            var list = new List<JsonValue>();
            if (slots == null)
                return list;
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null || string.IsNullOrEmpty(s.ItemId) || s.Count <= 0)
                    continue;
                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["itemId"] = JsonValue.FromString(s.ItemId ?? string.Empty),
                    ["count"] = JsonValue.FromNumber(s.Count)
                }));
            }

            return list;
        }

        static List<JsonValue> SerializeRelationshipEvents(List<RelationshipEventSnapshotDto> events)
        {
            var list = new List<JsonValue>();
            if (events == null)
                return list;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev == null)
                    continue;
                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["tick"] = U(ev.Tick),
                    ["fromEntityId"] = U(ev.FromEntityId),
                    ["toEntityId"] = U(ev.ToEntityId),
                    ["delta"] = JsonValue.FromNumber(ev.Delta),
                    ["reasonTag"] = JsonValue.FromString(ev.ReasonTag ?? string.Empty),
                    ["causeEventId"] = U(ev.CauseEventId),
                    ["hasCauseEventId"] = JsonValue.FromBool(ev.HasCauseEventId)
                }));
            }

            return list;
        }

        static List<JsonValue> SerializeStringList(List<string> values)
        {
            var list = new List<JsonValue>();
            if (values == null)
                return list;
            foreach (var v in values)
                list.Add(JsonValue.FromString(v ?? string.Empty));
            return list;
        }

        static List<JsonValue> SerializeArtMastery(List<ArtMasterySnapshotDto> values)
        {
            var list = new List<JsonValue>();
            if (values == null)
                return list;
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value == null || string.IsNullOrEmpty(value.ArtId))
                    continue;
                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["artId"] = JsonValue.FromString(value.ArtId),
                    ["tier"] = JsonValue.FromNumber(value.Tier),
                    ["progress"] = JsonValue.FromNumber(value.Progress),
                    ["progressRequired"] = JsonValue.FromNumber(value.ProgressRequired)
                }));
            }

            return list;
        }

        static List<JsonValue> SerializeOpportunitySites(List<OpportunitySiteSnapshotDto> sites)
        {
            var list = new List<JsonValue>();
            if (sites == null)
                return list;
            foreach (var s in sites)
            {
                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["id"] = JsonValue.FromString(s.Id ?? string.Empty),
                    ["allowsCultivation"] = JsonValue.FromBool(s.AllowsCultivation),
                    ["offeredManualId"] = JsonValue.FromString(s.OfferedManualId ?? string.Empty),
                    ["nameKey"] = JsonValue.FromString(s.NameKey ?? string.Empty),
                    ["description"] = JsonValue.FromString(s.Description ?? string.Empty)
                }));
            }

            return list;
        }

        static List<JsonValue> SerializeManuals(List<ManualSnapshotDto> manuals)
        {
            var list = new List<JsonValue>();
            if (manuals == null)
                return list;
            foreach (var m in manuals)
            {
                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["id"] = JsonValue.FromString(m.Id ?? string.Empty),
                    ["requiredRealm"] = JsonValue.FromString(m.RequiredRealm ?? string.Empty),
                    ["cultivationSpeed"] = JsonValue.FromNumber(m.CultivationSpeed),
                    ["breakthroughProgress"] = JsonValue.FromNumber(m.BreakthroughProgress)
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
                    ["remainingTicks"] = JsonValue.FromNumber(a.RemainingTicks),
                    ["targetRef"] = JsonValue.FromString(a.TargetRef ?? string.Empty),
                    ["activity"] = JsonValue.FromNumber(a.Activity)
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
                    ["waitTicks"] = JsonValue.FromNumber(o.WaitTicks),
                    ["targetRef"] = JsonValue.FromString(o.TargetRef ?? string.Empty),
                    ["activity"] = JsonValue.FromNumber(o.Activity)
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
                CultivationMinorStage = (int)e.GetNumber("cultivationMinorStage"),
                CultivationProgress = (int)e.GetNumber("cultivationProgress"),
                BreakthroughProgressRequired = (int)e.GetNumber("breakthroughProgressRequired"),
                CultivationSpeed = (int)e.GetNumber("cultivationSpeed"),
                LearnedManualId = e.GetString("learnedManualId", string.Empty),
                HasManualMastery = e.TryGetProperty("hasManualMastery", out var hmm) &&
                                   hmm.Kind == JsonValueKind.Boolean && hmm.Bool,
                ManualMasteryTier = (int)e.GetNumber("manualMasteryTier"),
                ManualMasteryProgress = (int)e.GetNumber("manualMasteryProgress"),
                ManualMasteryProgressRequired = (int)e.GetNumber("manualMasteryProgressRequired"),
                RequiredRealmName = e.GetString("requiredRealmName", string.Empty),
                HasDailyTask = e.TryGetProperty("hasDailyTask", out var hdt) && hdt.Kind == JsonValueKind.Boolean && hdt.Bool,
                LaborProgress = (int)e.GetNumber("laborProgress"),
                LaborQuota = (int)e.GetNumber("laborQuota"),
                RequiredAmount = (int)e.GetNumber("requiredAmount"),
                CompletedAmount = (int)e.GetNumber("completedAmount"),
                Deviation = (int)e.GetNumber("deviation"),
                PendingReprimand = e.TryGetProperty("pendingReprimand", out var pr) &&
                                  pr.Kind == JsonValueKind.Boolean && pr.Bool,
                LastSettledDeviation = (int)e.GetNumber("lastSettledDeviation"),
                HasSchedule = e.TryGetProperty("hasSchedule", out var hs) && hs.Kind == JsonValueKind.Boolean && hs.Bool,
                ScheduleDefinitionId = e.GetString("scheduleDefinitionId", string.Empty),
                ActiveOrderSource = (int)e.GetNumber("activeOrderSource"),
                PersonalConcealmentRisk = (int)e.GetNumber("personalConcealmentRisk"),
                HasEntityLocation = e.TryGetProperty("hasEntityLocation", out var hel) && hel.Kind == JsonValueKind.Boolean && hel.Bool,
                EntityLocationSnapshotFieldPresent = e.TryGetProperty("hasEntityLocation", out _),
                LocationId = e.GetString("locationId", string.Empty),
                HasPresentationOverride = e.TryGetProperty("hasPresentationOverride", out var hpo) && hpo.Kind == JsonValueKind.Boolean && hpo.Bool,
                PresentationOverrideX = (float)e.GetNumber("presentationOverrideX"),
                PresentationOverrideZ = (float)e.GetNumber("presentationOverrideZ"),
                FactionId = e.GetString("factionId", string.Empty),
                FactionRole = (int)e.GetNumber("factionRole"),
                HasCombatVitals = e.TryGetProperty("hasCombatVitals", out var hcv) &&
                                  hcv.Kind == JsonValueKind.Boolean &&
                                  hcv.Bool,
                CurrentHp = (int)e.GetNumber("currentHp"),
                CurrentSpiritPower = (int)e.GetNumber("currentSpiritPower"),
                VitalsPoolsInitialized = e.TryGetProperty("vitalsPoolsInitialized", out var vpi) &&
                                         vpi.Kind == JsonValueKind.Boolean &&
                                         vpi.Bool,
                BleedOutAfterTick = ReadU(e, "bleedOutAfterTick"),
                HasCorpse = e.TryGetProperty("hasCorpse", out var hco) &&
                            hco.Kind == JsonValueKind.Boolean &&
                            hco.Bool,
                CorpseRemoveAfterTick = ReadU(e, "corpseRemoveAfterTick")
            };

            if (e.TryGetProperty("knownSiteIds", out var known) && known.Kind == JsonValueKind.Array)
            {
                foreach (var k in known.Array)
                {
                    if (k.Kind == JsonValueKind.String)
                        dto.KnownSiteIds.Add(k.String);
                }
            }

            if (e.TryGetProperty("combatArtsLearned", out var learnedArts) && learnedArts.Kind == JsonValueKind.Array)
            {
                foreach (var art in learnedArts.Array)
                    if (art.Kind == JsonValueKind.String)
                        dto.CombatArtsLearned.Add(art.String);
            }

            if (e.TryGetProperty("combatArtsEquipped", out var equippedArts) && equippedArts.Kind == JsonValueKind.Array)
            {
                foreach (var art in equippedArts.Array)
                    if (art.Kind == JsonValueKind.String)
                        dto.CombatArtsEquipped.Add(art.String);
            }

            if (e.TryGetProperty("combatArtMastery", out var artMastery) && artMastery.Kind == JsonValueKind.Array)
            {
                foreach (var mastery in artMastery.Array)
                {
                    if (mastery.Kind != JsonValueKind.Object)
                        continue;
                    dto.CombatArtMastery.Add(new ArtMasterySnapshotDto
                    {
                        ArtId = mastery.GetString("artId", string.Empty),
                        Tier = (int)mastery.GetNumber("tier"),
                        Progress = (int)mastery.GetNumber("progress"),
                        ProgressRequired = (int)mastery.GetNumber("progressRequired")
                    });
                }
            }

            if (e.TryGetProperty("personalityTags", out var personalityTags) &&
                personalityTags.Kind == JsonValueKind.Array)
            {
                foreach (var tag in personalityTags.Array)
                {
                    if (tag.Kind == JsonValueKind.String)
                        dto.PersonalityTags.Add(tag.String);
                }
            }

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
            RemainingTicks = (ulong)a.GetNumber("remainingTicks"),
            TargetRef = a.GetString("targetRef", string.Empty),
            Activity = (int)a.GetNumber("activity")
        };

        static JsonValue U(ulong value) => JsonValue.FromString(value.ToString(CultureInfo.InvariantCulture));

        static ulong ReadU(JsonValue root, string name)
        {
            if (!root.TryGetProperty(name, out var v)) return 0;
            return ReadUValue(v);
        }

        static ulong ReadUValue(JsonValue v)
        {
            if (v == null) return 0;
            if (v.Kind == JsonValueKind.String &&
                ulong.TryParse(v.String, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
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
            WaitTicks = (ulong)o.GetNumber("waitTicks"),
            TargetRef = o.GetString("targetRef", string.Empty),
            Activity = (int)o.GetNumber("activity")
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

        static OpportunitySiteSnapshotDto ReadOpportunitySite(JsonValue s) => new OpportunitySiteSnapshotDto
        {
            Id = s.GetString("id", string.Empty),
            AllowsCultivation = s.TryGetProperty("allowsCultivation", out var ac) &&
                                ac.Kind == JsonValueKind.Boolean && ac.Bool,
            OfferedManualId = s.GetString("offeredManualId", string.Empty),
            NameKey = s.GetString("nameKey", string.Empty),
            Description = s.GetString("description", string.Empty)
        };

        static ManualSnapshotDto ReadManual(JsonValue m) => new ManualSnapshotDto
        {
            Id = m.GetString("id", string.Empty),
            RequiredRealm = m.GetString("requiredRealm", string.Empty),
            CultivationSpeed = (int)m.GetNumber("cultivationSpeed"),
            BreakthroughProgress = (int)m.GetNumber("breakthroughProgress")
        };

        static List<JsonValue> SerializeHexPath(List<HexCoordSnapshotDto> path)
        {
            var list = new List<JsonValue>();
            if (path == null)
                return list;
            for (var i = 0; i < path.Count; i++)
            {
                var coord = path[i];
                if (coord == null)
                    continue;
                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["q"] = JsonValue.FromNumber(coord.Q),
                    ["r"] = JsonValue.FromNumber(coord.R)
                }));
            }

            return list;
        }

        static List<HexCoordSnapshotDto> ReadHexPath(JsonValue pathNode)
        {
            var list = new List<HexCoordSnapshotDto>();
            if (pathNode.Kind != JsonValueKind.Array)
                return list;
            foreach (var node in pathNode.Array)
            {
                list.Add(new HexCoordSnapshotDto
                {
                    Q = (int)node.GetNumber("q"),
                    R = (int)node.GetNumber("r")
                });
            }

            return list;
        }

        static JsonValue SerializeStrategic(StrategicSnapshotDto strategic)
        {
            strategic ??= new StrategicSnapshotDto();
            var armies = new List<JsonValue>();
            if (strategic.FormalArmies != null)
            {
                for (var i = 0; i < strategic.FormalArmies.Count; i++)
                {
                    var a = strategic.FormalArmies[i];
                    if (a == null)
                        continue;
                    var members = new List<JsonValue>();
                    if (a.MemberCharacterIds != null)
                    {
                        for (var j = 0; j < a.MemberCharacterIds.Count; j++)
                            members.Add(U(a.MemberCharacterIds[j]));
                    }

                    armies.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["armyId"] = JsonValue.FromString(a.ArmyId ?? string.Empty),
                        ["factionId"] = JsonValue.FromString(a.FactionId ?? string.Empty),
                        ["leaderCharacterId"] = U(a.LeaderCharacterId),
                        ["state"] = JsonValue.FromNumber(a.State),
                        ["usesHexStrategicPosition"] = JsonValue.FromBool(a.UsesHexStrategicPosition),
                        ["currentHexQ"] = JsonValue.FromNumber(a.CurrentHexQ),
                        ["currentHexR"] = JsonValue.FromNumber(a.CurrentHexR),
                        ["destinationHexQ"] = JsonValue.FromNumber(a.DestinationHexQ),
                        ["destinationHexR"] = JsonValue.FromNumber(a.DestinationHexR),
                        ["stepProgress"] = JsonValue.FromNumber(a.StepProgress),
                        ["stepRemainingTicks"] = JsonValue.FromNumber(a.StepRemainingTicks),
                        ["stepTotalTicks"] = JsonValue.FromNumber(a.StepTotalTicks),
                        ["currentPathIndex"] = JsonValue.FromNumber(a.CurrentPathIndex),
                        ["locationKind"] = JsonValue.FromNumber(a.LocationKind),
                        ["siteId"] = JsonValue.FromString(a.SiteId ?? string.Empty),
                        ["worldX"] = JsonValue.FromNumber(a.WorldX),
                        ["worldY"] = JsonValue.FromNumber(a.WorldY),
                        ["destinationSiteId"] = JsonValue.FromString(a.DestinationSiteId ?? string.Empty),
                        ["currentOrderKind"] = JsonValue.FromNumber(a.CurrentOrderKind),
                        ["orderTargetArmyId"] = JsonValue.FromString(a.OrderTargetArmyId ?? string.Empty),
                        ["segmentProgress"] = JsonValue.FromNumber(a.SegmentProgress),
                        ["segmentIndex"] = JsonValue.FromNumber(a.SegmentIndex),
                        ["memberCharacterIds"] = JsonValue.FromArray(members),
                        ["hexPath"] = JsonValue.FromArray(SerializeHexPath(a.HexPath))
                    }));
                }
            }

            var memberships = new List<JsonValue>();
            if (strategic.ArmyMemberships != null)
            {
                for (var i = 0; i < strategic.ArmyMemberships.Count; i++)
                {
                    var m = strategic.ArmyMemberships[i];
                    if (m == null)
                        continue;
                    memberships.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["characterId"] = U(m.CharacterId),
                        ["armyId"] = JsonValue.FromString(m.ArmyId ?? string.Empty)
                    }));
                }
            }

            var residuals = new List<JsonValue>();
            if (strategic.ResidualCharacterPresences != null)
            {
                for (var i = 0; i < strategic.ResidualCharacterPresences.Count; i++)
                {
                    var r = strategic.ResidualCharacterPresences[i];
                    if (r == null)
                        continue;
                    residuals.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["characterId"] = U(r.CharacterId),
                        ["hexQ"] = JsonValue.FromNumber(r.HexQ),
                        ["hexR"] = JsonValue.FromNumber(r.HexR)
                    }));
                }
            }

            var characterWorldPresences = new List<JsonValue>();
            if (strategic.CharacterWorldPresences != null)
            {
                for (var i = 0; i < strategic.CharacterWorldPresences.Count; i++)
                {
                    var p = strategic.CharacterWorldPresences[i];
                    if (p == null)
                        continue;
                    characterWorldPresences.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["characterId"] = U(p.CharacterId),
                        ["mode"] = JsonValue.FromNumber(p.Mode),
                        ["siteId"] = JsonValue.FromString(p.SiteId ?? string.Empty),
                        ["hexQ"] = JsonValue.FromNumber(p.HexQ),
                        ["hexR"] = JsonValue.FromNumber(p.HexR),
                        ["hasWorldPosition"] = JsonValue.FromBool(p.HasWorldPosition),
                        ["worldX"] = JsonValue.FromNumber(p.WorldX),
                        ["worldY"] = JsonValue.FromNumber(p.WorldY)
                    }));
                }
            }

            var siteOwners = new List<JsonValue>();
            if (strategic.WorldSiteOwners != null)
            {
                for (var i = 0; i < strategic.WorldSiteOwners.Count; i++)
                {
                    var s = strategic.WorldSiteOwners[i];
                    if (s == null)
                        continue;
                    siteOwners.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["siteId"] = JsonValue.FromString(s.SiteId ?? string.Empty),
                        ["ownerFactionId"] = JsonValue.FromString(s.OwnerFactionId ?? string.Empty)
                    }));
                }
            }

            var territoryControllers = new List<JsonValue>();
            if (strategic.TerritoryRegionControllers != null)
            {
                for (var i = 0; i < strategic.TerritoryRegionControllers.Count; i++)
                {
                    var r = strategic.TerritoryRegionControllers[i];
                    if (r == null)
                        continue;
                    territoryControllers.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["regionId"] = JsonValue.FromString(r.RegionId ?? string.Empty),
                        ["controlFactionId"] = JsonValue.FromString(r.ControlFactionId ?? string.Empty)
                    }));
                }
            }

            var wars = new List<JsonValue>();
            if (strategic.Wars != null)
            {
                for (var i = 0; i < strategic.Wars.Count; i++)
                {
                    var w = strategic.Wars[i];
                    if (w == null)
                        continue;
                    wars.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["warId"] = JsonValue.FromString(w.WarId ?? string.Empty),
                        ["active"] = JsonValue.FromBool(w.Active),
                        ["attackers"] = JsonValue.FromArray(SerializeStringList(w.Attackers)),
                        ["defenders"] = JsonValue.FromArray(SerializeStringList(w.Defenders))
                    }));
                }
            }

            var alliances = new List<JsonValue>();
            if (strategic.Alliances != null)
            {
                for (var i = 0; i < strategic.Alliances.Count; i++)
                {
                    var a = strategic.Alliances[i];
                    if (a == null)
                        continue;
                    alliances.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["allianceId"] = JsonValue.FromString(a.AllianceId ?? string.Empty),
                        ["members"] = JsonValue.FromArray(SerializeStringList(a.Members))
                    }));
                }
            }

            var vassalages = new List<JsonValue>();
            if (strategic.Vassalages != null)
            {
                for (var i = 0; i < strategic.Vassalages.Count; i++)
                {
                    var v = strategic.Vassalages[i];
                    if (v == null)
                        continue;
                    vassalages.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["vassalFactionId"] = JsonValue.FromString(v.VassalFactionId ?? string.Empty),
                        ["overlordFactionId"] = JsonValue.FromString(v.OverlordFactionId ?? string.Empty)
                    }));
                }
            }

            var retreating = new List<JsonValue>();
            if (strategic.RetreatingArmies != null)
            {
                for (var i = 0; i < strategic.RetreatingArmies.Count; i++)
                {
                    var r = strategic.RetreatingArmies[i];
                    if (r == null)
                        continue;
                    var retreatMembers = new List<JsonValue>();
                    if (r.MemberCharacterIds != null)
                    {
                        for (var j = 0; j < r.MemberCharacterIds.Count; j++)
                            retreatMembers.Add(U(r.MemberCharacterIds[j]));
                    }

                    retreating.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["retreatingArmyId"] = JsonValue.FromString(r.RetreatingArmyId ?? string.Empty),
                        ["sourceArmyId"] = JsonValue.FromString(r.SourceArmyId ?? string.Empty),
                        ["factionId"] = JsonValue.FromString(r.FactionId ?? string.Empty),
                        ["hexQ"] = JsonValue.FromNumber(r.HexQ),
                        ["hexR"] = JsonValue.FromNumber(r.HexR),
                        ["memberCharacterIds"] = JsonValue.FromArray(retreatMembers)
                    }));
                }
            }

            var captureObjectives = new List<JsonValue>();
            if (strategic.CaptureObjectives != null)
            {
                for (var i = 0; i < strategic.CaptureObjectives.Count; i++)
                {
                    var c = strategic.CaptureObjectives[i];
                    if (c == null)
                        continue;
                    captureObjectives.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["objectiveId"] = JsonValue.FromString(c.ObjectiveId ?? string.Empty),
                        ["siteId"] = JsonValue.FromString(c.SiteId ?? string.Empty),
                        ["workAreaId"] = JsonValue.FromString(c.WorkAreaId ?? string.Empty),
                        ["currentHp"] = JsonValue.FromNumber(c.CurrentHp),
                        ["maxHp"] = JsonValue.FromNumber(c.MaxHp),
                        ["occupyProgressSeconds"] = JsonValue.FromNumber(c.OccupyProgressSeconds),
                        ["occupyHoldSeconds"] = JsonValue.FromNumber(c.OccupyHoldSeconds),
                        ["completed"] = JsonValue.FromBool(c.Completed)
                    }));
                }
            }

            var root = new Dictionary<string, JsonValue>
            {
                ["playerFactionId"] = JsonValue.FromString(strategic.PlayerFactionId ?? string.Empty),
                ["ch01FormationScenarioCompat"] = JsonValue.FromBool(strategic.Ch01FormationScenarioCompat),
                ["formalArmies"] = JsonValue.FromArray(armies),
                ["armyMemberships"] = JsonValue.FromArray(memberships),
                ["residualCharacterPresences"] = JsonValue.FromArray(residuals),
                ["characterWorldPresences"] = JsonValue.FromArray(characterWorldPresences),
                ["worldSiteOwners"] = JsonValue.FromArray(siteOwners),
                ["territoryRegionControllers"] = JsonValue.FromArray(territoryControllers),
                ["wars"] = JsonValue.FromArray(wars),
                ["alliances"] = JsonValue.FromArray(alliances),
                ["vassalages"] = JsonValue.FromArray(vassalages),
                ["retreatingArmies"] = JsonValue.FromArray(retreating),
                ["captureObjectives"] = JsonValue.FromArray(captureObjectives)
            };

            if (strategic.PlayerPartyTravel != null && strategic.PlayerPartyTravel.HasPosition)
            {
                var p = strategic.PlayerPartyTravel;
                root["playerPartyTravel"] = JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["hasPosition"] = JsonValue.FromBool(true),
                    ["locationKind"] = JsonValue.FromNumber(p.LocationKind),
                    ["siteId"] = JsonValue.FromString(p.SiteId ?? string.Empty),
                    ["worldX"] = JsonValue.FromNumber(p.WorldX),
                    ["worldY"] = JsonValue.FromNumber(p.WorldY),
                    ["currentHexQ"] = JsonValue.FromNumber(p.CurrentHexQ),
                    ["currentHexR"] = JsonValue.FromNumber(p.CurrentHexR)
                });
            }

            if (strategic.PlayerParty != null && strategic.PlayerParty.MemberCharacterIds != null &&
                strategic.PlayerParty.MemberCharacterIds.Count > 0)
            {
                var members = new List<JsonValue>();
                for (var i = 0; i < strategic.PlayerParty.MemberCharacterIds.Count; i++)
                    members.Add(U(strategic.PlayerParty.MemberCharacterIds[i]));
                root["playerParty"] = JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["activeCharacterId"] = U(strategic.PlayerParty.ActiveCharacterId),
                    ["memberCharacterIds"] = JsonValue.FromArray(members)
                });
            }

            if (strategic.LoadedLocalMapCharacterPlacements != null &&
                strategic.LoadedLocalMapCharacterPlacements.Count > 0)
            {
                var placements = new List<JsonValue>();
                for (var i = 0; i < strategic.LoadedLocalMapCharacterPlacements.Count; i++)
                {
                    var p = strategic.LoadedLocalMapCharacterPlacements[i];
                    if (p == null || p.CharacterId == 0 || string.IsNullOrWhiteSpace(p.LocalMapId))
                        continue;
                    placements.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["characterId"] = U(p.CharacterId),
                        ["localMapId"] = JsonValue.FromString(p.LocalMapId),
                        ["localX"] = JsonValue.FromNumber(p.LocalX),
                        ["localZ"] = JsonValue.FromNumber(p.LocalZ)
                    }));
                }

                if (placements.Count > 0)
                    root["loadedLocalMapCharacterPlacements"] = JsonValue.FromArray(placements);
            }

            if (strategic.PendingEngagement != null &&
                !string.IsNullOrEmpty(strategic.PendingEngagement.EngagementId))
            {
                root["pendingEngagement"] = SerializePendingEngagement(strategic.PendingEngagement);
            }

            return JsonValue.FromObject(root);
        }

        static StrategicSnapshotDto ReadStrategic(JsonValue strategic)
        {
            var dto = new StrategicSnapshotDto
            {
                PlayerFactionId = strategic.GetString("playerFactionId", string.Empty),
                Ch01FormationScenarioCompat = strategic.TryGetProperty("ch01FormationScenarioCompat", out var c) &&
                                              c.Kind == JsonValueKind.Boolean && c.Bool
            };

            if (strategic.TryGetProperty("formalArmies", out var armies) && armies.Kind == JsonValueKind.Array)
            {
                foreach (var a in armies.Array)
                {
                    var army = new FormalArmySnapshotDto
                    {
                        ArmyId = a.GetString("armyId", string.Empty),
                        FactionId = a.GetString("factionId", string.Empty),
                        LeaderCharacterId = ReadU(a, "leaderCharacterId"),
                        State = (int)a.GetNumber("state"),
                        UsesHexStrategicPosition = a.TryGetProperty("usesHexStrategicPosition", out var usesHex) &&
                                                     usesHex.Kind == JsonValueKind.Boolean && usesHex.Bool,
                        CurrentHexQ = a.TryGetProperty("currentHexQ", out var cq) ? (int)cq.Number : 0,
                        CurrentHexR = a.TryGetProperty("currentHexR", out var cr) ? (int)cr.Number : 0,
                        DestinationHexQ = a.TryGetProperty("destinationHexQ", out var dq) ? (int)dq.Number : 0,
                        DestinationHexR = a.TryGetProperty("destinationHexR", out var dr) ? (int)dr.Number : 0,
                        StepProgress = a.TryGetProperty("stepProgress", out var sp) ? (float)sp.Number : 0f,
                        StepRemainingTicks = a.TryGetProperty("stepRemainingTicks", out var srt) ? (int)srt.Number : 0,
                        StepTotalTicks = a.TryGetProperty("stepTotalTicks", out var stt) ? (int)stt.Number : 0,
                        CurrentPathIndex = a.TryGetProperty("currentPathIndex", out var cpi) ? (int)cpi.Number : 0,
                        LocationKind = a.TryGetProperty("locationKind", out var lk) ? (int)lk.Number : 0,
                        SiteId = a.GetString("siteId", string.Empty),
                        WorldX = a.TryGetProperty("worldX", out var wx) ? (float)wx.Number : 0f,
                        WorldY = a.TryGetProperty("worldY", out var wy) ? (float)wy.Number : 0f,
                        DestinationSiteId = a.GetString("destinationSiteId", string.Empty),
                        CurrentOrderKind = a.TryGetProperty("currentOrderKind", out var ok) ? (int)ok.Number : 0,
                        OrderTargetArmyId = a.GetString("orderTargetArmyId", string.Empty),
                        SegmentProgress = a.TryGetProperty("segmentProgress", out var sp2) ? (float)sp2.Number : 0f,
                        SegmentIndex = a.TryGetProperty("segmentIndex", out var si) ? (int)si.Number : 0,
                    };
                    if (a.TryGetProperty("memberCharacterIds", out var members) && members.Kind == JsonValueKind.Array)
                    {
                        foreach (var m in members.Array)
                            army.MemberCharacterIds.Add(ReadUValue(m));
                    }

                    if (a.TryGetProperty("hexPath", out var hexPath))
                        army.HexPath = ReadHexPath(hexPath);

                    dto.FormalArmies.Add(army);
                }
            }

            if (strategic.TryGetProperty("armyMemberships", out var armyMemberships) &&
                armyMemberships.Kind == JsonValueKind.Array)
            {
                foreach (var m in armyMemberships.Array)
                {
                    dto.ArmyMemberships.Add(new ArmyMembershipSnapshotDto
                    {
                        CharacterId = ReadU(m, "characterId"),
                        ArmyId = m.GetString("armyId", string.Empty)
                    });
                }
            }

            if (strategic.TryGetProperty("residualCharacterPresences", out var residuals) &&
                residuals.Kind == JsonValueKind.Array)
            {
                foreach (var r in residuals.Array)
                {
                    dto.ResidualCharacterPresences.Add(new ResidualCharacterPresenceDto
                    {
                        CharacterId = ReadU(r, "characterId"),
                        HexQ = (int)r.GetNumber("hexQ"),
                        HexR = (int)r.GetNumber("hexR")
                    });
                }
            }

            if (strategic.TryGetProperty("characterWorldPresences", out var charPresences) &&
                charPresences.Kind == JsonValueKind.Array)
            {
                foreach (var p in charPresences.Array)
                {
                    dto.CharacterWorldPresences.Add(new CharacterWorldPresenceSnapshotDto
                    {
                        CharacterId = ReadU(p, "characterId"),
                        Mode = p.TryGetProperty("mode", out var modeNode) ? (int)modeNode.Number : 0,
                        SiteId = p.GetString("siteId", string.Empty),
                        HexQ = p.TryGetProperty("hexQ", out var hq) ? (int)hq.Number : int.MinValue,
                        HexR = p.TryGetProperty("hexR", out var hr) ? (int)hr.Number : int.MinValue,
                        HasWorldPosition =
                            p.TryGetProperty("hasWorldPosition", out var hwp) &&
                            hwp.Kind == JsonValueKind.Boolean &&
                            hwp.Bool,
                        WorldX = p.TryGetProperty("worldX", out var wx)
                            ? (float)wx.Number
                            : 0f,
                        WorldY = p.TryGetProperty("worldY", out var wy)
                            ? (float)wy.Number
                            : 0f
                    });
                }
            }

            if (strategic.TryGetProperty("worldSiteOwners", out var siteOwners) &&
                siteOwners.Kind == JsonValueKind.Array)
            {
                foreach (var s in siteOwners.Array)
                {
                    dto.WorldSiteOwners.Add(new WorldSiteOwnerSnapshotDto
                    {
                        SiteId = s.GetString("siteId", string.Empty),
                        OwnerFactionId = s.GetString("ownerFactionId", string.Empty)
                    });
                }
            }

            if (strategic.TryGetProperty("territoryRegionControllers", out var territoryControllers) &&
                territoryControllers.Kind == JsonValueKind.Array)
            {
                foreach (var r in territoryControllers.Array)
                {
                    dto.TerritoryRegionControllers.Add(new TerritoryRegionControllerSnapshotDto
                    {
                        RegionId = r.GetString("regionId", string.Empty),
                        ControlFactionId = r.GetString("controlFactionId", string.Empty)
                    });
                }
            }

            if (strategic.TryGetProperty("wars", out var wars) && wars.Kind == JsonValueKind.Array)
            {
                foreach (var w in wars.Array)
                {
                    var war = new WarSnapshotDto
                    {
                        WarId = w.GetString("warId", string.Empty),
                        Active = w.TryGetProperty("active", out var active) &&
                                 active.Kind == JsonValueKind.Boolean && active.Bool
                    };
                    if (w.TryGetProperty("attackers", out var attackers) && attackers.Kind == JsonValueKind.Array)
                    {
                        foreach (var faction in attackers.Array)
                        {
                            if (faction.Kind == JsonValueKind.String)
                                war.Attackers.Add(faction.String);
                        }
                    }

                    if (w.TryGetProperty("defenders", out var defenders) && defenders.Kind == JsonValueKind.Array)
                    {
                        foreach (var faction in defenders.Array)
                        {
                            if (faction.Kind == JsonValueKind.String)
                                war.Defenders.Add(faction.String);
                        }
                    }

                    dto.Wars.Add(war);
                }
            }

            if (strategic.TryGetProperty("alliances", out var alliances) && alliances.Kind == JsonValueKind.Array)
            {
                foreach (var a in alliances.Array)
                {
                    var alliance = new AllianceSnapshotDto
                    {
                        AllianceId = a.GetString("allianceId", string.Empty)
                    };
                    if (a.TryGetProperty("members", out var members) && members.Kind == JsonValueKind.Array)
                    {
                        foreach (var member in members.Array)
                        {
                            if (member.Kind == JsonValueKind.String)
                                alliance.Members.Add(member.String);
                        }
                    }

                    dto.Alliances.Add(alliance);
                }
            }

            if (strategic.TryGetProperty("vassalages", out var vassalages) && vassalages.Kind == JsonValueKind.Array)
            {
                foreach (var v in vassalages.Array)
                {
                    dto.Vassalages.Add(new VassalageSnapshotDto
                    {
                        VassalFactionId = v.GetString("vassalFactionId", string.Empty),
                        OverlordFactionId = v.GetString("overlordFactionId", string.Empty)
                    });
                }
            }

            if (strategic.TryGetProperty("retreatingArmies", out var retreating) &&
                retreating.Kind == JsonValueKind.Array)
            {
                foreach (var r in retreating.Array)
                {
                    var retreat = new RetreatingArmySnapshotDto
                    {
                        RetreatingArmyId = r.GetString("retreatingArmyId", string.Empty),
                        SourceArmyId = r.GetString("sourceArmyId", string.Empty),
                        FactionId = r.GetString("factionId", string.Empty),
                        HexQ = (int)r.GetNumber("hexQ"),
                        HexR = (int)r.GetNumber("hexR")
                    };
                    if (r.TryGetProperty("memberCharacterIds", out var members) && members.Kind == JsonValueKind.Array)
                    {
                        foreach (var member in members.Array)
                            retreat.MemberCharacterIds.Add(ReadUValue(member));
                    }

                    dto.RetreatingArmies.Add(retreat);
                }
            }

            if (strategic.TryGetProperty("captureObjectives", out var captureObjectives) &&
                captureObjectives.Kind == JsonValueKind.Array)
            {
                foreach (var objNode in captureObjectives.Array)
                {
                    dto.CaptureObjectives.Add(new CaptureObjectiveSnapshotDto
                    {
                        ObjectiveId = objNode.GetString("objectiveId", string.Empty),
                        SiteId = objNode.GetString("siteId", string.Empty),
                        WorkAreaId = objNode.GetString("workAreaId", string.Empty),
                        CurrentHp = (int)objNode.GetNumber("currentHp"),
                        MaxHp = (int)objNode.GetNumber("maxHp"),
                        OccupyProgressSeconds = objNode.TryGetProperty("occupyProgressSeconds", out var occupyProgress)
                            ? (float)occupyProgress.Number : 0f,
                        OccupyHoldSeconds = objNode.TryGetProperty("occupyHoldSeconds", out var occupyHold)
                            ? (float)occupyHold.Number : 0f,
                        Completed = objNode.TryGetProperty("completed", out var completed) &&
                                    completed.Kind == JsonValueKind.Boolean && completed.Bool
                    });
                }
            }

            if (strategic.TryGetProperty("playerPartyTravel", out var partyTravel) &&
                partyTravel.Kind == JsonValueKind.Object)
            {
                dto.PlayerPartyTravel = new PlayerPartyTravelSnapshotDto
                {
                    HasPosition = partyTravel.TryGetProperty("hasPosition", out var hasPos) &&
                                    hasPos.Kind == JsonValueKind.Boolean && hasPos.Bool,
                    LocationKind = partyTravel.TryGetProperty("locationKind", out var lk) ? (int)lk.Number : 0,
                    SiteId = partyTravel.GetString("siteId", string.Empty),
                    WorldX = partyTravel.TryGetProperty("worldX", out var wx) ? (float)wx.Number : 0f,
                    WorldY = partyTravel.TryGetProperty("worldY", out var wy) ? (float)wy.Number : 0f,
                    CurrentHexQ = partyTravel.TryGetProperty("currentHexQ", out var cq) ? (int)cq.Number : 0,
                    CurrentHexR = partyTravel.TryGetProperty("currentHexR", out var cr) ? (int)cr.Number : 0
                };
            }

            if (strategic.TryGetProperty("playerParty", out var playerParty) &&
                playerParty.Kind == JsonValueKind.Object)
            {
                dto.PlayerParty = new PlayerPartyRuntimeSnapshotDto
                {
                    ActiveCharacterId = ReadU(playerParty, "activeCharacterId")
                };
                if (playerParty.TryGetProperty("memberCharacterIds", out var members) &&
                    members.Kind == JsonValueKind.Array)
                {
                    foreach (var m in members.Array)
                        dto.PlayerParty.MemberCharacterIds.Add(ReadUValue(m));
                }
            }

            if (strategic.TryGetProperty("loadedLocalMapCharacterPlacements", out var placements) &&
                placements.Kind == JsonValueKind.Array)
            {
                foreach (var node in placements.Array)
                {
                    if (node.Kind != JsonValueKind.Object)
                        continue;
                    dto.LoadedLocalMapCharacterPlacements.Add(new LoadedLocalMapCharacterPlacementSnapshotDto
                    {
                        CharacterId = ReadU(node, "characterId"),
                        LocalMapId = node.GetString("localMapId", string.Empty),
                        LocalX = node.TryGetProperty("localX", out var lx) ? (float)lx.Number : 0f,
                        LocalZ = node.TryGetProperty("localZ", out var lz) ? (float)lz.Number : 0f
                    });
                }
            }

            if (strategic.TryGetProperty("pendingEngagement", out var pendingNode) &&
                pendingNode.Kind == JsonValueKind.Object)
            {
                dto.PendingEngagement = ReadPendingEngagement(pendingNode);
            }

            return dto;
        }

        static JsonValue SerializePendingEngagement(PendingEngagementSnapshotDto p)
        {
            if (p == null)
                return JsonValue.Null;

            var battleArea = SerializeHexCoordPairs(p.BattleAreaHexQList, p.BattleAreaHexRList);
            var supportArea = SerializeHexCoordPairs(p.SupportAreaHexQList, p.SupportAreaHexRList);

            var lockedPlayer = new List<JsonValue>();
            if (p.PlayerFormalArmyIds != null)
            {
                for (var i = 0; i < p.PlayerFormalArmyIds.Count; i++)
                    lockedPlayer.Add(JsonValue.FromString(p.PlayerFormalArmyIds[i] ?? string.Empty));
            }

            var lockedEnemy = new List<JsonValue>();
            if (p.EnemyFormalArmyIds != null)
            {
                for (var i = 0; i < p.EnemyFormalArmyIds.Count; i++)
                    lockedEnemy.Add(JsonValue.FromString(p.EnemyFormalArmyIds[i] ?? string.Empty));
            }

            var lockedParty = new List<JsonValue>();
            if (p.PlayerPartyMemberIds != null)
            {
                for (var i = 0; i < p.PlayerPartyMemberIds.Count; i++)
                    lockedParty.Add(U(p.PlayerPartyMemberIds[i]));
            }

            var offer = new Dictionary<string, JsonValue>
            {
                ["offerId"] = JsonValue.FromString(p.OfferId ?? string.Empty),
                ["title"] = JsonValue.FromString(p.OfferTitle ?? string.Empty),
                ["armyStackId"] = JsonValue.FromString(p.ArmyStackId ?? string.Empty),
                ["encounterLocalMapId"] = JsonValue.FromString(p.EncounterLocalMapId ?? string.Empty),
                ["origin"] = JsonValue.FromNumber(p.OfferOrigin),
                ["requiresWarDeclaration"] = JsonValue.FromBool(p.OfferRequiresWarDeclaration),
                ["pendingWarAttackerFactionId"] = JsonValue.FromString(p.PendingWarAttackerFactionId ?? string.Empty),
                ["pendingWarDefenderFactionId"] = JsonValue.FromString(p.PendingWarDefenderFactionId ?? string.Empty)
            };

            var retreat = new Dictionary<string, JsonValue>
            {
                ["hasValue"] = JsonValue.FromBool(p.RetreatHasValue),
                ["armyLocationKind"] = JsonValue.FromNumber(p.RetreatArmyLocationKind),
                ["partyLocationKind"] = JsonValue.FromNumber(p.RetreatPartyLocationKind),
                ["siteId"] = JsonValue.FromString(p.RetreatSiteId ?? string.Empty),
                ["worldX"] = JsonValue.FromNumber(p.RetreatWorldX),
                ["worldY"] = JsonValue.FromNumber(p.RetreatWorldY),
                ["hexQ"] = JsonValue.FromNumber(p.RetreatHexQ),
                ["hexR"] = JsonValue.FromNumber(p.RetreatHexR),
                ["isPlayerParty"] = JsonValue.FromBool(p.RetreatIsPlayerParty)
            };

            var records = new List<JsonValue>();
            if (p.ParticipantRecords != null)
            {
                for (var i = 0; i < p.ParticipantRecords.Count; i++)
                {
                    var r = p.ParticipantRecords[i];
                    if (r == null)
                        continue;
                    records.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                    {
                        ["kind"] = JsonValue.FromNumber(r.Kind),
                        ["entityId"] = U(r.EntityId),
                        ["armyStackId"] = JsonValue.FromString(r.ArmyStackId ?? string.Empty),
                        ["formalArmyId"] = JsonValue.FromString(r.FormalArmyId ?? string.Empty),
                        ["displayLabel"] = JsonValue.FromString(r.DisplayLabel ?? string.Empty),
                        ["combatPower"] = JsonValue.FromNumber(r.CombatPower),
                        ["selected"] = JsonValue.FromBool(r.Selected),
                        ["includedReason"] = JsonValue.FromString(r.IncludedReason ?? string.Empty),
                        ["hasPreBattle"] = JsonValue.FromBool(r.HasPreBattle),
                        ["preBattleMode"] = JsonValue.FromNumber(r.PreBattleMode),
                        ["preBattleSiteId"] = JsonValue.FromString(r.PreBattleSiteId ?? string.Empty),
                        ["preBattleHexQ"] = JsonValue.FromNumber(r.PreBattleHexQ),
                        ["preBattleHexR"] = JsonValue.FromNumber(r.PreBattleHexR),
                        ["preBattleFollowStackId"] = JsonValue.FromString(r.PreBattleFollowStackId ?? string.Empty),
                        ["preBattleCombatPursuitStackId"] = JsonValue.FromString(r.PreBattleCombatPursuitStackId ?? string.Empty)
                    }));
                }
            }

            var participantSnapshot = new Dictionary<string, JsonValue>
            {
                ["offerId"] = JsonValue.FromString(p.ParticipantOfferId ?? string.Empty),
                ["attackerArmyId"] = JsonValue.FromString(p.ParticipantAttackerArmyId ?? string.Empty),
                ["defenderArmyId"] = JsonValue.FromString(p.ParticipantDefenderArmyId ?? string.Empty),
                ["primaryEnemyStackId"] = JsonValue.FromString(p.ParticipantPrimaryEnemyStackId ?? string.Empty),
                ["battleAnchorHexQ"] = JsonValue.FromNumber(p.ParticipantBattleAnchorHexQ),
                ["battleAnchorHexR"] = JsonValue.FromNumber(p.ParticipantBattleAnchorHexR),
                ["encounterLocalMapId"] = JsonValue.FromString(p.ParticipantEncounterLocalMapId ?? string.Empty),
                ["localMapResolutionKind"] = JsonValue.FromNumber(p.ParticipantLocalMapResolutionKind),
                ["hasLocalMapResolutionKind"] = JsonValue.FromBool(p.HasParticipantLocalMapResolutionKind),
                ["records"] = JsonValue.FromArray(records)
            };

            return JsonValue.FromObject(new Dictionary<string, JsonValue>
            {
                ["engagementId"] = JsonValue.FromString(p.EngagementId ?? string.Empty),
                ["initiatorKind"] = JsonValue.FromNumber(p.InitiatorKind),
                ["initiatorFormalArmyId"] = JsonValue.FromString(p.InitiatorFormalArmyId ?? string.Empty),
                ["initiatorIsPlayerSide"] = JsonValue.FromBool(p.InitiatorIsPlayerSide),
                ["decisionSubjectKind"] = JsonValue.FromNumber(p.DecisionSubjectKind),
                ["decisionSubjectFormalArmyId"] = JsonValue.FromString(p.DecisionSubjectFormalArmyId ?? string.Empty),
                ["battleLocationHexQ"] = JsonValue.FromNumber(p.BattleLocationHexQ),
                ["battleLocationHexR"] = JsonValue.FromNumber(p.BattleLocationHexR),
                ["battleAreaHexes"] = JsonValue.FromArray(battleArea),
                ["supportAreaHexes"] = JsonValue.FromArray(supportArea),
                ["supportBattleSiteId"] = JsonValue.FromString(p.SupportBattleSiteId ?? string.Empty),
                ["supportBattleSiteResolutionSource"] = JsonValue.FromString(p.SupportBattleSiteResolutionSource ?? string.Empty),
                ["initiatorEngagementHexQ"] = JsonValue.FromNumber(p.InitiatorEngagementHexQ),
                ["initiatorEngagementHexR"] = JsonValue.FromNumber(p.InitiatorEngagementHexR),
                ["initiatorEngagementSiteId"] = JsonValue.FromString(p.InitiatorEngagementSiteId ?? string.Empty),
                ["primaryEnemyFactionId"] = JsonValue.FromString(p.PrimaryEnemyFactionId ?? string.Empty),
                ["attackerFormalArmyId"] = JsonValue.FromString(p.AttackerFormalArmyId ?? string.Empty),
                ["defenderFormalArmyId"] = JsonValue.FromString(p.DefenderFormalArmyId ?? string.Empty),
                ["playerPartyIncluded"] = JsonValue.FromBool(p.PlayerPartyIncluded),
                ["playerInclusionReason"] = JsonValue.FromString(p.PlayerInclusionReason ?? string.Empty),
                ["involvesPlayerSide"] = JsonValue.FromBool(p.InvolvesPlayerSide),
                ["requiresPlayerDecision"] = JsonValue.FromBool(p.RequiresPlayerDecision),
                ["pendingBattleTriggerReason"] = JsonValue.FromString(p.PendingBattleTriggerReason ?? string.Empty),
                ["initiatorCommittedHexQ"] = JsonValue.FromNumber(p.InitiatorCommittedHexQ),
                ["initiatorCommittedHexR"] = JsonValue.FromNumber(p.InitiatorCommittedHexR),
                ["defenderCommittedHexQ"] = JsonValue.FromNumber(p.DefenderCommittedHexQ),
                ["defenderCommittedHexR"] = JsonValue.FromNumber(p.DefenderCommittedHexR),
                ["offer"] = JsonValue.FromObject(offer),
                ["lockedPlayerFormalArmyIds"] = JsonValue.FromArray(lockedPlayer),
                ["lockedEnemyFormalArmyIds"] = JsonValue.FromArray(lockedEnemy),
                ["lockedPlayerPartyMemberIds"] = JsonValue.FromArray(lockedParty),
                ["retreat"] = JsonValue.FromObject(retreat),
                ["participantSnapshot"] = JsonValue.FromObject(participantSnapshot)
            });
        }

        static List<JsonValue> SerializeHexCoordPairs(List<int> qList, List<int> rList)
        {
            var list = new List<JsonValue>();
            if (qList == null || rList == null)
                return list;
            var count = System.Math.Min(qList.Count, rList.Count);
            for (var i = 0; i < count; i++)
            {
                list.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["q"] = JsonValue.FromNumber(qList[i]),
                    ["r"] = JsonValue.FromNumber(rList[i])
                }));
            }

            return list;
        }

        static void ReadHexCoordPairsInto(JsonValue node, List<int> qList, List<int> rList)
        {
            qList.Clear();
            rList.Clear();
            if (node == null || node.Kind != JsonValueKind.Array)
                return;
            foreach (var h in node.Array)
            {
                if (h.Kind != JsonValueKind.Object)
                    continue;
                qList.Add((int)h.GetNumber("q"));
                rList.Add((int)h.GetNumber("r"));
            }
        }

        static void ReadStringArrayInto(JsonValue node, List<string> into)
        {
            into.Clear();
            if (node == null || node.Kind != JsonValueKind.Array)
                return;
            foreach (var v in node.Array)
            {
                if (v.Kind == JsonValueKind.String)
                    into.Add(v.String);
            }
        }

        static PendingEngagementSnapshotDto ReadPendingEngagement(JsonValue node)
        {
            var p = new PendingEngagementSnapshotDto
            {
                EngagementId = node.GetString("engagementId", string.Empty),
                InitiatorKind = (int)node.GetNumber("initiatorKind"),
                InitiatorFormalArmyId = node.GetString("initiatorFormalArmyId", string.Empty),
                InitiatorIsPlayerSide = node.GetBool("initiatorIsPlayerSide"),
                DecisionSubjectKind = (int)node.GetNumber("decisionSubjectKind"),
                DecisionSubjectFormalArmyId = node.GetString("decisionSubjectFormalArmyId", string.Empty),
                BattleLocationHexQ = (int)node.GetNumber("battleLocationHexQ"),
                BattleLocationHexR = (int)node.GetNumber("battleLocationHexR"),
                SupportBattleSiteId = node.GetString("supportBattleSiteId", string.Empty),
                SupportBattleSiteResolutionSource = node.GetString("supportBattleSiteResolutionSource", string.Empty),
                InitiatorEngagementHexQ = (int)node.GetNumber("initiatorEngagementHexQ"),
                InitiatorEngagementHexR = (int)node.GetNumber("initiatorEngagementHexR"),
                InitiatorEngagementSiteId = node.GetString("initiatorEngagementSiteId", string.Empty),
                PrimaryEnemyFactionId = node.GetString("primaryEnemyFactionId", string.Empty),
                AttackerFormalArmyId = node.GetString("attackerFormalArmyId", string.Empty),
                DefenderFormalArmyId = node.GetString("defenderFormalArmyId", string.Empty),
                PlayerPartyIncluded = node.GetBool("playerPartyIncluded"),
                PlayerInclusionReason = node.GetString("playerInclusionReason", string.Empty),
                InvolvesPlayerSide = node.GetBool("involvesPlayerSide"),
                PendingBattleTriggerReason = node.GetString("pendingBattleTriggerReason", string.Empty),
                InitiatorCommittedHexQ = (int)node.GetNumber("initiatorCommittedHexQ", int.MinValue),
                InitiatorCommittedHexR = (int)node.GetNumber("initiatorCommittedHexR", int.MinValue),
                DefenderCommittedHexQ = (int)node.GetNumber("defenderCommittedHexQ", int.MinValue),
                DefenderCommittedHexR = (int)node.GetNumber("defenderCommittedHexR", int.MinValue)
            };
            // 旧 snapshot 缺 requiresPlayerDecision 时回退 involvesPlayerSide（等价历史推导，Restore 不再重推）。
            p.RequiresPlayerDecision = node.TryGetProperty("requiresPlayerDecision", out var rpd) &&
                                       rpd.Kind == JsonValueKind.Boolean
                ? rpd.Bool
                : p.InvolvesPlayerSide;

            if (node.TryGetProperty("battleAreaHexes", out var battleAreaNode))
                ReadHexCoordPairsInto(battleAreaNode, p.BattleAreaHexQList, p.BattleAreaHexRList);
            if (node.TryGetProperty("supportAreaHexes", out var supportAreaNode))
                ReadHexCoordPairsInto(supportAreaNode, p.SupportAreaHexQList, p.SupportAreaHexRList);

            if (node.TryGetProperty("lockedPlayerFormalArmyIds", out var lockedPlayerNode))
                ReadStringArrayInto(lockedPlayerNode, p.PlayerFormalArmyIds);
            if (node.TryGetProperty("lockedEnemyFormalArmyIds", out var lockedEnemyNode))
                ReadStringArrayInto(lockedEnemyNode, p.EnemyFormalArmyIds);
            if (node.TryGetProperty("lockedPlayerPartyMemberIds", out var lockedPartyNode) &&
                lockedPartyNode.Kind == JsonValueKind.Array)
            {
                p.PlayerPartyMemberIds.Clear();
                foreach (var m in lockedPartyNode.Array)
                    p.PlayerPartyMemberIds.Add(ReadUValue(m));
            }

            if (node.TryGetProperty("offer", out var offerNode) && offerNode.Kind == JsonValueKind.Object)
            {
                p.OfferId = offerNode.GetString("offerId", string.Empty);
                p.OfferTitle = offerNode.GetString("title", string.Empty);
                p.ArmyStackId = offerNode.GetString("armyStackId", string.Empty);
                p.EncounterLocalMapId = offerNode.GetString("encounterLocalMapId", string.Empty);
                p.OfferOrigin = (int)offerNode.GetNumber("origin");
                p.OfferRequiresWarDeclaration = offerNode.GetBool("requiresWarDeclaration");
                p.PendingWarAttackerFactionId = offerNode.GetString("pendingWarAttackerFactionId", string.Empty);
                p.PendingWarDefenderFactionId = offerNode.GetString("pendingWarDefenderFactionId", string.Empty);
            }

            if (node.TryGetProperty("retreat", out var retreatNode) && retreatNode.Kind == JsonValueKind.Object)
            {
                p.RetreatHasValue = retreatNode.GetBool("hasValue");
                p.RetreatArmyLocationKind = (int)retreatNode.GetNumber("armyLocationKind");
                p.RetreatPartyLocationKind = (int)retreatNode.GetNumber("partyLocationKind");
                p.RetreatSiteId = retreatNode.GetString("siteId", string.Empty);
                p.RetreatWorldX = (float)retreatNode.GetNumber("worldX");
                p.RetreatWorldY = (float)retreatNode.GetNumber("worldY");
                p.RetreatHexQ = (int)retreatNode.GetNumber("hexQ");
                p.RetreatHexR = (int)retreatNode.GetNumber("hexR");
                p.RetreatIsPlayerParty = retreatNode.GetBool("isPlayerParty");
            }

            if (node.TryGetProperty("participantSnapshot", out var snapNode) &&
                snapNode.Kind == JsonValueKind.Object)
            {
                p.ParticipantOfferId = snapNode.GetString("offerId", string.Empty);
                p.ParticipantAttackerArmyId = snapNode.GetString("attackerArmyId", string.Empty);
                p.ParticipantDefenderArmyId = snapNode.GetString("defenderArmyId", string.Empty);
                p.ParticipantPrimaryEnemyStackId = snapNode.GetString("primaryEnemyStackId", string.Empty);
                p.ParticipantBattleAnchorHexQ = (int)snapNode.GetNumber("battleAnchorHexQ");
                p.ParticipantBattleAnchorHexR = (int)snapNode.GetNumber("battleAnchorHexR");
                p.ParticipantEncounterLocalMapId = snapNode.GetString("encounterLocalMapId", string.Empty);
                p.ParticipantLocalMapResolutionKind = (int)snapNode.GetNumber("localMapResolutionKind");
                p.HasParticipantLocalMapResolutionKind =
                    snapNode.GetBool("hasLocalMapResolutionKind");
                if (snapNode.TryGetProperty("records", out var recordsNode) &&
                    recordsNode.Kind == JsonValueKind.Array)
                {
                    p.ParticipantRecords.Clear();
                    foreach (var r in recordsNode.Array)
                    {
                        if (r.Kind != JsonValueKind.Object)
                            continue;
                        var rec = new PendingEngagementParticipantRecordDto
                        {
                            Kind = (int)r.GetNumber("kind"),
                            EntityId = ReadU(r, "entityId"),
                            ArmyStackId = r.GetString("armyStackId", string.Empty),
                            FormalArmyId = r.GetString("formalArmyId", string.Empty),
                            DisplayLabel = r.GetString("displayLabel", string.Empty),
                            CombatPower = (int)r.GetNumber("combatPower"),
                            Selected = r.GetBool("selected"),
                            IncludedReason = r.GetString("includedReason", string.Empty),
                            HasPreBattle = r.GetBool("hasPreBattle"),
                            PreBattleMode = (int)r.GetNumber("preBattleMode"),
                            PreBattleSiteId = r.GetString("preBattleSiteId", string.Empty),
                            PreBattleHexQ = (int)r.GetNumber("preBattleHexQ", int.MinValue),
                            PreBattleHexR = (int)r.GetNumber("preBattleHexR", int.MinValue),
                            PreBattleFollowStackId = r.GetString("preBattleFollowStackId", string.Empty),
                            PreBattleCombatPursuitStackId = r.GetString("preBattleCombatPursuitStackId", string.Empty)
                        };
                        p.ParticipantRecords.Add(rec);
                    }
                }
            }

            return p;
        }
    }
}

