using UnityEngine;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 世界人物（NPC）点选信息。只读展示，不可下达指令。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldCharacterInspectable : MonoBehaviour
    {
        [SerializeField] private string displayName = "村民";
        [SerializeField] private string roleTitle = "村民";
        [SerializeField] private string realm = "凡人";
        [SerializeField] private string statusNote = "按课表生活";
        [SerializeField] private float threatLevel;

        public string DisplayName => displayName;
        public string RoleTitle => roleTitle;
        public string Realm => realm;
        public string StatusNote => statusNote;
        public float ThreatLevel => threatLevel;

        public void Configure(
            string name,
            string role,
            string cultivationRealm,
            string note,
            float threat = 0f)
        {
            displayName = name;
            roleTitle = role;
            realm = cultivationRealm;
            statusNote = note;
            threatLevel = Mathf.Clamp01(threat);
        }
    }
}
