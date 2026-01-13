using System;
using System.Linq;
using System.Threading;
using TocflQuiz.Forms;
using TocflQuiz.Models;
using TocflQuiz;

namespace TocflQuiz.Services
{
    public sealed class VocabToastScheduler : IDisposable
    {
        private readonly Func<CardSet?> _getSelectedSet;

        private SynchronizationContext? _ui;
        private System.Threading.Timer? _timer;

        private VocabToastSettings _settings = new VocabToastSettings();
        private readonly Random _rng = new Random();

        // cache words để random nhanh
        private CardSet? _cachedSet;
        private (string han, string pinyin, string meaning)[] _cachedItems = Array.Empty<(string, string, string)>();

        public VocabToastScheduler(Func<CardSet?> getSelectedSet)
        {
            _getSelectedSet = getSelectedSet;
        }

        public void AttachUiContext()
        {
            _ui = SynchronizationContext.Current; // gọi từ UI thread (CardForm.OnShown)
        }

        public void ApplySettings(VocabToastSettings settings)
        {
            _settings = settings?.Clone() ?? new VocabToastSettings();
        }

        public void Restart()
        {
            Stop();
            if (!_settings.Enabled) return;

            var intervalMs = (int)Math.Max(1000, _settings.EveryMinutes * 60_000.0);

            _timer = new System.Threading.Timer(_ => Tick(), null, intervalMs, intervalMs);
        }

        public void Stop()
        {
            try { _timer?.Dispose(); } catch { }
            _timer = null;
        }

        public void NotifySelectedSetChanged()
        {
            _cachedSet = null;
            _cachedItems = Array.Empty<(string, string, string)>();
        }

        public void ShowOneNow()
        {
            Tick(force: true);
        }

        private void Tick(bool force = false)
        {
            if (!_settings.Enabled && !force) return;
            if (_ui == null) return; // chưa AttachUiContext

            var item = PickOne();
            if (string.IsNullOrWhiteSpace(item.han)) return;

            _ui.Post(_ =>
            {
                // show toast trên UI thread
                var toast = new VocabToastForm(item.han, item.pinyin, item.meaning, _settings.ShowSeconds);
                toast.Show(); // modeless
            }, null);
        }

        private (string han, string pinyin, string meaning) PickOne()
        {
            var set = _getSelectedSet();
            if (set == null) return default;

            if (!ReferenceEquals(set, _cachedSet) || _cachedItems.Length == 0)
            {
                _cachedSet = set;

                // ✅ Map đúng model của bạn: CardSet.Items (CardItem: Term/Definition/Pinyin)
                var items = set.Items ?? new System.Collections.Generic.List<CardItem>();

                _cachedItems = items
                    .Select(i =>
                    {
                        var han = (i.Term ?? "").Trim();
                        var meaning = (i.Definition ?? "").Trim();
                        var pinyin = (i.Pinyin ?? "").Trim();
                        return (han, pinyin, meaning);
                    })
                    .Where(x => x.han.Length > 0 && (x.meaning.Length > 0 || x.pinyin.Length > 0))
                    .ToArray();
            }

            if (_cachedItems.Length == 0) return default;

            return _cachedItems[_rng.Next(_cachedItems.Length)];
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
