using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Bugi
{
    /// <summary>근무복 종류(가이드북 p18 "다양한 의상")와 아이템(p20 "다양한 아이콘")을 몸 윤곽 안쪽·머리 위에 덧그린다. 부기의 비율·형태는 그대로다.</summary>
    internal enum OutfitStyle { Vest, Uniform, Rescue, Suit, Safety, LifeJacket }

    internal static partial class Rig
    {
        private static readonly Color cShirtBlue = Color.FromArgb(88, 150, 214);    //g:p18 제복 셔츠
        private static readonly Color cShirtLight = Color.FromArgb(124, 178, 230);  //g:p18 제복 칼라
        private static readonly Color cNavy = Color.FromArgb(30, 42, 104);          //g:p18 남색 바지·재킷
        private static readonly Color cRescue = Color.FromArgb(246, 156, 52);       //g:p18 구조복 주황
        private static readonly Color cSafety = Color.FromArgb(255, 238, 40);       //g:p18 안전 조끼 형광 노랑
        private static readonly Color cReflect = Color.FromArgb(226, 226, 230);     //g:p18 반사띠

        /// <summary>소매가 있는 옷(제복·정장)이면 날개 흰 부분 대신 칠할 색.</summary>
        private static bool SleeveColor(Pose p, out Color col)
        {
            col = Color.White;
            if (!p.Outfit) return false;
            if (p.OutfitKind == (int)OutfitStyle.Uniform) { col = cShirtBlue; return true; }
            if (p.OutfitKind == (int)OutfitStyle.Suit) { col = cNavy; return true; }
            return false;
        }

        private static PointF[] Edge(Ctx c, float dd)
        {
            var pts = new PointF[17];
            for (int i = -8; i <= 8; i++) pts[i + 8] = c.Map(Axis + i * D, Yd(dd));
            return pts;
        }

        private static PointF[] Band(Ctx c, float d0, float d1)
        {
            var pts = new List<PointF>(Edge(c, d0)); var low = Edge(c, d1); Array.Reverse(low); pts.AddRange(low); return pts.ToArray();
        }

        private static void FillBand(Graphics g, Ctx c, Color col, float d0, float d1)
        { using (var br = new SolidBrush(col)) g.FillPolygon(br, Band(c, d0, d1)); }

        /// <summary>가슴 표면의 점들(xd, dd 쌍)을 이어 만든 다각형. 모두 앞쪽(보이는 면)이면 true.</summary>
        private static bool ChestPoly(Ctx c, out PointF[] pts, params float[] xy)
        {
            pts = new PointF[xy.Length / 2]; bool visible = true;
            for (int i = 0; i < pts.Length; i++) { float d; pts[i] = Chest(c, xy[i * 2], xy[i * 2 + 1], out d); if (d < .25f) visible = false; }
            return visible;
        }

        private static void FillChest(Graphics g, Ctx c, Color col, bool outline, params float[] xy)
        {
            PointF[] pts;
            if (!ChestPoly(c, out pts, xy)) return;
            using (var br = new SolidBrush(col)) g.FillPolygon(br, pts);
            if (outline) g.DrawPolygon(c.Ink, pts);
        }

        private static void LineChest(Graphics g, Ctx c, Pen pen, params float[] xy)
        {
            PointF[] pts;
            if (ChestPoly(c, out pts, xy)) g.DrawLines(pen, pts);
        }

        private static void DrawOutfitStyle(Graphics g, Ctx c)
        {
            var st = g.Save();
            using (var clip = new GraphicsPath()) { clip.AddPolygon(BodyOutline(c)); g.SetClip(clip, CombineMode.Intersect); }
            Pen thin = new Pen(cInk, Math.Max(1.6f, c.Ink.Width * .7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            try
            {
                switch ((OutfitStyle)c.P.OutfitKind)
                {
                    case OutfitStyle.Uniform: DrawUniform(g, c, thin); break;
                    case OutfitStyle.Rescue: DrawRescue(g, c, thin); break;
                    case OutfitStyle.Suit: DrawSuit(g, c, thin); break;
                    case OutfitStyle.Safety: DrawSafety(g, c, thin); break;
                    case OutfitStyle.LifeJacket: DrawLifeJacket(g, c, thin); break;
                }
            }
            finally { thin.Dispose(); g.Restore(st); }
        }

        // 제복(p18 왼쪽 위): 파란 셔츠 + 가슴 주머니 + 검은 허리띠 + 남색 바지
        private static void DrawUniform(Graphics g, Ctx c, Pen thin)
        {
            FillBand(g, c, cShirtBlue, 7.8f, 15.4f);
            FillBand(g, c, cNavy, 16.2f, 21f);
            FillBand(g, c, cInk, 15.4f, 16.2f);
            g.DrawLines(c.Ink, Edge(c, 7.8f)); g.DrawLines(c.Ink, Edge(c, 16.2f));
            PointF d0 = Chest(c, 0, 16.2f, out float depth);
            if (depth > .25f)
            {
                PointF a = Chest(c, 0, 20.2f, out depth); g.DrawLine(thin, d0, a);                       // 바지 가운데 솔기
                FillChest(g, c, Color.FromArgb(196, 196, 204), true, -.7f, 15.45f, .7f, 15.45f, .7f, 16.15f, -.7f, 16.15f);   //g:p18 버클
                LineChest(g, c, thin, 0f, 8.4f, 0f, 15.4f);                                               // 단추 줄
                FillChest(g, c, cShirtLight, true, -.2f, 8.0f, -3.0f, 8.5f, -1.5f, 10.3f);               // 칼라
                FillChest(g, c, cShirtLight, true, .2f, 8.0f, 3.0f, 8.5f, 1.5f, 10.3f);
                foreach (int s in new[] { -1, 1 })
                {
                    FillChest(g, c, cShirtLight, true, s * 1.0f, 10.8f, s * 3.4f, 10.8f, s * 3.4f, 12.8f, s * 1.0f, 12.8f);   // 가슴 주머니
                    LineChest(g, c, thin, s * 1.0f, 11.5f, s * 3.4f, 11.5f);
                }
            }
        }

        // 구조복(p18 위 가운데): 주황 조끼 + 큰 주머니 두 개 + 남색 바지
        private static void DrawRescue(Graphics g, Ctx c, Pen thin)
        {
            FillBand(g, c, cNavy, 15.8f, 21f);
            FillBand(g, c, cRescue, 8.8f, 15.8f);
            g.DrawLines(c.Ink, Edge(c, 8.8f)); g.DrawLines(c.Ink, Edge(c, 15.8f));
            PointF top = Chest(c, 0, 15.8f, out float depth);
            if (depth > .25f)
            {
                PointF bot = Chest(c, 0, 21f, out depth); g.DrawLine(thin, top, bot);
                FillChest(g, c, cNavy, true, -3.2f, 8.8f, 0f, 12.2f, 3.2f, 8.8f, 2.4f, 8.8f, 0f, 10.9f, -2.4f, 8.8f);        // 목둘레 남색 단
                LineChest(g, c, thin, 0f, 12.2f, 0f, 15.8f);
                foreach (int s in new[] { -1, 1 })
                {
                    FillChest(g, c, Color.FromArgb(236, 138, 36), true, s * .8f, 12.4f, s * 3.6f, 12.4f, s * 3.6f, 15.0f, s * .8f, 15.0f);   //g:p18 주머니
                    LineChest(g, c, thin, s * .8f, 13.2f, s * 3.6f, 13.2f);
                }
            }
        }

        // 정장(p18 위 오른쪽): 남색 재킷 + 흰 셔츠 + 빨간 넥타이 + 명찰
        private static void DrawSuit(Graphics g, Ctx c, Pen thin)
        {
            FillBand(g, c, cNavy, 8.6f, 16.4f);
            g.DrawLines(c.Ink, Edge(c, 8.6f)); g.DrawLines(c.Ink, Edge(c, 16.4f));
            PointF top = Chest(c, 0, 16.4f, out float depth);
            if (depth > .25f)
            {
                FillChest(g, c, BugiGuide.White, true, -1.9f, 8.6f, 1.9f, 8.6f, .5f, 16.4f, -.5f, 16.4f);                  // 흰 셔츠
                LineChest(g, c, thin, -1.9f, 8.6f, -.5f, 12.6f, -1.9f, 12.9f);                                            // 라펠
                LineChest(g, c, thin, 1.9f, 8.6f, .5f, 12.6f, 1.9f, 12.9f);
                FillChest(g, c, BugiGuide.Red, true, -.5f, 9.1f, .5f, 9.1f, .4f, 10.0f, -.4f, 10.0f);                      // 넥타이 매듭
                FillChest(g, c, BugiGuide.Red, true, -.55f, 10.0f, .55f, 10.0f, .3f, 14.0f, 0f, 14.6f, -.3f, 14.0f);       // 넥타이
                FillChest(g, c, BugiGuide.White, true, 1.1f, 11.6f, 2.3f, 11.6f, 2.3f, 13.4f, 1.1f, 13.4f);                // 명찰
                LineChest(g, c, thin, 1.4f, 12.2f, 2.0f, 12.2f);
            }
        }

        // 안전복(p18 아래 왼쪽): 형광 노랑 반사 조끼. 안전모와 눈 위에 내려쓴 안경은 머리 쪽(DrawHeadAccessories)에서 그린다.
        private static void DrawSafety(Graphics g, Ctx c, Pen thin)
        {
            FillBand(g, c, cSafety, 9.4f, 16.6f);
            FillBand(g, c, cReflect, 11.4f, 12.2f); FillBand(g, c, cReflect, 14.2f, 15.0f);
            g.DrawLines(c.Ink, Edge(c, 9.4f)); g.DrawLines(c.Ink, Edge(c, 16.6f));
            PointF top = Chest(c, 0, 16.6f, out float depth);
            if (depth > .25f)
            {
                LineChest(g, c, thin, 0f, 11.4f, 0f, 16.6f);
                foreach (int s in new[] { -1, 1 })
                {
                    FillChest(g, c, cReflect, false, s * 2.0f, 9.5f, s * 2.9f, 9.5f, s * 2.9f, 11.4f, s * 2.0f, 11.4f);   // 어깨에서 내려오는 띠
                    LineChest(g, c, thin, s * 2.0f, 9.5f, s * 2.0f, 11.4f); LineChest(g, c, thin, s * 2.9f, 9.5f, s * 2.9f, 11.4f);
                }
                FillChest(g, c, cSafety, true, -2.2f, 9.4f, 0f, 12.0f, 2.2f, 9.4f, 1.6f, 9.4f, 0f, 10.9f, -1.6f, 9.4f);
            }
            g.DrawLines(thin, Edge(c, 11.4f)); g.DrawLines(thin, Edge(c, 12.2f)); g.DrawLines(thin, Edge(c, 14.2f)); g.DrawLines(thin, Edge(c, 15.0f));
        }

        // 구명조끼(p18 아래 오른쪽): 빨간 조끼 + 검은 띠 세 줄과 버클
        private static void DrawLifeJacket(Graphics g, Ctx c, Pen thin)
        {
            FillBand(g, c, BugiGuide.Red, 9.2f, 16.6f);
            foreach (float d in new[] { 10.8f, 12.8f, 14.8f }) FillBand(g, c, cInk, d, d + .9f);
            g.DrawLines(c.Ink, Edge(c, 9.2f)); g.DrawLines(c.Ink, Edge(c, 16.6f));
            PointF top = Chest(c, 0, 16.6f, out float depth);
            if (depth > .25f)
            {
                LineChest(g, c, thin, 0f, 9.4f, 0f, 16.6f);
                foreach (float d in new[] { 10.8f, 12.8f, 14.8f })
                    FillChest(g, c, BugiGuide.White, true, -.9f, d - .2f, .9f, d - .2f, .9f, d + 1.1f, -.9f, d + 1.1f);                 // 버클 바탕
                foreach (float d in new[] { 10.8f, 12.8f, 14.8f })
                {
                    FillChest(g, c, cInk, false, -.7f, d, 0f, d + .45f, -.7f, d + .9f); FillChest(g, c, cInk, false, .7f, d, 0f, d + .45f, .7f, d + .9f);   // 나비 모양 버클
                }
            }
        }

        // ───────── 아이템(p20): 크로스백 · 바이저 · 선글라스 ─────────
        private static void DrawBag(Graphics g, Ctx c)
        {
            if (c.BCos < .35f) return;
            float da, db;
            PointF s0 = Chest(c, 3.2f, 10.2f, out da), hipEnd = Chest(c, -4.2f, 16.4f, out db);
            using (var edge = new Pen(cInk, 5.4f + c.Ink.Width * .9f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var strap = new Pen(BugiGuide.Green, 5.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawLine(edge, s0, hipEnd); g.DrawLine(strap, s0, hipEnd); }
            PointF at = Chest(c, -4.2f, 17.6f, out db);
            var st = g.Save();
            g.TranslateTransform(at.X, at.Y);
            using (var body = new GraphicsPath())
            {
                body.AddArc(-24, -22, 48, 44, 180, 180);       // 둥근 윗부분
                body.AddLine(24, 0, 22, 14); body.AddLine(22, 14, -22, 14); body.AddLine(-22, 14, -24, 0); body.CloseFigure();
                g.FillPath(bBeak, body); g.DrawPath(c.Ink, body);
            }
            using (var flap = new Pen(cInk, c.Ink.Width * .7f)) g.DrawArc(flap, -22, -12, 44, 36, 195, 150);
            using (var band = new Pen(BugiGuide.Green, 8f) { StartCap = LineCap.Flat }) { g.DrawLine(band, 0, -21, 0, 14); }
            DrawCamellia(g, 0, 2, 7f);
            g.Restore(st);
        }

        /// <summary>머리에 쓰는 아이템: 바이저 · 선글라스 · 안전모. 머리 좌표(눈 y=-11, 머리 꼭대기 y≈-65)로 그린다.</summary>
        private static void DrawHeadAccessories(Graphics g, Ctx c)
        {
            Pose p = c.P;
            if (p.Visor)
            {
                using (var brim = new GraphicsPath())
                {
                    brim.AddBezier(-46, -48, -30, -66, 30, -66, 46, -48);
                    brim.AddBezier(46, -48, 30, -34, -30, -34, -46, -48);
                    g.FillPath(bBeak, brim); g.DrawPath(c.Ink, brim);
                }
                using (var band = new Pen(cInk, c.Ink.Width * .6f)) g.DrawArc(band, -42, -60, 84, 22, 200, 140);
            }
            if (p.Sunglasses)
            {
                float cs = c.HCos, sn = c.HSin;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * 1.5f * D, z = (float)Math.Sqrt(HeadR * HeadR - x * x), xs = x * cs - z * sn, depth = x * sn + z * cs, f = Math.Min(1f, depth / z);
                    if (f < .2f) continue;
                    var lens = new RectangleF(xs - 19 * f, -23, 38 * f, 26);
                    using (var path = RoundRect(lens, 9f * f + 2))
                    {
                        g.FillPath(bInk, path);
                        using (var frame = new Pen(BugiGuide.Yellow, 4f)) g.DrawPath(frame, path);
                        using (var edge = new Pen(cInk, c.Ink.Width * .6f)) g.DrawPath(edge, path);
                    }
                    using (var glint = new Pen(Color.FromArgb(210, 255, 255, 255), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })   //fx 렌즈 반짝임
                        g.DrawLine(glint, xs - 12 * f, -17, xs - 6 * f, -21);
                    using (var arm = new Pen(BugiGuide.Yellow, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(arm, xs + s * 19 * f, -14, xs + s * (19 * f + 14), -12);
                }
                using (var bridge = new Pen(BugiGuide.Yellow, 4f)) g.DrawLine(bridge, -2, -14, 2, -14);
            }
            if (p.Helmet)
            {
                using (var dome = new GraphicsPath())
                {
                    dome.AddArc(-44, -112, 88, 80, 180, 180); dome.CloseFigure();
                    g.FillPath(bBody, dome); g.DrawPath(c.Ink, dome);
                }
                using (var ridge = new Pen(cInk, c.Ink.Width * .7f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                { g.DrawLine(ridge, 0, -112, 0, -72); g.DrawLine(ridge, -12, -110, -10, -75); g.DrawLine(ridge, 12, -110, 10, -75); }
                using (var path = RoundRect(new RectangleF(-50, -74, 100, 10), 5f)) { g.FillPath(bBody, path); g.DrawPath(c.Ink, path); }
                using (var knob = RoundRect(new RectangleF(-4, -119, 8, 9), 3f)) { g.FillPath(bBody, knob); g.DrawPath(c.Ink, knob); }
                using (var cross = new SolidBrush(BugiGuide.Green))
                { g.FillRectangle(cross, -3, -100, 6, 18); g.FillRectangle(cross, -9, -94, 18, 6); }
            }
        }
    }
}
