using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum HexRightClickResolvedAction
    {
        None,
        DirectMove,
        DirectAttackArmy,
        DirectAttackLingeringBattlefield,
        DirectEnterFriendlyLingering,
        ShowWorldSiteEnterMenu,
        ShowAttackTargetMenu
    }

    public sealed class HexRightClickResolution
    {
        public HexRightClickResolvedAction Action { get; set; } = HexRightClickResolvedAction.None;
        public string StatusHint { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public bool HasEnemyResidualPresentation { get; set; }
        public List<HexStrategicContextActionKind> MenuActions { get; } = new List<HexStrategicContextActionKind>(3);
        public HexResidualContextView Context { get; set; }
    }

    /// <summary>
    /// Hex 右键 RTS 决策：
    /// Active living Enemy FormalArmy → WorldSite Enter Menu → Direct Move（最后 fallback）。
    /// Residual 不参与 command resolution：Residual-only Hex 永远允许普通移动（只要 tile passable）。
    /// </summary>
    public static class HexRightClickResolver
    {
        public static HexRightClickResolution Resolve(
            SimulationWorld world,
            HexCoord hex,
            string attackerFactionId,
            bool hasSelectedLivingArmy,
            bool hasSelectedMovableArmy,
            bool passableHex,
            FormalArmy selectedArmy = null,
            bool canPlayerPartyAttackTarget = false)
        {
            var resolution = new HexRightClickResolution
            {
                Context = HexResidualContextQuery.Build(world, hex, attackerFactionId)
            };
            var ctx = resolution.Context;
            if (ctx == null)
            {
                resolution.Action = HexRightClickResolvedAction.None;
                resolution.StatusHint = "无法解析 Hex 战略上下文";
                return resolution;
            }

            // 仅信息性 / debug：Residual presence 不阻止普通移动，不产生 Encounter action。
            resolution.HasEnemyResidualPresentation =
                !string.IsNullOrEmpty(ctx.EnemyResidualSummary);

            // Phase 5S-B2-3.5：PlayerParty 选中且 CanIssueAttackOrder 成立时同样产生 AttackArmy ——
            // 菜单与距离解耦（与 FormalArmy 一致）：无论 PlayerParty 在同 Hex / 邻 Hex / 5+ Hex 外，
            // 只要目标是合法 living Enemy FormalArmy 就显示「攻击军队」；点击后由 command service
            // 决定立即接战或先追击（远距离不再只显示普通旅行）。
            var hasAttackArmy = (hasSelectedLivingArmy || canPlayerPartyAttackTarget) && ctx.HasActiveEnemyArmy;

            resolution.MenuActions.Clear();
            if (hasAttackArmy)
                resolution.MenuActions.Add(HexStrategicContextActionKind.AttackArmy);

            if (resolution.MenuActions.Count > 0)
            {
                resolution.Action = HexRightClickResolvedAction.ShowAttackTargetMenu;
                return resolution;
            }

            if (TryResolveShowWorldSiteEnterMenu(
                    world,
                    hex,
                    selectedArmy,
                    hasSelectedLivingArmy,
                    resolution))
                return resolution;

            if (hasSelectedMovableArmy && passableHex)
            {
                resolution.Action = HexRightClickResolvedAction.DirectMove;
                return resolution;
            }

            if (!hasSelectedLivingArmy && ctx.HasActiveEnemyArmy)
            {
                resolution.StatusHint = "请先左键选中我方军团";
            }
            else if (!hasSelectedMovableArmy)
            {
                resolution.StatusHint = "请左键选中军团，再右键 Hex 移动";
            }

            resolution.Action = HexRightClickResolvedAction.None;
            return resolution;
        }

        public static bool TryGetAttackableEnemyLingeringBattlefieldAtHex(
            SimulationWorld world,
            HexCoord hex,
            out LingeringBattlefieldContext battlefield)
        {
            battlefield = null;
            if (!LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(world, hex, out var ctx) ||
                ctx == null ||
                !ctx.HasAttackableEnemyLingering)
                return false;

            battlefield = ctx;
            return true;
        }

        public static bool TryGetFriendlyLingeringBattlefieldAtHex(
            SimulationWorld world,
            HexCoord hex,
            out LingeringBattlefieldContext battlefield)
        {
            battlefield = null;
            if (!LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(world, hex, out var ctx) ||
                ctx == null ||
                ctx.FriendlyFocusId.IsNone)
                return false;

            battlefield = ctx;
            return true;
        }

        static bool TryResolveShowWorldSiteEnterMenu(
            SimulationWorld world,
            HexCoord hex,
            FormalArmy selectedArmy,
            bool hasSelectedLivingArmy,
            HexRightClickResolution resolution)
        {
            if (resolution == null ||
                !hasSelectedLivingArmy ||
                selectedArmy == null ||
                selectedArmy.State == FormalArmyState.Moving ||
                StrategicClockFreezeService.IsModalEncounter(world))
                return false;

            if (!StrategicWorldSiteAccessService.TryGetEnterableWorldSiteAtHex(world, hex, out var site) ||
                site == null)
                return false;

            if (!StrategicWorldSiteAccessService.IsFormalArmyAtSiteFootprint(selectedArmy, site))
                return false;

            var access = StrategicWorldSiteAccessService.CanEnterWorldSiteLocalMap(
                world, site.SiteId, selectedArmy.ArmyId);
            if (access.IsFailure)
            {
                resolution.StatusHint = access.Error.Message;
                return false;
            }

            resolution.Action = HexRightClickResolvedAction.ShowWorldSiteEnterMenu;
            resolution.SiteId = site.SiteId;
            return true;
        }
    }
}
