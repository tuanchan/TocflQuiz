using System;
using System.IO;
using System.Text.Json;

namespace TocflQuiz.Services
{
    // ===============================
    // Model lưu cấu hình app
    // ===============================
    public sealed class AppSettings
    {
        public string DatasetRoot { get; set; } = "";
    }

    // ===============================
    // Service đọc / ghi settings.json
    // ===============================
    public static class SettingsService
    {
        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TocflQuiz",
                "settings.json"
            );

        // -------------------------------
        // Load cấu hình (khi mở app)
        // -------------------------------
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var cfg = JsonSerializer.Deserialize<AppSettings>(json);
                    return cfg ?? new AppSettings();
                }
            }
            catch
            {
                // nuốt lỗi để app không crash
            }

            return new AppSettings();
        }

        // -------------------------------
        // Save cấu hình (khi bấm Áp dụng)
        // -------------------------------
        public static void Save(AppSettings settings)
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions { WriteIndented = true }
            );

            File.WriteAllText(SettingsPath, json);
        }
    }
}
