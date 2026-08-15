using System.Collections.Generic;
using XianXia.Core.Attributes;

namespace XianXia.Core.Cultivation
{
    /// <summary>Session realm ladder (content-driven; not Snapshot v1).</summary>
    public sealed class RealmLadderBoard
    {
        readonly List<RealmLadderStep> _steps = new List<RealmLadderStep>();

        public IReadOnlyList<RealmLadderStep> Steps => _steps;

        public void Clear() => _steps.Clear();

        public void ReplaceAll(IEnumerable<RealmLadderStep> steps)
        {
            _steps.Clear();
            if (steps == null)
                return;
            foreach (var s in steps)
            {
                if (s != null && s.ProgressRequired > 0)
                    _steps.Add(s);
            }
        }

        public bool TryGetStep(RealmStage realm, int minor, out RealmLadderStep step)
        {
            for (var i = 0; i < _steps.Count; i++)
            {
                var s = _steps[i];
                if (s.FromRealm == realm && s.FromMinor == minor)
                {
                    step = s;
                    return true;
                }
            }

            step = null;
            return false;
        }

        /// <summary>Default Ch1 ladder: 感应前中后 → 炼气1–10 → 筑基.</summary>
        public static RealmLadderBoard CreateDefault()
        {
            var board = new RealmLadderBoard();
            board.ReplaceAll(BuildDefaultSteps());
            return board;
        }

        public static List<RealmLadderStep> BuildDefaultSteps()
        {
            var list = new List<RealmLadderStep>(16);

            list.Add(MinorStep(RealmStage.Mortal, 0, RealmStage.Mortal, 1, 100, 95, false, 0,
                Bonus(AttributeId.MaxHp, 5), Bonus(AttributeId.Stamina, 2)));
            list.Add(MinorStep(RealmStage.Mortal, 1, RealmStage.Mortal, 2, 200, 95, false, 0,
                Bonus(AttributeId.MaxHp, 8), Bonus(AttributeId.SpiritSense, 2)));
            list.Add(MinorStep(RealmStage.Mortal, 2, RealmStage.QiRefining, 1, 300, 90, true, 50,
                Bonus(AttributeId.MaxHp, 25), Bonus(AttributeId.Attack, 5), Bonus(AttributeId.Defense, 4),
                Bonus(AttributeId.Comprehension, 2)));

            var qiNeeds = new[] { 400, 500, 650, 850, 1100, 1400, 1800, 2300, 3000 };
            for (var layer = 1; layer <= 9; layer++)
            {
                list.Add(MinorStep(
                    RealmStage.QiRefining, layer,
                    RealmStage.QiRefining, layer + 1,
                    qiNeeds[layer - 1], 92, false, 0,
                    Bonus(AttributeId.MaxHp, 6 + layer),
                    Bonus(AttributeId.Attack, 1 + layer / 3),
                    Bonus(AttributeId.SpiritPower, 5 + layer)));
            }

            list.Add(MinorStep(
                RealmStage.QiRefining, 10,
                RealmStage.Foundation, 0,
                8000, 35, true, 0,
                Bonus(AttributeId.MaxHp, 80), Bonus(AttributeId.Attack, 20), Bonus(AttributeId.Defense, 15),
                Bonus(AttributeId.SpiritPower, 40), Bonus(AttributeId.SpiritSense, 10)));

            return list;
        }

        static RealmLadderStep MinorStep(
            RealmStage fromRealm, int fromMinor,
            RealmStage toRealm, int toMinor,
            int progress, int success, bool major, int grantSpirit,
            params KeyValuePair<AttributeId, int>[] bonuses)
        {
            var step = new RealmLadderStep
            {
                FromRealm = fromRealm,
                FromMinor = fromMinor,
                ToRealm = toRealm,
                ToMinor = toMinor,
                ProgressRequired = progress,
                SuccessPercent = success,
                MajorRealmJump = major,
                GrantSpiritPower = grantSpirit
            };
            for (var i = 0; i < bonuses.Length; i++)
                step.AttributeBonuses[bonuses[i].Key] = bonuses[i].Value;
            return step;
        }

        static KeyValuePair<AttributeId, int> Bonus(AttributeId id, int value) =>
            new KeyValuePair<AttributeId, int>(id, value);
    }
}
