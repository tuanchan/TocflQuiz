using System.Collections.Generic;

namespace TocflQuiz.Models
{
    public sealed class QuestionGroup
    {
        /// <summary>
        /// Ví dụ: "Listening" / "Reading"
        /// </summary>
        public string Mode { get; set; } = "";

        /// <summary>
        /// Ví dụ: "Dialogue_Listening", "Gap Filling", "Paragraph Completion"...
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// ID file: "D00137300", "Q00116500"...
        /// </summary>
        public string FileId { get; set; } = "";

        /// <summary>
        /// Đáp án đúng cho group.
        /// - Dạng thường: Count = 1
        /// - Gap Filling / Paragraph Completion: Count = N (vd 5)
        /// </summary>
        public List<string> CorrectAnswers { get; set; } = new();

        /// <summary>
        /// PDF đề / câu hỏi (thường là *_T)
        /// </summary>
        public string? PdfQuestionPath { get; set; }

        /// <summary>
        /// PDF script (listening)
        /// </summary>
        public string? PdfScriptPath { get; set; }

        /// <summary>
        /// MP3 (listening)
        /// </summary>
        public string? Mp3Path { get; set; }

        /// <summary>
        /// 4 = A-D; 6 = A-F (Paragraph Completion)
        /// </summary>
        public int OptionCount { get; set; } = 4;

        public override string ToString()
            => $"{Mode}/{Category}/{FileId} ({CorrectAnswers.Count} q)";
    }
}
