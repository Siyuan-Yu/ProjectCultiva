using System;
using System.Collections.Generic;
using System.Text;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex 战略：WorldSite LocalMap 准入与栈接战描述。</summary>
    public static class StrategicSiteAccessService
    {
        public static bool HasPartyMemberAtSite(SimulationWorld world, string siteId)
        {
            if (world == null || string.IsNullOrEmpty(siteId))
                return false;

            foreach (var kv in world.WorldPresence.All)
            {
                if (IsPartyMemberAtSite(world, kv.Value, siteId))
                    return true;
            }

            return false;
        }

        public static int CountPartyMembersAtSite(SimulationWorld world, string siteId)
        {
            if (world == null || string.IsNullOrEmpty(siteId))
                return 0;
            var count = 0;
            foreach (var kv in world.WorldPresence.All)
            {
                if (IsPartyMemberAtSite(world, kv.Value, siteId))
                    count++;
            }

            return count;
        }

        public static string BuildSiteDetailText(SimulationWorld world, WorldSite site)
        {
            if (site == null)
                return string.Empty;
            var sb = new StringBuilder(DescribeSite(site));
            sb.Append('\n').Append(StrategicAcceptanceInspector.BuildSiteOwnerLine(world, site));
            StrategicAcceptanceInspector.AppendCaptureObjectivesForSite(world, site, sb);
            var here = CountPartyMembersAtSite(world, site.SiteId);
            sb.Append("\n我方在场：").Append(here > 0 ? here + " 人" : "无");
            if (!string.IsNullOrEmpty(site.LocalMapId))
                sb.Append("\n场景：").Append(site.LocalMapId);
            var access = CanEnterSiteLocalMap(world, site.SiteId);
            sb.Append(access.IsSuccess ? "\n可进入 LocalMap" : "\n" + access.Error.Message);
            return sb.ToString();
        }

        public static Result CanEnterSiteLocalMap(SimulationWorld world, string siteId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (string.IsNullOrEmpty(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "siteId required.");
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", siteId);

            if (!HasPartyMemberAtSite(world, siteId))
                return Result.Failure(ErrorCode.InvalidOperation, "无己方角色在此地点，无法进入场景。");

            return Result.Success();
        }

        public static string DescribeSite(WorldSite site)
        {
            if (site == null)
                return string.Empty;
            return string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
        }

        public static string DescribeSite(SimulationWorld world, string siteId)
        {
            if (world?.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGet(siteId, out var site) &&
                site != null)
                return DescribeSite(site);
            return siteId ?? string.Empty;
        }

        public static string ResolveStackTravelTarget(ArmyStack stack)
        {
            if (stack == null)
                return string.Empty;
            return stack.SiteId ?? string.Empty;
        }

        public static string DescribeStackTravelTarget(SimulationWorld world, ArmyStack stack)
        {
            if (stack == null)
                return string.Empty;
            var stackName = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
            if (!string.IsNullOrEmpty(stack.SiteId) &&
                world?.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGet(stack.SiteId, out var site) &&
                site != null)
            {
                var siteName = DescribeSite(site);
                return "接战「" + stackName + "」@" + siteName;
            }

            return "接战「" + stackName + "」";
        }

        public static bool CanEngageStackNow(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack) =>
            StrategicEngageRules.CanEngageStackNow(world, party, stack);

        public static bool IsAgentAtStackAnchor(
            SimulationWorld world,
            WorldAgentPresence p,
            ArmyStack stack) =>
            StrategicEngageRules.IsAgentColocatedWithStack(world, p, stack);

        public static bool IsPartyAtStackAnchor(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            if (world == null || stack == null || party == null || party.Count == 0)
                return false;

            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone ||
                    !world.WorldPresence.TryGet(party[i], out var p) ||
                    p == null ||
                    !StrategicEngageRules.IsAgentColocatedWithStack(world, p, stack))
                    return false;
            }

            return true;
        }

        public static void CollectPartyReadyToEngageStack(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack,
            List<EntityId> into) =>
            StrategicEngageRules.CollectPartyReadyToEngageStack(world, party, stack, into);

        static bool IsPartyMemberAtSite(
            SimulationWorld world,
            WorldAgentPresence p,
            string siteId)
        {
            if (p == null || string.IsNullOrEmpty(siteId) || !IsPlayerAgent(world, p.EntityId))
                return false;

            if (p.Mode == PartyWorldPresenceMode.AtSite)
                return string.Equals(p.SiteId, siteId, StringComparison.Ordinal);

            if (p.Mode == PartyWorldPresenceMode.InEncounter)
                return string.Equals(p.SiteId, siteId, StringComparison.Ordinal);

            return false;
        }

        static bool IsPlayerAgent(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;
            return (entity.Tags & EntityTag.Npc) == 0;
        }
    }
}
