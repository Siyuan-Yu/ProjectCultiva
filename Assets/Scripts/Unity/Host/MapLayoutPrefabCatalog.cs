using System;
using UnityEngine;

namespace XianXia.Unity.Host
{
    public sealed class MapLayoutPrefabCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string path;
            public GameObject prefab;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        public GameObject Find(string path)
        {
            foreach (var entry in entries)
                if (entry != null && string.Equals(entry.path, path, StringComparison.Ordinal))
                    return entry.prefab;
            return null;
        }
    }
}
