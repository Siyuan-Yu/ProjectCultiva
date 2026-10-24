using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Presentation-only shared-bone paper-doll. The parent EntityView remains
    /// the sole character object, world position, collider, selection and Core binding.
    /// </summary>
    public sealed class HumanCommonRigView : MonoBehaviour
    {
        public enum Equipment { None, Axe, Hoe, Pick, Sickle, Spear }

        static readonly Dictionary<string, Sprite[]> PartsCache = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, Sprite[]> SideHeadsCache = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, Sprite[]> DiagonalCache = new Dictionary<string, Sprite[]>();
        static readonly HashSet<string> ReportedDiagonalFailures = new HashSet<string>();
        static readonly Dictionary<string, Sprite[]> SideArmsCache = new Dictionary<string, Sprite[]>();
        static readonly HashSet<string> ReportedSideArmFailures = new HashSet<string>();
        static readonly Dictionary<string, Sprite> ClosedHeadCache = new Dictionary<string, Sprite>();
        static readonly HashSet<string> ReportedClosedHeadFailures = new HashSet<string>();
        static Sprite[] _equipment;
        [Serializable] sealed class PieceLayout
        {
            public int x, y, width, height;
            public float pivotX, pivotY, worldHeight, gripX, gripY;
            public bool hasSockets, mirrorX;
            public Vector2 neckSocket, leftShoulderSocket, rightShoulderSocket;
            public Vector2[] meshVertices;
            public int[] meshTriangles;
        }
        [Serializable] sealed class AtlasLayout { public PieceLayout[] parts; }
        static readonly Dictionary<string, AtlasLayout> Layouts = new Dictionary<string, AtlasLayout>();

        Transform _footAnchor;
        Transform _upperBody;
        Transform _torso, _head, _leftArm, _rightArm, _leftLeg, _rightLeg;
        Transform _toolSlot, _weaponSlot;
        SpriteRenderer _torsoSprite, _headSprite, _leftArmSprite, _rightArmSprite;
        SpriteRenderer _leftLegSprite, _rightLegSprite, _toolSprite, _basketSprite;
        SpriteRenderer[] _renderers;
        bool _hasTint, _hasDepth;
        Color _appliedTint;
        (int Order, int Layer, HostStepAArt.Direction Direction, bool Bed, int LeftLeg, int LeftArm) _appliedDepth;
        Sprite[] _parts;
        Sprite[] _diagonalParts;
        Sprite _sideArm;
        AtlasLayout _partLayout;
        AtlasLayout _diagonalLayout;
        Sprite _sideHead;
        Sprite _frontClosedHead, _sideClosedHead;
        bool _hasBasket;
        HumanCommonRigEquipment _equipmentBinding;
        SortingGroup _sortingGroup;
        SortingGroup _bedHeadSortingGroup;
        Vector3 _rightGrip = new Vector3(.22f, -.7f, 0f);
        Vector3 _leftGrip = new Vector3(-.22f, -.7f, 0f);
        Vector3 _sideArmGrip = new Vector3(0f, -.72f, 0f);
        float _leftNeutralAngle, _rightNeutralAngle;
        float _sideArmNeutralAngle;
        const float MinArmStretch = .72f;
        const float MaxArmStretch = 1.15f;
        // Shared presentation proportions, applied around calibrated neck/torso pivots.
        // Keep the parent bones unchanged so gait, tools and attachments retain their scale.
        const float HeadProportionScale = .88f;
        const float FrontBackTorsoWidthScale = 1.06f;
        const float SideTorsoWidthScale = 1.12f;
        float _walkBlend;
        float _leftFootHeight, _rightFootHeight;
        float _workClockOrigin;
        HostStepAArt.Action _lastAction;
        HostStepAArt.Direction _lastDirection = HostStepAArt.Direction.South;
        HumanWorkMotion _lastWorkMotion;
        HumanWorkMotion _pendingWorkContact;
        bool _hasPendingWorkContact;
        bool _supportHandActive;
        bool _sleepPose;
        bool _lifePose;
        bool _downedPose, _deadPose, _carriedPose;
        bool _sleepOnBed;
        Vector3 _sleepWorldPosition;
        int _sleepQuarterTurns;
        HostBedVisualLayers _bedVisual;
        bool _usingBedFrame;
        bool _lastUsingBedFrame;
        bool _hasAppliedFrame;
        bool _bedEntryActive;
        float _bedEntryElapsed;
        Vector3 _bedEntryOrigin;
        Quaternion _bedEntryRotation;
        const float BedEntrySeconds = .42f;
        int _bedSortingLayerId, _bedBaseOrder;
        enum RestPose { None, Sleeping, Downed, Dead, Carried }
        RestPose _lastRestPose;

        public bool IsSleepPose => _sleepPose;
        public bool UsesPoseBounds => _sleepPose || _lifePose;
        public bool IsReady => _parts != null && _footAnchor != null;
        public string InitializationFailureReason { get; private set; } = string.Empty;
        public float WalkBlend => _walkBlend;
        public HostStepAArt.Direction LastDirection => _lastDirection;
        public bool HasVisibleBody => _torsoSprite != null && _torsoSprite.enabled &&
            _torsoSprite.gameObject.activeInHierarchy;
        public int RendererCount => 8;

        RestPose CurrentRestPose => _carriedPose ? RestPose.Carried :
            _deadPose ? RestPose.Dead : _downedPose ? RestPose.Downed :
            _sleepPose ? RestPose.Sleeping : RestPose.None;

        /// <summary>External bed sandwich depth, separate from the rig's internal limb orders.</summary>
        public bool TryGetBedDepth(out int sortingLayerId, out int order)
        {
            sortingLayerId = _bedSortingLayerId;
            order = _bedBaseOrder + (_bedEntryActive ? 3 : 1);
            return _usingBedFrame && !_deadPose && !_carriedPose && _bedVisual != null;
        }

        /// <summary>Visible anatomy only; attachments and world UI do not define a character's body.</summary>
        public bool TryGetVisibleBodyBounds(out Bounds bounds)
        {
            bounds = default;
            if (_renderers == null || !gameObject.activeInHierarchy) return false;
            var found = false;
            foreach (var part in _renderers)
            {
                if (part == null || part == _toolSprite || part == _basketSprite ||
                    !part.enabled || part.sprite == null || !part.gameObject.activeInHierarchy) continue;
                if (found) bounds.Encapsulate(part.bounds);
                else { bounds = part.bounds; found = true; }
            }
            return found;
        }

        public void NotifyWorkContact(HumanWorkMotion motion)
        {
            if (_lifePose || _sleepPose) return;
            _pendingWorkContact = motion;
            _hasPendingWorkContact = true;
        }

        public bool Initialize(HostStepAArt.Profile profile, HumanCommonRigEquipment equipmentBinding = null,
            HostStepAArt.RigSkin skin = null)
        {
            InitializationFailureReason = string.Empty;
            _equipmentBinding = equipmentBinding;
            _hasBasket = false;
            string id, sideAtlas;
            int sideIndex;
            if (skin != null)
            {
                id = skin.partsBase;
                sideAtlas = skin.sideHeadAtlas;
                sideIndex = skin.sideHeadIndex;
                _hasBasket = skin.basket;
            }
            else switch (profile)
            {
                case HostStepAArt.Profile.Villager: id = "ART-CHAR-001"; sideAtlas = "ART-CHAR-001_002_004_HumanCommonRig_sideheads_v01"; sideIndex = 0; break;
                case HostStepAArt.Profile.Protagonist: id = "ART-CHAR-002"; sideAtlas = "ART-CHAR-001_002_004_HumanCommonRig_sideheads_v01"; sideIndex = 1; break;
                case HostStepAArt.Profile.Guard: id = "ART-CHAR-004"; sideAtlas = "ART-CHAR-001_002_004_HumanCommonRig_sideheads_v01"; sideIndex = 2; break;
                case HostStepAArt.Profile.CompanionA: id = "ART-CHAR-002_companion_a"; sideAtlas = "ART-CHAR-002_003_005_HumanCommonRig_sideheads_v01"; sideIndex = 0; break;
                case HostStepAArt.Profile.CompanionB: id = "ART-CHAR-002_companion_b"; sideAtlas = "ART-CHAR-002_003_005_HumanCommonRig_sideheads_v01"; sideIndex = 1; break;
                case HostStepAArt.Profile.Supervisor: id = "ART-CHAR-003_supervisor"; sideAtlas = "ART-CHAR-002_003_005_HumanCommonRig_sideheads_v01"; sideIndex = 2; break;
                case HostStepAArt.Profile.FarmerHoe: id = "ART-CHAR-005_hoe"; sideAtlas = "ART-CHAR-002_003_005_HumanCommonRig_sideheads_v01"; sideIndex = 3; break;
                case HostStepAArt.Profile.FarmerApron: id = "ART-CHAR-005_apron"; sideAtlas = "ART-CHAR-005_006_007_HumanCommonRig_sideheads_v01"; sideIndex = 0; break;
                case HostStepAArt.Profile.ResidentBasket: id = "ART-CHAR-005_basket"; sideAtlas = "ART-CHAR-005_006_007_HumanCommonRig_sideheads_v01"; sideIndex = 1; _hasBasket = true; break;
                case HostStepAArt.Profile.Merchant: id = "ART-CHAR-006_merchant"; sideAtlas = "ART-CHAR-005_006_007_HumanCommonRig_sideheads_v01"; sideIndex = 2; break;
                case HostStepAArt.Profile.Other: id = "ART-CHAR-007_other"; sideAtlas = "ART-CHAR-005_006_007_HumanCommonRig_sideheads_v01"; sideIndex = 3; break;
                case HostStepAArt.Profile.JiangLao: id = "ART-CHAR-007_jiang_lao"; sideAtlas = "ART-CHAR-007_jiang_lao_HumanCommonRig_sidehead_v01"; sideIndex = 0; break;
                default: return InitializationFailed("Unsupported character profile: " + profile + " (" + (int)profile + ").");
            }
            var partsKey = id + "_HumanCommonRig_parts_v01";
            _partLayout = null;
            if (!PartsCache.TryGetValue(id, out _parts))
            {
                var texture = Resources.Load<Texture2D>(partsKey);
                if (texture == null) return InitializationFailed("Missing Texture2D resource: " + partsKey + ".");
                _parts = LayoutSprites(texture, partsKey, 9, out var failure);
                if (_parts == null) return InitializationFailed(failure);
                PartsCache.Add(id, _parts);
            }
            if (Layouts.TryGetValue(partsKey, out var partLayout))
            {
                _partLayout = partLayout;
                var grip = partLayout.parts[2];
                _rightGrip = new Vector3((grip.gripX - grip.pivotX) * _parts[2].rect.width / _parts[2].pixelsPerUnit,
                    (grip.gripY - grip.pivotY) * _parts[2].rect.height / _parts[2].pixelsPerUnit, 0f);
                if (grip.mirrorX) _rightGrip.x = -_rightGrip.x;
                grip = partLayout.parts[3];
                _leftGrip = new Vector3((grip.gripX - grip.pivotX) * _parts[3].rect.width / _parts[3].pixelsPerUnit,
                    (grip.gripY - grip.pivotY) * _parts[3].rect.height / _parts[3].pixelsPerUnit, 0f);
                if (grip.mirrorX) _leftGrip.x = -_leftGrip.x;
            }
            if (_partLayout == null || !_partLayout.parts[0].hasSockets ||
                !_partLayout.parts[6].hasSockets || !_partLayout.parts[8].hasSockets)
                return InitializationFailed("Missing calibrated torso sockets in " + partsKey + "_layout.");
            if (_leftGrip.y >= -.01f || _rightGrip.y >= -.01f)
                return InitializationFailed("Calibrated arm grips must lie below the shoulder in " + partsKey + "_layout.");
            // The PNG arms are painted diagonally. Their shoulder-to-palm vector,
            // not the image axes, defines the same hanging-arm rest pose for every skin.
            _leftNeutralAngle = -90f - Mathf.Atan2(_leftGrip.y, _leftGrip.x) * Mathf.Rad2Deg;
            _rightNeutralAngle = -90f - Mathf.Atan2(_rightGrip.y, _rightGrip.x) * Mathf.Rad2Deg;
            var sideArmKey = "SideArms/" + id + "_HumanCommonRig_sidearm_v01";
            _sideArm = null;
            if (!SideArmsCache.TryGetValue(sideArmKey, out var sideArms))
            {
                var sideArmTexture = Resources.Load<Texture2D>(sideArmKey);
                if (sideArmTexture != null)
                {
                    sideArms = LayoutSprites(sideArmTexture, sideArmKey, 1, out var sideArmFailure);
                    if (sideArms == null && ReportedSideArmFailures.Add(sideArmKey))
                        Debug.LogWarning("[HumanCommonRig] " + sideArmFailure + " Using front arms for side views.");
                    if (sideArms != null) SideArmsCache.Add(sideArmKey, sideArms);
                }
                else if (ReportedSideArmFailures.Add(sideArmKey))
                    Debug.LogWarning("[HumanCommonRig] Missing side arm " + sideArmKey +
                        ". Using front arms for side views.");
            }
            if (sideArms != null && Layouts.TryGetValue(sideArmKey, out var sideArmLayout))
            {
                _sideArm = sideArms[0];
                var arm = sideArmLayout.parts[0];
                _sideArmGrip = new Vector3((arm.gripX - arm.pivotX) * _sideArm.rect.width / _sideArm.pixelsPerUnit,
                    (arm.gripY - arm.pivotY) * _sideArm.rect.height / _sideArm.pixelsPerUnit, 0f);
                if (arm.mirrorX) _sideArmGrip.x = -_sideArmGrip.x;
                if (_sideArmGrip.y >= -.01f)
                {
                    _sideArm = null;
                    if (ReportedSideArmFailures.Add(sideArmKey))
                        Debug.LogWarning("[HumanCommonRig] Side arm palm must lie below shoulder: " + sideArmKey);
                }
                else _sideArmNeutralAngle = -90f - Mathf.Atan2(_sideArmGrip.y, _sideArmGrip.x) * Mathf.Rad2Deg;
            }
            if (!SideHeadsCache.TryGetValue(sideAtlas, out var sideHeads))
            {
                var texture = Resources.Load<Texture2D>(sideAtlas);
                if (texture == null) return InitializationFailed("Missing Texture2D resource: " + sideAtlas + ".");
                var sideColumns = skin != null ? skin.sideHeadColumns :
                    profile == HostStepAArt.Profile.JiangLao ? 1 :
                    profile == HostStepAArt.Profile.Villager ||
                    profile == HostStepAArt.Profile.Protagonist || profile == HostStepAArt.Profile.Guard ? 3 : 4;
                sideHeads = LayoutSprites(texture, sideAtlas, sideColumns, out var failure);
                if (sideHeads == null) return InitializationFailed(failure);
                SideHeadsCache.Add(sideAtlas, sideHeads);
            }
            _sideHead = sideHeads[sideIndex];
            // Two authored right-facing quarter views provide all four diagonals by
            // mirroring the whole rig. Missing optional art falls back to front/back.
            var diagonalKey = "Diagonal/" + id + "_HumanCommonRig_diagonal_v01";
            _diagonalParts = null;
            _diagonalLayout = null;
            if (!DiagonalCache.TryGetValue(diagonalKey, out _diagonalParts))
            {
                var diagonalTexture = Resources.Load<Texture2D>(diagonalKey);
                if (diagonalTexture != null)
                {
                    _diagonalParts = LayoutSprites(diagonalTexture, diagonalKey, 4, out var diagonalFailure);
                    if (_diagonalParts == null && ReportedDiagonalFailures.Add(diagonalKey))
                        Debug.LogWarning("[HumanCommonRig] " + diagonalFailure + " Using cardinal art for diagonal views.");
                    if (_diagonalParts != null) DiagonalCache.Add(diagonalKey, _diagonalParts);
                }
                else if (ReportedDiagonalFailures.Add(diagonalKey))
                    Debug.LogWarning("[HumanCommonRig] Missing diagonal atlas " + diagonalKey +
                        ". Using cardinal art for diagonal views.");
            }
            if (_diagonalParts != null && Layouts.TryGetValue(diagonalKey, out var diagonalLayout) &&
                diagonalLayout.parts[0].hasSockets && diagonalLayout.parts[2].hasSockets)
                _diagonalLayout = diagonalLayout;
            else _diagonalParts = null;
            // The closed-eye atlases reuse the original head layout and neck pivot. They are
            // optional expression art, never a reason to abandon the whole character rig.
            _frontClosedHead = LoadClosedHead(partsKey, 1);
            _sideClosedHead = LoadClosedHead(sideAtlas, sideIndex);
            if (_equipment == null)
            {
                const string toolsKey = "ART-ANIM-002_004_HumanCommonRig_tools_v01";
                var texture = Resources.Load<Texture2D>(toolsKey);
                if (texture == null) return InitializationFailed("Missing Texture2D resource: " + toolsKey + ".");
                _equipment = LayoutSprites(texture, toolsKey, 6, out var failure);
                if (_equipment == null) return InitializationFailed(failure);
            }
            BuildHierarchy();
            Apply(HostStepAArt.Action.Idle, HostStepAArt.Direction.South, 0f, 0f, Equipment.None);
            return true;
        }

        bool InitializationFailed(string reason)
        {
            InitializationFailureReason = reason;
            return false;
        }

        static Sprite LoadClosedHead(string atlasKey, int index)
        {
            var cacheKey = atlasKey + ":" + index;
            if (ClosedHeadCache.TryGetValue(cacheKey, out var cached)) return cached;
            var resourceKey = "ClosedEyes/" + atlasKey + "_head_" + index + "_closedeyes_v01";
            var texture = Resources.Load<Texture2D>(resourceKey);
            if (!Layouts.TryGetValue(atlasKey, out var layout) ||
                layout.parts == null || index < 0 || index >= layout.parts.Length)
            {
                if (ReportedClosedHeadFailures.Add(cacheKey))
                    Debug.LogWarning("[HumanCommonRig] Closed-eye head layout unavailable: " +
                        resourceKey + " part " + index + ". Keeping this character's open-eye head.");
                return null;
            }
            var part = layout.parts[index];
            if (texture == null || texture.width != part.width || texture.height != part.height)
            {
                if (ReportedClosedHeadFailures.Add(cacheKey))
                    Debug.LogWarning("[HumanCommonRig] Closed-eye head missing or size mismatch: " +
                        resourceKey + " expected " + part.width + "x" + part.height +
                        ". Keeping this character's open-eye head.");
                return null;
            }
            var sprite = Sprite.Create(texture,
                new Rect(0, 0, part.width, part.height),
                new Vector2(part.pivotX, part.pivotY), part.height / part.worldHeight,
                0, SpriteMeshType.FullRect);
            if (part.meshVertices != null)
            {
                var vertices = new Vector2[part.meshVertices.Length];
                for (var i = 0; i < vertices.Length; i++)
                    vertices[i] = SpriteMeshPoint(sprite, part.meshVertices[i]);
                var triangles = new ushort[part.meshTriangles.Length];
                for (var i = 0; i < triangles.Length; i++)
                    triangles[i] = (ushort)part.meshTriangles[i];
                sprite.OverrideGeometry(vertices, triangles);
            }
            ClosedHeadCache.Add(cacheKey, sprite);
            return sprite;
        }

        static Sprite[] LayoutSprites(Texture2D texture, string resourceKey, int count, out string failure)
        {
            failure = string.Empty;
            var layoutKey = resourceKey + "_layout";
            if (!Layouts.TryGetValue(resourceKey, out var layout))
            {
                var json = Resources.Load<TextAsset>(layoutKey);
                if (json == null)
                {
                    failure = "Missing TextAsset layout resource: " + layoutKey + ".";
                    return null;
                }
                try { layout = JsonUtility.FromJson<AtlasLayout>(json.text); }
                catch (Exception error)
                {
                    failure = "Invalid layout JSON " + layoutKey + ": " + error.Message;
                    return null;
                }
            }
            if (layout?.parts == null || layout.parts.Length != count)
            {
                Layouts.Remove(resourceKey);
                failure = "Layout " + layoutKey + " expected " + count + " parts, found " +
                    (layout?.parts == null ? "no parts array" : layout.parts.Length.ToString()) + ".";
                return null;
            }
            // Validate every rectangle before creating sprites or caching the layout. A failed
            // attempt must be able to load a corrected resource on the bounded binding retry.
            for (var i = 0; i < count; i++)
            {
                var p = layout.parts[i];
                if (p == null)
                {
                    Layouts.Remove(resourceKey);
                    failure = "Layout " + layoutKey + " part[" + i + "] is null.";
                    return null;
                }
                if (p.width <= 0 || p.height <= 0 || p.x < 0 || p.y < 0 ||
                    p.width > texture.width || p.height > texture.height ||
                    p.x > texture.width - p.width || p.y > texture.height - p.height)
                {
                    Layouts.Remove(resourceKey);
                    failure = "Layout " + layoutKey + " part[" + i + "] rectangle (" +
                        p.x + "," + p.y + "," + p.width + "," + p.height + ") exceeds Texture2D " +
                        resourceKey + " bounds " + texture.width + "x" + texture.height + ".";
                    return null;
                }
                if (!IsFinite(p.worldHeight) || p.worldHeight <= 0f)
                {
                    Layouts.Remove(resourceKey);
                    failure = "Layout " + layoutKey + " part[" + i +
                        "] worldHeight must be finite and positive, found " + p.worldHeight + ".";
                    return null;
                }
                if (!IsFinite(p.pivotX) || !IsFinite(p.pivotY) ||
                    p.pivotX < 0f || p.pivotX > 1f || p.pivotY < 0f || p.pivotY > 1f)
                {
                    Layouts.Remove(resourceKey);
                    failure = "Layout " + layoutKey + " part[" + i +
                        "] pivot must be finite and normalized, found (" + p.pivotX + "," + p.pivotY + ").";
                    return null;
                }
                if (!IsFinite(p.gripX) || !IsFinite(p.gripY))
                {
                    Layouts.Remove(resourceKey);
                    failure = "Layout " + layoutKey + " part[" + i +
                        "] grip must be finite, found (" + p.gripX + "," + p.gripY + ").";
                    return null;
                }
                if (count == 9 && (i == 0 || i == 6 || i == 8) && !p.hasSockets)
                {
                    Layouts.Remove(resourceKey);
                    failure = "Layout " + layoutKey + " part[" + i + "] is missing calibrated torso sockets.";
                    return null;
                }
                if (count == 9 && (i == 2 || i == 3) &&
                    ((p.pivotY - p.gripY) * p.worldHeight <= .01f ||
                    p.gripX < 0f || p.gripX > 1f || p.gripY < 0f || p.gripY > 1f))
                {
                    Layouts.Remove(resourceKey);
                    failure = "Layout " + layoutKey + " part[" + i + "] has an invalid shoulder-to-palm calibration.";
                    return null;
                }
                if (p.hasSockets && (!IsNormalized(p.neckSocket) ||
                    !IsNormalized(p.leftShoulderSocket) || !IsNormalized(p.rightShoulderSocket)))
                {
                    Layouts.Remove(resourceKey);
                    failure = "Layout " + layoutKey + " part[" + i + "] has invalid normalized torso sockets.";
                    return null;
                }
                if (p.meshVertices != null || p.meshTriangles != null)
                {
                    if (p.meshVertices == null || p.meshVertices.Length < 3 || p.meshVertices.Length > ushort.MaxValue ||
                        p.meshTriangles == null || p.meshTriangles.Length == 0 || p.meshTriangles.Length % 3 != 0)
                    {
                        Layouts.Remove(resourceKey);
                        failure = "Layout " + layoutKey + " part[" + i + "] has incomplete visible-body geometry.";
                        return null;
                    }
                    for (var v = 0; v < p.meshVertices.Length; v++)
                        if (!IsNormalized(p.meshVertices[v]))
                        {
                            Layouts.Remove(resourceKey);
                            failure = "Layout " + layoutKey + " part[" + i + "] has invalid mesh vertex " + v + ".";
                            return null;
                        }
                    for (var t = 0; t < p.meshTriangles.Length; t++)
                        if (p.meshTriangles[t] < 0 || p.meshTriangles[t] >= p.meshVertices.Length)
                        {
                            Layouts.Remove(resourceKey);
                            failure = "Layout " + layoutKey + " part[" + i + "] has invalid mesh triangle index " + t + ".";
                            return null;
                        }
                }
            }
            var result = new Sprite[count];
            for (var i = 0; i < count; i++)
            {
                var p = layout.parts[i];
                result[i] = Sprite.Create(texture,
                    new Rect(p.x, texture.height - p.y - p.height, p.width, p.height),
                    new Vector2(p.pivotX, p.pivotY), p.height / p.worldHeight, 0, SpriteMeshType.FullRect);
                if (p.meshVertices != null)
                {
                    var vertices = new Vector2[p.meshVertices.Length];
                    for (var v = 0; v < vertices.Length; v++)
                        vertices[v] = SpriteMeshPoint(result[i], p.meshVertices[v]);
                    var triangles = new ushort[p.meshTriangles.Length];
                    for (var t = 0; t < triangles.Length; t++) triangles[t] = (ushort)p.meshTriangles[t];
                    result[i].OverrideGeometry(vertices, triangles);
                }
            }
            if (!Layouts.ContainsKey(resourceKey)) Layouts.Add(resourceKey, layout);
            return result;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsNormalized(Vector2 point) => IsFinite(point.x) && IsFinite(point.y) &&
            point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
        // OverrideGeometry expects pixel positions in the Sprite rect, not pivot-relative world units.
        static Vector2 SpriteMeshPoint(Sprite sprite, Vector2 point) => new Vector2(
            point.x * sprite.rect.width, point.y * sprite.rect.height);
        static Vector2 SpritePoint(Sprite sprite, Vector2 point) => new Vector2(
            (point.x * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit,
            (point.y * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit);
        static Vector3 TorsoSocket(Sprite sprite, PieceLayout layout, Vector2 point, float widthScale)
        {
            var local = SpritePoint(sprite, point);
            return new Vector3((layout.mirrorX ? -local.x : local.x) * widthScale, local.y, 0f);
        }

        static SpriteRenderer Part(Transform parent, string name, Vector3 local, out Transform piece)
        {
            var go = new GameObject(name);
            piece = go.transform;
            piece.SetParent(parent, false);
            piece.localPosition = local;
            return go.AddComponent<SpriteRenderer>();
        }

        void BuildHierarchy()
        {
            _hasTint = _hasDepth = false;
            _sortingGroup = gameObject.AddComponent<SortingGroup>();
            var anchor = new GameObject("FootAnchor");
            _footAnchor = anchor.transform;
            _footAnchor.SetParent(transform, false);
            _footAnchor.localScale = Vector3.one * .58f;
            _leftLegSprite = Part(_footAnchor, "LeftLeg", new Vector3(-.14f, .72f, 0f), out _leftLeg);
            _rightLegSprite = Part(_footAnchor, "RightLeg", new Vector3(.14f, .72f, 0f), out _rightLeg);
            _upperBody = new GameObject("UpperBodyBone").transform;
            _upperBody.SetParent(_footAnchor, false);
            _upperBody.localPosition = new Vector3(0f, .94f, 0f);
            _basketSprite = Part(_upperBody, "BackAccessory", new Vector3(0f, -.02f, 0f), out _);
            _torsoSprite = Part(_upperBody, "Torso", Vector3.zero, out _torso);
            _leftArmSprite = Part(_upperBody, "LeftArm", new Vector3(-.25f, .25f, 0f), out _leftArm);
            _rightArmSprite = Part(_upperBody, "RightArm", new Vector3(.25f, .25f, 0f), out _rightArm);
            _head = new GameObject("Head").transform;
            _head.SetParent(_upperBody, false);
            _head.localPosition = new Vector3(0f, .43f, 0f);
            var headArt = new GameObject("HeadSprite");
            headArt.transform.SetParent(_head, false);
            _headSprite = headArt.AddComponent<SpriteRenderer>();
            // A bed needs the head above its quilt while the body remains below it. This
            // nested group ignores the body's group only for a valid bed rest frame.
            _bedHeadSortingGroup = _head.gameObject.AddComponent<SortingGroup>();
            _bedHeadSortingGroup.sortAtRoot = true;
            _bedHeadSortingGroup.enabled = false;
            var tool = new GameObject("ToolSlot");
            _toolSlot = tool.transform;
            _toolSlot.SetParent(_upperBody, false);
            _toolSprite = Part(_toolSlot, "ToolSprite", Vector3.zero, out _);
            var weapon = new GameObject("WeaponSlot");
            _weaponSlot = weapon.transform;
            _weaponSlot.SetParent(_upperBody, false);
            _renderers = new[] { _torsoSprite, _headSprite, _leftArmSprite, _rightArmSprite,
                _leftLegSprite, _rightLegSprite, _toolSprite, _basketSprite };
        }

        public void Apply(HostStepAArt.Action action, HostStepAArt.Direction direction,
            float walkPhase01, float clock, Equipment equipment,
            bool carryingCargo = false, bool carryingPerson = false,
            HumanWorkMotion workMotion = HumanWorkMotion.Generic,
            HumanAttackStyle attackStyle = HumanAttackStyle.Unarmed, float deltaTime = 1f / 60f,
            bool locomoting = false, Vector3? aimDirection = null, bool airborne = false)
        {
            if (_parts == null || _footAnchor == null) return;
            var restPose = CurrentRestPose;
            var resting = restPose != RestPose.None;
            var pillowWorld = Vector3.zero;
            var bedRotation = Quaternion.identity;
            _usingBedFrame = resting && _sleepPose && _sleepOnBed && !_deadPose && !_carriedPose &&
                _bedVisual != null && _bedVisual.TryGetSleepFrame(
                    out pillowWorld, out bedRotation, out _bedSortingLayerId, out _bedBaseOrder);
            if (_usingBedFrame && !_lastUsingBedFrame && _hasAppliedFrame)
            {
                _bedEntryActive = true;
                _bedEntryElapsed = 0f;
                _bedEntryOrigin = transform.position;
                _bedEntryRotation = transform.rotation;
            }
            else if (!_usingBedFrame)
                _bedEntryActive = false;
            if (resting)
                direction = _usingBedFrame ? HostStepAArt.Direction.South : HostStepAArt.Direction.East;
            // The previous frame's bed/ground alignment must never feed back into this frame's
            // socket positions. EntityView's parent remains the unchanged authority/foot point.
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _footAnchor.localRotation = Quaternion.identity;
            _head.localRotation = Quaternion.identity;
            if (!resting && _lastRestPose != RestPose.None)
                RestoreUprightBones();
            var diagonal = direction == HostStepAArt.Direction.NorthEast ||
                direction == HostStepAArt.Direction.NorthWest ||
                direction == HostStepAArt.Direction.SouthEast ||
                direction == HostStepAArt.Direction.SouthWest;
            var back = direction == HostStepAArt.Direction.North ||
                direction == HostStepAArt.Direction.NorthEast || direction == HostStepAArt.Direction.NorthWest;
            var side = diagonal || direction == HostStepAArt.Direction.East || direction == HostStepAArt.Direction.West;
            var flip = direction == HostStepAArt.Direction.West ||
                direction == HostStepAArt.Direction.NorthWest || direction == HostStepAArt.Direction.SouthWest;
            var useDiagonalArt = diagonal && _diagonalParts != null && _diagonalLayout != null;
            var torsoIndex = back ? 6 : side && !diagonal ? 8 : 0;
            var torsoWidth = side ? SideTorsoWidthScale : FrontBackTorsoWidthScale;
            var torsoLayout = useDiagonalArt ? _diagonalLayout.parts[back ? 2 : 0] : _partLayout.parts[torsoIndex];
            _torsoSprite.sprite = useDiagonalArt ? _diagonalParts[back ? 2 : 0] : _parts[torsoIndex];
            // Standardize this specific painted torso to the same facing as its head.
            // Global west-facing mirroring still applies to the complete rig afterwards.
            _torso.localScale = new Vector3((torsoLayout.mirrorX ? -1f : 1f) * torsoWidth, 1f, 1f);
            var eyesClosed = _sleepPose || _downedPose || _deadPose;
            var headLayout = useDiagonalArt ? _diagonalLayout.parts[back ? 3 : 1] : null;
            _headSprite.sprite = useDiagonalArt ? _diagonalParts[back ? 3 : 1] :
                side ? eyesClosed && _sideClosedHead != null ? _sideClosedHead : _sideHead
                : back ? _parts[7] : eyesClosed && _frontClosedHead != null ? _frontClosedHead : _parts[1];
            // Shrink from the neck pivot, keeping the neck attached while lowering the crown.
            _head.localScale = new Vector3(headLayout != null && headLayout.mirrorX ?
                -HeadProportionScale : HeadProportionScale, HeadProportionScale, HeadProportionScale);
            _head.localPosition = TorsoSocket(_torsoSprite.sprite, torsoLayout, torsoLayout.neckSocket, torsoWidth);
            var leftShoulder = TorsoSocket(_torsoSprite.sprite, torsoLayout, torsoLayout.leftShoulderSocket, torsoWidth);
            var rightShoulder = TorsoSocket(_torsoSprite.sprite, torsoLayout, torsoLayout.rightShoulderSocket, torsoWidth);
            var useSideArm = side && !resting && _sideArm != null;
            var leftGrip = useSideArm ? _sideArmGrip : _leftGrip;
            var rightGrip = useSideArm ? _sideArmGrip : _rightGrip;
            var leftNeutralAngle = useSideArm ? _sideArmNeutralAngle : _leftNeutralAngle;
            var rightNeutralAngle = useSideArm ? _sideArmNeutralAngle : _rightNeutralAngle;
            _leftArmSprite.sprite = useSideArm ? _sideArm : _parts[3];
            _rightArmSprite.sprite = useSideArm ? _sideArm : _parts[2];
            // A reflected painted arm must reflect its palm vector as well (see Initialize).
            _leftArmSprite.flipX = !useSideArm && _partLayout.parts[3].mirrorX;
            _rightArmSprite.flipX = !useSideArm && _partLayout.parts[2].mirrorX;
            _leftLegSprite.sprite = _parts[4];
            _rightLegSprite.sprite = _parts[5];
            // Mirror the complete hierarchy, including joints and attachment offsets.
            _footAnchor.localScale = new Vector3(flip ? -.58f : .58f, .58f, .58f);

            var blend = _lifePose || _sleepPose || airborne ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) * 26f);
            _walkBlend = _lifePose || _sleepPose || airborne ? 0f : Mathf.MoveTowards(_walkBlend,
                locomoting || action == HostStepAArt.Action.Walk ? 1f : 0f, Mathf.Max(0f, deltaTime) * 9f);
            var step = Mathf.Sin(walkPhase01 * Mathf.PI * 2f);
            var stride = step * _walkBlend;
            var armStride = stride;
            if (back)
            {
                // Keep the existing north-facing gait used as the producer's visual reference.
                PoseNorthLeg(_leftLeg, _parts[4], -.14f, stride, blend);
                PoseNorthLeg(_rightLeg, _parts[5], .14f, -stride, blend);
            }
            else
            {
                var travel = PoseWalkingLeg(_leftLeg, _parts[4], side ? -.075f : -.14f,
                    walkPhase01, side, _walkBlend, out _leftFootHeight);
                PoseWalkingLeg(_rightLeg, _parts[5], side ? .075f : .14f,
                    walkPhase01 + .5f, side, _walkBlend, out _rightFootHeight);
                armStride = travel * _walkBlend;
            }

            var leftArm = (side ? -22f : back ? -9f : -13f) * armStride;
            var rightArm = (side ? 22f : back ? 9f : 13f) * armStride;
            var lean = 0f;
            var reach = 0f;
            var bodyDrop = 0f;
            var working = action == HostStepAArt.Action.Work && !_lifePose && !_sleepPose;
            var attacking = action == HostStepAArt.Action.Attack && !_lifePose && !_sleepPose;
            if (working && (_lastAction != action || _lastWorkMotion != workMotion))
                _workClockOrigin = clock;
            if (_hasPendingWorkContact)
            {
                if (working && !carryingCargo && !carryingPerson && _pendingWorkContact == workMotion)
                    _workClockOrigin = clock - WorkCycleSeconds(workMotion) * .48f;
                // Contacts belong to this visible action only; never replay them on a later work entry.
                _hasPendingWorkContact = false;
            }
            var phase = Mathf.Repeat(Mathf.Max(0f, clock - _workClockOrigin) / WorkCycleSeconds(workMotion), 1f);
            var hand = new Vector3(side ? .32f : .27f, -.42f, 0f);
            var toolAngle = side ? -10f : back ? -8f : 8f;
            if (working)
            {
                switch (workMotion)
                {
                    case HumanWorkMotion.Chop:
                        leftArm = WorkKey(phase, 8f, 16f, 3f);
                        rightArm = -8f;
                        lean = WorkKey(phase, 2f, -4f, 10f);
                        bodyDrop = WorkKey(phase, 0f, 0f, .045f);
                        hand = new Vector3(WorkKey(phase, side ? .32f : .27f,
                            side ? .25f : .15f, side ? .46f : .29f),
                            WorkKey(phase, -.42f, -.34f, -.46f), 0f);
                        toolAngle = WorkKey(phase, side ? -28f : -18f, side ? 24f : 20f,
                            side ? -108f : back ? -48f : -165f);
                        break;
                    case HumanWorkMotion.Mine:
                        lean = WorkKey(phase, 3f, -3f, 9f);
                        bodyDrop = WorkKey(phase, 0f, 0f, .04f);
                        hand = new Vector3(WorkKey(phase, side ? .02f : .18f,
                            side ? -.02f : .13f, side ? .09f : back ? .18f : .14f),
                            WorkKey(phase, back ? -.48f : -.40f, back ? -.48f : -.43f,
                                back ? -.51f : side ? -.44f : -.32f), 0f);
                        toolAngle = WorkKey(phase, side ? -25f : -15f, side ? 35f : 25f,
                            side ? -112f : back ? -48f : -165f);
                        break;
                    case HumanWorkMotion.Soil:
                        lean = WorkKey(phase, 5f, 2f, 11f);
                        bodyDrop = WorkKey(phase, .01f, 0f, .045f);
                        hand = new Vector3(WorkKey(phase, side ? .02f : back ? .16f : .10f,
                            side ? -.04f : back ? .12f : .07f, side ? .09f : back ? .18f : .14f),
                            back ? -.49f : side ? -.41f : -.31f, 0f);
                        toolAngle = WorkKey(phase, side ? -80f : back ? -15f : -155f,
                            side ? -38f : back ? -45f : -110f,
                            side ? -118f : back ? -5f : -178f);
                        break;
                    case HumanWorkMotion.Sow:
                    case HumanWorkMotion.Tend:
                        leftArm = WorkKey(phase, 8f, 20f, 14f);
                        rightArm = WorkKey(phase, -6f, -27f, -10f);
                        lean = WorkKey(phase, 4f, 2f, 10f);
                        reach = WorkKey(phase, .02f, 0f, .10f);
                        bodyDrop = WorkKey(phase, .01f, 0f, .04f);
                        break;
                    case HumanWorkMotion.Harvest:
                        leftArm = WorkKey(phase, 14f, 8f, 22f);
                        lean = WorkKey(phase, 5f, 3f, 11f);
                        hand = new Vector3(WorkKey(phase, side ? .31f : .27f,
                            side ? .39f : .34f, side ? .22f : .16f), -.45f, 0f);
                        toolAngle = WorkKey(phase, side ? -65f : -130f, side ? -105f : -165f,
                            side ? -30f : back ? -30f : -95f);
                        bodyDrop = WorkKey(phase, .015f, 0f, .04f);
                        break;
                    case HumanWorkMotion.Build:
                        leftArm = 12f;
                        rightArm = WorkKey(phase, -12f, -35f, 6f);
                        lean = WorkKey(phase, 3f, 0f, 6f);
                        reach = WorkKey(phase, .01f, 0f, .06f);
                        break;
                    case HumanWorkMotion.BoardThink:
                    case HumanWorkMotion.BoardPlace:
                    case HumanWorkMotion.Meditate:
                        // Seated cushion pose: lower the torso and fold both legs outward.
                        // The autonomous AI only enters this motion after physical arrival.
                        leftArm = 25f; rightArm = -25f; bodyDrop = .23f;
                        if (workMotion == HumanWorkMotion.BoardPlace)
                        { rightArm = WorkKey(phase, -25f, -55f, -30f); reach = WorkKey(phase, 0f, .08f, 0f); }
                        _leftLeg.localPosition = new Vector3(-.08f, .43f, 0f);
                        _rightLeg.localPosition = new Vector3(.08f, .43f, 0f);
                        _leftLeg.localRotation = Quaternion.Euler(0f, 0f, -67f);
                        _rightLeg.localRotation = Quaternion.Euler(0f, 0f, 67f);
                        _leftLeg.localScale = _rightLeg.localScale = new Vector3(1f, .82f, 1f);
                        break;
                    case HumanWorkMotion.Eat:
                        leftArm = 10f; rightArm = WorkKey(phase, -20f, -45f, -50f);
                        break;
                    default:
                        leftArm = WorkKey(phase, 4f, 12f, -2f);
                        rightArm = WorkKey(phase, -4f, -18f, 5f);
                        lean = WorkKey(phase, 0f, 1f, 4f);
                        break;
                }
            }
            var pulse = attacking ? clock < .1f ? Smooth(clock / .1f) : 1f - Smooth((clock - .1f) / .26f) : 0f;
            if (attacking)
            {
                if (attackStyle == HumanAttackStyle.Thrust)
                {
                    hand = new Vector3(side ? .02f + .12f * pulse : .14f,
                        back ? -.48f + .08f * pulse : side ? -.34f : -.32f - .02f * pulse, 0f);
                    toolAngle = side ? -90f : back ? 0f : 180f;
                }
                else if (attackStyle == HumanAttackStyle.Cast)
                { leftArm = 35f * pulse; rightArm = -35f * pulse; reach = .16f * pulse; }
                else
                {
                    leftArm = -12f * pulse; rightArm = 38f * pulse;
                    hand += new Vector3(side ? .10f * pulse : 0f, -.04f * pulse, 0f);
                    toolAngle = Mathf.Lerp(15f, side ? -110f : back ? -55f : -160f, pulse);
                }
                lean = -5f * pulse;
            }
            if (carryingCargo || carryingPerson)
            { leftArm = 16f - 2f * stride; rightArm = -16f + 2f * stride; reach = .08f; }
            // Rest hands/limbs get their own complete pose after the ordinary tool branch.
            if (resting) { leftArm = 0f; rightArm = 0f; lean = 0f; reach = 0f; }
            if (side && (working || carryingCargo || carryingPerson ||
                attacking && attackStyle == HumanAttackStyle.Cast))
            {
                // With a vertical neutral arm, positive Z reaches toward the right-facing head.
                // Front-view paired +/- gestures otherwise send the near hand behind the back.
                rightArm = -rightArm;
            }
            if (side)
                rightArm += Mathf.Atan2(reach, rightGrip.magnitude) * Mathf.Rad2Deg;
            Rotate(_leftArm, leftNeutralAngle + leftArm, blend);
            Rotate(_rightArm, rightNeutralAngle + rightArm, blend);
            Scale(_leftArm, Vector3.one, blend);
            Scale(_rightArm, new Vector3(1f,
                side ? 1f : Mathf.Clamp(1f + (back ? -reach : reach) / rightGrip.magnitude,
                    MinArmStretch, MaxArmStretch), 1f), blend);
            Rotate(_upperBody, side ? -lean : lean * .15f, blend);
            // Torso image and sockets change together on a direction switch. Interpolating
            // shoulder positions independently would briefly detach the sleeve again.
            _leftArm.localPosition = leftShoulder;
            _rightArm.localPosition = rightShoulder;
            Move(_upperBody, new Vector3(side ? 0f : .018f * stride,
                .94f + .028f * Mathf.Abs(stride) - bodyDrop, 0f), blend);
            var breathing = action == HostStepAArt.Action.Idle && !_lifePose && !_sleepPose ? .003f * Mathf.Sin(clock * 2f) : 0f;
            Move(_footAnchor, new Vector3(0f, breathing, 0f), blend);

            var chosen = _lifePose || _sleepPose || carryingCargo || carryingPerson ||
                attacking && (attackStyle == HumanAttackStyle.Cast || attackStyle == HumanAttackStyle.Unarmed) ? Equipment.None :
                working ? equipment : _equipmentBinding != null ? _equipmentBinding.WeaponKind : Equipment.None;
            var index = chosen == Equipment.Spear ? 0 : chosen == Equipment.Axe ? 1 :
                chosen == Equipment.Hoe ? 2 : chosen == Equipment.Pick ? 3 :
                chosen == Equipment.Sickle ? 4 : -1;
            var fallback = index >= 0 && _equipment != null ? _equipment[index] : null;
            var attachment = _equipmentBinding == null ? HumanCommonRigEquipment.Default(fallback, chosen) :
                working ? _equipmentBinding.ResolveWorkTool(workMotion, fallback, chosen) : _equipmentBinding.ResolveWeapon(fallback);
            if (_lifePose || _sleepPose || carryingCargo || carryingPerson ||
                attacking && (attackStyle == HumanAttackStyle.Cast || attackStyle == HumanAttackStyle.Unarmed))
                attachment.sprite = null;
            var toolParent = working ? _toolSlot : _weaponSlot;
            if (_toolSprite.transform.parent != toolParent)
            {
                _toolSprite.transform.SetParent(toolParent, false);
                toolParent.localPosition = hand;
                toolParent.localRotation = Quaternion.Euler(0f, 0f, toolAngle);
            }
            if (!working && !attacking && attachment.twoHanded)
                hand = new Vector3(side ? .03f : .19f, back ? -.48f : -.42f, 0f);
            Move(toolParent, hand, blend);
            Rotate(toolParent, toolAngle, blend);
            ApplyAttachment(attachment);
            _supportHandActive = attachment.sprite != null && attachment.twoHanded;
            if (attachment.sprite != null)
            {
                var supportOffset = Vector3.zero;
                if (_supportHandActive)
                {
                    var sprite = attachment.sprite;
                    var supportFromPrimary = new Vector3(
                        (attachment.supportGrip01.x - attachment.grip01.x) * sprite.rect.width / sprite.pixelsPerUnit,
                        (attachment.supportGrip01.y - attachment.grip01.y) * sprite.rect.height / sprite.pixelsPerUnit, 0f);
                    supportOffset = toolParent.localRotation * (_toolSprite.transform.localRotation *
                        Vector3.Scale(supportFromPrimary, _toolSprite.transform.localScale));
                }
                // Keep the shoulder sockets fixed. Constrain the actual, smoothed tool pose,
                // so neither interpolation nor a second hand can pull a sleeve off the torso.
                toolParent.localPosition = ConstrainToolGrip(toolParent.localPosition,
                    rightShoulder, rightGrip, leftShoulder, leftGrip, supportOffset, _supportHandActive);
                PoseArmToGrip(_rightArm, rightGrip, toolParent.localPosition, rightShoulder);
                if (_supportHandActive)
                {
                    var target = toolParent.localPosition + supportOffset;
                    // An explicitly overridden attachment can have physically impossible grip
                    // spacing. Release the support hand instead of faking a stretched joint.
                    _supportHandActive = IsArmReachable(target, leftShoulder, leftGrip);
                    if (_supportHandActive) PoseArmToGrip(_leftArm, leftGrip, target, leftShoulder);
                }
            }
            if (aimDirection.HasValue && !resting && !carryingCargo && !carryingPerson)
            {
                var localAim = _rightArm.parent.InverseTransformVector(aimDirection.Value);
                localAim.z = 0f;
                if (localAim.sqrMagnitude > .0001f)
                    PoseArmToGrip(_rightArm, rightGrip,
                        rightShoulder + localAim.normalized * rightGrip.magnitude, rightShoulder);
                _toolSprite.enabled = false;
            }
            _basketSprite.sprite = (_hasBasket || carryingCargo) && _equipment != null ? _equipment[5] : null;
            _basketSprite.enabled = !_lifePose && !_sleepPose && (back || side) && _basketSprite.sprite != null;
            if (resting)
            {
                ApplyRestPose(restPose, _usingBedFrame, leftShoulder, rightShoulder);
                PlaceRestPose(restPose, pillowWorld, bedRotation);
                if (_bedEntryActive)
                {
                    _bedEntryElapsed = Mathf.Min(BedEntrySeconds,
                        _bedEntryElapsed + Mathf.Max(0f, deltaTime));
                    var entryBlend = Smooth(_bedEntryElapsed / BedEntrySeconds);
                    transform.position = Vector3.Lerp(_bedEntryOrigin, transform.position, entryBlend);
                    transform.rotation = Quaternion.Slerp(_bedEntryRotation, transform.rotation, entryBlend);
                    if (_bedEntryElapsed >= BedEntrySeconds) _bedEntryActive = false;
                }
            }
            _lastAction = action;
            _lastWorkMotion = workMotion;
            _lastDirection = direction;
            _lastRestPose = restPose;
            _lastUsingBedFrame = _usingBedFrame;
            _hasAppliedFrame = true;
        }

        void RestoreUprightBones()
        {
            _head.localRotation = Quaternion.identity;
            _torso.localRotation = Quaternion.identity;
            _upperBody.localPosition = new Vector3(0f, .94f, 0f);
            _upperBody.localRotation = Quaternion.identity;
            _upperBody.localScale = Vector3.one;
            _leftLeg.localPosition = new Vector3(-.14f, .72f, 0f);
            _rightLeg.localPosition = new Vector3(.14f, .72f, 0f);
            _leftLeg.localRotation = _rightLeg.localRotation = Quaternion.identity;
            _leftLeg.localScale = _rightLeg.localScale = Vector3.one;
            _leftArm.localRotation = Quaternion.Euler(0f, 0f, _leftNeutralAngle);
            _rightArm.localRotation = Quaternion.Euler(0f, 0f, _rightNeutralAngle);
            _leftArm.localScale = _rightArm.localScale = Vector3.one;
            _walkBlend = 0f;
        }

        void ApplyRestPose(RestPose pose, bool onBed, Vector3 leftShoulder, Vector3 rightShoulder)
        {
            // One static rest rig for every skin: only a few angles/hand targets differ.
            // Whole painted arms/legs remain single bones; these are relaxed limb poses,
            // not simulated elbows/knees, and the open-eye texture is deliberately unchanged.
            var dead = pose == RestPose.Dead;
            var downed = pose == RestPose.Downed;
            var carried = pose == RestPose.Carried;
            var hip = onBed ? .12f : .065f;
            var leftLegAngle = onBed ? -6f : carried ? 31f : downed ? 23f : dead ? 4f : 16f;
            var rightLegAngle = onBed ? 8f : carried ? 16f : downed ? -7f : dead ? -9f : 5f;
            _leftLeg.localPosition = new Vector3(-hip, .70f, 0f);
            _rightLeg.localPosition = new Vector3(hip, .72f, 0f);
            _leftLeg.localRotation = Quaternion.Euler(0f, 0f, leftLegAngle);
            _rightLeg.localRotation = Quaternion.Euler(0f, 0f, rightLegAngle);
            _leftLeg.localScale = new Vector3(1f, carried ? .88f : downed ? .92f : .96f, 1f);
            _rightLeg.localScale = new Vector3(1f, carried ? .91f : .98f, 1f);
            _upperBody.localPosition = new Vector3(0f, carried ? .88f : .91f, 0f);
            _upperBody.localRotation = Quaternion.Euler(0f, 0f,
                onBed ? downed ? -3f : 0f : carried ? -7f : downed ? -6f : dead ? -2f : -4f);
            _head.localRotation = Quaternion.Euler(0f, 0f,
                onBed ? downed ? -8f : 3f : carried ? 12f : downed ? 14f : dead ? -11f : 8f);
            _footAnchor.localPosition = Vector3.zero;
            _walkBlend = 0f;

            // Relaxed hands rest across the lower chest/abdomen. Keep the calibrated shoulder
            // sockets fixed and respect each painted arm's reachable length, just as tools do.
            var leftTarget = new Vector3(onBed ? .045f : .12f, downed ? -.32f : -.24f, 0f);
            var rightTarget = new Vector3(onBed ? -.045f : .07f, downed ? -.29f : -.28f, 0f);
            if (dead)
            {
                leftTarget = leftShoulder + new Vector3(-.09f, -_leftGrip.magnitude * .94f, 0f);
                rightTarget = rightShoulder + new Vector3(.12f, -_rightGrip.magnitude * .96f, 0f);
            }
            if (carried)
            {
                leftTarget.y = -.20f;
                rightTarget.y = -.24f;
            }
            PoseArmToGrip(_leftArm, _leftGrip,
                ClampArmReach(leftTarget, leftShoulder, _leftGrip), leftShoulder);
            PoseArmToGrip(_rightArm, _rightGrip,
                ClampArmReach(rightTarget, rightShoulder, _rightGrip), rightShoulder);
            _toolSprite.enabled = false;
            _basketSprite.enabled = false;
            _supportHandActive = false;
        }

        void PlaceRestPose(RestPose pose, Vector3 pillowWorld, Quaternion bedRotation)
        {
            if (_usingBedFrame)
            {
                transform.rotation = bedRotation;
                // Use the current head after its skin, pose and proportion transforms. No fixed
                // bed offset or standing-height assumption can survive here across quarter turns.
                var support = _head.TransformPoint(_headSprite.sprite.bounds.center);
                var correction = pillowWorld - support;
                correction.z = 0f;
                transform.position += correction;
                return;
            }

            // Ground rest and transported bodies keep their existing Core point, with only this
            // visual child centered around it. A missing bed never draws a phantom bed/actor.
            var quarter = pose == RestPose.Sleeping && !_sleepOnBed ? _sleepQuarterTurns : 0;
            var angle = pose == RestPose.Carried ? 76f : pose == RestPose.Dead ? 88f : 83f;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f * quarter);
            var anchor = pose == RestPose.Sleeping && !_sleepOnBed ? _sleepWorldPosition :
                transform.parent != null ? transform.parent.position : transform.position;
            if (!TryGetVisibleBodyBounds(out var body)) return;
            var groundCorrection = anchor - body.center;
            groundCorrection.z = 0f;
            transform.position += groundCorrection;
        }

        static float Smooth(float value)
        {
            var t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        static float WorkCycleSeconds(HumanWorkMotion motion)
        {
            switch (motion)
            {
                case HumanWorkMotion.Chop: return .9f;
                case HumanWorkMotion.Mine: return 1.02f;
                case HumanWorkMotion.Soil: return 1.2f;
                case HumanWorkMotion.Tend: return 1.4f;
                case HumanWorkMotion.Eat: return 1.8f;
                default: return 1.15f;
            }
        }

        static float WorkKey(float phase, float ready, float prepared, float contact)
        {
            // Quiet preparation, a quicker stroke, a brief contact hold, then a slower recovery.
            // This is visual timing only; neither production nor hits are triggered by this curve.
            if (phase < .28f) return Mathf.Lerp(ready, prepared, Smooth(phase / .28f));
            if (phase < .44f) return Mathf.Lerp(prepared, contact, Smooth((phase - .28f) / .16f));
            if (phase < .56f) return contact;
            return Mathf.Lerp(contact, ready, Smooth((phase - .56f) / .36f));
        }

        static void PoseNorthLeg(Transform leg, Sprite sprite, float hipX, float stride, float blend)
        {
            var lift = Mathf.Max(0f, stride);
            var angle = -6f * stride;
            var stretch = 1f - .14f * lift;
            // Compensate the whole-leg rotation so a planted shoe stays at the same foot line.
            var length = sprite.pivot.y / sprite.pixelsPerUnit;
            var hipY = .72f - length * (1f - Mathf.Cos(angle * Mathf.Deg2Rad)) + .035f * lift;
            Rotate(leg, angle, blend);
            Move(leg, new Vector3(hipX + .012f * stride, hipY, 0f), blend);
            Scale(leg, new Vector3(1f, stretch, 1f), blend);
        }

        static float PoseWalkingLeg(Transform leg, Sprite sprite, float hipX, float phase,
            bool side, float weight, out float footHeight)
        {
            var cycle = Mathf.Repeat(phase, 1f);
            float travel, lift;
            if (cycle < .5f)
            {
                // Grounded half: the shoe travels backward at an even rate relative to the hips.
                travel = 1f - cycle * 4f;
                lift = 0f;
            }
            else
            {
                // Passing half: clear the planted shoe instead of hiding both legs in one outline.
                var swing = (cycle - .5f) * 2f;
                travel = Mathf.Lerp(-1f, 1f, Smooth(swing));
                var arc = Mathf.Sin(swing * Mathf.PI);
                lift = arc * arc;
            }
            var length = Mathf.Max(.001f, sprite.pivot.y / sprite.pixelsPerUnit);
            var footX = (side ? .30f : .015f) * travel * weight;
            var footRise = (side ? .16f * lift : -.08f * travel + .12f * lift) * weight;
            var legHeight = length - footRise;
            var angle = Mathf.Atan2(footX, legHeight) * Mathf.Rad2Deg;
            var stretch = Mathf.Sqrt(footX * footX + legHeight * legHeight) / length;
            // The common displacement phase is already continuous and weight handles starting/stopping.
            // Filtering these bones a second time reduces the short passing step at normal camera scale.
            leg.localPosition = new Vector3(hipX, .72f, 0f);
            leg.localRotation = Quaternion.Euler(0f, 0f, angle);
            leg.localScale = new Vector3(1f, stretch, 1f);
            footHeight = .72f - length + footRise;
            return travel;
        }

        static void PoseArmToGrip(Transform arm, Vector3 grip, Vector3 target, Vector3 shoulder)
        {
            arm.localPosition = shoulder;
            var offset = target - shoulder;
            var stretch = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(.0001f,
                offset.sqrMagnitude - grip.x * grip.x)) / Mathf.Max(.001f, Mathf.Abs(grip.y)),
                MinArmStretch, MaxArmStretch);
            var scaledGrip = new Vector2(grip.x, grip.y * stretch);
            var angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg -
                Mathf.Atan2(scaledGrip.y, scaledGrip.x) * Mathf.Rad2Deg;
            arm.localRotation = Quaternion.Euler(0f, 0f, angle);
            arm.localScale = new Vector3(1f, stretch, 1f);
        }

        static void ArmReach(Vector3 grip, out float minimum, out float maximum)
        {
            minimum = new Vector2(grip.x, grip.y * MinArmStretch).magnitude;
            maximum = new Vector2(grip.x, grip.y * MaxArmStretch).magnitude;
        }

        static bool IsArmReachable(Vector3 target, Vector3 shoulder, Vector3 grip)
        {
            ArmReach(grip, out var minimum, out var maximum);
            var distance = (target - shoulder).magnitude;
            return distance >= minimum - .0001f && distance <= maximum + .0001f;
        }

        static Vector3 ClampArmReach(Vector3 target, Vector3 shoulder, Vector3 grip)
        {
            ArmReach(grip, out var minimum, out var maximum);
            var delta = target - shoulder;
            var distance = delta.magnitude;
            return shoulder + (distance > .0001f ? delta / distance : Vector3.down) *
                Mathf.Clamp(distance, minimum, maximum);
        }

        static Vector3 ConstrainToolGrip(Vector3 desired, Vector3 primaryShoulder, Vector3 primaryGrip,
            Vector3 supportShoulder, Vector3 supportGrip, Vector3 supportOffset, bool twoHanded)
        {
            var primary = ClampArmReach(desired, primaryShoulder, primaryGrip);
            if (!twoHanded || IsArmReachable(primary + supportOffset, supportShoulder, supportGrip)) return primary;
            ArmReach(primaryGrip, out var primaryMin, out var primaryMax);
            ArmReach(supportGrip, out var supportMin, out var supportMax);
            var supportCenter = supportShoulder - supportOffset;
            var found = false;
            var best = primary;
            var bestDistance = float.PositiveInfinity;
            // The feasible tool position is the intersection of two reachable annuli.
            // A nearest point is either a radial projection onto a boundary or a pair of
            // circle intersections. Enumerating those points avoids iterative shoulder drift.
            ConsiderGripCandidate(ProjectToCircle(desired, primaryShoulder, primaryMin), desired,
                primaryShoulder, primaryGrip, supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
            ConsiderGripCandidate(ProjectToCircle(desired, primaryShoulder, primaryMax), desired,
                primaryShoulder, primaryGrip, supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
            ConsiderGripCandidate(ProjectToCircle(desired, supportCenter, supportMin), desired,
                primaryShoulder, primaryGrip, supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
            ConsiderGripCandidate(ProjectToCircle(desired, supportCenter, supportMax), desired,
                primaryShoulder, primaryGrip, supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
            ConsiderCircleIntersections(primaryShoulder, primaryMin, supportCenter, supportMin, desired,
                primaryGrip, supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
            ConsiderCircleIntersections(primaryShoulder, primaryMin, supportCenter, supportMax, desired,
                primaryGrip, supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
            ConsiderCircleIntersections(primaryShoulder, primaryMax, supportCenter, supportMin, desired,
                primaryGrip, supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
            ConsiderCircleIntersections(primaryShoulder, primaryMax, supportCenter, supportMax, desired,
                primaryGrip, supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
            return found ? best : primary;
        }

        static Vector3 ProjectToCircle(Vector3 point, Vector3 center, float radius)
        {
            var delta = point - center;
            return center + (delta.sqrMagnitude > .00000001f ? delta.normalized : Vector3.down) * radius;
        }

        static void ConsiderGripCandidate(Vector3 point, Vector3 desired, Vector3 primaryShoulder, Vector3 primaryGrip,
            Vector3 supportShoulder, Vector3 supportGrip, Vector3 supportOffset,
            ref bool found, ref Vector3 best, ref float bestDistance)
        {
            if (!IsArmReachable(point, primaryShoulder, primaryGrip) ||
                !IsArmReachable(point + supportOffset, supportShoulder, supportGrip)) return;
            var distance = (point - desired).sqrMagnitude;
            if (distance >= bestDistance) return;
            found = true;
            best = point;
            bestDistance = distance;
        }

        static void ConsiderCircleIntersections(Vector3 primaryCenter, float primaryRadius,
            Vector3 supportCenter, float supportRadius, Vector3 desired, Vector3 primaryGrip,
            Vector3 supportShoulder, Vector3 supportGrip, Vector3 supportOffset,
            ref bool found, ref Vector3 best, ref float bestDistance)
        {
            var delta = supportCenter - primaryCenter;
            var distance = delta.magnitude;
            if (distance < .0001f || distance > primaryRadius + supportRadius + .0001f ||
                distance < Mathf.Abs(primaryRadius - supportRadius) - .0001f) return;
            var along = (primaryRadius * primaryRadius - supportRadius * supportRadius + distance * distance) / (2f * distance);
            var height = Mathf.Sqrt(Mathf.Max(0f, primaryRadius * primaryRadius - along * along));
            var axis = delta / distance;
            var center = primaryCenter + axis * along;
            var perpendicular = new Vector3(-axis.y, axis.x, 0f) * height;
            ConsiderGripCandidate(center + perpendicular, desired, primaryCenter, primaryGrip,
                supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
            ConsiderGripCandidate(center - perpendicular, desired, primaryCenter, primaryGrip,
                supportShoulder, supportGrip, supportOffset, ref found, ref best, ref bestDistance);
        }

        static void Rotate(Transform bone, float degrees, float blend) =>
            bone.localRotation = Quaternion.Lerp(bone.localRotation, Quaternion.Euler(0f, 0f, degrees), blend);
        static void Move(Transform bone, Vector3 position, float blend) =>
            bone.localPosition = Vector3.Lerp(bone.localPosition, position, blend);
        static void Scale(Transform bone, Vector3 scale, float blend) =>
            bone.localScale = Vector3.Lerp(bone.localScale, scale, blend);

        void ApplyAttachment(HumanCommonRigEquipment.Attachment attachment)
        {
            var sprite = attachment.sprite;
            _toolSprite.sprite = sprite;
            _toolSprite.enabled = sprite != null;
            if (sprite == null) return;
            var scale = attachment.height > 0f ? attachment.height / (sprite.rect.height / sprite.pixelsPerUnit) : 1f;
            _toolSprite.transform.localScale = Vector3.one * scale;
            var rotation = Quaternion.Euler(0f, 0f, attachment.angle);
            _toolSprite.transform.localPosition = rotation * new Vector3(
                (sprite.pivot.x - sprite.rect.width * attachment.grip01.x) / sprite.pixelsPerUnit * scale,
                (sprite.pivot.y - sprite.rect.height * attachment.grip01.y) / sprite.pixelsPerUnit * scale, 0f);
            _toolSprite.transform.localRotation = rotation;
        }

        public void SetLifePose(bool downed, bool dead, bool carried = false)
        {
            _downedPose = downed;
            _deadPose = dead;
            _carriedPose = carried;
            _lifePose = downed || dead || carried;
            if (_lifePose) _hasPendingWorkContact = false;
            if (dead || carried) _usingBedFrame = false;
        }

        public void SetSleepPose(bool sleeping, Vector3 worldPosition, int quarterTurns = 0, bool onBed = false,
            HostBedVisualLayers bedVisual = null)
        {
            _sleepPose = sleeping;
            if (sleeping) _hasPendingWorkContact = false;
            _sleepWorldPosition = worldPosition;
            _sleepQuarterTurns = ((quarterTurns % 4) + 4) % 4;
            _sleepOnBed = sleeping && onBed;
            _bedVisual = _sleepOnBed ? bedVisual : null;
            // Positioning happens after this frame's bones in Apply; SetLifePose may be called
            // later in the same frame and must get the final say over stale sleep/bed flags.
            if (!sleeping) _usingBedFrame = false;
        }

        public void SetTint(Color color)
        {
            if (_renderers == null || _hasTint && _appliedTint.Equals(color)) return;
            _hasTint = true;
            _appliedTint = color;
            foreach (var renderer in _renderers)
                if (renderer != null) renderer.color = color;
        }

        public void SetDepth(int order, HostStepAArt.Direction direction, int sortingLayerId = 0)
        {
            if (_torsoSprite == null) return;
            if (_sleepPose || _lifePose) direction = _lastDirection;
            var back = direction == HostStepAArt.Direction.North ||
                direction == HostStepAArt.Direction.NorthEast || direction == HostStepAArt.Direction.NorthWest;
            var side = direction == HostStepAArt.Direction.East || direction == HostStepAArt.Direction.West ||
                direction == HostStepAArt.Direction.NorthEast || direction == HostStepAArt.Direction.NorthWest ||
                direction == HostStepAArt.Direction.SouthEast || direction == HostStepAArt.Direction.SouthWest;
            var leftFootInFront = !back && !side && _walkBlend > .01f &&
                _leftFootHeight < _rightFootHeight - .001f;
            var leftLegOrder = leftFootInFront ? -1 : -2;
            var leftArmOrder = _sleepPose || _lifePose ? 2 :
                back ? -2 : _supportHandActive ? 3 : side ? -2 : 1;
            var depth = (order, sortingLayerId, direction, _usingBedFrame, leftLegOrder, leftArmOrder);
            if (_hasDepth && _appliedDepth.Equals(depth)) return;
            _hasDepth = true;
            _appliedDepth = depth;
            _sortingGroup.sortingLayerID = sortingLayerId;
            _sortingGroup.sortingOrder = order;
            if (_bedHeadSortingGroup != null)
            {
                _bedHeadSortingGroup.enabled = _usingBedFrame;
                if (_usingBedFrame)
                {
                    _bedHeadSortingGroup.sortingLayerID = sortingLayerId;
                    _bedHeadSortingGroup.sortingOrder = order + 2;
                }
            }
            _basketSprite.sortingOrder = back ? 3 : -3;
            // Front-view shoes still exchange depth during the passing step.
            _leftLegSprite.sortingOrder = leftLegOrder;
            _rightLegSprite.sortingOrder = leftFootInFront ? -2 : -1;
            _leftArmSprite.sortingOrder = leftArmOrder;
            _torsoSprite.sortingOrder = 0;
            _headSprite.sortingOrder = 5;
            _rightArmSprite.sortingOrder = back ? -2 : 3;
            _toolSprite.sortingOrder = back ? -3 : 2;
        }
    }
}
