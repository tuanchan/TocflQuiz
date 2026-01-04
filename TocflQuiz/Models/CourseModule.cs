using System;

namespace TocflQuiz.Models
{
    /// <summary>
    /// "Học phần" hiển thị ở form Danh sách học phần.
    /// Hiện tại map theo Category (hoặc dữ liệu bạn tự nạp sau này).
    /// </summary>
    public sealed class CourseModule
    {
        public string Id { get; }
        public string Title { get; }

        public CourseModule(string id, string title)
        {
            Id = (id ?? string.Empty).Trim();
            Title = (title ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(Id))
                throw new ArgumentException("Id is required.", nameof(id));

            if (string.IsNullOrWhiteSpace(Title))
                Title = Id;
        }

        public override string ToString() => Title;
    }
}
