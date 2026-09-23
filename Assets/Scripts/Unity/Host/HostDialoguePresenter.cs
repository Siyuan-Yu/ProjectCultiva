using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// NPC onTalk dialogue host adapter: controller + UGUI view（默认）/ IMGUI 回退。
    /// 其它 ContentEvent 仍走 <see cref="HostContentInterruptPresenter"/>。
    /// </summary>
    public sealed class HostDialoguePresenter : MonoBehaviour
    {
        const string PauseOwner = "Dialogue";
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostDialogueUguiView uguiView;
        [Tooltip("仅调试：强制使用 IMGUI 底栏。")]
        [SerializeField] bool useImguiFallback;

        readonly HostDialogueController _controller = new HostDialogueController();
        readonly HostDialogueImguiView _imguiView = new HostDialogueImguiView();
        bool _holdingPause;

        public bool IsActive => _controller.Model.IsActive;

        public HostDialogueModel Model => _controller.Model;

        public HostDialogueUguiView UguiView => uguiView;

        void Awake()
        {
            if (uguiView == null)
                uguiView = GetComponent<HostDialogueUguiView>();
            if (uguiView == null && !useImguiFallback)
                uguiView = gameObject.AddComponent<HostDialogueUguiView>();
        }

        public void Bind(
            PlayableHostBootstrap host,
            HostCommandBridge bridge,
            HostSelectionController selection)
        {
            bootstrap = host;
            commandBridge = bridge;
            selectionController = selection;
        }

        public void ClearSessionState()
        {
            if (_holdingPause && bootstrap?.Session != null)
                bootstrap.Session.ReleaseModalPause(PauseOwner);
            _controller.Clear();
            _holdingPause = false;
            if (uguiView != null)
                uguiView.Hide();
        }

        public bool TryPresentOnTalk(EntityId actor, EntityId speakerNpc)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized)
                return false;

            var subject = ResolveSubject(session, actor);
            if (!_controller.TryBuildFromActiveOnTalk(session, subject, speakerNpc))
                return false;

            session.AcquireModalPause(PauseOwner);
            _holdingPause = true;
            HostInputGate.BlockWorldInteraction = true;
            return true;
        }

        public bool TryStartInteraction(EntityId actor, EntityId target, string definitionId)
            => TryStartInteraction(ContentInteractionContext.ForNpc(actor, target, definitionId), "onTalk");

        public bool TryStartInteraction(ContentInteractionContext context, string trigger)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized ||
                !_controller.TryStartInteraction(session, context, trigger)) return false;
            session.AcquireModalPause(PauseOwner);
            _holdingPause = true;
            HostInputGate.BlockWorldInteraction = true;
            bootstrap.DispatchDrainedEvents();
            return true;
        }

        public void ShowFallback(string speakerName, string body)
        {
            _controller.ShowFallback(speakerName, body);
            var session = bootstrap?.Session;
            if (session != null && session.IsInitialized)
            {
                session.AcquireModalPause(PauseOwner);
                _holdingPause = true;
            }
            HostInputGate.BlockWorldInteraction = true;
        }

        void Update()
        {
            SyncPause();
            if (!IsActive)
            {
                if (uguiView != null && !useImguiFallback)
                    uguiView.Hide();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) && _controller.Model.CanDismiss)
                DismissFallback();

            if (uguiView != null && !useImguiFallback)
            {
                uguiView.Sync(
                    _controller.Model,
                    OnChoiceSelected,
                    DismissFallback,
                    Time.unscaledDeltaTime);
            }
        }

        void OnGUI()
        {
            if (uguiView != null && !useImguiFallback)
                return;
            if (!IsActive)
                return;
            _imguiView.Draw(_controller.Model, OnChoiceSelected, DismissFallback);
        }

        void OnChoiceSelected(int index)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized)
                return;

            var wasActive = IsActive;
            _controller.TrySelectChoice(index, session, commandBridge, bootstrap);
            if (wasActive && !IsActive)
            {
                if (_holdingPause)
                {
                    session.ReleaseModalPause(PauseOwner);
                    _holdingPause = false;
                }
                var ttt = bootstrap != null ? bootstrap.TicTacToePanel : null;
                if (ttt == null || !ttt.IsOpen)
                    HostInputGate.BlockWorldInteraction = false;
            }
        }

        void DismissFallback()
        {
            if (!_controller.Model.CanDismiss)
                return;
            _controller.Clear();
            HostInputGate.BlockWorldInteraction = false;
            if (uguiView != null)
                uguiView.Hide();
            var session = bootstrap?.Session;
            if (session != null && session.IsInitialized && _holdingPause)
            {
                session.ReleaseModalPause(PauseOwner);
                _holdingPause = false;
            }
        }

        void OnDisable()
        {
            if (_holdingPause && bootstrap?.Session != null)
                bootstrap.Session.ReleaseModalPause(PauseOwner);
            _holdingPause = false;
        }

        void SyncPause()
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized)
            {
                _holdingPause = false;
                return;
            }

            if (IsActive)
            {
                if (!_holdingPause)
                {
                    session.AcquireModalPause(PauseOwner);
                    _holdingPause = true;
                }
            }
            else if (_holdingPause)
            {
                session.ReleaseModalPause(PauseOwner);
                _holdingPause = false;
            }
        }

        EntityId ResolveSubject(PlayableHostSession session, EntityId actor)
        {
            if (!actor.IsNone)
                return actor;
            if (selectionController != null && selectionController.State.Count > 0)
            {
                var id = selectionController.State.SelectedIds[0];
                if (!id.IsNone)
                    return id;
            }

            return session.CharacterIds.Count > 0 ? session.CharacterIds[0] : EntityId.None;
        }
    }
}
