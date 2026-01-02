using System;
using System.IO;
using System.Text;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class CardSetStorage
    {
        // Lưu ở AppData\Local\TocflQuiz\cardsets\
        public static string BaseDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TocflQuiz", "cardsets");

        public static string EnsureDir()
        {
            Directory.CreateDirectory(BaseDir);
            return BaseDir;
        }

        public static string SaveSet(CardSet set, string rawInput, string termDefSep, string cardSep)
        {
            EnsureDir();

            // đảm bảo Id
            if (string.IsNullOrWhiteSpace(set.Id))
                set.Id = $"set_{DateTime.Now:yyyyMMdd_HHmmss}";

            var safeId = MakeSafeFileName(set.Id);
            var setDir = Path.Combine(BaseDir, safeId);
            Directory.CreateDirectory(setDir);

            // 1) JSON
            var jsonPath = Path.Combine(setDir, "set.json");
            var json = JsonSerializer.Serialize(set, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json, Encoding.UTF8);

            // 2) Raw import (TSV style)
            var rawPath = Path.Combine(setDir, "raw.txt");
            File.WriteAllText(rawPath, rawInput ?? "", Encoding.UTF8);

            // 3) Meta
            var metaPath = Path.Combine(setDir, "meta.txt");
            File.WriteAllText(metaPath,
                $"title={set.Title}\nitems={set.Items.Count}\ntermDefSep={Escape(termDefSep)}\ncardSep={Escape(cardSep)}\ncreatedAt={set.CreatedAt:O}\n",
                Encoding.UTF8);

            return setDir;
        }

        private static string MakeSafeFileName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Trim();
        }

        private static string Escape(string s) => (s ?? "").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}
