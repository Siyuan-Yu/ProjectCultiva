using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>?????????????????????????</summary>
    public sealed class PreBattleWorldPresence
    {
        public PartyWorldPresenceMode Mode { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public int HexQ { get; set; } = WorldAgentPresence.InvalidHexComponent;
        public int HexR { get; set; } = WorldAgentPresence.InvalidHexComponent;
        public string FollowStackId { get; set; } = string.Empty;
        public string CombatPursuitStackId { get; set; } = string.Empty;

        public static PreBattleWorldPresence Capture(WorldAgentPresence p)
        {
            if (p == null)
                return null;
            return new PreBattleWorldPresence
            {
                Mode = p.Mode,
                SiteId = p.SiteId ?? string.Empty,
                HexQ = p.HexQ,
                HexR = p.HexR,
                FollowStackId = p.FollowStackId ?? string.Empty,
                CombatPursuitStackId = p.CombatPursuitStackId ?? string.Empty
            };
        }

        public void ApplyTo(WorldAgentPresence p)
        {
            if (p == null)
                return;
            p.Mode = Mode;
            p.SiteId = SiteId ?? string.Empty;
            p.HexQ = HexQ;
            p.HexR = HexR;
            p.FollowStackId = FollowStackId ?? string.Empty;
            p.CombatPursuitStackId = CombatPursuitStackId ?? string.Empty;
        }
    }

    public enum BattleParticipantKind
    {
        MandatoryFriendly = 0,
        OptionalFriendly = 1,
        EnemyPrimary = 2,
        EnemyReinforcement = 3
    }

    public sealed class BattleParticipantRecord
    {
        public BattleParticipantKind Kind { get; set; }
        public EntityId EntityId { get; set; }
        public string ArmyStackId { get; set; } = string.Empty;
        public string FormalArmyId { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public int CombatPower { get; set; }
        public bool Selected { get; set; }
        public PreBattleWorldPresence PreBattle { get; set; }
        /// <summary>Phase 4 Debug：写入 Participants 的原因。</summary>
        public string IncludedReason { get; set; } = string.Empty;
    }

    /// <summary>BattleOffer ?????????ADR-0023 Phase B?Pure Hex??</summary>
    public sealed class BattleParticipantSnapshot
    {
        public string OfferId { get; set; } = string.Empty;
        public int BattleAnchorHexQ { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int BattleAnchorHexR { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public string PrimaryEnemyStackId { get; set; } = string.Empty;
        public string AttackerArmyId { get; set; } = string.Empty;
        public string DefenderArmyId { get; set; } = string.Empty;
        public string EncounterLocalMapId { get; set; } =
            StrategicEncounterCatalog.DefaultEncounterLocalMapId;
        /// <summary>
        /// 本场 Manual Battle 的地点解析类别（Phase 5S：结束战斗时按类别决定
        /// 原地留在真实 LocalMap 还是回 ExplicitEncounterMap 旧路径）。
        /// </summary>
        public BattleLocalMapResolutionKind LocalMapResolutionKind { get; set; }
            = BattleLocalMapResolutionKind.ExplicitEncounterMap;
        public string LastBattleSummary { get; set; } = string.Empty;
        public bool PlayerWon { get; set; }
        public bool IsAutoSettlement { get; set; }

        readonly List<BattleParticipantRecord> _records = new List<BattleParticipantRecord>(16);

        public IReadOnlyList<BattleParticipantRecord> Records => _records;

        public void Clear()
        {
            OfferId = string.Empty;
            BattleAnchorHexQ = ArmyHexBattleAnchorService.InvalidHexComponent;
            BattleAnchorHexR = ArmyHexBattleAnchorService.InvalidHexComponent;
            PrimaryEnemyStackId = string.Empty;
            AttackerArmyId = string.Empty;
            DefenderArmyId = string.Empty;
            EncounterLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            LocalMapResolutionKind = BattleLocalMapResolutionKind.ExplicitEncounterMap;
            LastBattleSummary = string.Empty;
            PlayerWon = false;
            IsAutoSettlement = false;
            _records.Clear();
        }

        public void Add(BattleParticipantRecord record)
        {
            if (record == null)
                return;
            _records.Add(record);
        }

        public List<EntityId> CollectSelectedFriendly()
        {
            var list = new List<EntityId>(_records.Count);
            for (var i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r.EntityId.IsNone)
                    continue;
                if (r.Kind == BattleParticipantKind.MandatoryFriendly ||
                    (r.Kind == BattleParticipantKind.OptionalFriendly && r.Selected))
                    list.Add(r.EntityId);
            }

            return list;
        }

        public List<string> CollectEnemyStackIds()
        {
            var list = new List<string>(4);
            for (var i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (string.IsNullOrEmpty(r.ArmyStackId))
                    continue;
                if (r.Kind != BattleParticipantKind.EnemyPrimary &&
                    r.Kind != BattleParticipantKind.EnemyReinforcement)
                    continue;
                if (!list.Contains(r.ArmyStackId))
                    list.Add(r.ArmyStackId);
            }

            return list;
        }

        public void CollectEnemyEntityIds(List<EntityId> into)
        {
            into?.Clear();
            if (into == null)
                return;
            for (var i = 0; i < _records.Count; i++)
            {
                var rec = _records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.EnemyPrimary &&
                    rec.Kind != BattleParticipantKind.EnemyReinforcement)
                    continue;
                var exists = false;
                for (var j = 0; j < into.Count; j++)
                {
                    if (into[j] == rec.EntityId)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    into.Add(rec.EntityId);
            }
        }

        public void RemoveFriendlyRecords()
        {
            for (var i = _records.Count - 1; i >= 0; i--)
            {
                var kind = _records[i].Kind;
                if (kind == BattleParticipantKind.MandatoryFriendly ||
                    kind == BattleParticipantKind.OptionalFriendly)
                    _records.RemoveAt(i);
            }
        }

        public BattleParticipantRecord FindByEntity(EntityId id)
        {
            if (id.IsNone)
                return null;
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].EntityId == id)
                    return _records[i];
            }

            return null;
        }

        public void CopyFrom(BattleParticipantSnapshot src)
        {
            if (src == null)
            {
                Clear();
                return;
            }

            OfferId = src.OfferId;
            BattleAnchorHexQ = src.BattleAnchorHexQ;
            BattleAnchorHexR = src.BattleAnchorHexR;
            PrimaryEnemyStackId = src.PrimaryEnemyStackId;
            AttackerArmyId = src.AttackerArmyId;
            DefenderArmyId = src.DefenderArmyId;
            EncounterLocalMapId = src.EncounterLocalMapId;
            LocalMapResolutionKind = src.LocalMapResolutionKind;
            LastBattleSummary = src.LastBattleSummary;
            PlayerWon = src.PlayerWon;
            IsAutoSettlement = src.IsAutoSettlement;
            _records.Clear();
            for (var i = 0; i < src._records.Count; i++)
            {
                var r = src._records[i];
                if (r == null)
                    continue;
                _records.Add(new BattleParticipantRecord
                {
                    Kind = r.Kind,
                    EntityId = r.EntityId,
                    ArmyStackId = r.ArmyStackId,
                    FormalArmyId = r.FormalArmyId,
                    DisplayLabel = r.DisplayLabel,
                    CombatPower = r.CombatPower,
                    Selected = r.Selected,
                    PreBattle = r.PreBattle == null
                        ? null
                        : new PreBattleWorldPresence
                        {
                            Mode = r.PreBattle.Mode,
                            SiteId = r.PreBattle.SiteId,
                            HexQ = r.PreBattle.HexQ,
                            HexR = r.PreBattle.HexR,
                            FollowStackId = r.PreBattle.FollowStackId,
                            CombatPursuitStackId = r.PreBattle.CombatPursuitStackId
                        }
                });
            }
        }

        public void CopyInto(BattleParticipantSnapshot dst) => dst?.CopyFrom(this);
    }

    /// <summary>?????????????????Pure Hex??</summary>
    public static class ReinforcementRangeService
    {
        public static float DefaultWorldRadius { get; set; } = 0.25f;

        public static float GetWorldRadius(SimulationWorld world)
        {
            if (world?.Strategic != null && world.Strategic.ReinforcementWorldRadius > 0f)
                return world.Strategic.ReinforcementWorldRadius;
            return DefaultWorldRadius;
        }

        public static bool IsWithinReinforcementRange(
            SimulationWorld world,
            WorldAgentPresence presence,
            HexCoord anchorHex)
        {
            if (!TryGetWorldDistance(world, presence, anchorHex, out var dist))
                return false;
            return dist <= GetWorldRadius(world);
        }

        public static bool TryGetWorldDistance(
            SimulationWorld world,
            WorldAgentPresence from,
            HexCoord anchorHex,
            out float distance)
        {
            distance = float.MaxValue;
            if (!TryGetPresenceWorldXY(world, from, out var fx, out var fy))
                return false;
            HexMath.ToWorldPosition(anchorHex, world.HexWorld.HexSize, out var ax, out var ay);
            var dx = fx - ax;
            var dy = fy - ay;
            distance = (float)Math.Sqrt(dx * dx + dy * dy);
            return true;
        }

        public static bool TryGetPresenceWorldXY(
            SimulationWorld world,
            WorldAgentPresence presence,
            out float x,
            out float y)
        {
            x = y = 0f;
            if (world == null || presence == null)
                return false;
            return WorldAgentMapPositionResolver.TryResolve(
                world,
                presence.EntityId,
                presence,
                out x,
                out y);
        }

        public static bool IsStackWithinRange(
            SimulationWorld world,
            ArmyStack stack,
            HexCoord anchorHex)
        {
            if (stack == null)
                return false;
            if (!ArmyStackAdapter.TryGetFormalArmy(world, stack, out var army) || army == null)
                return false;
            HexMath.ToWorldPosition(army.CurrentHex, world.HexWorld.HexSize, out var sx, out var sy);
            HexMath.ToWorldPosition(anchorHex, world.HexWorld.HexSize, out var ax, out var ay);
            var dx = sx - ax;
            var dy = sy - ay;
            var dist = (float)Math.Sqrt(dx * dx + dy * dy);
            return dist <= GetWorldRadius(world);
        }
    }
}
