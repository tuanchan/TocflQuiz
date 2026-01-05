using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;
using TocflQuiz.Forms;
using System.IO;
using System.Linq; // cần cho .Take(5)

namespace TocflQuiz
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Load config
            var cfg = AppConfig.LoadFromAppsettings();
            cfg.EnsureAppDataDir();

            // ===================== [SETTINGS-LOAD] Load DatasetRoot từ LocalAppData =====================
            // File: %LOCALAPPDATA%\TocflQuiz\settings.json
            // Ví dụ nội dung:
            // { "DatasetRoot": "D:\\TOCFL\\Data" }
            var userSettings = SettingsService.Load();
            if (!string.IsNullOrWhiteSpace(userSettings.DatasetRoot))
            {
                cfg.DatasetRoot = userSettings.DatasetRoot;
            }
            // ============================================================================================

            // Scan content
            var scanner = new ContentScanner();
            List<QuestionGroup> groups = scanner.ScanAll(cfg);

            // Nếu không scan được đề nào -> báo lý do
            if (groups.Count == 0)
            {
                var listeningDir = Path.Combine(cfg.DatasetRoot, "Listening");
                var readingDir = Path.Combine(cfg.DatasetRoot, "Reading");

                var msg =
                    "Không scan được đề nào.\n\n" +
                    $"DatasetRoot: {cfg.DatasetRoot}\n" +
                    $"Listening folder exists: {Directory.Exists(listeningDir)}\n" +
                    $"Reading folder exists: {Directory.Exists(readingDir)}\n\n" +
                    "Gợi ý: kiểm tra có file *_Answer.xlsx trong từng category.\n";

                if (scanner.LastErrors.Count > 0)
                {
                    msg += "\nMột vài lỗi khi đọc Excel:\n" + string.Join("\n", scanner.LastErrors.Take(5));
                }

                MessageBox.Show(msg, "Scan Debug", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Load progress
            var store = new ProgressStoreJson(cfg);
            Dictionary<string, ProgressRecord> progressMap = store.Load();

            // Spaced repetition
            var sr = new SpacedRepetition(cfg.ReviewIntervalsDays);

            

            Application.Run(new MainForm(cfg, groups, progressMap, store, sr));
        }
    }
}
