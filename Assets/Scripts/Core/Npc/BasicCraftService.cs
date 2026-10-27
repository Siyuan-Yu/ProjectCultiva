using System;
using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public sealed class BasicRecipe
    {
        public string Id, Name, Output, RoomKind;
        public ulong Ticks;
        public readonly Dictionary<string,int> Costs = new Dictionary<string,int>();
    }
    public static class BasicCraftService
    {
        public const string Pick="base:item_iron_pick", Powder="base:item_healing_powder", Pill="base:item_qi_pill", Focus="base:item_focus_charm";
        public static readonly BasicRecipe[] Recipes={
            Recipe("iron","炼铁锭","base:resource_iron_ingot","smithy",18,"base:resource_iron_ore",2,HuangcunSurvivalService.Wood,1),
            Recipe("copper","炼铜锭","base:resource_copper_ingot","smithy",18,"base:resource_copper_ore",2,HuangcunSurvivalService.Wood,1),
            Recipe("silver","炼银锭","base:resource_silver_ingot","smithy",24,"base:resource_silver_ore",2,HuangcunSurvivalService.Wood,1),
            Recipe("pick","锻铁镐",Pick,"smithy",24,"base:resource_iron_ingot",2,HuangcunSurvivalService.Wood,2),
            Recipe("focus","制凝神器",Focus,"smithy",30,"base:resource_silver_ingot",1,"base:resource_copper_ingot",1,"base:resource_spirit_ore",1),
            Recipe("powder","制疗伤散",Powder,"alchemy",18,HuangcunSurvivalService.Herb,2,HuangcunSurvivalService.Wood,1),
            Recipe("pill","炼培元丸",Pill,"alchemy",30,HuangcunSurvivalService.Herb,3,"base:resource_spirit_ore",1)
        };
        static BasicRecipe Recipe(string id,string name,string output,string kind,ulong ticks,params object[] costs)
        { var r=new BasicRecipe{Id=id,Name=name,Output=output,RoomKind=kind,Ticks=ticks}; for(var i=0;i<costs.Length;i+=2)r.Costs[(string)costs[i]]=(int)costs[i+1]; return r; }
        public static BasicRecipe Find(string task)
        { var parts=(task??"").Split('|'); if(parts.Length!=3 || parts[0]!="craft")return null; foreach(var r in Recipes)if(r.Id==parts[1])return r; return null; }
        public static bool IsTask(string task) => Find(task)!=null;
        public static ulong Duration(string task) => Find(task)?.Ticks??1;
        public static string Task(BasicRecipe r,string room) => "craft|"+r.Id+"|"+room;
        public static bool FindRoom(SimulationWorld w,EntityId id,string kind,out OutdoorConstructedAssetState room)
        {
            room=null; var best=float.MaxValue;
            if(!CharacterWorldPresenceQuery.TryResolve(w,id,out var at)||!at.HasWorldPosition)return false;
            foreach(var a in w.OutdoorConstructedAssets.Assets.Values)
            {
                if(!RoomBedGeometry.IsRoom(a)||a.SurfaceId!=at.SurfaceId || a.BuildingId.IndexOf(kind,StringComparison.OrdinalIgnoreCase)<0 ||
                    w.OutdoorStatefulObjects.IsDestructibleDestroyed(a.StableAssetId))continue;
                var distance=WorldVec2.Distance(at.WorldPosition,Point(w,a)); if(distance>=best)continue; room=a; best=distance;
            }
            return room!=null;
        }
        public static WorldVec2 Point(SimulationWorld w,OutdoorConstructedAssetState room)
        { var cell=w.SurfaceSpatial.TryGet(room.SurfaceId,out var metric)?metric.CellSize:room.WorldWidth/room.CellsW;
          return new WorldVec2(room.WorldX+room.WorldWidth*.5f,room.WorldY+cell*3); }
        static Result Fail(string text)=>Result.Failure(ErrorCode.ActionCannotStart,text);
        public static Result Validate(SimulationWorld w,EntityId id,string task)
        {
            var r=Find(task); var parts=(task??"").Split('|');
            if(r==null || !w.Entities.TryGet(id,out var e) || !CombatLifeStateService.CanFight(e) ||
                w.Strategic.PlayerPartyContext?.IsMember(id)!=true || AutonomousActionContinuationService.BlocksOrdinaryWork(w,id) ||
                MortalTransportConstraintService.ValidateUnburdenedAction(w,id).IsFailure) return Fail("当前无法制作。");
            if(!w.OutdoorConstructedAssets.Assets.TryGetValue(parts[2],out var room) || !RoomBedGeometry.IsRoom(room) ||
                room.BuildingId.IndexOf(r.RoomKind,StringComparison.OrdinalIgnoreCase)<0 ||
                w.OutdoorStatefulObjects.IsDestructibleDestroyed(room.StableAssetId) ||
                !CharacterWorldPresenceQuery.TryResolve(w,id,out var at) || !at.HasWorldPosition || at.SurfaceId!=room.SurfaceId ||
                !w.SurfaceSpatial.TryGet(room.SurfaceId,out var metric) || WorldVec2.Distance(at.WorldPosition,Point(w,room))>metric.CellSize*2)
                return Fail("请先实际走到对应工坊内的操作位置。");
            if(e.TryGet<PersonalLifeComponent>(out var needs) && needs.Energy<=15)return Fail("精力不足，先休息再制作。");
            foreach(var cost in r.Costs)if(w.Inventory.GetCount(cost.Key)<cost.Value)return Fail("材料不足："+w.InventoryCatalog.GetName(cost.Key)+" ×"+cost.Value);
            // Check the actual post-consumption inventory without changing persistent state.
            var snapshot=w.Inventory.CaptureState();
            foreach(var cost in r.Costs)w.Inventory.TryRemoveAll(cost.Key,cost.Value);
            var fits=w.Inventory.CanAddAll(r.Output,1); w.Inventory.RestoreState(snapshot);
            return fits?Result.Success():Fail("背包无法容纳成品。");
        }
        public static Result Complete(SimulationWorld w,EntityId id,string task)
        {
            var valid=Validate(w,id,task); if(valid.IsFailure)return valid; var r=Find(task);
            var snapshot=w.Inventory.CaptureState();
            foreach(var cost in r.Costs)if(!w.Inventory.TryRemoveAll(cost.Key,cost.Value)){w.Inventory.RestoreState(snapshot);return Fail("材料已变化，未制作。");}
            if(!w.Inventory.TryAddAll(r.Output,1)){w.Inventory.RestoreState(snapshot);return Fail("背包已满，材料已保留。");}
            if(w.Entities.TryGet(id,out var e)&&e.TryGet<PersonalLifeComponent>(out var p)){p.Energy=Math.Max(0,p.Energy-6);}
            w.HuangcunSurvival.Message=r.Name+"完成，获得"+w.InventoryCatalog.GetName(r.Output)+" ×1";
            w.Events.Publish(XianXia.Core.Events.EventType.PartyInventoryChanged,w.Tick,actor:id,payload:"craft:"+r.Output);
            return Result.Success();
        }
        public static Result Use(SimulationWorld w,EntityId actor,EntityId target,string item)
        {
            if(!w.Entities.TryGet(actor,out var a)||!CombatLifeStateService.CanFight(a)||w.Strategic.PlayerPartyContext?.IsMember(actor)!=true ||
                AutonomousActionContinuationService.BlocksOrdinaryWork(w,actor) || !w.Entities.TryGet(target,out var e) ||
                !e.TryGet<LifecycleComponent>(out var life)||life.IsDead||life.IsRemoved || w.Inventory.GetCount(item)<1) return Fail("当前不能使用，或背包没有此物。");
            if(w.Strategic.PlayerPartyContext?.IsMember(target)!=true && !HuangcunSurvivalService.IsPrisoner(w,target))return Fail("只能照顾同行同伴或被囚同伴。");
            if(actor!=target && (!CharacterWorldPresenceQuery.TryResolve(w,actor,out var from)||!from.HasWorldPosition ||
                !CharacterWorldPresenceQuery.TryResolve(w,target,out var to)||!to.HasWorldPosition||from.SurfaceId!=to.SurfaceId ||
                !w.SurfaceSpatial.TryGet(from.SurfaceId,out var metric)||WorldVec2.Distance(from.WorldPosition,to.WorldPosition)>metric.CellSize*3 ||
                !WorldObservation.HasLineOfSight(w,from.SurfaceId,from.WorldPosition,to.WorldPosition)))return Fail("请先靠近同伴，不能隔墙递药。");
            if(HuangcunSurvivalService.IsPrisoner(w,target)){var access=HuangcunPrisonVisitService.ValidateVisit(w,actor);if(access.IsFailure)return access;}
            if(item==Powder)
            { if(life.HerbDoses>=5)return Fail("药量已足，等待恢复。"); life.HerbDoses=Math.Min(5,life.HerbDoses+2); }
            else if(item==Pill || item==Focus)
            {
                if(!CombatLifeStateService.CanFight(e) || CombatInjuryService.HasBrokenMeridians(e) || !e.TryGet<CultivationComponent>(out var c) || c.IsAtBottleneck)
                    return Fail("需要清醒且经脉完好；瓶颈请先突破。");
                var key="base:craft-buff:"+target+":"+item;
                if(w.ContentCounters.Get(key)>0)return Fail("上次药力尚未炼化，不可重复使用。");
                w.ContentCounters.Set(key,item==Pill?10:30);
            }
            else return Fail("此物无需主动使用；铁镐在背包中自动辅助采矿。");
            w.Inventory.TryRemoveAll(item,1);
            w.Events.Publish(XianXia.Core.Events.EventType.PartyInventoryChanged,w.Tick,actor:actor,payload:"use:"+item);
            return Result.Success();
        }
        public static int CultivationBonus(SimulationWorld w,EntityId id)
        {
            var result=0; foreach(var item in new[]{Pill,Focus})
            {var key="base:craft-buff:"+id+":"+item;var left=w.ContentCounters.Get(key);if(left>0){w.ContentCounters.Set(key,left-1);result+=item==Pill?5:1;}}
            return result;
        }
    }
}
