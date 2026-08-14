using System.Collections.Generic;

namespace XianXia.Unity.Host
{
    public sealed class HostDialogueChoiceLine
    {
        public string ChoiceId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>View-model for NPC dialogue (onTalk ContentEvent or fallback).</summary>
    public sealed class HostDialogueModel
    {
        public bool IsActive { get; set; }
        public bool IsFallback { get; set; }
        public string SpeakerName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        /// <summary>立绘资源路径；空则显示占位框。美术接入后由 Controller 填充。</summary>
        public string PortraitResourceId { get; set; } = string.Empty;
        public List<HostDialogueChoiceLine> Choices { get; } = new List<HostDialogueChoiceLine>(4);
    }
}
