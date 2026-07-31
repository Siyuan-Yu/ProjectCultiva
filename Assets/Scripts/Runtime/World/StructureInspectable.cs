using UnityEngine;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 可点选查看的建筑占位信息。本阶段只做 Inspect，不下指令。
    /// </summary>
    public sealed class StructureInspectable : MonoBehaviour
    {
        [SerializeField] private string displayName = "建筑";
        [SerializeField] private string purpose = "占位建筑";
        [SerializeField] private string statusNote = "详情面板原型";

        public string DisplayName => displayName;
        public string Purpose => purpose;
        public string StatusNote => statusNote;

        public void Configure(string name, string purposeText, string note)
        {
            displayName = name;
            purpose = purposeText;
            statusNote = note;
        }
    }
}
