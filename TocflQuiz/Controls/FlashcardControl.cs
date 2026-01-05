using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace TocflQuiz.Controls
{
    public sealed partial class FlashcardControl : Control
    {
        public string FrontText { get; set; } = "";
        public string BackText { get; set; } = "";
        public string SubText { get; set; } = "";
        public bool IsFlipped { get; private set; }

        private bool _starred;
        public bool Starred
        {
            get => _starred;
            set { if (_starred == value) return; _starred = value; Invalidate(); }
        }

        public event EventHandler? StarIconClicked;
        public event EventHandler? PencilIconClicked;
        public event EventHandler? SoundIconClicked;

        // ===== animation =====
        private readonly System.Windows.Forms.Timer _anim = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps
        private readonly Stopwatch _sw = new Stopwatch();
        private const int AnimDurationMs = 260; // nhanh + mượt (có thể chỉnh 220..320)

        private float _angle;      // 0..180
        private float _fromAngle;
        private float _toAngle;
        private bool _animating;

        // icon hit rects
        private Rectangle _rcPencil, _rcSound, _rcStar;

        // ===== caches =====
        private Bitmap? _bg;           // shadow + card body
        private Bitmap? _faceFront;    // text face (for animation)
        private Bitmap? _faceBack;
        private int _cacheW, _cacheH;
        private string _cacheKey = "";

        // icon bitmaps
        private Bitmap? _icPencil, _icSound, _icStarOn, _icStarOff;

        // ===== TOCFL / Taiwan font (Kai like web) =====
        private static readonly string[] TcFontFamilies =
        {
            "DFKai-SB",   // 標楷體 (Taiwan - giống web nhất)
            "BiauKai",
            "KaiTi",
            "STKaiti",
            "Microsoft JhengHei UI",
            "Microsoft JhengHei",
            "PMingLiU",
            "MingLiU"
        };

        private static readonly string TcPrimaryFontName =
            PickInstalledFont(TcFontFamilies) ?? "Microsoft JhengHei";

        public FlashcardControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);

            DoubleBuffered = true;
            Cursor = Cursors.Hand;

            try { BackColor = Color.Transparent; }
            catch { BackColor = Color.White; }

            _anim.Tick += (_, __) =>
            {
                var t = Math.Min(1.0, _sw.Elapsed.TotalMilliseconds / AnimDurationMs);
                var eased = EaseInOutSine(t);
                _angle = Lerp(_fromAngle, _toAngle, (float)eased);

                if (t >= 1.0)
                {
                    _anim.Stop();
                    _sw.Stop();
                    _animating = false;

                    _angle = _toAngle;
                    IsFlipped = (_toAngle >= 90f);
                }

                Invalidate();
            };

            BuildIconCache();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bg?.Dispose();
                _faceFront?.Dispose();
                _faceBack?.Dispose();

                _icPencil?.Dispose();
                _icSound?.Dispose();
                _icStarOn?.Dispose();
                _icStarOff?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            InvalidateAllCaches();
        }

        public void SetCard(string front, string back, string sub = "")
        {
            FrontText = front ?? "";
            BackText = back ?? "";
            SubText = sub ?? "";
            InvalidateFaceCache();
            ForceFront();
        }

        private void InvalidateAllCaches()
        {
            _cacheKey = "";
            _bg?.Dispose(); _bg = null;
            _faceFront?.Dispose(); _faceFront = null;
            _faceBack?.Dispose(); _faceBack = null;
        }

        private void InvalidateFaceCache()
        {
            _cacheKey = "";
            _faceFront?.Dispose(); _faceFront = null;
            _faceBack?.Dispose(); _faceBack = null;
        }

        public void ForceFront()
        {
            _anim.Stop();
            _sw.Reset();
            _animating = false;

            IsFlipped = false;
            _angle = 0f;
            _fromAngle = 0f;
            _toAngle = 0f;
            Invalidate();
        }

        public void ToggleFlip()
        {
            if (_animating) return;

            _animating = true;
            _fromAngle = _angle;
            _toAngle = IsFlipped ? 0f : 180f;

            _sw.Restart();
            _anim.Start();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_animating) return;

            if (_rcPencil.Contains(e.Location)) { PencilIconClicked?.Invoke(this, EventArgs.Empty); return; }
            if (_rcSound.Contains(e.Location)) { SoundIconClicked?.Invoke(this, EventArgs.Empty); return; }
            if (_rcStar.Contains(e.Location)) { StarIconClicked?.Invoke(this, EventArgs.Empty); return; }

            ToggleFlip();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            EnsureBgCache();
            EnsureFaceCache(); // only builds bitmaps once per card/size

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 1 || rect.Height <= 1) return;

            // flip scale
            double rad = _angle * Math.PI / 180.0;
            float scaleX = (float)Math.Abs(Math.Cos(rad));
            scaleX = Math.Max(0.02f, scaleX);

            float cx = rect.Width / 2f;
            float cy = rect.Height / 2f;

            // apply transform for flip
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scaleX, 1f);
            g.TranslateTransform(-cx, -cy);

            // draw cached bg (shadow + rounded card)
            if (_bg != null) g.DrawImageUnscaled(_bg, 0, 0);

            bool showBack = _angle >= 90f;

            // ✅ Khi đang animation -> dùng bitmap mặt (nhẹ, mượt)
            // ✅ Khi đứng yên -> vẽ chữ trực tiếp (nét nhất)
            bool useBitmapFace = _animating || scaleX < 0.98f;

            if (useBitmapFace)
            {
                var face = showBack ? _faceBack : _faceFront;
                if (face != null) g.DrawImageUnscaled(face, 0, 0);
            }
            else
            {
                DrawFaceDirect(g, rect, showBack);
            }

            // icons inside card
            DrawTopRightIcons(g, rect);

            g.ResetTransform();
        }

        private void EnsureBgCache()
        {
            if (Width <= 1 || Height <= 1) return;
            if (_bg != null && _bg.Width == Width && _bg.Height == Height) return;

            _bg?.Dispose();
            _bg = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);

            using var g = Graphics.FromImage(_bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // shadow
            var shadowRect = new Rectangle(rect.X + 6, rect.Y + 8, rect.Width - 6, rect.Height - 8);
            DrawRoundedShadow(g, shadowRect, 18);

            // card body
            DrawRoundedCard(g, rect, 18, Color.White, Color.FromArgb(230, 230, 230));
        }

        private void EnsureFaceCache()
        {
            if (Width <= 1 || Height <= 1) return;

            var key = $"{Width}x{Height}|F:{FrontText}|B:{BackText}|S:{SubText}";
            if (_faceFront != null && _faceBack != null && _cacheW == Width && _cacheH == Height && _cacheKey == key)
                return;

            _cacheW = Width;
            _cacheH = Height;
            _cacheKey = key;

            _faceFront?.Dispose();
            _faceBack?.Dispose();

            _faceFront = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            _faceBack = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);

            RenderFaceBitmap(_faceFront, showBack: false);
            RenderFaceBitmap(_faceBack, showBack: true);
        }

        private void RenderFaceBitmap(Bitmap bmp, bool showBack)
        {
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var rect = new Rectangle(0, 0, bmp.Width - 1, bmp.Height - 1);

            // indicator
            using (var fInd = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var bInd = new SolidBrush(Color.FromArgb(90, 90, 90)))
            {
                g.DrawString(showBack ? "Mặt sau" : "Mặt trước", fInd, bInd, new PointF(18, 14));
            }

            // main text
            DrawCenteredTextGdiPlus(g, showBack ? BackText : FrontText, rect);

            // sub
            if (showBack && !string.IsNullOrWhiteSpace(SubText))
            {
                using var fSub = new Font("Segoe UI", 12f, FontStyle.Regular);
                using var bSub = new SolidBrush(Color.FromArgb(110, 110, 110));
                var subRect = new RectangleF(24, rect.Height - 58, rect.Width - 48, 32);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(SubText, fSub, bSub, subRect, sf);
            }
        }

        private void DrawFaceDirect(Graphics g, Rectangle rect, bool showBack)
        {
            using (var fInd = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var bInd = new SolidBrush(Color.FromArgb(90, 90, 90)))
            {
                g.DrawString(showBack ? "Mặt sau" : "Mặt trước", fInd, bInd, new PointF(18, 14));
            }

            DrawCenteredTextGdiPlus(g, showBack ? BackText : FrontText, rect);

            if (showBack && !string.IsNullOrWhiteSpace(SubText))
            {
                using var fSub = new Font("Segoe UI", 12f, FontStyle.Regular);
                using var bSub = new SolidBrush(Color.FromArgb(110, 110, 110));
                var subRect = new RectangleF(24, rect.Height - 58, rect.Width - 48, 32);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(SubText, fSub, bSub, subRect, sf);
            }
        }

        private void DrawCenteredTextGdiPlus(Graphics g, string text, Rectangle rect)
        {
            text ??= "";
            text = text.Trim();

            float size = 44f;
            if (text.Length > 10) size = 34f;
            if (text.Length > 24) size = 26f;

            bool hasCjk = ContainsCjk(text);

            using var font = hasCjk
                ? new Font(TcPrimaryFontName, size, FontStyle.Regular, GraphicsUnit.Point)
                : new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point);

            using var brush = new SolidBrush(Color.FromArgb(35, 35, 35));

            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisWord,
                FormatFlags = StringFormatFlags.LineLimit
            };

            var textRect = new RectangleF(24, 40, rect.Width - 48, rect.Height - 90);
            g.DrawString(text, font, brush, textRect, sf);
        }

        private void DrawTopRightIcons(Graphics g, Rectangle rect)
        {
            int pad = 14;
            int w = 34;
            int h = 30;
            int gap = 6;
            int top = 10;

            int xStar = rect.Right - pad - w;
            int xSound = xStar - gap - w;
            int xPencil = xSound - gap - w;

            _rcPencil = new Rectangle(xPencil, top, w, h);
            _rcSound = new Rectangle(xSound, top, w, h);
            _rcStar = new Rectangle(xStar, top, w, h);

            if (_icPencil != null) g.DrawImage(_icPencil, _rcPencil);
            if (_icSound != null) g.DrawImage(_icSound, _rcSound);

            var star = Starred ? _icStarOn : _icStarOff;
            if (star != null) g.DrawImage(star, _rcStar);
        }

        private void BuildIconCache()
        {
            _icPencil?.Dispose();
            _icSound?.Dispose();
            _icStarOn?.Dispose();
            _icStarOff?.Dispose();

            var size = new Size(34, 30);
            using var f = new Font("Segoe UI Symbol", 11.5f, FontStyle.Bold);

            _icPencil = RenderGlyph("✎", Color.FromArgb(60, 60, 60), f, size);
            _icSound = RenderGlyph("🔊", Color.FromArgb(60, 60, 60), f, size);

            _icStarOff = RenderGlyph("☆", Color.FromArgb(60, 60, 60), f, size);
            _icStarOn = RenderGlyph("★", Color.FromArgb(255, 170, 0), f, size);
        }

        private static Bitmap RenderGlyph(string glyph, Color color, Font font, Size size)
        {
            var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var b = new SolidBrush(color);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(glyph, font, b, new RectangleF(0, 0, size.Width, size.Height), sf);
            return bmp;
        }

        private static bool ContainsCjk(string s)
        {
            foreach (var ch in s)
            {
                int code = ch;
                if ((code >= 0x4E00 && code <= 0x9FFF) ||
                    (code >= 0x3400 && code <= 0x4DBF) ||
                    (code >= 0x20000 && code <= 0x2A6DF) ||
                    (code >= 0x2A700 && code <= 0x2B73F) ||
                    (code >= 0x2B740 && code <= 0x2B81F) ||
                    (code >= 0x2B820 && code <= 0x2CEAF))
                    return true;
            }
            return false;
        }

        private static string? PickInstalledFont(string[] candidates)
        {
            try
            {
                using var ifc = new InstalledFontCollection();
                var names = ifc.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var c in candidates)
                    if (names.Contains(c)) return c;
            }
            catch { }
            return null;
        }

        private static double EaseInOutSine(double t)
        {
            // mượt hơn cubic ở cảm giác flip
            return -(Math.Cos(Math.PI * t) - 1) / 2;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static void DrawRoundedCard(Graphics g, Rectangle rect, int radius, Color fill, Color border)
        {
            using var path = RoundedRect(rect, radius);
            using var fillBrush = new SolidBrush(fill);
            using var pen = new Pen(border, 1f);

            g.FillPath(fillBrush, path);
            g.DrawPath(pen, path);
        }

        private static void DrawRoundedShadow(Graphics g, Rectangle rect, int radius)
        {
            using var path = RoundedRect(rect, radius);
            using var brush = new SolidBrush(Color.FromArgb(25, 0, 0, 0));
            g.FillPath(brush, path);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
