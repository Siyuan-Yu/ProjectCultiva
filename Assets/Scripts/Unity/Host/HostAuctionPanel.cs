using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Inventory;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>Formal AUCTION-01 UGUI. Core owns listing, escrow, review, settlement, and claim commands.</summary>
    public sealed class HostAuctionPanel : MonoBehaviour
    {
        const string PauseOwner = "AuctionHouse";
        PlayableHostBootstrap host; PlayableHostSession pausedSession; SimulationWorld openedWorld;
        EntityId actor, provider; string houseId, selectedListing, selectedItem, selectedClaim; int tab, quantity=1;
        GameObject root; RectTransform panel, rows; Text header, status, detail, quantityText; InputField startingPrice; Button bidButton, consignButton, claimButton; Font font;
        public bool IsOpen => root != null && root.activeSelf;
        public void Bind(PlayableHostBootstrap value) => host=value;

        public bool TryOpen(EntityId acting, EntityId target)
        {
            if(host?.Session==null || !host.Session.IsInitialized || IsOpen || host.Session.ModalHardPaused ||
                acting!=HostNpcInteraction.ResolveActiveCommandAuthority(host.Session) || HostNpcInteraction.IsHostileNpc(host.Session,target) || !Near(acting,target) ||
                !AuctionHouseService.TryGetProvider(host.Session.World.Commerce,host.Session.World,target,out var id)) return false;
            host.InventoryPanel?.Close(); host.WorldMapPanel?.Close(); host.ConstructionPanel?.Close();
            actor=acting; provider=target; houseId=id; tab=0; quantity=1; selectedListing=selectedItem=selectedClaim=null;
            if(root==null) Build(); pausedSession=host.Session; openedWorld=pausedSession.World;
            pausedSession.AcquireModalPause(PauseOwner); HostInputGate.Acquire(PauseOwner); host.GetComponent<HostLevelTesterCheatPanel>()?.Hide();
            root.SetActive(true); status.text="同档灵石竞价，不自动兑换。出价与寄拍物品立即进入托管。"; Refresh(); Block(); return true;
        }

        bool Near(EntityId a,EntityId b)
        {
            if(host?.ViewSpawner==null || !host.ViewSpawner.Registry.TryGet(a,out var av) || av==null || !host.ViewSpawner.Registry.TryGet(b,out var bv) || bv==null) return false;
            var d=av.transform.position-bv.transform.position; d.z=0; return d.sqrMagnitude<=HostNpcInteraction.DefaultMeleeEngageRange*HostNpcInteraction.DefaultMeleeEngageRange;
        }
        void Block(){HostInputGate.BlockWorldInteraction=true;HostInputGate.BlockWorldCamera=true;}
        void Update()
        {
            if(!IsOpen)return;
            if(host?.Session!=pausedSession || host.Session.World!=openedWorld || Input.GetKeyDown(KeyCode.Escape) || host.InventoryPanel.IsOpen || host.WorldMapPanel.IsOpen || host.ConstructionPanel.IsOpen ||
                host.GetComponent<HostShopTradePanel>()?.IsOpen==true || host.GetComponent<HostLevelTesterCheatPanel>()?.IsVisible==true){Close();return;} Block();
        }
        public void Close(){if(root!=null)root.SetActive(false);pausedSession?.ReleaseModalPause(PauseOwner);pausedSession=null;openedWorld=null;host?.GetComponent<HostMoveController>()?.ReleaseNpcForInteraction(provider);HostInputGate.Release(PauseOwner);HostInputGate.Clear();}
        void OnDisable(){if(IsOpen||pausedSession!=null)Close();} void OnDestroy(){if(root!=null)Destroy(root);}

        void Refresh()
        {
            if(openedWorld==null || !openedWorld.Commerce.AuctionDefinitions.TryGetValue(houseId,out var definition) || !openedWorld.Commerce.AuctionHouses.TryGetValue(houseId,out var house)){Close();return;}
            header.text=definition.DisplayName+"\n玩家灵石："+Wallet(openedWorld.Commerce.PlayerWallet)+"   拍卖行资金："+Wallet(house.HouseWallet); bidButton.interactable=false;consignButton.interactable=false;claimButton.interactable=false;startingPrice.interactable=tab==1;
            quantityText.text="数量："+quantity; ClearRows(rows); detail.text=""; float y=0;
            if(tab==0)
            {
                foreach(var listing in Ordered(house)) if(listing.Status==AuctionListingStatus.Open)
                { var current=listing; Button(rows,(selectedListing==current.ListingId?"▶ ":"")+ItemName(current.ItemId)+" ×"+current.Quantity+"   "+Price(current)+"\n最高："+Bidder(current)+"   剩余 "+Remaining(current),0,y,740,52,()=>{selectedListing=current.ListingId;Refresh();});y+=56; }
                if(selectedListing!=null && house.Listings.TryGetValue(selectedListing,out var selected) && selected.Status==AuctionListingStatus.Open)
                { var own=selected.SellerKind==AuctionSellerKind.Player; var high=selected.CurrentBidderKind==AuctionBidderKind.Player; bidButton.interactable=!own&&!high; detail.text=ItemName(selected.ItemId)+" ×"+selected.Quantity+" · "+(own?"自己的寄拍":"最低出价 "+Minimum(definition,selected))+" · "+(high?"你是最高出价者":"最高 "+Bidder(selected)); }
            }
            else if(tab==1)
            {
                foreach(var listing in Ordered(house)) if(listing.SellerKind==AuctionSellerKind.Player && listing.Status==AuctionListingStatus.Open)
                { Button(rows,ItemName(listing.ItemId)+" ×"+listing.Quantity+"   "+Price(listing)+"\n最高："+Bidder(listing)+"   剩余 "+Remaining(listing),0,y,740,52,()=>{});y+=56; }
                var seen=new HashSet<string>();
                foreach(var slot in openedWorld.Inventory.Slots) if(!slot.IsEmpty && seen.Add(slot.ItemId) && openedWorld.InventoryCatalog.TryGet(slot.ItemId,out var item) && ShopTradeService.Tradable(item))
                { var id=slot.ItemId; Button(rows,(selectedItem==id?"▶ ":"")+"寄拍候选："+item.Name+" ×"+openedWorld.Inventory.GetCount(id)+" · 档位 "+ShopTradeService.GradeName(item.BaseTradePrice.Grade),0,y,740,44,()=>{selectedItem=id;startingPrice.text=item.BaseTradePrice.Amount.ToString();Refresh();});y+=48; }
                detail.text=string.IsNullOrEmpty(selectedItem)?"选择随身背包中的可寄拍物品。":"整批起拍价（档位由物品基准价固定）："+startingPrice.text;
                consignButton.interactable=!string.IsNullOrEmpty(selectedItem);
            }
            else
            {
                foreach(var claim in Claims(house))
                { var current=claim; Button(rows,(selectedClaim==current.ClaimId?"▶ ":"")+ItemName(current.ItemId)+" ×"+current.Quantity+" · "+(current.ClaimKind==AuctionClaimKind.WonItem?"竞拍所得":"流拍退回"),0,y,740,46,()=>{selectedClaim=current.ClaimId;Refresh();});y+=50; }
                detail.text=string.IsNullOrEmpty(selectedClaim)?"选择待领取物。":"领取前检查随身背包容量；失败时 Claim 会保留。";
                claimButton.interactable=!string.IsNullOrEmpty(selectedClaim);
            }
            rows.sizeDelta=new Vector2(0,Mathf.Max(310,y));
        }

        void Bid()
        {
            if(!ValidCommand() || !openedWorld.Commerce.AuctionDefinitions.TryGetValue(houseId,out var def) || !openedWorld.Commerce.AuctionHouses[houseId].Listings.TryGetValue(selectedListing??"",out var listing) || !AuctionHouseService.TryGetMinimumBid(def,listing,out var amount)){status.text="请选择可竞拍的拍品。";return;}
            AuctionHouseService.TryBid(openedWorld,provider,houseId,listing.ListingId,amount,out var message);status.text=message;Refresh();
        }
        void Consign()
        {
            if(!ValidCommand() || !long.TryParse(startingPrice.text,out var amount)){status.text="请输入正整数整批起拍价。";return;}
            AuctionHouseService.TryConsign(openedWorld,provider,houseId,selectedItem,quantity,amount,out var message);status.text=message;Refresh();
        }
        void Claim()
        { if(!ValidCommand()){Close();return;} AuctionHouseService.TryClaim(openedWorld,provider,houseId,selectedClaim,out var message);status.text=message;Refresh(); }
        bool ValidCommand()=>IsOpen&&openedWorld==host.Session.World&&actor==HostNpcInteraction.ResolveActiveCommandAuthority(host.Session)&&Near(actor,provider)&&!HostNpcInteraction.IsHostileNpc(host.Session,provider);

        List<AuctionListing> Ordered(AuctionHouseRuntime house){var list=new List<AuctionListing>(house.Listings.Values);list.Sort((a,b)=>{var c=a.EndTick.CompareTo(b.EndTick);return c!=0?c:a.Sequence.CompareTo(b.Sequence);});return list;}
        List<AuctionClaim> Claims(AuctionHouseRuntime house){var list=new List<AuctionClaim>(house.Claims.Values);list.Sort((a,b)=>a.Sequence.CompareTo(b.Sequence));return list;}
        string Wallet(SpiritStoneWallet w)=>"下品 "+w.Low+"  中品 "+w.Mid+"  上品 "+w.High;
        string ItemName(string id)=>openedWorld.InventoryCatalog.TryGet(id,out var item)?item.Name:id;
        string Price(AuctionListing l)=>l.CurrentBidderKind==AuctionBidderKind.None?"起拍 "+l.StartingPriceAmount+" "+ShopTradeService.GradeName(l.Grade):"当前 "+l.CurrentBidAmount+" "+ShopTradeService.GradeName(l.Grade);
        string Bidder(AuctionListing l)=>l.CurrentBidderKind==AuctionBidderKind.None?"暂无":l.CurrentBidderKind==AuctionBidderKind.Player?"你":"市场竞拍者";
        string Minimum(AuctionHouseDefinition d,AuctionListing l)=>AuctionHouseService.TryGetMinimumBid(d,l,out var n)?n+" "+ShopTradeService.GradeName(l.Grade):"不可加价";
        string Remaining(AuctionListing l){var ticks=l.EndTick>openedWorld.Tick.Value?l.EndTick-openedWorld.Tick.Value:0;var days=ticks/(ulong)WorldTick.TicksPerDay;var hours=(ticks%(ulong)WorldTick.TicksPerDay)/(ulong)(60/WorldTick.GameMinutesPerTick);return days+"天"+hours+"时";}
        static void ClearRows(RectTransform parent){for(int i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}}
        RectTransform Box(Transform parent,string name,float x,float y,float w,float h){var go=new GameObject(name,typeof(RectTransform));var rt=go.GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;}
        Text Label(Transform parent,string value,float x,float y,float w,float h,int size=16){var rt=Box(parent,"Label",x,y,w,h);var t=rt.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=new Color(.95f,.9f,.8f);t.text=value;t.raycastTarget=false;return t;}
        Button Button(Transform parent,string value,float x,float y,float w,float h,Action action){var rt=Box(parent,"Button",x,y,w,h);var img=rt.gameObject.AddComponent<Image>();img.color=new Color(.27f,.24f,.19f);var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=img;b.onClick.AddListener(()=>action());var t=Label(rt,value,8,2,w-16,h-4);t.alignment=TextAnchor.MiddleLeft;return b;}
        void SetTab(int value){tab=value;selectedListing=selectedClaim=null;Refresh();}
        void Build()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");root=new GameObject("AuctionCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=351;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1000,700);scaler.matchWidthOrHeight=.5f;if(FindObjectOfType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));root.AddComponent<Image>().color=new Color(0,0,0,.75f);
            panel=Box(root.transform,"Auction",0,0,820,650);panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;panel.gameObject.AddComponent<Image>().color=new Color(.15f,.14f,.12f);
            header=Label(panel,"",20,15,670,72,18);Button(panel,"关闭",720,18,80,34,Close);Button(panel,"拍品",20,92,150,34,()=>SetTab(0));Button(panel,"我的寄拍",180,92,150,34,()=>SetTab(1));Button(panel,"待领取",340,92,150,34,()=>SetTab(2));
            var view=Box(panel,"Scroll",20,138,780,330);view.gameObject.AddComponent<Image>().color=new Color(.1f,.1f,.09f);view.gameObject.AddComponent<RectMask2D>();var scroll=view.gameObject.AddComponent<ScrollRect>();rows=Box(view,"Rows",0,0,780,330);rows.anchorMax=new Vector2(1,1);scroll.viewport=view;scroll.content=rows;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=26;
            detail=Label(panel,"",20,478,780,48);bidButton=Button(panel,"出价",20,535,130,36,Bid);Button(panel,"−",170,535,38,34,()=>{quantity=Math.Max(1,quantity-1);Refresh();});quantityText=Label(panel,"",218,540,90,28);Button(panel,"+",310,535,38,34,()=>{if(quantity<int.MaxValue)quantity++;Refresh();});
            var inputRt=Box(panel,"StartingPrice",360,535,150,36);inputRt.gameObject.AddComponent<Image>().color=new Color(.08f,.08f,.07f);startingPrice=inputRt.gameObject.AddComponent<InputField>();var inputText=Label(inputRt,"",8,2,134,32);inputText.alignment=TextAnchor.MiddleLeft;startingPrice.textComponent=inputText;startingPrice.contentType=InputField.ContentType.IntegerNumber;startingPrice.text="1";
            consignButton=Button(panel,"寄拍",520,535,130,36,Consign);claimButton=Button(panel,"领取",670,535,130,36,Claim);status=Label(panel,"",20,585,780,54);
        }
    }
}
