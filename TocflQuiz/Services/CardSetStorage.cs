using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class CardSetStorage
    {
        // ✅ Lưu học phần tại: D:\TOCFL\hocphan\
        public static string BaseDir => Path.Combine(@"D:\TOCFL", "hocphan");

        public static string EnsureDir()
        {
            // tạo luôn D:\TOCFL và hocphan nếu chưa có
            Directory.CreateDirectory(BaseDir);
            return BaseDir;
        }

        public static string SaveSet(CardSet set, string rawInput, string termDefSep, string cardSep)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            EnsureDir();

            // đảm bảo Id
            if (string.IsNullOrWhiteSpace(set.Id))
                set.Id = $"set_{DateTime.Now:yyyyMMdd_HHmmss}";

            set.Items ??= new List<CardItem>();
            if (set.CreatedAt == default) set.CreatedAt = DateTime.Now;

            var safeId = MakeSafeFileName(set.Id);
            var setDir = Path.Combine(BaseDir, safeId);
            Directory.CreateDirectory(setDir);

            // 1) JSON
            var jsonPath = Path.Combine(setDir, "set.json");
            var json = JsonSerializer.Serialize(set, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json, Encoding.UTF8);

            // 2) Raw import
            var rawPath = Path.Combine(setDir, "raw.txt");
            File.WriteAllText(rawPath, rawInput ?? "", Encoding.UTF8);

            // 3) Meta
            var metaPath = Path.Combine(setDir, "meta.txt");
            File.WriteAllText(metaPath,
                $"title={set.Title}\n" +
                $"items={set.Items.Count}\n" +
                $"termDefSep={Escape(termDefSep)}\n" +
                $"cardSep={Escape(cardSep)}\n" +
                $"createdAt={set.CreatedAt:O}\n",
                Encoding.UTF8);

            return setDir;
        }

        public static string GetSetDirectory(string setId)
        {
            EnsureDir();
            if (string.IsNullOrWhiteSpace(setId))
                setId = $"set_{DateTime.Now:yyyyMMdd_HHmmss}";
            var safeId = MakeSafeFileName(setId);
            return Path.Combine(BaseDir, safeId);
        }

        public static void SaveSetJsonOnly(CardSet set)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            EnsureDir();

            if (string.IsNullOrWhiteSpace(set.Id))
                set.Id = $"set_{DateTime.Now:yyyyMMdd_HHmmss}";

            set.Items ??= new List<CardItem>();
            if (set.CreatedAt == default) set.CreatedAt = DateTime.Now;

            var setDir = GetSetDirectory(set.Id);
            Directory.CreateDirectory(setDir);

            var jsonPath = Path.Combine(setDir, "set.json");
            var json = JsonSerializer.Serialize(set, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json, Encoding.UTF8);
        }

        private static string MakeSafeFileName(string s)
        {
            s ??= "";
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Trim();
        }

        private static string Escape(string s)
            => (s ?? "")
                .Replace("\\", "\\\\")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");

        public static IReadOnlyList<CardSet> LoadAllSetsSafe()
        {
            EnsureDir();

            var sets = new List<CardSet>();
            foreach (var dir in Directory.EnumerateDirectories(BaseDir))
            {
                var jsonPath = Path.Combine(dir, "set.json");
                if (!File.Exists(jsonPath)) continue;

                try
                {
                    var json = File.ReadAllText(jsonPath, Encoding.UTF8);
                    var set = JsonSerializer.Deserialize<CardSet>(json);
                    if (set == null) continue;

                    if (string.IsNullOrWhiteSpace(set.Id))
                        set.Id = Path.GetFileName(dir);

                    set.Items ??= new List<CardItem>();
                    sets.Add(set);
                }
                catch
                {
                    // bỏ qua set lỗi
                }
            }

            // mới nhất lên trước
            return sets
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }
    }
}
