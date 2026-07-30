using UnityEngine;

namespace XianXia.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ReplaceableSprite : MonoBehaviour
    {
        [SerializeField] private string spriteId;
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite sprite;

        public string SpriteId => spriteId;
        public Sprite Sprite => sprite;

        private void Awake()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Apply();
        }
#endif

        public void Configure(string id, SpriteRenderer renderer, Sprite value)
        {
            spriteId = id;
            targetRenderer = renderer;
            sprite = value;
            Apply();
        }

        public void SetSprite(Sprite value)
        {
            sprite = value;
            Apply();
        }

        private void Apply()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (targetRenderer != null)
            {
                targetRenderer.sprite = sprite;
            }
        }
    }
}
