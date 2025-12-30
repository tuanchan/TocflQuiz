using System;
using System.IO;
using System.Text.Json;

namespace TocflQuiz.Models
{
    public sealed class AppConfig
    {
        public string DatasetRoot { get; set; } = "";
        public string ProgressFileName { get; set; } = "progress.json";
        public int[] ReviewIntervalsDays { get; set; } = new[] { 1, 7, 30 };

        /// <summary>
        /// Thư mục lưu dữ liệu local của app (LocalAppData\TocflQuiz)
        /// </summary>
        public string AppDataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TocflQuiz");

        /// <summary>
        /// File progress.json nằm trong AppDataDir
        /// </summary>
        public string ProgressFilePath => Path.Combine(AppDataDir, ProgressFileName);

        public static AppConfig LoadFromAppsettings(string? baseDir = null)
        {
            baseDir ??= AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "appsettings.json");

            if (!File.Exists(path))
                return new AppConfig();

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AppConfig();

            // Normalize + fallback
            cfg.DatasetRoot = (cfg.DatasetRoot ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cfg.ProgressFileName))
                cfg.ProgressFileName = "progress.json";
            if (cfg.ReviewIntervalsDays == null || cfg.ReviewIntervalsDays.Length == 0)
                cfg.ReviewIntervalsDays = new[] { 1, 7, 30 };

            return cfg;
        }

        public void EnsureAppDataDir()
        {
            Directory.CreateDirectory(AppDataDir);
        }
    }
}
