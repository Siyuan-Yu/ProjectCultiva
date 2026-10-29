using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Combat;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Optional, non-blocking life notebook. Gameplay and validation stay in Core.</summary>
    public sealed class HostHuangcunSurvivalPanel
    {
        bool _open;
        Vector2 _scroll;
        GUIStyle _text;
        string _feedback = "";
        public bool Draw(PlayableHostSession session)
        {
            var w=session.World;
            if (!w.HuangcunSurvival.Enabled) { _open=false; return false; }
            if(_text==null) _text=new GUIStyle(GUI.skin.label) { fontSize=14,wordWrap=true };
            if(!_open)
            {
                DrawPlaceLabels(session);
                var button=new Rect(8,Screen.height-88,190,28);
                HostUiHitTest.Block(button);
                if(GUI.Button(button,"荒村生活 / 同伴营救")) _open=true;
                var notice=new Rect(8,button.yMax+3,260,44); HostUiHitTest.Block(notice);
                GUI.Box(notice,GUIContent.none);
                var captive=HuangcunSurvivalService.Prisoner(w);
                string warning="营地与口粮可在生活面板查看";
                if(captive!=null && !w.HuangcunSurvival.Released && captive.TryGet<LifecycleComponent>(out var life))
                {
                    captive.TryGet<PersonalLifeComponent>(out var needs); captive.TryGet<CombatVitalsComponent>(out var hp);
                    warning=life.IsDead || life.IsRemoved ? "被囚同伴已经死亡" : "被囚同伴：生命 "+(hp?.CurrentHp??0)+" · 饱食 "+(needs?.Satiety??0)+"\n走进牢房，靠近同伴按 F 或右键交谈与照料";
                }
                GUI.Label(new Rect(notice.x+5,notice.y+3,notice.width-10,notice.height-6),warning,_text);
                return false;
            }
            var width=Mathf.Min(760,Screen.width-24); var height=Mathf.Min(760,Screen.height-120);
            var panel=new Rect((Screen.width-width)/2,96,width,height);
            HostUiHitTest.Block(new Rect(0,0,Screen.width,Screen.height));
            GUI.Box(panel,GUIContent.none);
            GUI.Label(new Rect(panel.x+14,panel.y+10,width-100,28),"荒村生活 · 自选行动，世界时间继续流逝",_text);
            if(GUI.Button(new Rect(panel.xMax-70,panel.y+8,58,26),"关闭")) _open=false;
            var view=new Rect(panel.x+12,panel.y+46,width-24,height-140);
            var content=new Rect(0,0,view.width-22,3100);
            _scroll=GUI.BeginScrollView(view,_scroll,content);
            float y=0; var cw=content.width;
            Label("每日配给：每人最多 24 饱食，饥饿时到短工集合点附近领取，只够维生。同伴可单独分工，也可允许饥饿时取用队伍私粮；分工者留在当地，不随队移动。修炼须饱食 >40、精力 >15。",ref y,cw,72);
            if(session.PlayerParty.HasActive && w.Entities.TryGet(session.PlayerParty.ActiveCharacterId,out var actor))
            {
                actor.TryGet<PersonalLifeComponent>(out var needs);
                Label("当前主控：饱食 "+(needs?.Satiety??0)+" / 精力 "+(needs?.Energy??0)+"    私粮 "+w.Inventory.GetCount(HuangcunSurvivalService.Grain)+" · 木料 "+w.Inventory.GetCount(HuangcunSurvivalService.Wood)+" · 灵草 "+w.Inventory.GetCount(HuangcunSurvivalService.Herb),ref y,cw,30);
            }
            if(GUI.Button(new Rect(0,y,cw,30),"基础加工与丹药：矿锭、铁镐、疗伤散、培元丸")){HostBasicCraftPanel.Open();_open=false;} y+=38;
            Row(session,"eat","吃一份私粮：饱食 +30",ref y,cw,false);
            foreach(var member in session.PlayerParty.Members)
            {
                if(!w.Entities.TryGet(member,out var companion)||!HuangcunSurvivalService.Applies(w,companion))continue;
                var eat=OpeningCompanionDuty.AutoFood(w,member);
                var chosen=GUI.Toggle(new Rect(0,y,cw,28),eat,companion.DisplayName+"：允许饥饿时自动吃私粮");y+=30;
                if(chosen!=eat)OpeningCompanionDuty.SetAutoFood(w,member,chosen);
                if(member==session.PlayerParty.ActiveCharacterId)continue;
                var oldDuty=OpeningCompanionDuty.Duty(w,member);
                var selectedDuty=oldDuty<OpeningCompanionDuty.Names.Length?oldDuty:-1;
                var duty=GUI.SelectionGrid(new Rect(0,y,cw,54),selectedDuty,OpeningCompanionDuty.Names,4);y+=60;
                if(duty>=0&&duty!=selectedDuty)
                {
                    var assigned=OpeningCompanionDuty.Assign(w,session.Loop,member,duty);
                    _feedback=assigned.IsFailure?assigned.Error.Message:"分工已安排；手动下令或切为主控会接管此人。";
                }
                companion.TryGet<PersonalLifeComponent>(out var companionNeeds);
                Label(companionNeeds?.Status??"等待安排",ref y,cw,28);
            }
            Row(session,"work","短工：60 游戏分钟，粮食 +1，劳动额 +12",ref y,cw);
            Row(session,"food","采集口粮：40 分钟，粮食 +1，每日最多 6 次",ref y,cw);
            Row(session,"wood","捡取木料：40 分钟，木料 +4，每日最多 6 次",ref y,cw);
            Row(session,"herb","采集灵草：40 分钟，灵草 +1，每日最多 3 次",ref y,cw);
            Label("私人营地：先在村外空地立标记（粗木4，半径24格），再从顶部建筑面板选位置建设。由当前人物亲自施工，不需要城池建筑工；巡卫看见会立即追来战斗。",ref y,cw,65);
            if(GUI.Button(new Rect(0,y,cw,30),"在当前位置立营地标记（粗木4，6秒）"))
            {
                var id=session.PlayerParty.ActiveCharacterId;var task=PrivateCampService.MarkerTask(w,id);
                var check=PrivateCampService.Validate(w,id,task);
                if(check.IsSuccess)check=session.Loop.EnqueueOrder(new Order(session.Loop.AllocateOrderId(),id,OrderType.HuangcunSurvival,OrderSource.Player,waitTicks:6,targetRef:task));
                _feedback=check.IsFailure?check.Error.Message:"正在立营地标记。";
            }
            y+=40;
            if(!string.IsNullOrEmpty(w.HuangcunSurvival.CampRoomId))
            {
                Row(session,"camp_rest","营地休息：精力 +35；不瞬间治愈伤势",ref y,cw);
                foreach(var item in HuangcunSurvivalService.StorageItems)
                {
                    var itemId=HuangcunSurvivalService.StorageItemId(item);
                    var n=PrivateCampService.Stored(w,session.PlayerParty.ActiveCharacterId,itemId);
                    Row(session,"store_"+item,"存入 "+w.InventoryCatalog.GetName(itemId)+" 1（现存 "+n+" /30）",ref y,cw,false);
                    Row(session,"take_"+item,"取出营地 "+w.InventoryCatalog.GetName(itemId)+" 1",ref y,cw,false);
                }
            }
            if(GUI.Button(new Rect(0,y,cw,28),"在当前位置修炼一阵（可在野外；营地效率更高）"))
            {
                var order=session.Loop.CreateCultivateOrder(session.PlayerParty.ActiveCharacterId,12);
                var translated=w.Translator.Translate(order);
                var r=translated.IsFailure?Result.Failure(translated.Error):translated.Value.CanStart(w);
                if(r.IsSuccess) r=session.Loop.EnqueueOrder(order);
                _feedback=r.IsFailure?r.Error.ToString():"已安排修炼；状态不满足时请查看下方反馈。";
            }
            y+=38;
            var prisoner=HuangcunSurvivalService.Prisoner(w);
            if(prisoner!=null)
            {
                prisoner.TryGet<CombatVitalsComponent>(out var hp); prisoner.TryGet<PersonalLifeComponent>(out var needs);
                Label("被囚同伴：生命 "+(hp?.CurrentHp??0)+" · 饱食 "+(needs?.Satiety??0)+" · "+(w.HuangcunSurvival.Released?"已解救":CombatLifeStateService.FormatLifeStateWithCountdown(w,prisoner))+"。稀粥只能勉强维生。进牢房后靠近同伴按 F 或右键交谈、接请求、送饭送药；占领前牢房及外沿三格是禁区，被室外清醒且未修炼的主管或巡卫看见会直接追赶开战。",ref y,cw,72);
            }
            if(!w.HuangcunSurvival.Released)
            {
                Row(session,"ransom","交涉赎人：粮食 6 + 灵草 3",ref y,cw);
                Label("武力营救：右键监狱墙体，按正常宣战流程开战；打出可通行缺口后自动解除拘押。占领后可对同伴下令释放。",ref y,cw,55);
            }
            else Label("获救不自动痊愈。靠近清醒同伴后邀请入队；昏迷者先治疗、搬运。救援失败不会强制重开，也不会锁住夺取荒村。",ref y,cw,55);
            GUI.EndScrollView();
            GUI.Label(new Rect(panel.x+14,panel.yMax-88,width-28,40),w.HuangcunSurvival.Message,_text);
            GUI.Label(new Rect(panel.x+14,panel.yMax-47,width-28,40),_feedback,_text);
            return true;
        }
        void DrawPlaceLabels(PlayableHostSession s)
        {
            var camera=Camera.main; var w=s.World;
            if(camera==null || w.LocalMap.IsInInterior || w.PlayerPartyTravel.SurfaceId!=w.OpeningArea.SurfaceId) return;
            foreach(var pair in w.HuangcunSurvival.Places)
            {
                if(!string.IsNullOrEmpty(w.HuangcunSurvival.CampRoomId) && pair.Key.StartsWith("camp_")) continue;
                if(w.HuangcunSurvival.Released && pair.Key=="prison") continue;
                var p=camera.WorldToScreenPoint(HostPresentationSpace.FromPresentation(pair.Value.X,pair.Value.Y));
                var y=Screen.height-p.y;
                if(p.z<=0 || p.x<100 || p.x>Screen.width-350 || y<170 || y>Screen.height-290) continue;
                if(pair.Key=="prison" || pair.Key=="camp_near" || pair.Key=="camp_far") continue; // Prison visits use the physical room's right-click menu.
                var name=pair.Key=="food"?"拾荒口粮点":pair.Key=="wood"?"枯枝采集点":pair.Key=="herb"?"野生灵草点":
                    pair.Key=="camp_near"?"近郊营地选址":pair.Key=="camp_far"?"远郊营地选址":pair.Key=="camp"?"私人修炼棚":pair.Key=="work"?"短工集合点":"监狱探视处";
                var rect=new Rect(p.x-68,y-14,136,28); HostUiHitTest.Block(rect);
                if(GUI.Button(rect,name)) { _feedback="当前位置："+name+"。在相应一行选择行动。"; _open=true; }
            }
        }
        void Label(string text,ref float y,float width,float h) { GUI.Label(new Rect(0,y,width,h),text,_text); y+=h+5; }
        void Row(PlayableHostSession s,string task,string label,ref float y,float width,bool travel=true)
        {
            GUI.Label(new Rect(0,y,width-168,34),label,_text);
            if(travel && GUI.Button(new Rect(width-160,y,72,28),"前往"))
            {
                if(HuangcunSurvivalService.TryDestination(s.World,task,out var point))
                {
                    var r=PlayerPartySurfaceTravelService.BeginTravel(s.World,s.PlayerParty,point,"",s.World.OpeningArea.CellSize*(task=="camp_rest"?.3f:3f));
                    _feedback=r.IsFailure?r.Error.ToString():"已开始前往，路途仍受村门巡卫与地形限制。";
                    if(r.IsSuccess) _open=false;
                }
            }
            if(GUI.Button(new Rect(width-80,y,76,28),"行动"))
            {
                var id=s.PlayerParty.ActiveCharacterId;
                var check=HuangcunSurvivalService.Validate(s.World,id,task);
                if(check.IsFailure) _feedback=check.Error.ToString();
                else
                {
                    var r=s.Loop.EnqueueOrder(new Order(s.Loop.AllocateOrderId(),id,OrderType.HuangcunSurvival,OrderSource.Player,
                        waitTicks:HuangcunSurvivalService.Duration(task),targetRef:task));
                    _feedback=r.IsFailure?r.Error.ToString():"行动进行中，离开地点或条件变化会中止；完成时才结算。";
                }
            }
            y+=39;
        }
    }
}
