using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public sealed partial class HostNpcContextMenu
    {
        Texture2D _battleAttackCursor;
        bool _battleCursorOwned;
        bool CanRtsAttack(EntityId actor, EntityId target)
        {
            if (!HostRpgControls.SceneRts) return false;
            var world = bootstrap?.Session?.World;
            if (world == null || actor.IsNone || target.IsNone ||
                bootstrap.Session.PlayerParty.IsPlayerControllableMember(actor) != true ||
                !world.Entities.TryGet(actor, out var attacker) || !CombatLifeStateService.CanFight(attacker) ||
                !world.Entities.TryGet(target, out var defender) || !CombatLifeStateService.CanFight(defender)) return false;
            if (XianXia.Core.Npc.MortalTransportConstraintService.ValidateAttack(world, actor).IsFailure) return false;
            var state = world.Strategic.CharacterEncounter;
            if (state != null)
                return (state.Phase == CharacterEncounterPhase.Active || state.Phase == CharacterEncounterPhase.ReadyToEnd) &&
                    bootstrap.GetComponent<HostCharacterEncounter>()?.Phase != HostCharacterEncounter.PresentationPhase.ReadyToStart &&
                    state.Opposing(actor.Value, target.Value) && CanInitiatePlayerHostileAction(actor, target);
            var melee = bootstrap.GetComponent<HostNpcMeleeAssault>();
            return melee != null && melee.IsInFight(actor) && melee.IsInFight(target) && actor != target;
        }
        void LateUpdate()
        {
            if (bootstrap?.Session == null || IsOpen || HostInputGate.BlockWorldInteraction ||
                bootstrap.WorkTargetMode?.IsActive == true || HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
            { ClearBattleAttackCursor(); return; }
            var actor = HostNpcInteraction.ResolveActiveCommandAuthority(bootstrap.Session);
            if (worldCamera == null) worldCamera = Camera.main;
            if (!HostNpcPicker.TryPickAtMouse(worldCamera, bootstrap.ViewSpawner, out var target, out _) || !CanRtsAttack(actor, target))
            { ClearBattleAttackCursor(); return; }
            if (_battleAttackCursor == null)
            {
                _battleAttackCursor = new Texture2D(24, 24, TextureFormat.RGBA32, false);
                for (var y = 0; y < 24; y++) for (var x = 0; x < 24; x++)
                {
                    var dx = Mathf.Abs(x - 11); var dy = Mathf.Abs(y - 11);
                    var mark = (dx <= 1 && dy <= 9 || dy <= 1 && dx <= 9) && (dx >= 4 || dy >= 4) || dx <= 1 && dy <= 1;
                    _battleAttackCursor.SetPixel(x, y, mark ? new Color(1f, .12f, .12f, 1f) : Color.clear);
                }
                _battleAttackCursor.Apply();
            }
            _battleCursorOwned = true;
            Cursor.SetCursor(_battleAttackCursor, new Vector2(11, 11), CursorMode.Auto);
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused) ClearBattleAttackCursor();
        }
        void ClearBattleAttackCursor()
        {
            if (!_battleCursorOwned) return;
            _battleCursorOwned = false; Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
