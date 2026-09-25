using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Inventory;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>Formal UGUI shop. Commands delegate to Core; no wallet arithmetic in presentation.</summary>
    public sealed class HostShopTradePanel : MonoBehaviour
    {
        const string PauseOwner = "ShopTrade";
        PlayableHostBootstrap host;
        PlayableHostSession pausedSession;
        SimulationWorld openedWorld;
        EntityId actor, provider;
        string shopId, selectedBuy, selectedSell;
        int quantity = 1;
        GameObject root;
        RectTransform panel, left, right;
        Text header, status, quantityText, buyInfo, sellInfo;
        Font font;
        public bool IsOpen => root != null && root.activeSelf;
        public void Bind(PlayableHostBootstrap bootstrap) => host = bootstrap;
        public bool TryOpen(EntityId acting, EntityId target)
        {
            if(host?.Session == null || !host.Session.IsInitialized || IsOpen || host.Session.ModalHardPaused ||
                acting != HostNpcInteraction.ResolveActiveCommandAuthority(host.Session) ||
                HostNpcInteraction.IsHostileNpc(host.Session,target) || !Near(acting,target) ||
                !host.Session.World.Commerce.TryGetProvider(host.Session.World,target,out var id)) return false;
            host.InventoryPanel?.Close(); host.WorldMapPanel?.Close(); host.ConstructionPanel?.Close();
            actor = acting; provider = target; shopId = id; quantity = 1; selectedBuy = selectedSell = null;
            if(root == null) Build();
            pausedSession = host.Session; openedWorld = pausedSession.World;
            pausedSession.AcquireModalPause(PauseOwner);
            HostInputGate.Acquire(PauseOwner);
            host.GetComponent<HostLevelTesterCheatPanel>()?.Hide();
            root.SetActive(true); status.text = "同档灵石结算，不自动兑换。出售仅使用随身背包。";
            Refresh(); Block(); return true;
        }
        bool Near(EntityId a, EntityId b)
        {
            if(host?.ViewSpawner == null || !host.ViewSpawner.Registry.TryGet(a,out var av) || av == null ||
                !host.ViewSpawner.Registry.TryGet(b,out var bv) || bv == null) return false;
            var d = av.transform.position - bv.transform.position; d.z = 0;
            return d.sqrMagnitude <= HostNpcInteraction.DefaultMeleeEngageRange * HostNpcInteraction.DefaultMeleeEngageRange;
        }
        void Block() { HostInputGate.BlockWorldInteraction = true; HostInputGate.BlockWorldCamera = true; }
        void Update()
        {
            if(!IsOpen) return;
            if(host?.Session != pausedSession || host.Session.World != openedWorld || Input.GetKeyDown(KeyCode.Escape) ||
                host.InventoryPanel.IsOpen || host.WorldMapPanel.IsOpen || host.ConstructionPanel.IsOpen ||
                (host.QuestJournal != null && host.QuestJournal.IsOpen) ||
                (host.CharacterSheetPanel != null && host.CharacterSheetPanel.IsOpen) ||
                (host.CultivationPanel != null && host.CultivationPanel.IsOpen) ||
                (host.GetComponent<HostLevelTesterCheatPanel>()?.IsVisible == true)) { Close(); return; }
            Block();
        }
        public void Close()
        {
            if(root != null) root.SetActive(false);
            pausedSession?.ReleaseModalPause(PauseOwner); pausedSession = null; openedWorld = null;
            host?.GetComponent<HostMoveController>()?.ReleaseNpcForInteraction(provider);
            HostInputGate.Release(PauseOwner);
            HostInputGate.Clear();
        }
        void OnDisable() { if(IsOpen || pausedSession != null) Close(); }
        void OnDestroy() { if(root != null) Destroy(root); }
        void Trade(bool buy)
        {
            if(!IsOpen || openedWorld != host.Session.World || actor != HostNpcInteraction.ResolveActiveCommandAuthority(host.Session) ||
                !Near(actor,provider) || HostNpcInteraction.IsHostileNpc(host.Session,provider)) { Close(); return; }
            ShopTradeService.TryTrade(openedWorld,provider,shopId,buy ? selectedBuy : selectedSell,quantity,buy,out var message);
            status.text = message; Refresh();
        }
        string Wallet(SpiritStoneWallet w) => "下品 " + w.Low + "   中品 " + w.Mid + "   上品 " + w.High;
        string Price(TradePrice p) => p == null ? "不可交易" : p.Amount + " " + ShopTradeService.GradeName(p.Grade);
        void Refresh()
        {
            var board = openedWorld.Commerce; var shop = board.Shops[shopId]; var def = board.Definitions[shopId];
            header.text = def.DisplayName + "\n玩家：" + Wallet(board.PlayerWallet) + "\n商店：" + Wallet(shop.Wallet);
            quantityText.text = "数量：" + quantity;
            ClearRows(left); ClearRows(right);
            float y = 0;
            foreach(var e in def.InitialStock)
            {
                if(!openedWorld.InventoryCatalog.TryGet(e.ItemId,out var item)) continue;
                var id = e.ItemId; var n = shop.Stock[id];
                Button(left,(selectedBuy == id ? "▶ " : "") + item.Name + "   ×" + n + (n == 0 ? "（售罄）" : "") + "\n单价 " + Price(ShopTradeService.SalePrice(def,item)),0,y,344,54,()=>{selectedBuy=id;Refresh();}); y += 58;
            }
            left.sizeDelta = new Vector2(0,y);
            y = 0; var seen = new HashSet<string>();
            foreach(var slot in openedWorld.Inventory.Slots)
            {
                if(slot.IsEmpty || !seen.Add(slot.ItemId) || !openedWorld.InventoryCatalog.TryGet(slot.ItemId,out var item)) continue;
                var id = slot.ItemId;
                var refusal = !ShopTradeService.Tradable(item) ? "不可交易" : !def.AcceptedTradeCategories.Contains(item.TradeCategory) ? "不收购此类别" : "收购价 " + Price(ShopTradeService.BuybackPrice(def,item));
                Button(right,(selectedSell == id ? "▶ " : "") + item.Name + "   ×" + openedWorld.Inventory.GetCount(id) + "\n" + refusal,0,y,344,54,()=>{selectedSell=id;Refresh();}); y += 58;
            }
            right.sizeDelta = new Vector2(0,y);
            buyInfo.text = Detail(selectedBuy,true); sellInfo.text = Detail(selectedSell,false);
        }
        string Detail(string id, bool buy)
        {
            if(string.IsNullOrEmpty(id) || !openedWorld.InventoryCatalog.TryGet(id,out var item)) return buy ? "选择商店商品" : "选择随身物品";
            var board=openedWorld.Commerce; var def=board.Definitions[shopId]; var p=buy ? ShopTradeService.SalePrice(def,item) : ShopTradeService.BuybackPrice(def,item);
            return item.Name + " · " + Price(p) + (p == null ? "" : "\n玩家同档余额 " + board.PlayerWallet.Balance(p.Grade));
        }
        static void ClearRows(RectTransform parent)
        { for(int i=parent.childCount-1;i>=0;i--) { var child=parent.GetChild(i).gameObject;child.SetActive(false);Destroy(child); } }
        RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
        {
            var go = new GameObject(name,typeof(RectTransform)); var rt=go.GetComponent<RectTransform>(); rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        Text Label(Transform parent,string value,float x,float y,float w,float h,int size=16)
        {
            var rt=Box(parent,"Label",x,y,w,h);var t=rt.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=new Color(.95f,.9f,.8f);t.text=value;t.raycastTarget=false;return t;
        }
        void Button(Transform parent,string value,float x,float y,float w,float h,Action action)
        {
            var rt=Box(parent,"Button",x,y,w,h);var img=rt.gameObject.AddComponent<Image>();img.color=new Color(.27f,.24f,.19f);
            var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=img;b.onClick.AddListener(()=>action());
            var t=Label(rt,value,8,2,w-16,h-4);t.alignment=TextAnchor.MiddleLeft;
        }
        RectTransform List(Transform parent,float x)
        {
            var view=Box(parent,"Scroll",x,150,350,290);var img=view.gameObject.AddComponent<Image>();img.color=new Color(.10f,.10f,.09f);
            view.gameObject.AddComponent<RectMask2D>();var scroll=view.gameObject.AddComponent<ScrollRect>();
            var content=Box(view,"Rows",0,0,350,290);content.anchorMax=new Vector2(1,1);content.sizeDelta=new Vector2(0,290);
            scroll.viewport=view;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=26;return content;
        }
        void Build()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root=new GameObject("ShopTradeCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=350;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1000,700);scaler.matchWidthOrHeight=.5f;
            if(FindObjectOfType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            var shade=root.AddComponent<Image>();shade.color=new Color(0,0,0,.75f);
            panel=Box(root.transform,"Shop",0,0,780,620);panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
            panel.gameObject.AddComponent<Image>().color=new Color(.15f,.14f,.12f);
            header=Label(panel,"",20,16,640,95,18);Button(panel,"关闭",680,18,80,34,Close);
            Label(panel,"商店出售",20,122,340,26,18);Label(panel,"玩家随身背包",410,122,340,26,18);
            left=List(panel,20);right=List(panel,410);
            buyInfo=Label(panel,"",20,450,350,48);sellInfo=Label(panel,"",410,450,350,48);
            Button(panel,"−",280,505,42,32,()=>{quantity=Math.Max(1,quantity-1);Refresh();});quantityText=Label(panel,"",330,509,120,28);
            Button(panel,"+",450,505,42,32,()=>{if(quantity<int.MaxValue)quantity++;Refresh();});
            Button(panel,"购买",20,505,170,38,()=>Trade(true));Button(panel,"出售",570,505,170,38,()=>Trade(false));
            status=Label(panel,"",20,554,735,55);
        }
    }
}
