using UnityEngine;
using XianXia.Core.Domain.Time;
using XianXia.Core.Exploration;
using XianXia.Core.Schedule;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Demo [49] village crowd label (layer-4 presentation; not per-villager sim).
    /// </summary>
    public sealed class HostCrowdPresenter : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] Transform root;
        GameObject _labelGo;
        TextMesh _label;

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
        }

        void LateUpdate()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
                return;

            EnsureLabel();
            if (!TryFindHouses(session, out var houses))
            {
                _labelGo.SetActive(false);
                return;
            }

            _labelGo.SetActive(true);
            _labelGo.transform.position = HostPresentationSpace.FromPresentation(
                houses.PresentationX, houses.PresentationZ + 2.2f, -0.3f);
            _label.text = "村民：" + CrowdPhase(session);
        }

        static bool TryFindHouses(PlayableHostSession session, out WorldLocationState houses)
        {
            houses = null;
            foreach (var kv in session.World.WorldRegion.Locations)
            {
                if (kv.Key.Contains("house") || (kv.Value.Name != null && kv.Value.Name.Contains("房屋")))
                {
                    houses = kv.Value;
                    return true;
                }
            }

            return false;
        }

        static string CrowdPhase(PlayableHostSession session)
        {
            // Prefer mortal schedule if registered.
            if (session.World.TryGetSchedule("base:schedule_mortal_day", out var def) &&
                def.TryResolve(session.World.Tick, out var block))
            {
                if (block.Activity == ScheduleActivity.Labor)
                    return "工作中";
                if (block.Activity == ScheduleActivity.Eat)
                    return "吃饭中";
                if (block.Activity == ScheduleActivity.Rest)
                    return "休息中";
            }

            var hour = DayClock.FromWorldTick(session.World.Tick).HourOfDay;
            if (hour >= 7 && hour < 18)
                return "工作中";
            return "休息中";
        }

        void EnsureLabel()
        {
            if (_labelGo != null)
                return;
            if (root == null)
            {
                var go = new GameObject("CrowdLabels");
                go.transform.SetParent(transform, false);
                root = go.transform;
            }

            _labelGo = new GameObject("VillageCrowd");
            _labelGo.transform.SetParent(root, false);
            _label = _labelGo.AddComponent<TextMesh>();
            _label.characterSize = 0.14f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.fontSize = 36;
            _label.color = new Color(0.95f, 0.9f, 0.7f);
        }
    }
}
