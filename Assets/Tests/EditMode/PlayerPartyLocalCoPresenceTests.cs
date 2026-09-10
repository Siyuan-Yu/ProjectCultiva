using System.Linq;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>
    /// PlayerParty Follow 的「同一可交互空间」判定（迁移 §B，targeted regression E–H/J）。
    ///
    /// <para>
    /// Continuous Outdoor 已经没有 Outdoor LocalMap（<c>ActiveMapLayoutId == ""</c>），
    /// 因此 join 不得再要求 same LocalMap / same SiteId / same Hex；只要求同一个 active
    /// Continuous Surface presentation scope。Legacy／Interior／Cave 保持旧独立空间规则。
    /// </para>
    /// </summary>
    public sealed class PlayerPartyLocalCoPresenceTests
    {
        const string VillageSiteId = "base:site_huangcun";
        const string AdjacentWildernessSiteId = "base:site_linjian";
        const string OverworldMap = "base:map_ch01_reference";
        const string InteriorMap = "base:map_ch01_cave";

        static readonly EntityId ActiveId = new EntityId(1);
        static readonly EntityId FollowerId = new EntityId(2);
        static readonly EntityId[] Roster = { ActiveId, FollowerId };

        // ---------------------------------------------------------------- E
        [Test]
        public void E_ContinuousOutdoorWithoutActiveLocalMapAllowsJoin()
        {
            var world = BuildContinuousWorld();
            Assert.AreEqual(string.Empty, world.LocalMap.ActiveMapLayoutId,
                "Continuous Outdoor 没有 active LocalMap（本测试前提）");
            Assert.IsFalse(world.LocalMap.IsInInterior);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);
            Assert.IsTrue(party.TryAddMember(world, Roster, FollowerId, out var joinError),
                "Continuous Outdoor 同处 loaded scope 的同伴必须可以加入 Follow：" + joinError);
            Assert.AreEqual(PlayerPartyCoPresenceScope.ContinuousOutdoorPresentation,
                PlayerPartyLocalCoPresenceQuery.Evaluate(world, party, FollowerId).Scope);
        }

        // ---------------------------------------------------------------- F
        [Test]
        public void F_DifferentSiteIdDoesNotBlockContinuousJoin()
        {
            var world = BuildContinuousWorld();
            // 主角刚出村界（PhysicalRegion = 荒村），同伴还在荒村内：SiteId 不同，物理相邻。
            world.WorldPresence.SetAtSiteWithAnchor(
                ActiveId, VillageSiteId, new WorldVec2(5.2f, 10.16f));
            world.WorldPresence.SetAtSiteWithAnchor(
                FollowerId, AdjacentWildernessSiteId, new WorldVec2(5.28f, 10.16f));

            Assert.AreNotEqual(
                ResolveSiteOf(world, ActiveId), ResolveSiteOf(world, FollowerId),
                "本测试前提：两人 SiteId 必须不同");

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);
            Assert.IsTrue(party.TryAddMember(world, Roster, FollowerId, out var joinError),
                "SiteId 只是 context，不是 co-presence boundary：" + joinError);
        }

        // ---------------------------------------------------------------- G
        [Test]
        public void G_InteriorDifferentLocalMapIsRejectedWithSpaceNeutralMessage()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(4, 4);
            world.LocalMap.EnsureOverworld(OverworldMap);
            world.LocalMap.ActiveMapLayoutId = InteriorMap;
            Assert.IsTrue(world.LocalMap.IsInInterior, "本测试前提：必须处于 Interior");

            RegisterPlace(world, "loc_cave_chamber", InteriorMap);
            RegisterPlace(world, "base:loc_ref_labor_yard", OverworldMap);
            AddCharacter(world, ActiveId, "loc_cave_chamber");
            AddCharacter(world, FollowerId, "base:loc_ref_labor_yard");
            world.LocalMap.AddOccupant(ActiveId);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);

            Assert.IsFalse(party.ValidateJoin(world, Roster, FollowerId, out var deny),
                "Interior：不在同一 LocalMap 的同伴必须被拒（保持旧独立空间规则）");
            Assert.IsNotEmpty(deny);
            StringAssert.DoesNotContain("LocalMap", deny,
                "§9：玩家提示必须空间中性，不得再出现 LocalMap");
            Assert.AreEqual(PlayerPartyLocalCoPresenceQuery.DeniedPlayerMessage, deny);

            // Legacy 规则本身保留（给 old save / Interiors 用）。
            Assert.IsFalse(PlayerPartyRuntime.IsOnSameLocalMap(world, ActiveId, FollowerId));
        }

        [Test]
        public void G2_LegacySameLocalMapJoinStillWorks()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(4, 4);
            world.LocalMap.EnsureOverworld(OverworldMap);
            world.LocalMap.ActiveMapLayoutId = OverworldMap;
            Assert.IsFalse(world.LocalMap.IsInInterior);

            RegisterPlace(world, "loc_a", OverworldMap);
            RegisterPlace(world, "loc_b", OverworldMap);
            AddCharacter(world, ActiveId, "loc_a");
            AddCharacter(world, FollowerId, "loc_b");
            world.LocalMap.AddOccupant(ActiveId);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);
            Assert.IsTrue(party.TryAddMember(world, Roster, FollowerId, out var joinError), joinError);
            Assert.AreEqual(PlayerPartyCoPresenceScope.LegacyLocalMap,
                PlayerPartyLocalCoPresenceQuery.Evaluate(world, party, FollowerId).Scope);
        }

        // ---------------------------------------------------------------- H + J
        [Test]
        public void H_ContinuousFollowSyncsPresenceAndTravelingMembershipWithoutLocalMapOccupant()
        {
            var world = BuildContinuousWorld();
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);

            // 复刻 HostPlayerPartyController.TryFollowActive 的 Continuous 分支。
            Assert.IsTrue(party.TryAddMember(world, Roster, FollowerId, out var joinError), joinError);
            BackgroundCharacterTravelService.CancelTravelIfAny(world, FollowerId);
            Assert.IsTrue(PlayerPartyLocalCoPresenceQuery.IsContinuousOutdoorPresentationScope(world));
            PlayerPartyTransitionMembership.SyncMemberPresenceFromMotion(world, FollowerId);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);

            Assert.IsTrue(party.IsMember(FollowerId));
            Assert.IsTrue(world.PlayerPartyTravel.TravelingMembers.Contains(FollowerId),
                "新 follower 必须纳入 traveling members");
            Assert.IsTrue(world.WorldPresence.TryGet(FollowerId, out var presence));
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, presence.Mode,
                "§11：Domain membership 位置必须同步为当前 Party continuous travel authority");
            Assert.AreEqual(world.PlayerPartyTravel.WorldPosition.X, presence.WorldPosX, 1e-6);
            Assert.AreEqual(world.PlayerPartyTravel.WorldPosition.Y, presence.WorldPosY, 1e-6);
            Assert.AreEqual(world.PlayerPartyTravel.CurrentHex, presence.ResidualHex);

            Assert.IsFalse(world.LocalMap.ContainsOccupant(FollowerId),
                "§10：Continuous Outdoor 不写 LocalMap occupant");
            Assert.AreEqual(string.Empty, world.LocalMap.ActiveMapLayoutId);
        }

        // ---------------------------------------------------------------- J
        [Test]
        public void J_StopFollowKeepsCurrentContinuousWorldPosition()
        {
            var world = BuildContinuousWorld();
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);
            Assert.IsTrue(party.TryAddMember(world, Roster, FollowerId, out var joinError), joinError);
            PlayerPartyTransitionMembership.SyncMemberPresenceFromMotion(world, FollowerId);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);

            // follower 实际走过一段，停在荒村内一个 precise 位置。
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : HexWorldScale.DefaultHexOuterRadius;
            HexMath.ToWorldPosition(new HexCoord(3, 3), hexSize, out var px, out var py);
            var precise = new WorldVec2(px, py);
            Assert.AreEqual(VillageSiteId, WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(world, precise),
                "本测试前提：precise 位置必须解析回荒村 PhysicalRegion");

            Assert.IsTrue(party.TryRemoveMember(FollowerId, out var removeError), removeError);
            PlayerPartyTransitionMembership.SyncIndependentCharacterPresenceFromPosition(
                world, FollowerId, precise);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);

            Assert.IsFalse(world.PlayerPartyTravel.TravelingMembers.Contains(FollowerId));
            Assert.IsTrue(world.WorldPresence.TryGet(FollowerId, out var after));
            Assert.IsTrue(after.HasContinuousWorldPosition,
                "§12：Stop Follow 不得丢位置（禁止回 Site arrival）");
            Assert.AreEqual(precise.X, after.WorldPosX, 1e-6);
            Assert.AreEqual(precise.Y, after.WorldPosY, 1e-6);
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, after.Mode);
            Assert.AreEqual(VillageSiteId, after.SiteId);

            // 荒野位的对照：不在任何 Continuous Site 内 → AtWorldPosition，位置同样保留。
            HexMath.ToWorldPosition(new HexCoord(0, 0), hexSize, out var wx, out var wy);
            var wilderness = new WorldVec2(wx, wy);
            Assert.AreEqual(string.Empty,
                WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(world, wilderness));
            PlayerPartyTransitionMembership.SyncIndependentCharacterPresenceFromPosition(
                world, FollowerId, wilderness);
            Assert.IsTrue(world.WorldPresence.TryGet(FollowerId, out var afterWild));
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, afterWild.Mode);
            Assert.AreEqual(wilderness.X, afterWild.WorldPosX, 1e-6);
            Assert.AreEqual(wilderness.Y, afterWild.WorldPosY, 1e-6);
        }

        static string ResolveSiteOf(SimulationWorld world, EntityId id) =>
            world.WorldPresence.TryGet(id, out var presence) ? presence.SiteId : string.Empty;

        static SimulationWorld BuildContinuousWorld()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(6, 6);

            // 荒村 PhysicalRegion（Continuous Outdoor）在 footprint hex 上注册。
            var site = new WorldSite
            {
                SiteId = VillageSiteId,
                DisplayName = "青石荒村",
                AnchorHex = new HexCoord(2, 2),
                LocalMapId = OverworldMap,
                UsesContinuousOutdoorSurface = true
            };
            site.SetFootprint(new[] { new HexCoord(2, 2), new HexCoord(3, 2), new HexCoord(2, 3), new HexCoord(3, 3) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            RegisterPlace(world, "base:loc_ref_labor_yard", OverworldMap);
            AddCharacter(world, ActiveId, "base:loc_ref_labor_yard");
            AddCharacter(world, FollowerId, "base:loc_ref_labor_yard");

            // Continuous Outdoor：没有 active LocalMap，party 位于 canonical WorldPosition。
            world.LocalMap.ActiveMapLayoutId = string.Empty;
            world.LocalMap.OverworldMapLayoutId = string.Empty;
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(5.2f, 10.16f), new HexCoord(2, 2));
            world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext(VillageSiteId);

            // runtime 已建立的连续呈现 scope：loaded site + 两人都 materialized。
            world.ContinuousOutdoorMaterialization.RegisterLoadedSite(VillageSiteId);
            world.ContinuousOutdoorMaterialization.Materialize(ActiveId);
            world.ContinuousOutdoorMaterialization.Materialize(FollowerId);

            world.WorldPresence.SetAtSiteWithAnchor(
                ActiveId, VillageSiteId, new WorldVec2(5.2f, 10.16f));
            world.WorldPresence.SetAtSiteWithAnchor(
                FollowerId, VillageSiteId, new WorldVec2(5.28f, 10.16f));
            return world;
        }

        static void RegisterPlace(SimulationWorld world, string locationId, string localMapId)
        {
            if (world.WorldRegion.TryGet(locationId, out _))
                return;
            world.WorldRegion.Register(new WorldLocationState
            {
                Id = locationId,
                LocalMapId = localMapId
            });
        }

        static void AddCharacter(SimulationWorld world, EntityId id, string locationId)
        {
            var entity = new Entity(id, new DefinitionId("test", "char"), EntityTag.Character, "test");
            entity.AddComponent(new LifecycleComponent(LifecycleState.Alive));
            entity.AddComponent(new ActionStateComponent());
            entity.AddComponent(new EntityLocationComponent { LocationId = locationId });
            world.Entities.AddExisting(entity);
        }
    }
}
