using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class CardImportParser
    {
        // Tách pinyin dạng (...) ở CUỐI definition
        private static readonly Regex TailParenRegex = new(@"\s*\(([^()]*)\)\s*$", RegexOptions.Compiled);

        public static List<CardItem> Parse(string raw, string termDefSep, string cardSep)
        {
            raw ??= "";
            raw = NormalizeNewlines(raw).Trim();

            var results = new List<CardItem>();
            if (string.IsNullOrWhiteSpace(raw)) return results;

            // split cards
            var cardChunks = SplitBySeparator(raw, cardSep);

            foreach (var chunk in cardChunks)
            {
                var line = chunk.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split term/definition at FIRST occurrence
                var pair = SplitFirst(line, termDefSep);
                if (pair == null) continue;

                var term = pair.Value.left.Trim();
                var defRaw = pair.Value.right.Trim();

                if (string.IsNullOrWhiteSpace(term) || string.IsNullOrWhiteSpace(defRaw))
                    continue;

                string? pinyin = null;
                var def = defRaw;

                var m = TailParenRegex.Match(defRaw);
                if (m.Success)
                {
                    pinyin = m.Groups[1].Value.Trim();
                    def = TailParenRegex.Replace(defRaw, "").Trim();
                }

                results.Add(new CardItem
                {
                    Term = term,
                    Definition = def,
                    Pinyin = string.IsNullOrWhiteSpace(pinyin) ? null : pinyin
                });
            }

            return results;
        }

        public static string NormalizeNewlines(string s)
            => s.Replace("\r\n", "\n").Replace("\r", "\n");

        // cardSep supports: "\n", ";", or any custom string
        private static List<string> SplitBySeparator(string text, string sep)
        {
            if (string.IsNullOrEmpty(sep)) return new List<string> { text };

            // If separator is newline, split on '\n' but keep empty lines out later
            if (sep == "\n")
            {
                return new List<string>(text.Split('\n', StringSplitOptions.None));
            }

            return new List<string>(text.Split(sep, StringSplitOptions.None));
        }

        private static (string left, string right)? SplitFirst(string text, string sep)
        {
            if (string.IsNullOrEmpty(sep)) return null;

            var idx = text.IndexOf(sep, StringComparison.Ordinal);
            if (idx < 0) return null;

            var left = text.Substring(0, idx);
            var right = text.Substring(idx + sep.Length);
            return (left, right);
        }
    }
}
