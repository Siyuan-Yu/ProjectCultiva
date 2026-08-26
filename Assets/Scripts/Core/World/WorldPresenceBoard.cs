using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World
{
    /// <summary>单个可控角色在宏观 Hex 战略图上的位置。</summary>
    public sealed class WorldAgentPresence
    {
        public const int InvalidHexComponent = ArmyHexBattleAnchorService.InvalidHexComponent;

        public EntityId EntityId { get; set; }
        public PartyWorldPresenceMode Mode { get; set; } = PartyWorldPresenceMode.AtSite;
        /// <summary>Pure Hex AtSite 模式：WorldSiteId 战略位置真源。</summary>
        public string SiteId { get; set; } = string.Empty;
        /// <summary>宏观 RTS：跟随的 ArmyStack id；空表示未跟随。</summary>
        public string FollowStackId { get; set; } = string.Empty;
        /// <summary>攻击／追击目标栈 id；有值则到站不弹「是否查看」，只走接战。</summary>
        public string CombatPursuitStackId { get; set; } = string.Empty;

        /// <summary>
        /// 已因接战弹窗消费过本次抵达：撤退／关窗后勿再弹「是否查看」。
        /// 新的宏观出行下令时清除。
        /// </summary>
        public bool SuppressArrivalNotice { get; set; }

        /// <summary>AtHex Residual 战略坐标（唯一 Residual 位置真源）。</summary>
        public int HexQ { get; set; } = InvalidHexComponent;
        public int HexR { get; set; } = InvalidHexComponent;

        /// <summary>Phase 2D：AtWorldPosition 连续世界坐标真源。</summary>
        public float WorldPosX { get; set; }
        public float WorldPosY { get; set; }
        public bool HasContinuousWorldPosition { get; set; }

        public bool UsesHexPresence =>
            Mode == PartyWorldPresenceMode.AtHex &&
            HexQ != InvalidHexComponent &&
            HexR != InvalidHexComponent;

        public HexCoord ResidualHex => new HexCoord(HexQ, HexR);

        public bool IsFollowingStack => !string.IsNullOrEmpty(FollowStackId);

        public bool IsCombatPursuing => !string.IsNullOrEmpty(CombatPursuitStackId);

        public void ClearHexPresence()
        {
            HexQ = InvalidHexComponent;
            HexR = InvalidHexComponent;
        }

        public void SetAtHex(HexCoord hex)
        {
            Mode = PartyWorldPresenceMode.AtHex;
            HexQ = hex.Q;
            HexR = hex.R;
            SiteId = string.Empty;
            HasContinuousWorldPosition = false;
            ClearFollow();
            ClearCombatPursuit();
        }

        public void SetAtWorldPosition(WorldVec2 pos, HexCoord derivedHex)
        {
            Mode = PartyWorldPresenceMode.AtWorldPosition;
            SiteId = string.Empty;
            HasContinuousWorldPosition = true;
            WorldPosX = pos.X;
            WorldPosY = pos.Y;
            HexQ = derivedHex.Q;
            HexR = derivedHex.R;
            ClearFollow();
            ClearCombatPursuit();
        }

        public WorldVec2 ContinuousWorldPosition =>
            HasContinuousWorldPosition ? new WorldVec2(WorldPosX, WorldPosY) : default;

        public HexCoord DerivedHexFromWorldPosition =>
            HexQ != InvalidHexComponent && HexR != InvalidHexComponent
                ? new HexCoord(HexQ, HexR)
                : default;

        public void ClearFollow() => FollowStackId = string.Empty;

        public void ClearCombatPursuit() => CombatPursuitStackId = string.Empty;
    }

    /// <summary>全员宏观位置；PartyWorld 作「当前镜头／焦点 Site」摘要。</summary>
    public sealed class WorldPresenceBoard
    {
        readonly Dictionary<ulong, WorldAgentPresence> _byEntity =
            new Dictionary<ulong, WorldAgentPresence>();

        public IReadOnlyDictionary<ulong, WorldAgentPresence> All => _byEntity;

        public void Clear() => _byEntity.Clear();

        public WorldAgentPresence GetOrCreate(EntityId id)
        {
            if (id.IsNone)
                throw new ArgumentException("EntityId required.");
            if (_byEntity.TryGetValue(id.Value, out var existing))
                return existing;
            var p = new WorldAgentPresence { EntityId = id };
            _byEntity[id.Value] = p;
            return p;
        }

        public bool TryGet(EntityId id, out WorldAgentPresence presence)
        {
            presence = null;
            if (id.IsNone)
                return false;
            return _byEntity.TryGetValue(id.Value, out presence);
        }

        /// <summary>尸体腐烂／实体移除后从大地图抹掉位置。</summary>
        public bool Remove(EntityId id)
        {
            if (id.IsNone)
                return false;
            return _byEntity.Remove(id.Value);
        }

        public void SetAtSite(EntityId id, string siteId)
        {
            var p = GetOrCreate(id);
            p.SiteId = siteId ?? string.Empty;
            p.Mode = PartyWorldPresenceMode.AtSite;
            p.ClearHexPresence();
            p.HasContinuousWorldPosition = false;
            p.ClearFollow();
            p.ClearCombatPursuit();
        }

        public void SetAtHex(EntityId id, HexCoord hex)
        {
            var p = GetOrCreate(id);
            p.EntityId = id;
            p.SetAtHex(hex);
        }

        public void SetAtWorldPosition(EntityId id, WorldVec2 pos, HexCoord derivedHex)
        {
            var p = GetOrCreate(id);
            p.EntityId = id;
            p.SetAtWorldPosition(pos, derivedHex);
        }

        public void CollectAtSite(string siteId, List<EntityId> into)
        {
            if (into == null || string.IsNullOrEmpty(siteId))
                return;
            foreach (var kv in _byEntity)
            {
                var p = kv.Value;
                if (p == null || p.Mode != PartyWorldPresenceMode.AtSite)
                    continue;
                if (string.Equals(p.SiteId, siteId, StringComparison.Ordinal))
                    into.Add(p.EntityId);
            }
        }
    }
}
