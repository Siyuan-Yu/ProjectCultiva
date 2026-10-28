using System;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public static class PrivateCampService
    {
        public const string MarkerKind="privateCampMarker", MarkerBuilding="base:building_private_camp_marker";
        public const int RadiusCells=24, MarkerWoodCost=4;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        sealed class AssetIndex
        {
            public AssetIndex() {}
            public ulong Revision=ulong.MaxValue;
            public readonly List<OutdoorConstructedAssetState> Assets=new List<OutdoorConstructedAssetState>();
        }
        static readonly ConditionalWeakTable<SimulationWorld,AssetIndex> AssetIndexes=new ConditionalWeakTable<SimulationWorld,AssetIndex>();
        public static IReadOnlyList<OutdoorConstructedAssetState> Assets(SimulationWorld w)
        {
            var index=AssetIndexes.GetOrCreateValue(w);
            if(index.Revision!=w.OutdoorConstructedAssets.Revision)
            {
                index.Assets.Clear();
                foreach(var a in w.OutdoorConstructedAssets.Assets.Values)
                    if(IsPrivate(w,a.StableAssetId))index.Assets.Add(a);
                index.Revision=w.OutdoorConstructedAssets.Revision;
            }
            return index.Assets;
        }
        public static bool Owns(SimulationWorld w,string asset,string faction) => !string.IsNullOrEmpty(faction) && w.ContentCounters.Get("base:camp-owner:"+asset+"|"+faction)>0;
        public static bool IsPrivate(SimulationWorld w,string asset) => w.ContentCounters.Get("base:camp-private:"+asset)>0;
        static void MarkOwner(SimulationWorld w,string asset,string faction)
        {
            if(Owns(w,asset,faction)&&IsPrivate(w,asset))return;
            w.ContentCounters.Set("base:camp-owner:"+asset+"|"+faction,1);w.ContentCounters.Set("base:camp-private:"+asset,1);
            AssetIndexes.GetOrCreateValue(w).Revision=ulong.MaxValue;
        }
        public static void RestoreLegacyOwnership(SimulationWorld w)
        {
            const string legacy = "asset:huangcun:private_camp";
            if (!w.OutdoorConstructedAssets.Assets.ContainsKey(legacy)) return;
            MarkOwner(w, legacy, w.HuangcunSurvival.OwnerFactionId);
            foreach (var a in w.OutdoorConstructedAssets.BedsInRoom(legacy))
                if (a.ParentRoomId == legacy) MarkOwner(w, a.StableAssetId, w.HuangcunSurvival.OwnerFactionId);
        }
        public static string StorageRoom(SimulationWorld w,EntityId id)
        {
            if(!CharacterWorldPresenceQuery.TryResolve(w,id,out var at)||!at.HasWorldPosition)return "";
            foreach(var a in Assets(w))
                if(RoomBedGeometry.IsRoom(a)&&a.SurfaceId==at.SurfaceId&&Owns(w,a.StableAssetId,w.Strategic.PlayerFactionId)&&
                    !w.OutdoorStatefulObjects.IsDestructibleDestroyed(a.StableAssetId)&&at.WorldPosition.X>a.WorldX&&at.WorldPosition.X<a.WorldX+a.WorldWidth&&
                    at.WorldPosition.Y>a.WorldY&&at.WorldPosition.Y<a.WorldY+a.WorldHeight)return a.StableAssetId;
            return "";
        }
        static string StorageKey(string room,string item)=>"base:camp-stock:"+room+"|"+item;
        public static int Stored(SimulationWorld w,EntityId id,string item) => w.ContentCounters.Get(StorageKey(StorageRoom(w,id),item));
        public static void ChangeStored(SimulationWorld w,EntityId id,string item,int delta)
        {var key=StorageKey(StorageRoom(w,id),item);w.ContentCounters.Set(key,w.ContentCounters.Get(key)+delta);}
        public static void MigrateStorage(SimulationWorld w)
        {
            var room=w.HuangcunSurvival.CampRoomId;
            if(string.IsNullOrEmpty(room)||!w.OutdoorConstructedAssets.Assets.ContainsKey(room))return;
            foreach(var p in w.HuangcunSurvival.StoredItems)
            {var key=StorageKey(room,p.Key);w.ContentCounters.Set(key,w.ContentCounters.Get(key)+p.Value);}
            w.HuangcunSurvival.StoredItems.Clear();
        }
        public static bool IsTask(string task) => task!=null&&(task.StartsWith("campmark|")||task.StartsWith("campbuild|"));
        public static ulong Duration(string task) => task!=null&&task.StartsWith("campmark|")?6UL:18UL;
        public static string MarkerTask(SimulationWorld w,EntityId id)
        {
            if(!CharacterWorldPresenceQuery.TryResolve(w,id,out var at)||!at.HasWorldPosition||!w.SurfaceSpatial.TryGet(at.SurfaceId,out var metric))return "";
            var x=metric.OriginWorldX+(float)Math.Floor((at.WorldPosition.X-metric.OriginWorldX)/metric.CellSize)*metric.CellSize;
            var y=metric.OriginWorldY+(float)Math.Floor((at.WorldPosition.Y-metric.OriginWorldY)/metric.CellSize)*metric.CellSize;
            return "campmark|"+at.SurfaceId+"|"+x.ToString("R",Inv)+"|"+y.ToString("R",Inv);
        }
        public static string BuildTask(OutdoorConstructedAssetState a) => "campbuild|"+a.BuildingId+"|"+a.SurfaceId+"|"+a.WorldX.ToString("R",Inv)+"|"+a.WorldY.ToString("R",Inv)+"|"+a.QuarterTurns+"|"+a.ParentRoomId;
        public static bool TryCandidate(SimulationWorld w,string task,out OutdoorConstructedAssetState a)
        {
            a=null; var p=(task??"").Split('|'); var marker=p.Length==4&&p[0]=="campmark";
            if(!marker&&(p.Length!=7||p[0]!="campbuild"))return false;
            var surface=marker?p[1]:p[2];
            if(!float.TryParse(p[marker?2:3],NumberStyles.Float,Inv,out var x)||!float.TryParse(p[marker?3:4],NumberStyles.Float,Inv,out var y)||
                !w.SurfaceSpatial.TryGet(surface,out var metric))return false;
            BuildingConstructionSpec spec=null;var q=0;
            if(!marker&&(!w.ConstructionCatalog.TryGet(p[1],out spec)||!spec.UnlockedByDefault||!int.TryParse(p[5],out q)||q<0||q>3))return false;
            if(!marker&&spec.PlacementKind!=ConstructionPlacementKind.Room&&spec.PlacementKind!=ConstructionPlacementKind.Bed&&
                spec.PlacementKind!=ConstructionPlacementKind.WallSegment&&spec.PlacementKind!=ConstructionPlacementKind.FarmField)return false;
            var cw=marker?1:spec.FootprintCellsW;var ch=marker?1:spec.FootprintCellsH;
            if(q%2==1){var tmp=cw;cw=ch;ch=tmp;}
            a=new OutdoorConstructedAssetState{StableAssetId="preview:private",BuildingId=marker?MarkerBuilding:spec.BuildingId,Kind=marker?MarkerKind:spec.OutdoorKind,
                SurfaceId=surface,WorldX=x,WorldY=y,WorldWidth=cw*metric.CellSize*(marker?1:spec.PhysicalScale),WorldHeight=ch*metric.CellSize*(marker?1:spec.PhysicalScale),
                CellsW=cw,CellsH=ch,QuarterTurns=q,ParentRoomId=marker?"":p[6],BoundLocationId="runtime:private-preview",StructureDurability=marker?60:Math.Max(40,spec.StructureDurability)};
            return a.IsValid;
        }
        public static bool TryCover(SimulationWorld w,string faction,OutdoorConstructedAssetState a,out OutdoorConstructedAssetState marker)
        {
            marker=null;if(w==null||a==null||!w.SurfaceSpatial.TryGet(a.SurfaceId,out var metric))return false;
            if(a.Kind!="room"&&a.Kind!="bed"&&a.Kind!="wallSegment"&&a.Kind!="grainField")return false;
            foreach(var m in Assets(w))
            {
                if(m.Kind!=MarkerKind||m.SurfaceId!=a.SurfaceId||!Owns(w,m.StableAssetId,faction)||w.OutdoorStatefulObjects.IsDestructibleDestroyed(m.StableAssetId))continue;
                var cx=m.WorldX+m.WorldWidth*.5f;var cy=m.WorldY+m.WorldHeight*.5f;var r=metric.CellSize*RadiusCells;
                var dx=Math.Max(Math.Abs(a.WorldX-cx),Math.Abs(a.WorldX+a.WorldWidth-cx));var dy=Math.Max(Math.Abs(a.WorldY-cy),Math.Abs(a.WorldY+a.WorldHeight-cy));
                if(dx*dx+dy*dy>r*r)continue;
                var foreign=false;
                foreach(var cell in a.EnumeratePhysicalCells(metric.OriginWorldX,metric.OriginWorldY,metric.CellSize))
                    if(WorldSiteAdministrativeControlResolver.TryResolve(w,cell.SurfaceId,cell.WorldX,cell.WorldY,out var site,out _)&&site!=null){foreign=true;break;}
                if(!foreign){marker=m;return true;}
            }
            return false;
        }
        static Result Fail(string text)=>Result.Failure(ErrorCode.ActionCannotStart,text);
        public static Result Validate(SimulationWorld w,EntityId id,string task)
        {
            if(!TryCandidate(w,task,out var a)||!w.Entities.TryGet(id,out var e)||!CombatLifeStateService.CanFight(e)||
                w.Strategic.PlayerPartyContext?.IsMember(id)!=true||AutonomousActionContinuationService.BlocksOrdinaryWork(w,id)||
                MortalTransportConstraintService.ValidateUnburdenedAction(w,id).IsFailure)return Fail("当前不能建造私人营地。");
            if(!CharacterWorldPresenceQuery.TryResolve(w,id,out var at)||!at.HasWorldPosition||at.SurfaceId!=a.SurfaceId||
                !w.SurfaceSpatial.TryGet(a.SurfaceId,out var metric)||WorldVec2.Distance(at.WorldPosition,new WorldVec2(a.WorldX+a.WorldWidth*.5f,a.WorldY+a.WorldHeight*.5f))>metric.CellSize*12)
                return Fail("请先靠近建造位置（12格内）。");
            if(!HuangcunOpeningAccess.AllowsPosition(w,a.SurfaceId,a.WorldX,a.WorldY)||
                !HuangcunOpeningAccess.AllowsPosition(w,a.SurfaceId,a.WorldX+a.WorldWidth,a.WorldY+a.WorldHeight))return Fail("不能越过尚未开放的盆地边界。");
            if(a.Kind==MarkerKind)
            {
                if(WorldSiteAdministrativeControlResolver.TryResolve(w,a.SurfaceId,a.WorldX,a.WorldY,out var site,out _)&&site!=null)return Fail("营地标记只能放在城池行政范围外。");
                foreach(var m in Assets(w))
                    if(m.Kind==MarkerKind&&m.SurfaceId==a.SurfaceId&&!w.OutdoorStatefulObjects.IsDestructibleDestroyed(m.StableAssetId)&&
                        WorldVec2.Distance(new WorldVec2(m.WorldX,m.WorldY),new WorldVec2(a.WorldX,a.WorldY))<metric.CellSize*RadiusCells*2)
                        return Fail("营地范围不能重叠，请另选空地。");
                if(w.Inventory.GetCount(HuangcunSurvivalService.Wood)<MarkerWoodCost)return Fail("营地标记需要私人粗木4。");
            }
            else
            {
                if(!TryCover(w,w.Strategic.PlayerFactionId,a,out _))return Fail("请先立营地标记，建筑必须完整位于24格范围内。");
                if(!w.ConstructionCatalog.TryGet(a.BuildingId,out var spec))return Fail("建筑配置不可用。");
                foreach(var c in spec.Costs)if(w.Inventory.GetCount(c.ItemId)<c.Count)return Fail("私人材料不足："+w.InventoryCatalog.GetName(c.ItemId));
            }
            var physical=OutdoorFactionConstructionAuthorizationService.ValidatePhysicalPlacement(w,a);if(physical.IsFailure)return physical;
            var geometry=RoomBedGeometry.ValidatePlacement(w.OutdoorConstructedAssets,a);if(geometry.IsFailure)return geometry;
            if(!w.SurfaceGround.TryGet(a.SurfaceId,out var ground))return Fail("地形未就绪。");
            foreach(var cell in a.EnumeratePhysicalCells(metric.OriginWorldX,metric.OriginWorldY,metric.CellSize))if(!ground.IsWalkable(cell.WorldX,cell.WorldY))return Fail("此处地形不可建造。");
            if(a.Kind=="room"||a.Kind=="bed"||a.Kind=="wallSegment")
                foreach(var person in w.Entities.All)
                {
                    if(!CharacterWorldPresenceQuery.TryResolve(w,person.Id,out var presence)||!presence.HasWorldPosition||presence.SurfaceId!=a.SurfaceId)continue;
                    var p=presence.WorldPosition;
                    if(p.X<a.WorldX||p.Y<a.WorldY||p.X>a.WorldX+a.WorldWidth||p.Y>a.WorldY+a.WorldHeight)continue;
                    if(a.Kind!="room")return Fail("建筑占地上有人，请让开后施工。");
                    for(var y=0;y<a.CellsH;y++)for(var x=0;x<a.CellsW;x++)for(var part=0;part<2;part++)
                        if(RoomBedGeometry.TryRoomWallRectangle(a,x,y,metric.CellSize,part==1,out var wx,out var wy,out var width,out var height)&&
                            p.X>=wx&&p.X<=wx+width&&p.Y>=wy&&p.Y<=wy+height)return Fail("房墙位置上有人，请让开后施工。");
                }
            return Result.Success();
        }
        public static Result Complete(SimulationWorld w,EntityId id,string task)
        {
            var valid=Validate(w,id,task);if(valid.IsFailure)return valid;TryCandidate(w,task,out var a);
            a.StableAssetId="runtime:private:"+w.OutdoorConstructedAssets.NextIdForKind(a.Kind);a.BoundLocationId="location:"+a.StableAssetId;
            var inventory=w.Inventory.CaptureState();
            if(a.Kind==MarkerKind)w.Inventory.TryRemoveAll(HuangcunSurvivalService.Wood,MarkerWoodCost);
            else {w.ConstructionCatalog.TryGet(a.BuildingId,out var spec);foreach(var c in spec.Costs)if(!w.Inventory.TryRemoveAll(c.ItemId,c.Count)){w.Inventory.RestoreState(inventory);return Fail("建材已变化。");}}
            if(!w.OutdoorConstructedAssets.TryRegister(a)){w.Inventory.RestoreState(inventory);return Fail("建造位置冲突，材料保留。");}
            w.OutdoorConstructedAssets.AdvanceSequence(); MarkOwner(w,a.StableAssetId,w.Strategic.PlayerFactionId);
            if(a.Kind=="grainField")foreach(var anchor in a.AdministrativeCellAnchors()) { w.OutdoorAdministrativeAssetAnchors.TryRegister(anchor); MarkOwner(w,anchor.StableAssetId,w.Strategic.PlayerFactionId); }
            if(RoomBedGeometry.IsRoom(a)&&string.IsNullOrEmpty(w.HuangcunSurvival.CampRoomId))
            {w.HuangcunSurvival.CampRoomId=a.StableAssetId;w.HuangcunSurvival.Places["camp"]=new WorldVec2(a.WorldX+a.WorldWidth*.5f,a.WorldY+w.OpeningArea.CellSize*3);}
            w.HuangcunSurvival.Message=a.Kind==MarkerKind?"营地标记已立，24格内可亲自建设；被巡卫发现会遭袭。":"营地建筑已完成，已消耗真实私人材料。";
            w.Events.Publish(XianXia.Core.Events.EventType.PartyInventoryChanged,w.Tick,actor:id,payload:"private-build:"+a.StableAssetId);
            return Result.Success();
        }
        public static bool IsInsideOwnedRoom(SimulationWorld w,EntityId id)
        {
            if(!CharacterWorldPresenceQuery.TryResolve(w,id,out var at)||!at.HasWorldPosition)return false;
            foreach(var a in Assets(w))
                if(RoomBedGeometry.IsRoom(a)&&a.SurfaceId==at.SurfaceId&&Owns(w,a.StableAssetId,w.Strategic.PlayerFactionId)&&
                    !w.OutdoorStatefulObjects.IsDestructibleDestroyed(a.StableAssetId)&&at.WorldPosition.X>a.WorldX&&at.WorldPosition.X<a.WorldX+a.WorldWidth&&at.WorldPosition.Y>a.WorldY&&at.WorldPosition.Y<a.WorldY+a.WorldHeight)return true;
            return false;
        }
    }
}
