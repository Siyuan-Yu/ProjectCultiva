using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>把内部关系原因转换为人物故事文案；永不暴露 reasonTag 或数值 delta。</summary>
    public static class HostSocialHistoryPresentation
    {
        public static string Format(SimulationWorld world, EntityId subject, RelationshipEvent evt)
        {
            if (evt == null)
                return "发生了一段人际经历";

            var peer = evt.From == subject ? evt.To : evt.From;
            var peerName = HostCharacterRelationUi.ResolveDisplayName(world, peer);
            var tag = evt.ReasonTag ?? string.Empty;
            switch (tag)
            {
                case "opening_companion":
                    return "与" + peerName + "成为同行伙伴";
                case "opening_acquaintance":
                    return "与" + peerName + "相识";
                case "help":
                case "character_helped":
                case "character_helped_reaction":
                    return evt.From == subject
                        ? "受到" + peerName + "的帮助"
                        : "帮助了" + peerName;
                case "slight":
                    return "与" + peerName + "发生不快";
                case "character_attacked":
                case "character_attacked_reaction":
                    return evt.From == subject
                        ? "遭到" + peerName + "攻击"
                        : "攻击了" + peerName;
                case "character_rescued":
                case "character_rescued_reaction":
                    return evt.From == subject
                        ? "受到" + peerName + "的救助"
                        : "救助了" + peerName;
                case "character_killed_reaction":
                    var victim = evt.ContextEntityId.HasValue
                        ? HostCharacterRelationUi.ResolveDisplayName(world, evt.ContextEntityId.Value)
                        : "某人";
                    if (evt.ContextEntityId.HasValue && evt.ContextEntityId.Value == subject)
                    {
                        var reactor = HostCharacterRelationUi.ResolveDisplayName(world, evt.From);
                        var killer = HostCharacterRelationUi.ResolveDisplayName(world, evt.To);
                        return "其死亡影响了" + reactor + "对" + killer + "的态度";
                    }
                    var worsened = evt.Axis == SocialAttitudeAxis.Grudge
                        ? evt.Delta > 0
                        : evt.Delta < 0;
                    if (evt.From == subject)
                        return "因" + victim + "之死，对" + peerName + "的态度" +
                               (worsened ? "恶化" : "改善");
                    var affected = HostCharacterRelationUi.ResolveDisplayName(world, evt.From);
                    return affected + "因" + victim + "之死，对其态度" +
                           (worsened ? "恶化" : "改善");
                default:
                    return "发生了一段人际经历";
            }
        }
    }
}
