using System;
using System.Collections.Generic;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester 的稳定、显式功能目录。目录项只负责导航，不执行任何业务。</summary>
    public static class LevelTesterToolCatalog
    {
        public sealed class Entry
        {
            public Entry(string id, string title, string group, string keywords, string description)
            {
                Id = id; Title = title; Group = group; GroupKey = ResolveGroupKey(group);
                Keywords = keywords; Description = description;
            }

            public string Id { get; }
            public string Title { get; }
            public string Group { get; }
            public string GroupKey { get; }
            public string Keywords { get; }
            public string Description { get; }

            public bool Matches(string query)
            {
                if (string.IsNullOrWhiteSpace(query)) return true;
                return (Id + " " + Title + " " + Group + " " + Keywords + " " + Description)
                    .IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public static readonly IReadOnlyList<Entry> All = new[]
        {
            E("civilian.satiety", "设置饱食", "人物与需求", "饥饿 凡人 80 10", "修改所选受管凡人的饱食预设。"),
            E("civilian.energy", "设置精力", "人物与需求", "睡眠 疲劳 80 30 5", "修改所选受管凡人的精力预设。"),
            E("civilian.detained-loyalty", "设置囚犯忠诚", "人物与需求", "Detained 忠诚 10", "仅把所选 Detained 凡人的忠诚设为 10。"),
            E("combat.incapacitate-selected", "让选中人物进入弥留", "人物与战斗", "伤员 Incapacitated", "使唯一选中人物进入弥留状态。"),
            E("combat.party-attack", "增加我方全员攻击", "人物与战斗", "Party 攻击 +10", "仅修改 PlayerParty 成员。"),
            E("combat.party-max-hp", "增加我方全员最大生命", "人物与战斗", "Party 最大生命 +50", "仅修改 PlayerParty 成员并回满生命。"),
            E("combat.party-refill", "恢复我方生命与灵力", "人物与战斗", "Party 回满", "恢复 PlayerParty 存活成员的生命与灵力。"),

            E("civilian.grain-add", "添加公库粗粮", "资源与场景准备", "公共库存 粗粮 20", "向当前验收 Site 公库加入粗粮。"),
            E("civilian.grain-clear", "清空公库粗粮", "资源与场景准备", "公共库存 无粮", "清空当前验收 Site 的粗粮。"),
            E("civilian.reset", "重置凡人验收场景", "资源与场景准备", "CIVILIAN Reset", "重置 CIVILIAN-LIFE-01 全局验收前置。"),

            E("focus.huangcun", "定位荒村", "定位", "Site", "把相机定位到荒村。"),
            E("focus.grain-field", "定位粮田", "定位", "劳役场", "把相机定位到粮田 WorkArea。"),
            E("focus.herb-field", "定位药田", "定位", "药田", "把相机定位到药田 WorkArea。"),
            E("focus.woodcut", "定位伐木区", "定位", "森林", "把相机定位到伐木 WorkArea。"),
            E("focus.medical", "定位医疗点", "定位", "恢复点 medical", "把相机定位到荒村医疗点。"),
            E("focus.prison", "定位囚室", "定位", "prisonDropOff", "把相机定位到荒村囚室。"),
            E("focus.injured", "定位选中伤员", "定位", "EntityId", "定位唯一选中的伤员。"),
            E("focus.shop", "定位验收商店", "定位", "SHOP-TRADE", "定位青石坊市验收商店。"),
            E("focus.auction", "定位验收拍卖行", "定位", "AUCTION", "定位青石坊市验收拍卖行。"),

            E("commerce.player-wallet", "设置玩家钱包", "商业", "灵石 Low Mid High", "设置玩家三档灵石钱包预设。"),
            E("commerce.shop-wallet", "设置商店资金", "商业", "商店 钱包", "设置验收商店资金，保持库存。"),
            E("commerce.shop-reset", "重置验收商店", "商业", "库存 资金 Reset", "恢复验收商店初始库存与资金。"),
            E("commerce.auction-items", "发放寄拍验收物品", "商业", "粗木 秘籍", "向玩家背包发放寄拍验收物品。"),
            E("commerce.auction-wallet", "设置拍卖验收钱包", "商业", "Auction 灵石", "设置拍卖验收用玩家钱包。"),
            E("commerce.auction-reset", "重置验收拍卖行", "商业", "Auction Reset", "恢复验收拍卖行初始拍品。"),

            E("quest.secret-a", "接取秘境验收任务 A", "社交与内容", "Quest cave", "接取秘境同行验收任务 A。"),
            E("quest.secret-b", "接取秘境验收任务 B", "社交与内容", "Quest cave_b", "接取秘境同行验收任务 B。"),
            E("quest.general", "接取普通对照任务", "社交与内容", "Quest general", "接取普通非秘境对照任务。"),
            E("social.relation", "设置社交好感及方向对照", "社交与内容", "关系 NPC Active 19 20 30", "设置 NPC→Active 或反向好感预设。"),
            E("content.flag", "设置或清除剧情 Flag", "社交与内容", "标记 story", "为明确目标设置或清除 Content Flag。"),
            E("content.force-event", "强制呈现事件", "社交与内容", "Event", "为明确目标强制呈现指定事件。"),
            E("content.merchants", "生成两名同模板行商", "社交与内容", "机会物体 merchant", "生成两个同模板临时行商实例。"),
            E("content.hidden-stele", "生成隐藏石碑", "社交与内容", "Hidden", "生成隐藏石碑验收物体。"),
            E("content.notice-package", "生成通知包裹", "社交与内容", "PublicNotice", "生成公开通知包裹。"),
            E("content.visible-herb", "生成可见灵草", "社交与内容", "WorldVisible", "生成世界可见灵草。"),
            E("content.clear-dynamic", "清理动态发现验收物体", "社交与内容", "DYNAMIC-DISCOVERY", "清理本组动态机会物体。"),

            E("world.background-travel", "后台人物前往据点", "世界与战斗", "Travel Site", "让指定后台人物前往指定世界地点。"),
            E("world.background-cancel", "取消后台人物旅行", "世界与战斗", "Cancel Travel", "取消指定人物的后台旅行。"),
            E("world.npc-squad-move", "所选 NPC 小队移动", "世界与战斗", "Squad 主控附近", "让所选 NPC 小队移动到主控附近测试点。"),
            E("diplomacy.war", "宣战", "世界与战斗", "势力 战争", "在两个明确势力之间宣战。"),
            E("diplomacy.alliance", "结盟", "世界与战斗", "势力 联盟", "在两个明确势力之间建立联盟。"),
            E("diplomacy.vassal", "建立附庸", "世界与战斗", "宗主 附庸", "建立明确宗主与附庸关系。"),
            E("succession.prepare", "准备继承候选", "世界与战斗", "SUCCESSION A B", "准备 A/B 继承候选。"),
            E("succession.kill-party", "使当前 Party 全灭", "世界与战斗", "SUCCESSION 死亡", "通过正式生命周期使当前 Party 全灭。"),
            E("control.incapacitate-party", "使当前 Party 全员弥留", "世界与战斗", "CONTROL-HANDOFF", "使当前 Party 全员进入弥留。"),
            E("encounter.intervene", "遭遇候选介入", "世界与战斗", "关系合格 介入", "查看并执行关系合格的遭遇候选介入。"),

            E("time.speed", "调整运行倍速", "时间与会话", "1x 2x 5x 20x", "调整正常 Host 帧循环速度。"),
            E("time.core-ticks", "Core Tick 步进与批量推进", "时间与会话", "Tick", "只推进 Core 数据，不证明 Host 移动。"),
            E("time.midnight", "推进至日界线", "时间与会话", "次日 第3日 第7日", "推进到指定日界线，仅 Core 数据。"),
            E("session.save", "保存测试存档", "时间与会话", "Snapshot", "保存当前 LevelTester 快照。"),
            E("session.load", "读取测试存档", "时间与会话", "Snapshot Restore", "读取测试快照并清理旧上下文。"),
            E("session.reset", "重置整个会话", "时间与会话", "Reset", "重建整个 LevelTester 会话。"),

            E("diagnostic.movement", "人物移动诊断", "诊断", "MovementIntent WalkGrid", "查看选中人物的移动 authority 与路径状态。"),
            E("diagnostic.character", "人物生命与势力诊断", "诊断", "Faction Loyalty Lifecycle Recruit", "查看选中人物的生命、势力、忠诚和交互状态。"),
            E("diagnostic.needs", "民生食宿诊断", "诊断", "Satiety Energy FoodFetch Sleep Flee", "查看选中凡人的需求、取食、睡眠和失所状态。"),
            E("diagnostic.rescue", "救援搬运诊断", "诊断", "RecoverySpot Rescue", "查看救援目标、恢复点和到达状态。"),
            E("diagnostic.prison", "囚犯押送与关押诊断", "诊断", "Carrier DropOff Detain", "查看押送、交接距离与关押合法性。"),
            E("diagnostic.construction", "凡人建设诊断", "诊断", "Construction Job Carrier Payload", "查看建设工单、载荷、材料和劳动进度。"),
            E("diagnostic.encounter", "据点遭遇诊断", "诊断", "CharacterEncounter Participant Target", "查看当前据点遭遇名单与目标。"),
            E("diagnostic.shop", "商店运行态", "诊断", "Shop Stock Wallet", "查看验收商店资金和有限库存。"),
            E("diagnostic.auction", "拍卖运行态", "诊断", "Auction Listing Escrow Claim", "查看验收拍卖行拍品、托管和待领取。"),
            E("diagnostic.quest", "任务与临时同行诊断", "诊断", "Quest Companion Relationship", "查看社交任务实例与临时同行绑定。"),
            E("diagnostic.diplomacy", "外交状态诊断", "诊断", "War Alliance Vassal", "查看当前战争、联盟和附庸状态。"),
            E("diagnostic.delayed-events", "延迟事件队列", "诊断", "Scheduled Event", "查看等待执行的 ContentEvent。"),
            E("diagnostic.content", "内容状态", "诊断", "Flag Dump Quest", "按当前明确目标查看内容状态。"),
            E("diagnostic.system", "性能与系统诊断", "诊断", "FPS authority system", "查看系统、场景和运行状态。"),
        };

        public static Entry Find(string id)
        {
            for (var i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        static string ResolveGroupKey(string group)
        {
            switch (group)
            {
                case "人物与需求": return "characters.needs";
                case "人物与战斗": return "characters.combat";
                case "资源与场景准备": return "setup.resources";
                case "定位": return "navigation.focus";
                case "商业": return "commerce";
                case "社交与内容": return "social.content";
                case "世界与战斗": return "world.combat";
                case "时间与会话": return "time.session";
                case "诊断": return "diagnostics";
                default: throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown LevelTester group.");
            }
        }

        static Entry E(string id, string title, string group, string keywords, string description) =>
            new Entry(id, title, group, keywords, description);
    }
}
