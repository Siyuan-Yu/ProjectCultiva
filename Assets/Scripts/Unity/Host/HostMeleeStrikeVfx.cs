using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 近战挥砍弧＋统一远程弹道（程序化）。纱衣普攻及日后所有远程暂共用弹道表现。
    /// </summary>
    public sealed class HostMeleeStrikeVfx : MonoBehaviour
    {
        [SerializeField] float lifetime = 0.28f;
        [SerializeField] float hitFlashSeconds = 0.12f;
        [SerializeField] float projectileSpeed = 8.5f;
        [SerializeField] float projectileMinSeconds = 0.22f;
        [SerializeField] float projectileMaxSeconds = 1.1f;
        [SerializeField] float projectileHitRadius = 0.42f;
        [SerializeField] float impactLifetime = 0.18f;
        [SerializeField] int poolSize = 16;

        enum FxKind
        {
            Melee = 0,
            Projectile = 1,
            Impact = 2
        }

        struct Fx
        {
            public Transform Root;
            public SpriteRenderer Renderer;
            public SpriteRenderer Trail;
            public float DieAt;
            public float BornAt;
            public Vector3 From;
            public Vector3 To;
            public Vector3 Direction;
            public float MaxRange;
            public float Traveled;
            public float Speed;
            public FxKind Kind;
            public EntityView HitTarget;
            public bool HitApplied;
            public Func<bool> ResolveHit;
            public Func<Vector3, Vector3, bool> Sweep;
        }

        readonly List<Fx> _live = new List<Fx>(24);
        readonly Stack<Fx> _pool = new Stack<Fx>(24);
        Transform _root;
        Sprite _slashSprite;
        Sprite _boltSprite;
        PlayableHostBootstrap _host;
        float _clock;

        void Awake() => _host = GetComponent<PlayableHostBootstrap>();

        public void PlayBetween(
            EntityViewSpawner spawner,
            EntityId attacker,
            EntityId defender)
        {
            PlayBetween(spawner, attacker, defender, ranged: false);
        }

        /// <summary>统一远程弹道：沿发射方向直线飞行，进入命中半径后结算并受击闪白。</summary>
        public bool PlayRangedBetween(
            EntityViewSpawner spawner,
            EntityId attacker,
            EntityId defender,
            Func<bool> resolveHit = null)
        {
            return PlayBetween(spawner, attacker, defender, ranged: true,
                resolveHit: resolveHit);
        }

        /// <summary>Current active arts are unarmed fist/claw; a held weapon must not select their gesture.</summary>
        public void PlaySkillBetween(EntityViewSpawner spawner, EntityId attacker, EntityId defender) =>
            PlayBetween(spawner, attacker, defender, ranged: false, styleOverride: HumanAttackStyle.Unarmed);

        bool PlayBetween(
            EntityViewSpawner spawner,
            EntityId attacker,
            EntityId defender,
            bool ranged, HumanAttackStyle? styleOverride = null,
            Func<bool> resolveHit = null)
        {
            if (spawner == null ||
                !spawner.Registry.TryGet(attacker, out var aView) || aView == null ||
                !spawner.Registry.TryGet(defender, out var dView) || dView == null)
                return false;

            aView.PlayArtAttack(dView.transform.position, ranged, styleOverride);
            if (ranged)
                PlayProjectile(aView.CombatVisualPoint,
                    dView.CombatVisualPoint, dView, resolveHit);
            else
            {
                Play(aView.transform.position, dView.transform.position);
                dView.PlayHitFlash(hitFlashSeconds);
                HostAudioFeedback.Play(gameObject, HostAudioFeedback.Cue.Hit);
            }
            return true;
        }

        public void Play(Vector3 from, Vector3 to) =>
            SpawnFx(from, to, FxKind.Melee, null);

        public void Play(Vector3 from, Vector3 to, bool ranged)
        {
            if (ranged)
                PlayProjectile(from, to, null);
            else
                Play(from, to);
        }

        public void PlayProjectile(Vector3 from, Vector3 to, EntityView hitTarget,
            Func<bool> resolveHit = null)
        {
            from.z = HostPresentationSpace.EntityZ - 0.02f;
            to.z = HostPresentationSpace.EntityZ - 0.02f;
            SpawnFx(from, to, FxKind.Projectile, hitTarget, resolveHit);
        }

        public void PlayDirectionalProjectile(Vector3 origin, Vector3 direction, float range,
            Func<Vector3, Vector3, bool> sweep)
        {
            SpawnFx(origin, origin + direction.normalized * range, FxKind.Projectile, null, sweep: sweep);
        }

        /// <summary>Earliest segment/circle intersection, including starting inside; no low-frame-rate tunnelling.</summary>
        public static bool SweptContact(Vector3 from, Vector3 to, Vector3 center, float radius, out float fraction)
        {
            var delta = Flat(to - from);
            var relative = Flat(from - center);
            fraction = 0f;
            var c = relative.sqrMagnitude - radius * radius;
            if (c <= 0f) return true;
            var a = delta.sqrMagnitude;
            if (a <= .0000001f) return false;
            var b = Vector3.Dot(relative, delta);
            var discriminant = b * b - a * c;
            if (discriminant < 0f) return false;
            fraction = (-b - Mathf.Sqrt(discriminant)) / a;
            return fraction >= 0f && fraction <= 1f;
        }

        void SpawnFx(Vector3 from, Vector3 to, FxKind kind, EntityView hitTarget,
            Func<bool> resolveHit = null, Func<Vector3, Vector3, bool> sweep = null)
        {
            EnsureRoot();
            var fx = Rent();
            fx.BornAt = _clock;
            fx.From = from;
            fx.To = to;
            fx.Direction = Vector3.right;
            fx.MaxRange = 0f;
            fx.Traveled = 0f;
            fx.Speed = 0f;
            fx.Kind = kind;
            fx.HitTarget = hitTarget;
            fx.HitApplied = false;
            fx.ResolveHit = resolveHit;
            fx.Sweep = sweep;

            if (kind == FxKind.Projectile)
            {
                var flatDelta = Flat(to) - Flat(from);
                var dist = flatDelta.magnitude;
                if (dist < 0.0001f)
                {
                    flatDelta = Vector3.right;
                    dist = 0.5f;
                }
                else
                    flatDelta /= dist;

                fx.Direction = flatDelta;
                fx.MaxRange = dist;
                fx.To = from + fx.Direction * dist;
                fx.To.z = from.z;

                var travel = Mathf.Clamp(
                    dist / Mathf.Max(1f, projectileSpeed),
                    projectileMinSeconds,
                    projectileMaxSeconds);
                fx.Speed = Mathf.Max(.1f, dist / travel);
                fx.DieAt = fx.BornAt + travel + 0.08f;
            }
            else if (kind == FxKind.Impact)
                fx.DieAt = fx.BornAt + impactLifetime;
            else
                fx.DieAt = fx.BornAt + lifetime;

            if (fx.Root != null) fx.Root.position = from;
            Place(fx);
            _live.Add(fx);
        }

        void LateUpdate()
        {
            if (_host != null && _host.Session != null && _host.Session.IsPaused) return;
            _clock += Time.unscaledDeltaTime;
            if (_live.Count == 0)
                return;

            var now = _clock;
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                if (s.Root == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                var span = Mathf.Max(0.01f, s.DieAt - s.BornAt);
                var t = Mathf.Clamp01((now - s.BornAt) / span);
                if (s.Kind == FxKind.Projectile && !s.HitApplied)
                {
                    var target = s.HitTarget;
                    if (target == null && s.ResolveHit != null)
                    {
                        Return(s);
                        _live.RemoveAt(i);
                        continue;
                    }

                    var previous = s.Root.position;
                    var step = Mathf.Min(s.Speed * Time.unscaledDeltaTime, s.MaxRange - s.Traveled);
                    var pos = previous + s.Direction * step;
                    pos.z = HostPresentationSpace.EntityZ - 0.02f;
                    s.Root.position = pos;
                    s.Traveled += step;

                    var travelT = Mathf.Clamp01(s.Traveled / Mathf.Max(0.01f, s.MaxRange));
                    Animate(s, travelT);

                    if (s.Sweep != null && s.Sweep(previous, pos))
                    {
                        Return(s);
                        _live.RemoveAt(i);
                        continue;
                    }

                    if (target != null && target.gameObject.activeInHierarchy)
                    {
                        var targetPos = target.CombatVisualPoint;
                        targetPos.z = HostPresentationSpace.EntityZ - 0.02f;
                        var hitRadius = Mathf.Max(0.08f, projectileHitRadius);
                        if (SweptContact(previous, pos, targetPos, hitRadius, out _))
                        {
                            s.To = targetPos;
                            s.HitApplied = true;
                            ApplyProjectileHit(s);
                            Return(s);
                            _live.RemoveAt(i);
                            continue;
                        }
                    }

                    if (s.Traveled >= s.MaxRange)
                    {
                        Return(s);
                        _live.RemoveAt(i);
                        continue;
                    }
                }
                else Animate(s, t);

                if (now >= s.DieAt)
                {
                    Return(s);
                    _live.RemoveAt(i);
                    continue;
                }
                // Fx is a value type: preserve travelled distance for the next swept segment.
                _live[i] = s;
            }
        }

        void ApplyProjectileHit(Fx s)
        {
            if (s.ResolveHit != null && !s.ResolveHit()) return;
            if (s.HitTarget != null)
                s.HitTarget.PlayHitFlash(hitFlashSeconds);
            HostAudioFeedback.Play(gameObject, HostAudioFeedback.Cue.Hit);
            SpawnFx(s.To, s.To, FxKind.Impact, null);
        }

        void Animate(Fx s, float t)
        {
            switch (s.Kind)
            {
                case FxKind.Projectile:
                    AnimateProjectile(s, t);
                    break;
                case FxKind.Impact:
                    AnimateImpact(s, t);
                    break;
                default:
                    AnimateMelee(s, t);
                    break;
            }
        }

        void AnimateMelee(Fx s, float t)
        {
            var expand = t < 0.35f ? t / 0.35f : 1f;
            var fade = t < 0.45f ? 1f : 1f - (t - 0.45f) / 0.55f;
            var mid = Vector3.Lerp(s.From, s.To, 0.55f);
            mid.z = HostPresentationSpace.EntityZ - 0.02f;
            var dir = Flat(s.To - s.From);
            var len = Mathf.Max(0.55f, dir.magnitude * 0.85f);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.right;
            else
                dir.Normalize();

            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            angle += Mathf.Lerp(-38f, 28f, expand);
            s.Root.position = mid;
            s.Root.rotation = Quaternion.Euler(0f, 0f, angle);
            s.Root.localScale = new Vector3(
                len * Mathf.Lerp(0.35f, 1.05f, expand),
                Mathf.Lerp(0.45f, 1.15f, expand) * 0.9f,
                1f);

            if (s.Renderer != null)
            {
                var c = s.Renderer.color;
                c.a = Mathf.Clamp01(fade) * 0.92f;
                s.Renderer.color = c;
            }
        }

        void AnimateProjectile(Fx s, float t)
        {
            // Constant travel speed keeps a short-range bolt readable instead of
            // lingering at launch and then seeming to jump into the target.
            var pos = s.Root.position;
            pos.z = HostPresentationSpace.EntityZ - 0.03f;
            var dir = s.Direction.sqrMagnitude < 0.0001f ? Vector3.right : s.Direction;

            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            s.Root.position = pos;
            s.Root.rotation = Quaternion.Euler(0f, 0f, angle);
            // 沿飞行方向略拉长，读成弹道而非定点光斑
            var pulse = 0.92f + 0.12f * Mathf.Sin(t * Mathf.PI);
            s.Root.localScale = new Vector3(0.95f * pulse, 0.55f * pulse, 1f);

            if (s.Renderer != null)
            {
                var c = ProjectileColor;
                c.a = Mathf.Lerp(0.55f, 0.95f, Mathf.Sin(t * Mathf.PI));
                s.Renderer.color = c;
            }
            if (s.Trail != null)
            {
                s.Trail.transform.localPosition = new Vector3(-.53f, 0f, 0f);
                s.Trail.transform.localScale = new Vector3(.95f, .42f, 1f);
                var c = ProjectileColor;
                c.a = .38f * Mathf.Clamp01((1f - t) * 2.5f);
                s.Trail.color = c;
            }
        }

        void AnimateImpact(Fx s, float t)
        {
            var pos = s.To;
            pos.z = HostPresentationSpace.EntityZ - 0.025f;
            s.Root.position = pos;
            s.Root.rotation = Quaternion.identity;
            var scale = Mathf.Lerp(0.35f, 1.35f, t);
            s.Root.localScale = new Vector3(scale, scale, 1f);
            if (s.Renderer != null)
            {
                var c = ProjectileColor;
                c.a = Mathf.Clamp01(1f - t) * 0.9f;
                s.Renderer.color = c;
            }
        }

        void Place(Fx s)
        {
            if (s.Root == null)
                return;
            s.Root.gameObject.SetActive(true);
            if (s.Renderer != null)
            {
                if (s.Kind == FxKind.Melee)
                {
                    s.Renderer.sprite = SlashSprite();
                    s.Renderer.color = new Color(1f, 0.92f, 0.72f, 0.9f);
                }
                else
                {
                    s.Renderer.sprite = BoltSprite();
                    s.Renderer.color = ProjectileColor;
                }
                s.Renderer.sortingOrder = 32750;
            }
            if (s.Trail != null)
            {
                s.Trail.enabled = s.Kind == FxKind.Projectile;
                s.Trail.sprite = BoltSprite();
                s.Trail.sortingOrder = 32749;
            }

            Animate(s, 0f);
        }

        static readonly Color ProjectileColor = new Color(0.42f, 0.88f, 1f, 0.92f);

        static Vector3 Flat(Vector3 v)
        {
            v.z = 0f;
            return v;
        }

        Fx Rent()
        {
            if (_pool.Count > 0)
                return _pool.Pop();
            return CreateFx();
        }

        void Return(Fx s)
        {
            if (s.Root == null)
                return;
            s.HitTarget = null;
            s.HitApplied = false;
            s.ResolveHit = null;
            s.Sweep = null;
            s.Direction = Vector3.right;
            s.MaxRange = 0f;
            s.Traveled = 0f;
            s.Root.gameObject.SetActive(false);
            _pool.Push(s);
        }

        Fx CreateFx()
        {
            EnsureRoot();
            var go = new GameObject("StrikeFx");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SlashSprite();
            sr.color = new Color(1f, 0.92f, 0.72f, 0.9f);
            sr.sortingOrder = 32750;
            var trailGo = new GameObject("ProjectileTrail");
            trailGo.transform.SetParent(go.transform, false);
            var trail = trailGo.AddComponent<SpriteRenderer>();
            trail.sprite = BoltSprite();
            trail.color = ProjectileColor;
            trail.sortingOrder = 32749;
            trail.enabled = false;
            go.SetActive(false);
            return new Fx { Root = go.transform, Renderer = sr, Trail = trail };
        }

        void EnsureRoot()
        {
            if (_root != null)
                return;
            var go = new GameObject("HostMeleeStrikeVfx");
            go.transform.SetParent(transform, false);
            _root = go.transform;

            for (var i = 0; i < poolSize; i++)
                _pool.Push(CreateFx());
        }

        Sprite SlashSprite()
        {
            if (_slashSprite != null)
                return _slashSprite;
            return _slashSprite = HostSpriteFactory.MeleeSlashSprite();
        }

        Sprite BoltSprite()
        {
            if (_boltSprite != null)
                return _boltSprite;
            return _boltSprite = HostSpriteFactory.RangedProjectileSprite();
        }
    }
}
