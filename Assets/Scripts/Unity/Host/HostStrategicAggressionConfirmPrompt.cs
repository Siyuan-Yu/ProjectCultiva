using System;
using UnityEngine;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>WorldMap 等入口共用的轻量战略军事侵略确认框；外交规则只委托 Core Preview／Commit。</summary>
    public sealed class HostStrategicAggressionConfirmPrompt : MonoBehaviour
    {
        PlayableHostBootstrap _bootstrap;
        string _attacker = string.Empty;
        string _defender = string.Empty;
        Action _afterCommit;
        Rect _rect;

        public bool IsOpen => _afterCommit != null;

        public bool Open(PlayableHostBootstrap bootstrap, string attackerFactionId, string defenderFactionId, Action afterCommit)
        {
            _bootstrap = bootstrap;
            var world = bootstrap?.Session?.World;
            if (!StrategicMilitaryAggressionService.TryPreview(world, attackerFactionId, defenderFactionId, out var preview, out var reason))
            {
                Debug.LogWarning("[Host] 军事侵略预览失败：" + reason);
                return false;
            }
            if (!preview.RequiresConfirmation)
            {
                afterCommit?.Invoke();
                return true;
            }
            _attacker = attackerFactionId ?? string.Empty;
            _defender = defenderFactionId ?? string.Empty;
            _afterCommit = afterCommit;
            HostInputGate.BlockWorldInteraction = true;
            return true;
        }

        void OnGUI()
        {
            if (!IsOpen)
                return;
            var world = _bootstrap?.Session?.World;
            if (!StrategicMilitaryAggressionService.TryPreview(world, _attacker, _defender, out var preview, out _))
            {
                Close();
                return;
            }
            _rect = new Rect((Screen.width - 400f) * .5f, (Screen.height - 190f) * .5f, 400f, 190f);
            HostUiHitTest.Block(_rect);
            GUI.Box(_rect, "军事侵略确认");
            GUI.Label(new Rect(_rect.x + 16f, _rect.y + 38f, _rect.width - 32f, 76f),
                "当前关系：" + Format(preview.Relation) + "\n" + preview.Description);
            if (GUI.Button(new Rect(_rect.x + 16f, _rect.yMax - 46f, 176f, 30f), "确认攻击"))
            {
                if (StrategicMilitaryAggressionService.TryCommit(world, _attacker, _defender, out var reason))
                {
                    var callback = _afterCommit;
                    Close();
                    callback?.Invoke();
                }
                else
                    Debug.LogWarning("[Host] 军事侵略提交失败：" + reason);
            }
            if (GUI.Button(new Rect(_rect.xMax - 192f, _rect.yMax - 46f, 176f, 30f), "取消"))
                Close();
        }

        void Close()
        {
            _afterCommit = null;
            _attacker = string.Empty;
            _defender = string.Empty;
            HostInputGate.BlockWorldInteraction = false;
        }

        static string Format(FactionDiplomacyRelation relation)
        {
            switch (relation)
            {
                case FactionDiplomacyRelation.War: return "战争";
                case FactionDiplomacyRelation.Alliance: return "联盟";
                case FactionDiplomacyRelation.Overlord: return "宗主";
                case FactionDiplomacyRelation.Vassal: return "附庸";
                default: return "普通";
            }
        }
    }
}
