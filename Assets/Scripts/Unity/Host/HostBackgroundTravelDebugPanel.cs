using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Phase 2D：Background Character World Travel 开发验收（非正式 Gameplay）。</summary>
    public sealed class HostBackgroundTravelDebugPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.F12;
        [SerializeField] string targetSiteId = "base:site_chengzhen";
        [SerializeField] int targetHexQ = 10;
        [SerializeField] int targetHexR = 4;

        string _status = "F12 Background Travel DEBUG";

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            const float pad = 8f;
            var width = 540f;
            var height = 360f;
            var rect = new Rect(pad, pad + 80f, width, height);
            GUI.Box(rect, "Background Travel DEBUG (F12)");

            var x = rect.x + 8f;
            var y = rect.y + 24f;
            var w = rect.width - 16f;

            GUI.Label(new Rect(x, y, w, 20f), DescribeFocus());
            y += 24f;

            GUI.Label(new Rect(x, y, 80f, 22f), "SiteId");
            targetSiteId = GUI.TextField(new Rect(x + 84f, y, w - 84f, 22f), targetSiteId ?? string.Empty);
            y += 26f;

            GUI.Label(new Rect(x, y, 80f, 22f), "Hex Q/R");
            var qText = GUI.TextField(new Rect(x + 84f, y, 60f, 22f), targetHexQ.ToString());
            var rText = GUI.TextField(new Rect(x + 150f, y, 60f, 22f), targetHexR.ToString());
            int.TryParse(qText, out targetHexQ);
            int.TryParse(rText, out targetHexR);
            y += 30f;

            if (GUI.Button(new Rect(x, y, 150f, 24f), "Travel To WorldSite"))
                TravelToSite();
            if (GUI.Button(new Rect(x + 158f, y, 120f, 24f), "Travel To Hex"))
                TravelToHex();
            if (GUI.Button(new Rect(x + 286f, y, 100f, 24f), "Cancel"))
                CancelTravel();
            y += 30f;

            if (GUI.Button(new Rect(x, y, 120f, 24f), "Advance 8 ticks"))
                AdvanceTicks(8);
            if (GUI.Button(new Rect(x + 128f, y, 120f, 24f), "Advance 32 ticks"))
                AdvanceTicks(32);
            y += 28f;

            GUI.Label(new Rect(x, y, w, 40f), _status);
        }

        EntityId FocusId()
        {
            if (selectionController != null && selectionController.State.Count > 0)
                return selectionController.State.SelectedIds[0];
            var session = bootstrap?.Session;
            if (session?.CharacterIds != null && session.CharacterIds.Count > 1)
                return session.CharacterIds[1];
            if (session?.CharacterIds != null && session.CharacterIds.Count > 0)
                return session.CharacterIds[0];
            return EntityId.None;
        }

        string DescribeFocus()
        {
            var session = bootstrap?.Session;
            var id = FocusId();
            if (session == null || !session.IsInitialized || id.IsNone)
                return "No focus character.";

            if (!BackgroundCharacterTravelService.TryDescribeTravel(
                    session.World,
                    id,
                    session.PlayerParty,
                    out var authority,
                    out var kind,
                    out var siteId,
                    out var pos,
                    out var hex,
                    out var travelKind,
                    out var destHex,
                    out var destSite,
                    out var seg,
                    out var segProgress))
                return "Cannot describe " + id.Value;

            return "Id=" + id.Value +
                   " Authority=" + authority +
                   " Loc=" + kind +
                   (string.IsNullOrEmpty(siteId) ? "" : " Site=" + siteId) +
                   " Pos=(" + pos.X.ToString("F2") + "," + pos.Y.ToString("F2") + ")" +
                   " Hex=" + hex +
                   " Travel=" + travelKind +
                   " DestHex=" + destHex +
                   (string.IsNullOrEmpty(destSite) ? "" : " DestSite=" + destSite) +
                   " Seg=" + seg + " P=" + segProgress.ToString("F2");
        }

        void TravelToSite()
        {
            var session = bootstrap?.Session;
            var id = FocusId();
            if (session == null || id.IsNone)
            {
                _status = "No session/focus.";
                return;
            }

            var result = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                session.World,
                id,
                targetSiteId,
                session.PlayerParty,
                debugOverrideLocalOccupant: true);
            _status = result.IsSuccess ? "Started site travel." : result.Error.ToString();
        }

        void TravelToHex()
        {
            var session = bootstrap?.Session;
            var id = FocusId();
            if (session == null || id.IsNone)
            {
                _status = "No session/focus.";
                return;
            }

            var result = BackgroundCharacterTravelService.BeginTravelToHex(
                session.World,
                id,
                new XianXia.Core.World.Hex.HexCoord(targetHexQ, targetHexR),
                session.PlayerParty,
                debugOverrideLocalOccupant: true);
            _status = result.IsSuccess ? "Started hex travel." : result.Error.ToString();
        }

        void CancelTravel()
        {
            var session = bootstrap?.Session;
            var id = FocusId();
            if (session == null || id.IsNone)
            {
                _status = "No session/focus.";
                return;
            }

            var result = BackgroundCharacterTravelService.CancelTravel(session.World, id);
            _status = result.IsSuccess ? "Canceled." : result.Error.ToString();
        }

        void AdvanceTicks(int ticks)
        {
            var session = bootstrap?.Session;
            if (session?.Loop == null)
            {
                _status = "Loop missing.";
                return;
            }

            for (var i = 0; i < ticks; i++)
                session.Loop.TickOnce();
            _status = "Advanced " + ticks + " ticks. " + DescribeFocus();
        }
    }
}
