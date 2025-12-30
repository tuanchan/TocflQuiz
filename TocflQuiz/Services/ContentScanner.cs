using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public sealed class ContentScanner
    {
        private readonly AnswerExcelReader _answerReader = new();
        public List<string> LastErrors { get; } = new();

        // Extensions we accept as "question media"
        private static readonly string[] QuestionExts = new[] { ".pdf", ".png", ".jpg", ".jpeg" };

        public List<QuestionGroup> ScanAll(AppConfig cfg)
        {
            LastErrors.Clear();

            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.DatasetRoot) || !Directory.Exists(cfg.DatasetRoot))
            {
                LastErrors.Add("DatasetRoot không tồn tại.");
                return new List<QuestionGroup>();
            }

            var result = new List<QuestionGroup>();

            var listeningDir = Path.Combine(cfg.DatasetRoot, "Listening");
            var readingDir = Path.Combine(cfg.DatasetRoot, "Reading");

            if (Directory.Exists(listeningDir))
                result.AddRange(ScanModeFolder("Listening", listeningDir));
            else
                LastErrors.Add($"Không thấy folder: {listeningDir}");

            if (Directory.Exists(readingDir))
                result.AddRange(ScanModeFolder("Reading", readingDir));
            else
                LastErrors.Add($"Không thấy folder: {readingDir}");

            return result
                .OrderBy(x => x.Mode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.FileId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private IEnumerable<QuestionGroup> ScanModeFolder(string mode, string modeDir)
        {
            var groups = new List<QuestionGroup>();

            foreach (var categoryDir in Directory.GetDirectories(modeDir))
            {
                var category = Path.GetFileName(categoryDir);

                // 1) find *_Answer.xlsx
                var answerExcel = Directory.GetFiles(categoryDir, "*_Answer.xlsx", SearchOption.TopDirectoryOnly)
                                           .FirstOrDefault()
                               ?? Directory.GetFiles(categoryDir, "*_Answer.xlsx", SearchOption.AllDirectories)
                                           .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(answerExcel) || !File.Exists(answerExcel))
                {
                    LastErrors.Add($"[{mode}/{category}] Không tìm thấy *_Answer.xlsx");
                    continue;
                }

                Dictionary<string, List<string>> answerMap;
                try
                {
                    answerMap = _answerReader.Read(answerExcel);
                    if (answerMap.Count == 0)
                    {
                        LastErrors.Add($"[{mode}/{category}] Answer.xlsx rỗng: {Path.GetFileName(answerExcel)}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    LastErrors.Add($"[{mode}/{category}] Lỗi đọc Answer.xlsx: {Path.GetFileName(answerExcel)} | {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                // 2) detect subfolders
                var subDirs = Directory.GetDirectories(categoryDir);

                var tDir = PickFolder(subDirs, n =>
                    n.Contains("_T", StringComparison.OrdinalIgnoreCase) &&
                    !n.Contains("script", StringComparison.OrdinalIgnoreCase));

                var mp3Dir = PickFolder(subDirs, n =>
                    n.Contains("mp3", StringComparison.OrdinalIgnoreCase));

                var scriptDir = PickFolder(subDirs, n =>
                    n.Contains("script", StringComparison.OrdinalIgnoreCase));

                // 3) build indexes (fast & reliable matching)
                // Question media: prefer _T folder if exists, else category root.
                var questionIndex = BuildMediaIndex(
                    roots: tDir != null ? new[] { tDir } : new[] { categoryDir },
                    exts: QuestionExts,
                    excludeRoot: scriptDir // don't accidentally take script as question
                );

                // If _T exists but lacks some files, allow fallback scan in whole category (excluding script)
                var fallbackQuestionIndex = tDir != null
                    ? BuildMediaIndex(new[] { categoryDir }, QuestionExts, excludeRoot: scriptDir)
                    : questionIndex;

                var mp3Index = BuildMediaIndex(
                    roots: mp3Dir != null ? new[] { mp3Dir } : new[] { categoryDir },
                    exts: new[] { ".mp3" },
                    excludeRoot: null
                );

                var scriptIndex = scriptDir != null
                    ? BuildMediaIndex(new[] { scriptDir }, new[] { ".pdf" }, excludeRoot: null)
                    : new MediaIndex();

                // 4) create groups
                foreach (var (rawId, answers) in answerMap)
                {
                    if (string.IsNullOrWhiteSpace(rawId)) continue;
                    if (answers == null || answers.Count == 0) continue;

                    var fileId = NormalizeId(rawId);

                    var g = new QuestionGroup
                    {
                        Mode = mode,
                        Category = category,
                        FileId = fileId,
                        CorrectAnswers = answers.ToList()
                    };

                    // QUESTION (pdf/image)
                    g.PdfQuestionPath =
                        questionIndex.Find(fileId)
                        ?? (tDir != null ? fallbackQuestionIndex.Find(fileId) : null);

                    // SCRIPT (pdf) - only in scriptDir
                    g.PdfScriptPath = scriptIndex.Find(fileId);

                    // MP3
                    g.Mp3Path = mp3Index.Find(fileId);

                    // FILTER by mode
                    var hasQuestion = !string.IsNullOrWhiteSpace(g.PdfQuestionPath) && File.Exists(g.PdfQuestionPath);

                    if (mode.Equals("Reading", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasQuestion)
                        {
                            // skip this item, but other items may still exist
                            continue;
                        }
                    }
                    else if (mode.Equals("Listening", StringComparison.OrdinalIgnoreCase))
                    {
                        var hasMp3 = !string.IsNullOrWhiteSpace(g.Mp3Path) && File.Exists(g.Mp3Path);
                        if (!hasQuestion || !hasMp3)
                        {
                            continue;
                        }
                    }

                    g.OptionCount = GuessOptionCount(category, g.CorrectAnswers);
                    groups.Add(g);
                }
            }

            return groups;
        }

        // --------- helpers ---------

        private static string? PickFolder(string[] dirs, Func<string, bool> predicate)
        {
            foreach (var d in dirs)
            {
                var name = Path.GetFileName(d);
                if (predicate(name)) return d;
            }
            return null;
        }

        private sealed class MediaIndex
        {
            // exact key: normalized name without ext -> path
            public Dictionary<string, string> Exact { get; } = new(StringComparer.OrdinalIgnoreCase);

            // digits key: digits-only -> path (first wins)
            public Dictionary<string, string> Digits { get; } = new(StringComparer.OrdinalIgnoreCase);

            public string? Find(string fileId)
            {
                var id = NormalizeId(fileId);

                if (Exact.TryGetValue(id, out var p1)) return p1;

                var d = DigitsOnly(id);
                if (!string.IsNullOrEmpty(d) && Digits.TryGetValue(d, out var p2)) return p2;

                // last resort: contains (very safe, but slower; only when others fail)
                if (!string.IsNullOrEmpty(d))
                {
                    foreach (var kv in Digits)
                    {
                        // allow digits key being longer due to suffix/prefix
                        if (kv.Key == d || kv.Key.StartsWith(d) || kv.Key.EndsWith(d))
                            return kv.Value;
                    }
                }

                return null;
            }
        }

        private static MediaIndex BuildMediaIndex(IEnumerable<string> roots, IEnumerable<string> exts, string? excludeRoot)
        {
            var idx = new MediaIndex();

            string? ex = null;
            if (!string.IsNullOrWhiteSpace(excludeRoot))
                ex = Path.GetFullPath(excludeRoot!)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;

            foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r)))
            {
                foreach (var ext in exts)
                {
                    IEnumerable<string> files;
                    try
                    {
                        files = Directory.EnumerateFiles(root, "*" + ext, SearchOption.AllDirectories);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var f in files)
                    {
                        try
                        {
                            if (ex != null)
                            {
                                var ff = Path.GetFullPath(f);
                                if (ff.StartsWith(ex, StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }

                            var name = NormalizeId(Path.GetFileNameWithoutExtension(f));
                            if (!idx.Exact.ContainsKey(name))
                                idx.Exact[name] = f;

                            var digits = DigitsOnly(name);
                            if (!string.IsNullOrEmpty(digits) && !idx.Digits.ContainsKey(digits))
                                idx.Digits[digits] = f;
                        }
                        catch
                        {
                            // ignore one file
                        }
                    }
                }
            }

            return idx;
        }

        private static string NormalizeId(string s)
        {
            s = (s ?? "").Trim();
            s = Path.GetFileNameWithoutExtension(s);

            // remove trailing tokens (spaces/tabs)
            var firstToken = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return (firstToken ?? s).Trim();
        }

        private static string DigitsOnly(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return new string(s.Where(char.IsDigit).ToArray());
        }

        private static int GuessOptionCount(string category, List<string> answers)
        {
            if (category.Contains("Paragraph Completion", StringComparison.OrdinalIgnoreCase) ||
                category.Contains("完成段落", StringComparison.OrdinalIgnoreCase))
                return 6;

            foreach (var a in answers)
            {
                if (string.IsNullOrWhiteSpace(a)) continue;
                var ch = char.ToUpperInvariant(a.Trim()[0]);
                if (ch >= 'E') return 6;
            }
            return 4;
        }
    }
}
