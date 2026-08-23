using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 多场 Lingering Battlefield 注册表：Hex → BattlefieldId → scoped runtime（ENCOUNTER-INV-01..06）。
    /// </summary>
    public sealed class LingeringBattlefieldRegistry
    {
        readonly Dictionary<string, LingeringBattlefieldState> _byId =
            new Dictionary<string, LingeringBattlefieldState>(8, StringComparer.Ordinal);

        readonly Dictionary<(int Q, int R), string> _hexToId =
            new Dictionary<(int Q, int R), string>(8);

        public int Count => _byId.Count;

        public IEnumerable<LingeringBattlefieldState> Enumerate() => _byId.Values;

        public static string AllocateBattlefieldId(HexCoord hex, string offerId)
        {
            var suffix = string.IsNullOrEmpty(offerId) ? "linger" : offerId;
            return "battlefield:" + hex.Q + ":" + hex.R + ":" + suffix;
        }

        public LingeringBattlefieldState RegisterOrUpdate(
            HexCoord hex,
            string enemyStackId,
            string encounterLocalMapId,
            string offerId,
            string encounterLinkId = null)
        {
            if (_hexToId.TryGetValue((hex.Q, hex.R), out var existingId) &&
                _byId.TryGetValue(existingId, out var existing) &&
                existing != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!string.Equals(existing.EnemyStackId, enemyStackId ?? string.Empty, StringComparison.Ordinal))
                {
                    System.Diagnostics.Debug.Fail(
                        "[LingeringBattlefieldRegistry] Duplicate hex " + hex +
                        " with different EnemyStackId: existing=" + existing.EnemyStackId +
                        " incoming=" + enemyStackId);
                }
#endif
                existing.EnemyStackId = enemyStackId ?? string.Empty;
                if (!string.IsNullOrEmpty(encounterLocalMapId))
                    existing.EncounterLocalMapId = encounterLocalMapId;
                if (!string.IsNullOrEmpty(encounterLinkId))
                    existing.EncounterLinkId = encounterLinkId;
                return existing;
            }

            var state = new LingeringBattlefieldState
            {
                BattlefieldId = AllocateBattlefieldId(hex, offerId),
                BattleAnchorHex = hex,
                EnemyStackId = enemyStackId ?? string.Empty,
                EncounterLocalMapId = string.IsNullOrEmpty(encounterLocalMapId)
                    ? StrategicEncounterCatalog.DefaultEncounterLocalMapId
                    : encounterLocalMapId,
                EncounterLinkId = encounterLinkId ?? string.Empty
            };
            _byId[state.BattlefieldId] = state;
            _hexToId[(hex.Q, hex.R)] = state.BattlefieldId;
            return state;
        }

        public bool TryGetById(string battlefieldId, out LingeringBattlefieldState state)
        {
            state = null;
            if (string.IsNullOrEmpty(battlefieldId))
                return false;
            return _byId.TryGetValue(battlefieldId, out state) && state != null;
        }

        public bool TryGetAtHex(HexCoord hex, out LingeringBattlefieldState state)
        {
            state = null;
            if (!_hexToId.TryGetValue((hex.Q, hex.R), out var id))
                return false;
            return TryGetById(id, out state);
        }

        public bool HasAtHex(HexCoord hex) => _hexToId.ContainsKey((hex.Q, hex.R));

        public void Remove(string battlefieldId)
        {
            if (string.IsNullOrEmpty(battlefieldId) ||
                !_byId.TryGetValue(battlefieldId, out var state) ||
                state == null)
                return;

            _byId.Remove(battlefieldId);
            _hexToId.Remove((state.BattleAnchorHex.Q, state.BattleAnchorHex.R));
        }

        /// <summary>
        /// 将当前 Active Encounter 的 spawn 列表与 snap 冻结写入 Registry，并清空 Active spawns。
        /// </summary>
        public static LingeringBattlefieldState CommitActiveSession(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world?.Strategic == null || snap == null)
                return null;

            if (!ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out var hex) &&
                !StrategicEncounterResolveService.TryGetLingeringBattleAnchorHex(world, out hex))
                return null;

            var rt = world.Strategic.Encounter;
            var registry = world.Strategic.LingeringBattlefields;
            var stackId = !string.IsNullOrEmpty(rt.ArmyStackId)
                ? rt.ArmyStackId
                : snap.PrimaryEnemyStackId ?? string.Empty;
            var state = registry.RegisterOrUpdate(
                hex,
                stackId,
                string.IsNullOrEmpty(snap.EncounterLocalMapId)
                    ? rt.LingeringLocalMapId
                    : snap.EncounterLocalMapId,
                snap.OfferId,
                rt.EncounterLinkId);

            state.Participants.CopyFrom(snap);
            state.FieldCleared = rt.FieldCleared;

            // 替换而非 merge：避免同 Hex re-commit / 串 scope 时累积他场 ID
            state.ClearTrackedIds();
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
                state.TrackSpawn(rt.SpawnedEntityIds[i]);

            rt.ClearTrackedIds();
            rt.ClearActiveEncounterSession();
            rt.BattlefieldLingering = registry.Count > 0;
            return state;
        }

        public static void BeginLocalMapSession(
            SimulationWorld world,
            LingeringBattlefieldState battlefield)
        {
            if (world?.Strategic == null || battlefield == null)
                return;

            var rt = world.Strategic.Encounter;
            rt.ActiveBattlefieldId = battlefield.BattlefieldId;
            rt.ArmyStackId = battlefield.EnemyStackId;
            rt.EncounterLinkId = string.IsNullOrEmpty(battlefield.EncounterLinkId)
                ? "linger"
                : battlefield.EncounterLinkId;
            rt.LingeringLocalMapId = battlefield.EncounterLocalMapId;
            rt.FieldCleared = battlefield.FieldCleared;
            rt.BattlefieldLingering = true;
            rt.SetLingeringBattleAnchorHex(battlefield.BattleAnchorHex);
            battlefield.Participants.CopyInto(world.Strategic.Participants);
        }
    }
}
