using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Construction;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Opportunity;
using XianXia.Core.Npc;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Cheat · Content flags / events。</summary>
    public sealed class LevelTesterCheatContentSection
    {
        readonly ContentDebugService _debug = new ContentDebugService();
        string _flagInput = "story:debug_flag";
        string _eventInput = "base:event_herb_whisper";
        string _dump = string.Empty;
        string _sectionStatus = string.Empty;
        string _pendingConfirmation = string.Empty;
        readonly Dictionary<string, string> _toolStatuses = new Dictionary<string, string>();
        string _statusToolId = string.Empty;

        public string SectionStatus => _sectionStatus;

        public void ResetSessionState()
        {
            _dump = string.Empty;
            _sectionStatus = string.Empty;
            _pendingConfirmation = string.Empty;
            _toolStatuses.Clear();
            _statusToolId = string.Empty;
        }

        public void CancelPendingConfirmation() => _pendingConfirmation = string.Empty;

        public float DrawTool(string toolId, PlayableHostBootstrap host, HostSelectionController selection,
            float x, float y, float width, GUIStyle body)
        {
            ActivateToolStatus(toolId);
            var session = host?.Session;
            var world = session?.World;
            if (world == null || !session.IsInitialized)
            {
                GUI.Label(new Rect(x, y, width, 22f), "会话未就绪。", body);
                return y + 24f;
            }

            switch (toolId)
            {
                case "civilian.satiety": y = DrawCivilianNeedTool(world, selection, true, x, y, width, body); break;
                case "civilian.energy": y = DrawCivilianNeedTool(world, selection, false, x, y, width, body); break;
                case "civilian.detained-loyalty": y = DrawDetainedLoyaltyTool(world, selection, x, y, width, body); break;
                case "civilian.grain-add": y = DrawGrainTool(world, true, x, y, width, body); break;
                case "civilian.herb-add": y = DrawHerbTool(world, x, y, width, body); break;
                case "civilian.grain-clear": y = DrawGrainTool(world, false, x, y, width, body); break;
                case "civilian.reset": y = DrawConfirmedTool(toolId, "影响全部救援绑定、恢复点占用、建设工单、凡人状态、荒村 owner、粮食及验收人物 Faction。", () => ExecuteCivilianReset(host), x, y, width, body); break;
                case "focus.huangcun": y = DrawFocusHuangcun(host, x, y, width, body); break;
                case "focus.grain-field": y = DrawFocusTool(host, "base:workarea_labor_yard", x, y, width, body); break;
                case "focus.herb-field": y = DrawFocusTool(host, "base:workarea_herb_field", x, y, width, body); break;
                case "focus.woodcut": y = DrawFocusTool(host, "base:workarea_forest_woodcut", x, y, width, body); break;
                case "focus.medical": y = DrawFocusAsset(host, "base:site_huangcun:room_medical", x, y, width, body); break;
                case "focus.prison": y = DrawFocusAsset(host, "base:site_huangcun:room_prison", x, y, width, body); break;
                case "focus.injured": y = DrawActionTool("定位唯一选中的伤员", () => FocusInjured(host, selection), x, y, width, body); break;
                case "focus.shop": y = DrawFixedFocusTool(host, "base:shop_qingshi_general", 5.14f, 11.10f, x, y, width, body); break;
                case "focus.auction": y = DrawFixedFocusTool(host, "base:auction_house_qingshi", 5.3f, 11.1f, x, y, width, body); break;
                case "commerce.player-wallet": y = DrawPlayerWalletTool(world, x, y, width, body); break;
                case "commerce.shop-wallet": y = DrawShopWalletTool(world, x, y, width, body); break;
                case "commerce.shop-reset": y = DrawActionTool("恢复商店初始库存与资金", () => { world.Commerce.ResetShopForDebug("base:shop_qingshi_general"); _sectionStatus = "成功：验收商店已重置。"; }, x, y, width, body); break;
                case "commerce.auction-items": y = DrawAuctionItemsTool(world, x, y, width, body); break;
                case "commerce.auction-wallet": y = DrawActionTool("设置 10000 下品 / 10 中品 / 5 上品", () => { world.Commerce.PlayerWallet = new XianXia.Core.Inventory.SpiritStoneWallet(10000, 10, 5); _sectionStatus = "成功：拍卖验收钱包已设置。"; }, x, y, width, body); break;
                case "commerce.auction-reset": y = DrawActionTool("恢复拍卖行初始拍品", () => { XianXia.Core.Inventory.AuctionHouseService.ResetForDebug(world.Commerce, "base:auction_house_qingshi", world.Tick.Value); _sectionStatus = "成功：验收拍卖行已重置。"; }, x, y, width, body); break;
                case "quest.secret-a": y = DrawQuestTool(session, "base:quest_social01_cave", x, y, width, body); break;
                case "quest.secret-b": y = DrawQuestTool(session, "base:quest_social01_cave_b", x, y, width, body); break;
                case "quest.general": y = DrawQuestTool(session, "base:quest_social01_general", x, y, width, body); break;
                case "social.relation": y = DrawRelationshipTool(session, selection, x, y, width, body); break;
                case "content.flag": y = DrawFlagTool(session, selection, x, y, width, body); break;
                case "content.force-event": y = DrawForceEventTool(session, selection, x, y, width, body); break;
                case "content.merchants": y = DrawActionTool("生成两个同模板临时行商实例", () => SpawnMerchants(session), x, y, width, body); break;
                case "content.hidden-stele": y = DrawActionTool("生成 Hidden 石碑", () => SpawnDynamic(session, "base:world_opportunity_dynamic_hidden_stele"), x, y, width, body); break;
                case "content.notice-package": y = DrawActionTool("生成 PublicNotice 包裹", () => SpawnDynamic(session, "base:world_opportunity_dynamic_public_package"), x, y, width, body); break;
                case "content.visible-herb": y = DrawActionTool("生成 WorldVisible 灵草", () => SpawnDynamic(session, "base:world_opportunity_dynamic_visible_herb"), x, y, width, body); break;
                case "content.clear-dynamic": y = DrawActionTool("清理本组动态机会物体", () => ClearDynamic(session), x, y, width, body); break;
                case "diagnostic.movement": y = DrawMovementDiagnostic(world, host, selection, x, y, width, body); break;
                case "diagnostic.character": y = DrawCharacterDiagnostic(world, host, selection, x, y, width, body); break;
                case "diagnostic.needs": y = DrawNeedsDiagnostic(world, selection, x, y, width, body); break;
                case "diagnostic.rescue": y = DrawRescueDiagnostic(world, selection, x, y, width, body); break;
                case "diagnostic.prison": y = DrawPrisonDiagnostic(world, x, y, width, body); break;
                case "diagnostic.construction": y = DrawConstructionDiagnostic(world, x, y, width, body); break;
                case "diagnostic.encounter": y = DrawEncounterDiagnostic(world, x, y, width, body); break;
                case "diagnostic.shop": y = DrawShopDiagnostic(world, x, y, width, body); break;
                case "diagnostic.auction": y = DrawAuctionDiagnostic(world, x, y, width, body); break;
                case "diagnostic.quest": y = DrawQuestDiagnostic(session, selection, x, y, width, body); break;
                case "diagnostic.diplomacy": y = DrawDiplomacyDiagnostic(world, x, y, width, body); break;
                case "diagnostic.delayed-events": y = DrawDelayedEventDiagnostic(world, x, y, width, body); break;
                case "diagnostic.content": y = DrawContentDiagnostic(session, selection, x, y, width, body); break;
            }
            _toolStatuses[toolId] = _sectionStatus ?? string.Empty;
            return y;
        }

        void ActivateToolStatus(string toolId)
        {
            if (string.Equals(_statusToolId, toolId, System.StringComparison.Ordinal)) return;
            if (!string.IsNullOrEmpty(_statusToolId)) _toolStatuses[_statusToolId] = _sectionStatus ?? string.Empty;
            _statusToolId = toolId ?? string.Empty;
            _sectionStatus = _toolStatuses.TryGetValue(_statusToolId, out var status) ? status : string.Empty;
        }

        float DrawCivilianNeedTool(XianXia.Core.Simulation.SimulationWorld world,
            HostSelectionController selection, bool satiety, float x, float y, float width, GUIStyle body)
        {
            var valid = TrySelectedCivilian(world, selection, out var state);
            GUI.Label(new Rect(x, y, width, 22f), valid
                ? "目标 EntityId=" + selection.State.SelectedIds[0].Value + "　当前" + (satiety ? "饱食=" + state.Satiety : "精力=" + state.Energy)
                : "目标：请唯一选中一名已有正式 MortalCivilianState 的凡人。", body);
            y += 28f;
            GUI.enabled = valid;
            if (satiety)
            {
                if (GUI.Button(new Rect(x, y, 140f, 26f), "正常 80")) { state.Satiety = 80; _sectionStatus = "饱食已设为 80。"; }
                if (GUI.Button(new Rect(x + 146f, y, 140f, 26f), "低饱食 10")) { state.Satiety = 10; _sectionStatus = "饱食已设为 10。"; }
            }
            else
            {
                if (GUI.Button(new Rect(x, y, 110f, 26f), "正常 80")) { state.Energy = 80; _sectionStatus = "精力已设为 80。"; }
                if (GUI.Button(new Rect(x + 116f, y, 140f, 26f), "需要睡眠 30")) { state.Energy = 30; _sectionStatus = "精力已设为 30。"; }
                if (GUI.Button(new Rect(x + 262f, y, 140f, 26f), "紧急疲劳 5")) { state.Energy = 5; _sectionStatus = "精力已设为 5。"; }
            }
            GUI.enabled = true;
            return DrawResult(x, y + 34f, width, body);
        }

        float DrawDetainedLoyaltyTool(XianXia.Core.Simulation.SimulationWorld world,
            HostSelectionController selection, float x, float y, float width, GUIStyle body)
        {
            var valid = TrySelectedCivilian(world, selection, out var state) && state.Disposition == CivilianDisposition.Detained;
            GUI.Label(new Rect(x, y, width, 38f), valid ? "目标：Detained EntityId=" + selection.State.SelectedIds[0].Value : "仅允许唯一选中的 Detained 凡人。", body);
            y += 44f; GUI.enabled = valid;
            if (GUI.Button(new Rect(x, y, width, 26f), "忠诚设为 10"))
            { XianXia.Core.Social.CharacterFactionLoyaltyService.SetLoyalty(world, selection.State.SelectedIds[0], 10); _sectionStatus = "成功：囚犯忠诚已设为 10。"; }
            GUI.enabled = true; return DrawResult(x, y + 34f, width, body);
        }

        float DrawHerbTool(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            var siteId = ResolveAcceptanceSite(world);
            var count = WorldSitePublicStockService.GetCount(world, siteId, MortalCivilianService.HerbResourceId);
            GUI.Label(new Rect(x, y, width, 22f), "实际对象：" + siteId + "　当前灵药=" + count, body); y += 28f;
            if (GUI.Button(new Rect(x, y, width, 26f), "公库灵药 +20"))
            {
                var result = WorldSitePublicStockService.TryAdd(world, siteId, MortalCivilianService.HerbResourceId, 20);
                _sectionStatus = result.IsSuccess ? "成功：已加入灵药20份。" : "失败：" + result.Error;
            }
            return DrawResult(x, y + 34f, width, body);
        }

        float DrawGrainTool(XianXia.Core.Simulation.SimulationWorld world, bool add,
            float x, float y, float width, GUIStyle body)
        {
            var siteId = ResolveAcceptanceSite(world);
            var count = WorldSitePublicStockService.GetCount(world, siteId, MortalCivilianService.GrainResourceId);
            GUI.Label(new Rect(x, y, width, 22f), "实际对象：" + siteId + "　当前粗粮=" + count, body); y += 28f;
            if (GUI.Button(new Rect(x, y, width, 26f), add ? "加入粗粮 20" : "清空粗粮"))
            {
                var result = add
                    ? WorldSitePublicStockService.TryAdd(world, siteId, MortalCivilianService.GrainResourceId, 20)
                    : WorldSitePublicStockService.TryRemove(world, siteId, MortalCivilianService.GrainResourceId, count);
                _sectionStatus = result.IsSuccess ? (add ? "成功：已加入粗粮 20。" : "成功：粗粮已清空。") : "失败：" + result.Error;
            }
            return DrawResult(x, y + 34f, width, body);
        }

        float DrawActionTool(string label, System.Action action, float x, float y, float width, GUIStyle body)
        {
            if (GUI.Button(new Rect(x, y, width, 26f), label)) action();
            return DrawResult(x, y + 34f, width, body);
        }

        float DrawConfirmedTool(string confirmationId, string impact, System.Action action,
            float x, float y, float width, GUIStyle body)
        {
            GUI.Label(new Rect(x, y, width, 54f), "影响范围：" + impact, body); y += 60f;
            if (_pendingConfirmation != confirmationId)
            {
                if (GUI.Button(new Rect(x, y, width, 26f), "准备执行…"))
                { _pendingConfirmation = confirmationId; _sectionStatus = "等待确认：尚未修改任何状态。"; }
            }
            else
            {
                if (GUI.Button(new Rect(x, y, width * .48f, 26f), "确认执行"))
                { _pendingConfirmation = string.Empty; action(); }
                if (GUI.Button(new Rect(x + width * .52f, y, width * .48f, 26f), "取消"))
                { _pendingConfirmation = string.Empty; _sectionStatus = "已取消：未修改任何状态。"; }
            }
            return DrawResult(x, y + 34f, width, body);
        }

        float DrawResult(float x, float y, float width, GUIStyle body)
        {
            GUI.Label(new Rect(x, y, width, 20f), "最近结果", body);
            GUI.Label(new Rect(x, y + 22f, width, 72f), string.IsNullOrEmpty(_sectionStatus) ? "尚未执行。" : _sectionStatus, body);
            return y + 98f;
        }

        float DrawFocusHuangcun(PlayableHostBootstrap host, float x, float y, float width, GUIStyle body)
        {
            return DrawActionTool("定位荒村 Site Core", () =>
            {
                var world = host.Session.World;
                if (!world.Strategic.Sites.TryGet("base:site_huangcun", out var site) || site == null || !site.HasContinuousCore)
                { _sectionStatus = "失败：荒村 Continuous Site 不可用。"; return; }
                var ok = host.TryFocusContinuousWorldPosition(site.CoreSurfaceId, new WorldVec2(site.CoreWorldX, site.CoreWorldY), out var reason);
                _sectionStatus = ok ? "成功：已定位荒村；未传送或修改状态。" : "失败：" + reason;
            }, x, y, width, body);
        }

        float DrawFocusTool(PlayableHostBootstrap host, string workAreaId,
            float x, float y, float width, GUIStyle body) =>
            DrawActionTool("定位 " + workAreaId, () => FocusWorkArea(host, workAreaId), x, y, width, body);

        float DrawFocusAsset(PlayableHostBootstrap host, string assetId,
            float x, float y, float width, GUIStyle body) =>
            DrawActionTool("定位 " + assetId, () =>
            {
                if (!host.Session.World.OutdoorConstructedAssets.Assets.TryGetValue(assetId, out var asset))
                { _sectionStatus = "失败：房间运行态不存在。"; return; }
                var point = new WorldVec2(asset.WorldX + asset.WorldWidth * .5f,
                    asset.WorldY + asset.WorldHeight * .5f);
                var ok = host.TryFocusContinuousWorldPosition(asset.SurfaceId, point, out var reason);
                _sectionStatus = ok ? "成功：已定位房间；未传送或修改状态。" : "失败：" + reason;
            }, x, y, width, body);

        float DrawFixedFocusTool(PlayableHostBootstrap host, string targetId, float worldX, float worldY,
            float x, float y, float width, GUIStyle body)
        {
            return DrawActionTool("定位 " + targetId, () =>
            {
                var ok = host.TryFocusContinuousWorldPosition("base:surface_main_wilderness_v1", new WorldVec2(worldX, worldY), out var reason);
                _sectionStatus = ok ? "成功：已定位 " + targetId + "；未传送或修改状态。" : "失败：" + reason;
            }, x, y, width, body);
        }

        float DrawPlayerWalletTool(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            var wallet = world.Commerce.PlayerWallet;
            GUI.Label(new Rect(x, y, width, 22f), "实际对象：PlayerParty 共用钱包　当前 " + wallet.Low + " / " + wallet.Mid + " / " + wallet.High, body);
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "标准：10000 / 5 / 2"))
            { world.Commerce.PlayerWallet = new XianXia.Core.Inventory.SpiritStoneWallet(10000, 5, 2); _sectionStatus = "成功：玩家钱包=10000/5/2。"; }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "仅中品：0 / 10 / 0"))
            { world.Commerce.PlayerWallet = new XianXia.Core.Inventory.SpiritStoneWallet(0, 10, 0); _sectionStatus = "成功：玩家钱包=0/10/0。"; }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "仅下品：5000 / 0 / 0"))
            { world.Commerce.PlayerWallet = new XianXia.Core.Inventory.SpiritStoneWallet(5000, 0, 0); _sectionStatus = "成功：玩家钱包=5000/0/0。"; }
            return DrawResult(x, y + 34f, width, body);
        }

        float DrawShopWalletTool(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            const string shopId = "base:shop_qingshi_general";
            if (!world.Commerce.Shops.TryGetValue(shopId, out var shop) || !world.Commerce.Definitions.TryGetValue(shopId, out var definition))
            { GUI.Label(new Rect(x, y, width, 22f), "验收商店运行态不可用。", body); return y + 24f; }
            GUI.Label(new Rect(x, y, width, 22f), "实际对象：" + shopId + "　当前 " + shop.Wallet.Low + " / " + shop.Wallet.Mid + " / " + shop.Wallet.High, body); y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "标准资金（保持库存）"))
            { shop.Wallet = definition.InitialWallet.Copy(); _sectionStatus = "成功：商店资金已恢复，库存未改。"; }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "下品不足：0 / 10 / 2"))
            { shop.Wallet = new XianXia.Core.Inventory.SpiritStoneWallet(0, 10, 2); _sectionStatus = "成功：商店资金=0/10/2。"; }
            return DrawResult(x, y + 34f, width, body);
        }

        float DrawAuctionItemsTool(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            return DrawActionTool("发放粗木 10 / 中品秘籍 1", () =>
            {
                var wood = world.Inventory.TryAdd("base:item_rough_wood", 10);
                var manual = world.Inventory.TryAdd("base:item_manual_jiang_lao_legacy", 1);
                _sectionStatus = "已加入粗木 " + wood + "、中品秘籍 " + manual + "（受背包容量限制）。";
            }, x, y, width, body);
        }

        float DrawQuestTool(PlayableHostSession session, string questId,
            float x, float y, float width, GUIStyle body)
        {
            GUI.Label(new Rect(x, y, width, 22f), "Actor=" + session.PlayerParty.ActiveCharacterId + "　QuestId=" + questId, body); y += 28f;
            if (GUI.Button(new Rect(x, y, width, 26f), "接取任务"))
            {
                var result = new QuestService().TryStart(session.World, questId, session.PlayerParty.ActiveCharacterId);
                _sectionStatus = result.IsSuccess ? "成功：已接取 " + questId : "失败：" + result.Error;
            }
            return DrawResult(x, y + 34f, width, body);
        }

        float DrawRelationshipTool(PlayableHostSession session, HostSelectionController selection,
            float x, float y, float width, GUIStyle body)
        {
            var actor = session.PlayerParty.ActiveCharacterId;
            var target = selection != null && selection.State.Count == 1 ? selection.State.SelectedIds[0] : EntityId.None;
            var valid = !actor.IsNone && !target.IsNone && target != actor;
            GUI.Label(new Rect(x, y, width, 40f), "目标方向：NPC EntityId=" + target.Value + " → Active EntityId=" + actor.Value +
                "　当前=" + session.World.Relationships.Score(target, actor), body); y += 46f;
            GUI.enabled = valid;
            if (GUI.Button(new Rect(x, y, 140f, 26f), "NPC→Active 19")) SetSocialScore(session, selection, 19, false);
            if (GUI.Button(new Rect(x + 146f, y, 140f, 26f), "NPC→Active 20")) SetSocialScore(session, selection, 20, false);
            if (GUI.Button(new Rect(x + 292f, y, 170f, 26f), "反向 Active→NPC 30")) SetSocialScore(session, selection, 30, true);
            GUI.enabled = true; return DrawResult(x, y + 34f, width, body);
        }

        float DrawFlagTool(PlayableHostSession session, HostSelectionController selection,
            float x, float y, float width, GUIStyle body)
        {
            GUI.Label(new Rect(x, y, 50f, 22f), "Flag", body);
            GUI.SetNextControlName("LevelTester.Flag");
            _flagInput = GUI.TextField(new Rect(x + 52f, y, width - 52f, 24f), _flagInput); y += 30f;
            if (GUI.Button(new Rect(x, y, width * .48f, 26f), "设置")) RunFlag(session, selection, true);
            if (GUI.Button(new Rect(x + width * .52f, y, width * .48f, 26f), "清除")) RunFlag(session, selection, false);
            return DrawResult(x, y + 34f, width, body);
        }

        float DrawForceEventTool(PlayableHostSession session, HostSelectionController selection,
            float x, float y, float width, GUIStyle body)
        {
            GUI.Label(new Rect(x, y, 50f, 22f), "Event", body);
            GUI.SetNextControlName("LevelTester.Event");
            _eventInput = GUI.TextField(new Rect(x + 52f, y, width - 52f, 24f), _eventInput); y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "强制呈现")) ForceEvent(session, selection);
            return DrawResult(x, y + 34f, width, body);
        }

        void SpawnMerchants(PlayableHostSession session)
        {
            var result = WorldOpportunityDriver.SpawnAcceptanceInstances(session.World,
                "base:world_opportunity_event02_temporary_merchant", 2, out var summary);
            _sectionStatus = result.IsSuccess ? "已生成：" + summary : "失败：" + result.Error;
        }

        void ClearDynamic(PlayableHostSession session)
        {
            var count = WorldOpportunityDriver.ClearAcceptanceInstances(session.World,
                "base:world_opportunity_dynamic_hidden_stele", "base:world_opportunity_dynamic_public_package",
                "base:world_opportunity_dynamic_visible_herb");
            _sectionStatus = "已清理 " + count + " 个动态机会物体。";
        }

        float DrawMovementDiagnostic(XianXia.Core.Simulation.SimulationWorld world, PlayableHostBootstrap host,
            HostSelectionController selection, float x, float y, float width, GUIStyle body)
        {
            if (selection == null || selection.State.Count != 1 || !world.Entities.TryGet(selection.State.SelectedIds[0], out var entity))
            { GUI.Label(new Rect(x, y, width, 36f), "请只选中一个有效人物。", body); return y + 40f; }
            entity.TryGet<MovementIntentComponent>(out var intent);
            var owner = world.Civilians.TryGet(entity.Id, out var civilian)
                ? MortalCivilianMovementAuthority.Resolve(world, entity.Id, civilian).ToString() : "非受管凡人";
            var core = CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var presence) && presence.HasWorldPosition
                ? presence.SurfaceId + "@(" + presence.WorldPosition.X.ToString("0.###") + "," + presence.WorldPosition.Y.ToString("0.###") + ")" : "—";
            var text = "Entity=" + entity.DisplayName + "〔" + entity.Id.Value + "〕\nMovementOwner=" + owner + "\nCorePosition=" + core +
                       "\nIntent.Active=" + (intent?.Active == true) + " DestinationKey=" + (intent?.DestinationKey ?? "—") +
                       "\nTarget=" + (intent == null ? "—" : intent.TargetSurfaceId + "@(" + intent.TargetWorldX.ToString("0.###") + "," + intent.TargetWorldY.ToString("0.###") + ")") +
                       "\nHostPathState=" + (intent == null ? "—" : intent.HostPathState.ToString()) + " HostArrived=" + (intent?.HostArrived == true) +
                       "\nHostMoving=" + (host.MoveController?.IsMoving(entity.Id) == true) + " Failure=" + (intent?.HostPathFailureReason ?? "—");
            var h = body.CalcHeight(new GUIContent(text), width); GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawCharacterDiagnostic(XianXia.Core.Simulation.SimulationWorld world, PlayableHostBootstrap host,
            HostSelectionController selection, float x, float y, float width, GUIStyle body)
        {
            if (selection == null || selection.State.Count != 1 || !world.Entities.TryGet(selection.State.SelectedIds[0], out var entity))
            { GUI.Label(new Rect(x, y, width, 36f), "请只选中一个有效人物。", body); return y + 40f; }
            var faction = entity.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership) && membership.IsAffiliated
                ? membership.FactionId : "None";
            var loyalty = XianXia.Core.Social.CharacterFactionLoyaltyService.TryGetLoyalty(world, entity.Id, out var score)
                ? score.ToString() : "—";
            var lifecycle = entity.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life) ? life.State.ToString() : "Missing";
            var hp = entity.TryGet<XianXia.Core.Combat.CombatVitalsComponent>(out var vitals) ? vitals.CurrentHp.ToString() : "—";
            var text = "Entity=" + entity.DisplayName + "〔" + entity.Id.Value + "〕\nFaction=" + faction + " Loyalty=" + loyalty +
                       "\nLifecycle=" + lifecycle + " HP=" + hp +
                       "\nPermanentManageable=" + PlayerPartyRuntime.CanPlayerControlCharacter(world, entity.Id) +
                       " InPlayerParty=" + (world.Strategic.PlayerPartyContext?.IsMember(entity.Id) == true) +
                       "\nPendingPersonAction=" + (host.MoveController?.DescribePendingPersonAction() ?? "MoverMissing") +
                       "\nRecruitInteraction=" + (host.GetComponent<HostNpcContextMenu>()?.DescribeRecruitInteraction() ?? "MenuMissing");
            var h = body.CalcHeight(new GUIContent(text), width); GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawNeedsDiagnostic(XianXia.Core.Simulation.SimulationWorld world, HostSelectionController selection,
            float x, float y, float width, GUIStyle body)
        {
            if (selection == null || selection.State.Count != 1 || !world.Civilians.TryGet(selection.State.SelectedIds[0], out var state) || state == null)
            { GUI.Label(new Rect(x, y, width, 36f), "请只选中一个已有 MortalCivilianState 的人物。", body); return y + 40f; }
            var schedulePhase = FactionMortalSchedule.Current(world, world.Entities.TryGet(state.EntityId, out var scheduledEntity) ? scheduledEntity : null,
                out var scheduleId, out var scheduleBlock, out var scheduleReason);
            var text = "EntityId=" + state.EntityId + " State=" + state.Disposition +
                       "\nSatiety=" + state.Satiety + " Energy=" + state.Energy + " Profession=" + state.Profession +
                       " Activity=" + state.Activity + " Schedule=" + scheduleId + "/" +
                       (scheduleBlock == null ? schedulePhase.ToString() : scheduleBlock.Activity.ToString()) +
                       " Reason=" + (string.IsNullOrEmpty(state.ScheduleStatus) ? scheduleReason : state.ScheduleStatus) +
                       "\nSite=" + state.CurrentSiteId + " Residence=" + state.ResidenceWorkAreaId +
                       "\nFoodFetch: Phase=" + state.FoodFetchPhase + " Storage=" + state.FoodFetchStorageRoomId +
                       " Resource=" + state.PendingFoodResourceId + " Status=" + state.FoodFetchStatus +
                       "\nSleep: Phase=" + state.SleepPhase + " StartedTick=" + state.SleepStartedTick +
                       "\nFlee: Source=" + state.FleeSourceSiteId + " Refuge=" + state.RefugeTargetSiteId +
                       " Candidate=" + state.FleeCandidateIndex + " Attempts=" + state.FleeAttemptCount + " Status=" + state.FleeStatus +
                       "\nBackgroundTravel=" + world.BackgroundCharacterTravel.IsTraveling(state.EntityId) +
                       " Materialized=" + world.ContinuousOutdoorMaterialization.IsMaterialized(state.EntityId);
            var h = body.CalcHeight(new GUIContent(text), width); GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawRescueDiagnostic(XianXia.Core.Simulation.SimulationWorld world, HostSelectionController selection,
            float x, float y, float width, GUIStyle body)
        {
            var sb = new System.Text.StringBuilder();
            var selected = selection != null && selection.State.Count == 1 ? selection.State.SelectedIds[0] : EntityId.None;
            sb.AppendLine("Selected=" + selected);
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                if (state == null || (state.RescueTargetId.IsNone && !state.RescueCarrying &&
                    string.IsNullOrEmpty(state.RescueDestinationId) && string.IsNullOrEmpty(state.RescueStatus))) continue;
                var faction = world.Entities.TryGet(state.EntityId, out var rescuer) &&
                              rescuer.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership)
                    ? membership.FactionId : "—";
                sb.AppendLine("Rescuer=" + state.EntityId + " Faction=" + faction + " Target=" + state.RescueTargetId +
                              " Phase=" + (state.RescueCarrying ? "CarryingToBed" :
                                  state.RescueTargetId.IsNone ? "IdleOrBlocked" : "ApproachingTarget"));
                sb.AppendLine("Status=" + state.RescueStatus);
                sb.AppendLine("RecoverySpotId=" + state.RescueDestinationId + " Rejected=" + state.RescueRejectedDestinationId +
                              " Reserved=" + MortalCivilianService.IsRecoverySpotReserved(world, state.RescueDestinationId, EntityId.None));
                if (world.OutdoorConstructedAssets.Assets.TryGetValue(state.RescueDestinationId, out var bed))
                    sb.AppendLine("BedUsage=" +
                                  (RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Medical) ? RoomUsage.Medical :
                                   RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Prison) ? RoomUsage.Prison :
                                   RoomUsage.Residential) +
                                  " Reservation=" + bed.ReservedCharacterId + " Occupant=" + bed.OccupantCharacterId);
                if (world.Entities.TryGet(state.RescueTargetId, out var injured) &&
                    CombatLifeStateService.TryGetLifeStateCountdown(world, injured, out _, out var seconds))
                    sb.AppendLine("BleedOutRemaining=" + seconds + " Stabilized=" +
                                  CombatLifeStateService.IsMedicallyStabilized(world, injured, out _));
                sb.AppendLine("Delivered=" + (!state.RescueTargetId.IsNone && world.RecoverySpotDeliveries.ContainsInjured(state.RescueTargetId)));
            }
            if (sb.Length == 0) sb.AppendLine("当前没有活动救援。 ");
            var text = sb.ToString(); var h = Mathf.Max(60f, body.CalcHeight(new GUIContent(text), width));
            GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawPrisonDiagnostic(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            var sb = new System.Text.StringBuilder();
            const string prisonId = "base:site_huangcun:room_prison";
            var hasDropOff = MortalCivilianService.TryResolvePrisonDropOff(world, prisonId, out var point, out _, out _);
            sb.AppendLine("DropOffId=" + (hasDropOff ? point.StableAssetId : "—"));
            var capacity = 0; var occupied = 0;
            foreach (var bed in RoomBedFacilityService.BedsInRoom(world, prisonId))
            {
                capacity++;
                if (!bed.OccupantCharacterId.IsNone) occupied++;
                sb.AppendLine("Bed=" + bed.StableAssetId + " Reserved=" + bed.ReservedCharacterId +
                              " Occupant=" + bed.OccupantCharacterId);
            }
            sb.AppendLine("PrisonBeds=" + capacity + " Occupied=" + occupied + " Free=" + (capacity - occupied));
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                if (state == null || (state.Disposition != CivilianDisposition.CapturedEscorted && state.Disposition != CivilianDisposition.Detained)) continue;
                var can = MortalCivilianService.CanDetain(world, state.EntityId, prisonId);
                sb.AppendLine("Captive=" + state.EntityId + " State=" + state.Disposition + " Carrier=" + state.CarrierId +
                              " Room=" + state.DetainedResidenceWorkAreaId +
                              " DetainLegal=" + can.IsSuccess + " Reason=" + (can.IsSuccess ? "OK" : can.Error.Message));
                if (!state.CarrierId.IsNone)
                {
                    var attack = MortalTransportConstraintService.ValidateAttack(world, state.CarrierId);
                    sb.AppendLine("CarrierAttackAllowed=" + attack.IsSuccess +
                                  " Reason=" + (attack.IsSuccess ? "OK" : attack.Error.Message));
                }
            }
            var text = sb.ToString(); var h = Mathf.Max(60f, body.CalcHeight(new GUIContent(text), width));
            GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawConstructionDiagnostic(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Jobs=" + world.CivilianConstructionJobs.Jobs.Count);
            foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
            {
                sb.AppendLine(job.JobId + " | " + job.BuildingId + " | Site=" + job.SiteId +
                              " | Labor=" + job.LaborProgress + "/" + CivilianConstructionJobService.RequiredLabor);
                sb.AppendLine("Carrier=" + job.CarrierId + " Payload=" + job.PayloadItemId + "×" + job.PayloadCount + " Error=" + job.LastError);
                foreach (var cost in job.Required)
                { job.Delivered.TryGetValue(cost.ItemId, out var delivered); sb.AppendLine(cost.ItemId + " " + delivered + "/" + cost.Count); }
            }
            var text = sb.ToString(); var h = Mathf.Max(60f, body.CalcHeight(new GUIContent(text), width));
            GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawEncounterDiagnostic(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            var encounter = world.Strategic.CharacterEncounter;
            if (encounter == null) { GUI.Label(new Rect(x, y, width, 24f), "当前没有 CharacterEncounter。", body); return y + 28f; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Encounter=" + encounter.EncounterId + " Phase=" + encounter.Phase + " RosterVersion=" + encounter.RosterVersion);
            if (encounter.Objective != null) sb.AppendLine("SiteId=" + encounter.Objective.SiteId);
            foreach (var participant in encounter.Participants)
                sb.AppendLine("EntityId=" + participant.CharacterId + " Squad=" + participant.SquadId +
                              " Side=" + (participant.Enemy ? "Enemy" : "Friendly") +
                              " TargetId=" + (participant.TargetId == ulong.MaxValue ? "None" : participant.TargetId.ToString()));
            var text = sb.ToString(); var h = body.CalcHeight(new GUIContent(text), width);
            GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawShopDiagnostic(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            const string id = "base:shop_qingshi_general";
            if (!world.Commerce.Shops.TryGetValue(id, out var shop)) { GUI.Label(new Rect(x, y, width, 24f), "验收商店不存在。", body); return y + 28f; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(id + " Wallet=" + shop.Wallet.Low + "L " + shop.Wallet.Mid + "M " + shop.Wallet.High + "H");
            foreach (var stock in shop.Stock) sb.AppendLine(stock.Key + " ×" + stock.Value);
            var text = sb.ToString(); var h = body.CalcHeight(new GUIContent(text), width);
            GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawAuctionDiagnostic(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            const string id = "base:auction_house_qingshi";
            if (!world.Commerce.AuctionHouses.TryGetValue(id, out var house)) { GUI.Label(new Rect(x, y, width, 24f), "验收拍卖行不存在。", body); return y + 28f; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(id + " Wallet=" + house.HouseWallet.Low + "L " + house.HouseWallet.Mid + "M " + house.HouseWallet.High + "H");
            foreach (var listing in house.Listings.Values)
                sb.AppendLine(listing.ListingId + " | " + listing.ItemId + " | " + listing.Status + " | Bid=" + listing.CurrentBidAmount +
                              " " + listing.Grade + " | Escrow=" + listing.PlayerEscrowAmount + " | End=" + listing.EndTick);
            foreach (var claim in house.Claims.Values) sb.AppendLine("Claim " + claim.ClaimId + " | " + claim.ClaimKind + " | " + claim.ItemId + "×" + claim.Quantity);
            var text = sb.ToString(); var h = Mathf.Max(60f, body.CalcHeight(new GUIContent(text), width));
            GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawQuestDiagnostic(PlayableHostSession session, HostSelectionController selection,
            float x, float y, float width, GUIStyle body)
        {
            var actor = session.PlayerParty.ActiveCharacterId;
            var target = selection != null && selection.State.Count == 1 ? selection.State.SelectedIds[0] : EntityId.None;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Actor=" + actor + " Target=" + target + " NPC→Actor=" + session.World.Relationships.Score(target, actor));
            foreach (var quest in session.World.Quests.Runtime.Values)
                if (session.World.Quests.TryGetSpec(quest.QuestId, out var spec) && (spec.IsSecretRealm || quest.QuestId == "base:quest_social01_general"))
                    sb.AppendLine(quest.QuestInstanceId + " | " + spec.QuestKind + " | " + quest.Status);
            foreach (var binding in session.World.QuestCompanions.Bindings.Values)
                sb.AppendLine("NPC=" + binding.CompanionEntityId + " Quest=" + binding.QuestInstanceId + " OriginalSquad=" + binding.OriginalSquadId + " State=" + binding.State);
            var text = sb.ToString(); var h = Mathf.Max(60f, body.CalcHeight(new GUIContent(text), width));
            GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawDiplomacyDiagnostic(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            var sb = new System.Text.StringBuilder();
            if (world.Strategic.Wars?.All != null)
                foreach (var pair in world.Strategic.Wars.All) if (pair.Value != null && pair.Value.Active) sb.AppendLine("War " + pair.Value.WarId);
            sb.AppendLine("Alliances=" + world.Strategic.Alliances.All.Count + " Vassalages=" + world.Strategic.Vassalages.All.Count);
            var text = sb.ToString(); var h = Mathf.Max(60f, body.CalcHeight(new GUIContent(text), width));
            GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawDelayedEventDiagnostic(XianXia.Core.Simulation.SimulationWorld world,
            float x, float y, float width, GUIStyle body)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Pending=" + world.ScheduledContentEvents.Pending.Count);
            foreach (var item in world.ScheduledContentEvents.OrderedPending())
                sb.AppendLine(item.InstanceId + " | " + item.EventId + " | ExecuteTick=" + item.ExecuteTick + " | Actor=" + item.ActorEntityId + " Target=" + item.TargetEntityId);
            if (!string.IsNullOrEmpty(world.ScheduledContentEvents.LastDiagnostic)) sb.AppendLine(world.ScheduledContentEvents.LastDiagnostic);
            var text = sb.ToString(); var h = Mathf.Max(60f, body.CalcHeight(new GUIContent(text), width));
            GUI.TextArea(new Rect(x, y, width, h), text); return y + h + 4f;
        }

        float DrawContentDiagnostic(PlayableHostSession session, HostSelectionController selection,
            float x, float y, float width, GUIStyle body)
        {
            if (GUI.Button(new Rect(x, y, 120f, 26f), "刷新内容状态")) RefreshDump(session, selection);
            y += 34f;
            var h = Mathf.Max(80f, Mathf.Min(900f, body.CalcHeight(new GUIContent(_dump), width)));
            GUI.TextArea(new Rect(x, y, width, h), _dump); return y + h + 4f;
        }

        void ExecuteCivilianReset(PlayableHostBootstrap host)
        {
            var world = host?.Session?.World;
            if (world == null) { _sectionStatus = "失败：会话未就绪。"; return; }
            MortalCivilianService.CancelAllRescues(world);
            world.RecoverySpotDeliveries.Clear();
            CivilianConstructionJobService.CancelAllJobs(world);
            world.Civilians.Clear();
            var mover = host.GetComponent<HostNpcScheduleMover>();
            var farm = host.GetComponent<HostFarmFieldLabor>();
            foreach (var entity in world.Entities.All)
            {
                var state = MortalCivilianService.Ensure(world, entity);
                if (state == null) continue;
                if (host.Session.Registry.TryGetCharacter(entity.DefinitionId, out var definition))
                {
                    if (System.Enum.TryParse(definition.InitialMortalProfession, true, out MortalProfession profession))
                        state.Profession = profession;
                    state.ResidenceWorkAreaId = definition.HomeWorkAreaId ?? string.Empty;
                    if (entity.TryGet<ActivityTendencyComponent>(out var definitionTendency))
                        definitionTendency.HomeWorkAreaId = state.ResidenceWorkAreaId;
                }
                MortalCivilianService.ResetTransientNeedsActivity(entity, state);
                mover?.CancelCivilianIntent(entity.Id);
                host.MoveController?.CancelPresentationMovementPublic(entity.Id);
                if (farm?.IsFarming(entity.Id) == true) farm.StopNpcScheduleFarmOwnershipOnly(entity.Id);

                var id = entity.DefinitionId.ToString();
                if (id != "base:character_ch01_ref_mortal_b" && id != "base:character_ch01_ref_farmer_b") continue;
                if (entity.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership))
                    membership.Assign("base:sect_huangcun_labor", XianXia.Core.Social.FactionRoleKind.LaborDisciple);
                if (entity.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life))
                { life.State = XianXia.Core.Entities.LifecycleState.Alive; life.ClearBleedOut(); }
                XianXia.Core.Combat.CombatDamageRules.EnsureVitals(entity);
                if (entity.TryGet<XianXia.Core.Combat.CombatVitalsComponent>(out var vitals) &&
                    entity.TryGet<XianXia.Core.Entities.AttributesComponent>(out var attributes))
                    vitals.CurrentHp = System.Math.Max(1, attributes.GetFinal(XianXia.Core.Attributes.AttributeId.MaxHp));
                state.ResidenceWorkAreaId = string.Empty;
                if (entity.TryGet<ActivityTendencyComponent>(out var tendency)) tendency.HomeWorkAreaId = string.Empty;
                if (id == "base:character_ch01_ref_mortal_b")
                    XianXia.Core.Social.CharacterFactionLoyaltyService.SetLoyalty(world, entity.Id, 10);
                else
                {
                    XianXia.Core.Social.CharacterFactionLoyaltyService.SetLoyalty(world, entity.Id, 60);
                    state.Profession = MortalProfession.Farmer;
                }
            }

            if (world.Strategic.Sites.TryGet("base:site_huangcun", out var site) && site != null)
            {
                WorldSiteOwnershipService.SetOwner(world, site.SiteId, "base:sect_huangcun_labor");
                var grain = WorldSitePublicStockService.GetCount(world, site.SiteId, MortalCivilianService.GrainResourceId);
                if (grain > 0) WorldSitePublicStockService.TryRemove(world, site.SiteId, MortalCivilianService.GrainResourceId, grain);
                WorldSitePublicStockService.TryAdd(world, site.SiteId, MortalCivilianService.GrainResourceId, 20);
                var offset = -.25f;
                foreach (var entity in world.Entities.All)
                {
                    var id = entity.DefinitionId.ToString();
                    if (id != "base:character_ch01_ref_mortal_b" && id != "base:character_ch01_ref_farmer_b") continue;
                    var position = new WorldVec2(site.CoreWorldX + offset, site.CoreWorldY - .35f);
                    world.WorldPresence.SetAtWorldPosition(entity.Id, position, site.CoreSurfaceId);
                    if (world.Civilians.TryGet(entity.Id, out var state)) state.CurrentSiteId = site.SiteId;
                    if (host.ViewSpawner?.Registry != null && host.ViewSpawner.Registry.TryGet(entity.Id, out var view) &&
                        view != null && host.ContinuousOutdoorSurfaceRuntime != null)
                    {
                        host.ContinuousOutdoorSurfaceRuntime.Mapper.WorldToPresentation(position.X, position.Y, out var px, out var py);
                        view.transform.position = new Vector3(px, py, HostPresentationSpace.EntityZ);
                        host.ContinuousOutdoorSurfaceRuntime.CommitContinuousNpcPosition(entity.Id, view.transform.position);
                    }
                    offset += .5f;
                }
            }
            _sectionStatus = "成功：民生状态已重置；阿青忠诚 10，阿土忠诚 60 / Farmer。";
        }

        static bool TrySelectedCivilian(XianXia.Core.Simulation.SimulationWorld world, HostSelectionController selection, out MortalCivilianState state)
        {
            state = null;
            if (selection == null || selection.State.Count != 1 || !world.Entities.TryGet(selection.State.SelectedIds[0], out var entity)) return false;
            // Read-only lookup: merely opening diagnostics must never create/repair civilian state.
            return world.Civilians.TryGet(entity.Id, out state) && state != null;
        }

        static string ResolveAcceptanceSite(XianXia.Core.Simulation.SimulationWorld world)
        {
            return world?.Strategic?.Sites != null &&
                   world.Strategic.Sites.TryGet("base:site_huangcun", out var site) && site != null
                ? site.SiteId
                : string.Empty;
        }

        void FocusWorkArea(PlayableHostBootstrap host, string workAreaId)
        {
            var world = host?.Session?.World;
            if (world == null || !world.TryGetWorkArea(workAreaId, out var area) || area == null)
            {
                _sectionStatus = "失败：未找到明确 WorkArea " + workAreaId + "。";
                return;
            }
            var siteId = ResolveAcceptanceSite(world);
            if (string.IsNullOrEmpty(siteId) ||
                !MortalCivilianQuery.TryResolveWorkAreaWorldCenter(
                    world, siteId, area, out var surfaceId, out var center, out _))
            {
                _sectionStatus = "失败：WorkArea " + workAreaId + " 当前无可显示世界位置。";
                return;
            }
            var ok = host.TryFocusContinuousWorldPosition(surfaceId, center, out var reason);
            _sectionStatus = ok
                ? "成功：已定位 WorkArea " + (string.IsNullOrWhiteSpace(area.Name) ? area.Id : area.Name) +
                  "〔" + area.Id + "〕；未传送人物或修改状态。"
                : "失败：WorkArea " + area.Id + "，" + reason;
        }

        void FocusInjured(PlayableHostBootstrap host, HostSelectionController selection)
        {
            var world = host?.Session?.World;
            if (world == null || selection?.State == null || selection.State.Count != 1)
            {
                _sectionStatus = "失败：请只选中一个明确伤员。";
                return;
            }
            var id = selection.State.SelectedIds[0];
            if (!world.Entities.TryGet(id, out var entity) ||
                !entity.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life) ||
                !entity.TryGet<XianXia.Core.Combat.CombatVitalsComponent>(out var vitals) ||
                !entity.TryGet<XianXia.Core.Entities.AttributesComponent>(out var attrs) ||
                (!life.IsIncapacitated && vitals.CurrentHp >= attrs.GetFinal(XianXia.Core.Attributes.AttributeId.MaxHp)))
            {
                _sectionStatus = "失败：所选 EntityId=" + id.Value + " 不是伤员。";
                return;
            }
            if (!CharacterWorldPresenceQuery.TryResolve(world, id, out var presence) || !presence.HasWorldPosition)
            {
                _sectionStatus = "失败：所选伤员 EntityId=" + id.Value + " 当前不在可定位世界空间。";
                return;
            }
            var ok = host.TryFocusContinuousWorldPosition(presence.SurfaceId, presence.WorldPosition, out var reason);
            _sectionStatus = ok
                ? "成功：已定位伤员 " + entity.DisplayName + "〔EntityId=" + id.Value + "〕；未传送或修改生命。"
                : "失败：伤员 EntityId=" + id.Value + "，" + reason;
        }

        void SetSocialScore(PlayableHostSession session,HostSelectionController selection,int score,bool reverse)
        {
            var actor = session.PlayerParty.ActiveCharacterId;
            var target = selection != null && selection.State.Count == 1 ? selection.State.SelectedIds[0] : EntityId.None;
            if (actor.IsNone || target == actor || !session.World.Entities.TryGet(target,out var npc) ||
                (npc.Tags & XianXia.Core.Entities.EntityTag.Npc) == 0)
            { _sectionStatus = "请先选中一名真实 NPC，并保持合法 Active。"; return; }
            var from = reverse ? actor : target; var to = reverse ? target : actor;
            var result = new XianXia.Core.Social.RelationshipService().Record(session.World,from,to,score-session.World.Relationships.Score(from,to),"social_quest01_acceptance");
            _sectionStatus = result.IsSuccess ? "好感已通过 RelationshipLedger 更新。" : result.Error.ToString();
        }

        void RunFlag(PlayableHostSession session, HostSelectionController selection, bool set)
        {
            var subject = FocusId(session, selection);
            var r = set
                ? _debug.SetFlag(session.World, _flagInput, subject)
                : _debug.ClearFlag(session.World, _flagInput, subject);
            _sectionStatus = r.IsSuccess
                ? (set ? "成功：标记已设置。" : "成功：标记已清除。") +
                  " 实际目标=" + (subject.IsNone ? "全世界" : "EntityId=" + subject.Value) + "。"
                : "失败：" + r.Error;
            RefreshDump(session, selection);
        }

        void SpawnDynamic(PlayableHostSession session, string definitionId)
        {
            var result = WorldOpportunityDriver.SpawnAcceptanceInstances(session.World, definitionId, 1, out var summary);
            _sectionStatus = result.IsSuccess ? "已生成：" + summary : "失败：" + result.Error;
        }

        void ForceEvent(PlayableHostSession session, HostSelectionController selection)
        {
            var r = _debug.ForcePresentEvent(session.World, FocusId(session, selection), _eventInput);
            var target = FocusId(session, selection);
            _sectionStatus = r.IsSuccess
                ? "成功：事件已呈现；实际目标=" + (target.IsNone ? "全世界" : "EntityId=" + target.Value) + "。"
                : "失败：" + r.Error;
            RefreshDump(session, selection);
        }

        void RefreshDump(PlayableHostSession session, HostSelectionController selection)
        {
            _dump = _debug.Dump(session.World, FocusId(session, selection));
        }

        static EntityId FocusId(PlayableHostSession session, HostSelectionController selection)
        {
            if (selection != null && selection.State.Count == 1)
                return selection.State.SelectedIds[0];
            return EntityId.None;
        }
    }
}
