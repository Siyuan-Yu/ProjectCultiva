using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>Player-assigned independent opening duties; never replaces a manual order or combat.</summary>
    public static class OpeningCompanionDuty
    {
        static readonly string[] Tasks={"","food","wood","herb","work","rest","cultivate"};
        public static readonly string[] Names={"随队","拾荒口粮","采集木料","采集灵草","短工","原地休息","原地修炼"};
        static string Key(EntityId id)=>"base:opening-duty:"+id;
        public static int Duty(SimulationWorld w,EntityId id)=>w.ContentCounters.Get(Key(id));
        public static bool Owns(SimulationWorld w,EntityId id)=>w.HuangcunSurvival.Enabled && Duty(w,id)>0 &&
            w.Strategic.PlayerPartyContext?.IsMember(id)==true && w.Strategic.PlayerPartyContext.ActiveCharacterId!=id;
        public static bool AutoFood(SimulationWorld w,EntityId id)=>w.ContentCounters.Get(Key(id)+":food")>0;
        public static void SetAutoFood(SimulationWorld w,EntityId id,bool value)
        {if(w.Strategic.PlayerPartyContext?.IsMember(id)==true)w.ContentCounters.Set(Key(id)+":food",value?1:0);}
        public static Result Assign(SimulationWorld w,SimulationLoop loop,EntityId id,int duty)
        {
            if(duty<0||duty>=Tasks.Length||w.Strategic.PlayerPartyContext?.IsMember(id)!=true||
                !w.Entities.TryGet(id,out var e)||!CombatLifeStateService.CanFight(e)||
                !HuangcunSurvivalService.Applies(w,e)||w.Strategic.CharacterEncounter!=null||
                duty>0&&w.Strategic.PlayerPartyContext.ActiveCharacterId==id)
                return Result.Failure(ErrorCode.ActionCannotStart,"仅可给未主控的清醒在队同伴分工；战斗中不可更改。");
            if(CharacterWorldPresenceQuery.TryResolve(w,id,out var at)&&at.HasWorldPosition)
                w.WorldPresence.SetAtWorldPosition(id,at.WorldPosition,at.SurfaceId);
            var returning=duty==0&&Owns(w,id);
            loop.StopSubject(id); ClearIntent(e);w.ContentCounters.Set(Key(id),returning?7:duty);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(w,w.Strategic.PlayerPartyContext);
            return Result.Success();
        }
        static void ClearIntent(Entity e)
        {if(e.TryGet<MovementIntentComponent>(out var m)&&m.DestinationKey?.StartsWith("opening-duty:")==true)m.Clear();}
        public static void Drive(SimulationWorld w,SimulationLoop loop)
        {
            foreach(var e in w.Entities.All)
            {
                if(Duty(w,e.Id)==0)continue;
                if(!Owns(w,e.Id)) {w.ContentCounters.Set(Key(e.Id),0);ClearIntent(e);continue;}
                if(!CombatLifeStateService.CanFight(e)||AutonomousActionContinuationService.BlocksOrdinaryWork(w,e.Id)||
                    MortalTransportConstraintService.ValidateUnburdenedAction(w,e.Id).IsFailure||PartyRestService.BlocksMovement(e))
                {ClearIntent(e);continue;}
                var queue=w.GetOrCreateOrderQueue(e.Id);
                if(queue.HasSource(OrderSource.Player)||queue.HasSource(OrderSource.Event)||
                    e.TryGet<ActionStateComponent>(out var state)&&state.HasActiveAction&&state.ActiveOrderSource!=OrderSource.Ai)
                {w.ContentCounters.Set(Key(e.Id),0);ClearIntent(e);continue;}
                if(state!=null&&state.HasActiveAction)continue;
                var needs=PersonalLifeService.Ensure(w,e); if(needs==null)continue;
                var duty=Duty(w,e.Id);
                if(duty==7)
                {
                    if(!CharacterWorldPresenceQuery.TryResolve(w,e.Id,out var from)||!from.HasWorldPosition||
                        !CharacterWorldPresenceQuery.TryResolve(w,w.Strategic.PlayerPartyContext.ActiveCharacterId,out var leader)||!leader.HasWorldPosition||leader.SurfaceId!=from.SurfaceId)
                    {needs.Status="等待归队：主控不在同一地表";continue;}
                    var distance=XianXia.Core.World.WorldVec2.Distance(from.WorldPosition,leader.WorldPosition);
                    if(distance<=w.OpeningArea.CellSize*3&&WorldObservation.HasLineOfSight(w,from.SurfaceId,from.WorldPosition,leader.WorldPosition))
                    {
                        ClearIntent(e);w.ContentCounters.Set(Key(e.Id),0);
                        PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(w,w.Strategic.PlayerPartyContext);continue;
                    }
                    if(!e.TryGet<MovementIntentComponent>(out var returning)){returning=new MovementIntentComponent();e.AddComponent(returning);}
                    if(!returning.Active||!returning.HasWorldTarget||XianXia.Core.World.WorldVec2.Distance(new XianXia.Core.World.WorldVec2(returning.TargetWorldX,returning.TargetWorldY),leader.WorldPosition)>w.OpeningArea.CellSize*2)
                        returning.BeginWorldPoint(from.SurfaceId,leader.WorldPosition.X,leader.WorldPosition.Y,destinationKey:"opening-duty:return",arrivalRadiusWorld:w.OpeningArea.CellSize*2);
                    if(!w.ContinuousOutdoorMaterialization.IsMaterialized(e.Id))NpcColdMovement.Advance(w,e,from.SurfaceId,leader.WorldPosition,w.OpeningArea.CellSize*2,out _);
                    needs.Status="分工结束：正在步行归队";continue;
                }
                if(duty<=0||duty>=Tasks.Length)continue;
                var task=Tasks[duty];
                if(needs.Energy<=15||task=="rest")
                {ClearIntent(e);loop.EnqueueOrder(new Order(loop.AllocateOrderId(),e.Id,OrderType.Rest,OrderSource.Ai,waitTicks:6));needs.Status="分工：休息";continue;}
                if(task!="cultivate"&&HuangcunSurvivalService.TryDestination(w,task,out var destination))
                {
                    if(!CharacterWorldPresenceQuery.TryResolve(w,e.Id,out var at)||!at.HasWorldPosition||at.SurfaceId!=w.OpeningArea.SurfaceId)continue;
                    if(XianXia.Core.World.WorldVec2.Distance(at.WorldPosition,destination)>w.OpeningArea.CellSize*3)
                    {
                        if(!e.TryGet<MovementIntentComponent>(out var intent)){intent=new MovementIntentComponent();e.AddComponent(intent);}
                        if(!intent.Active||intent.DestinationKey!="opening-duty:"+task)
                            intent.BeginWorldPoint(at.SurfaceId,destination.X,destination.Y,destinationKey:"opening-duty:"+task,arrivalRadiusWorld:w.OpeningArea.CellSize*2);
                        if(!w.ContinuousOutdoorMaterialization.IsMaterialized(e.Id))NpcColdMovement.Advance(w,e,at.SurfaceId,destination,w.OpeningArea.CellSize*2,out _);
                        needs.Status="分工：前往"+Names[duty];continue;
                    }
                }
                ClearIntent(e);
                var order=new Order(loop.AllocateOrderId(),e.Id,task=="cultivate"?OrderType.Cultivate:OrderType.HuangcunSurvival,
                    OrderSource.Ai,waitTicks:task=="cultivate"?12UL:HuangcunSurvivalService.Duration(task),targetRef:task);
                var translated=w.Translator.Translate(order);
                var result=translated.IsFailure?Result.Failure(translated.Error):translated.Value.CanStart(w);
                if(result.IsSuccess)result=loop.EnqueueOrder(order);
                needs.Status=result.IsFailure?"分工等待："+result.Error.Message:"分工："+Names[duty];
            }
        }
    }
}
