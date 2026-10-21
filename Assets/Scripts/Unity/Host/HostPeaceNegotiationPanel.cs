using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Normal faction diplomacy entry; each edit invalidates the previous counteroffer.</summary>
    public sealed class HostPeaceNegotiationPanel
    {
        string _message = string.Empty;
        PeaceNegotiation _amountOffer;
        string _amountText = "0";
        int _amountValue;
        public float Draw(float width, float y, SimulationWorld world, string other, GUIStyle style, PlayableHostBootstrap host)
        {
            var player = world.Strategic.PlayerFactionId;
            if (player == other || string.IsNullOrEmpty(player)) return y;
            GUI.Label(new Rect(0, y, width - 22, 24), "停战谈判（赔偿／释放俘虏／附庸）", style); y += 26;
            if (!WarGateService.IsAtWar(world, player, other))
            {
                var message = "双方没有战争。";
                foreach (var saved in world.Strategic.PeaceNegotiations)
                    if (saved.OtherFaction == other && saved.Status == PeaceOfferStatus.Accepted)
                        message = saved.Message + "\n第 " + saved.Round + " 轮成交；赔偿 " + saved.Amount + " " + saved.ResourceId +
                            "；释放 " + saved.ReleasePrisoners.Count + " 人；附庸 " + (saved.Vassalage ? "是" : "否");
                GUI.Label(new Rect(0, y, width - 22, 86), message, style);
                return y + 88;
            }
            PeaceNegotiation offer = null;
            foreach (var entry in world.Strategic.PeaceNegotiations)
                if (entry.OtherFaction == other && entry.Status != PeaceOfferStatus.Accepted && entry.Status != PeaceOfferStatus.Cancelled)
                    offer = entry;
            if (offer == null)
            {
                if (GUI.Button(new Rect(0, y, width - 24, 24), "开始停战谈判"))
                { PeaceNegotiationService.Open(world, other); _message = string.Empty; }
                return y + 30;
            }
            var ownSites = Sites(world, player); var otherSites = Sites(world, other);
            y = SiteSelector(width, y, "我方交付地点", ownSites, ref offer.PlayerSite, offer);
            y = SiteSelector(width, y, "对方交付地点", otherSites, ref offer.OtherSite, offer);
            var resources = new List<string>();
            CollectResources(world, offer.PlayerSite, resources); CollectResources(world, offer.OtherSite, resources);
            resources.Sort(StringComparer.Ordinal);
            if (resources.Count > 0)
            {
                if (!resources.Contains(offer.ResourceId)) { offer.ResourceId = resources[0]; Changed(offer); }
                if (GUI.Button(new Rect(0, y, width - 24, 24), "赔偿资源：" + (world.InventoryCatalog.TryGet(offer.ResourceId, out var info) ? info.Name : offer.ResourceId)))
                { offer.ResourceId = resources[(resources.IndexOf(offer.ResourceId) + 1) % resources.Count]; Changed(offer); }
                y += 27;
            }
            GUI.Label(new Rect(0, y, width - 24, 42), "数量：正数对方赔我，负数我方支付，0为不赔偿\n只使用所选地点真实公库库存。", style); y += 44;
            if (!ReferenceEquals(_amountOffer, offer) || _amountValue != offer.Amount)
            { _amountOffer = offer; _amountValue = offer.Amount; _amountText = offer.Amount.ToString(); }
            var text = GUI.TextField(new Rect(0, y, width - 24, 24), _amountText);
            _amountText = text;
            var amountValid = int.TryParse(text, out var amount) && amount != int.MinValue;
            if (amountValid && amount != offer.Amount) { offer.Amount = amount; _amountValue = amount; Changed(offer); }
            y += 27;
            GUI.Label(new Rect(0, y, width - 24, 22), "我方库存 " + Stock(world, offer.PlayerSite, offer.ResourceId) +
                " / 对方库存 " + Stock(world, offer.OtherSite, offer.ResourceId), style); y += 24;
            var vassal = GUI.Toggle(new Rect(0, y, width - 24, 24), offer.Vassalage, "要求对方势力成为我方附庸");
            if (vassal != offer.Vassalage) { offer.Vassalage = vassal; Changed(offer); }
            y += 28;
            GUI.Label(new Rect(0, y, width - 24, 42), "释放以下俘虏（不招募、不交易自由人物、不押送）：", style); y += 44;
            var people = new List<ulong>();
            foreach (var e in world.Entities.All) people.Add(e.Id.Value);
            people.Sort();
            foreach (var id in people)
            {
                var faction = PeaceNegotiationService.CustodianFaction(world, new EntityId(id));
                if (faction != player && faction != other) continue;
                if (!world.Entities.TryGet(new EntityId(id), out var entity)) continue;
                var selected = offer.ReleasePrisoners.Contains(id);
                var next = GUI.Toggle(new Rect(0, y, width - 24, 24), selected,
                    (faction == player ? "我方释放：" : "要求对方释放：") + entity.DisplayName);
                if (next != selected)
                { if (next) offer.ReleasePrisoners.Add(id); else offer.ReleasePrisoners.Remove(id); Changed(offer); }
                y += 27;
            }
            GUI.Label(new Rect(0, y, width - 24, 62), "第 " + offer.Round + " 轮 · " + offer.Message +
                "\n成交将结束双方所在战争及其绑定参战方，不改变地点所有权。", style); y += 64;
            var previous = GUI.enabled;
            GUI.enabled = previous && amountValid;
            if (GUI.Button(new Rect(0, y, (width - 28) * .5f, 26), offer.Round == 0 ? "提出条件" : "修改条件再谈"))
            {
                var result = PeaceNegotiationService.Propose(world, offer);
                _message = result.IsSuccess ? offer.Message : result.Error.Message;
                if (result.IsSuccess && offer.Status == PeaceOfferStatus.Accepted)
                    host?.ContinuousOutdoorSurfaceRuntime?.SynchronizeDomainRelocations(offer.ReleasePrisoners);
            }
            GUI.enabled = previous && amountValid && offer.Status == PeaceOfferStatus.CounterOffer;
            if (GUI.Button(new Rect((width - 28) * .5f + 4, y, (width - 28) * .5f, 26), "接受对方还价"))
            {
                var result = PeaceNegotiationService.AcceptCounter(world, offer);
                _message = result.IsSuccess ? offer.Message : result.Error.Message;
                if (result.IsSuccess)
                    host?.ContinuousOutdoorSurfaceRuntime?.SynchronizeDomainRelocations(offer.ReleasePrisoners);
            }
            GUI.enabled = previous; y += 30;
            if (GUI.Button(new Rect(0, y, width - 24, 24), "取消本次谈判（战争继续）"))
            { offer.Status = PeaceOfferStatus.Cancelled; _message = "谈判取消，未交付任何条款。"; }
            y += 28;
            GUI.Label(new Rect(0, y, width - 24, 62), _message, style);
            return y + 66;
        }

        static int Stock(SimulationWorld world, string site, string resource) =>
            WorldSitePublicStockService.GetCount(world, site, resource);
        static void Changed(PeaceNegotiation offer) { offer.Status = PeaceOfferStatus.Draft; offer.Message = "条件已修改，等待提交。"; }
        static List<string> Sites(SimulationWorld world, string faction)
        {
            var result = new List<string>();
            foreach (var site in world.Strategic.Sites.Sites.Values)
                if (site.IsCoreActive && site.OwnerFactionId == faction) result.Add(site.SiteId);
            result.Sort(StringComparer.Ordinal); return result;
        }
        static float SiteSelector(float width, float y, string label, List<string> sites, ref string selected, PeaceNegotiation offer)
        {
            if (sites.Count == 0)
            { GUI.Label(new Rect(0, y, width - 24, 24), label + "：无（只可无资源谈判）"); return y + 27; }
            if (!sites.Contains(selected)) { selected = sites[0]; Changed(offer); }
            if (GUI.Button(new Rect(0, y, width - 24, 24), label + "：" + selected))
            { selected = sites[(sites.IndexOf(selected) + 1) % sites.Count]; Changed(offer); }
            return y + 27;
        }
        static void CollectResources(SimulationWorld world, string site, List<string> result)
        {
            if (string.IsNullOrEmpty(site) || !world.Strategic.SitePublicStocks.TryGet(site, out var stock)) return;
            foreach (var pair in stock.Resources) if (pair.Value > 0 && !result.Contains(pair.Key)) result.Add(pair.Key);
        }
    }
}
