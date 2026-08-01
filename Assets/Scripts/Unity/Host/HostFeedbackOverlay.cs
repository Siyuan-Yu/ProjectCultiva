using System.Collections.Generic;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>Demo-like floating text (presentation only).</summary>
    public sealed class HostFeedbackOverlay : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] float lifetime = 1.6f;

        struct Floater
        {
            public Vector3 World;
            public string Text;
            public float DieAt;
            public Color Color;
        }

        readonly List<Floater> _items = new List<Floater>();

        public void Bind(Camera cam)
        {
            worldCamera = cam != null ? cam : Camera.main;
        }

        public void SpawnAtEntity(EntityViewSpawner spawner, XianXia.Core.Domain.Ids.EntityId id, string text, Color color)
        {
            var pos = Vector3.zero;
            if (spawner != null && spawner.Registry.TryGet(id, out var view) && view != null)
                pos = view.transform.position + Vector3.up * 0.55f;
            Spawn(pos, text, color);
        }

        public void Spawn(Vector3 world, string text, Color color)
        {
            _items.Add(new Floater
            {
                World = world,
                Text = text ?? string.Empty,
                DieAt = Time.unscaledTime + lifetime,
                Color = color
            });
        }

        void OnGUI()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null || _items.Count == 0)
                return;
            var now = Time.unscaledTime;
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                var f = _items[i];
                if (now >= f.DieAt)
                {
                    _items.RemoveAt(i);
                    continue;
                }

                var lift = (lifetime - (f.DieAt - now)) * 20f;
                var screen = worldCamera.WorldToScreenPoint(f.World + Vector3.up * (lift * 0.01f));
                if (screen.z < 0f)
                    continue;
                var gui = new Rect(screen.x - 40f, Screen.height - screen.y - 12f, 80f, 24f);
                var old = GUI.color;
                GUI.color = f.Color;
                GUI.Label(gui, f.Text);
                GUI.color = old;
            }
        }
    }
}
