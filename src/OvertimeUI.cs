using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace Bugi
{
    /// <summary>설정 → 알림 쪽의 위쪽 탭 줄(정각 · 점심 … / 초과근무 알림).</summary>
    internal sealed class SubTabs : Control
    {
        private readonly string[] labels; private int selected, hot = -1;
        internal event EventHandler SelectedIndexChanged;

        internal SubTabs(string[] labels)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            this.labels = labels; Height = 36; Cursor = Cursors.Hand; TabStop = true; Margin = Padding.Empty;
        }

        internal int SelectedIndex
        {
            get { return selected; }
            set { value = Math.Max(0, Math.Min(labels.Length - 1, value)); if (value == selected) return; selected = value; Invalidate(); var h = SelectedIndexChanged; if (h != null) h(this, EventArgs.Empty); }
        }

        private Rectangle Cell(int i)
        {
            float s = DeviceDpi / 96f; int x = (int)(18 * s);
            using (var f = new Font("Malgun Gothic", 9.5f, FontStyle.Bold))
                for (int k = 0; k <= i; k++)
                {
                    int w = TextRenderer.MeasureText(labels[k], f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + (int)(28 * s);
                    if (k == i) return new Rectangle(x, 0, w, Height);
                    x += w + (int)(4 * s);
                }
            return Rectangle.Empty;
        }

        private int HitTest(Point p) { for (int i = 0; i < labels.Length; i++) if (Cell(i).Contains(p)) return i; return -1; }
        protected override void OnMouseMove(MouseEventArgs e) { int h = HitTest(e.Location); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { int h = HitTest(e.Location); if (e.Button == MouseButtons.Left && h >= 0) { Focus(); SelectedIndex = h; } base.OnMouseDown(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) SelectedIndex = selected - 1; else if (e.KeyCode == Keys.Right) SelectedIndex = selected + 1;
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(FriendUI.Sheet); g.SmoothingMode = SmoothingMode.AntiAlias; float s = DeviceDpi / 96f;
            using (var pen = new Pen(Color.FromArgb(206, 218, 240))) g.DrawLine(pen, 16 * s, Height - 1, Width - 16 * s, Height - 1);
            for (int i = 0; i < labels.Length; i++)
            {
                Rectangle r = Cell(i); bool sel = i == selected;
                using (var f = new Font("Malgun Gothic", 9.5f, sel ? FontStyle.Bold : FontStyle.Regular))
                    TextRenderer.DrawText(g, labels[i], f, new Rectangle(r.X, r.Y, r.Width, r.Height - (int)(3 * s)), sel ? FriendUI.BlueDark : (i == hot ? FriendUI.Blue : Color.DimGray), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                if (sel) using (var br = new SolidBrush(FriendUI.Blue)) g.FillRectangle(br, r.X + 8 * s, Height - 3 * s, r.Width - 16 * s, 3 * s);
            }
        }
    }

    /// <summary>
    /// "오늘 초과근무 알림" 전원 단추. 꺼져 있으면 시간대 확인 · 기록 알림 · 퇴근 알림이 모두 돌지 않는다.
    /// 켜지면 기능 표시등이 노랗게 들어온다(insarang.sAIde 의 전원 줄과 같은 생각).
    /// </summary>
    internal sealed class PowerBar : Control
    {
        private static readonly string[] LightNames = { "시간대 확인", "기록 알림" };
        private static readonly string[] Glyphs = { "", "" };   // 시계 · 종 (Segoe MDL2 Assets)
        private static readonly string[] Fallback = { "시", "알" };
        private static bool? hasGlyphFont;
        private readonly ToolTip tips; private bool on; private string detail = ""; private bool[] lit = new bool[2]; private int hotLight = -1; private bool hotButton;
        internal event EventHandler Toggled;

        internal PowerBar(ToolTip tips)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            this.tips = tips; Size = new Size(500, 66); TabStop = true; Margin = new Padding(0, 6, 0, 6);
            AccessibleName = "오늘 초과근무 알림"; AccessibleRole = AccessibleRole.CheckButton;
        }

        private static bool GlyphFont { get { if (hasGlyphFont == null) hasGlyphFont = FontFamily.Families.Any(f => f.Name == "Segoe MDL2 Assets"); return hasGlyphFont.Value; } }
        private float UiScale { get { return DeviceDpi / 96f; } }
        private PointF ButtonCenter { get { return new PointF(34 * UiScale, Height / 2f); } }
        private float ButtonRadius { get { return 20 * UiScale; } }
        private RectangleF LightRect(int i) { float s = UiScale; return new RectangleF(70 * s + i * 32 * s, Height - 11 * s - 24 * s, 24 * s, 24 * s); }

        /// <param name="isLit">표시등: 지금 그 기능이 돌고 있는가</param>
        internal void Set(bool isOn, string text, bool[] isLit)
        {
            bool same = on == isOn && detail == text && lit.SequenceEqual(isLit);
            if (same) return;
            on = isOn; detail = text; lit = (bool[])isLit.Clone(); Invalidate();
            AccessibleDefaultActionDescription = on ? "끄기" : "켜기";
        }

        private bool InButton(Point p) { var c = ButtonCenter; float dx = p.X - c.X, dy = p.Y - c.Y; return dx * dx + dy * dy <= ButtonRadius * ButtonRadius; }
        private int LightAt(Point p) { for (int i = 0; i < 2; i++) if (LightRect(i).Contains(p)) return i; return -1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hb = InButton(e.Location); int hl = LightAt(e.Location);
            Cursor = hb ? Cursors.Hand : Cursors.Default;
            if (hb != hotButton) { hotButton = hb; Invalidate(); tips.SetToolTip(this, hb ? (on ? "오늘 초과근무 알림 — 끄기" : "오늘 초과근무 알림 — 켜기\n초과근무하는 날에만 켜세요. 다음 날이 되면 저절로 꺼져요.") : ""); }
            if (hl != hotLight)
            {
                hotLight = hl;
                if (hl >= 0) tips.SetToolTip(this, LightNames[hl] + " · " + (!on ? "꺼짐" : lit[hl] ? "작동 중" : "쉬는 중"));
                else if (!hb) tips.SetToolTip(this, "");
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hotButton = false; hotLight = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left && InButton(e.Location)) { Focus(); Fire(); } base.OnMouseDown(e); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Space || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Fire(); e.Handled = true; } base.OnKeyDown(e); }
        private void Fire() { var h = Toggled; if (h != null) h(this, EventArgs.Empty); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(FriendUI.Sheet); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            float s = UiScale; var rc = new Rectangle(0, 0, Width - 1, Height - 1); PointF c = ButtonCenter; float r = ButtonRadius;
            using (var path = ChoiceStrip.RoundRect(rc, (int)(12 * s)))
            {
                using (var br = new SolidBrush(on ? FriendUI.Navy : Color.FromArgb(238, 241, 245))) g.FillPath(br, path);
                if (on)
                {   // 단추에서 퍼지는 빛
                    g.SetClip(path);
                    using (var glow = new GraphicsPath())
                    {
                        glow.AddEllipse(c.X - 110 * s, c.Y - 90 * s, 220 * s, 180 * s);
                        using (var pg = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(120, 70, 150, 240), SurroundColors = new[] { Color.FromArgb(0, 70, 150, 240) } }) g.FillPath(pg, glow);
                    }
                    g.ResetClip();
                }
                using (var pen = new Pen(on ? FriendUI.Blue : Color.FromArgb(220, 227, 238), 1.5f)) g.DrawPath(pen, path);
            }
            // 전원 단추
            if (on) using (var ring = new Pen(Color.FromArgb(110, 150, 200, 255), 2f)) g.DrawEllipse(ring, c.X - r - 4 * s, c.Y - r - 4 * s, 2 * (r + 4 * s), 2 * (r + 4 * s));
            var bb = new RectangleF(c.X - r, c.Y - r, 2 * r, 2 * r);
            using (var br = new SolidBrush(on ? Color.FromArgb(46, 128, 224) : (hotButton ? Color.FromArgb(244, 248, 255) : Color.White))) g.FillEllipse(br, bb);
            using (var pen = new Pen(on ? Color.FromArgb(176, 208, 250) : (hotButton ? FriendUI.Blue : Color.FromArgb(195, 204, 217)), 2f)) g.DrawEllipse(pen, bb);
            Color ic = on ? Color.White : (hotButton ? FriendUI.Blue : Color.FromArgb(138, 150, 168));
            using (var pen = new Pen(ic, 2.4f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float ir = 8.5f * s;
                g.DrawArc(pen, c.X - ir, c.Y - ir + 1.5f * s, 2 * ir, 2 * ir, -55, 290);
                g.DrawLine(pen, c.X, c.Y - 10 * s, c.X, c.Y - 1 * s);
            }
            // 상태 줄
            using (var f = new Font("Malgun Gothic", 9.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, on ? "작동 중 · " + detail : "꺼짐", f, new Rectangle((int)(70 * s), (int)(9 * s), Width - (int)(80 * s), (int)(22 * s)), on ? Color.White : Color.FromArgb(82, 96, 121), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            // 기능 표시등
            for (int i = 0; i < 2; i++)
            {
                RectangleF lr = LightRect(i); bool isLit = on && lit[i];
                if (isLit)
                {
                    using (var glow = new SolidBrush(Color.FromArgb(46, FriendUI.Gold))) g.FillEllipse(glow, lr.X - 3 * s, lr.Y - 3 * s, lr.Width + 6 * s, lr.Height + 6 * s);
                    using (var br = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillEllipse(br, lr);
                }
                else using (var br = new SolidBrush(on ? Color.FromArgb(20, 255, 255, 255) : Color.White)) g.FillEllipse(br, lr);
                Color gc = isLit ? FriendUI.Gold : on ? Color.FromArgb(110, 255, 255, 255) : Color.FromArgb(160, 171, 189);
                if (GlyphFont)
                    using (var f = new Font("Segoe MDL2 Assets", 10f * s, FontStyle.Regular, GraphicsUnit.Pixel))
                    using (var br = new SolidBrush(gc)) using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(Glyphs[i], f, br, new RectangleF(lr.X, lr.Y + 1, lr.Width, lr.Height), sf);
                else
                    using (var f = new Font("Malgun Gothic", 8f, FontStyle.Bold)) TextRenderer.DrawText(g, Fallback[i], f, Rectangle.Round(lr), gc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            if (Focused) using (var pen = new Pen(Color.FromArgb(160, FriendUI.Gold)) { DashStyle = DashStyle.Dot }) g.DrawEllipse(pen, c.X - r - 7 * s, c.Y - r - 7 * s, 2 * (r + 7 * s), 2 * (r + 7 * s));
        }
    }

    /// <summary>
    /// 오늘의 시간대별 기록 현황표. 왼쪽 띠의 `시작` · `끝` 손잡이를 위아래로 끌어 초근 시간을 정하고,
    /// 띠 밖의 줄을 누르면 그 줄까지 넓히며, 상태 알약을 누르면 그 시간대를 기록함/기록 전으로 바꾼다.
    /// </summary>
    internal sealed class OvertimeBoard : Control
    {
        private const int HeaderH = 30, RowH = 26, BandW = 52;
        private enum Edge { None, Start, End }
        internal OvertimeState St; internal bool Active; internal Func<DateTime> Clock = () => DateTime.Now;
        internal event Action<int, int> RangeChanged; internal event Action<int> SlotClicked;
        private Edge drag; private int dStart, dEnd; private int hotBand = -1;
        private readonly List<Rectangle> chips = new List<Rectangle>(); private List<OvertimeSlot> slots = new List<OvertimeSlot>();

        internal OvertimeBoard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            Size = new Size(500, HeaderH + RowH * 6 + 2); Margin = new Padding(0, 4, 0, 6); TabStop = false;
        }

        private float UiScale { get { return DeviceDpi / 96f; } }
        private int Hdr { get { return (int)Math.Round(HeaderH * UiScale); } }
        private int RowPx { get { return (int)Math.Round(RowH * UiScale); } }
        private int Band { get { return (int)Math.Round(BandW * UiScale); } }

        /// <summary>현황이 바뀌었는지 가려내는 값(바뀔 때만 다시 그린다).</summary>
        internal string Signature(DateTime now)
        {
            if (St == null) return "";
            return string.Join(",", St.Slots(now).Select(x => x.Hour + ":" + (int)x.Status + ":" + x.RecordedAt).ToArray()) + "|" + St.StartHour + "-" + St.EndHour + "|" + Active;
        }

        internal void Rebind(DateTime now)
        {
            if (St == null) return;
            slots = St.Slots(now);
            int h = Hdr + RowPx * Math.Max(1, slots.Count) + 2;
            if (Height != h) Height = h;
            Invalidate();
        }

        private int Start { get { return drag != Edge.None ? dStart : St.StartHour; } }
        private int End { get { return drag != Edge.None ? dEnd : St.EndHour; } }
        private bool Shown(OvertimeSlot x) { return x.Hour >= Start && x.Hour < End; }   // 기본근무 시간은 표에 줄이 없다(Slots 에서 빠짐)
        private int RowTop(int i) { return Hdr + i * RowPx; }

        private int FirstShown() { for (int i = 0; i < slots.Count; i++) if (Shown(slots[i])) return i; return -1; }
        private int LastShown() { for (int i = slots.Count - 1; i >= 0; i--) if (Shown(slots[i])) return i; return -1; }

        private Rectangle HandleRect(Edge edge)
        {
            int i = edge == Edge.Start ? FirstShown() : LastShown(); if (i < 0) return Rectangle.Empty;
            float s = UiScale; int w = (int)(42 * s), h = (int)(18 * s), cx = Band / 2, cy = edge == Edge.Start ? RowTop(i) : RowTop(i) + RowPx;
            return new Rectangle(cx - w / 2, cy - h / 2, w, h);
        }

        private int HourAt(int y)
        {
            if (slots.Count == 0) return -1;
            int i = (y - Hdr) / Math.Max(1, RowPx); if (y < Hdr) i = 0;
            return slots[Math.Max(0, Math.Min(slots.Count - 1, i))].Hour;
        }

        private int RowAt(int y) { if (y < Hdr) return -1; int i = (y - Hdr) / Math.Max(1, RowPx); return i < slots.Count ? i : -1; }

        private void MoveEdge(Edge edge, int hour)
        {
            if (edge == Edge.Start) dStart = Math.Min(hour, dEnd - 1); else dEnd = Math.Max(hour + 1, dStart + 1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || St == null || !Active) return;
            foreach (Edge edge in new[] { Edge.Start, Edge.End })
                if (HandleRect(edge).Contains(e.Location)) { drag = edge; dStart = St.StartHour; dEnd = St.EndHour; Capture = true; Invalidate(); return; }
            int i = RowAt(e.Y); if (i < 0) return;
            OvertimeSlot slot = slots[i];
            if (e.X < Band)
            {   // 띠 밖의 줄을 누르면 그 줄까지 넓힌다
                if (Shown(slot)) return;
                var h = RangeChanged; if (h == null) return;
                if (FirstShown() < 0) h(slot.Hour, slot.Hour + 1); else h(Math.Min(St.StartHour, slot.Hour), Math.Max(St.EndHour, slot.Hour + 1));
                return;
            }
            if (i < chips.Count && chips[i].Contains(e.Location) && slot.Status != SlotStatus.Upcoming) { var h = SlotClicked; if (h != null) h(slot.Hour); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (St == null) return;
            if (drag != Edge.None)
            {
                int hour = HourAt(e.Y); if (hour < 0) return;
                int a = dStart, b = dEnd; MoveEdge(drag, hour);
                if (a != dStart || b != dEnd) Invalidate();
                return;
            }
            int row = RowAt(e.Y); int band = row >= 0 && e.X < Band && !Shown(slots[row]) && Active ? row : -1;
            if (band != hotBand) { hotBand = band; Invalidate(); }
            bool overHandle = Active && (HandleRect(Edge.Start).Contains(e.Location) || HandleRect(Edge.End).Contains(e.Location));
            bool overChip = Active && row >= 0 && row < chips.Count && chips[row].Contains(e.Location) && slots[row].Status != SlotStatus.Upcoming;
            Cursor = overHandle ? Cursors.SizeNS : (overChip || band >= 0) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (drag == Edge.None) return;
            int a = dStart, b = dEnd; drag = Edge.None; Capture = false;
            Invalidate();
            if (St != null && (a != St.StartHour || b != St.EndHour)) { var h = RangeChanged; if (h != null) h(a, b); }
        }

        protected override void OnMouseLeave(EventArgs e) { if (hotBand != -1) { hotBand = -1; Invalidate(); } base.OnMouseLeave(e); }

        private static readonly Color RecBg = Color.FromArgb(225, 242, 232), RecFg = Color.FromArgb(22, 110, 70), PenBg = Color.FromArgb(251, 240, 221), PenFg = Color.FromArgb(154, 91, 0),
            MisBg = Color.FromArgb(251, 233, 231), MisFg = Color.FromArgb(180, 35, 24), UpBg = Color.FromArgb(232, 238, 247), UpFg = Color.FromArgb(82, 96, 121);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(FriendUI.Sheet); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            if (St == null) return;
            float s = UiScale; int hdr = Hdr, row = RowPx, band = Band; int xTime = band + (int)(6 * s), xStat = (int)(196 * s), xRec = (int)(296 * s);
            Color grey = Color.FromArgb(82, 96, 121); Color bandOn = Active ? FriendUI.Blue : Color.FromArgb(170, 181, 196);
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            using (var fb = new Font("Malgun Gothic", 9f, FontStyle.Bold)) using (var fr = new Font("Malgun Gothic", 9f)) using (var fs = new Font("Malgun Gothic", 8.5f, FontStyle.Bold))
            {
                var top = new Rectangle(0, 0, Width, hdr - (int)(4 * s));
                TextRenderer.DrawText(g, "초근", fb, new Rectangle(0, 0, band, top.Height), grey, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, "근무시간", fb, new Rectangle(xTime, 0, xStat - xTime, top.Height), grey, flags);
                TextRenderer.DrawText(g, "상태", fb, new Rectangle(xStat, 0, xRec - xStat, top.Height), grey, flags);
                TextRenderer.DrawText(g, "기록시각", fb, new Rectangle(xRec, 0, Width - xRec, top.Height), grey, flags);
                using (var pen = new Pen(Color.FromArgb(206, 218, 240))) g.DrawLine(pen, 0, hdr - 1, Width, hdr - 1);

                chips.Clear();
                int first = FirstShown(), last = LastShown();
                for (int i = 0; i < slots.Count; i++)
                {
                    OvertimeSlot x = slots[i]; int y = RowTop(i); bool shown = Shown(x);
                    if (shown) using (var br = new SolidBrush(Active ? Color.FromArgb(229, 239, 252) : Color.FromArgb(241, 244, 248))) g.FillRectangle(br, 0, y, Width, row);
                    using (var pen = new Pen(Color.FromArgb(232, 238, 247))) g.DrawLine(pen, 0, y + row - 1, Width, y + row - 1);
                    // 왼쪽 띠: 초근 시간 밖은 가는 선(눌러서 넓히는 자리는 마우스를 올리면 굵어진다). 안쪽 굵은 띠는 줄을 다 그린 뒤 한 번에 그린다.
                    float bx = band / 2f;
                    if (shown) { }
                    else if (hotBand == i) using (var br = new SolidBrush(Color.FromArgb(167, 200, 235))) g.FillRectangle(br, bx - 4 * s, y, 8 * s, row);
                    else using (var br = new SolidBrush(Color.FromArgb(220, 227, 238))) g.FillRectangle(br, bx - 1 * s, y, 2 * s, row);

                    TextRenderer.DrawText(g, x.Label, fr, new Rectangle(xTime, y, xStat - xTime, row), Color.FromArgb(15, 27, 45), flags);
                    Color bg, fg; string text;
                    switch (x.Status)
                    {
                        case SlotStatus.Recorded: bg = RecBg; fg = RecFg; text = "기록됨"; break;
                        case SlotStatus.Pending: bg = PenBg; fg = PenFg; text = "기록 전"; break;
                        case SlotStatus.Missed: bg = MisBg; fg = MisFg; text = "미기록"; break;
                        default: bg = UpBg; fg = UpFg; text = "미도래"; break;
                    }
                    Size ts = TextRenderer.MeasureText(text, fs, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                    var chip = new Rectangle(xStat, y + (row - (int)(20 * s)) / 2, ts.Width + (int)(14 * s), (int)(20 * s)); chips.Add(chip);
                    using (var p = ChoiceStrip.RoundRect(chip, (int)(5 * s))) using (var br = new SolidBrush(bg)) g.FillPath(br, p);
                    TextRenderer.DrawText(g, text, fs, chip, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, x.RecordedAt, fr, new Rectangle(xRec, y, Width - xRec, row), Color.FromArgb(15, 27, 45), flags);
                }
                if (first >= 0)
                {   // 초근 시간 띠: 이어진 줄마다 둥근 막대 하나
                    float bw = 8 * s;
                    for (int i = first; i <= last; i++)
                    {
                        if (!Shown(slots[i])) continue;
                        int k = i; while (k + 1 <= last && Shown(slots[k + 1])) k++;
                        var bar = new RectangleF(band / 2f - bw / 2, RowTop(i), bw, RowTop(k) + row - RowTop(i));
                        using (var br = new SolidBrush(bandOn)) using (var p = ChoiceStrip.RoundRect(Rectangle.Round(bar), (int)bw)) g.FillPath(br, p);
                        i = k;
                    }
                    DrawHandle(g, Edge.Start, "시작"); DrawHandle(g, Edge.End, "끝");
                }
            }
        }

        // ───────── 자체 점검용 ─────────
        internal int RowCount { get { return slots.Count; } }
        internal Point HandleCenter(bool start) { Rectangle r = HandleRect(start ? Edge.Start : Edge.End); return new Point(r.X + r.Width / 2, r.Y + r.Height / 2); }
        internal Point RowPoint(int hour, int x) { int i = slots.FindIndex(t => t.Hour == hour); return new Point(x, RowTop(i) + RowPx / 2); }
        internal Point BandPoint(int hour) { return RowPoint(hour, Band / 2); }
        internal Point ChipPoint(int hour) { int i = slots.FindIndex(t => t.Hour == hour); return new Point(chips[i].X + chips[i].Width / 2, chips[i].Y + chips[i].Height / 2); }

        private void DrawHandle(Graphics g, Edge edge, string text)
        {
            Rectangle r = HandleRect(edge); if (r.IsEmpty) return; float s = UiScale;
            Color fill = Active ? (drag == edge ? Color.FromArgb(14, 70, 150) : Color.FromArgb(20, 90, 170)) : Color.FromArgb(123, 135, 152);
            using (var p = ChoiceStrip.RoundRect(r, r.Height / 2))
            {
                using (var sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) { g.TranslateTransform(0, 1.5f * s); g.FillPath(sh, p); g.ResetTransform(); }
                using (var br = new SolidBrush(fill)) g.FillPath(br, p);
                using (var pen = new Pen(Color.White, 2f * s)) g.DrawPath(pen, p);
            }
            // 위아래 화살표
            float ax = r.X + 9 * s, cy = r.Y + r.Height / 2f;
            using (var br = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
            {
                g.FillPolygon(br, new[] { new PointF(ax, cy - 5.5f * s), new PointF(ax + 3.2f * s, cy - 1.5f * s), new PointF(ax - 3.2f * s, cy - 1.5f * s) });
                g.FillPolygon(br, new[] { new PointF(ax, cy + 5.5f * s), new PointF(ax + 3.2f * s, cy + 1.5f * s), new PointF(ax - 3.2f * s, cy + 1.5f * s) });
            }
            using (var f = new Font("Malgun Gothic", 8f, FontStyle.Bold))
                TextRenderer.DrawText(g, text, f, new Rectangle(r.X + (int)(13 * s), r.Y, r.Width - (int)(14 * s), r.Height), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>
    /// 설정 → 알림 → "초과근무 알림". 구성은 insarang.sAIde 의 초과근무 기록 화면과 같다:
    /// 전원 줄 → 마지막 확인 · 지금 기록할 시간대 → 기본근무/초근 카드 → 시간대 현황표.
    /// 바꾸는 즉시 저장하고 즉시 적용한다(저장 단추를 기다리지 않는다).
    /// </summary>
    internal sealed class OvertimePanel : FlowLayoutPanel
    {
        private readonly App app; private readonly ToolTip tips; private readonly OvertimeState st;
        private readonly PowerBar power; private readonly OvertimeBoard board;
        private readonly Panel banner, card;
        private readonly DateTimePicker basicFrom, basicTo, unusedFrom, unusedTo;
        private readonly Button bRecord;
        private readonly Label checkLine, bannerText, otText, otHours, basicHint, basicTag, unusedHint, unusedTag, otTag, howTo;
        private readonly System.Windows.Forms.Timer live;
        private string boardSig = ""; private bool syncing;
        private static readonly Color Ink = Color.FromArgb(15, 27, 45);

        internal OvertimePanel(App app, ToolTip tips)
        {
            this.app = app; this.tips = tips; st = app.Overtime;
            FlowDirection = FlowDirection.TopDown; WrapContents = false; AutoScroll = true; Padding = new Padding(16, 0, 8, 8); BackColor = FriendUI.Sheet;
            DateTime now = DateTime.Now;

            power = new PowerBar(tips); power.Toggled += delegate { DateTime n = DateTime.Now; st.EnsureToday(n); st.SetWatch(!st.WatchOn(n), n); Persist(); Refresh(); };
            Controls.Add(power);

            // ① 마지막 확인 · 지금 기록할 시간대
            checkLine = new Label { Size = new Size(500, 22), ForeColor = Color.DimGray, Margin = new Padding(3, 4, 0, 2), AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(checkLine);
            banner = new Panel { Size = new Size(500, 42), Margin = new Padding(0, 2, 0, 2), Visible = false };
            banner.Paint += delegate(object o, PaintEventArgs e)
            { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var p = ChoiceStrip.RoundRect(new Rectangle(0, 0, banner.Width - 1, banner.Height - 1), 9)) using (var br = new SolidBrush(Color.FromArgb(251, 240, 221))) e.Graphics.FillPath(br, p); };
            bannerText = new Label { Left = 14, Top = 0, Width = 340, Height = 42, ForeColor = Color.FromArgb(154, 91, 0), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Malgun Gothic", 9f, FontStyle.Bold) };
            bRecord = new Button { Text = "기록했어요", Left = 380, Top = 7, Width = 106, Height = 28 }; bRecord.Click += delegate { RecordPending(); };
            tips.SetToolTip(bRecord, "인사랑에서 이 시간대를 기록했으면 눌러서 표시해요.\n(부기는 인사랑에 접속하지 않아요)");
            banner.Controls.AddRange(new Control[] { bannerText, bRecord });
            Controls.Add(banner);

            // ② 기본근무 · 초근 카드
            card = new Panel { Size = new Size(500, 114),Margin = new Padding(0, 2, 0, 2) };
            card.Paint += delegate(object o, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; bool active = IsActive(DateTime.Now);
                using (var p = ChoiceStrip.RoundRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 9)) using (var br = new SolidBrush(active ? Color.FromArgb(232, 241, 253) : Color.FromArgb(241, 244, 248))) e.Graphics.FillPath(br, p);
            };
            basicTag = MakeTag("기본", 10, 8, Color.White, Color.FromArgb(82, 96, 121));
            basicFrom = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Left = 62, Top = 6, Width = 64 };
            basicTo = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Left = 146, Top = 6, Width = 64 };
            basicFrom.Value = now.Date.AddMinutes(st.BasicStartMin); basicTo.Value = now.Date.AddMinutes(st.BasicEndMin);
            basicFrom.ValueChanged += delegate { BasicChanged(); }; basicTo.ValueChanged += delegate { BasicChanged(); };
            tips.SetToolTip(basicFrom, "기본근무 시작 시각"); tips.SetToolTip(basicTo, "기본근무 끝 시각\n기본근무 시간대는 기록 대상이 아니라서 시간표에서 빠져요.");
            card.Controls.Add(new Label { Text = "~", AutoSize = true, Left = 130, Top = 10, BackColor = Color.Transparent, ForeColor = Ink });
            basicHint = new Label { Text = "기록 대상 아님", AutoSize = true, Left = 222, Top = 11, ForeColor = Color.FromArgb(123, 135, 152), BackColor = Color.Transparent, Font = new Font("Malgun Gothic", 8.5f) };
            // 미사용 시간대: 표에서 빠지고 알리지도 않는다. 시작과 끝을 같게 하면 없음, 시작이 끝보다 늦으면 자정을 넘긴다.
            unusedTag = MakeTag("미사용", 10, 44, Color.White, Color.FromArgb(82, 96, 121));
            unusedFrom = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Left = 62, Top = 42, Width = 64 };
            unusedTo = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Left = 146, Top = 42, Width = 64 };
            unusedFrom.Value = now.Date.AddMinutes(st.UnusedStartMin); unusedTo.Value = now.Date.AddMinutes(st.UnusedEndMin);
            unusedFrom.ValueChanged += delegate { UnusedChanged(); }; unusedTo.ValueChanged += delegate { UnusedChanged(); };
            tips.SetToolTip(unusedFrom, "미사용 시간대 시작 시각\n시작과 끝을 같게 두면 미사용 시간이 없어요.\n시작이 끝보다 늦으면 자정을 넘겨요(예: 23:00 ~ 07:00).");
            tips.SetToolTip(unusedTo, "미사용 시간대 끝 시각\n이 시간대는 시간표에 나오지 않고 알리지도 않아요.");
            card.Controls.Add(new Label { Text = "~", AutoSize = true, Left = 130, Top = 46, BackColor = Color.Transparent, ForeColor = Ink });
            unusedHint = new Label { Text = "", AutoSize = true, Left = 222, Top = 47, ForeColor = Color.FromArgb(123, 135, 152), BackColor = Color.Transparent, Font = new Font("Malgun Gothic", 8.5f) };
            otTag = MakeTag("초근", 10, 80, FriendUI.Blue, Color.White);
            otText = new Label { AutoSize = true, Left = 62, Top = 82, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold), ForeColor = Ink, BackColor = Color.Transparent };
            otHours = new Label { AutoSize = true, Left = 200, Top = 84, ForeColor = Color.FromArgb(82, 96, 121), BackColor = Color.Transparent, Font = new Font("Malgun Gothic", 8.5f) };
            card.Controls.AddRange(new Control[] { basicTag, basicFrom, basicHint, basicTo, unusedTag, unusedFrom, unusedHint, unusedTo, otTag, otText, otHours });
            Controls.Add(card);

            // ③ 시간대 현황표
            board = new OvertimeBoard { St = st };
            board.RangeChanged += delegate(int a, int b) { st.SetRange(a, b); Persist(); Refresh(); };
            board.SlotClicked += delegate(int hour)
            {
                DateTime n = DateTime.Now; var slot = st.Slots(n).FirstOrDefault(x => x.Hour == hour); if (slot == null) return;
                st.MarkRecorded(hour, slot.Status != SlotStatus.Recorded, n); Persist(); Refresh();
            };
            tips.SetToolTip(board, "왼쪽 띠의 시작 · 끝 손잡이를 위아래로 끌면 초근 시간이 바뀌어요.\n띠 밖의 줄을 누르면 그 줄까지 넓어져요.\n상태 알약을 누르면 그 시간대를 기록함 ↔ 기록 전으로 바꿔요.");
            Controls.Add(board);

            howTo = new Label { AutoSize = true, MaximumSize = new Size(488, 0), ForeColor = Color.DimGray, Margin = new Padding(3, 4, 3, 3),
                Text = "알림이 오면 인사랑에서 직접 기록한 뒤 '기록했어요'를 눌러 표시하면, 그 시간대는 더 알리지 않아요.\n한 시간에 두 번(정각 뒤 3~10분 사이 · 45분) 알려 주고, 초근 시간 밖에는 알리지 않아요." };
            Controls.Add(howTo);

            live = new System.Windows.Forms.Timer { Interval = 1000 }; live.Tick += delegate { if (Visible && IsHandleCreated) Refresh(); };
            VisibleChanged += delegate { if (Visible) Refresh(); }; live.Start();
            Refresh();
        }

        private Label MakeTag(string text, int x, int y, Color bg, Color fg)
        { return new Label { Text = text, Left = x, Top = y, Width = 44, Height = 22, TextAlign = ContentAlignment.MiddleCenter, BackColor = bg, ForeColor = fg, Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold) }; }

        private bool IsActive(DateTime now) { return st.WatchOn(now); }
        private void Persist() { st.SaveIfDirty(); }

        private void RecordPending()
        {
            DateTime now = DateTime.Now; int h = st.PendingHour(now); if (h < 0) return;
            st.MarkRecorded(h, true, now); Persist(); Refresh();
        }

        private void BasicChanged()
        {
            if (syncing) return;
            if (!st.SetBasic(basicFrom.Value.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), basicTo.Value.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)))
                SyncBasic();   // 앞뒤가 뒤집힌 기본근무는 받지 않고 원래 값으로 되돌린다
            Persist(); Refresh();
        }

        private void SyncBasic()
        {
            DateTime d = DateTime.Today; syncing = true;
            basicFrom.Value = d.AddMinutes(st.BasicStartMin); basicTo.Value = d.AddMinutes(st.BasicEndMin); syncing = false;
        }

        private void UnusedChanged()
        {
            if (syncing) return;
            st.SetUnused(unusedFrom.Value.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), unusedTo.Value.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            Persist(); Refresh();
        }

        private static void SetText(Control c, string text) { if (c.Text != text) c.Text = text; }
        private static void SetEnabled(Control c, bool on) { if (c.Enabled != on) c.Enabled = on; }
        private static void SetVisible(Control c, bool on) { if (c.Visible != on) c.Visible = on; }

        /// <summary>상태를 화면에 다시 맞춘다. 초마다 불리므로 바뀐 것만 건드린다.</summary>
        internal new void Refresh()
        {
            DateTime now = DateTime.Now; st.EnsureToday(now);
            bool watch = st.WatchOn(now), active = watch;

            power.Set(watch, OvertimeState.HourText(st.StartHour) + " ~ " + OvertimeState.HourText(st.EndHour), new[] { active && now.Hour < st.EndHour, active && now.Hour < st.EndHour });

            // 마지막 확인 · 지금 기록할 시간대
            if (!watch) SetText(checkLine, "오늘 초과근무 알림이 꺼져 있어요. 왼쪽 위 전원 단추를 눌러 켜세요.");
            else if (st.LastCheckAt == DateTime.MinValue) SetText(checkLine, "아직 확인 전이에요. 초근 시간(" + OvertimeState.HourText(st.StartHour) + " ~ " + OvertimeState.HourText(st.EndHour) + ")에 시간대마다 알려 줄게요.");
            else SetText(checkLine, "마지막 확인 " + st.LastCheckAt.ToString("HH:mm") + " · " + st.LastCheckMsg);
            int pend = st.PendingHour(now);
            SetVisible(banner, pend >= 0);
            if (pend >= 0) SetText(bannerText, OvertimeState.HourLabel(pend) + " 기록이 아직입니다.");

            // 기본근무 · 초근 카드
            SetEnabled(basicFrom, watch); SetEnabled(basicTo, watch); SetEnabled(unusedFrom, watch); SetEnabled(unusedTo, watch);
            SetText(unusedHint, st.UnusedStartMin == st.UnusedEndMin ? "없음 · 시작과 끝을 같게 두면 쓰지 않아요" : st.UnusedStartMin > st.UnusedEndMin ? "표에서 빠짐 · 알림 없음 · 자정을 넘김" : "표에서 빠짐 · 알림 없음");
            SetText(otText, OvertimeState.HourText(st.StartHour) + " ~ " + OvertimeState.HourText(st.EndHour));
            SetText(otHours, (st.EndHour - st.StartHour) + "시간");
            Color tagBg = active ? FriendUI.Blue : Color.FromArgb(170, 181, 196); if (otTag.BackColor != tagBg) otTag.BackColor = tagBg;
            card.Invalidate();

            // 시간대 현황표
            board.Active = active;
            string sig = board.Signature(now); if (sig != boardSig) { boardSig = sig; board.Rebind(now); }
        }

        protected override void Dispose(bool disposing) { if (disposing) { live.Stop(); live.Dispose(); } base.Dispose(disposing); }
    }

    internal static class OvertimeUiTests
    {
        private static void Send(Control c, string name, MouseButtons b, Point p)
        {
            typeof(Control).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(c, new object[] { new MouseEventArgs(b, 1, p.X, p.Y, 0) });
        }

        /// <summary>시간표를 실제로 그려 보고, 손잡이 끌기 · 줄 눌러 넓히기 · 알약 누르기가 맞게 움직이는지 본다.</summary>
        internal static void Run(Action<bool, string> check)
        {
            DateTime now = DateTime.Now.Date.AddHours(20).AddMinutes(15);
            var st = new OvertimeState(); st.SetWatch(true, now); st.EnsureToday(now); st.SetRange(18, 22);
            using (var board = new OvertimeBoard { St = st, Active = true })
                        {
                int a = -1, b = -1, clicked = -1;
                board.RangeChanged += (x, y) => { a = x; b = y; };
                board.SlotClicked += h => clicked = h;
                Action<Action> redraw = act => { board.Rebind(now); using (var bmp = new Bitmap(board.Width, board.Height)) board.DrawToBitmap(bmp, new Rectangle(0, 0, board.Width, board.Height)); act(); };
                redraw(delegate { });
                check(board.Height > 350 &&board.HandleCenter(true).Y > board.RowPoint(8, 0).Y &&board.HandleCenter(true).Y < board.HandleCenter(false).Y, "초과근무 표: 시작 손잡이가 끝 손잡이보다 위에 그려짐");

                // 끝 손잡이를 23시 줄로 끌면 초근끝이 24시
                Point p = board.HandleCenter(false);
                Send(board, "OnMouseDown", MouseButtons.Left, p);
                Send(board, "OnMouseMove", MouseButtons.None, board.RowPoint(23, p.X));
                Send(board, "OnMouseUp", MouseButtons.Left, board.RowPoint(23, p.X));
                check(a == 18 && b == 24, "초과근무 표: 끝 손잡이를 아래 줄로 끌면 초근끝이 바뀜 (" + a + "~" + b + ")");

                // 시작 손잡이를 20시 줄로 끌면 초근시작이 20시
                st.SetRange(18, 22); redraw(delegate { }); a = b = -1;
                p = board.HandleCenter(true);
                Send(board, "OnMouseDown", MouseButtons.Left, p);
                Send(board, "OnMouseMove", MouseButtons.None, board.RowPoint(20, p.X));
                Send(board, "OnMouseUp", MouseButtons.Left, board.RowPoint(20, p.X));
                check(a == 20 && b == 22, "초과근무 표: 시작 손잡이를 아래 줄로 끌면 초근시작이 바뀜 (" + a + "~" + b + ")");

                // 손잡이를 반대편 너머로 끌어도 한 줄은 남는다
                st.SetRange(18, 22); redraw(delegate { }); a = b = -1;
                p = board.HandleCenter(true);
                Send(board, "OnMouseDown", MouseButtons.Left, p);
                Send(board, "OnMouseMove", MouseButtons.None, board.RowPoint(23, p.X));
                Send(board, "OnMouseUp", MouseButtons.Left, board.RowPoint(23, p.X));
                check(a == 21 && b == 22, "초과근무 표: 시작 손잡이를 끝 너머로 끌어도 한 줄은 남음 (" + a + "~" + b + ")");

                // 띠 밖의 줄을 누르면 그 줄까지 넓어진다
                st.SetRange(18, 21); redraw(delegate { }); a = b = -1;
                Send(board, "OnMouseDown", MouseButtons.Left, board.BandPoint(23));
                Send(board, "OnMouseUp", MouseButtons.Left, board.BandPoint(23));
                check(a == 18 && b == 24, "초과근무 표: 띠 밖의 줄을 누르면 그 줄까지 넓어짐 (" + a + "~" + b + ")");

                // 상태 알약: 지나간 줄은 눌러서 기록 표시, 아직 안 온 줄은 눌러도 반응 없음
                st.SetRange(18, 22); redraw(delegate { });
                Send(board, "OnMouseDown", MouseButtons.Left, board.ChipPoint(19));
                check(clicked == 19, "초과근무 표: 지나간 시간대의 상태 알약을 누르면 알려 줌");
                clicked = -1; Send(board, "OnMouseDown", MouseButtons.Left, board.ChipPoint(22));
                check(clicked == -1, "초과근무 표: 미도래 시간대는 눌러도 반응 없음");

                // 꺼져 있으면(비활성) 아무것도 못 바꾼다
                board.Active = false; a = b = -1; clicked = -1;
                Send(board, "OnMouseDown", MouseButtons.Left, board.HandleCenter(false)); Send(board, "OnMouseUp", MouseButtons.Left, board.HandleCenter(false));
                Send(board, "OnMouseDown", MouseButtons.Left, board.ChipPoint(19));
                check(a == -1 && clicked == -1, "초과근무 표: 꺼져 있으면 손잡이도 알약도 안 움직임");
            }
        }
    }
}
