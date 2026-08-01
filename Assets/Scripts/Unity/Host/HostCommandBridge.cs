using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;
using XianXia.Core.Results;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// VS0.4 Phase D: selection → PlayerCommandRequest → IPlayerInputPort.
    /// No direct component mutation. No Move／combat／HUD.
    /// </summary>
    public sealed class HostCommandBridge : MonoBehaviour
    {
        public const ulong DefaultDurationTicks = 4;

        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool enableDebugKeys = true;
        [SerializeField] bool showDebugButtons = true;
        [SerializeField] KeyCode laborKey = KeyCode.Alpha1;
        [SerializeField] KeyCode restKey = KeyCode.Alpha2;
        [SerializeField] KeyCode observeKey = KeyCode.Alpha3;
        [SerializeField] KeyCode cultivateKey = KeyCode.Alpha4;

        PlayableHostSession _session;
        string _lastStatus = "No command yet";
        int _lastSuccessCount;
        int _lastFailureCount;

        public string LastStatus => _lastStatus;

        public int LastSuccessCount => _lastSuccessCount;

        public int LastFailureCount => _lastFailureCount;

        public void Bind(PlayableHostSession session, HostSelectionController selection)
        {
            _session = session;
            selectionController = selection;
            _lastStatus = "Bound";
            _lastSuccessCount = 0;
            _lastFailureCount = 0;
        }

        void Update()
        {
            if (!enableDebugKeys || _session == null || !_session.IsInitialized)
                return;

            if (Input.GetKeyDown(laborKey))
                IssueSelected(PlayerCommandKind.Labor);
            else if (Input.GetKeyDown(restKey))
                IssueSelected(PlayerCommandKind.Rest);
            else if (Input.GetKeyDown(observeKey))
                IssueSelected(PlayerCommandKind.Observe);
            else if (Input.GetKeyDown(cultivateKey))
                IssueSelected(PlayerCommandKind.Cultivate);
        }

        void OnGUI()
        {
            if (!showDebugButtons || _session == null || !_session.IsInitialized)
                return;

            const float w = 88f;
            const float h = 28f;
            var y = 8f;
            if (GUI.Button(new Rect(8f, y, w, h), "劳动(1)"))
                IssueSelected(PlayerCommandKind.Labor);
            if (GUI.Button(new Rect(8f + (w + 6f), y, w, h), "休息(2)"))
                IssueSelected(PlayerCommandKind.Rest);
            if (GUI.Button(new Rect(8f + 2f * (w + 6f), y, w, h), "观察(3)"))
                IssueSelected(PlayerCommandKind.Observe);
            if (GUI.Button(new Rect(8f + 3f * (w + 6f), y, w, h), "修炼(4)"))
                IssueSelected(PlayerCommandKind.Cultivate);
        }

        /// <summary>Issue to current selection. Empty selection = no-op.</summary>
        public int IssueSelected(PlayerCommandKind kind, ulong durationTicks = DefaultDurationTicks)
        {
            if (selectionController == null)
            {
                _lastStatus = "No selection controller";
                _lastSuccessCount = 0;
                _lastFailureCount = 0;
                return 0;
            }

            return IssueTo(selectionController.State.SelectedIds, kind, durationTicks);
        }

        /// <summary>
        /// Batch submit: one request per target. Failures do not abort the batch.
        /// Only CharacterIds from the session are accepted.
        /// </summary>
        public int IssueTo(
            IReadOnlyList<EntityId> targets,
            PlayerCommandKind kind,
            ulong durationTicks = DefaultDurationTicks)
        {
            _lastSuccessCount = 0;
            _lastFailureCount = 0;

            if (_session == null || !_session.IsInitialized || _session.Port == null)
            {
                _lastStatus = "Session／Port not ready";
                return 0;
            }

            if (targets == null || targets.Count == 0)
            {
                _lastStatus = "Empty selection";
                return 0;
            }

            if (durationTicks == 0)
            {
                _lastStatus = "DurationTicks must be > 0";
                return 0;
            }

            var allowed = BuildAllowedSet(_session.CharacterIds);
            for (var i = 0; i < targets.Count; i++)
            {
                var id = targets[i];
                if (id.IsNone || !allowed.Contains(id.Value))
                {
                    _lastFailureCount++;
                    Debug.LogWarning("[HostCommand] Skip non-controllable entity: " + id, this);
                    continue;
                }

                var result = _session.Port.Submit(new PlayerCommandRequest(id, kind, durationTicks));
                if (result.IsSuccess)
                {
                    _lastSuccessCount++;
                }
                else
                {
                    _lastFailureCount++;
                    Debug.LogWarning(
                        "[HostCommand] Submit failed kind=" + kind + " entity=" + id + " err=" + FormatError(result),
                        this);
                }
            }

            _lastStatus = "kind=" + kind +
                          " ok=" + _lastSuccessCount +
                          " fail=" + _lastFailureCount;
            return _lastSuccessCount;
        }

        static HashSet<ulong> BuildAllowedSet(IReadOnlyList<EntityId> characterIds)
        {
            var set = new HashSet<ulong>();
            if (characterIds == null)
                return set;
            for (var i = 0; i < characterIds.Count; i++)
            {
                if (!characterIds[i].IsNone)
                    set.Add(characterIds[i].Value);
            }

            return set;
        }

        static string FormatError(Result result) =>
            result.IsFailure ? result.Error.ToString() : string.Empty;
    }
}
