using UnityEngine;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 在世界坐标上绘制工作区／灵地名称与简易边界，方便找区域。
    /// </summary>
    public sealed class ZoneMapLabelOverlay : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool showWorkZones = true;
        // 灵地由 SpiritSiteMapMarker 负责菱形与标签，此处默认只画工作区边框。
        [SerializeField] private bool showSpiritSites = false;

        private GUIStyle _labelStyle;
        private GUIStyle _spiritStyle;
        private Texture2D _pixel;

        public void Configure(Camera camera)
        {
            worldCamera = camera;
        }

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void OnGUI()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null)
                {
                    return;
                }
            }

            EnsureStyles();

            if (showWorkZones)
            {
                WorkZone[] zones = FindObjectsOfType<WorkZone>();
                for (int i = 0; i < zones.Length; i++)
                {
                    WorkZone zone = zones[i];
                    if (zone == null)
                    {
                        continue;
                    }

                    DrawZoneFrame(zone.Bounds, new Color(0.95f, 0.7f, 0.25f, 0.55f), 2f);
                    DrawWorldLabel(zone.Bounds.center, zone.DisplayName, _labelStyle);
                }
            }

            if (showSpiritSites)
            {
                SpiritSiteZone[] sites = FindObjectsOfType<SpiritSiteZone>();
                for (int i = 0; i < sites.Length; i++)
                {
                    SpiritSiteZone site = sites[i];
                    if (site == null)
                    {
                        continue;
                    }

                    DrawZoneFrame(site.Bounds, new Color(0.25f, 0.95f, 0.9f, 0.85f), 3f);
                    DrawWorldLabel(site.Bounds.center, $"★ {site.DisplayName}", _spiritStyle);
                }
            }
        }

        private void DrawWorldLabel(Vector3 worldCenter, string text, GUIStyle style)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(worldCenter);
            if (screen.z < 0f)
            {
                return;
            }

            Vector2 size = style.CalcSize(new GUIContent(text));
            float x = screen.x - size.x * 0.5f;
            float y = Screen.height - screen.y - size.y * 0.5f - 12f;
            Rect rect = new(x - 6f, y - 4f, size.x + 12f, size.y + 8f);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
            GUI.Label(new Rect(x, y, size.x, size.y), text, style);
        }

        private void DrawZoneFrame(Bounds bounds, Color color, float thickness)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new(min.x, min.y, 0f),
                new(max.x, min.y, 0f),
                new(max.x, max.y, 0f),
                new(min.x, max.y, 0f)
            };

            Vector2[] screenCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 screen = worldCamera.WorldToScreenPoint(corners[i]);
                if (screen.z < 0f)
                {
                    return;
                }

                screenCorners[i] = new Vector2(screen.x, Screen.height - screen.y);
            }

            Color previous = GUI.color;
            GUI.color = color;
            DrawLine(screenCorners[0], screenCorners[1], thickness);
            DrawLine(screenCorners[1], screenCorners[2], thickness);
            DrawLine(screenCorners[2], screenCorners[3], thickness);
            DrawLine(screenCorners[3], screenCorners[0], thickness);
            GUI.color = previous;
        }

        private void DrawLine(Vector2 a, Vector2 b, float thickness)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.5f)
            {
                return;
            }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), _pixel);
            GUI.matrix = previous;
        }

        private void EnsureStyles()
        {
            if (_pixel == null)
            {
                _pixel = Texture2D.whiteTexture;
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.92f, 0.7f) }
                };
            }

            if (_spiritStyle == null)
            {
                _spiritStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.55f, 1f, 0.95f) }
                };
            }
        }
    }
}
