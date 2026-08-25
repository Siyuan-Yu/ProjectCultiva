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
                return false;

            var motion = world.PlayerPartyTravel;
            // healDrift 仅允许 Startup 等显式调用；且不得用 PartyWorld 覆盖已成立的 AtWorldPosition。
            if (healDrift)
                TryHealStartupOnly(world, party, motion);

            if (!motion.HasPosition)
                return false;

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId) &&
                world.Strategic.Sites.TryGet(motion.SiteId, out var site) &&
                site != null)
            {
                HexMath.ToWorldPosition(
                    site.PresenceHex,
                    world.HexWorld != null && world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f,
                    out var sx,
                    out var sy);
                resolved = new Resolved
                {
                    HasValue = true,
                    LocationKind = PlayerPartyLocationKind.AtWorldSite,
                    SiteId = site.SiteId,
                    WorldPosition = new WorldVec2(sx, sy),
                    DerivedHex = site.PresenceHex,
                    ResolvedLocalMapId = site.LocalMapId ?? string.Empty,
                };
                return true;
            }

            var hexSize2 = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            var derived = HexMath.WorldToHex(motion.WorldPosition.X, motion.WorldPosition.Y, hexSize2);
            if (derived != motion.CurrentHex)
                motion.SetWorldPositionInternal(motion.WorldPosition, derived);

            WildernessLocalMapFallback.TryResolve(world, derived, out var mapId);
            resolved = new Resolved
            {
                HasValue = true,
                LocationKind = PlayerPartyLocationKind.AtWorldPosition,
                SiteId = string.Empty,
                WorldPosition = motion.WorldPosition,
                DerivedHex = derived,
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
            var active = party != null && party.HasActive
                ? party.ActiveCharacterId.Value.ToString()
                : "none";
            var msg =
                "[PlayerPartyWorldLocation] " + reason +
                " active=" + active +
                " kind=" + motion.LocationKind +
                " site=" + (motion.SiteId ?? "") +
                " pos=" + motion.WorldPosition +
                " hex=" + motion.CurrentHex +
                " moving=" + motion.IsMoving +
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
