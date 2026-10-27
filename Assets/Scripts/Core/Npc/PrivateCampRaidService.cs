using System;
using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public static class PrivateCampRaidService
    {
        static string Key(EntityId guard,string asset)=>"base:camp-raid:"+guard+"|"+asset;
        public static bool Owns(SimulationWorld w,EntityId guard)
        {
            if(!HuangcunOpeningAccess.IsLocked(w)||!w.Entities.TryGet(guard,out var e)||!CombatLifeStateService.CanFight(e))return false;
            foreach(var a in w.OutdoorConstructedAssets.Assets.Values)if(w.ContentCounters.Get(Key(guard,a.StableAssetId))>0)return true;
            return false;
        }
        static WorldVec2 Facing(OutdoorConstructedAssetState a,WorldVec2 from,float cell)
        {
            var x=Math.Max(a.WorldX,Math.Min(a.WorldX+a.WorldWidth,from.X));var y=Math.Max(a.WorldY,Math.Min(a.WorldY+a.WorldHeight,from.Y));
            if(from.X<a.WorldX)x-=cell*.6f;else if(from.X>a.WorldX+a.WorldWidth)x+=cell*.6f;
            if(from.Y<a.WorldY)y-=cell*.6f;else if(from.Y>a.WorldY+a.WorldHeight)y+=cell*.6f;
            return new WorldVec2(x,y);
        }
        public static bool TryTarget(SimulationWorld w,EntityId guard,out OutdoorConstructedAssetState asset,out WorldVec2 approach)
        {
            asset=null;approach=default;
            if(!Owns(w,guard)||!CharacterWorldPresenceQuery.TryResolve(w,guard,out var at)||!at.HasWorldPosition||!w.SurfaceSpatial.TryGet(at.SurfaceId,out var metric))return false;
            var best=float.MaxValue;var encounter=w.Strategic.CharacterEncounter;
            foreach(var a in w.OutdoorConstructedAssets.Assets.Values)
            {
                if(a.SurfaceId!=at.SurfaceId||w.ContentCounters.Get(Key(guard,a.StableAssetId))==0||w.OutdoorStatefulObjects.IsDestructibleDestroyed(a.StableAssetId))continue;
                var point=Facing(a,at.WorldPosition,metric.CellSize);
                if(encounter!=null && (!encounter.Contains(point.X,point.Y)||!encounter.Contains(a.WorldX+a.WorldWidth*.5f,a.WorldY+a.WorldHeight*.5f)))continue;
                if(!w.SurfaceGround.TryGet(a.SurfaceId,out var ground)||!NpcLocalNavigation.TryLandingPoint(w,ground,point,
                    p=>WorldVec2.Distance(p,point)<=metric.CellSize*2,out var landing))continue;
                var d=WorldVec2.Distance(at.WorldPosition,landing);if(d>=best)continue;
                asset=a;approach=landing;best=d;
            }
            return asset!=null;
        }
        public static bool KeepsEncounterActive(SimulationWorld w)
        {
            var state=w.Strategic.CharacterEncounter;if(state==null||!w.HuangcunSurvival.PunishmentBattleActive)return false;
            foreach(var p in state.Participants)if(p.Enemy&&!p.Withdrawn&&CharacterEncounterService.IsLiving(w,p.CharacterId)&&
                TryTarget(w,new EntityId(p.CharacterId),out _,out _))return true;
            return false;
        }
        public static bool Observe(SimulationWorld w,SimulationLoop loop,out Entity observer,out Entity subject)
        {
            observer=subject=null;if(!HuangcunOpeningAccess.IsLocked(w)||w.Strategic.CharacterEncounter!=null)return false;
            foreach(var guard in w.Entities.All)
            {
                if(!SupervisorAngerDriver.IsOnWatch(w,guard)||!CharacterWorldPresenceQuery.TryResolve(w,guard.Id,out var from)||!from.HasWorldPosition)continue;
                var radius=w.OpeningArea.CellSize*SupervisorAngerDriver.ObservationRangeCells;
                // First priority is a visible person actually cultivating/building outside the village.
                foreach(var person in w.Entities.All)
                {
                    if(w.Strategic.PlayerPartyContext?.IsMember(person.Id)!=true||!HuangcunSurvivalService.Applies(w,person)||!CombatLifeStateService.CanFight(person)||
                        !CharacterWorldPresenceQuery.TryResolve(w,person.Id,out var at)||!at.HasWorldPosition||at.SurfaceId!=from.SurfaceId||
                        w.OpeningArea.ContainsVillage(at.SurfaceId,at.WorldPosition.X,at.WorldPosition.Y)||WorldVec2.Distance(from.WorldPosition,at.WorldPosition)>radius||
                        !WorldObservation.HasLineOfSight(w,at.SurfaceId,from.WorldPosition,at.WorldPosition))continue;
                    if(!person.TryGet<ActionStateComponent>(out var action)||!action.HasActiveAction||!w.ActiveActions.TryGetValue(action.ActiveActionId,out var current)||
                        !(current is CultivateAction || current is HuangcunSurvivalAction build&&PrivateCampService.IsTask(build.Task)))continue;
                    MarkNearby(w,guard.Id,from.SurfaceId,at.WorldPosition);observer=guard;subject=person;return true;
                }
                foreach(var a in w.OutdoorConstructedAssets.Assets.Values)
                {
                    if(!PrivateCampService.Owns(w,a.StableAssetId,w.Strategic.PlayerFactionId)||a.SurfaceId!=from.SurfaceId||
                        w.OutdoorStatefulObjects.IsDestructibleDestroyed(a.StableAssetId))continue;
                    var point=Facing(a,from.WorldPosition,w.OpeningArea.CellSize);
                    if(WorldVec2.Distance(from.WorldPosition,point)>radius||!WorldObservation.HasLineOfSight(w,a.SurfaceId,from.WorldPosition,point))continue;
                    MarkNearby(w,guard.Id,a.SurfaceId,new WorldVec2(a.WorldX+a.WorldWidth*.5f,a.WorldY+a.WorldHeight*.5f));
                    foreach(var person in w.Entities.All)
                    {
                        if(w.Strategic.PlayerPartyContext?.IsMember(person.Id)!=true||!HuangcunSurvivalService.Applies(w,person)||!CombatLifeStateService.CanFight(person)||
                            !CharacterWorldPresenceQuery.TryResolve(w,person.Id,out var at)||!at.HasWorldPosition||at.SurfaceId!=from.SurfaceId||
                            WorldVec2.Distance(from.WorldPosition,at.WorldPosition)>radius||!WorldObservation.HasLineOfSight(w,at.SurfaceId,from.WorldPosition,at.WorldPosition))continue;
                        observer=guard;subject=person;return true;
                    }
                    break;
                }
            }
            return false;
        }
        static void MarkNearby(SimulationWorld w,EntityId guard,string surface,WorldVec2 point)
        {
            foreach(var a in w.OutdoorConstructedAssets.Assets.Values)
                if(a.SurfaceId==surface&&PrivateCampService.Owns(w,a.StableAssetId,w.Strategic.PlayerFactionId)&&
                    WorldVec2.Distance(point,new WorldVec2(a.WorldX+a.WorldWidth*.5f,a.WorldY+a.WorldHeight*.5f))<=w.OpeningArea.CellSize*PrivateCampService.RadiusCells*2)
                    w.ContentCounters.Set(Key(guard,a.StableAssetId),1);
        }
        public static void AdvanceOutdoor(SimulationWorld w,SimulationLoop loop)
        {
            if(w.Strategic.CharacterEncounter!=null||!HuangcunOpeningAccess.IsLocked(w))return;
            foreach(var guard in w.Entities.All)
            {
                if(w.HuangcunSurvival.PunisherId==guard.Id.Value||!TryTarget(w,guard.Id,out var a,out var goal))continue;
                loop?.StopSubject(guard.Id);
                if(!guard.TryGet<MovementIntentComponent>(out var intent)){intent=new MovementIntentComponent();guard.AddComponent(intent);}
                if(!intent.Active||intent.DestinationKey!="camp-raid:"+a.StableAssetId)intent.BeginWorldPoint(a.SurfaceId,goal.X,goal.Y,destinationKey:"camp-raid:"+a.StableAssetId,arrivalRadiusWorld:w.OpeningArea.CellSize);
                if(!w.ContinuousOutdoorMaterialization.IsMaterialized(guard.Id))NpcColdMovement.Advance(w,guard,a.SurfaceId,goal,w.OpeningArea.CellSize,out _);
                if(w.Tick.Value%2==0)Strike(w,guard.Id,a.StableAssetId);
            }
        }
        public static bool Strike(SimulationWorld w,EntityId guard,string target)
        {
            if(!TryTarget(w,guard,out var a,out var goal)||a.StableAssetId!=target||!w.Entities.TryGet(guard,out var attacker)||
                !CharacterWorldPresenceQuery.TryResolve(w,guard,out var from)||!from.HasWorldPosition||
                WorldVec2.Distance(from.WorldPosition,goal)>w.OpeningArea.CellSize*2||
                !WorldObservation.HasLineOfSight(w,a.SurfaceId,from.WorldPosition,goal))return false;
            var hp=w.OutdoorStatefulObjects.TryGetDestructible(a.StableAssetId,out var saved)?saved.Hp:Math.Max(40,a.StructureDurability);
            hp=Math.Max(0,hp-StrategicBuildingDamage.Compute(attacker,0));
            w.OutdoorStatefulObjects.SetDestructible(a.StableAssetId,hp,hp==0);
            w.HuangcunSurvival.Message="巡卫正在破坏私人营地："+a.Kind+"，耐久 "+hp;
            if(hp==0)
            {
                var children=new List<OutdoorConstructedAssetState>();foreach(var b in w.OutdoorConstructedAssets.Assets.Values)if(b.ParentRoomId==a.StableAssetId)children.Add(b);
                foreach(var b in children)Destroy(w,b);Destroy(w,a);
                w.ContentCounters.Set("base:camp-visual-revision",w.ContentCounters.Get("base:camp-visual-revision")+1);
            }
            return true;
        }
        static void Destroy(SimulationWorld w,OutdoorConstructedAssetState a)
        {
            w.OutdoorStatefulObjects.SetDestructible(a.StableAssetId,0,true);
            if(!a.OccupantCharacterId.IsNone)RoomBedFacilityService.Release(w,a.OccupantCharacterId);
            foreach(var anchor in a.AdministrativeCellAnchors())w.OutdoorAdministrativeAssetAnchors.Remove(anchor.StableAssetId);
            w.OutdoorConstructedAssets.Remove(a.StableAssetId);
            if(w.HuangcunSurvival.CampRoomId==a.StableAssetId)
            {
                w.HuangcunSurvival.CampRoomId="";w.HuangcunSurvival.Places.Remove("camp");
                foreach(var room in w.OutdoorConstructedAssets.Assets.Values)
                    if(RoomBedGeometry.IsRoom(room)&&PrivateCampService.Owns(w,room.StableAssetId,w.Strategic.PlayerFactionId))
                    {w.HuangcunSurvival.CampRoomId=room.StableAssetId;w.HuangcunSurvival.Places["camp"]=BasicCraftService.Point(w,room);break;}
            }
        }
        public static void CancelDefeated(SimulationWorld w)
        {
            foreach(var guard in w.Entities.All)if(HuangcunOpeningAccess.IsOpeningStaff(w,guard.Id)&&!CombatLifeStateService.CanFight(guard))
                foreach(var a in w.OutdoorConstructedAssets.Assets.Values)w.ContentCounters.Set(Key(guard.Id,a.StableAssetId),0);
        }
    }
}
