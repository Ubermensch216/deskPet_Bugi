using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Bugi
{
    /// <summary>
    /// 가이드북 응용동작(p27~36)에 나오는 소품·옷·효과 그리기. 모두 부기 본체(Rig.cs) 위에 덧그리고 본체의 비율·형태는 건드리지 않는다.
    /// 색은 p12 전용색상(BugiGuide)을 먼저 쓰고, 소품 고유색(나무·지도·청바지 등)은 //g:쪽수 로 출처를 적는다(build.ps1 이 빠진 줄을 경고한다).
    /// 좌표는 Rig 기준 치수(발바닥 y=300, 몸 가운데 x=200).
    /// </summary>
    internal static partial class Rig
    {
        private static Color Tint(Color a, Color b, float t)
        { return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }

        // 소품 고유색 (가이드북 응용동작 그림에서 가져옴)
        private static readonly Color cWood = Color.FromArgb(158, 104, 52);      //g:p31 · p34 이젤과 표지판의 갈색
        private static readonly Color cJeans = Color.FromArgb(112, 148, 196);    //g:p31 청바지
        private static readonly Color cTee = Color.FromArgb(92, 92, 94);         //g:p31 회색 티셔츠
        private static readonly Color cSky = Color.FromArgb(92, 168, 226);       //g:p34 지도·하늘의 파랑
        private static readonly Color cTeal = Color.FromArgb(104, 190, 168);     //g:p34 지도의 청록
        private static readonly Color cPom = Color.FromArgb(255, 229, 89);       //g:p30 응원 술(노랑을 흰색과 섞음)

        private static GraphicsPath TextPath(string s, float size, float x, float y, bool center)
        {
            var path = new GraphicsPath();
            using (var fam = SafeFamily("Malgun Gothic"))
            using (var fmt = new StringFormat(StringFormat.GenericTypographic) { Alignment = center ? StringAlignment.Center : StringAlignment.Near })
                path.AddString(s, fam, (int)FontStyle.Bold, size, new PointF(x, y), fmt);
            return path;
        }

        private static FontFamily SafeFamily(string name)
        { try { return new FontFamily(name); } catch { return FontFamily.GenericSansSerif; } }

        // ───────── 몸 뒤에 놓이는 것: 망토 · 감독 의자 · 이젤 · 표지판 · 여행 가방 ─────────
        private static void DrawPropsBack(Graphics g, Ctx c)
        {
            Pose p = c.P;
            if (p.Cape) DrawCape(g, c);
            if (p.Chair) DrawChair(g, c);
            if (p.Prop == PropKind.Board) DrawEasel(g, c);
            if (p.Prop == PropKind.Sign) DrawSign(g, c);
            if (p.Prop == PropKind.Map) DrawSuitcase(g, c);
        }

        private static void DrawCape(Graphics g, Ctx c)
        {
            float w = (float)Math.Sin(c.P.PropT * 5) * 5f;
            PointF[] pts =
            {
                c.Map(150, 150), c.Map(200, 140), c.Map(252, 152), new PointF(c.Map(300, 142).X + w, c.Map(300, 142).Y), new PointF(c.Map(352, 128).X + w * 1.6f, c.Map(352, 128).Y),
                new PointF(c.Map(366, 176).X + w * 1.4f, c.Map(366, 176).Y), new PointF(c.Map(340, 222).X + w, c.Map(340, 222).Y), c.Map(262, 262), c.Map(150, 262)
            };
            PointF[] cape = Geo.Smooth(pts, true, 5);
            using (var br = new LinearGradientBrush(c.Map(200, 150), c.Map(350, 230), BugiGuide.Red, BugiGuide.DarkRed)) g.FillPolygon(br, cape);   // 망토: 전용 빨강 → 짙은 빨강(p22)
            g.DrawPolygon(c.Ink, cape);
            using (var fold = new Pen(BugiGuide.DarkRed, c.Ink.Width * .8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawLine(fold, c.Map(262, 178), new PointF(c.Map(330, 190).X + w, c.Map(330, 190).Y)); g.DrawLine(fold, c.Map(256, 214), new PointF(c.Map(318, 226).X + w, c.Map(318, 226).Y)); }
        }

        private static void DrawChair(Graphics g, Ctx c)
        {
            using (var pen = new Pen(cInk, 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                PointF a = c.Map(132, 268), b = c.Map(268, 268);
                g.DrawLine(pen, c.Map(146, 270), c.Map(254, 298)); g.DrawLine(pen, c.Map(254, 270), c.Map(146, 298));   // X 다리
                g.DrawLine(pen, c.Map(132, 270), c.Map(268, 270));                                                       // 앉는 판
                g.DrawLine(pen, c.Map(272, 270), c.Map(272, 190)); g.DrawLine(pen, c.Map(128, 270), c.Map(128, 236));    // 등받이·팔걸이 기둥
                g.DrawLine(pen, c.Map(122, 236), c.Map(184, 236));
            }
            PointF[] back = { c.Map(262, 192), c.Map(284, 192), c.Map(284, 222), c.Map(262, 222) };
            g.FillPolygon(bInk, back);
        }

        private static void DrawEasel(Graphics g, Ctx c)
        {
            using (var leg = new Pen(cWood, 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var edge = new Pen(cInk, 6f + c.Ink.Width * 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                foreach (var pr in new[] { new[] { 306f, 190f, 286f, 298f }, new[] { 306f, 190f, 326f, 298f } })
                { g.DrawLine(edge, pr[0], pr[1], pr[2], pr[3]); g.DrawLine(leg, pr[0], pr[1], pr[2], pr[3]); }
            }
            var board = new RectangleF(262, 70, 92, 112);
            g.FillRectangle(bBody, board); g.DrawRectangle(c.Ink, board.X, board.Y, board.Width, board.Height);
            using (var line = new Pen(cInk, c.Ink.Width * .7f) { LineJoin = LineJoin.Round })
            {
                // 도시 풍경(부산 빌딩) 선화와 해
                g.DrawRectangle(line, 272, 106, 20, 54); g.DrawRectangle(line, 294, 92, 24, 68); g.DrawRectangle(line, 320, 118, 22, 42);
                for (int i = 0; i < 4; i++) g.DrawLine(line, 297, 100 + i * 14, 315, 100 + i * 14);
                g.DrawLine(line, 270, 160, 346, 160);
            }
            using (var cloud = new Pen(cSky, 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawLine(cloud, 272, 84, 292, 84); g.DrawLine(cloud, 322, 96, 340, 96); }
            using (var line = new Pen(cInk, c.Ink.Width * .7f))
            {
                g.DrawEllipse(line, 266, 166, 22, 12); g.DrawLine(line, 296, 176, 306, 170); g.DrawLine(line, 306, 170, 314, 176); g.DrawLine(line, 314, 176, 322, 168); g.DrawLine(line, 322, 168, 334, 176);
            }
            using (var frame = new Pen(cWood, 6f)) { g.DrawLine(frame, 258, 186, 358, 186); }
        }

        private static void DrawSign(Graphics g, Ctx c)
        {
            using (var post = new Pen(cWood, 7f) { StartCap = LineCap.Flat })
            using (var edge = new Pen(cInk, 7f + c.Ink.Width * 1.6f) { StartCap = LineCap.Flat })
            { g.DrawLine(edge, 52, 226, 52, 300); g.DrawLine(post, 52, 226, 52, 300); }
            PointF[] sign = { new PointF(10, 178), new PointF(78, 178), new PointF(100, 202), new PointF(78, 226), new PointF(10, 226) };
            using (var br = new SolidBrush(cWood)) g.FillPolygon(br, sign);
            g.DrawPolygon(c.Ink, sign);
            using (var t1 = TextPath("Busan", 14, 14, 181, false)) using (var t2 = TextPath("is good", 15, 14, 197, false))
            using (var dark = new SolidBrush(Tint(cWood, cInk, .55f))) { g.FillPath(dark, t1); g.FillPath(dark, t2); }
        }

        private static void DrawSuitcase(Graphics g, Ctx c)
        {
            var body = new RectangleF(274, 214, 46, 58);
            using (var handle = new Pen(cInk, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawLine(handle, 300, 214, 300, 168); g.DrawLine(handle, 292, 168, 308, 168); }
            using (var path = RoundRect(body, 6f)) { g.FillPath(bBeak, path); g.DrawPath(c.Ink, path); }
            using (var band = new Pen(cInk, c.Ink.Width * .7f)) { g.DrawLine(band, 285, 214, 285, 272); g.DrawLine(band, 309, 214, 309, 272); g.DrawLine(band, 274, 228, 320, 228); }
            g.FillEllipse(bInk, 278, 270, 11, 11); g.FillEllipse(bInk, 306, 270, 11, 11);
        }

        // ───────── 몸 앞·날개 뒤: 하트 · 노트북 ─────────
        private static void DrawPropsMid(Graphics g, Ctx c)
        {
            Pose p = c.P;
            if (p.Prop == PropKind.Heart) DrawBigHeart(g, c);
            if (p.Prop == PropKind.Laptop) DrawLaptop(g, c);
        }

        private static void DrawBigHeart(Graphics g, Ctx c)
        {
            float pulse = 1 + .04f * (float)Math.Sin(c.P.PropT * 7);
            PointF at = c.Map(200, 200);
            var st = g.Save();
            g.TranslateTransform(at.X, at.Y); g.ScaleTransform(pulse, pulse);
            using (var path = HeartPath(0, 0, 118))
            { g.FillPath(bRed, path); g.DrawPath(c.Ink, path); }
            using (var shade = new Pen(BugiGuide.DarkRed, c.Ink.Width * .8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(shade, -34, -30, 30, 26, 200, 70);
            g.Restore(st);
        }

        private static GraphicsPath HeartPath(float cx, float cy, float size)
        {
            var path = new GraphicsPath(); float s = size / 2;
            path.AddBezier(cx, cy + s * .72f, cx - s * 1.25f, cy - s * .05f, cx - s * .6f, cy - s * .9f, cx, cy - s * .38f);
            path.AddBezier(cx, cy - s * .38f, cx + s * .6f, cy - s * .9f, cx + s * 1.25f, cy - s * .05f, cx, cy + s * .72f);
            path.CloseFigure(); return path;
        }

        private static void DrawLaptop(Graphics g, Ctx c)
        {
            float bob = (float)Math.Sin(c.P.PropT * 14) * 1.2f;
            PointF[] baseP = { c.Map(150, 262), c.Map(250, 262), c.Map(262, 276), c.Map(138, 276) };
            g.FillPolygon(bBody, baseP); g.DrawPolygon(c.Ink, baseP);
            var scr = new[] { c.Map(158, 262), c.Map(242, 262), c.Map(238, 214 + bob), c.Map(162, 214 + bob) };
            g.FillPolygon(bBody, scr); g.DrawPolygon(c.Ink, scr);
            // 화면 속 부기 얼굴: 눈 두 개와 노란 부리
            PointF e1 = c.Map(184, 234), e2 = c.Map(216, 234), bk = c.Map(200, 244);
            g.FillEllipse(bInk, e1.X - 3, e1.Y - 3, 6, 6); g.FillEllipse(bInk, e2.X - 3, e2.Y - 3, 6, 6);
            g.FillEllipse(bBeak, bk.X - 9, bk.Y - 5, 18, 10); g.DrawEllipse(c.Ink, bk.X - 9, bk.Y - 5, 18, 10);
        }

        // ───────── 날개 앞: 응원 술 · 확성기 · 지도 · 머리 장식 ─────────
        private static void DrawPropsFront(Graphics g, Ctx c)
        {
            Pose p = c.P;
            if (p.Prop == PropKind.Pompom) for (int s = 0; s < 2; s++) DrawPompom(g, c, s);
            if (p.Prop == PropKind.Megaphone) DrawMegaphone(g, c);
            if (p.Prop == PropKind.Map) DrawMap(g, c);
            if (p.Prop == PropKind.Board) DrawPointer(g, c);
            if (p.Mask || p.Hat) DrawHeadgear(g, c);
        }

        private static void DrawPompom(Graphics g, Ctx c, int side)
        {
            Wing w = WingGeom(c, side);
            float sh = (float)Math.Sin(c.P.PropT * 16 + side * 1.7) * 3f;
            float cx = w.W.X + (side == 0 ? -4 : 4) + sh, cy = w.W.Y - 14 + sh * .6f, r = 27f;
            var pts = new List<PointF>();
            for (int i = 0; i < 18; i++)
            {
                double a = i * Math.PI * 2 / 18; float rr = r * (i % 2 == 0 ? 1.04f : .84f);
                pts.Add(new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr));
            }
            PointF[] fluff = Geo.Smooth(pts.ToArray(), true, 4);
            using (var br = new SolidBrush(cPom)) g.FillPolygon(br, fluff);
            g.DrawPolygon(c.Ink, fluff);
            using (var tuft = new Pen(cInk, c.Ink.Width * .6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < 4; i++) g.DrawArc(tuft, cx - 15 + (i % 2) * 12, cy - 12 + (i / 2) * 15, 14, 10, 200, 140);
        }

        private static void DrawMegaphone(Graphics g, Ctx c)
        {
            PointF[] cone = { new PointF(148, 90), new PointF(102, 62), new PointF(102, 130), new PointF(148, 106) };
            g.FillPolygon(bRed, cone); g.DrawPolygon(c.Ink, cone);
            g.FillEllipse(bRed, 94, 62, 16, 68); g.DrawEllipse(c.Ink, 94, 62, 16, 68);
            using (var rim = new SolidBrush(Tint(BugiGuide.Red, BugiGuide.White, .35f))) g.FillEllipse(rim, 98, 70, 8, 52);
            g.FillRectangle(bInk, 146, 90, 10, 16);
            using (var burst = new Pen(cInk, c.Ink.Width * .8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawLine(burst, 84, 56, 70, 44); g.DrawLine(burst, 80, 96, 62, 96); g.DrawLine(burst, 84, 136, 70, 148); }
        }

        private static void DrawMap(Graphics g, Ctx c)
        {
            PointF[][] panels =
            {
                new[] { new PointF(150, 152), new PointF(184, 160), new PointF(184, 232), new PointF(150, 222) },
                new[] { new PointF(184, 160), new PointF(216, 150), new PointF(216, 224), new PointF(184, 232) },
                new[] { new PointF(216, 150), new PointF(252, 160), new PointF(252, 230), new PointF(216, 224) }
            };
            Color[] fill = { cTeal, cSky, cTeal };
            for (int i = 0; i < 3; i++)
            {
                using (var br = new SolidBrush(fill[i])) g.FillPolygon(br, panels[i]);
                g.DrawPolygon(c.Ink, panels[i]);
            }
            using (var land = new Pen(Tint(cSky, BugiGuide.White, .55f), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawLine(land, 160, 180, 176, 196); g.DrawLine(land, 192, 188, 206, 178); g.DrawLine(land, 226, 186, 240, 200); }
        }

        private static void DrawPointer(Graphics g, Ctx c)
        {
            Wing w = WingGeom(c, 1);
            PointF a = new PointF(w.W.X, w.W.Y), b = new PointF(w.W.X + 38, w.W.Y - 38);
            using (var edge = new Pen(cInk, 5.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(edge, a, b);
            using (var stick = new Pen(BugiGuide.Yellow, 2.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(stick, a, b);
        }

        /// <summary>머리에 쓰는 것: 밀짚모자, "b" 입가리개(두두 등장). 머리 좌표(눈 y=-11, 안경 y=-68)로 그린다.</summary>
        private static void DrawHeadgear(Graphics g, Ctx c)
        {
            Pose p = c.P;
            var st = g.Save();
            g.TranslateTransform(c.HeadC.X, c.HeadC.Y); g.RotateTransform(c.HeadAng); g.ScaleTransform(c.HeadSx, c.HeadSy);
            if (p.Hat)
            {
                using (var crown = new GraphicsPath())
                {
                    crown.AddArc(-34, -106, 68, 56, 180, 180); crown.CloseFigure();
                    g.FillPath(bBeak, crown); g.DrawPath(c.Ink, crown);
                }
                using (var band = new Pen(cRed, 6f)) g.DrawLine(band, -33, -66, 33, -66);
                var brim = new RectangleF(-58, -78, 116, 22);
                g.FillEllipse(bBeak, brim); g.DrawEllipse(c.Ink, brim);
                using (var weave = new Pen(cInk, c.Ink.Width * .5f)) { g.DrawArc(weave, -42, -76, 84, 16, 20, 140); g.DrawArc(weave, -26, -75, 52, 12, 20, 140); }
            }
            if (p.Mask)
            {
                var rect = new RectangleF(-50, 2, 100, 34);
                using (var path = RoundRect(rect, 7f)) { g.FillPath(bBeak, path); g.DrawPath(c.Ink, path); }
                using (var b = TextPath("b", 26, 0, 4, true)) { g.FillPath(bInk, b); }
            }
            g.Restore(st);
        }

        // ───────── 옷: 회색 티셔츠 + 청바지(p31 발표) · 목걸이 명찰(p34) ─────────
        private static void DrawCasual(Graphics g, Ctx c)
        {
            var st = g.Save();
            using (var clip = new GraphicsPath()) { clip.AddPolygon(BodyOutline(c)); g.SetClip(clip, CombineMode.Intersect); }
            float yTop = Yd(10.4f), yWaist = Yd(15.2f), yBot = Yd(20.2f);
            PointF[] tee = { c.Map(100, yTop), c.Map(300, yTop), c.Map(300, yWaist), c.Map(100, yWaist) };
            PointF[] jeans = { c.Map(100, yWaist), c.Map(300, yWaist), c.Map(300, yBot), c.Map(100, yBot) };
            using (var br = new SolidBrush(cTee)) g.FillPolygon(br, tee);
            using (var br = new SolidBrush(cJeans)) g.FillPolygon(br, jeans);
            g.DrawLine(c.Ink, c.Map(100, yWaist), c.Map(300, yWaist)); g.DrawLine(c.Ink, c.Map(100, yTop), c.Map(300, yTop));
            using (var seam = new Pen(cInk, c.Ink.Width * .6f)) { g.DrawLine(seam, c.Map(200, yWaist), c.Map(200, yBot)); g.DrawArc(seam, c.Map(214, yWaist + 8).X, c.Map(214, yWaist + 8).Y, 16, 18, 0, 160); }
            g.Restore(st);
        }

        private static void DrawLanyard(Graphics g, Ctx c)
        {
            PointF top = CardTop(c);
            using (var strap = new Pen(cStrap, Math.Max(2.4f, c.Ink.Width * .85f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int sgn = -1; sgn <= 1; sgn += 2)
                { float ds; PointF s0 = Chest(c, sgn * 2.9f, 10.8f, out ds); g.DrawLine(strap, s0, top); }
            if (c.BCos > .25f) DrawCard(g, c, top, 1f, c.BCos, c.P.CardSwing);
        }

        // ───────── 효과: 하트 · 반짝별 · "ㅋ" · 만화 글자 ─────────
        private static void DrawEffects(Graphics g, Ctx c)
        {
            Pose p = c.P;
            if (p.Hearts >= 0)
            {
                float[][] spots = { new[] { 124f, 48f, 1f }, new[] { 176f, 20f, .55f }, new[] { 262f, 38f, .8f }, new[] { 292f, 78f, .5f }, new[] { 104f, 96f, .5f } };
                for (int i = 0; i < spots.Length; i++)
                {
                    float ph = (p.Hearts * .9f + i * .21f) % 1f, rise = -ph * 16, s = 28 * spots[i][2] * (.85f + .15f * (float)Math.Sin(p.Hearts * 6 + i));
                    int a = (int)(255 * Math.Min(1f, Math.Min(ph * 5, (1 - ph) * 3)));
                    using (var path = HeartPath(spots[i][0], spots[i][1] + rise, s))
                    using (var fill = new SolidBrush(Color.FromArgb(a, cRed))) using (var pen = new Pen(Color.FromArgb(a, cInk), c.Ink.Width * .6f)) { g.FillPath(fill, path); g.DrawPath(pen, path); }
                }
            }
            if (p.Stars >= 0)
            {
                float[][] spots = { new[] { 118f, 52f, 9f }, new[] { 292f, 66f, 11f }, new[] { 106f, 168f, 7f }, new[] { 300f, 150f, 8f } };
                for (int i = 0; i < spots.Length; i++)
                {
                    float tw = .6f + .4f * (float)Math.Sin(p.Stars * 8 + i * 1.9);
                    DrawSpark(g, spots[i][0], spots[i][1], spots[i][2] * tw, Color.FromArgb((int)(255 * (.5 + .5 * tw)), BugiGuide.Yellow));   //g:p30 노란 반짝별
                }
            }
            if (p.Marks >= 0)
            {
                float[][] spots = { new[] { 96f, 40f }, new[] { 132f, 8f }, new[] { 176f, -6f }, new[] { 232f, 2f }, new[] { 276f, 28f }, new[] { 304f, 66f }, new[] { 92f, 84f }, new[] { 300f, 124f }, new[] { 84f, 130f }, new[] { 310f, 168f } };
                for (int i = 0; i < spots.Length; i++)
                {
                    float jig = (float)Math.Sin(p.Marks * 18 + i * 2.3) * 2.5f;
                    var st = g.Save(); g.TranslateTransform(spots[i][0], spots[i][1] + jig); g.RotateTransform(-20 + i * 9);
                    using (var t = TextPath("ㅋ", 17, 0, 0, true)) g.FillPath(bInk, t);
                    g.Restore(st);
                }
            }
            if (!string.IsNullOrEmpty(p.Sfx))
            {
                float jig = (float)Math.Sin(p.SfxT * 9) * (p.SfxStyle == 1 ? 1.4f : 1.8f);
                using (var path = TextPath(p.Sfx, p.SfxSize, p.SfxAt.X, p.SfxAt.Y + jig, true))
                {
                    if (p.SfxStyle == 1)
                    {
                        using (var pen = new Pen(cInk, p.SfxSize * .2f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
                        g.FillPath(bBeak, path);
                    }
                    else g.FillPath(bInk, path);
                }
            }
        }

        private static void DrawSpark(Graphics g, float cx, float cy, float r, Color col)
        {
            var pts = new[]
            {
                new PointF(cx, cy - r), new PointF(cx + r * .24f, cy - r * .24f), new PointF(cx + r, cy), new PointF(cx + r * .24f, cy + r * .24f),
                new PointF(cx, cy + r), new PointF(cx - r * .24f, cy + r * .24f), new PointF(cx - r, cy), new PointF(cx - r * .24f, cy - r * .24f)
            };
            using (var br = new SolidBrush(col)) g.FillPolygon(br, pts);
        }
    }
}
