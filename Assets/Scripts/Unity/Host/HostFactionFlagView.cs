using UnityEngine;

namespace XianXia.Unity.Host
{
    public sealed class HostFactionFlagView : MonoBehaviour
    {
        public string FlagId { get; private set; } = string.Empty;
        public void Bind(string flagId) => FlagId = flagId ?? string.Empty;
    }
}
