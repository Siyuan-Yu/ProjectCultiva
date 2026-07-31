using UnityEngine;
using XianXia.Unity.Cultivation;

namespace XianXia.Unity.Presentation
{
    /// <summary>
    /// 选中单位的指令路线预览：当前位置 → 目的地／追击目标。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DemoUnitController))]
    public sealed class UnitOrderPathPreview : MonoBehaviour
    {
        [SerializeField] private DemoUnitController unit;
        [SerializeField] private float lineWidth = 0.07f;

        private LineRenderer _line;
        private UnitCultivation _cultivation;
        private static Material _sharedMaterial;

        private void Awake()
        {
            if (unit == null)
            {
                unit = GetComponent<DemoUnitController>();
            }

            _cultivation = GetComponent<UnitCultivation>();
            EnsureLine();
        }

        private void LateUpdate()
        {
            if (unit == null || _line == null)
            {
                return;
            }

            // 入定中不画路线；未选中也不画，避免地图 clutter。
            if (!unit.IsSelected
                || (_cultivation != null && _cultivation.IsCultivating)
                || !TryGetPreviewEnd(out Vector3 end, out Color color))
            {
                _line.enabled = false;
                return;
            }

            Vector3 start = unit.transform.position;
            start.z = 0f;
            end.z = 0f;
            if ((end - start).sqrMagnitude < 0.04f)
            {
                _line.enabled = false;
                return;
            }

            _line.positionCount = 2;
            _line.SetPosition(0, start);
            _line.SetPosition(1, end);
            _line.startColor = color;
            _line.endColor = new Color(color.r, color.g, color.b, color.a * 0.4f);
            _line.enabled = true;
        }

        private bool TryGetPreviewEnd(out Vector3 end, out Color color)
        {
            if (unit.IsAttacking && unit.AttackTarget != null)
            {
                end = unit.AttackTarget.position;
                color = new Color(1f, 0.35f, 0.3f, 0.9f);
                return true;
            }

            if (unit.HasDestination)
            {
                end = unit.CurrentDestination;
                if (unit.IsWorking || unit.AssignedWorkSpot != null)
                {
                    color = new Color(1f, 0.82f, 0.28f, 0.9f);
                }
                else
                {
                    color = new Color(0.45f, 0.95f, 0.55f, 0.85f);
                }

                return true;
            }

            end = default;
            color = default;
            return false;
        }

        private void EnsureLine()
        {
            _line = GetComponent<LineRenderer>();
            if (_line == null)
            {
                _line = gameObject.AddComponent<LineRenderer>();
            }

            if (_sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                _sharedMaterial = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            }

            _line.sharedMaterial = _sharedMaterial;
            _line.textureMode = LineTextureMode.Stretch;
            _line.alignment = LineAlignment.View;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
            _line.useWorldSpace = true;
            _line.sortingOrder = 5100;
            _line.widthMultiplier = lineWidth;
            _line.startWidth = lineWidth;
            _line.endWidth = lineWidth * 0.55f;
            _line.enabled = false;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
        }
    }
}
