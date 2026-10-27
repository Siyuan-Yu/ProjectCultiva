using System;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public static class MiningService
    {
        public const int WorkTicks = 10;
        public const int RegrowDays = 10;
        static string Key(string location) => "base:mining:"+location;
        public static bool IsOre(string item) => item == "base:resource_iron_ore" || item == "base:resource_copper_ore" ||
            item == "base:resource_silver_ore" || item == "base:resource_spirit_ore";
        public static bool IsMine(SimulationWorld w,string location) => WorldLocationQuery.TryGet(w,location,out var at) && IsOre(at.ResourceOnExploreId);
        public static int Capacity(string item) => item == "base:resource_spirit_ore" ? 8 : item == "base:resource_silver_ore" ? 12 : 24;
        public static int Remaining(SimulationWorld w,string location)
        {
            if(!WorldLocationQuery.TryGet(w,location,out var at) || !IsOre(at.ResourceOnExploreId)) return 0;
            var key=Key(location); var day=(int)Math.Min(int.MaxValue-20,(double)(w.Tick.Value/288));
            if(w.ContentCounters.Get(key+":init")==0 || w.ContentCounters.Get(key+":renew")>0 && day>=w.ContentCounters.Get(key+":renew"))
            { w.ContentCounters.Set(key+":left",Capacity(at.ResourceOnExploreId)); w.ContentCounters.Set(key+":init",1); w.ContentCounters.Set(key+":renew",0); }
            return w.ContentCounters.Get(key+":left");
        }
        public static string Description(SimulationWorld w,string location) => Remaining(w,location)>0
            ? "矿点剩余 "+Remaining(w,location)+" 份 · 每10秒开采1份"
            : "矿脉枯竭 · 第"+(w.ContentCounters.Get(Key(location)+":renew")+1)+"天恢复";
        public static bool TryPosition(SimulationWorld w,string location,out string surface,out WorldVec2 point,out string siteId)
        {
            surface=siteId=string.Empty; point=default;
            foreach(var pair in w.Strategic.Sites.Sites)
            {
                var site=pair.Value;
                if(!w.ContinuousOutdoorMaterialization.TryGetPlace(pair.Key,location,out var place) ||
                    !w.SurfaceSpatial.TryGet(site.CoreSurfaceId,out var metric)) continue;
                var mapper=new XianXia.Core.World.Surface.OutdoorSurfaceCoordinateMapper(metric.ChunkWidth,metric.ChunkHeight,metric.CellSize,
                    presentationUnitsPerWorldUnit:1f/metric.CellSize,originWorldX:metric.OriginWorldX,originWorldY:metric.OriginWorldY);
                mapper.PresentationToWorld(place.PresentationX,place.PresentationZ,out var x,out var y);
                point=new WorldVec2(x,y); surface=site.CoreSurfaceId; siteId=pair.Key; return true;
            }
            return false;
        }
        public static bool IsAt(SimulationWorld w,EntityId id,string location) => TryPosition(w,location,out var surface,out var point,out _) &&
            CharacterWorldPresenceQuery.TryResolve(w,id,out var at) && at.HasWorldPosition && at.SurfaceId==surface &&
            w.SurfaceSpatial.TryGet(surface,out var metric) && WorldVec2.Distance(at.WorldPosition,point)<=metric.CellSize*3 &&
            WorldObservation.HasLineOfSight(w,surface,at.WorldPosition,point);
        public static Result Validate(SimulationWorld w,EntityId id,string location)
        {
            if(!IsMine(w,location) || Remaining(w,location)<=0) return Result.Failure(ErrorCode.ActionCannotStart,"矿点已挖空，十天后恢复。请换一处矿点。");
            if(!IsAt(w,id,location)) return Result.Failure(ErrorCode.ActionCannotStart,"需要实际到达矿点旁才能采矿。");
            return Result.Success();
        }
        public static Result Harvest(SimulationWorld w,EntityId id,string location,bool publicStock)
        {
            var valid=Validate(w,id,location); if(valid.IsFailure) return valid;
            if(AutonomousActionContinuationService.BlocksOrdinaryWork(w,id)) return Result.Failure(ErrorCode.ActionCannotStart,"战斗中不能采矿。");
            WorldLocationQuery.TryGet(w,location,out var at);
            if(publicStock)
            {
                if(!TryPosition(w,location,out _,out _,out var siteId) || !w.Strategic.Sites.TryGet(siteId,out var site) ||
                    !w.Entities.TryGet(id,out var e) || !e.TryGet<FactionMembershipComponent>(out var f) || f.FactionId!=site.OwnerFactionId)
                    return Result.Failure(ErrorCode.ActionCannotStart,"已失去矿点所属城池的工作权限。");
                var added=WorldSitePublicStockService.TryAdd(w,siteId,at.ResourceOnExploreId,1,id);
                if(added.IsFailure || added.Value!=1) return Result.Failure(ErrorCode.ActionCannotStart,"公库无法接收矿石。");
            }
            else if(!w.Inventory.TryAddAll(at.ResourceOnExploreId,1)) return Result.Failure(ErrorCode.ActionCannotStart,"背包已满，矿石尚未开采。");
            var left=Remaining(w,location)-1; w.ContentCounters.Set(Key(location)+":left",left);
            if(left==0) w.ContentCounters.Set(Key(location)+":renew",(int)Math.Min(int.MaxValue-20,(double)(w.Tick.Value/288))+RegrowDays);
            if(!publicStock && w.Entities.TryGet(id,out var actor))
            { w.LocationLabor.AddHarvest(actor.DefinitionId.ToString(),location,1); XianXia.Core.Content.QuestProgressRefresh.AfterWorldChange(w,id); }
            w.Events.Publish(XianXia.Core.Events.EventType.PartyInventoryChanged,w.Tick,actor:id,payload:"mining:"+location);
            return Result.Success();
        }
        public static void Work(SimulationWorld w,Entity entity,MortalCivilianState state)
        {
            if(!MortalCivilianQuery.TryFindProfessionArea(w,entity,"mine",out var area))
            { state.ScheduleStatus="采矿受阻：没有可用矿点"; return; }
            if(!entity.TryGet<MovementIntentComponent>(out var intent)) { intent=new MovementIntentComponent(); entity.AddComponent(intent); }
            if(!IsAt(w,entity.Id,area.LocationId))
            {
                if(!intent.Active || intent.TargetWorkAreaId!=area.Id || intent.HostArrived) intent.Begin(area.LocationId,area.Id,0);
                state.ScheduleStatus="前往矿点"; return;
            }
            var key="base:miner:"+entity.Id+":"+area.LocationId;
            var tick=(int)(w.Tick.Value%int.MaxValue);
            if(w.ContentCounters.Get(key+":last")==tick) return;
            var progress=w.ContentCounters.Get(key+":last")==tick-1 ? w.ContentCounters.Get(key+":progress")+1 : 1;
            w.ContentCounters.Set(key+":last",tick); w.ContentCounters.Set(key+":progress",progress);
            state.ScheduleStatus="采矿 "+progress+"/10 · "+Description(w,area.LocationId);
            if(progress<WorkTicks) return;
            var result=Harvest(w,entity.Id,area.LocationId,true); w.ContentCounters.Set(key+":progress",0);
            if(result.IsFailure) state.ScheduleStatus=result.Error.Message;
        }
    }
}
