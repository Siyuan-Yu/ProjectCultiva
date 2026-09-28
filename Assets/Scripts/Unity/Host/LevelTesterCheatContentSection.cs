using UnityEngine;
using XianXia.Core.Construction;
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
        ulong _lastDiagnosticWorldTick = ulong.MaxValue;
        float _lastDiagnosticTickChangeAt;

        public string SectionStatus => _sectionStatus;

        public float Draw(
            PlayableHostBootstrap bootstrap,
            HostSelectionController selection,
            float x,
            float y,
            float width,
            GUIStyle body)
        {
            var lineH = 18f;
            GUI.Label(new Rect(x, y, width, lineH), "内容", body);
            y += lineH + 4f;

            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized)
            {
                GUI.Label(new Rect(x, y, width, lineH), "会话未就绪。");
                return y + lineH;
            }

            y = DrawShop(bootstrap,x,y,width,body);
            y = DrawAuction(bootstrap,x,y,width,body);
            y = DrawCivilian(bootstrap, selection, x, y, width, body);
            RefreshDump(session, selection);
            y = DrawQuestSocial(session,selection,x,y,width,body);
            var schedules = session.World.ScheduledContentEvents;
            GUI.Label(new Rect(x, y, width, lineH), "Scheduled Content Events · pending " + schedules.Pending.Count +
                " · 已履约 " + session.World.ContentCounters.Get("acceptance:delayed_event_presented"), body);
            y += lineH;
            foreach (var item in schedules.OrderedPending())
            {
                var remaining = item.ExecuteTick > session.World.Tick.Value ? item.ExecuteTick - session.World.Tick.Value : 0;
                var info = item.InstanceId + " / " + item.EventId + "\nExecuteTick=" + item.ExecuteTick +
                    " remaining=" + remaining + " ticks / " + ((double)remaining / XianXia.Core.Domain.Time.WorldTick.TicksPerDay).ToString("0.###") +
                    " days\nActor=" + item.ActorEntityId + " Target=" + item.TargetEntityId + " Issuer=" + item.IssuerEntityId +
                    "\nOpportunity=" + item.OpportunityInstanceId + " Key=" + item.TargetKey;
                var height = body.CalcHeight(new GUIContent(info), width);
                GUI.Label(new Rect(x, y, width, height), info, body); y += height + 4f;
            }
            if (!string.IsNullOrEmpty(schedules.LastDiagnostic))
            {
                var height = body.CalcHeight(new GUIContent(schedules.LastDiagnostic), width);
                GUI.Label(new Rect(x, y, width, height), schedules.LastDiagnostic, body); y += height + 4f;
            }

            GUI.Label(new Rect(x, y, 40f, lineH), "标记");
            _flagInput = GUI.TextField(new Rect(x + 44f, y, width - 200f, 22f), _flagInput);
            if (GUI.Button(new Rect(x + width - 148f, y, 68f, 22f), "设置"))
                RunFlag(session, selection, true);
            if (GUI.Button(new Rect(x + width - 74f, y, 68f, 22f), "清除"))
                RunFlag(session, selection, false);
            y += 26f;

            GUI.Label(new Rect(x, y, 40f, lineH), "事件");
            _eventInput = GUI.TextField(new Rect(x + 44f, y, width - 100f, 22f), _eventInput);
            if (GUI.Button(new Rect(x + width - 90f, y, 88f, 22f), "强制呈现"))
                ForceEvent(session, selection);
            y += 26f;

            if (GUI.Button(new Rect(x, y, width, 24f), "QUEST-INSTANCE-01：生成两名同模板临时行商"))
            {
                var result = WorldOpportunityDriver.SpawnAcceptanceInstances(
                    session.World, "base:world_opportunity_event02_temporary_merchant", 2, out var summary);
                _sectionStatus = result.IsSuccess ? "已生成：" + summary : "失败：" + result.Error;
            }
            y += 28f;

            GUI.Label(new Rect(x, y, width, lineH), "DYNAMIC-DISCOVERY-01", body); y += lineH + 2f;
            if (GUI.Button(new Rect(x, y, width, 24f), "生成 Hidden 石碑"))
                SpawnDynamic(session, "base:world_opportunity_dynamic_hidden_stele");
            y += 27f;
            if (GUI.Button(new Rect(x, y, width, 24f), "生成 PublicNotice 包裹"))
                SpawnDynamic(session, "base:world_opportunity_dynamic_public_package");
            y += 27f;
            if (GUI.Button(new Rect(x, y, width, 24f), "生成 WorldVisible 灵草"))
                SpawnDynamic(session, "base:world_opportunity_dynamic_visible_herb");
            y += 27f;
            if (GUI.Button(new Rect(x, y, width, 24f), "清理 DYNAMIC-DISCOVERY-01 验收物体"))
            {
                var count = WorldOpportunityDriver.ClearAcceptanceInstances(session.World,
                    "base:world_opportunity_dynamic_hidden_stele",
                    "base:world_opportunity_dynamic_public_package",
                    "base:world_opportunity_dynamic_visible_herb");
                _sectionStatus = "已清理 " + count + " 个动态机会物体。";
            }
            y += 28f;

            if (GUI.Button(new Rect(x, y, 100f, 24f), "刷新 Dump"))
                RefreshDump(session, selection);
            y += 28f;

            if (!string.IsNullOrEmpty(_sectionStatus))
            {
                GUI.Label(new Rect(x, y, width, lineH * 2f), _sectionStatus, body);
                y += lineH * 2f;
            }

            var dumpH = Mathf.Min(120f, body.CalcHeight(new GUIContent(_dump), width));
            GUI.TextArea(new Rect(x, y, width, dumpH), _dump);
            y += dumpH + 4f;
            return y;
        }

        float DrawShop(PlayableHostBootstrap host,float x,float y,float width,GUIStyle body)
        {
            const string shopId = "base:shop_qingshi_general";
            var board = host.Session.World.Commerce;
            GUI.Label(new Rect(x,y,width,20),"SHOP-TRADE-01 · 指定商店与三档钱包",body); y += 24;
            if (GUI.Button(new Rect(x,y,width,26),"定位 SHOP-TRADE-01 验收商店"))
            {
                host.TryFocusContinuousWorldPosition("base:surface_main_wilderness_v1",new XianXia.Core.World.WorldVec2(5.14f,11.10f),out _sectionStatus);
            }
            y += 30;
            if (GUI.Button(new Rect(x,y,width,26),"玩家标准钱包：10000 下品 / 5 中品 / 2 上品"))
                board.PlayerWallet = new XianXia.Core.Inventory.SpiritStoneWallet(10000,5,2);
            y += 30;
            if (GUI.Button(new Rect(x,y,width,26),"玩家仅中品：0 / 10 / 0（不能买下品商品）"))
                board.PlayerWallet = new XianXia.Core.Inventory.SpiritStoneWallet(0,10,0);
            y += 30;
            if (GUI.Button(new Rect(x,y,width,26),"玩家仅下品：5000 / 0 / 0（不能买中品商品）"))
                board.PlayerWallet = new XianXia.Core.Inventory.SpiritStoneWallet(5000,0,0);
            y += 30;
            if (board.Shops.TryGetValue(shopId,out var shop) && board.Definitions.TryGetValue(shopId,out var def))
            {
                if (GUI.Button(new Rect(x,y,width,26),"商店标准资金（保持库存）")) shop.Wallet = def.InitialWallet.Copy();
                y += 30;
                if (GUI.Button(new Rect(x,y,width,26),"商店下品不足：0 / 10 / 2")) shop.Wallet = new XianXia.Core.Inventory.SpiritStoneWallet(0,10,2);
                y += 30;
                if (GUI.Button(new Rect(x,y,width,26),"Reset Acceptance Shop：恢复初始库存与资金")) board.ResetShopForDebug(shopId);
                y += 30;
            }
            return y + 8;
        }

        float DrawAuction(PlayableHostBootstrap host,float x,float y,float width,GUIStyle body)
        {
            const string houseId="base:auction_house_qingshi";
            var world=host.Session.World; var board=world.Commerce;
            GUI.Label(new Rect(x,y,width,20),"AUCTION-01 · 寄拍、竞价托管与待领取",body);y+=24;
            if(GUI.Button(new Rect(x,y,width,26),"定位 AUCTION-01 验收拍卖行"))
                host.TryFocusContinuousWorldPosition("base:surface_main_wilderness_v1",new XianXia.Core.World.WorldVec2(5.3f,11.1f),out _sectionStatus);
            y+=30;
            if(GUI.Button(new Rect(x,y,width,26),"Auction 标准钱包：10000 下品 / 10 中品 / 5 上品")) board.PlayerWallet=new XianXia.Core.Inventory.SpiritStoneWallet(10000,10,5);
            y+=30;
            if(GUI.Button(new Rect(x,y,width,26),"给玩家验收寄拍物品（粗木 10 / 中品秘籍 1）"))
            {
                var wood=world.Inventory.TryAdd("base:item_rough_wood",10);var manual=world.Inventory.TryAdd("base:item_manual_jiang_lao_legacy",1);
                _sectionStatus="已加入粗木 "+wood+"、中品秘籍 "+manual+"（受背包容量限制）。";
            }
            y+=30;
            if(board.AuctionDefinitions.ContainsKey(houseId) && GUI.Button(new Rect(x,y,width,26),"Reset Acceptance Auction：恢复初始拍品"))
                XianXia.Core.Inventory.AuctionHouseService.ResetForDebug(board,houseId,world.Tick.Value);
            y+=30;
            if(board.AuctionHouses.TryGetValue(houseId,out var house))
            {
                var text="Open Listings / Claims="+house.Claims.Count+" / HouseWallet "+house.HouseWallet.Low+"L "+house.HouseWallet.Mid+"M "+house.HouseWallet.High+"H\n";
                foreach(var listing in house.Listings.Values) if(listing.Status==XianXia.Core.Inventory.AuctionListingStatus.Open)
                {
                    var remaining=listing.EndTick>world.Tick.Value?listing.EndTick-world.Tick.Value:0;
                    text+=listing.ListingId+" | "+listing.ItemId+" | "+listing.CurrentBidAmount+" "+listing.Grade+" | "+listing.CurrentBidderKind+"\nEndTick="+listing.EndTick+" Remaining="+remaining+" Escrow="+listing.PlayerEscrowAmount+"\n";
                }
                foreach(var claim in house.Claims.Values) text+="Claim "+claim.ClaimId+" | "+claim.ClaimKind+" | "+claim.ItemId+" ×"+claim.Quantity+"\n";
                var h=Mathf.Min(190f,body.CalcHeight(new GUIContent(text),width));GUI.TextArea(new Rect(x,y,width,h),text);y+=h+6;
            }
            return y+8;
        }

        float DrawCivilian(PlayableHostBootstrap host, HostSelectionController selection, float x, float y, float width, GUIStyle body)
        {
            var world = host.Session.World;
            GUI.Label(new Rect(x, y, width, 20f), "CIVILIAN-LIFE-01 · 凡人民生 / 占领 / 俘虏", body); y += 24f;
            if (GUI.Button(new Rect(x, y, width, 26f), "定位 CIVILIAN-LIFE-01 荒村"))
            {
                WorldSite found = null;
                foreach (var site in world.Strategic.Sites.Sites)
                    if (site.Value != null && (site.Value.SiteId.IndexOf("huangcun", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        site.Value.DisplayName.IndexOf("荒村", System.StringComparison.OrdinalIgnoreCase) >= 0)) { found = site.Value; break; }
                if (found != null && found.HasContinuousCore)
                    host.TryFocusContinuousWorldPosition(found.CoreSurfaceId, new XianXia.Core.World.WorldVec2(found.CoreWorldX, found.CoreWorldY), out _sectionStatus);
                else _sectionStatus = "未找到荒村 Continuous Site。";
            }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width / 3f - 3f, 24f), "定位粮田")) FocusWorkArea(world, "grain");
            if (GUI.Button(new Rect(x + width / 3f, y, width / 3f - 3f, 24f), "定位药田")) FocusWorkArea(world, "herb");
            if (GUI.Button(new Rect(x + width * 2f / 3f, y, width / 3f, 24f), "定位伐木区")) FocusWorkArea(world, "wood");
            y += 28f;
            if (GUI.Button(new Rect(x, y, width, 24f), "定位伤员 / Medic 验收目标")) FocusInjured(host);
            y += 28f;
            if (GUI.Button(new Rect(x, y, width, 26f), "准备 Rescue：阿石搬阿青，阿土治疗"))
                PrepareCivilianRescue(host);
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "准备建设：当前己方 Site 木材 20 / 阿石待命"))
                PrepareCivilianConstruction(host);
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "公共库存：加入验收粗粮 20"))
            {
                var siteId = ResolveAcceptanceSite(world);
                var result = WorldSitePublicStockService.TryAdd(world, siteId, MortalCivilianService.GrainResourceId, 20);
                _sectionStatus = result.IsSuccess ? "已加入粗粮 20。" : result.Error.ToString();
            }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "公共库存：清空验收粗粮"))
            {
                var siteId = ResolveAcceptanceSite(world);
                var amount = WorldSitePublicStockService.GetCount(world, siteId, MortalCivilianService.GrainResourceId);
                var result = WorldSitePublicStockService.TryRemove(world, siteId, MortalCivilianService.GrainResourceId, amount);
                _sectionStatus = result.IsSuccess ? "粗粮已清空。" : result.Error.ToString();
            }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "选中凡人：饱食正常 80"))
            {
                if (TrySelectedCivilian(world, selection, out var state)) { state.Satiety = 80; _sectionStatus = "饱食已设为 80。"; }
                else _sectionStatus = "请选中一名凡人 NPC。";
            }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "选中凡人：饱食低 10"))
            { if (TrySelectedCivilian(world, selection, out var state)) { state.Satiety = 10; _sectionStatus = "饱食已设为 10。"; } else _sectionStatus = "请选中一名凡人 NPC。"; }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "选中凡人：精力正常 80"))
            { if (TrySelectedCivilian(world, selection, out var state)) { state.Energy = 80; _sectionStatus = "精力已设为 80。"; } else _sectionStatus = "请选中一名凡人 NPC。"; }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "选中凡人：精力需要睡眠 30"))
            { if (TrySelectedCivilian(world, selection, out var state)) { state.Energy = 30; _sectionStatus = "精力已设为 30。"; } else _sectionStatus = "请选中一名凡人 NPC。"; }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "选中凡人：精力 5（紧急地面睡眠）"))
            { if (TrySelectedCivilian(world, selection, out var state)) { state.Energy = 5; _sectionStatus = "精力已设为 5。"; } else _sectionStatus = "请选中一名凡人 NPC。"; }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "准备食宿 A：有粮取食"))
                PrepareCivilianNeedsAcceptance(host, selection, "A");
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "准备食宿 B：正常回床"))
                PrepareCivilianNeedsAcceptance(host, selection, "B");
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "准备食宿 C：紧急睡眠"))
                PrepareCivilianNeedsAcceptance(host, selection, "C");
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "准备食宿 D：无粮且极低精力"))
                PrepareCivilianNeedsAcceptance(host, selection, "D");
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "准备 Supervisor Life：主管满血存活"))
                PrepareSupervisorLife(host);
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "选中被关押凡人：忠诚 10"))
            {
                if (TrySelectedCivilian(world, selection, out var state) && state.Disposition == CivilianDisposition.Detained)
                {
                    XianXia.Core.Social.CharacterFactionLoyaltyService.SetLoyalty(world, selection.State.SelectedIds[0], 10);
                    _sectionStatus = "被关押凡人忠诚已设为 10。";
                }
                else _sectionStatus = "请选中 Detained 凡人。";
            }
            y += 30f;
            if (GUI.Button(new Rect(x, y, width, 26f), "Reset CIVILIAN-LIFE-01 runtime"))
            {
                MortalCivilianService.CancelAllRescues(world);
                XianXia.Core.Construction.CivilianConstructionJobService.CancelAllJobs(world);
                world.Civilians.Clear();
                var scheduleMover = host.GetComponent<HostNpcScheduleMover>();
                var farmLabor = host.GetComponent<HostFarmFieldLabor>();
                foreach (var entity in world.Entities.All)
                {
                    var state = MortalCivilianService.Ensure(world, entity);
                    if (state == null) continue;
                    if (host.Session.Registry.TryGetCharacter(entity.DefinitionId, out var definition))
                    {
                        if (System.Enum.TryParse(definition.InitialMortalProfession, true, out MortalProfession initialProfession))
                            state.Profession = initialProfession;
                        state.ResidenceWorkAreaId = definition.HomeWorkAreaId ?? string.Empty;
                        if (entity.TryGet<ActivityTendencyComponent>(out var definitionTendency))
                            definitionTendency.HomeWorkAreaId = state.ResidenceWorkAreaId;
                    }
                    else
                        state.ResidenceWorkAreaId = entity.TryGet<ActivityTendencyComponent>(out var initialTendency)
                            ? initialTendency.HomeWorkAreaId ?? string.Empty : string.Empty;
                    MortalCivilianService.ResetTransientNeedsActivity(entity, state);
                    scheduleMover?.CancelCivilianIntent(entity.Id);
                    host.MoveController?.CancelPresentationMovementPublic(entity.Id);
                    if (farmLabor?.IsFarming(entity.Id) == true)
                        farmLabor.StopNpcScheduleFarmOwnershipOnly(entity.Id);
                    var acceptance = entity.DefinitionId.ToString() == "base:character_ch01_ref_mortal_b" ||
                                     entity.DefinitionId.ToString() == "base:character_ch01_ref_farmer_b";
                    if (!acceptance) continue;
                    if (entity.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership))
                        membership.Assign("base:sect_huangcun_labor", XianXia.Core.Social.FactionRoleKind.LaborDisciple);
                    if (entity.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life))
                    { life.State = XianXia.Core.Entities.LifecycleState.Alive; life.ClearBleedOut(); }
                    XianXia.Core.Combat.CombatDamageRules.EnsureVitals(entity);
                    if (entity.TryGet<XianXia.Core.Combat.CombatVitalsComponent>(out var vitals) &&
                        entity.TryGet<XianXia.Core.Entities.AttributesComponent>(out var attributes))
                        vitals.CurrentHp = System.Math.Max(1, attributes.GetFinal(XianXia.Core.Attributes.AttributeId.MaxHp));
                    state.ResidenceWorkAreaId = "base:workarea_houses";
                    if (entity.TryGet<ActivityTendencyComponent>(out var tendency))
                        tendency.HomeWorkAreaId = state.ResidenceWorkAreaId;
                    if (entity.DefinitionId.ToString() == "base:character_ch01_ref_mortal_b")
                        XianXia.Core.Social.CharacterFactionLoyaltyService.SetLoyalty(world, entity.Id, 10);
                    if (entity.DefinitionId.ToString() == "base:character_ch01_ref_farmer_b")
                    {
                        XianXia.Core.Social.CharacterFactionLoyaltyService.SetLoyalty(world, entity.Id, 60);
                        state.Profession = MortalProfession.Farmer;
                    }
                }
                if (world.Strategic.Sites.TryGet("base:site_huangcun", out var huangcun))
                {
                    WorldSiteOwnershipService.SetOwner(world, huangcun.SiteId, "base:sect_huangcun_labor");
                    var grain = WorldSitePublicStockService.GetCount(world, huangcun.SiteId, MortalCivilianService.GrainResourceId);
                    if (grain > 0) WorldSitePublicStockService.TryRemove(world, huangcun.SiteId, MortalCivilianService.GrainResourceId, grain);
                    WorldSitePublicStockService.TryAdd(world, huangcun.SiteId, MortalCivilianService.GrainResourceId, 20);
                    var offset = -0.25f;
                    foreach (var entity in world.Entities.All)
                    {
                        var id = entity.DefinitionId.ToString();
                        if (id != "base:character_ch01_ref_mortal_b" && id != "base:character_ch01_ref_farmer_b") continue;
                        world.WorldPresence.SetAtWorldPosition(entity.Id,
                            new XianXia.Core.World.WorldVec2(huangcun.CoreWorldX + offset, huangcun.CoreWorldY - 0.35f),
                            huangcun.CoreSurfaceId);
                        if (host.ViewSpawner?.Registry != null && host.ViewSpawner.Registry.TryGet(entity.Id, out var resetView) &&
                            resetView != null && host.ContinuousOutdoorSurfaceRuntime != null)
                        {
                            host.ContinuousOutdoorSurfaceRuntime.Mapper.WorldToPresentation(
                                huangcun.CoreWorldX + offset, huangcun.CoreWorldY - .35f, out var resetX, out var resetY);
                            resetView.transform.position = new Vector3(resetX, resetY, HostPresentationSpace.EntityZ);
                            host.ContinuousOutdoorSurfaceRuntime.CommitContinuousNpcPosition(entity.Id, resetView.transform.position);
                        }
                        offset += 0.5f;
                        if (world.Civilians.TryGet(entity.Id, out var restored))
                            restored.CurrentSiteId = huangcun.SiteId;
                    }
                }
                _sectionStatus = "民生状态已重置；阿青忠诚 10，阿土忠诚 60 / Farmer。";
            }
            y += 30f;
            var selected = selection != null && selection.State.Count > 0 ? selection.State.SelectedIds[0] : EntityId.None;
            var diagnostic = "Selected=" + selected;
            if (world.Entities.TryGet(selected, out var selectedCharacter))
            {
                var factionId = selectedCharacter.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership) &&
                    membership.IsAffiliated ? membership.FactionId : "None";
                diagnostic += "\nFactionId=" + factionId + " Loyalty=" +
                    (XianXia.Core.Social.CharacterFactionLoyaltyService.TryGetLoyalty(world, selected, out var loyalty)
                        ? loyalty.ToString() : "—");
                if (selectedCharacter.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life))
                {
                    var hp = selectedCharacter.TryGet<XianXia.Core.Combat.CombatVitalsComponent>(out var vitals)
                        ? vitals.CurrentHp.ToString() : "—";
                    var countdown = XianXia.Core.Combat.CombatLifeStateService.TryGetLifeStateCountdown(
                        world, selectedCharacter, out _, out var remaining) ? remaining.ToString() : "—";
                    diagnostic += "\nLife: State=" + life.State + " HP=" + hp +
                                  " BleedOutRemaining=" + countdown +
                                  " IncapacitatedAtTick=" + life.IncapacitatedAtTick +
                                  " LastTransition=" + life.LastTransitionFrom + "->" + life.LastTransitionTo +
                                  " TransitionReason=" + life.LastLifeTransitionReason +
                                  " DeathConfirmationReason=" + life.LastDeathConfirmationReason;
                }
            }
            diagnostic += "\nPendingPersonAction: " +
                          (host.MoveController != null ? host.MoveController.DescribePendingPersonAction() : "MoverMissing") +
                          "\nRecruitInteraction: " +
                          (host.GetComponent<HostNpcContextMenu>() != null
                              ? host.GetComponent<HostNpcContextMenu>().DescribeRecruitInteraction()
                              : "MenuMissing");
            if (world.Civilians.TryGet(selected, out var civilian))
            {
                world.Entities.TryGet(selected, out var selectedEntity);
                MovementIntentComponent movementIntent = null;
                var hasMovementIntent = selectedEntity != null &&
                    selectedEntity.TryGet<MovementIntentComponent>(out movementIntent);
                var farmLabor = host.GetComponent<HostFarmFieldLabor>();
                var moveController = host.MoveController;
                var movementOwner = MortalCivilianMovementAuthority.Resolve(world, selected, civilian);
                var hasSquad = world.Strategic.Squads.TryGetForCharacter(selected, out var selectedSquad) &&
                               selectedSquad != null;
                SquadWorldMotionState squadMotion = null;
                var hasSquadMotion = hasSquad &&
                    world.Strategic.SquadWorldMotions.TryGet(selectedSquad.SquadId, out squadMotion) &&
                    squadMotion != null;
                var storageCenter = "—";
                var storageSurface = "—";
                if (world.SiteStorageRooms.TryGet(civilian.FoodFetchStorageRoomId, out var storageRoom))
                {
                    storageCenter = "(" + storageRoom.WorldX.ToString("0.###") + "," +
                                    storageRoom.WorldY.ToString("0.###") + ")";
                    storageSurface = storageRoom.SurfaceId;
                }
                var storageAccess = civilian.HasFoodFetchAccessPoint
                    ? "(" + civilian.FoodFetchAccessWorldX.ToString("0.###") + "," +
                      civilian.FoodFetchAccessWorldY.ToString("0.###") + ")"
                    : "—";
                var corePosition = "—";
                if (CharacterWorldPresenceQuery.TryResolve(world, selected, out var presence) && presence.HasWorldPosition)
                    corePosition = presence.SurfaceId + "@(" + presence.WorldPosition.X.ToString("0.###") + "," +
                                   presence.WorldPosition.Y.ToString("0.###") + ")";
                var actualPosition = "—";
                EntityView selectedView = null;
                if (host.ViewSpawner?.Registry != null && host.ViewSpawner.Registry.TryGet(selected, out selectedView) && selectedView != null)
                {
                    if (host.ContinuousOutdoorSurfaceRuntime != null && host.ContinuousOutdoorSurfaceRuntime.IsActive)
                    {
                        host.ContinuousOutdoorSurfaceRuntime.Mapper.PresentationToWorld(
                            selectedView.transform.position.x, selectedView.transform.position.y, out var actualX, out var actualY);
                        actualPosition = host.ContinuousOutdoorSurfaceRuntime.ActiveSurfaceId + "@(" +
                                         actualX.ToString("0.###") + "," + actualY.ToString("0.###") + ")";
                    }
                    else actualPosition = "Presentation(" + selectedView.transform.position.x.ToString("0.###") + "," +
                                          selectedView.transform.position.y.ToString("0.###") + ")";
                }
                DescribeIntentGrid(host, world, selected, movementIntent, selectedView,
                    out var sourceGrid, out var targetGrid);
                if (_lastDiagnosticWorldTick != world.Tick.Value)
                {
                    _lastDiagnosticWorldTick = world.Tick.Value;
                    _lastDiagnosticTickChangeAt = Time.unscaledTime;
                }
                var tickAdvancing = !host.Session.IsPaused && Time.unscaledTime - _lastDiagnosticTickChangeAt < 2f;
                var firstGate = DescribeMovementGate(host, selected, civilian, movementIntent, farmLabor, moveController);
                var targetKey = hasMovementIntent && movementIntent.Active
                    ? movementIntent.Revision + ":" +
                      (movementIntent.HasWorldTarget
                          ? movementIntent.TargetSurfaceId + "@" + movementIntent.TargetWorldX.ToString("R") + "," + movementIntent.TargetWorldY.ToString("R")
                          : movementIntent.TargetWorkAreaId + "|" + movementIntent.TargetLocationId + "|" + movementIntent.SlotIndex)
                    : "None";
                diagnostic += "\nSatiety=" + civilian.Satiety + " Energy=" + civilian.Energy +
                              "\nProfession=" + civilian.Profession + " CurrentActivity=" + civilian.Activity +
                              " ActualPhase=" + HostMortalActivityPresentation.Describe(civilian) +
                              " CurrentSchedulePhase=" + FactionMortalSchedule.Current(world.Tick) +
                              " CivilianState=" + civilian.Disposition +
                              "\nCivilianMovementOwner=" + movementOwner +
                              "\nSquad: SquadId=" + (hasSquad ? selectedSquad.SquadId : "None") +
                              " MemberCount=" + (hasSquad ? selectedSquad.MemberCharacterIds.Count : 0) +
                              " CommandKind=" + (hasSquad ? selectedSquad.CommandKind.ToString() : "None") +
                              " HasMotion=" + hasSquadMotion +
                              " MotionIsMoving=" + (hasSquadMotion && squadMotion.IsMoving) +
                              "\nFoodFetch: StorageRoomId=" + civilian.FoodFetchStorageRoomId +
                              " SourceSiteId=" + civilian.CurrentSiteId +
                              " FoodCount=" + MortalCivilianService.CountSiteFood(world, civilian.CurrentSiteId) +
                              " Phase=" + civilian.FoodFetchPhase +
                              " StorageCenter=" + storageCenter +
                               " StorageSurface=" + storageSurface +
                               " ResolvedAccessPoint=" + storageAccess +
                               " CandidateIndex=" + civilian.FoodFetchAccessCandidateIndex +
                               " CandidateAttempts=" + civilian.FoodFetchAccessAttemptCount +
                               " FoodResourceId=" + civilian.PendingFoodResourceId +
                              " Status=" + civilian.FoodFetchStatus +
                              "\nResidence=" + civilian.ResidenceWorkAreaId +
                              " CaptiveCarrier=" + civilian.CarrierId +
                              " DetainedResidence=" + civilian.DetainedResidenceWorkAreaId +
                              "\nPosition: Actual=" + actualPosition + " Core=" + corePosition +
                              "\nPermanentManageable=" + (selectedEntity != null &&
                                  PlayerPartyRuntime.CanPlayerControlCharacter(world, selected)) +
                              " InPlayerParty=" + (world.Strategic.PlayerPartyContext?.IsMember(selected) == true) +
                              "\nMovementIntent: Active=" + (hasMovementIntent && movementIntent.Active) +
                              " HasWorldTarget=" + (hasMovementIntent && movementIntent.HasWorldTarget) +
                              " Surface=" + (hasMovementIntent ? movementIntent.TargetSurfaceId : string.Empty) +
                              " Target=(" + (hasMovementIntent ? movementIntent.TargetWorldX.ToString("0.###") : "—") +
                              "," + (hasMovementIntent ? movementIntent.TargetWorldY.ToString("0.###") : "—") + ")" +
                              " PathState=" + (hasMovementIntent ? movementIntent.HostPathState.ToString() : "None") +
                              " TargetKey=" + targetKey +
                              " PathRequested=" + (hasMovementIntent && movementIntent.HostPathRequested) +
                              " PathAccepted=" + (hasMovementIntent && movementIntent.HostPathAccepted) +
                              " PathFailure=" + (hasMovementIntent ? movementIntent.HostPathFailureReason : string.Empty) +
                              " HostArrived=" + (hasMovementIntent && movementIntent.HostArrived) +
                              " SpeedMultiplier=" + (hasMovementIntent ? movementIntent.SpeedMultiplier.ToString("0.###") : "1") +
                              "\nWalkGrid: Source=" + sourceGrid + " Target=" + targetGrid +
                              "\nHostMovement: IsMoving=" + (moveController?.IsMoving(selected) == true) +
                              " EffectiveSpeed=" + (moveController != null
                                  ? moveController.GetEffectiveMoveSpeed(selected).ToString("0.###") : "Unavailable") +
                              " IsFarmWorker=" + (farmLabor?.IsFarming(selected) == true) +
                              " IsHeldForInteraction=" + (moveController?.IsNpcHeldForInteraction(selected) == true) +
                              " SquadOwnsMovement=" +
                              (SquadCommandService.OwnsIndividualSchedule(world, selected) ||
                               SquadWorldMotionService.OwnsCharacter(world, selected)) +
                              " FirstGate=" + firstGate +
                              "\nClock: WorldTick=" + world.Tick.Value + " AdvancingRecently=" + tickAdvancing +
                              " PresentationDeltaTime=" + host.PresentationDeltaTime.ToString("0.####") +
                               " SessionPaused=" + host.Session.IsPaused +
                               "\nFlee: SourceSite=" + civilian.FleeSourceSiteId +
                               " Candidate=" + civilian.FleeCandidateIndex +
                               " Attempts=" + civilian.FleeAttemptCount +
                               " Target=(" + civilian.FleeTargetX.ToString("0.###") + "," +
                               civilian.FleeTargetY.ToString("0.###") + ") Status=" + civilian.FleeStatus +
                               "\nSleep: Phase=" + civilian.SleepPhase +
                              " StartedTick=" + civilian.SleepStartedTick +
                              " RestSource=" +
                              (civilian.SleepPhase == CivilianSleepPhase.SleepingAtResidence ? "Bed" :
                               civilian.SleepPhase == CivilianSleepPhase.SleepingOnGround ? "Ground" : "None");
            }
            var siteEncounter = world.Strategic.CharacterEncounter;
            if (siteEncounter?.Objective != null)
            {
                diagnostic += "\nCurrent Site Encounter: SiteId=" + siteEncounter.Objective.SiteId +
                              " Phase=" + siteEncounter.Phase;
                for (var i = 0; i < siteEncounter.Participants.Count; i++)
                {
                    var participant = siteEncounter.Participants[i];
                    var participantId = new EntityId(participant.CharacterId);
                    world.Entities.TryGet(participantId, out var participantEntity);
                    var lifecycle = participantEntity != null &&
                                    participantEntity.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life)
                        ? life.State.ToString() : "Missing";
                    diagnostic += "\n  EntityId=" + participant.CharacterId +
                                  " Name=" + (participantEntity?.DisplayName ?? "Missing") +
                                  " SquadId=" + participant.SquadId +
                                  " Side=" + (participant.Enemy ? "Enemy" : "Friendly") +
                                  " Lifecycle=" + lifecycle +
                                  " TargetId=" + (participant.TargetId == ulong.MaxValue
                                      ? "None" : participant.TargetId.ToString());
                }
            }
            var h = body.CalcHeight(new GUIContent(diagnostic), width);
            GUI.TextArea(new Rect(x, y, width, h), diagnostic); y += h + 8f;
            foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
            {
                var row = "建设 " + job.JobId + " " + job.BuildingId + " Site=" + job.SiteId +
                          " Labor=" + job.LaborProgress + "/" + XianXia.Core.Construction.CivilianConstructionJobService.RequiredLabor +
                          " Carrier=" + job.CarrierId + " Payload=" + job.PayloadItemId + "×" + job.PayloadCount +
                          " Error=" + job.LastError;
                foreach (var cost in job.Required)
                { job.Delivered.TryGetValue(cost.ItemId, out var delivered); row += "\n" + cost.ItemId + " " + delivered + "/" + cost.Count; }
                var rowHeight = body.CalcHeight(new GUIContent(row), width);
                GUI.TextArea(new Rect(x, y, width, rowHeight), row); y += rowHeight + 4f;
            }
            return y;
        }

        static string DescribeMovementGate(
            PlayableHostBootstrap host, EntityId id, MortalCivilianState civilian,
            MovementIntentComponent intent, HostFarmFieldLabor farm, HostMoveController mover)
        {
            if (host?.Session == null || !host.Session.IsInitialized) return "SessionUnavailable";
            if (host.Session.IsPaused) return "SessionPaused";
            if (host.Session.World.ContentEvents.HasActive) return "ContentEvent";
            var owner = MortalCivilianMovementAuthority.Resolve(host.Session.World, id, civilian);
            if (owner != CivilianMovementOwner.CivilianLocal) return owner.ToString();
            if (farm?.IsFarming(id) == true) return "FarmPresentation";
            if (mover?.IsNpcHeldForInteraction(id) == true) return "InteractionHold";
            if (intent == null || !intent.Active) return "NoActiveMovementIntent";
            if (!intent.HostPathRequested) return "AwaitingHostPathRequest";
            if (!intent.HostPathAccepted) return "HostPathRejected";
            if (intent.HostArrived) return "Arrived";
            if (mover?.IsMoving(id) == true) return "None";
            return HostInputGate.BlockWorldInteraction ? "PlayerInputBlockedOnly" : "HostNotMoving";
        }

        static void DescribeIntentGrid(
            PlayableHostBootstrap host, XianXia.Core.Simulation.SimulationWorld world,
            EntityId entityId, MovementIntentComponent intent, EntityView view,
            out string source, out string target)
        {
            source = target = "Unavailable";
            var grid = host?.MoveController?.WalkGrid;
            if (grid == null) return;
            if (view != null) source = DescribeGridPoint(grid, view.transform.position);
            if (intent == null || !intent.Active) return;
            Vector3 point;
            var scheduleMover = host.GetComponent<HostNpcScheduleMover>();
            if (scheduleMover != null && scheduleMover.TryGetResolvedPresentationTarget(entityId, out point))
            {
                target = DescribeGridPoint(grid, point);
                return;
            }
            if (intent.HasWorldTarget)
            {
                var runtime = host.ContinuousOutdoorSurfaceRuntime;
                if (runtime == null || !runtime.IsActive ||
                    !string.Equals(runtime.ActiveSurfaceId, intent.TargetSurfaceId, System.StringComparison.Ordinal))
                    return;
                runtime.Mapper.WorldToPresentation(intent.TargetWorldX, intent.TargetWorldY, out var px, out var py);
                point = new Vector3(px, py, HostPresentationSpace.EntityZ);
            }
            else
            {
                var locationId = intent.TargetLocationId;
                var ox = 0f; var oy = 0f;
                if (!string.IsNullOrEmpty(intent.TargetWorkAreaId) &&
                    world.TryGetWorkArea(intent.TargetWorkAreaId, out var area))
                {
                    locationId = area.LocationId;
                    ox = area.OffsetX; oy = area.OffsetZ;
                }
                if (!HostZoneQuery.TryGetLocationCenter(world, locationId, out point)) return;
                point += new Vector3(ox, oy, 0f);
            }
            target = DescribeGridPoint(grid, point);
        }

        static string DescribeGridPoint(XianXia.Core.Navigation.WalkGrid grid, Vector3 point)
        {
            if (!grid.TryWorldToCell(point.x, point.y, out var x, out var y)) return "Outside";
            return "(" + x + "," + y + ") Walkable=" + grid.IsWalkable(x, y);
        }

        void PrepareCivilianRescue(PlayableHostBootstrap host)
        {
            var world = host.Session.World;
            if (!world.Strategic.Sites.TryGet("base:site_huangcun", out var site) || !site.HasContinuousCore)
            { _sectionStatus = "黄村 Site 不可用。"; return; }
            XianXia.Core.Entities.Entity rescuer = null, injured = null, medic = null;
            foreach (var entity in world.Entities.All)
            {
                var id = entity.DefinitionId.ToString();
                if (id == "base:character_ch01_ref_mortal_a") rescuer = entity;
                else if (id == "base:character_ch01_ref_mortal_b") injured = entity;
                else if (id == "base:character_ch01_ref_farmer_b") medic = entity;
            }
            if (rescuer == null || injured == null || medic == null)
            { _sectionStatus = "阿石／阿青／阿土缺失。"; return; }
            MortalCivilianService.CancelAllRescues(world);
            var people = new[] { rescuer, injured, medic };
            for (var i = 0; i < people.Length; i++)
            {
                var person = people[i];
                if (!person.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership))
                { membership = new XianXia.Core.Social.FactionMembershipComponent(); person.AddComponent(membership); }
                membership.Assign(site.OwnerFactionId, XianXia.Core.Social.FactionRoleKind.Member);
                if (person.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life))
                { life.State = XianXia.Core.Entities.LifecycleState.Alive; life.ClearBleedOut(); }
                var state = MortalCivilianService.Ensure(world, person);
                state.Disposition = CivilianDisposition.Normal; state.Satiety = state.Energy = 90;
                state.RescueTargetId = EntityId.None; state.RescueCarrying = false;
                state.Profession = person == medic ? MortalProfession.Medic : MortalProfession.Unassigned;
                if (person.TryGet<MovementIntentComponent>(out var intent)) intent.Clear();
                var dx = person == rescuer ? -3f : person == injured ? 3f : -1.5f;
                var pos = new XianXia.Core.World.WorldVec2(site.CoreWorldX + dx, site.CoreWorldY);
                world.WorldPresence.SetAtWorldPosition(person.Id, pos, site.CoreSurfaceId);
                if (host.ViewSpawner?.Registry != null && host.ViewSpawner.Registry.TryGet(person.Id, out var view) &&
                    view != null && host.ContinuousOutdoorSurfaceRuntime != null)
                {
                    host.ContinuousOutdoorSurfaceRuntime.Mapper.WorldToPresentation(pos.X, pos.Y, out var px, out var py);
                    view.transform.position = new Vector3(px, py, HostPresentationSpace.EntityZ);
                }
            }
            XianXia.Core.Combat.CombatDamageRules.EnsureVitals(injured);
            if (!XianXia.Core.Combat.CombatLifeStateService.TryEnterIncapacitated(world, injured))
            { _sectionStatus = "阿青无法进入弥留；请 Reset 后重试。"; return; }
            if (injured.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var injuredLife))
                injuredLife.BleedOutAfterTick = world.Tick.Value + 288UL;
            world.Civilians.GetOrCreate(rescuer.Id).RescueTargetId = injured.Id;
            host.TryFocusContinuousWorldPosition(site.CoreSurfaceId,
                new XianXia.Core.World.WorldVec2(site.CoreWorldX, site.CoreWorldY), out _sectionStatus);
            _sectionStatus = "已准备：阿石非 Medic、阿青弥留、阿土 Medic；观察搬运及送达后治疗。";
        }

        void PrepareCivilianConstruction(PlayableHostBootstrap host)
        {
            var world = host.Session.World;
            if (!XianXia.Core.Inventory.PlayerStrategicResourceService.TryResolveCurrentManagingSite(world, out var site) ||
                !site.HasContinuousCore)
            { _sectionStatus = "请先进入己方实际控制的 Continuous Site。"; return; }
            var added = WorldSitePublicStockService.TryAdd(world, site.SiteId, MortalCivilianService.WoodResourceId, 20);
            if (added.IsFailure) { _sectionStatus = added.Error.ToString(); return; }
            foreach (var entity in world.Entities.All)
            {
                if (entity.DefinitionId.ToString() != "base:character_ch01_ref_mortal_a") continue;
                if (!entity.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership))
                { membership = new XianXia.Core.Social.FactionMembershipComponent(); entity.AddComponent(membership); }
                membership.Assign(world.Strategic.PlayerFactionId, XianXia.Core.Social.FactionRoleKind.Member);
                var state = MortalCivilianService.Ensure(world, entity);
                state.Disposition = CivilianDisposition.Normal; state.Profession = MortalProfession.Unassigned;
                state.Satiety = state.Energy = 90; state.CurrentSiteId = site.SiteId;
                var pos = new XianXia.Core.World.WorldVec2(site.CoreWorldX - 1.5f, site.CoreWorldY);
                world.WorldPresence.SetAtWorldPosition(entity.Id, pos, site.CoreSurfaceId);
                if (host.ViewSpawner?.Registry != null && host.ViewSpawner.Registry.TryGet(entity.Id, out var view) &&
                    view != null && host.ContinuousOutdoorSurfaceRuntime != null)
                {
                    host.ContinuousOutdoorSurfaceRuntime.Mapper.WorldToPresentation(pos.X, pos.Y, out var px, out var py);
                    view.transform.position = new Vector3(px, py, HostPresentationSpace.EntityZ);
                }
                break;
            }
            _sectionStatus = CivilianConstructionJobService.HasAvailableWorker(world, site.SiteId,
                world.Strategic.PlayerFactionId)
                ? "木材已入 Site PublicStock；阿石可搬运。请用正式建设 UI 放置农田／恢复处／储藏室。"
                : "木材已入 Site PublicStock，但当前没有可自主工作的势力凡人；请先解除组队／Squad 调度，再用正式建设 UI 放置建筑。";
        }

        void PrepareSupervisorLife(PlayableHostBootstrap host)
        {
            var world = host?.Session?.World;
            if (world == null || !world.Strategic.Sites.TryGet("base:site_huangcun", out var site) ||
                site == null || !site.HasContinuousCore)
            { _sectionStatus = "荒村 Continuous Site 不可用。"; return; }
            XianXia.Core.Entities.Entity supervisor = null;
            foreach (var entity in world.Entities.All)
                if (entity.DefinitionId.ToString() == "base:character_ch01_ref_supervisor")
                { supervisor = entity; break; }
            if (supervisor == null)
            { _sectionStatus = "杂役主管实体不存在。"; return; }
            if (!supervisor.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life))
            { _sectionStatus = "杂役主管缺少 Lifecycle。"; return; }
            life.State = XianXia.Core.Entities.LifecycleState.Alive;
            life.ClearBleedOut();
            life.IncapacitatedAtTick = 0;
            life.LastDeathConfirmationReason = XianXia.Core.Entities.DeathConfirmationReason.Unknown;
            life.RecordTransition(life.LastTransitionTo, XianXia.Core.Entities.LifecycleState.Alive, "LevelTesterReset");
            supervisor.RemoveComponent<XianXia.Core.Combat.CorpseComponent>();
            supervisor.RemoveComponent<XianXia.Core.Combat.CombatDeathAttributionComponent>();
            XianXia.Core.Combat.CombatDamageRules.EnsureVitals(supervisor);
            if (supervisor.TryGet<XianXia.Core.Combat.CombatVitalsComponent>(out var vitals) &&
                supervisor.TryGet<XianXia.Core.Entities.AttributesComponent>(out var attributes))
                vitals.CurrentHp = System.Math.Max(1,
                    attributes.GetFinal(XianXia.Core.Attributes.AttributeId.MaxHp));
            var position = new WorldVec2(site.CoreWorldX, site.CoreWorldY + .35f);
            world.WorldPresence.SetAtWorldPosition(supervisor.Id, position, site.CoreSurfaceId);
            host.MoveController?.CancelPresentationMovementPublic(supervisor.Id);
            if (host.ViewSpawner?.Registry != null && host.ViewSpawner.Registry.TryGet(supervisor.Id, out var view) &&
                view != null && host.ContinuousOutdoorSurfaceRuntime != null)
            {
                host.ContinuousOutdoorSurfaceRuntime.Mapper.WorldToPresentation(
                    position.X, position.Y, out var px, out var py);
                view.transform.position = new Vector3(px, py, HostPresentationSpace.EntityZ);
                view.SetActivityText(string.Empty);
                host.ContinuousOutdoorSurfaceRuntime.CommitContinuousNpcPosition(supervisor.Id, view.transform.position);
            }
            _sectionStatus = "杂役主管已恢复 Alive、满血、无尸体/弥留，并放回荒村合法世界位置。";
        }

        static bool TrySelectedCivilian(XianXia.Core.Simulation.SimulationWorld world, HostSelectionController selection, out MortalCivilianState state)
        {
            state = null;
            if (selection == null || selection.State.Count == 0 || !world.Entities.TryGet(selection.State.SelectedIds[0], out var entity)) return false;
            state = MortalCivilianService.Ensure(world, entity);
            return state != null;
        }

        void PrepareCivilianNeedsAcceptance(
            PlayableHostBootstrap host, HostSelectionController selection, string scenario)
        {
            var world = host?.Session?.World;
            if (world == null || selection == null || selection.State.Count == 0 ||
                !world.Entities.TryGet(selection.State.SelectedIds[0], out var entity) ||
                !MortalCivilianQuery.IsManagedCivilian(world, entity) ||
                !world.Civilians.TryGet(entity.Id, out var state) ||
                state.Disposition != CivilianDisposition.Normal ||
                world.Strategic.PlayerPartyContext?.IsMember(entity.Id) == true)
            {
                _sectionStatus = "请选择 Normal、非 PlayerParty 的普通凡人。";
                return;
            }

            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            if (string.IsNullOrEmpty(siteId) || !world.Strategic.Sites.TryGet(siteId, out var site) ||
                site == null || !site.HasContinuousCore)
            {
                _sectionStatus = "选中凡人当前不在有效 Continuous Site。";
                return;
            }
            if ((scenario == "B" || scenario == "C") &&
                (string.IsNullOrEmpty(state.ResidenceWorkAreaId) ||
                 !world.TryGetWorkArea(state.ResidenceWorkAreaId, out var residence) ||
                 !MortalActivityEvaluator.HasTag(residence, "home") ||
                 world.Civilians.GetResidenceUsage(state.ResidenceWorkAreaId) != ResidenceUsage.Normal))
            {
                _sectionStatus = "选中凡人没有合法 Normal Residence。";
                return;
            }

            MortalCivilianService.ResetTransientNeedsActivity(entity, state);
            host.GetComponent<HostNpcScheduleMover>()?.CancelCivilianIntent(entity.Id);
            host.MoveController?.CancelPresentationMovementPublic(entity.Id);
            var farm = host.GetComponent<HostFarmFieldLabor>();
            if (farm?.IsFarming(entity.Id) == true) farm.StopNpcScheduleFarmOwnershipOnly(entity.Id);

            if (scenario == "A" || scenario == "D")
            {
                var current = WorldSitePublicStockService.GetCount(world, siteId, MortalCivilianService.GrainResourceId);
                if (current > 0)
                    WorldSitePublicStockService.TryRemove(world, siteId, MortalCivilianService.GrainResourceId, current);
                if (scenario == "A")
                    WorldSitePublicStockService.TryAdd(world, siteId, MortalCivilianService.GrainResourceId, 5);
            }

            state.Satiety = scenario == "A" || scenario == "D" ? 10 : 80;
            state.Energy = scenario == "B" ? 30 : scenario == "A" ? 80 : 5;
            state.Clamp();
            var start = new WorldVec2(site.CoreWorldX, site.CoreWorldY - .35f);
            world.WorldPresence.SetAtWorldPosition(entity.Id, start, site.CoreSurfaceId);
            state.CurrentSiteId = siteId;
            if (host.ViewSpawner?.Registry != null && host.ViewSpawner.Registry.TryGet(entity.Id, out var view) &&
                view != null && host.ContinuousOutdoorSurfaceRuntime != null)
            {
                host.ContinuousOutdoorSurfaceRuntime.Mapper.WorldToPresentation(start.X, start.Y, out var px, out var py);
                view.transform.position = new Vector3(px, py, HostPresentationSpace.EntityZ);
                host.ContinuousOutdoorSurfaceRuntime.CommitContinuousNpcPosition(entity.Id, view.transform.position);
            }

            var grain = WorldSitePublicStockService.GetCount(world, siteId, MortalCivilianService.GrainResourceId);
            _sectionStatus = "食宿 " + scenario + " 已准备：人物=" + entity.DefinitionId +
                             " SiteId=" + siteId + " 粗粮=" + grain +
                             " Satiety=" + state.Satiety + " Energy=" + state.Energy +
                             "；未直接完成移动、取食或睡眠。";
        }

        static string ResolveAcceptanceSite(XianXia.Core.Simulation.SimulationWorld world)
        {
            foreach (var pair in world.Strategic.Sites.Sites)
                if (pair.Value != null && (pair.Value.SiteId.IndexOf("huangcun", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pair.Value.DisplayName.IndexOf("荒村", System.StringComparison.OrdinalIgnoreCase) >= 0)) return pair.Value.SiteId;
            return string.Empty;
        }

        void FocusWorkArea(XianXia.Core.Simulation.SimulationWorld world, string tag)
        {
            foreach (var pair in world.WorkAreas)
            {
                var area = pair.Value;
                if (area == null || !MortalActivityEvaluator.HasTag(area, tag) ||
                    !HostZoneQuery.TryGetLocationCenter(world, area.LocationId, out var center)) continue;
                var camera = Camera.main;
                if (camera != null) camera.transform.position = new Vector3(center.x, center.y, camera.transform.position.z);
                _sectionStatus = "已定位 " + (string.IsNullOrWhiteSpace(area.Name) ? area.Id : area.Name) + "。";
                return;
            }
            _sectionStatus = "未找到 " + tag + " 工区。";
        }

        void FocusInjured(PlayableHostBootstrap host)
        {
            var world = host.Session.World;
            foreach (var entity in world.Entities.All)
            {
                if (!entity.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life) ||
                    !entity.TryGet<XianXia.Core.Combat.CombatVitalsComponent>(out var vitals) ||
                    !entity.TryGet<XianXia.Core.Entities.AttributesComponent>(out var attrs)) continue;
                var max = attrs.GetFinal(XianXia.Core.Attributes.AttributeId.MaxHp);
                if (!life.IsIncapacitated && vitals.CurrentHp >= max) continue;
                if (host.ViewSpawner.Registry.TryGet(entity.Id, out var view) && view != null && Camera.main != null)
                {
                    Camera.main.transform.position = new Vector3(view.transform.position.x, view.transform.position.y, Camera.main.transform.position.z);
                    _sectionStatus = "已定位伤员 " + entity.DisplayName + "。";
                    return;
                }
            }
            _sectionStatus = "当前加载区域没有伤员；可先用战斗制造受伤目标。";
        }

        float DrawQuestSocial(PlayableHostSession session, HostSelectionController selection, float x,float y,float width,GUIStyle body)
        {
            GUI.Label(new Rect(x,y,width,20), "SOCIAL-QUEST-01（关系方向：NPC → 当前 Active，门槛 20）",body); y += 24;
            foreach (var suffix in new[] { "cave", "cave_b", "general" })
            {
                var id = "base:quest_social01_" + suffix;
                if (GUI.Button(new Rect(x,y,width,24), "接取：" + (suffix == "general" ? "普通对照任务" : suffix == "cave" ? "秘境任务 A" : "秘境任务 B")))
                {
                    var result = new QuestService().TryStart(session.World,id,session.PlayerParty.ActiveCharacterId);
                    _sectionStatus = result.IsSuccess ? "已接取 " + id : result.Error.ToString();
                }
                y += 27;
            }
            if (GUI.Button(new Rect(x,y,width,24),"选中 NPC → Active 好感设为 19（不足）")) SetSocialScore(session,selection,19,false); y += 27;
            if (GUI.Button(new Rect(x,y,width,24),"选中 NPC → Active 好感设为 20（达标）")) SetSocialScore(session,selection,20,false); y += 27;
            if (GUI.Button(new Rect(x,y,width,24),"仅 Active → 选中 NPC 好感设为 30（反向对照）")) SetSocialScore(session,selection,30,true); y += 27;
            var actor = session.PlayerParty.ActiveCharacterId;
            var target = selection != null && selection.State.Count > 0 ? selection.State.SelectedIds[0] : EntityId.None;
            var text = "Actor=" + actor + " Target=" + target + " NPC→Actor=" + session.World.Relationships.Score(target,actor) + "\n";
            foreach (var q in session.World.Quests.Runtime.Values)
                if (session.World.Quests.TryGetSpec(q.QuestId,out var spec) && (spec.IsSecretRealm || q.QuestId == "base:quest_social01_general"))
                    text += q.QuestInstanceId + " | " + spec.QuestKind + " | " + q.Status + "\n";
            foreach (var b in session.World.QuestCompanions.Bindings.Values)
                text += "NPC=" + b.CompanionEntityId + " Quest=" + b.QuestInstanceId + "\nOriginalSquad=" + b.OriginalSquadId + " | " + b.State + "\n";
            var height = body.CalcHeight(new GUIContent(text),width);
            GUI.Label(new Rect(x,y,width,height),text,body);
            return y + height + 5;
        }

        void SetSocialScore(PlayableHostSession session,HostSelectionController selection,int score,bool reverse)
        {
            var actor = session.PlayerParty.ActiveCharacterId;
            var target = selection != null && selection.State.Count > 0 ? selection.State.SelectedIds[0] : EntityId.None;
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
                ? (set ? "成功：标记已设置。" : "成功：标记已清除。")
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
            _sectionStatus = r.IsSuccess ? "成功：事件已呈现。" : "失败：" + r.Error;
            RefreshDump(session, selection);
        }

        void RefreshDump(PlayableHostSession session, HostSelectionController selection)
        {
            _dump = _debug.Dump(session.World, FocusId(session, selection));
        }

        static EntityId FocusId(PlayableHostSession session, HostSelectionController selection)
        {
            if (selection != null && selection.State.Count > 0)
                return selection.State.SelectedIds[0];
            if (session?.CharacterIds != null && session.CharacterIds.Count > 0)
                return session.CharacterIds[0];
            return EntityId.None;
        }
    }
}
