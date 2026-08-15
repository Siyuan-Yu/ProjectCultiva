using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Cultivation;
using XianXia.Core.Results;
using XianXia.Data.Content;

namespace XianXia.Data.Cultivation
{
    public static class RealmLadderMapper
    {
        public static Result<RealmLadderBoard> ToBoard(RealmLadderDefinition def)
        {
            if (def == null)
                return Result.Fail<RealmLadderBoard>(ErrorCode.InvalidArgument, "RealmLadderDefinition null.");

            var steps = new List<RealmLadderStep>();
            for (var i = 0; i < def.Steps.Count; i++)
            {
                var src = def.Steps[i];
                if (src == null)
                    continue;
                if (!CultivationService.TryParseRealm(src.FromRealm, out var fromRealm) ||
                    !CultivationService.TryParseRealm(src.ToRealm, out var toRealm))
                {
                    return Result.Fail<RealmLadderBoard>(
                        ErrorCode.InvalidArgument,
                        "Invalid realm on ladder step.",
                        src.FromRealm + "→" + src.ToRealm);
                }

                if (src.ProgressRequired <= 0)
                {
                    return Result.Fail<RealmLadderBoard>(
                        ErrorCode.InvalidArgument,
                        "progressRequired must be > 0.",
                        i.ToString());
                }

                var step = new RealmLadderStep
                {
                    FromRealm = fromRealm,
                    FromMinor = src.FromMinor,
                    ToRealm = toRealm,
                    ToMinor = src.ToMinor,
                    ProgressRequired = src.ProgressRequired,
                    SuccessPercent = src.SuccessPercent <= 0 ? 95 : src.SuccessPercent,
                    MajorRealmJump = src.MajorRealmJump,
                    GrantSpiritPower = src.GrantSpiritPower
                };

                foreach (var kv in src.Bonuses)
                {
                    if (Enum.TryParse(kv.Key, true, out AttributeId attr))
                        step.AttributeBonuses[attr] = kv.Value;
                }

                steps.Add(step);
            }

            var board = new RealmLadderBoard();
            board.ReplaceAll(steps);
            return Result.Ok(board);
        }
    }
}
