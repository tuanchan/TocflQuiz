using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public sealed class ProgressStoreJson
    {
        private readonly AppConfig _cfg;

        public ProgressStoreJson(AppConfig cfg)
        {
            _cfg = cfg;
            _cfg.EnsureAppDataDir();
        }

        public Dictionary<string, ProgressRecord> Load()
        {
            var path = _cfg.ProgressFilePath;
            if (!File.Exists(path))
                return new Dictionary<string, ProgressRecord>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<ProgressRecord>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ProgressRecord>();

                // map by FileId
                return list
                    .Where(x => !string.IsNullOrWhiteSpace(x.FileId))
                    .GroupBy(x => x.FileId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // nếu file bị hỏng: trả về rỗng để app vẫn chạy
                return new Dictionary<string, ProgressRecord>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public void Save(Dictionary<string, ProgressRecord> map)
        {
            var path = _cfg.ProgressFilePath;
            var list = map.Values
                .Where(x => !string.IsNullOrWhiteSpace(x.FileId))
                .OrderBy(x => x.FileId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }

        public ProgressRecord GetOrCreate(Dictionary<string, ProgressRecord> map, string fileId)
        {
            if (map.TryGetValue(fileId, out var pr))
                return pr;

            pr = new ProgressRecord
            {
                FileId = fileId,
                Stage = 0,
                LastAttempt = null,
                NextDue = DateTime.MinValue,
                LastCorrect = 0,
                LastTotal = 0
            };
            map[fileId] = pr;
            return pr;
        }
    }
}
