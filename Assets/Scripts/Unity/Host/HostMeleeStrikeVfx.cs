using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 通用近战命中特效（程序化挥砍弧＋受击闪白）。暂作全员共用，后续可按武器／境界换皮。
    /// </summary>
    public sealed class HostMeleeStrikeVfx : MonoBehaviour
    {
        [SerializeField] float lifetime = 0.28f;
        [SerializeField] float hitFlashSeconds = 0.12f;
        [SerializeField] int poolSize = 12;

        struct Slash
        {
            public Transform Root;
            public SpriteRenderer Renderer;
            public float DieAt;
            public float BornAt;
            public Vector3 From;
            public Vector3 To;
        }

        readonly List<Slash> _live = new List<Slash>(16);
        readonly Stack<Slash> _pool = new Stack<Slash>(16);
        Transform _root;
        Sprite _slashSprite;

        public void PlayBetween(
            EntityViewSpawner spawner,
            EntityId attacker,
            EntityId defender)
        {
            if (spawner == null ||
                !spawner.Registry.TryGet(attacker, out var aView) || aView == null ||
                !spawner.Registry.TryGet(defender, out var dView) || dView == null)
                return;

            Play(aView.transform.position, dView.transform.position);
            dView.PlayHitFlash(hitFlashSeconds);
        }

        public void Play(Vector3 from, Vector3 to)
        {
            EnsureRoot();
            var slash = Rent();
            slash.BornAt = Time.unscaledTime;
            slash.DieAt = slash.BornAt + lifetime;
            slash.From = from;
            slash.To = to;
            Place(slash);
            _live.Add(slash);
        }

        void LateUpdate()
        {
            if (_live.Count == 0)
                return;

            var now = Time.unscaledTime;
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                if (s.Root == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                var t = Mathf.Clamp01((now - s.BornAt) / Mathf.Max(0.01f, lifetime));
                Animate(s, t);
                if (now >= s.DieAt)
                {
                    Return(s);
                    _live.RemoveAt(i);
                }
            }
        }

        void Animate(Slash s, float t)
        {
            // 前 35%：弧迅速张开；后段淡出并略回缩
            var expand = t < 0.35f ? t / 0.35f : 1f;
            var fade = t < 0.45f ? 1f : 1f - (t - 0.45f) / 0.55f;
            var mid = Vector3.Lerp(s.From, s.To, 0.55f);
            mid.z = HostPresentationSpace.EntityZ - 0.02f;
            var dir = s.To - s.From;
            dir.z = 0f;
            var len = Mathf.Max(0.55f, dir.magnitude * 0.85f);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.right;
            else
                dir.Normalize();

            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // 斜向挥砍：相对连线再偏一点
            angle += Mathf.Lerp(-38f, 28f, expand);
            s.Root.position = mid;
            s.Root.rotation = Quaternion.Euler(0f, 0f, angle);
            var scaleX = len * Mathf.Lerp(0.35f, 1.05f, expand);
            var scaleY = Mathf.Lerp(0.45f, 1.15f, expand) * 0.9f;
            s.Root.localScale = new Vector3(scaleX, scaleY, 1f);

            if (s.Renderer != null)
            {
                var c = s.Renderer.color;
                c.a = Mathf.Clamp01(fade) * 0.92f;
                s.Renderer.color = c;
            }
        }

        void Place(Slash s)
        {
            if (s.Root == null)
                return;
            s.Root.gameObject.SetActive(true);
            if (s.Renderer != null)
            {
                s.Renderer.sprite = SlashSprite();
                s.Renderer.color = new Color(1f, 0.92f, 0.72f, 0.9f);
                s.Renderer.sortingOrder = 1200;
            }

            Animate(s, 0f);
        }

        Slash Rent()
        {
            if (_pool.Count > 0)
                return _pool.Pop();
            return CreateSlash();
        }

        void Return(Slash s)
        {
            if (s.Root == null)
                return;
            s.Root.gameObject.SetActive(false);
            _pool.Push(s);
        }

        Slash CreateSlash()
        {
            EnsureRoot();
            var go = new GameObject("MeleeSlash");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SlashSprite();
            sr.color = new Color(1f, 0.92f, 0.72f, 0.9f);
            sr.sortingOrder = 1200;
            go.SetActive(false);
            return new Slash { Root = go.transform, Renderer = sr };
        }

        void EnsureRoot()
        {
            if (_root != null)
                return;
            var go = new GameObject("HostMeleeStrikeVfx");
            go.transform.SetParent(transform, false);
            _root = go.transform;

            for (var i = 0; i < poolSize; i++)
                _pool.Push(CreateSlash());
        }

        Sprite SlashSprite()
        {
            if (_slashSprite != null)
                return _slashSprite;
            return _slashSprite = HostSpriteFactory.MeleeSlashSprite();
        }
    }
}
