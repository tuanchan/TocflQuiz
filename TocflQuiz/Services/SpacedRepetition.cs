using System;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public sealed class SpacedRepetition
    {
        private readonly int[] _intervals;

        public SpacedRepetition(int[] intervalsDays)
        {
            _intervals = (intervalsDays == null || intervalsDays.Length == 0)
                ? new[] { 1, 7, 30 }
                : intervalsDays;
        }

        /// <summary>
        /// Luật:
        /// - Sai: reset stage=0, nextDue = hôm nay + 1 ngày
        /// - Đúng:
        ///   + Nếu làm trước hạn (today < nextDue): KHÔNG nâng stage, KHÔNG đổi nextDue
        ///   + Nếu đến hạn / quá hạn: nâng stage (tối đa), nextDue = hôm nay + interval(stage)
        /// </summary>
        public void ApplyResult(ProgressRecord pr, DateTime now, bool allCorrect, int correctCount, int totalCount)
        {
            pr.LastAttempt = now;
            pr.LastCorrect = correctCount;
            pr.LastTotal = totalCount;

            var today = now.Date;

            if (!allCorrect)
            {
                pr.Stage = 0;
                pr.NextDue = today.AddDays(_intervals[0]);
                return;
            }

            // đúng hết
            if (pr.NextDue != DateTime.MinValue && today < pr.NextDue.Date)
            {
                // làm trước hạn: không nâng, không đổi nextDue
                return;
            }

            // đến hạn / quá hạn: nâng stage
            var nextStage = pr.Stage + 1;
            if (nextStage > _intervals.Length - 1)
                nextStage = _intervals.Length - 1;

            pr.Stage = nextStage;
            pr.NextDue = today.AddDays(_intervals[pr.Stage]);
        }

        public DateTime ComputeFirstDue(DateTime now)
            => now.Date.AddDays(_intervals[0]);
    }
}
