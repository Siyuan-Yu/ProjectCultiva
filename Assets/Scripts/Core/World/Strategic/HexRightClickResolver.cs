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
    /// Active Enemy Army → Enemy Lingering → Friendly Enter → WorldSite Enter Menu → Direct Move（最后 fallback）。
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
            FormalArmy selectedArmy = null)
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

            resolution.HasEnemyResidualPresentation =
                !string.IsNullOrEmpty(ctx.EnemyResidualSummary);

            var hasAttackArmy = hasSelectedLivingArmy && ctx.HasActiveEnemyArmy;
            var hasAttackLingering = hasSelectedLivingArmy && ctx.CanAttackEnemyLingering;
            var hasEnterFriendly = ctx.CanEnterFriendlyLingering;

            if (hasEnterFriendly && !hasAttackArmy)
            {
                resolution.Action = HexRightClickResolvedAction.DirectEnterFriendlyLingering;
                return resolution;
            }

            resolution.MenuActions.Clear();
            if (hasAttackArmy)
                resolution.MenuActions.Add(HexStrategicContextActionKind.AttackArmy);
            if (hasEnterFriendly && hasAttackArmy)
                resolution.MenuActions.Add(HexStrategicContextActionKind.EnterLingeringBattlefield);
            if (hasAttackLingering)
                resolution.MenuActions.Add(HexStrategicContextActionKind.AttackLingeringBattlefield);

            if (resolution.MenuActions.Count > 0)
            {
                resolution.Action = HexRightClickResolvedAction.ShowAttackTargetMenu;
                return resolution;
            }

            if (hasEnterFriendly)
            {
                resolution.Action = HexRightClickResolvedAction.DirectEnterFriendlyLingering;
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
                if (hasSelectedLivingArmy &&
                    resolution.HasEnemyResidualPresentation &&
                    !ctx.CanAttackEnemyLingering)
                {
                    resolution.StatusHint =
                        "敌方残留存在但残留战场 Runtime 不可用（数据不一致），禁止普通移动";
                    resolution.Action = HexRightClickResolvedAction.None;
                    return resolution;
                }

                resolution.Action = HexRightClickResolvedAction.DirectMove;
                return resolution;
            }

            if (!hasSelectedLivingArmy &&
                (ctx.HasActiveEnemyArmy || ctx.CanAttackEnemyLingering))
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
