using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>将老井字棋：日限＋累计胜场写入 Content boards。</summary>
    public static class HostJiangLaoChess
    {
        public const string CounterWins = "chess_wins_jiang_lao";
        public const string DailyKey = "daily:jiang_lao_chess";
        public const string MinigameId = "ticTacToe";

        public static void ApplyResult(
            SimulationWorld world,
            EntityId subject,
            HostTicTacToePanel.ResultKind result)
        {
            if (world == null || subject.IsNone)
                return;

            var outcomes = new List<ContentOutcome>
            {
                new ContentOutcome { Kind = "setDailyFlag", Id = DailyKey }
            };
            if (result == HostTicTacToePanel.ResultKind.Win)
            {
                outcomes.Add(new ContentOutcome
                {
                    Kind = "addCounter",
                    Id = CounterWins,
                    Amount = 1
                });
            }

            ContentOutcomeApplier.ApplyAll(world, subject, outcomes);
            new QuestService().Evaluate(world, subject);
        }
    }
}
