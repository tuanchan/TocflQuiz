using System;
using System.Collections.Generic;
using System.Linq;
using TocflQuiz.Models;

namespace TocflQuiz.Controls.Features.Quiz
{
    public enum AnswerMode
    {
        Chinese = 0,     // hỏi: Definition (VN + pinyin), đáp án: Term (CN)  ✅ giống ảnh 2
        Vietnamese = 1,  // hỏi: Term (CN), đáp án: Definition (VN)
        Both = 2
    }

    public sealed class QuizConfig
    {
        public int Count { get; set; } = 20;
        public AnswerMode AnswerMode { get; set; } = AnswerMode.Chinese;
        public bool EnableMultipleChoice { get; set; } = true;
    }

    public sealed class QuizQuestion
    {
        public string SmallLabel { get; set; } = "Định nghĩa";
        public string QuestionText { get; set; } = "";
        public List<string> Choices { get; set; } = new();
        public string CorrectAnswer { get; set; } = "";

        public int Index { get; set; }
        public int Total { get; set; }

        public bool UseChineseFontForQuestion { get; set; }
        public bool UseChineseFontForChoices { get; set; }
    }

    public static class QuizEngine
    {
        public static List<QuizQuestion> BuildQuestions(CardSet set, QuizConfig cfg)
        {
            var items = (set.Items ?? new List<CardItem>())
                .Where(i => !string.IsNullOrWhiteSpace(i.Term) && !string.IsNullOrWhiteSpace(i.Definition))
                .ToList();

            if (items.Count < 4) return new List<QuizQuestion>();

            var rnd = new Random();

            int take = Math.Min(Math.Max(1, cfg.Count), items.Count);
            var selected = items.OrderBy(_ => rnd.Next()).Take(take).ToList();

            var result = new List<QuizQuestion>(take);

            for (int i = 0; i < selected.Count; i++)
            {
                var correct = selected[i];

                string questionText;
                string correctAnswer;
                Func<CardItem, string> answerSelector;
                bool qCn = false, aCn = false;

                if (cfg.AnswerMode == AnswerMode.Chinese)
                {
                    questionText = FormatDefinition(correct);
                    answerSelector = it => it.Term;
                    correctAnswer = correct.Term;
                    qCn = false;
                    aCn = true;
                }
                else if (cfg.AnswerMode == AnswerMode.Vietnamese)
                {
                    questionText = correct.Term;
                    answerSelector = it => FormatDefinition(it);
                    correctAnswer = FormatDefinition(correct);
                    qCn = true;
                    aCn = false;
                }
                else
                {
                    questionText = $"{correct.Term}\n{FormatDefinition(correct)}";
                    answerSelector = it => $"{it.Term}\n{FormatDefinition(it)}";
                    correctAnswer = $"{correct.Term}\n{FormatDefinition(correct)}";
                    qCn = true;
                    aCn = true;
                }

                var choices = Build4Choices(correct, items, answerSelector, correctAnswer, rnd);

                result.Add(new QuizQuestion
                {
                    SmallLabel = "Định nghĩa",
                    QuestionText = questionText,
                    CorrectAnswer = correctAnswer,
                    Choices = choices,
                    Index = i + 1,
                    Total = selected.Count,
                    UseChineseFontForQuestion = qCn,
                    UseChineseFontForChoices = aCn
                });
            }

            return result;
        }

        private static List<string> Build4Choices(
            CardItem correctItem,
            List<CardItem> all,
            Func<CardItem, string> selector,
            string correctAnswer,
            Random rnd)
        {
            var pool = all
                .Where(it => !ReferenceEquals(it, correctItem))
                .Select(selector)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var choices = new List<string> { correctAnswer };

            while (choices.Count < 4)
            {
                if (pool.Count == 0) break;

                var pick = pool[rnd.Next(pool.Count)];
                pool.Remove(pick);

                if (!choices.Contains(pick, StringComparer.Ordinal))
                    choices.Add(pick);
            }

            // fallback nếu dữ liệu ít/đụng trùng
            while (choices.Count < 4)
            {
                var pick = selector(all[rnd.Next(all.Count)]);
                if (string.IsNullOrWhiteSpace(pick)) continue;
                if (!choices.Contains(pick, StringComparer.Ordinal))
                    choices.Add(pick);
            }

            return choices.OrderBy(_ => rnd.Next()).ToList();
        }

        private static string FormatDefinition(CardItem item)
        {
            var def = (item.Definition ?? "").Trim();
            var p = (item.Pinyin ?? "").Trim();

            // nếu definition đã có (...) thì thôi
            if (!string.IsNullOrWhiteSpace(p) && !def.Contains("("))
                return $"{def} ({p})";

            return def;
        }

    }
}
