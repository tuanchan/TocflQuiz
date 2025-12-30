using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace TocflQuiz.Services
{
    public sealed class AnswerExcelReader
    {
        /// <summary>
        /// Đọc file *_Answer.xlsx theo rule:
        /// - Cột B (index 1) = FileId (có thể trống -> thuộc FileId trước)
        /// - Cột C (index 2) = Answer (A/B/C/D... hoặc A-F)
        /// </summary>
        public Dictionary<string, List<string>> Read(string excelPath)
        {
            if (string.IsNullOrWhiteSpace(excelPath))
                throw new ArgumentException("excelPath is empty.");

            if (!File.Exists(excelPath))
                throw new FileNotFoundException("Answer excel not found.", excelPath);

            // để đọc được .xlsx trên Windows
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);

            var ds = reader.AsDataSet(new ExcelDataReader.ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataReader.ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });

            var table = ds.Tables.Cast<DataTable>().FirstOrDefault();
            if (table == null || table.Rows.Count == 0)
                return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            string currentFileId = "";

            foreach (DataRow row in table.Rows)
            {
                // Cột B: fileId, Cột C: answer (theo file của bạn)
                var rawFileId = GetCell(row, 1);
                var rawAnswer = GetCell(row, 2);

                // Bỏ qua dòng tiêu đề (thường có "檔案名稱", "答案"...)
                if (rawFileId.Contains("檔案", StringComparison.OrdinalIgnoreCase) ||
                    rawAnswer.Contains("答案", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Nếu có fileId mới thì cập nhật currentFileId
                if (!string.IsNullOrWhiteSpace(rawFileId))
                    currentFileId = NormalizeFileId(rawFileId);

                if (string.IsNullOrWhiteSpace(currentFileId))
                    continue;

                // Nếu có đáp án thì add vào list
                var ans = NormalizeAnswer(rawAnswer);
                if (string.IsNullOrWhiteSpace(ans))
                    continue;

                if (!map.TryGetValue(currentFileId, out var list))
                {
                    list = new List<string>();
                    map[currentFileId] = list;
                }

                list.Add(ans);
            }

            return map;
        }

        private static string GetCell(DataRow row, int colIndex)
        {
            if (colIndex < 0 || colIndex >= row.Table.Columns.Count) return "";
            var obj = row[colIndex];
            return obj?.ToString()?.Trim() ?? "";
        }

        private static string NormalizeFileId(string s)
        {
            s = (s ?? "").Trim();

            // Ví dụ đôi khi có ".pdf" hoặc ".mp3" hay đường dẫn
            s = Path.GetFileNameWithoutExtension(s);

            // nếu có khoảng trắng dư: "D00137300  " -> "D00137300"
            var firstToken = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return (firstToken ?? s).Trim();
        }

        private static string NormalizeAnswer(string s)
        {
            s = (s ?? "").Trim();
            if (string.IsNullOrWhiteSpace(s)) return "";

            // Một số file có thể ghi "A " hoặc "a" -> "A"
            // Nếu có dạng "A、B" thì tuỳ bạn; hiện tại lấy token đầu tiên.
            var token = s.Split(new[] { ' ', '\t', ',', '，', '、', ';' }, StringSplitOptions.RemoveEmptyEntries)
                         .FirstOrDefault();

            return (token ?? s).Trim().ToUpperInvariant();
        }
    }
}
