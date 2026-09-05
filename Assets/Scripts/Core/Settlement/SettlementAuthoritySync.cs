using System;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Settlement
{
    /// <summary>由当前玩家拥有的 WorldSite 重建聚落权限；不以历史旗标或 ControlCore 本地状态为准。</summary>
    public static class SettlementAuthoritySync
    {
        public static void Rebuild(SimulationWorld world)
        {
            if (world?.SettlementAuthority == null)
                return;

            world.SettlementAuthority.Clear();
            var playerFactionId = world.Strategic?.PlayerFactionId ?? string.Empty;
            var hasPlayerOwnedCore = false;
            if (!string.IsNullOrEmpty(playerFactionId))
            {
                foreach (var pair in world.ControlCores.All)
                {
                    var core = pair.Value;
                    if (core == null || !CaptureObjectiveService.TryResolveCurrentOwner(
                            world, core, out _, out var ownerFactionId) ||
                        !string.Equals(ownerFactionId, playerFactionId, StringComparison.Ordinal))
                        continue;
                    world.SettlementAuthority.GrantAll(core.GrantsPrivileges);
                    hasPlayerOwnedCore = true;
                    world.Flags.Set("control_core_owned:" + core.WorkAreaId);
                }
            }

            if (hasPlayerOwnedCore)
                world.Flags.Set("settlement_player_controlled");
            else
                world.Flags.Clear("settlement_player_controlled");

            foreach (var pair in world.ControlCores.All)
            {
                var key = "control_core_owned:" + pair.Key;
                if (!world.Flags.Has(key))
                    continue;
                var core = pair.Value;
                if (core == null || !CaptureObjectiveService.TryResolveCurrentOwner(
                        world, core, out _, out var ownerFactionId) ||
                    !string.Equals(ownerFactionId, playerFactionId, StringComparison.Ordinal))
                    world.Flags.Clear(key);
            }
        }
    }
}
