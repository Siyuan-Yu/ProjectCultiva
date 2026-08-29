using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// PlayerParty 权威世界位置查询：WorldMap Marker 与 CloseWorldMapTakeover 必须共用。
    /// PartyWorld.SiteId / LocalMapId 不得反写 Domain WorldLocation。
    /// </summary>
    public static class PlayerPartyWorldLocationQuery
    {
        public struct Resolved
        {
            public PlayerPartyLocationKind LocationKind;
            public string SiteId;
            public WorldVec2 WorldPosition;
            public HexCoord DerivedHex;
            public string ResolvedLocalMapId;
            public bool HasValue;
        }

        /// <summary>
        /// 只读权威位置。默认不 heal——PartyWorld 不得覆盖 PlayerPartyWorldMotion。
        /// </summary>
        public static bool TryResolve(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out Resolved resolved,
            bool healDrift = false)
        {
            resolved = default;
            if (world?.PlayerPartyTravel == null)
            {
                return false;
            }

            var motion = world.PlayerPartyTravel;
            // healDrift 仅允许 Startup 等显式调用；且不得用 PartyWorld 覆盖已成立的 AtWorldPosition。
            if (healDrift)
                TryHealStartupOnly(world, party, motion);

            if (!motion.HasPosition)
            {
                return false;
            }

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId) &&
                world.Strategic.Sites.TryGet(motion.SiteId, out var site) &&
                site != null)
            {
                var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                    ? world.HexWorld.HexSize
                    : 1f;
                var markerPos = motion.IsMoving
                    ? motion.ResolveTravelPresentationWorld(hexSize)
                    : default(WorldVec2?);
                if (!markerPos.HasValue)
                {
                    HexMath.ToWorldPosition(site.PresenceHex, hexSize, out var sx, out var sy);
                    markerPos = new WorldVec2(sx, sy);
                }

                var derivedHex = motion.IsMoving
                    ? motion.CurrentHex
                    : site.PresenceHex;
                resolved = new Resolved
                {
                    HasValue = true,
                    LocationKind = PlayerPartyLocationKind.AtWorldSite,
                    SiteId = site.SiteId,
                    WorldPosition = markerPos.Value,
                    DerivedHex = derivedHex,
                    ResolvedLocalMapId = site.LocalMapId ?? string.Empty,
                };
                return true;
            }

            var hexSize2 = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            var worldPos = motion.IsMoving
                ? motion.ResolveTravelPresentationWorld(hexSize2)
                : motion.WorldPosition;

            // Phase 5R-B3B.2：正式 Wilderness Context = motion.CurrentHex（由 Context/Transition
            // authority 提交：正式跨格 / TravelPlan leg 起点）。Hex 边界中点 WorldToHex 存在数值歧义
            // （可翻到邻格），不得用它强写 CurrentHex / 决定 LocalMap / 当作上下文；否则会与已加载
            // LocalMap 的 hex 分裂 → SurfaceExit authority / reopen materialization 错乱。
            // 因此 map 与 DerivedHex（权威 Hex，供 reopen 加载 / presence / legal location）
            // 统一取已提交 Context，不再从连续位置反推。
            var contextHex = motion.CurrentHex;
            WildernessLocalMapFallback.TryResolve(world, contextHex, out var mapId);
            resolved = new Resolved
            {
                HasValue = true,
                LocationKind = PlayerPartyLocationKind.AtWorldPosition,
                SiteId = string.Empty,
                WorldPosition = worldPos,
                DerivedHex = contextHex,
                ResolvedLocalMapId = mapId ?? string.Empty,
            };
            return true;
        }

        /// <summary>
        /// 仅 Startup：Travel 尚无有效 LocationKind/Site 时，用 Active WorldPresence AtSite 初始化。
        /// 绝不用 PartyWorld 覆盖已有 AtWorldPosition。
        /// </summary>
        public static bool TryHealStartupOnly(
            SimulationWorld world,
            PlayerPartyRuntime party,
            PlayerPartyWorldMotion motion)
        {
            if (world == null || motion == null || motion.IsMoving)
                return false;

            // 已有正式开世界位置：禁止任何 Site 回写。
            if (motion.HasPosition &&
                motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition)
                return false;

            if (motion.HasPosition &&
                motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId))
                return false;

            var activeId = party != null && party.HasActive ? party.ActiveCharacterId : EntityId.None;
            if (activeId.IsNone ||
                !world.WorldPresence.TryGet(activeId, out var wp) ||
                wp == null ||
                wp.Mode != PartyWorldPresenceMode.AtSite ||
                string.IsNullOrEmpty(wp.SiteId))
                return false;

            if (!world.Strategic.Sites.TryGet(wp.SiteId, out var site) || site == null)
                return false;

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            motion.SetAtWorldSite(site.SiteId, site.PresenceHex, hexSize);
            if (party != null)
                motion.CaptureTravelingMembers(party.Members);
            PlayerPartyWorldLocationDebug.LogSnapshot(world, party, "HealStartupOnly");
            return true;
        }

        /// <summary>旧名保留：转发到 Startup-only，且永不反写 AtWorldPosition。</summary>
        public static bool TryHealSiteDrift(
            SimulationWorld world,
            PlayerPartyRuntime party,
            PlayerPartyWorldMotion motion) =>
            TryHealStartupOnly(world, party, motion);
    }

    /// <summary>关键点单次 Debug（非每帧）。</summary>
    public static class PlayerPartyWorldLocationDebug
    {
        public static System.Action<string> Sink { get; set; }

        static string _lastKey = string.Empty;

        public static void LogSnapshot(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string reason)
        {
            if (Sink == null || world?.PlayerPartyTravel == null)
                return;

            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            var travelPresentation = motion.IsMoving
                ? motion.ResolveTravelPresentationWorld(hexSize)
                : default(WorldVec2?);
            var insideSiteId = string.Empty;
            WorldSite footprintSite = null;
            var insideSite = motion.IsMoving &&
                             WorldSiteFootprintLocationAuthority.TryGetSiteAtHex(
                                 world,
                                 motion.CurrentHex,
                                 out footprintSite) &&
                             footprintSite != null;
            if (insideSite)
                insideSiteId = footprintSite.SiteId;
            var active = party != null && party.HasActive
                ? party.ActiveCharacterId.Value.ToString()
                : "none";
            var msg =
                "[PlayerPartyWorldLocation] " + reason +
                " active=" + active +
                " kind=" + motion.LocationKind +
                " site=" + (motion.SiteId ?? "") +
                " pos=" + motion.WorldPosition +
                " travelPresentation=" + (travelPresentation.HasValue ? travelPresentation.Value.ToString() : "n/a") +
                " hex=" + motion.CurrentHex +
                " insideSite=" + insideSite +
                " insideSiteId=" + insideSiteId +
                " moving=" + motion.IsMoving +
                " siteDeparturePending=" + motion.IsSiteDeparturePending +
                " usesTravelPresentation=" + motion.UsesTravelPresentation +
                " destSite=" + (motion.DestinationSiteId ?? "") +
                " partyWorld.site=" + (world.PartyWorld?.SiteId ?? "") +
                " partyWorld.map=" + (world.PartyWorld?.LocalMapId ?? "");
            var key = reason + "|" + msg;
            if (key == _lastKey)
                return;
            _lastKey = key;
            Sink(msg);
        }

        public static void LogTransition(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string reason) =>
            LogSnapshot(world, party, reason);

        public static void LogBeforeAfter(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string reason,
            PlayerPartyLocationKind kindBefore,
            string siteBefore,
            WorldVec2 posBefore,
            HexCoord hexBefore)
        {
            if (Sink == null || world?.PlayerPartyTravel == null)
                return;
            var motion = world.PlayerPartyTravel;
            var active = party != null && party.HasActive
                ? party.ActiveCharacterId.Value.ToString()
                : "none";
            Sink(
                "[PlayerPartyWorldLocation] " + reason +
                " active=" + active +
                " BEFORE kind=" + kindBefore +
                " site=" + (siteBefore ?? "") +
                " pos=" + posBefore +
                " hex=" + hexBefore +
                " AFTER kind=" + motion.LocationKind +
                " site=" + (motion.SiteId ?? "") +
                " pos=" + motion.WorldPosition +
                " hex=" + motion.CurrentHex +
                " moving=" + motion.IsMoving +
                " destSite=" + (motion.DestinationSiteId ?? "") +
                " partyWorld.site=" + (world.PartyWorld?.SiteId ?? "") +
                " partyWorld.map=" + (world.PartyWorld?.LocalMapId ?? ""));
        }

    }
}
