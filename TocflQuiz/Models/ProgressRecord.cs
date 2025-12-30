using System;

namespace TocflQuiz.Models
{
    public sealed class ProgressRecord
    {
        public string FileId { get; set; } = "";

        /// <summary>
        /// Cấp độ ôn (0..n). 0 tương ứng mốc đầu (1 ngày).
        /// </summary>
        public int Stage { get; set; } = 0;

        /// <summary>
        /// Lần làm gần nhất (có thể null nếu chưa làm)
        /// </summary>
        public DateTime? LastAttempt { get; set; }

        /// <summary>
        /// Ngày đến hạn ôn tiếp theo
        /// </summary>
        public DateTime NextDue { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Lần gần nhất đúng bao nhiêu
        /// </summary>
        public int LastCorrect { get; set; } = 0;

        /// <summary>
        /// Tổng số câu trong group
        /// </summary>
        public int LastTotal { get; set; } = 0;

        public bool IsDone => LastAttempt.HasValue;

        public bool IsDue(DateTime today)
        {
            if (NextDue == DateTime.MinValue) return false;
            return NextDue.Date <= today.Date;
        }
    }
}
