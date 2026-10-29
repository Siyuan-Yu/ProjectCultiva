using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Entities;
using XianXia.Core.Actions;
using XianXia.Core.Orders;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public static class HostBasicCraftPanel
    {
        static bool _open;
        static Vector2 _scroll;
        static string _feedback="";
        static XianXia.Core.Simulation.SimulationWorld _world;
        public static void Open() { _open=true; }
        public static bool Draw(PlayableHostSession session)
        {
            if(_world!=session.World){if(_world!=null)_open=false;_world=session.World;_feedback="";}
            if(!_open)return false;
            var w=session.World; var id=session.PlayerParty.ActiveCharacterId;
            var panel=new Rect((Screen.width-Mathf.Min(850,Screen.width-30))/2,85,Mathf.Min(850,Screen.width-30),Mathf.Min(760,Screen.height-105));
            HostUiHitTest.Block(new Rect(0,0,Screen.width,Screen.height)); GUI.Box(panel,GUIContent.none);
            var text=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=14};
            GUI.Label(new Rect(panel.x+12,panel.y+10,panel.width-100,30),"基础加工与丹药 · 时间继续流逝",text);
            if(GUI.Button(new Rect(panel.xMax-75,panel.y+8,60,28),"关闭"))_open=false;
            var view=new Rect(panel.x+12,panel.y+46,panel.width-24,panel.height-130);
            var recipients=0;
            foreach(var person in w.Entities.All)
                if(w.Strategic.PlayerPartyContext?.IsMember(person.Id)==true || HuangcunSurvivalService.IsPrisoner(w,person.Id))recipients++;
            var contentHeight=110+BasicCraftService.Recipes.Length*62+recipients*70;
            _scroll=GUI.BeginScrollView(view,_scroll,new Rect(0,0,view.width-20,contentHeight));
            var width=view.width-20;float y=0;
            GUI.Label(new Rect(0,y,width,55),"先前往丹室／炼器室内操作点，再开始制作。材料在完成时扣除；离开或战斗会中断。疗伤散提供药力，培元丸与凝神器须通过实际打坐炼化，不直接突破。",text);y+=60;
            foreach(var r in BasicCraftService.Recipes)
            {
                var costs="";foreach(var c in r.Costs)costs+=w.InventoryCatalog.GetName(c.Key)+" "+w.Inventory.GetCount(c.Key)+"/"+c.Value+"  ";
                GUI.Label(new Rect(0,y,width-180,52),r.Name+" · "+r.Ticks+"秒（1×）\n"+costs,text);
                var exists=BasicCraftService.FindRoom(w,id,r.RoomKind,out var room);
                GUI.enabled=exists;
                if(GUI.Button(new Rect(width-170,y,76,30),"前往"))
                {
                    var result=PlayerPartySurfaceTravelService.BeginTravel(w,session.PlayerParty,BasicCraftService.Point(w,room),"",w.OpeningArea.CellSize*.5f);
                    _feedback=result.IsFailure?result.Error.Message:"正在前往工坊内操作点。";if(result.IsSuccess)_open=false;
                }
                if(GUI.Button(new Rect(width-86,y,82,30),"制作"))
                {
                    var task=BasicCraftService.Task(r,room.StableAssetId);var check=BasicCraftService.Validate(w,id,task);
                    if(check.IsSuccess)check=session.Loop.EnqueueOrder(new Order(session.Loop.AllocateOrderId(),id,OrderType.HuangcunSurvival,OrderSource.Player,waitTicks:r.Ticks,targetRef:task));
                    _feedback=check.IsFailure?check.Error.Message:"制作中："+r.Name;
                }
                GUI.enabled=true;y+=62;
            }
            GUI.Label(new Rect(0,y,width,40),"工具与兵器：按 B，在背包中装备给主控后生效。装备矿镐采矿为7秒（1×）。丹药／凝神器在下方对人使用。",text);y+=50;
            foreach(var e in w.Entities.All)
            {
                if(w.Strategic.PlayerPartyContext?.IsMember(e.Id)!=true && !HuangcunSurvivalService.IsPrisoner(w,e.Id))continue;
                GUI.Label(new Rect(0,y,width,25),e.DisplayName+"（对同伴使用须靠近，囚犯须同房合法探视）",text);y+=28;
                var x=0f;foreach(var item in new[]{BasicCraftService.Powder,BasicCraftService.Pill,BasicCraftService.Focus})
                {
                    if(GUI.Button(new Rect(x,y,(width-12)/3,32),w.InventoryCatalog.GetName(item)+" ×"+w.Inventory.GetCount(item)))
                    {var result=BasicCraftService.Use(w,id,e.Id,item);_feedback=result.IsFailure?result.Error.Message:"已对"+e.DisplayName+"使用"+w.InventoryCatalog.GetName(item);}
                    x+=width/3;
                }
                y+=42;
            }
            GUI.EndScrollView();
            if(w.Entities.TryGet(id,out var actor)&&actor.TryGet<ActionStateComponent>(out var action)&&action.HasActiveAction &&
                w.ActiveActions.TryGetValue(action.ActiveActionId,out var active)&&active is HuangcunSurvivalAction crafting&&BasicCraftService.IsTask(crafting.Task))
                _feedback="制作进行中："+BasicCraftService.Find(crafting.Task).Name+"（余 "+crafting.Clock.RemainingTicks+" 拍）；可移动或停止来取消。";
            GUI.Label(new Rect(panel.x+12,panel.yMax-75,panel.width-24,65),_feedback+"\n"+w.HuangcunSurvival.Message,text);
            return true;
        }
    }
}
