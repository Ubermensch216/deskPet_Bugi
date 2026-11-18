using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Bugi
{
    /// <summary>
    /// 부캉이를 부위별로 그린다(스티커 그림을 보고 코드로 새로 그린 벡터 그림이라 얼마든지 크게 키워도 선명하다).
    /// 좌표 원점: 발바닥 y=300, 몸 가운데 x=200. 머리·몸통·팔·꼬리·발이 각각 회전(yaw)을 따로 받아
    /// 정면 · 3/4 · 옆모습이 모두 나오고, 둥근 몸 표면에 붙은 무늬(눈·입·턱받이·가방끈·어깨띠)는 표면을 따라 돈다.
    /// 머리는 가로로 넓은 둥근 타원(폭 184), 몸통은 통통한 배(폭 144), 팔은 두 마디(34+34), 발은 크고 둥근 덩어리다.
    /// </summary>
    internal static class BukangyeeRig
    {
        internal const float RefH = 298f, RefFootX = 200f, RefFootY = 300f;
        internal const float Axis = 200f;
        internal static readonly PointF HeadLocalOrigin = new PointF(200, 128);
        private const float HipY = 292f, LeanPivotY = 292f, NeckY = 196f;
        private const float HeadRX = 92f, HeadRY = 70f, HeadRZ = 80f;                 // 머리(가로로 넓은 타원체)
        private const float TorsoCY = 228f, TorsoRX = 72f, TorsoRY = 64f, TorsoRZ = 58f; // 몸통(통통한 배)
        /// <summary>통통한 몸은 옆으로 많이 돌면 어색하다: Turn 1.0 이 실제로는 약 58도(스티커의 3/4 모습 수준)다.</summary>
        private const float YawScale = .64f;
        private const float ArmL1 = 30f, ArmL2 = 30f;
        private const float ShoulderY = 192f, ShoulderHalf = 58f;

        // 색: 스티커에서 잰 값
        private static readonly Color cBlue = Color.FromArgb(70, 148, 240), cBlueHi = Color.FromArgb(118, 188, 252), cBlueLo = Color.FromArgb(40, 104, 214), cBlueDark = Color.FromArgb(26, 78, 176);   //ref
        private static readonly Color cWhite = Color.FromArgb(252, 252, 254), cWhiteLo = Color.FromArgb(214, 220, 238);   //ref
        private static readonly Color cInk = Color.FromArgb(24, 24, 30), cBag = Color.FromArgb(34, 34, 40), cBagHi = Color.FromArgb(78, 78, 88);   //ref
        private static readonly Color cMouth = Color.FromArgb(206, 30, 54), cMouthDark = Color.FromArgb(132, 14, 36), cTongue = Color.FromArgb(255, 124, 144);   //ref
        private static readonly Color cPink = Color.FromArgb(255, 126, 148);   //ref

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }
        private static float SmoothStep(float x) { x = Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
        private static Color A(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B); }
        private static Color Mix(Color a, Color b, float t) { return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }

        /// <summary>한 번 그리는 동안 쓰는 변형 정보: 기준 좌표 → 자세가 반영된 좌표.</summary>
        private sealed class Ctx
        {
            public Pose P; public bool Mirror; public float K;
            public float Edge;                     // 윤곽 그림자 굵기
            public PointF HeadC; public float HeadAng, HeadSx, HeadSy;
            public float Bd, Hd;                   // 몸·얼굴의 yaw(라디안, 왼쪽으로 돌면 +)
            public float BCos, BSin, HCos, HSin;

            public Ctx(Pose p, bool mirror, float k)
            {
                P = p; Mirror = mirror; K = k; Edge = Math.Max(1.4f, 1.1f / k);
                Bd = Clamp(-p.Turn, -1.2f, 1.2f) * (float)(Math.PI / 2) * YawScale;
                Hd = Clamp(-(p.Turn + p.Look), -1.25f, 1.25f) * (float)(Math.PI / 2) * YawScale;
                BCos = (float)Math.Cos(Bd); BSin = (float)Math.Sin(Bd); HCos = (float)Math.Cos(Hd); HSin = (float)Math.Sin(Hd);
                HeadC = Map(HeadLocalOrigin.X, HeadLocalOrigin.Y);
                float sq = Sq;
                HeadAng = Clamp(p.HeadTilt, -12f, 12f) + p.Lean; HeadSx = 1 + sq * .5f; HeadSy = 1 - sq;
            }

            /// <summary>몸이 눌리거나 늘어나는 정도는 최대 12%까지만 허용한다(말랑한 인형이라 조금 더 눌려도 된다).</summary>
            internal float Sq { get { return Clamp(P.Squash - P.Breath * .012f, -.12f, .12f); } }

            public PointF Map(float x, float y)
            {
                float sq = Sq;
                y = HipY + (y - HipY) * (1 - sq); x = Axis + (x - Axis) * (1 + sq * .5f);
                float w = SmoothStep((NeckY + 8 - y) / 74f);
                if (w > 0)
                {
                    double a = Clamp(P.HeadTilt, -12f, 12f) * w * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
                    float dx = x - Axis, dy = y - NeckY;
                    x = Axis + (float)(dx * ca - dy * sa) + Clamp(P.HeadX, -6f, 6f) * w; y = NeckY + (float)(dx * sa + dy * ca) + Clamp(P.HeadY, -6f, 8f) * w;
                }
                double l = P.Lean * Math.PI / 180, cl = Math.Cos(l), sl = Math.Sin(l);
                float lx = x - Axis, ly = y - LeanPivotY;
                x = Axis + (float)(lx * cl - ly * sl) + P.BodyX; y = LeanPivotY + (float)(lx * sl + ly * cl) + P.BodyY;
                return new PointF(x, y);
            }
            public PointF Map(PointF q) { return Map(q.X, q.Y); }
        }

        // ───────── 도형 도우미 ─────────
        private static PointF[] Ellipse(float cx, float cy, float rx, float ry, int n = 40)
        {
            var r = new PointF[n];
            for (int i = 0; i < n; i++) { double t = i * Math.PI * 2 / n; r[i] = new PointF(cx + (float)Math.Cos(t) * rx, cy + (float)Math.Sin(t) * ry); }
            return r;
        }

        /// <summary>입체감 있게 칠한다: 가장자리는 어둡고, 빛이 오는 쪽(hi)이 밝다.</summary>
        private static void FillBlob(Graphics g, PointF[] poly, PointF hi, Color edge, Color mid, Color light)
        {
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(poly);
                using (var pgb = new PathGradientBrush(path))
                {
                    RectangleF b = path.GetBounds();
                    pgb.CenterPoint = new PointF(Clamp(hi.X, b.Left + 1, b.Right - 1), Clamp(hi.Y, b.Top + 1, b.Bottom - 1));
                    pgb.InterpolationColors = new ColorBlend(3) { Colors = new[] { edge, mid, light }, Positions = new[] { 0f, .5f, 1f } };
                    g.FillPath(pgb, path);
                }
            }
        }

        private static void Edge(Graphics g, PointF[] poly, Ctx c, float alpha = 1f, bool closed = true)
        {
            using (var pen = new Pen(A(cBlueDark, (int)(120 * alpha)), c.Edge) { LineJoin = LineJoin.Round })
            { if (closed) g.DrawPolygon(pen, poly); else g.DrawLines(pen, poly); }
        }

        /// <summary>둥근 표면(타원체)에 붙은 점: 높이마다의 반폭(rx)에 대한 비율 u(-1..1)와 앞뒤 두께(rz)로 화면 가로 위치를 구한다.</summary>
        private static float SurfX(float u, float rx, float rz, float cs, float sn, out float depth)
        {
            u = Clamp(u, -1f, 1f);
            float x = u * rx, z = (float)Math.Sqrt(Math.Max(0f, 1 - u * u)) * rz;
            float xs = x * cs - z * sn; depth = (x * sn + z * cs) / Math.Max(1f, rz);
            if (depth < 0)
            {
                // 뒤로 돌아간 점은 윤곽선 위로 붙인다
                float sil = (float)Math.Sqrt(rx * rx * cs * cs + rz * rz * sn * sn);
                xs = (xs < 0 ? -1 : 1) * sil;
            }
            return xs;
        }

        // 얼굴 무늬를 붙일 표면: 눈 높이 위쪽은 머리통 그대로, 아래쪽(턱받이~목)은 세로로 더 긴 타원체로 이어 붙여 목까지 매끄럽게 돈다.
        private static float MapRY(float yl) { return yl > 0 ? 104f : HeadRY; }
        private static float HeadHalfAt(float yl) { float t = yl / MapRY(yl); return Math.Max(30f, HeadRX * (float)Math.Sqrt(Math.Max(0f, 1 - t * t))); }
        private static float HeadDepthAt(float yl) { float t = yl / MapRY(yl); return Math.Max(24f, HeadRZ * (float)Math.Sqrt(Math.Max(0f, 1 - t * t))); }

        /// <summary>머리 표면의 점(머리 가운데 기준 xl, yl) → 머리 틀 안의 화면 좌표. depth &gt; 0 이면 앞쪽이라 보인다.</summary>
        private static PointF HeadPt(Ctx c, float xl, float yl, out float depth)
        {
            float rx = HeadHalfAt(yl), rz = HeadDepthAt(yl);
            return new PointF(SurfX(xl / rx, rx, rz, c.HCos, c.HSin, out depth), yl);
        }

        private static float TorsoHalfAt(float y) { float t = (y - TorsoCY) / TorsoRY; return Math.Max(18f, TorsoRX * (float)Math.Sqrt(Math.Max(0f, 1 - t * t))); }
        private static float TorsoDepthAt(float y) { float t = (y - TorsoCY) / TorsoRY; return Math.Max(14f, TorsoRZ * (float)Math.Sqrt(Math.Max(0f, 1 - t * t))); }

        /// <summary>몸통 표면의 점(가운데에서 옆으로 xo, 기준 y) → 화면 좌표.</summary>
        private static PointF BodyPt(Ctx c, float xo, float y, out float depth)
        {
            float rx = TorsoHalfAt(y), rz = TorsoDepthAt(y);
            float xs = SurfX(xo / rx, rx, rz, c.BCos, c.BSin, out depth);
            return c.Map(Axis + xs, y);
        }

        // ───────── 그리기 ─────────
        /// <summary>g 의 원점이 발 가운데(픽셀)라고 가정하고 그린다. k = 기준 단위 1이 화면 몇 픽셀인지.</summary>
        internal static void Draw(Graphics g, Pose p, float k, bool faceRight)
        {
            var saved = g.Save();
            g.ScaleTransform(faceRight ? -k : k, k);
            g.TranslateTransform(-RefFootX, -RefFootY);
            var c = new Ctx(p, faceRight, k);
            DrawBackpack(g, c);
            DrawTail(g, c);
            for (int s = 0; s < 2; s++) if (!ArmInFront(c, s)) DrawArm(g, c, s);
            if (!p.Sit) DrawLegs(g, c);
            DrawTorso(g, c);
            DrawStraps(g, c);
            if (p.Outfit) DrawSash(g, c);
            if (p.Sit) DrawSitFeet(g, c); else DrawFeet(g, c);
            DrawHead(g, c);
            for (int s = 0; s < 2; s++) if (ArmInFront(c, s)) DrawArm(g, c, s);
            if (p.Tool == ToolKind.Camera) { Arm a = ArmGeom(c, 1); DrawCamera(g, c, a.W); }
            if (p.Tool == ToolKind.Flag) { Arm a = ArmGeom(c, 1); DrawFlag(g, c, a.W); }
            if (p.Cookie >= 0) DrawCookie(g, c);
            g.Restore(saved);
        }

        // ───────── 몸통 ─────────
        private static PointF[] TorsoOutline(Ctx c)
        {
            float hw = (float)Math.Sqrt(TorsoRX * TorsoRX * c.BCos * c.BCos + TorsoRZ * TorsoRZ * c.BSin * c.BSin);
            PointF[] e = Ellipse(Axis, TorsoCY, hw, TorsoRY, 44);
            for (int i = 0; i < e.Length; i++) e[i] = c.Map(e[i]);
            return e;
        }

        private static void DrawTorso(Graphics g, Ctx c)
        {
            PointF[] body = TorsoOutline(c);
            // 머리가 몸에 드리우는 그림자
            PointF hip = c.Map(Axis - 14 * c.BSin, 214);
            FillBlob(g, body, c.Map(Axis - 18 - 10 * c.BSin, 200), cBlueLo, cBlue, cBlueHi);
            // 흰 배: 몸통 표면에 붙은 둥근 덩어리
            var belly = new List<PointF>(); var vis = 0;
            float[][] right = { new[] { .50f, 168f }, new[] { .66f, 186f }, new[] { .74f, 208f }, new[] { .76f, 234f }, new[] { .68f, 258f }, new[] { .48f, 276f }, new[] { .18f, 284f } };
            var pts = new List<PointF>();
            for (int i = 0; i < right.Length; i++)
            {
                float d; float y = right[i][1], xo = -right[i][0] * TorsoHalfAt(y);
                pts.Add(BodyPt(c, xo, y, out d)); if (d > 0) vis++;
            }
            for (int i = right.Length - 1; i >= 0; i--)
            {
                float d; float y = right[i][1], xo = right[i][0] * TorsoHalfAt(y);
                pts.Add(BodyPt(c, xo, y, out d));
            }
            if (vis >= 2)
            {
                PointF[] bp = Geo.Smooth(pts.ToArray(), true, 4);
                var st = g.Save();
                using (var clip = new GraphicsPath()) { clip.AddPolygon(body); g.SetClip(clip, CombineMode.Intersect); }
                float d0; PointF ctr = BodyPt(c, -10, 226, out d0);
                FillBlob(g, bp, new PointF(ctr.X, ctr.Y - 14), cWhiteLo, cWhite, cWhite);
                g.Restore(st);
            }
            Edge(g, body, c, .8f);
        }

        // ───────── 가방과 어깨끈 ─────────
        private static void DrawBackpack(Graphics g, Ctx c)
        {
            // 몸 뒤에 걸린 검은 가방: 앞에서 보면 몸 양옆으로 살짝 삐져나오고, 돌면 뒤로 크게 보인다
            float zc = -42f, rx = 76f, rz = 30f;
            float hw = (float)Math.Sqrt(rx * rx * c.BCos * c.BCos + rz * rz * c.BSin * c.BSin);
            float cx = Axis - zc * c.BSin;
            PointF[] bag = Ellipse(cx, 224, hw, 60, 40);
            for (int i = 0; i < bag.Length; i++) bag[i] = c.Map(bag[i]);
            FillBlob(g, bag, c.Map(cx - hw * .3f, 196), Color.FromArgb(12, 12, 16), cBag, cBagHi);   //ref
            // 가방 윗덮개 선
            using (var pen = new Pen(A(cBagHi, 140), c.Edge * 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pen, c.Map(cx - hw * .7f, 190), c.Map(cx + hw * .7f, 190));
        }

        private static void DrawStraps(Graphics g, Ctx c)
        {
            // 어깨끈: 어깨에서 배 옆을 따라 내려와 허리에서 가방에 이어진다(검은 띠 + 은색 버클)
            for (int side = -1; side <= 1; side += 2)
            {
                float[][] path = { new[] { .60f, 172f }, new[] { .64f, 192f }, new[] { .68f, 214f }, new[] { .70f, 238f }, new[] { .66f, 262f } };
                var l = new List<PointF>(); float dMin = 9; float dMid = 0;
                for (int i = 0; i < path.Length; i++)
                {
                    float d, y = path[i][1], xo = side * path[i][0] * TorsoHalfAt(y);
                    l.Add(BodyPt(c, xo, y, out d)); dMin = Math.Min(dMin, d); if (i == 2) dMid = d;
                }
                if (dMid < .12f) continue;
                PointF[] line = Geo.Smooth(l.ToArray(), false, 6);
                using (var pen = new Pen(Color.FromArgb(22, 22, 28), 17f * Math.Max(.45f, dMid)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })   //ref
                    g.DrawLines(pen, line);
                using (var hi = new Pen(A(cBagHi, 110), 4f * Math.Max(.45f, dMid)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(hi, line);
                // 버클
                PointF b = l[2]; float bw = 13f * Math.Max(.4f, dMid);
                using (var br = new SolidBrush(Color.FromArgb(70, 70, 80)))   //ref
                using (var rim = new Pen(Color.FromArgb(150, 150, 162), 1.4f))   //ref
                using (var path2 = Rig.RoundRect(new RectangleF(b.X - bw / 2, b.Y - 6, bw, 12), 2.4f))
                { g.FillPath(br, path2); g.DrawPath(rim, path2); }
            }
        }

        // ───────── 홍보대사 어깨띠(스티커의 하얀 어깨띠 + 금색 메달) ─────────
        private static void DrawSash(Graphics g, Ctx c)
        {
            // 몸통 표면에 붙은 비스듬한 띠(어깨에서 반대쪽 엉덩이로)
            float[][] top = { new[] { -.60f, 172f }, new[] { -.38f, 190f }, new[] { -.10f, 208f }, new[] { .22f, 230f }, new[] { .52f, 252f }, new[] { .74f, 272f } };
            float half = 13f;
            var upper = new List<PointF>(); var lower = new List<PointF>(); var mid = new List<PointF>(); float dm = 1;
            for (int i = 0; i < top.Length; i++)
            {
                float y = top[i][1], rx = TorsoHalfAt(y), xo = top[i][0] * rx, d, d2, d3;
                float dy = i + 1 < top.Length ? top[i + 1][1] - y : y - top[i - 1][1], dx = i + 1 < top.Length ? (top[i + 1][0] - top[i][0]) * rx : (top[i][0] - top[i - 1][0]) * rx;
                float len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < 1e-3f) len = 1;
                float nx = -dy / len, ny = dx / len;
                upper.Add(BodyPt(c, xo + nx * half, y + ny * half, out d)); lower.Add(BodyPt(c, xo - nx * half, y - ny * half, out d2));
                mid.Add(BodyPt(c, xo, y, out d3)); if (i > 0 && i < top.Length - 1) dm = Math.Min(dm, d3);
            }
            if (dm < .1f) return;
            var poly = new List<PointF>(Geo.Smooth(upper.ToArray(), false, 5));
            var lowS = Geo.Smooth(lower.ToArray(), false, 5); Array.Reverse(lowS); poly.AddRange(lowS);
            var st = g.Save();
            using (var clip = new GraphicsPath()) { clip.AddPolygon(TorsoOutline(c)); g.SetClip(clip, CombineMode.Intersect); }
            using (var fill = new SolidBrush(Color.FromArgb(250, 246, 232))) g.FillPolygon(fill, poly.ToArray());   //ref
            using (var gold = new Pen(Color.FromArgb(214, 176, 80), c.Edge * 1.3f) { LineJoin = LineJoin.Round }) g.DrawPolygon(gold, poly.ToArray());   //ref
            // 글씨처럼 보이는 남색 점선
            PointF[] m = Geo.Smooth(mid.ToArray(), false, 8);
            using (var ink = new Pen(Color.FromArgb(46, 66, 150), 3.6f) { DashPattern = new[] { 1.4f, 1.1f, .6f, 1.1f, 2.2f, 1.1f }, StartCap = LineCap.Flat, EndCap = LineCap.Flat })   //ref
                g.DrawLines(ink, m);
            g.Restore(st);
            // 금색 메달과 리본: 띠 끝(엉덩이 쪽)에 달린다
            PointF end = lower[lower.Count - 1], endU = upper[upper.Count - 1];
            PointF mc = new PointF((end.X + endU.X) / 2 + 2, (end.Y + endU.Y) / 2 + 3);
            using (var ribbon = new SolidBrush(Color.FromArgb(232, 196, 96))) g.FillPolygon(ribbon, new[] { new PointF(mc.X - 5, mc.Y - 4), new PointF(mc.X - 9, mc.Y + 13), new PointF(mc.X - 3, mc.Y + 9), new PointF(mc.X + 1, mc.Y + 14), new PointF(mc.X + 4, mc.Y - 3) });   //ref
            using (var gold = new SolidBrush(Color.FromArgb(252, 206, 72))) using (var rim = new Pen(Color.FromArgb(200, 140, 30), 1.6f))   //ref
            { g.FillEllipse(gold, mc.X - 7, mc.Y - 7, 14, 14); g.DrawEllipse(rim, mc.X - 7, mc.Y - 7, 14, 14); g.FillEllipse(Brushes.White, mc.X - 3.5f, mc.Y - 4.5f, 3f, 3f); }
        }

        // ───────── 꼬리: 몸 뒤쪽 아래의 파란 지느러미(앞에서는 오른쪽 아래로 삐죽, 옆에서는 뒤로 쭉) ─────────
        private static void DrawTail(Graphics g, Ctx c)
        {
            Pose p = c.P;
            PointF baseP = c.Map(Axis + 44 * c.BCos + 38 * c.BSin, 268);
            float dirx = .8f * c.BCos + .95f * c.BSin, diry = .5f;
            double ang = Math.Atan2(diry, dirx) * 180 / Math.PI + p.Tail + p.TailWag * .6f;
            float len = 52f + 12f * Math.Abs(c.BSin);
            var st = g.Save();
            g.TranslateTransform(baseP.X, baseP.Y); g.RotateTransform((float)ang);
            var pts = new[]
            {
                new PointF(-6, -15), new PointF(len * .35f, -17), new PointF(len * .72f, -10), new PointF(len, 2),
                new PointF(len * .74f, 8), new PointF(len * .42f, 14), new PointF(-6, 16)
            };
            PointF[] poly = Geo.Smooth(pts, true, 5);
            FillBlob(g, poly, new PointF(len * .25f, -6), cBlueLo, cBlue, cBlueHi);
            Edge(g, poly, c, .9f);
            g.Restore(st);
        }

        // ───────── 다리 · 발 ─────────
        private static void DrawLegs(Graphics g, Ctx c)
        {
            Pose p = c.P;
            for (int side = 0; side < 2; side++)
            {
                int sign = side == 0 ? -1 : 1;
                float fx = side == 0 ? p.FootLX : p.FootRX, fy = side == 0 ? p.FootLY : p.FootRY;
                PointF hip = c.Map(Axis + sign * 34 * c.BCos, 262);
                PointF ankle = new PointF(Axis + sign * 36 * c.BCos + fx * .6f + p.BodyX * .5f, 282 + fy);
                using (var leg = new Pen(cBlue, 32f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(leg, hip, ankle);
            }
        }

        private static void DrawFeet(Graphics g, Ctx c)
        {
            Pose p = c.P;
            for (int side = 0; side < 2; side++)
            {
                int sign = side == 0 ? -1 : 1;
                float fx = side == 0 ? p.FootLX : p.FootRX, fy = side == 0 ? p.FootLY : p.FootRY, rot = side == 0 ? p.FootLRot : p.FootRRot;
                // 발끝은 몸이 향한 쪽으로, 바깥으로 살짝 벌어진다
                float psi = c.Bd + (sign < 0 ? .30f : -.30f), v = (float)Math.Sin(psi);
                float cx = Axis + sign * 40 * c.BCos + p.BodyX * .5f - 8f * v + fx, cy = 285 + fy;
                float rx = Pose.Lerp(30f, 36f, Math.Abs(v)), ry = 15.5f;
                var st = g.Save();
                g.TranslateTransform(cx, cy + ry * .1f); g.RotateTransform(rot * .6f);
                PointF[] foot = Ellipse(0, 0, rx, ry, 32);
                FillBlob(g, foot, new PointF(-rx * .3f, -ry * .5f), cBlueLo, cBlue, cBlueHi);
                Edge(g, foot, c, .9f);
                // 발가락 선(발끝 쪽에 짧은 줄 두 개)
                float tx = -Math.Sign(v == 0 ? 1 : v) * rx * (.52f + .2f * Math.Abs(v)); if (Math.Abs(v) < .12f) tx = 0;
                using (var toe = new Pen(A(cBlueDark, 120), c.Edge) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    if (Math.Abs(v) < .12f) { g.DrawLine(toe, -9, ry * .35f, -8, ry * .85f); g.DrawLine(toe, 9, ry * .35f, 8, ry * .85f); }
                    else { float dx = tx; g.DrawLine(toe, dx, -ry * .35f, dx * 1.02f, ry * .5f); g.DrawLine(toe, dx * .78f, -ry * .5f, dx * .8f, ry * .62f); }
                }
                g.Restore(st);
            }
        }

        /// <summary>앉았을 때: 두 다리를 앞으로 내밀어 동글한 발바닥이 보인다.</summary>
        private static void DrawSitFeet(Graphics g, Ctx c)
        {
            Pose p = c.P;
            for (int side = 0; side < 2; side++)
            {
                int sign = side == 0 ? -1 : 1;
                float fx = side == 0 ? p.FootLX : p.FootRX, fy = side == 0 ? p.FootLY : p.FootRY, rot = side == 0 ? p.FootLRot : p.FootRRot;
                PointF at = c.Map(Axis + sign * 46 * c.BCos + fx, 282 + fy);
                var st = g.Save();
                g.TranslateTransform(at.X, at.Y); g.RotateTransform(sign * (10 + rot * .5f));
                PointF[] foot = Ellipse(0, 0, 29, 22, 32);
                FillBlob(g, foot, new PointF(-8, -8), cBlueLo, cBlue, cBlueHi);
                Edge(g, foot, c, .9f);
                PointF[] pad = Ellipse(0, 3, 16, 12, 24);
                using (var br = new SolidBrush(A(cBlueHi, 150))) g.FillPolygon(br, pad);
                g.Restore(st);
            }
        }

        // ───────── 머리: 표면에 붙은 얼굴 무늬는 모두 머리 틀 안에서 그린다 ─────────
        private static void DrawHead(Graphics g, Ctx c)
        {
            Pose p = c.P;
            // 머리 아래 그림자(몸에 드리움)
            {
                PointF s0 = c.Map(Axis, 198);
                using (var br = new SolidBrush(A(cBlueDark, 46)))
                    g.FillEllipse(br, s0.X - 62, s0.Y - 10, 124, 22);
            }
            var st = g.Save();
            g.TranslateTransform(c.HeadC.X, c.HeadC.Y); g.RotateTransform(c.HeadAng); g.ScaleTransform(c.HeadSx, c.HeadSy);
            float cs = c.HCos, sn = c.HSin;
            float hw = (float)Math.Sqrt(HeadRX * HeadRX * cs * cs + HeadRZ * HeadRZ * sn * sn);

            // 머리통
            PointF[] skull = Ellipse(0, 0, hw, HeadRY, 56);
            FillBlob(g, skull, new PointF(-hw * .28f - sn * 14, -HeadRY * .42f), cBlueLo, cBlue, cBlueHi);

            using (var pen = new Pen(A(cBlueDark, 90), c.Edge) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, skull);   // 머리 윤곽의 은은한 그림자(턱받이가 위를 덮는다)
            DrawFin(g, c, hw);
            DrawCheekStripes(g, c);
            DrawMuzzle(g, c);
            DrawBlush(g, c);
            DrawEye(g, c, false); DrawEye(g, c, true);
            DrawMouth(g, c);
            if (p.Tears >= 0) DrawTears(g, c);
            if (p.Sweat >= 0) DrawSweat(g, p.Sweat);
            if (p.Anger > .01f) DrawAnger(g, p);
            if (p.Puff >= 0) DrawPuff(g, p.Puff, -(HeadRX + 8) * sn);
            DrawGlasses(g, c, hw);
            if (p.Sparkle >= 0) DrawSparkle(g, p.Sparkle);
            g.Restore(st);
        }

        /// <summary>머리 위 등지느러미: 선글라스 뒤로 삐죽 솟은 밝은 파랑 삼각형. 흔들림(Tuft)에 따라 끝이 휘청인다.</summary>
        private static void DrawFin(Graphics g, Ctx c, float hw)
        {
            Pose p = c.P;
            float cs = c.HCos, sn = c.HSin;
            float baseY = -58f, bx = -34f * sn;                       // 머리 윗면 가운데(살짝 앞)
            float sway = (p.Tuft + (float)Math.Sin(p.TuftPhase) * p.TuftWave) * (float)(Math.PI / 180);
            float tipX = bx + 12f * cs + 20f * sn + (float)Math.Sin(sway) * 20f, tipY = -102f + (1 - (float)Math.Cos(sway)) * 10f;
            float wL = 28f * (.62f + .38f * Math.Abs(cs)) + 6f, wR = 22f * (.62f + .38f * Math.Abs(cs)) + 6f;
            var pts = new[]
            {
                new PointF(bx - wL, baseY + 10), new PointF(bx - wL * .6f, baseY - 14), new PointF((bx + tipX) / 2 - 6, (baseY + tipY) / 2 - 4),
                new PointF(tipX, tipY), new PointF(tipX - 2 + (float)Math.Sin(sway) * 4, (baseY + tipY) / 2 + 12),
                new PointF(bx + wR, baseY - 4), new PointF(bx + wR + 2, baseY + 12)
            };
            PointF[] poly = Geo.Smooth(pts, true, 5);
            FillBlob(g, poly, new PointF(bx - 6, baseY - 22), cBlueLo, Mix(cBlue, cBlueHi, .45f), cBlueHi);
            using (var pen = new Pen(A(cBlueDark, 100), c.Edge) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, poly);
        }

        private static void DrawCheekStripes(Graphics g, Ctx c)
        {
            // 볼 바깥쪽의 짙은 파랑 아가미 줄무늬: 왼쪽 2~3줄, 오른쪽 2~3줄
            using (var pen = new Pen(Color.FromArgb(160, 36, 84, 190), 3.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })   //ref
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 3; i++)
                    {
                        float d0, d1;
                        float y0 = -6 + i * 10f, y1 = y0 + 3f;
                        PointF a = HeadPt(c, side * (72f - i * 2f), y0, out d0), b = HeadPt(c, side * (86f - i * 3f), y1, out d1);
                        if (d0 < .2f || d1 < .1f) continue;
                        g.DrawLine(pen, a, b);
                    }
        }

        /// <summary>하얀 턱받이: 볼에서 목으로 모이는 둥근 흰 면(몸통의 흰 배로 이어진다). 위쪽 가운데가 살짝 솟은 주둥이.</summary>
        private static void DrawMuzzle(Graphics g, Ctx c)
        {
            float[][] left =
            {
                new[] { -40f, 92f }, new[] { -52f, 74f }, new[] { -58f, 56f }, new[] { -63f, 38f }, new[] { -66f, 20f }, new[] { -56f, 8f }, new[] { -40f, 1f },
                new[] { -22f, -4f }, new[] { -8f, -8f }, new[] { 0f, -9f }
            };
            var pts = new List<PointF>(); int vis = 0;
            foreach (float[] q in left) { float d; pts.Add(HeadPt(c, q[0], q[1], out d)); if (d > 0) vis++; }
            for (int i = left.Length - 2; i >= 0; i--) { float d; pts.Add(HeadPt(c, -left[i][0], left[i][1], out d)); }
            if (vis < 2) return;
            PointF[] poly = Geo.Smooth(pts.ToArray(), true, 5);
            float d1; PointF mid = HeadPt(c, 0, 8, out d1);
            var st = g.Save();
            using (var clip = new GraphicsPath(FillMode.Winding))
            {
                // 머리통과 목(몸 위쪽)에만 칠한다
                clip.AddPolygon(Ellipse(0, 0, (float)Math.Sqrt(HeadRX * HeadRX * c.HCos * c.HCos + HeadRZ * HeadRZ * c.HSin * c.HSin), HeadRY, 56));
                clip.AddPolygon(Ellipse(0, 86, 60f, 34f, 32));
                g.SetClip(clip, CombineMode.Intersect);
            }
            using (var wb = new SolidBrush(cWhite)) g.FillPolygon(wb, poly);
            g.Restore(st);
            // 콧구멍 두 점
            float dn; PointF n0 = HeadPt(c, -8, 1.5f, out dn); PointF n1 = HeadPt(c, 8, 1.5f, out dn);
            if (dn > .25f)
                using (var br = new SolidBrush(A(cBlueDark, 150))) { g.FillEllipse(br, n0.X - 1.6f, n0.Y - 1.6f, 3.2f, 3.2f); g.FillEllipse(br, n1.X - 1.6f, n1.Y - 1.6f, 3.2f, 3.2f); }
        }

        private static void DrawBlush(Graphics g, Ctx c)
        {
            float b = Math.Min(1f, c.P.Blush);
            if (b < .01f) return;
            using (var br = new SolidBrush(Color.FromArgb((int)(170 * b), cPink.R, cPink.G, cPink.B)))
                for (int s = -1; s <= 1; s += 2)
                {
                    float d; PointF at = HeadPt(c, s * 56f, 14f, out d);
                    float f = Math.Max(0f, d);
                    if (f > .12f) g.FillEllipse(br, at.X - 12 * f, at.Y - 6, 24 * f, 13);
                }
        }

        /// <summary>눈: 반짝이는 검은 구슬. 얼굴이 돌면 둥근 얼굴 표면을 따라 이동하고 좁아진다.</summary>
        private static void DrawEye(Graphics g, Ctx c, bool right)
        {
            Pose p = c.P;
            float depth; PointF at = HeadPt(c, (right ? 1 : -1) * 42f, -10f, out depth);
            float f = Math.Max(0f, Math.Min(1f, depth));
            if (f < .16f) return;
            float open = right ? p.OpenR : p.OpenL, es = Clamp(p.EyeScale, .95f, 1.1f), inner = right ? -1 : 1;
            var st = g.Save();
            g.TranslateTransform(at.X + p.Gx * 2.4f, at.Y + p.Gy * 2f); g.ScaleTransform(f, 1);
            float w = 4.6f;
            using (var line = new Pen(cInk, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                EyeStyle style = p.Eyes;
                if (style == EyeStyle.Spiral)
                {
                    float r = 11f;
                    g.FillEllipse(Brushes.White, -r, -r, r * 2, r * 2); using (var rim = new Pen(cInk, 2f)) g.DrawEllipse(rim, -r, -r, r * 2, r * 2);
                    var pts = new List<PointF>();
                    using (var sp = new Pen(cInk, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    {
                        for (int i = 0; i <= 40; i++) { double a = i * .46 + p.SpiralT * (right ? -1 : 1), rr = 1 + i * (r - 2.5) / 40; pts.Add(new PointF((float)(Math.Cos(a) * rr), (float)(Math.Sin(a) * rr))); }
                        g.DrawLines(sp, pts.ToArray());
                    }
                    g.Restore(st); return;
                }
                if ((style == EyeStyle.Dot || style == EyeStyle.Wide) && open < .14f) style = EyeStyle.Closed;
                switch (style)
                {
                    case EyeStyle.Dot:
                    case EyeStyle.Wide:
                    {
                        float r = 7.6f * es * (style == EyeStyle.Wide ? 1.14f : 1f), hh = r * Math.Max(.14f, open);
                        using (var br = new SolidBrush(Color.FromArgb(14, 14, 20))) g.FillEllipse(br, -r, -hh, r * 2, hh * 2);   //ref
                        if (hh > r * .5f)
                        {
                            g.FillEllipse(Brushes.White, -r * .5f, -hh * .62f, r * .62f, hh * .62f);
                            using (var br = new SolidBrush(Color.FromArgb(170, 255, 255, 255))) g.FillEllipse(br, r * .12f, hh * .1f, r * .3f, hh * .3f);   //ref
                        }
                        break;
                    }
                    case EyeStyle.Half:
                    {
                        float r = 7.4f;
                        using (var br = new SolidBrush(Color.FromArgb(14, 14, 20))) g.FillEllipse(br, -r, -r * .5f, r * 2, r * 1.5f);   //ref
                        g.DrawLine(line, -9, -3 - p.Brow * 2 * inner, 9, -3 + p.Brow * 2 * inner);
                        break;
                    }
                    case EyeStyle.Closed:
                        g.DrawArc(line, -8, -6, 16, 11, 25, 130);
                        break;
                    case EyeStyle.Happy:
                    case EyeStyle.Squint:
                        // 스티커의 웃는 눈: 안쪽을 향한 굵은 꺾쇠 ( > < )
                        g.DrawLines(line, new[] { new PointF(-inner * 7.5f, -7), new PointF(inner * 4.5f, 0), new PointF(-inner * 7.5f, 7) });
                        break;
                }
                if (Math.Abs(p.Brow) > .05f && (style == EyeStyle.Dot || style == EyeStyle.Wide || style == EyeStyle.Half))
                {
                    float by = style == EyeStyle.Wide ? -19 : -16;
                    using (var brow = new Pen(Color.FromArgb(30, 66, 150), 3.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })   //ref
                        g.DrawLine(brow, inner * 11, by + p.Brow * 6f, -inner * 11, by - p.Brow * 4f);
                }
            }
            g.Restore(st);
        }

        /// <summary>입: 닫으면 작은 웃음 줄, 벌리면 빨간 입속에 분홍 혀와 하얀 이빨. 폭은 얼굴이 돌면 줄어든다.</summary>
        private static void DrawMouth(Graphics g, Ctx c)
        {
            Pose p = c.P;
            float open = Clamp(p.Beak, 0, 1.2f), dOpen = open * 34f;
            float d0; PointF cen = HeadPt(c, 0, 12f, out d0);
            if (d0 < .2f) return;
            float half = 36f;
            if (dOpen < 2.5f)
            {
                // 다문 입: 작은 웃음 + 하얀 잔이빨
                var arc = new List<PointF>();
                for (int i = 0; i <= 14; i++)
                {
                    float t = i / 14f, xl = -26 + 52 * t, yl = 17 + 8 * (float)Math.Sin(t * Math.PI), d;
                    PointF q = HeadPt(c, xl, yl, out d); arc.Add(new PointF(q.X + p.BeakShake, q.Y));
                }
                using (var br = new SolidBrush(Color.FromArgb(250, 250, 252)))   //ref
                    for (int i = 1; i < arc.Count - 1; i += 2) g.FillRectangle(br, arc[i].X - 1.6f, arc[i].Y - 1.2f, 3.2f, 4.2f);
                using (var pen = new Pen(Color.FromArgb(200, 70, 90), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(pen, arc.ToArray());   //ref
                return;
            }
            // 벌린 입: 위는 곧은 선(왼→오), 아래는 둥근 반원(오→왼)
            var ordered = new List<PointF>();
            for (int i = 0; i <= 16; i++) { float xl = -half + 2 * half * i / 16f, d; PointF q = HeadPt(c, xl, 12f, out d); ordered.Add(new PointF(q.X + p.BeakShake, q.Y)); }
            for (int i = 1; i < 16; i++)
            {
                double t = Math.PI * i / 16; float xl = half * (float)Math.Cos(t), yl = 12f + dOpen * (float)Math.Sin(t), d;
                PointF q = HeadPt(c, xl, yl, out d); ordered.Add(new PointF(q.X + p.BeakShake, q.Y));
            }
            PointF[] mouth = ordered.ToArray();
            var st = g.Save();
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(mouth); g.SetClip(path, CombineMode.Intersect);
                RectangleF b = path.GetBounds();
                using (var lg = new LinearGradientBrush(new RectangleF(b.X, b.Y - 1, b.Width, b.Height + 2), cMouthDark, cMouth, 90f)) g.FillPath(lg, path);
                // 혀
                float tr = Math.Min(12f, dOpen * .5f + 3), d1; PointF tc = HeadPt(c, 0, 12f + dOpen - tr * .6f, out d1);
                float tw = 20f * Math.Max(.4f, Math.Abs(c.HCos));
                using (var tb = new SolidBrush(cTongue)) g.FillEllipse(tb, tc.X - tw, tc.Y - tr * .85f, tw * 2, tr * 1.7f);
                using (var tl = new Pen(Color.FromArgb(200, 70, 100), 1.6f)) g.DrawLine(tl, tc.X, tc.Y - tr * .4f, tc.X, tc.Y + tr * .5f);   //ref
                // 위 이빨: 하얀 삼각형 다섯 개
                using (var tb = new SolidBrush(Color.FromArgb(252, 252, 252)))   //ref
                    for (int i = 0; i < 5; i++)
                    {
                        float xl = -26 + i * 13f, d2; PointF a = HeadPt(c, xl - 5.5f, 12f, out d2), bpt = HeadPt(c, xl + 5.5f, 12f, out d2), tip = HeadPt(c, xl, 12f + Math.Min(9.5f, 4f + dOpen * .35f), out d2);
                        g.FillPolygon(tb, new[] { new PointF(a.X + p.BeakShake, a.Y - 1), new PointF(bpt.X + p.BeakShake, bpt.Y - 1), new PointF(tip.X + p.BeakShake, tip.Y) });
                    }
                // 아래 이빨: 입을 크게 벌릴 때만 작은 세 개
                if (dOpen > 16f)
                    using (var tb = new SolidBrush(Color.FromArgb(240, 240, 244)))   //ref
                        for (int i = -1; i <= 1; i++)
                        {
                            float d3; float yb = 12f + dOpen - 2f;
                            PointF a = HeadPt(c, i * 14f - 4.5f, yb, out d3), bpt = HeadPt(c, i * 14f + 4.5f, yb, out d3), tip = HeadPt(c, i * 14f, yb - 7f, out d3);
                            g.FillPolygon(tb, new[] { a, bpt, tip });
                        }
            }
            g.Restore(st);
            using (var edge = new Pen(Color.FromArgb(130, 20, 40), 1.6f) { LineJoin = LineJoin.Round }) g.DrawPolygon(edge, mouth);   //ref
        }

        // ───────── 선글라스: 머리 위에 걸치거나(GlassDrop 0) 눈 위로 내려쓴다(1) ─────────
        private static void DrawGlasses(Graphics g, Ctx c, float hw)
        {
            Pose p = c.P;
            float d = Clamp(p.GlassDrop, 0, 1);
            float y = Pose.Lerp(-46f, -10f, d);
            float cs = c.HCos, sn = c.HSin;
            float lensW = 30f, lensH = 21.5f;
            var st = g.Save();
            g.TranslateTransform(0, y); g.RotateTransform(Pose.Lerp(3.5f, 0f, d) + p.GlassTilt);
            // 안경다리: 머리 옆면을 따라 귀 쪽으로 내려간다(가까운 쪽은 항상, 먼 쪽은 거의 정면일 때만)
            using (var temple = new Pen(Color.FromArgb(18, 18, 22), 6.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })   //ref
                for (int s = -1; s <= 1; s += 2)
                {
                    float d0, d1, d2;
                    PointF a = HeadPt(c, s * 64f, -4f, out d0), b = HeadPt(c, s * 84f, 8f - d * 6f, out d1), e = HeadPt(c, s * 90f, 30f - d * 14f, out d2);
                    if (d1 < .02f && Math.Abs(sn) > .1f) continue;
                    g.DrawLines(temple, new[] { a, b, e });
                }
            for (int s = -1; s <= 1; s += 2)
            {
                float dd; PointF at = HeadPt(c, s * 35f, 0f, out dd);
                float sc = Math.Max(.1f, Math.Abs(cs) * .75f + Math.Abs(sn) * .25f);
                float lw = lensW * sc;
                var lst = g.Save();
                g.TranslateTransform(at.X, at.Y);
                using (var path = Rig.RoundRect(new RectangleF(-lw, -lensH, lw * 2, lensH * 2), Math.Min(14f, lw * .8f)))
                {
                    using (var frame = new Pen(Color.FromArgb(10, 10, 14), 4.6f) { LineJoin = LineJoin.Round }) g.DrawPath(frame, path);   //ref
                    using (var lg = new LinearGradientBrush(new RectangleF(-lw, -lensH, lw * 2, lensH * 2), Color.FromArgb(52, 54, 62), Color.FromArgb(8, 8, 12), 70f)) g.FillPath(lg, path);   //ref
                    var cl = g.Save(); g.SetClip(path, CombineMode.Intersect);
                    using (var sh = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))   //ref
                        g.FillPolygon(sh, new[] { new PointF(-lw * .62f, -lensH), new PointF(-lw * .18f, -lensH), new PointF(-lw * .55f, lensH * .1f), new PointF(-lw * .95f, lensH * .1f) });
                    using (var sh = new SolidBrush(Color.FromArgb(34, 255, 255, 255))) g.FillEllipse(sh, lw * .1f, lensH * .25f, lw * .7f, lensH * .5f);   //ref
                    g.Restore(cl);
                }
                g.Restore(lst);
            }
            // 코다리(두 렌즈 사이)
            {
                float dd; PointF a = HeadPt(c, -8f, -4f, out dd), b = HeadPt(c, 8f, -4f, out dd);
                using (var bridge = new Pen(Color.FromArgb(14, 14, 18), 5.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(bridge, a, b);   //ref
            }
            g.Restore(st);
        }

        // ───────── 얼굴 장식 ─────────
        private static void DrawPuff(Graphics g, float q, float shift)
        {
            // 후~ 내쉬는 한숨: 입 옆에서 작은 구름 세 개가 퍼지며 사라진다
            float a = Math.Max(0, 1 - q);
            using (var edge = new Pen(Color.FromArgb((int)(210 * a), 255, 255, 255), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < 3; i++)
                {
                    float x = -84 + shift * .3f - q * 22 - i * 7 * q, y = 30 - q * 8 + (i - 1) * 7 * q, r = 4 + i * 1.5f + q * 3;
                    g.DrawArc(edge, x - r, y - r, r * 2, r * 2, 60, 250);
                }
        }

        private static void DrawTears(Graphics g, Ctx c)
        {
            float t = c.P.Tears;
            for (int side = 0; side < 2; side++)
            {
                float dir = side == 0 ? -1 : 1, depth;
                PointF e = HeadPt(c, dir * 42f, -2f, out depth);
                if (depth < .2f) continue;
                using (var streak = new Pen(Color.FromArgb(200, 170, 220, 255), 5.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })   //ref
                    g.DrawLine(streak, e.X + dir * 1, 6, e.X + dir * 5, 30 + 5 * (float)Math.Sin(t * 9 + side));
                for (int i = 0; i < 2; i++)
                {
                    float q = (t * 1.5f + i * .5f + side * .27f) % 1f;
                    float px = e.X + dir * (3 + q * 12), py = 14 + q * 74, r = 5.4f - 2f * q;
                    int a = (int)(235 * (1 - q * q));
                    using (var drop = new SolidBrush(Color.FromArgb(a, 130, 200, 255)))   //ref
                    using (var rim = new Pen(Color.FromArgb(a, 60, 120, 200), 1.8f))   //ref
                    { g.FillEllipse(drop, px - r, py - r * 1.3f, r * 2, r * 2.6f); g.DrawEllipse(rim, px - r, py - r * 1.3f, r * 2, r * 2.6f); }
                }
            }
        }

        private static void DrawSweat(Graphics g, float t)
        {
            float q = t % 1.2f / 1.2f;
            float x = 82 + 5 * q, y = -40 + q * 40; int a = (int)(235 * (1 - q * q * q));
            using (var drop = new SolidBrush(Color.FromArgb(a, 180, 224, 255)))   //ref
            using (var rim = new Pen(Color.FromArgb(a, 70, 130, 210), 2.6f))   //ref
            {
                var pts = new[] { new PointF(x, y - 14), new PointF(x + 7, y + 1), new PointF(x, y + 8), new PointF(x - 7, y + 1) };
                var path = new GraphicsPath(); path.AddCurve(pts, 0f); path.CloseFigure();
                g.FillPath(drop, path); g.DrawPath(rim, path); path.Dispose();
            }
        }

        private static void DrawAnger(Graphics g, Pose p)
        {
            // 만화식 💢 : 머리 옆에서 두근거리며 커졌다 작아진다
            float[][] spots = { new[] { 92f, -64f }, new[] { -94f, -50f } };
            float pulse = 1 + .12f * (float)Math.Sin(p.Anger * 40);
            for (int i = 0; i < 2; i++)
            {
                float s = (i == 0 ? 11f : 9f) * pulse * Math.Min(1f, p.Anger * 1.2f);
                var st = g.Save();
                g.TranslateTransform(spots[i][0], spots[i][1]);
                using (var pen = new Pen(Color.FromArgb(226, 56, 44), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })   //ref
                {
                    g.DrawArc(pen, -s - 3, -s - 3, s + 1, s + 1, 100, 80); g.DrawArc(pen, 2, -s - 3, s + 1, s + 1, 280, 80);
                    g.DrawArc(pen, -s - 3, 2, s + 1, s + 1, 80, -80); g.DrawArc(pen, 2, 2, s + 1, s + 1, 260, -80);
                }
                g.Restore(st);
            }
        }

        // ───────── 짠! 반짝임: 노란 별과 반짝선 ─────────
        private static void DrawSparkle(Graphics g, float t)
        {
            float a = .6f + .4f * (float)Math.Sin(t * 9);
            var gold = Color.FromArgb((int)(255 * (.78f + .22f * a)), 248, 190, 30);
            using (var pen = new Pen(gold, 3.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawArc(pen, 84, -112, 34, 16, 195, 110); g.DrawArc(pen, 88, -94, 34, 16, 195, 110); }
            using (var br = new SolidBrush(gold))
            {
                DrawStar(g, br, 130 + (float)Math.Sin(t * 5) * 1.5f, -108, 8f * (.8f + .3f * a), t * 20);
                DrawStar(g, br, -118, -96 + (float)Math.Sin(t * 4) * 2, 6f * (.8f + .3f * a), -t * 16);
            }
        }

        private static void DrawStar(Graphics g, Brush br, float cx, float cy, float r, float rotDeg)
        {
            var pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double ang = (rotDeg - 90 + i * 36) * Math.PI / 180; float rr = i % 2 == 0 ? r : r * .45f;
                pts[i] = new PointF(cx + (float)Math.Cos(ang) * rr, cy + (float)Math.Sin(ang) * rr);
            }
            g.FillPolygon(br, pts);
        }

        // ───────── 팔: 두 마디의 통통한 파란 팔 + 둥근 주먹(엄지척할 때 엄지가 솟는다) ─────────
        private struct Arm { public PointF S, E, W; public float Width; }

        private static bool ArmInFront(Ctx c, int side)
        {
            Pose p = c.P;
            if (side == 0 ? p.WingLTo.HasValue : p.WingRTo.HasValue) return true;
            if (p.WingBack) return false;
            bool near = (side == 1) == (c.Bd >= 0);
            return near && Math.Abs(c.BSin) > .3f;
        }

        private static Arm ArmGeom(Ctx c, int side)
        {
            Pose p = c.P; bool right = side == 1; int o = right ? 1 : -1;
            PointF S = c.Map(Axis + o * ShoulderHalf, ShoulderY);
            float A = right ? p.WingRA : p.WingLA, B = right ? p.WingRB : p.WingLB;
            PointF? to = right ? p.WingRTo : p.WingLTo;
            if (to.HasValue) IK(S, c.Map(to.Value), ArmL1, ArmL2, o, out A, out B);
            PointF E = new PointF(S.X + o * (float)Math.Sin(A * Math.PI / 180) * ArmL1, S.Y + (float)Math.Cos(A * Math.PI / 180) * ArmL1);
            PointF d = new PointF(o * (float)Math.Sin(B * Math.PI / 180), (float)Math.Cos(B * Math.PI / 180));
            PointF W = new PointF(E.X + d.X * ArmL2, E.Y + d.Y * ArmL2);
            float ax = c.Map(Axis, ShoulderY).X;
            float bc = c.BCos, back = (right ? p.WingRSweep : p.WingLSweep) * (ArmL1 + ArmL2) * c.BSin;
            // 몸의 yaw 로 옆으로 퍼진 거리는 줄고(cos), 앞뒤로 뻗은 거리는 옆모습에서 화면 가로로 드러난다(sin)
            Func<PointF, float, PointF> sq = (q, t) => new PointF(ax + (q.X - ax) * bc + back * t, q.Y);
            return new Arm { S = sq(S, 0f), E = sq(E, .5f), W = sq(W, 1f), Width = 1f };
        }

        private static void DrawArm(Graphics g, Ctx c, int side)
        {
            Arm a = ArmGeom(c, side);
            PointF[] band = Geo.Limb(new[] { a.S, a.E, a.W }, new[] { 35f, 32f, 29f }, true, 6);
            FillBlob(g, band, new PointF((a.S.X + a.E.X) / 2, Math.Min(a.S.Y, a.E.Y)), cBlueLo, cBlue, cBlueHi);
            Edge(g, band, c, .9f);
            // 주먹(손): 조금 더 둥글고 크다
            float hr = 17f;
            PointF[] fist = Ellipse(a.W.X, a.W.Y, hr, hr * .95f, 28);
            FillBlob(g, fist, new PointF(a.W.X - 4, a.W.Y - 5), cBlueLo, cBlue, cBlueHi);
            Edge(g, fist, c, .9f);
            double ang = Math.Atan2(a.W.Y - a.E.Y, a.W.X - a.E.X);
            // 손끝의 짧은 주름 두 줄
            using (var crease = new Pen(A2(cBlueDark, 110), c.Edge) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = -1; i <= 1; i += 2)
                {
                    double pa = ang + i * .42;
                    g.DrawLine(crease, a.W.X + (float)Math.Cos(pa) * hr * .45f, a.W.Y + (float)Math.Sin(pa) * hr * .45f, a.W.X + (float)Math.Cos(pa) * hr * .92f, a.W.Y + (float)Math.Sin(pa) * hr * .92f);
                }
            // 엄지척: 주먹 위로 엄지가 솟는다
            float th = Clamp(c.P.Thumb, 0, 1);
            if (th > .02f)
            {
                float tl = 6f + 15f * th;
                PointF[] thumb = Ellipse(a.W.X - (side == 0 ? -1 : 1) * 2f, a.W.Y - hr * .7f - tl * .45f, 6.6f, tl * .62f + 2f, 20);
                FillBlob(g, thumb, new PointF(thumb[0].X - 2, thumb[0].Y), cBlueLo, cBlue, cBlueHi);
                Edge(g, thumb, c, .9f);
            }
        }

        private static Color A2(Color col, int a) { return Color.FromArgb(a, col.R, col.G, col.B); }

        private static void IK(PointF S, PointF T, float L1, float L2, int outSign, out float A, out float B)
        {
            float dx = T.X - S.X, dy = T.Y - S.Y, rawDist = (float)Math.Sqrt(dx * dx + dy * dy);
            float dist = Math.Max(Math.Abs(L1 - L2) + .5f, Math.Min(L1 + L2 - .5f, rawDist));
            double th = Math.Atan2(dy, dx), alpha = Math.Acos(Math.Max(-1, Math.Min(1, (L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist))));
            PointF best = PointF.Empty; double bestScore = double.MinValue;
            for (int sg = -1; sg <= 1; sg += 2)
            {
                double a = th + sg * alpha;
                PointF e = new PointF(S.X + (float)Math.Cos(a) * L1, S.Y + (float)Math.Sin(a) * L1);
                double score = (e.X - S.X) * outSign + (e.Y - S.Y) * .6;   // 바깥·아래로 꺾이는 쪽을 고른다
                if (score > bestScore) { bestScore = score; best = e; }
            }
            float k = rawDist < 1e-3f ? 0 : dist / rawDist;
            PointF wr = new PointF(S.X + dx * k, S.Y + dy * k);
            PointF v1 = new PointF(best.X - S.X, best.Y - S.Y), v2 = new PointF(wr.X - best.X, wr.Y - best.Y);
            A = (float)(Math.Atan2(outSign * v1.X, v1.Y) * 180 / Math.PI);
            B = (float)(Math.Atan2(outSign * v2.X, v2.Y) * 180 / Math.PI);
        }

        // ───────── 카메라(스티커의 액션캠 + 셀카봉) ─────────
        private static void DrawCamera(Graphics g, Ctx c, PointF grip)
        {
            double a = c.P.ToolAng * Math.PI / 180;
            PointF up = new PointF((float)Math.Sin(a), -(float)Math.Cos(a)), side = new PointF((float)Math.Cos(a), (float)Math.Sin(a));
            PointF end = new PointF(grip.X + up.X * 34, grip.Y + up.Y * 34);
            using (var stick = new Pen(Color.FromArgb(28, 28, 34), 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })   //ref
                g.DrawLine(stick, new PointF(grip.X - up.X * 6, grip.Y - up.Y * 6), end);
            var st = g.Save();
            g.TranslateTransform(end.X, end.Y); g.RotateTransform(c.P.ToolAng);
            using (var body = Rig.RoundRect(new RectangleF(-14, -26, 28, 22), 5))
            {
                using (var br = new SolidBrush(Color.FromArgb(30, 30, 36))) g.FillPath(br, body);   //ref
                using (var rim = new Pen(Color.FromArgb(90, 92, 104), 1.6f)) g.DrawPath(rim, body);   //ref
            }
            using (var lens = new SolidBrush(Color.FromArgb(18, 24, 44))) g.FillEllipse(lens, -8, -23, 16, 16);   //ref
            using (var ring = new Pen(Color.FromArgb(110, 150, 220), 1.8f)) g.DrawEllipse(ring, -8, -23, 16, 16);   //ref
            using (var hi = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(hi, -5, -21, 4, 4);   //ref
            if (c.P.Flash >= 0)
            {
                float t = c.P.Flash, f = (float)Math.Max(0, Math.Sin(t * Math.PI * 2.4));
                using (var br = new SolidBrush(Color.FromArgb((int)(255 * (.35 + .65 * f)), 255, 252, 214)))
                    DrawStar(g, br, 0, -15, 10 + 24 * f, t * 40);
            }
            g.Restore(st);
        }

        // ───────── 깃발: 파도 무늬의 부산 파랑 깃발 ─────────
        private static void DrawFlag(Graphics g, Ctx c, PointF grip)
        {
            Pose p = c.P;
            double a = p.ToolAng * Math.PI / 180;
            PointF up = new PointF((float)Math.Sin(a), -(float)Math.Cos(a)), side = new PointF((float)Math.Cos(a), (float)Math.Sin(a));
            PointF end = new PointF(grip.X + up.X * 104, grip.Y + up.Y * 104), back = new PointF(grip.X - up.X * 14, grip.Y - up.Y * 14);
            using (var edge = new Pen(Color.FromArgb(40, 40, 48), 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })   //ref
            using (var pole = new Pen(Color.FromArgb(214, 216, 224), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })   //ref
            { g.DrawLine(edge, back, end); g.DrawLine(pole, back, end); }
            const int n = 14; const float fw = 56f, fh = 46f;
            var topPts = new PointF[n + 1]; var botPts = new PointF[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, wav = (float)Math.Sin(p.FlagWave - u * 5.5) * 5.5f * u;
                PointF t = new PointF(end.X - up.X * 3 + side.X * u * fw + up.X * wav, end.Y - up.Y * 3 + side.Y * u * fw + up.Y * wav);
                topPts[i] = t; botPts[i] = new PointF(t.X - up.X * fh, t.Y - up.Y * fh);
            }
            for (int i = 0; i < n; i++)
            {
                float shade = .86f + .14f * (float)Math.Sin(p.FlagWave - (i + .5f) / n * 5.5f);
                var col = Color.FromArgb((int)(30 * shade), (int)(118 * shade), (int)(222 * shade));
                using (var br = new SolidBrush(col))
                    g.FillPolygon(br, new[] { topPts[i], topPts[i + 1], botPts[i + 1], botPts[i] });
            }
            // 흰 파도 두 줄
            using (var wave = new Pen(Color.FromArgb(240, 255, 255, 255), 3.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })   //ref
                for (int r = 0; r < 2; r++)
                {
                    var line = new PointF[n + 1];
                    for (int i = 0; i <= n; i++)
                    {
                        float u = i / (float)n, f = .36f + r * .3f + .06f * (float)Math.Sin(u * 9 + r * 2 - p.FlagWave * .5f);
                        line[i] = new PointF(topPts[i].X + (botPts[i].X - topPts[i].X) * f, topPts[i].Y + (botPts[i].Y - topPts[i].Y) * f);
                    }
                    g.DrawLines(wave, line);
                }
            using (var rim = new Pen(Color.FromArgb(20, 70, 160), 2f) { LineJoin = LineJoin.Round })   //ref
            { g.DrawLines(rim, topPts); g.DrawLines(rim, botPts); g.DrawLine(rim, topPts[0], botPts[0]); g.DrawLine(rim, topPts[n], botPts[n]); }
        }

        // ───────── 간식 (새우깡 봉지에서 꺼내 먹기) ─────────
        private static void DrawCookie(Graphics g, Ctx c)
        {
            using (var rim = new Pen(Color.FromArgb(40, 40, 48), 2.4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })   //ref
                Rig.DrawSaeukkangSnack(g, c.P.Cookie, c.Map(182, 194), c.Map(200, 120), rim);
        }

    }
}
