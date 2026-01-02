using System;
using System.Collections.Generic;

namespace TocflQuiz.Models
{
    public sealed class CardSet
    {
        public string Id { get; set; } = $"set_{DateTime.Now:yyyyMMdd_HHmmss}";
        public string Title { get; set; } = "Untitled";
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<CardItem> Items { get; set; } = new();
    }

    public sealed class CardItem
    {
        public string Term { get; set; } = "";
        public string Definition { get; set; } = "";
        public string? Pinyin { get; set; } // optional: lấy từ (...) cuối dòng nếu có
    }
}
