using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>Optional Huangcun survival slice. All work spends WorldTick/ActionClock at a real location.</summary>
    public static class HuangcunSurvivalService
    {
        public const string Grain = "base:resource_grain", Wood = "base:resource_rough_wood", Herb = "base:resource_spirit_herb";
        const string CampId = "asset:huangcun:private_camp";
        public static bool Applies(SimulationWorld w, Entity e) => w.HuangcunSurvival.Enabled && e != null &&
            (e.DefinitionId.ToString() == "base:character_protagonist" ||
             e.DefinitionId.ToString() == "base:character_companion_a" || e.DefinitionId.ToString() == "base:character_companion_b");
        public static bool IsPrisoner(SimulationWorld w, EntityId id) => w.HuangcunSurvival.Enabled &&
            !w.HuangcunSurvival.Released && w.HuangcunSurvival.PrisonerId == id.Value;
        public static Entity Prisoner(SimulationWorld w) =>
            w.Entities.TryGet(new EntityId(w.HuangcunSurvival.PrisonerId), out var e) ? e : null;
        static Result Fail(string message) => Result.Failure(ErrorCode.ActionCannotStart, message);

        public static Result InitializeNewGame(SimulationWorld w, SimulationLoop loop, IReadOnlyList<EntityId> roster)
        {
            if (!w.Flags.Has("story:huangcun_survival_v1") || w.HuangcunSurvival.Enabled) return Result.Success();
            var s = w.HuangcunSurvival; var a = w.OpeningArea;
            if (!a.IsConfigured) return Fail("荒村生存开局缺少有效盆地。");
            OutdoorConstructedAssetState prison = null;
            foreach (var room in w.OutdoorConstructedAssets.Assets.Values)
                if (room.Kind == "room" && room.RoomUsage == RoomUsage.Prison && room.SurfaceId == a.SurfaceId &&
                    a.ContainsVillage(room.SurfaceId, room.WorldX, room.WorldY)) { prison = room; break; }
            if (prison == null) return Fail("荒村生存开局缺少实体监狱房间。");
            s.Enabled = true; s.StartedTick = s.LastProcessedTick = w.Tick.Value; s.PrisonRoomId = prison.StableAssetId;
            s.OwnerFactionId = w.Strategic.PlayerFactionId;
            s.Places["prison"] = new WorldVec2(prison.WorldX + prison.WorldWidth / 2, prison.WorldY - a.CellSize * 2);
            var cx = (a.VillageMinX + a.VillageMaxX) / 2; var cy = (a.VillageMinY + a.VillageMaxY) / 2;
            // Resolve once, persist exact points; never search the world each UI frame.
            if (!FindClearing(w, "camp_near", a.VillageMinX - a.CellSize * 24, cy - a.CellSize * 20) ||
                !FindClearing(w, "camp_far", cx + a.CellSize * 25, a.VillageMinY - a.CellSize * 65) ||
                !FindClearing(w, "food", a.VillageMaxX + a.CellSize * 20, cy) ||
                !FindClearing(w, "wood", a.VillageMinX - a.CellSize * 25, cy + a.CellSize * 40) ||
                !FindClearing(w, "herb", cx - a.CellSize * 20, a.VillageMaxY + a.CellSize * 35))
                return Fail("荒村生存选址未找到安全空地。");
            var workFound=false;
            if(w.SurfaceGround.TryGet(a.SurfaceId,out var villageGround))
                for(var r=0;r<35 && !workFound;r++)
                    for(var dx=-r;dx<=r && !workFound;dx++)
                        for(var dy=-r;dy<=r;dy++)
                        {
                            if(r>0 && Math.Abs(dx)!=r && Math.Abs(dy)!=r) continue;
                            var point=new WorldVec2(cx+dx*a.CellSize,cy+dy*a.CellSize);
                            if(!a.ContainsVillage(a.SurfaceId,point.X,point.Y) || !villageGround.IsWalkable(point.X,point.Y)) continue;
                            s.Places["work"]=point; workFound=true; break;
                        }
            if(!workFound) return Fail("荒村短工集合点没有可站立位置。");
            foreach (var e in w.Entities.All)
            {
                if (!Applies(w, e)) continue;
                var p = PersonalLifeService.Ensure(w, e); p.Satiety = 40; p.Energy = 65;
                if (e.DefinitionId.ToString() == "base:character_companion_a")
                {
                    // Initial roster seeding precedes presentation materialization; ordinary Join
                    // correctly requires a materialized co-presence and must not be weakened.
                    var joined = SquadMembershipService.Transfer(w, e.Id, w.Strategic.PlayerPartyContext.ControlledSquadId);
                    if (joined.IsFailure) return Result.Failure(joined.Error);
                }
                if (e.DefinitionId.ToString() != "base:character_companion_b") continue;
                s.PrisonerId = e.Id.Value; loop.StopSubject(e.Id);
                w.Strategic.PlayerPartyContext.TryRemoveMember(e.Id, out _);
                CombatLifeStateService.TryEnterCaptured(w, e, "base:sect_huangcun_labor");
                if (e.TryGet<MovementIntentComponent>(out var move)) move.Clear();
                // New-game placement only. Release never teleports or restores health.
                var captivePoint = new WorldVec2(prison.WorldX + prison.WorldWidth / 2, prison.WorldY + a.CellSize * 2.5f);
                foreach (var bed in w.OutdoorConstructedAssets.BedsInRoom(prison.StableAssetId))
                    if (bed.OccupantCharacterId.IsNone && RoomBedGeometry.TryResolveAccessPoint(w.OutdoorConstructedAssets, bed, out var access))
                    { captivePoint = access; break; }
                w.WorldPresence.SetAtWorldPosition(e.Id, captivePoint, a.SurfaceId);
                CombatDamageRules.EnsureVitals(e);
                if (e.TryGet<CombatVitalsComponent>(out var hp)) hp.CurrentHp = Math.Min(hp.CurrentHp, 75);
                p.Satiety = 30;
            }
            if (s.PrisonerId == 0) return Fail("荒村生存开局缺少被囚同伴。");
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(w,w.Strategic.PlayerPartyContext);
            PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(w,w.Strategic.PlayerPartyContext);
            s.Message = "一名同伴同行，另一名被关在监狱。配给只能维生；探视、送粮送药或筹备营救由你决定。";
            return Result.Success();
        }

        static bool FindClearing(SimulationWorld w, string key, float x, float y)
        {
            var a = w.OpeningArea; var c = a.CellSize;
            if (!w.SurfaceSpatial.TryGet(a.SurfaceId, out var metric)) return false;
            x = metric.OriginWorldX + (float)Math.Floor((x - metric.OriginWorldX) / c) * c;
            y = metric.OriginWorldY + (float)Math.Floor((y - metric.OriginWorldY) / c) * c;
            for (var r = 0; r <= 50; r += 2)
            for (var dy = -r; dy <= r; dy += 2)
            for (var dx = -r; dx <= r; dx += 2)
            {
                if (r > 0 && Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                var point = new WorldVec2(x + dx * c, y + dy * c);
                if (!ClearFootprint(w, point)) continue;
                w.HuangcunSurvival.Places[key] = point; return true;
            }
            return false;
        }
        static bool ClearFootprint(SimulationWorld w, WorldVec2 p)
        {
            var a = w.OpeningArea; var c = a.CellSize;
            if (!w.SurfaceGround.TryGet(a.SurfaceId, out var ground)) return false;
            for (var y = -2; y <= 14; y++) for (var x = -2; x <= 10; x++)
            {
                var px = p.X + (x + .5f) * c; var py = p.Y + (y + .5f) * c;
                if (!a.Contains(a.SurfaceId, px, py) || a.ContainsVillage(a.SurfaceId, px, py) || !ground.IsWalkable(px, py)) return false;
            }
            foreach (var asset in w.OutdoorConstructedAssets.QueryOverlapping(a.SurfaceId, p.X - 2*c, p.Y - 2*c, 13*c, 17*c)) return false;
            return true;
        }
        public static bool TryDestination(SimulationWorld w, string task, out WorldVec2 point)
        {
            var key = task;
            if (task == "feed" || task == "medicine" || task == "ransom" || task == "pick_lock" || task == "force_release") key = "prison";
            if (task == "camp_rest" || task.StartsWith("store_") || task.StartsWith("take_")) key = "camp";
            return w.HuangcunSurvival.Places.TryGetValue(key, out point);
        }
        static bool Near(SimulationWorld w, EntityId id, WorldVec2 point, float cells = 8) =>
            CharacterWorldPresenceQuery.TryResolve(w, id, out var at) && at.HasWorldPosition &&
            at.SurfaceId == w.OpeningArea.SurfaceId && WorldVec2.Distance(at.WorldPosition, point) <= w.OpeningArea.CellSize * cells;
        public static bool AtCamp(SimulationWorld w, EntityId id)
        {
            if (string.IsNullOrEmpty(w.HuangcunSurvival.CampRoomId) ||
                !w.OutdoorConstructedAssets.Assets.TryGetValue(w.HuangcunSurvival.CampRoomId,out var room) ||
                !CharacterWorldPresenceQuery.TryResolve(w,id,out var at) || !at.HasWorldPosition || at.SurfaceId!=room.SurfaceId) return false;
            var c=room.WorldWidth/room.CellsW;
            return at.WorldPosition.X>=room.WorldX+c && at.WorldPosition.X<=room.WorldX+room.WorldWidth-c &&
                at.WorldPosition.Y>=room.WorldY+c && at.WorldPosition.Y<=room.WorldY+room.WorldHeight-c;
        }
        public static bool IsKnownTask(string task) => task == "work" || task == "food" || task == "wood" || task == "herb" ||
            task == "eat" || task == "camp_near" || task == "camp_far" || task == "camp_rest" ||
            task == "store_food" || task == "store_wood" || task == "store_herb" ||
            task == "take_food" || task == "take_wood" || task == "take_herb" ||
            task == "feed" || task == "medicine" || task == "ransom" || task == "pick_lock" || task == "force_release";
        public static ulong Duration(string task) => task == "work" ? 12UL : task.StartsWith("camp_") ? 18UL :
            task == "pick_lock" ? 12UL : task == "food" || task == "wood" || task == "herb" ? 8UL : 2UL;
        static string Item(string task) => task.EndsWith("wood") ? Wood : task.EndsWith("herb") ? Herb : Grain;
        static int Count(SimulationWorld w, string key) => w.HuangcunSurvival.StoredItems.TryGetValue(key, out var n) ? n : 0;
        static bool Guard(Entity e) => e.DefinitionId.ToString() == "base:character_ch01_ref_guard_a" ||
            e.DefinitionId.ToString() == "base:character_ch01_ref_guard_b";
        static bool GuardNear(SimulationWorld w)
        { foreach (var e in w.Entities.All) if (Guard(e) && CombatLifeStateService.CanFight(e) && Near(w, e.Id, w.HuangcunSurvival.Places["prison"], 25)) return true; return false; }
        static bool GuardsDefeated(SimulationWorld w)
        { foreach (var e in w.Entities.All) if (Guard(e) && CombatLifeStateService.CanFight(e)) return false; return true; }
        static string DailyKey(SimulationWorld w, string task) => "survival:yield:" + task + ":" + (w.Tick.Value / 288 % 2);
        static int UsedToday(SimulationWorld w, string task)
        { var k = DailyKey(w, task); return w.ContentDaily.IsMarkedToday(k, w.Tick) ? w.ContentCounters.Get(k) : 0; }
        static void UseToday(SimulationWorld w, string task, int n)
        { var k = DailyKey(w, task); var used = UsedToday(w, task); w.ContentDaily.MarkToday(k, w.Tick); w.ContentCounters.Set(k, used+n); }

        public static Result Validate(SimulationWorld w, EntityId id, string task)
        {
            if (!w.HuangcunSurvival.Enabled || w.Strategic.PlayerFactionId != w.HuangcunSurvival.OwnerFactionId || !w.Entities.TryGet(id, out var e) || !CombatLifeStateService.CanFight(e) ||
                w.Strategic.PlayerPartyContext?.IsMember(id) != true || w.LocalMap.IsInInterior ||
                CharacterEncounterService.OwnsParticipantSpatialState(w, id)) return Fail("当前角色不能进行荒村生活行动。");
            if (w.PlayerPartyTravel.IsMoving) return Fail("先停下，再开始行动。");
            if (!e.TryGet<PersonalLifeComponent>(out var personal)) return Fail("角色生活状态未就绪。");
            if (TryDestination(w, task, out var point) && !Near(w, id, point)) return Fail("需要先到达对应地点附近。");
            if ((task == "food" || task == "wood" || task == "herb" || task == "work") && personal.Energy <= 15) return Fail("精力不足，先休息。");
            switch (task)
            {
                case "eat": return w.Inventory.GetCount(Grain) < 1 ? Fail("需要一份私人粮食。") : personal.Satiety > 75 ? Fail("现在不需要再吃一份粮食。") : Result.Success();
                case "work": case "food": case "wood": case "herb":
                    return UsedToday(w, task) >= (task == "herb" ? 3 : 6) ? Fail("这处今天已无剩余机会，明天再来，或寻找其他生产来源。") : Result.Success();
                case "camp_near": case "camp_far":
                    if (!string.IsNullOrEmpty(w.HuangcunSurvival.CampRoomId)) return Fail("已有私人营地，不必重复建造。");
                    if (!TryDestination(w, task, out point) || !ClearFootprint(w, point)) return Fail("营地位置被占用。");
                    return w.Inventory.GetCount(Wood) < 12 ? Fail("搭棚与铺床需要私人木料 12。") : Result.Success();
                case "camp_rest": return AtCamp(w,id) ? Result.Success() : Fail("需要在自己的营地附近。");
                case "store_food": case "store_wood": case "store_herb":
                    if (!AtCamp(w,id)) return Fail("需要在私人营地附近。");
                    return w.Inventory.GetCount(Item(task)) < 1 || Count(w,Item(task)) >= 30 ? Fail("背包无此物，或此类营地储物已满（30）。") : Result.Success();
                case "take_food": case "take_wood": case "take_herb":
                    return !AtCamp(w,id) || Count(w,Item(task)) < 1 ? Fail("需要在营地且有存货。") : Result.Success();
                case "feed": case "medicine": case "ransom": case "pick_lock": case "force_release":
                    var prisoner = Prisoner(w);
                    if (prisoner == null || w.HuangcunSurvival.Released || !prisoner.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved) return Fail("同伴已经获救或无法再营救。");
                    if (task == "feed" && w.Inventory.GetCount(Grain) < 1) return Fail("送饭需要私人粮食 1。");
                    if (task == "feed" && prisoner.TryGet<PersonalLifeComponent>(out var captiveNeeds) && captiveNeeds.Satiety > 65) return Fail("同伴暂时吃饱了，可以把粮食留到下次探视。");
                    if (task == "medicine" && w.Inventory.GetCount(Herb) < 1) return Fail("送药需要灵草 1。");
                    if (task == "medicine" && life.HerbDoses >= 5) return Fail("已敷足药量，先观察恢复情况。");
                    if (task == "ransom" && (w.Inventory.GetCount(Grain) < 6 || w.Inventory.GetCount(Herb) < 3)) return Fail("狱卒索要粮食 6、灵草 3，才肯放人。");
                    if (task == "pick_lock" && (GuardNear(w) || w.Inventory.GetCount(Wood) < 2)) return Fail("撬锁需要木料 2；附近有清醒巡卫时不能动手。");
                    if (task == "force_release" && HuangcunOpeningAccess.IsLocked(w) && !GuardsDefeated(w)) return Fail("先击败两名巡卫，或取得荒村后解救。");
                    return Result.Success();
                default: return Fail("未知生活行动。");
            }
        }

        public static Result Complete(SimulationWorld w, EntityId id, string task)
        {
            var valid = Validate(w,id,task); if (valid.IsFailure) return valid;
            w.Entities.TryGet(id, out var e); var p = PersonalLifeService.Ensure(w,e);
            var s = w.HuangcunSurvival;
            if (task == "food" || task == "wood" || task == "herb" || task == "work")
            {
                var item = Item(task); var amount = task == "wood" ? 4 : 1;
                if (!w.Inventory.TryAddAll(item, amount)) return Fail("背包放不下，未领取产出。");
                p.Energy -= 6; p.Satiety -= 2; p.Clamp(); UseToday(w,task,1);
                if (task == "work" && e.TryGet<DailyTaskComponent>(out var quota)) quota.CompletedAmount += 12;
                s.Message = "已完成" + (task == "work" ? "额外短工" : "采集") + "：私人" + w.InventoryCatalog.GetName(item) + " +" + amount;
            }
            else if (task == "eat") { w.Inventory.TryRemoveAll(Grain,1); p.Satiety += 30; p.Clamp(); s.Message = "吃下私人口粮，饱食 +30；可把余粮留给同伴或修炼。"; }
            else if (task == "camp_near" || task == "camp_far") return BuildCamp(w,id,task);
            else if (task == "camp_rest") { p.Energy += 35; p.CurrentMood += 8; p.Clamp(); s.Message = "在营地歇息了一阵；没有瞬间恢复伤势。"; }
            else if (task.StartsWith("store_")) { var item=Item(task); w.Inventory.TryRemoveAll(item,1); s.StoredItems[item]=Count(w,item)+1; s.Message="已存入私人营地 1 份。"; }
            else if (task.StartsWith("take_"))
            { var item=Item(task); if (!w.Inventory.TryAddAll(item,1)) return Fail("背包已满，存货未变。"); s.StoredItems[item]=Count(w,item)-1; s.Message="已取出营地存货 1 份。"; }
            else
            {
                var captive=Prisoner(w); var needs=PersonalLifeService.Ensure(w,captive); captive.TryGet<LifecycleComponent>(out var life);
                if (task == "feed") { w.Inventory.TryRemoveAll(Grain,1); needs.Satiety+=35; needs.Clamp(); s.Message="送饭成功，同伴饱食 +35；仍被关押。"; }
                else if (task == "medicine")
                { w.Inventory.TryRemoveAll(Herb,1); life.HerbDoses=Math.Min(5,life.HerbDoses+1); s.Message="已敷一份灵草。医疗使用原有药力规则，重伤时需要多份药，仍需观察。"; }
                else
                {
                    if(task=="ransom") { w.Inventory.TryRemoveAll(Grain,6); w.Inventory.TryRemoveAll(Herb,3); }
                    if(task=="pick_lock") w.Inventory.TryRemoveAll(Wood,2);
                    var unconscious=life.IsIncapacitated;
                    life.State=unconscious ? LifecycleState.Incapacitated : LifecycleState.Alive;
                    life.RecordTransition(LifecycleState.Captured,life.State,"HuangcunRescue"); s.Released=true;
                    new SocialEventService().RecordCharacterRescued(w,id,captive.Id);
                    s.Message=unconscious ? "牢门已开，但同伴仍昏迷，需要治疗和搬运，不能直接入队。" : "同伴已获救，伤势和饥饿保留；靠近他后通过队伍界面邀请入队，再安排休养。";
                }
            }
            w.Events.Publish(XianXia.Core.Events.EventType.PartyInventoryChanged,w.Tick,actor:id,payload:"huangcun-survival:"+task);
            return Result.Success();
        }

        static Result BuildCamp(SimulationWorld w, EntityId id, string task)
        {
            var a=w.OpeningArea; var c=a.CellSize * World.Surface.SurfaceCellGeometry.FurnitureScale; var p=w.HuangcunSurvival.Places[task];
            var room=new OutdoorConstructedAssetState { StableAssetId=CampId, BuildingId="base:room_survival_camp", Kind="room", SurfaceId=a.SurfaceId,
                WorldX=p.X,WorldY=p.Y,WorldWidth=4*c,WorldHeight=6*c,CellsW=4,CellsH=6,BoundLocationId="runtime:huangcun_private_camp",StructureDurability=100 };
            var bed=new OutdoorConstructedAssetState { StableAssetId=CampId+":bed", BuildingId="base:furniture_bed",Kind="bed",SurfaceId=a.SurfaceId,
                WorldX=p.X+2*c,WorldY=p.Y+3*c,WorldWidth=c,WorldHeight=2*c,CellsW=1,CellsH=2,ParentRoomId=CampId,
                BoundLocationId="runtime:huangcun_private_camp",StructureDurability=100 };
            var physical=OutdoorFactionConstructionAuthorizationService.ValidatePhysicalPlacement(w,room);
            if(physical.IsFailure) return physical;
            if(!w.OutdoorConstructedAssets.TryRegister(room)) return Fail("营地位置冲突，未扣材料。");
            if(RoomBedGeometry.ValidatePlacement(w.OutdoorConstructedAssets,bed).IsFailure || !w.OutdoorConstructedAssets.TryRegister(bed))
            { w.OutdoorConstructedAssets.Remove(room.StableAssetId); return Fail("营地铺床失败，未扣材料。"); }
            w.Inventory.TryRemoveAll(Wood,12); w.HuangcunSurvival.CampRoomId=CampId;
            w.HuangcunSurvival.Places["camp"]=new WorldVec2(p.X+2.5f*c,p.Y+2.5f*c);
            w.HuangcunSurvival.Message="私人棚屋与床铺搭好了。可存放余粮木料灵草、休息和修炼；不取得村庄或公共仓库权限。";
            w.Events.Publish(XianXia.Core.Events.EventType.PartyInventoryChanged,w.Tick,actor:id,payload:"huangcun-camp");
            return Result.Success();
        }

        public static Result ValidateCultivation(SimulationWorld w, Entity e)
        {
            var facility = HuangcunOpeningAccess.ValidateCurrentFacilityUse(w, e.Id);
            if (facility.IsFailure) return facility;
            if(!Applies(w,e) || !HuangcunOpeningAccess.IsLocked(w)) return Result.Success();
            if(!CharacterWorldPresenceQuery.TryResolve(w,e.Id,out var at) || !at.HasWorldPosition ||
                w.OpeningArea.ContainsVillage(at.SurfaceId,at.WorldPosition.X,at.WorldPosition.Y)) return Fail("城内修炼设施不向劳役开放。可到村外修炼，搭好私人营地更有效。");
            if(!e.TryGet<PersonalLifeComponent>(out var p) || p.Satiety<=40 || p.Energy<=15) return Fail("修炼需要多余口粮和精力：饱食须高于 40，精力须高于 15。");
            return Result.Success();
        }
        public static void TryRation(SimulationWorld w, Entity e, PersonalLifeComponent p)
        {
            if(!Applies(w,e) || !HuangcunOpeningAccess.IsLocked(w) || IsPrisoner(w,e.Id) || !CombatLifeStateService.CanFight(e) || p.Satiety>35 ||
                !CharacterWorldPresenceQuery.TryResolve(w,e.Id,out var at) || !at.HasWorldPosition ||
                !w.OpeningArea.ContainsVillage(at.SurfaceId,at.WorldPosition.X,at.WorldPosition.Y)) return;
            var key="ration:"+e.Id; var used=UsedToday(w,key); if(used>=24) return;
            if(used==0 && WorldSitePublicStockService.TryRemove(w,Ch01ScenarioProgressionHooks.HuangcunSiteId,Grain,1,e.Id).IsFailure) return;
            var amount=Math.Min(6,24-used); p.Satiety+=amount; UseToday(w,key,amount); p.Status="领取稀粥配给（仅够维生）";
        }
        public static void Tick(SimulationWorld w, SimulationLoop loop)
        {
            var s=w.HuangcunSurvival; if(!s.Enabled || s.LastProcessedTick==w.Tick.Value) return;
            s.LastProcessedTick=w.Tick.Value;
            if (HuangcunOpeningAccess.IsLocked(w) && w.Tick.Value % 12 == 0)
                foreach (var actor in w.Entities.All)
                    if (Applies(w,actor) && !IsPrisoner(w,actor.Id) && CombatLifeStateService.CanFight(actor) &&
                        actor.TryGet<PersonalLifeComponent>(out var hunger) && hunger.Satiety == 0)
                    {
                        CombatDamageRules.EnsureVitals(actor);
                        if (actor.TryGet<CombatVitalsComponent>(out var vitals))
                        { vitals.CurrentHp=Math.Max(0,vitals.CurrentHp-1); if(vitals.CurrentHp==0) CombatLifeStateService.TryEnterIncapacitated(w,actor); }
                        s.Message="有人已经饥饿到损失生命。回村领取剩余配给、吃私粮，或安排采集。";
                    }
            var e=Prisoner(w); if(e==null || s.Released || !e.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved) return;
            var p=PersonalLifeService.Ensure(w,e); p.LastProcessedTick=w.Tick.Value;
            if(w.Tick.Value%12==0) p.Satiety=Math.Max(0,p.Satiety-1);
            var elapsed=w.Tick.Value-s.StartedTick;
            if(elapsed>0 && elapsed%288==0)
            { p.Satiety=Math.Min(100,p.Satiety+6); Harm(w,e,12); s.Message="监狱传来消息：同伴又受了殴打，分到的稀粥远远不足。可以先送粮送药延缓恶化。"; }
            if(p.Satiety==0 && elapsed%12==0) Harm(w,e,2);
            if(life.IsDying) s.Message="被囚同伴已濒危！可送药稳定伤势，救出不会自动痊愈。";
        }
        static void Harm(SimulationWorld w,Entity e,int amount)
        {
            if(!e.TryGet<CombatVitalsComponent>(out var hp) || !e.TryGet<LifecycleComponent>(out var life) || life.IsIncapacitated) return;
            hp.CurrentHp=Math.Max(0,hp.CurrentHp-amount);
            if(hp.CurrentHp>0) return;
            life.State=LifecycleState.Alive; CombatLifeStateService.TryEnterIncapacitated(w,e);
            CombatLifeStateService.TryEnterCaptured(w,e,"base:sect_huangcun_labor");
        }
    }
}
