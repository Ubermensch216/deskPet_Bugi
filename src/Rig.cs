using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Bugi
{
    internal enum EyeStyle { Dot, Half, Closed, Happy, Squint, Wide, Spiral }
    internal enum ToolKind { None, Pickaxe, Flag }

    /// <summary>
    /// 부기의 한 순간 자세. 좌표는 Rig 의 기준 치수 단위이고 각도는 화면 기준 시계 방향이 +,
    /// 그림은 "왼쪽을 보는" 모습이 기준이다(오른쪽은 좌우 반전해서 그린다).
    /// </summary>
    internal sealed class Pose
    {
        // 몸(머리와 몸통은 하나로 이어진 서양배 모양 덩어리다)
        public float BodyX, BodyY, Lean;       // 몸 이동·기울임(발 쪽 엉덩이 축 기준)
        public float Squash;                   // + 납작하게 웅크림(위아래로 눌리고 옆으로 퍼짐), - 길게 늘어남
        public float Breath;                   // 숨쉬기 (-1..1)
        public bool Sit;                       // 앉은 자세(신발 바닥이 앞으로 보임)
        public float Turn;                     // 몸 전체의 방향: 0 정면, -1 왼쪽 옆모습(-0.5 쯤이 3/4), +1 오른쪽 옆모습
        public float Look;                     // 몸은 그대로 두고 얼굴만 도는 정도(Turn 에 더해짐)
        public float HeadX, HeadY, HeadTilt;   // 목 위쪽만 따로 움직임
        // 머리카락·안경
        public float Tuft, TuftWave, TuftPhase;
        public float GlassDrop;                // 0 머리 위, 1 눈 위로 내려씀
        public float GlassTilt, GlassGlow;     // 기울기, 스마트 렌즈 빛(0..1)
        // 눈
        public EyeStyle Eyes;
        public float OpenL = 1, OpenR = 1;
        public float Gx, Gy;                   // 눈의 미세한 위치(-1..1)
        public float PupilR = 1, EyeScale = 1;
        public float Brow;                     // + 화남(안쪽이 내려감), - 슬픔
        public float SpiralT;
        // 부리
        public float Beak;                     // 0..1 벌어짐
        public float BeakShake;                // 떨림(좌우 흔들림)
        public float Blush;                    // 볼 붉어짐 0..1
        // 날개(두 마디): 위쪽 각도 A, 아래쪽 각도 B — 아래를 0으로, 바깥쪽이 +
        public float WingLA = 46, WingLB = 42, WingRA = 46, WingRB = 42;
        public PointF? WingLTo, WingRTo;       // 날개 끝이 갈 곳(기준 좌표). 있으면 각도 대신 사용하고 몸 앞에 그림
        public bool WingBack;                  // 날개를 몸 뒤에 그림
        public float WingLSweep, WingRSweep;   // 날개가 앞(-)·뒤(+)로 뻗는 정도(-1..1). 옆모습일 때 팔 흔들기·뒤로 젖히기로 보인다
        // 다리·신발
        public float FootLX, FootLY, FootRX, FootRY, FootLRot, FootRRot;
        // 꼬리
        public float Tail, TailWag;
        // 장식
        public float Tears = -1, Sweat = -1, Anger, Cookie = -1, Puff = -1, Holo = -1;
        public ToolKind Tool; public float ToolAng;
        public float Debris = -1, DebrisX = 246, DebrisY = 294;
        public float Ghost, GhostAng;          // 꾸벅꾸벅 졸 때 머리가 남기는 잔상
        // 근무복(공식 3D 근무복: 흰 셔츠 깃 + 하늘색 조끼 + 목걸이 명찰). 몸 윤곽 안쪽에 겹쳐 그리므로 규격 비율은 그대로다.
        public bool Outfit;
        public float CardSwing;                // 명찰이 흔들리는 각도
        public float CardShow;                 // 0 가슴에 걸림, 1 번쩍 들어 보여줌
        public float Sparkle = -1;             // >=0 이면 짠! 반짝임(위상)
        public float FlagWave;                 // 깃발 펄럭임 위상

        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        /// <summary>그림이 달라지는 값만 모아 낮은 해상도로 해시 — 같은 모습이면 다시 그리지 않는다.</summary>
        public long Hash()
        {
            unchecked
            {
                long h = 1469598103934665603L;
                Action<float, float> add = (v, q) => { h = (h ^ (long)Math.Round(v * q)) * 1099511628211L; };
                add(BodyX, 2); add(BodyY, 2); add(Lean, 4); add(Squash, 40); add(Breath, 20); add(Sit ? 1 : 0, 1); add(Turn, 24); add(Look, 24);
                add(HeadX, 2); add(HeadY, 2); add(HeadTilt, 4);
                add(Tuft, 3); add(TuftWave, 3); add(TuftPhase, 6); add(GlassDrop, 12); add(GlassTilt, 3); add(GlassGlow, 8);
                add((int)Eyes, 1); add(OpenL, 12); add(OpenR, 12); add(Gx, 10); add(Gy, 10); add(PupilR, 10); add(EyeScale, 20);
                add(Brow, 10); add(SpiralT, 5); add(Beak, 14); add(BeakShake, 6); add(Blush, 8);
                add(WingLA, 3); add(WingLB, 3); add(WingRA, 3); add(WingRB, 3);
                add(WingLTo.HasValue ? WingLTo.Value.X : -999, 2); add(WingLTo.HasValue ? WingLTo.Value.Y : -999, 2);
                add(WingRTo.HasValue ? WingRTo.Value.X : -999, 2); add(WingRTo.HasValue ? WingRTo.Value.Y : -999, 2);
                add(WingBack ? 1 : 0, 1); add(WingLSweep, 10); add(WingRSweep, 10);
                add(FootLX, 2); add(FootLY, 2); add(FootRX, 2); add(FootRY, 2); add(FootLRot, 3); add(FootRRot, 3);
                add(Tail, 3); add(TailWag, 3);
                add(Tears, 8); add(Sweat, 8); add(Anger, 8); add(Cookie, 20); add(Puff, 14); add(Holo, 8);
                add((int)Tool, 1); add(ToolAng, 3); add(Debris, 14); add(DebrisX, 1); add(DebrisY, 1);
                add(Ghost, 10); add(GhostAng, 3); add(Outfit ? 1 : 0, 1); add(CardSwing, 3); add(CardShow, 14); add(Sparkle, 8); add(FlagWave, 6);
                return h;
            }
        }
    }

    /// <summary>다각형 계산 보조.</summary>
    internal static class Geo
    {
        private static PointF CR(PointF p0, PointF p1, PointF p2, PointF p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return new PointF(
                .5f * ((2 * p1.X) + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3),
                .5f * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3));
        }

        /// <summary>제어점을 지나는 부드러운 곡선(Catmull-Rom)을 촘촘한 꺾은선으로 바꾼다.</summary>
        public static PointF[] Smooth(PointF[] c, bool closed, int sub = 7)
        {
            int n = c.Length; var res = new List<PointF>();
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                PointF p0 = c[closed ? (i - 1 + n) % n : Math.Max(i - 1, 0)], p1 = c[i];
                PointF p2 = c[closed ? (i + 1) % n : i + 1], p3 = c[closed ? (i + 2) % n : Math.Min(i + 2, n - 1)];
                for (int s = 0; s < sub; s++) res.Add(CR(p0, p1, p2, p3, s / (float)sub));
            }
            if (!closed) res.Add(c[n - 1]);
            return res.ToArray();
        }

        /// <summary>뼈대(spine)를 따라 굵기(w)를 준 팔다리 윤곽. 끝은 둥글게 닫을 수 있다.</summary>
        public static PointF[] Limb(PointF[] spine, float[] w, bool roundEnd, int sub = 6)
        {
            PointF[] s = Smooth(spine, false, sub);
            int m = s.Length;
            var left = new PointF[m]; var right = new PointF[m];
            for (int i = 0; i < m; i++)
            {
                PointF a = s[Math.Max(i - 1, 0)], b = s[Math.Min(i + 1, m - 1)];
                float tx = b.X - a.X, ty = b.Y - a.Y, len = (float)Math.Sqrt(tx * tx + ty * ty);
                if (len < 1e-4f) { tx = 1; ty = 0; len = 1; }
                tx /= len; ty /= len;
                float u = i / (float)(m - 1) * (w.Length - 1); int i0 = Math.Min((int)u, w.Length - 2); float f = u - i0;
                float wd = (w.Length == 1 ? w[0] : w[i0] + (w[i0 + 1] - w[i0]) * f) / 2;
                left[i] = new PointF(s[i].X - ty * wd, s[i].Y + tx * wd);
                right[i] = new PointF(s[i].X + ty * wd, s[i].Y - tx * wd);
            }
            var res = new List<PointF>(left);
            if (roundEnd)
            {
                PointF a = s[m - 2], b = s[m - 1];
                double ang = Math.Atan2(b.Y - a.Y, b.X - a.X); float wd = w[w.Length - 1] / 2;
                for (int i = 1; i < 8; i++)
                {
                    double q = ang + Math.PI / 2 - Math.PI * i / 8.0;
                    res.Add(new PointF(b.X + (float)Math.Cos(q) * wd, b.Y + (float)Math.Sin(q) * wd));
                }
            }
            for (int i = m - 1; i >= 0; i--) res.Add(right[i]);
            return res.ToArray();
        }

        /// <summary>(a, b)를 반지름으로 하는 초타원(지수 n; 2 = 타원)의 둘레 점들.</summary>
        public static PointF[] SuperEllipse(float cx, float cy, float a, float b, float n, int count = 40)
        {
            var r = new PointF[count];
            for (int i = 0; i < count; i++)
            {
                double t = i * Math.PI * 2 / count, cs = Math.Cos(t), sn = Math.Sin(t);
                r[i] = new PointF(cx + a * (float)(Math.Sign(cs) * Math.Pow(Math.Abs(cs), 2.0 / n)), cy + b * (float)(Math.Sign(sn) * Math.Pow(Math.Abs(sn), 2.0 / n)));
            }
            return r;
        }
    }

    /// <summary>
    /// 부기를 부위별로 그린다. 치수는 공식 규격 시트(ref/bugi-spec.png)를 따른다.
    /// 기준 단위 D = 검은 눈의 지름(12). 글래스 선(G)에서 아래로:
    ///   눈 윗부분까지 4D · 눈 지름 1D · 눈 사이 2D · 부리 높이 약 2.3D(너비 약 3.6D, 가운데 굵은 가로줄) · 머리카락 높이 약 2D ·
    ///   안경 전체 너비 5.5D · 몸은 머리가 넓은 원통에서 아래로 퍼지는 서양배 모양(키 약 24.7D, 발 포함).
    /// 좌표 원점: 발바닥 y=300, 몸 가운데 x=200. 몸·머리·날개·꼬리·신발은 각각 회전(yaw)을 따로 받아
    /// 도면의 정면·3/4·옆모습이 그대로 나온다.
    /// </summary>
    internal static class Rig
    {
        internal const float RefH = 298f, RefFootX = 200f, RefFootY = 300f;
        internal const float Axis = 200f;
        private const float D = 12f, G = 23f;                // 눈 지름, 글래스 선(머리 꼭대기)의 y
        private const float HipY = 258f, LeanPivotY = 262f;
        internal static readonly PointF HeadLocalOrigin = new PointF(200, 88);
        private const float HeadR = 48f;                     // 눈 높이의 머리 반지름(원통 근사)

        private static readonly Color cInk = Color.FromArgb(30, 28, 32), cBody = Color.FromArgb(255, 255, 255);
        private static readonly Color cBeak = Color.FromArgb(255, 214, 10), cRed = Color.FromArgb(232, 20, 30);
        private static readonly Color cLeg = Color.FromArgb(255, 200, 20), cPink = Color.FromArgb(240, 124, 146), cMouth = Color.FromArgb(128, 40, 46);
        private static readonly SolidBrush bBody = new SolidBrush(cBody), bInk = new SolidBrush(cInk), bBeak = new SolidBrush(cBeak);
        private static readonly SolidBrush bRed = new SolidBrush(cRed), bLeg = new SolidBrush(cLeg);
        private static readonly SolidBrush bPink = new SolidBrush(cPink), bMouth = new SolidBrush(cMouth), bWhite = new SolidBrush(Color.White);

        // 서양배 몸통: {반너비(D), 글래스 선에서 내려온 깊이(D)} — 도면 정면 윤곽을 잰 값
        private static readonly float[][] bodyHalf =
        {
            new[] { 1.3f, -.4f }, new[] { 2.2f, -.1f }, new[] { 2.75f, .5f }, new[] { 3.2f, 1.1f }, new[] { 3.6f, 1.9f }, new[] { 3.85f, 3.0f },
            new[] { 4.0f, 4.4f }, new[] { 4.05f, 5.8f }, new[] { 4.25f, 7.6f }, new[] { 4.6f, 9.4f }, new[] { 5.0f, 11.0f }, new[] { 5.3f, 12.5f },
            new[] { 5.4f, 14.4f }, new[] { 5.2f, 16.5f }, new[] { 4.6f, 18.1f }, new[] { 3.3f, 19.2f }, new[] { 1.6f, 19.65f }
        };
        private const float WingL1 = 27.6f, WingL2 = 26.4f;   // 날개 길이 약 4.5D
        private const float ShoulderY = 173f, ShoulderHalf = 63f;     // 날개 뿌리는 몸 윤곽선 위(어깨 높이 12.5D)
        private static float Yd(float dd) { return G + D * dd; }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }
        private static float SmoothStep(float x) { x = Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

        /// <summary>한 번 그리는 동안 쓰는 변형 정보: 기준 좌표 → 자세가 반영된 좌표.</summary>
        private sealed class Ctx
        {
            public Pose P; public bool Mirror; public Pen Ink; public float K;
            public float ExtraTilt;                // 잔상용: 머리 기울임 덧셈
            public PointF HeadC; public float HeadAng, HeadSx, HeadSy;
            public float Bd, Hd;                   // 몸·얼굴의 yaw(라디안, 왼쪽으로 돌면 +)
            public float BCos, BSin, HCos, HSin;

            public Ctx(Pose p, bool mirror, Pen ink, float k, float extraTilt)
            {
                P = p; Mirror = mirror; Ink = ink; K = k; ExtraTilt = extraTilt;
                Bd = Clamp(-p.Turn, -1.2f, 1.2f) * (float)(Math.PI / 2);
                Hd = Clamp(-(p.Turn + p.Look), -1.25f, 1.25f) * (float)(Math.PI / 2);
                BCos = (float)Math.Cos(Bd); BSin = (float)Math.Sin(Bd); HCos = (float)Math.Cos(Hd); HSin = (float)Math.Sin(Hd);
                HeadC = Map(HeadLocalOrigin.X, HeadLocalOrigin.Y);
                float sq = Sq;
                HeadAng = Clamp(p.HeadTilt, -12f, 12f) + extraTilt + p.Lean; HeadSx = 1 + sq * .5f; HeadSy = 1 - sq;
            }

            /// <summary>규격 가드: 몸이 눌리거나 늘어나는 정도는 최대 5%까지만 허용한다.</summary>
            private float Sq { get { return Clamp(P.Squash - P.Breath * .012f, -.05f, .05f); } }

            public PointF Map(float x, float y)
            {
                float sq = Sq;
                y = HipY + (y - HipY) * (1 - sq); x = Axis + (x - Axis) * (1 + sq * .5f);
                float w = SmoothStep((150 - y) / 80f);
                if (w > 0)
                {
                    double a = (Clamp(P.HeadTilt, -12f, 12f) + ExtraTilt) * w * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
                    float dx = x - Axis, dy = y - 150;
                    x = Axis + (float)(dx * ca - dy * sa) + Clamp(P.HeadX, -6f, 6f) * w; y = 150 + (float)(dx * sa + dy * ca) + Clamp(P.HeadY, -6f, 8f) * w;
                }
                double l = P.Lean * Math.PI / 180, cl = Math.Cos(l), sl = Math.Sin(l);
                float lx = x - Axis, ly = y - LeanPivotY;
                x = Axis + (float)(lx * cl - ly * sl) + P.BodyX; y = LeanPivotY + (float)(lx * sl + ly * cl) + P.BodyY;
                return new PointF(x, y);
            }
            public PointF Map(PointF q) { return Map(q.X, q.Y); }
        }

        // ───────── 그리기 ─────────
        /// <summary>g 의 원점이 발 가운데(픽셀)라고 가정하고 그린다. k = 기준 단위 1이 화면 몇 픽셀인지.</summary>
        internal static void Draw(Graphics g, Pose p, float k, bool faceRight)
        {
            var saved = g.Save();
            g.ScaleTransform(faceRight ? -k : k, k);
            g.TranslateTransform(-RefFootX, -RefFootY);
            float ow = Math.Max(2.8f, 1.15f / k);
            using (var ink = new Pen(cInk, ow) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                var c = new Ctx(p, faceRight, ink, k, 0);
                if (p.Ghost > .02f) DrawGhosts(g, p, faceRight, ink, k);
                DrawTail(g, c);
                for (int s = 0; s < 2; s++) if (!WingInFront(c, s)) DrawWing(g, c, s);
                if (!p.Sit) DrawLegs(g, c);
                DrawBody(g, c);
                if (p.Outfit) DrawOutfit(g, c);
                if (p.Sit) DrawSitFeet(g, c);
                DrawFace(g, c);
                if (p.Outfit && p.CardShow >= .02f) DrawHeldCard(g, c);
                for (int s = 0; s < 2; s++) if (WingInFront(c, s)) DrawWing(g, c, s);
                if (p.Tool == ToolKind.Pickaxe) { Wing w = WingGeom(c, 1); DrawPickaxe(g, c, w.W); }
                if (p.Tool == ToolKind.Flag) { Wing w = WingGeom(c, 1); DrawFlag(g, c, w.W); }
                if (p.Cookie >= 0) DrawCookie(g, c);
                if (p.Debris >= 0) DrawDebris(g, p);
            }
            g.Restore(saved);
        }

        // ───────── 몸통 ─────────
        private static PointF[] BodyOutline(Ctx c)
        {
            var pts = new List<PointF>();
            pts.Add(c.Map(Axis, Yd(-.5f)));
            foreach (float[] q in bodyHalf) pts.Add(c.Map(Axis - q[0] * D, Yd(q[1])));
            pts.Add(c.Map(Axis, Yd(19.75f)));
            for (int i = bodyHalf.Length - 1; i >= 0; i--) pts.Add(c.Map(Axis + bodyHalf[i][0] * D, Yd(bodyHalf[i][1])));
            return Geo.Smooth(pts.ToArray(), true, 6);
        }

        private static void DrawBody(Graphics g, Ctx c)
        {
            PointF[] body = BodyOutline(c);
            g.FillPolygon(bBody, body);
            g.DrawPolygon(c.Ink, body);
        }

        // 졸 때 고개가 흔들리며 남기는 희미한 잔상(참조 그림 2 오른쪽)
        private static void DrawGhosts(Graphics g, Pose p, bool mirror, Pen ink, float k)
        {
            float[] mult = { -1f, -.55f, .55f, 1f };
            foreach (float m in mult)
            {
                var c = new Ctx(p, mirror, ink, k, p.GhostAng * m);
                PointF[] body = BodyOutline(c);
                int a = (int)(95 * Math.Min(1f, p.Ghost));
                using (var fill = new SolidBrush(Color.FromArgb(a / 2, 235, 235, 238)))
                using (var edge = new Pen(Color.FromArgb(a, 150, 150, 160), ink.Width * .7f) { LineJoin = LineJoin.Round })
                { g.FillPolygon(fill, body); g.DrawPolygon(edge, body); }
                var st = g.Save();
                g.TranslateTransform(c.HeadC.X, c.HeadC.Y); g.RotateTransform(c.HeadAng);
                using (var red = new Pen(Color.FromArgb(a, 232, 20, 30), ink.Width * .9f))
                { g.DrawEllipse(red, -24, -78, 20, 20); g.DrawEllipse(red, 4, -78, 20, 20); }
                using (var beak = new SolidBrush(Color.FromArgb(a, 255, 214, 10))) g.FillEllipse(beak, -22 + p.Look * 15, 0, 44, 25);
                g.Restore(st);
            }
        }

        // ───────── 꼬리: 몸 뒤쪽 아래의 검은 타원(뒷모습에서는 넓게, 옆모습에서는 뒤로 툭 튀어나옴) ─────────
        private static void DrawTail(Graphics g, Ctx c)
        {
            Pose p = c.P;
            const float a = 2.7f * D, b = 1.05f * D, depth = .8f * D, backZ = -(5.4f * D + .8f * D);
            PointF ctr = c.Map(Axis, Yd(13.6f));
            float cx = ctr.X - backZ * c.BSin, hw = (float)Math.Sqrt(a * a * c.BCos * c.BCos + depth * depth * c.BSin * c.BSin);
            var st = g.Save();
            g.TranslateTransform(cx, ctr.Y); g.RotateTransform(p.Tail + p.TailWag * .4f);
            PointF[] tail = Geo.SuperEllipse(0, 0, Math.Max(hw, 3f), b, 2.3f, 24);
            g.FillPolygon(bInk, tail); g.DrawPolygon(c.Ink, tail);
            g.Restore(st);
        }

        // ───────── 다리 · 신발 ─────────
        private static void DrawLegs(Graphics g, Ctx c)
        {
            Pose p = c.P;
            for (int side = 0; side < 2; side++)
            {
                int sign = side == 0 ? -1 : 1;
                float fx = side == 0 ? p.FootLX : p.FootRX, fy = side == 0 ? p.FootLY : p.FootRY, rot = side == 0 ? p.FootLRot : p.FootRRot;
                // 발의 방향: 몸의 방향에 바깥으로 벌어진 정도(약 28도)를 더한다
                float psi = c.Bd + (sign < 0 ? .49f : -.49f);
                float v = (float)Math.Sin(psi);
                float ankleX = Axis + sign * 16 * c.BCos + p.BodyX * .5f;
                float shoeX = ankleX + sign * 6 * c.BCos - 13.5f * v + fx;
                float shoeY = 300 + fy;
                PointF hip = c.Map(Axis + sign * 16 * c.BCos, 256);
                DrawLeg(g, c, hip, new PointF(ankleX + fx * .6f, shoeY - 34));
                DrawShoe(g, c, shoeX, shoeY, v, rot, sign);
            }
        }

        private static void DrawLeg(Graphics g, Ctx c, PointF hip, PointF ankle)
        {
            float w = 14f;
            using (var edge = new Pen(cInk, w + c.Ink.Width * 1.7f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
            using (var fill = new Pen(cLeg, w) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
            { g.DrawLine(edge, hip, ankle); g.DrawLine(fill, hip, ankle); }
        }

        // 신발(도면 3): 옆모습 윤곽(발끝이 -x)과 정면 윤곽을 같은 개수의 점으로 두고 보는 방향에 따라 섞는다.
        // 원점은 밑창 바닥 가운데, 위쪽이 -y. 밑창 높이 10, 윗부분은 약 26.
        private static readonly float[] sideX = { 28.7f, 28.3f, 26.8f, 23.9f, 19.9f, 16.5f, 7.9f, .7f, -8.2f, -17.2f, -24.7f, -28.4f, -28.7f };
        private static readonly float[] sideY = { -10f, -23.8f, -28f, -31.3f, -29.5f, -33.2f, -36.2f, -35.1f, -30.2f, -24.7f, -19.7f, -14.5f, -10f };
        private static readonly float[] frontX = { 28f, 28f, 26.5f, 24f, 20f, 15f, 8f, 0f, -8f, -15f, -22f, -27f, -28f };
        private static readonly float[] frontY = { -10f, -20f, -26f, -30f, -33f, -35.2f, -36.2f, -36.6f, -36.2f, -35.2f, -31f, -22f, -10f };

        /// <summary>v: -1..1, 양수면 발끝이 왼쪽(옆모습), 0 이면 정면, 음수면 발끝이 오른쪽.</summary>
        private static void DrawShoe(Graphics g, Ctx c, float x, float y, float v, float rot, int sign)
        {
            float t = Math.Abs(v), flip = v < 0 ? -1 : 1;
            var st = g.Save();
            g.TranslateTransform(x, y); g.ScaleTransform(flip, 1); g.RotateTransform(rot);
            var pts = new PointF[sideX.Length];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new PointF(Pose.Lerp(frontX[i], sideX[i], t), Pose.Lerp(frontY[i], sideY[i], t));
            float hw = Pose.Lerp(28f, 28.7f, t);
            PointF[] poly = Geo.Smooth(pts, false, 3);
            g.FillPolygon(bInk, poly);
            g.DrawLines(c.Ink, poly);
            // 흰 밑창(굵은 윤곽)
            using (var path = Rig.RoundRect(new RectangleF(-hw, -10, hw * 2, 10), 3f)) { g.FillPath(bBody, path); g.DrawPath(c.Ink, path); }
            // 신발끈: 흰 줄 세 개
            using (var lace = new Pen(Color.White, Math.Max(2.6f, c.Ink.Width * .8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float[][] sideLace = { new[] { .7f, -29.4f, -6f, -27.9f }, new[] { -3.7f, -25.7f, -10.5f, -23.8f }, new[] { -9f, -22.3f, -15.7f, -20.5f } };
                float[][] frontLace = { new[] { -5f, -31f, 5f, -31f }, new[] { -6f, -26.5f, 6f, -26.5f }, new[] { -7f, -22f, 7f, -22f } };
                for (int i = 0; i < 3; i++)
                    g.DrawLine(lace, Pose.Lerp(frontLace[i][0], sideLace[i][0], t), Pose.Lerp(frontLace[i][1], sideLace[i][1], t),
                        Pose.Lerp(frontLace[i][2], sideLace[i][2], t), Pose.Lerp(frontLace[i][3], sideLace[i][3], t));
            }
            // 동백꽃 무늬(바깥 옆면)
            DrawCamellia(g, Pose.Lerp(-sign * 9f * flip, 10.5f, t), Pose.Lerp(-14f, -19.7f, t), 5.4f);
            g.Restore(st);
        }

        private static void DrawCamellia(Graphics g, float x, float y, float r)
        {
            using (var leaf = new SolidBrush(Color.FromArgb(70, 170, 70)))
            { g.FillEllipse(leaf, x - r * 1.25f, y + r * .1f, r * .9f, r * .7f); g.FillEllipse(leaf, x + r * .35f, y + r * .55f, r * .9f, r * .65f); }
            for (int i = 0; i < 5; i++)
            {
                double a = i * Math.PI * 2 / 5 - Math.PI / 2;
                g.FillEllipse(bRed, x + (float)Math.Cos(a) * r * .62f - r * .55f, y + (float)Math.Sin(a) * r * .62f - r * .55f, r * 1.1f, r * 1.1f);
            }
            using (var center = new SolidBrush(Color.FromArgb(255, 218, 60))) g.FillEllipse(center, x - r * .3f, y - r * .3f, r * .6f, r * .6f);
        }

        /// <summary>앉았을 때: 두 다리를 앞으로 내밀어 신발 바닥(회색 무늬)이 보인다(참조 그림 2).</summary>
        private static void DrawSitFeet(Graphics g, Ctx c)
        {
            Pose p = c.P;
            for (int side = 0; side < 2; side++)
            {
                int sign = side == 0 ? -1 : 1;
                float fx = side == 0 ? p.FootLX : p.FootRX, fy = side == 0 ? p.FootLY : p.FootRY, rot = side == 0 ? p.FootLRot : p.FootRRot;
                PointF hip = c.Map(Axis + sign * 18, 262);
                PointF at = new PointF(Axis + sign * 42 + fx, 282 + fy);
                DrawLeg(g, c, hip, new PointF(at.X - sign * 6, at.Y - 12));
                var st = g.Save();
                g.TranslateTransform(at.X, at.Y); g.RotateTransform(sign * (14 + rot));
                using (var upper = new GraphicsPath()) { upper.AddEllipse(-20, -24, 40, 28); g.FillPath(bInk, upper); g.DrawPath(c.Ink, upper); }
                DrawCamellia(g, sign * 10, -15, 5f);
                var soleRect = new RectangleF(-20, -10, 40, 44);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(soleRect);
                    g.FillPath(bBody, path); g.DrawPath(c.Ink, path);
                    var rim = RectangleF.Inflate(soleRect, -5, -5);
                    using (var fill = new SolidBrush(Color.FromArgb(160, 162, 170))) g.FillEllipse(fill, rim);
                    g.DrawEllipse(c.Ink, rim);
                    using (var tread = new Pen(cInk, Math.Max(2f, c.Ink.Width * .6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        for (int i = 0; i < 3; i++) g.DrawLine(tread, -10, 3 + i * 9, 10, 3 + i * 9);
                }
                g.Restore(st);
            }
        }

        // ───────── 얼굴 ─────────
        private static void DrawFace(Graphics g, Ctx c)
        {
            Pose p = c.P;
            var st = g.Save();
            g.TranslateTransform(c.HeadC.X, c.HeadC.Y); g.RotateTransform(c.HeadAng); g.ScaleTransform(c.HeadSx, c.HeadSy);
            float cs = c.HCos, sn = c.HSin;

            // 볼(도면에는 없지만 공식 이모티콘에 나오는 발그레한 볼)
            if (p.Blush > .01f)
            {
                using (var blush = new SolidBrush(Color.FromArgb((int)(175 * Math.Min(1f, p.Blush)), 255, 128, 138)))
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = s * 36f, z = (float)Math.Sqrt(HeadR * HeadR - x * x), xs = x * cs - z * sn, f = Math.Min(1f, (x * sn + z * cs) / z);
                        if (f > .15f) g.FillEllipse(blush, xs - 9 * f, 1f, 18 * f, 11);
                    }
            }
            DrawEye(g, c, false); DrawEye(g, c, true);
            DrawBeak(g, c);
            if (p.Tears >= 0) DrawTears(g, c);
            if (p.Sweat >= 0) DrawSweat(g, p.Sweat);
            if (p.Anger > .01f) DrawAnger(g, p);
            if (p.Puff >= 0) DrawPuff(g, p.Puff, -(HeadR + 8) * sn);
            DrawHair(g, c);
            DrawGlasses(g, c);
            if (p.Sparkle >= 0) DrawSparkle(g, p.Sparkle);
            if (p.Holo >= 0) DrawHolo(g, p);
            g.Restore(st);
        }

        /// <summary>눈: 검은 점(지름 1D). 얼굴이 돌면 눈이 둥근 얼굴 표면을 따라 이동하고 좁아진다.</summary>
        private static void DrawEye(Graphics g, Ctx c, bool right)
        {
            Pose p = c.P;
            float x = (right ? 1 : -1) * 1.5f * D, z = (float)Math.Sqrt(HeadR * HeadR - x * x);
            float xs = x * c.HCos - z * c.HSin, depth = x * c.HSin + z * c.HCos, f = Math.Min(1f, depth / z);   // 정면에서는 지름 1D 의 완전한 원
            if (f < .14f) return;
            float open = right ? p.OpenR : p.OpenL, es = Clamp(p.EyeScale, .95f, 1.1f), inner = right ? -1 : 1;
            float cx = xs + p.Gx * 2f, cy = -11f + p.Gy * 1.6f;
            var st = g.Save();
            g.TranslateTransform(cx, cy); g.ScaleTransform(f, 1);
            float w = Math.Max(3.2f, c.Ink.Width * .9f);
            using (var line = new Pen(cInk, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                EyeStyle style = p.Eyes;
                if (style == EyeStyle.Spiral)
                {
                    float r = 8f;
                    g.FillEllipse(bWhite, -r, -r, r * 2, r * 2); g.DrawEllipse(c.Ink, -r, -r, r * 2, r * 2);
                    var pts = new List<PointF>();
                    using (var sp = new Pen(cInk, Math.Max(2.4f, c.Ink.Width * .6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
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
                        float r = D / 2 * es * (style == EyeStyle.Wide ? 1.08f : 1f), hh = r * Math.Max(.12f, open);
                        g.FillEllipse(bInk, -r, -hh, r * 2, hh * 2);
                        break;
                    }
                    case EyeStyle.Half:
                        g.DrawLine(line, -7, -3 - p.Brow * 2 * inner, 7, -3 + p.Brow * 2 * inner);
                        g.FillEllipse(bInk, -3f, 1f, 6f, 6f);
                        break;
                    case EyeStyle.Closed:
                        g.DrawArc(line, -7, -6, 14, 10, 25, 130);
                        break;
                    case EyeStyle.Happy:
                        g.DrawArc(line, -7, -3, 14, 11, 205, 130);
                        break;
                    case EyeStyle.Squint:
                        g.DrawLines(line, new[] { new PointF(-inner * 6, -6), new PointF(inner * 4, 0), new PointF(-inner * 6, 6) });
                        break;
                }
                if (Math.Abs(p.Brow) > .05f && (style == EyeStyle.Dot || style == EyeStyle.Wide || style == EyeStyle.Half))
                {
                    float by = style == EyeStyle.Wide ? -15 : -11;
                    using (var brow = new Pen(cInk, Math.Max(2.6f, c.Ink.Width * .75f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(brow, inner * 8, by + p.Brow * 5f, -inner * 8, by - p.Brow * 3f);
                }
            }
            g.Restore(st);
        }

        /// <summary>부리: 너비 약 3.6D · 높이 약 2.3D 의 노란 타원, 가운데에 굵은 가로줄. 열리면 아랫부리가 내려간다.</summary>
        private static void DrawBeak(Graphics g, Ctx c)
        {
            Pose p = c.P;
            const float a = 1.8f * D, b = 1.15f * D, depth = .75f * D, surfaceZ = 46f;
            float zc = surfaceZ + depth;
            float xs = -zc * c.HSin, halfW = (float)Math.Sqrt(a * a * c.HCos * c.HCos + depth * depth * c.HSin * c.HSin);
            float open = Clamp(p.Beak, 0, 1.2f), dOpen = open * 13f;
            var st = g.Save();
            g.TranslateTransform(xs + p.BeakShake, 12.4f); g.ScaleTransform(halfW / a, 1);
            PointF[] upper = HalfOval(a, b, -1), lower = HalfOval(a, b, 1);
            if (dOpen > .6f)
            {
                PointF[] inside = { new PointF(-a * .96f, 0), new PointF(a * .96f, 0), new PointF(a * .85f, dOpen), new PointF(-a * .85f, dOpen) };
                g.FillPolygon(bMouth, inside);
                var clip = g.Save();
                using (var path = new GraphicsPath()) { path.AddPolygon(inside); g.SetClip(path, CombineMode.Intersect); }
                float tr = Math.Min(9f, dOpen * .5f + 3);
                g.FillEllipse(bPink, -9, dOpen - tr * .9f, 18, tr * 1.6f);
                g.Restore(clip);
            }
            var lst = g.Save(); g.TranslateTransform(0, dOpen);
            g.FillPolygon(bBeak, lower); g.DrawPolygon(c.Ink, lower);
            g.Restore(lst);
            g.FillPolygon(bBeak, upper); g.DrawPolygon(c.Ink, upper);
            g.Restore(st);
        }

        /// <summary>초타원 부리의 위(-1) 또는 아래(+1) 절반. 가운데 줄(y=0)에 닿는 쪽은 곧은 선이고 윤곽선이 굵은 가로줄이 된다.</summary>
        private static PointF[] HalfOval(float a, float b, int half)
        {
            var pts = new List<PointF>();
            const int n = 22;
            for (int i = 0; i <= n; i++)
            {
                double t = Math.PI * i / n; double cs = Math.Cos(t), sn = Math.Sin(t);
                float x = a * (float)(Math.Sign(cs) * Math.Pow(Math.Abs(cs), 2.0 / 2.4));
                float y = b * (float)Math.Pow(Math.Abs(sn), 2.0 / 2.4) * half;
                pts.Add(new PointF(x, y));
            }
            return pts.ToArray();
        }

        // ───────── 머리카락: 검은 지그재그 선 세 봉우리 ─────────
        private static void DrawHair(Graphics g, Ctx c)
        {
            Pose p = c.P;
            float[][] pts =
            {
                new[] { -2.2f, .35f, 0f }, new[] { -1.9f, -1.2f, 1f }, new[] { -1.3f, .1f, 2f }, new[] { -.6f, -1.8f, 3f },
                new[] { .1f, 0f, 4f }, new[] { .8f, -1.55f, 5f }, new[] { 1.5f, .3f, 6f }
            };
            var list = new PointF[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                float h = Math.Max(0, -pts[i][1]);
                float sway = (p.Tuft + (float)Math.Sin(p.TuftPhase - pts[i][2] * .9) * p.TuftWave) * (float)(Math.PI / 180);
                float x = pts[i][0] * D + h * D * (float)Math.Sin(sway) * .9f;
                float y = -65f + pts[i][1] * D + (1 - (float)Math.Cos(sway)) * h * 4f;
                list[i] = new PointF(x * c.HCos - 18 * c.HSin, y);
            }
            using (var pen = new Pen(cInk, Math.Max(3.2f, c.Ink.Width * 1.05f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, list);
        }

        // ───────── 빨간 스마트 안경(도면 2): 총 너비 5.5D ─────────
        private static void DrawGlasses(Graphics g, Ctx c)
        {
            Pose p = c.P;
            float d = Clamp(p.GlassDrop, 0, 1);
            const float rOut = .85f * D, th = .3f * D, rc = rOut - th / 2, stub = .45f * 1.7f * D, hook = .25f * 1.7f * D, zg = 36f;
            float bridge = .57f * D, lensX = rOut + bridge / 2;     // 규격: 눈 위로 내려써도 안경 치수는 그대로
            float y = Pose.Lerp(-68f, -11f, d), yBar = -.09f * 1.7f * D;
            float cs = c.HCos, sn = c.HSin, lensScale = Math.Max(.05f, Math.Abs(cs));
            var st = g.Save();
            g.TranslateTransform(0, y); g.RotateTransform(p.GlassTilt);
            using (var ring = new Pen(cRed, th) { StartCap = LineCap.Flat, EndCap = LineCap.Flat, LineJoin = LineJoin.Miter })
            {
                float gl = Math.Min(1f, p.GlassGlow);
                for (int s = -1; s <= 1; s += 2)
                {
                    float cx = s * lensX * cs - zg * sn;
                    if (p.GlassGlow > .01f || d > .5f)
                        using (var glow = new SolidBrush(Color.FromArgb((int)(55 + 130 * gl), 140, 214, 255)))
                            g.FillEllipse(glow, cx - rc * lensScale, -rc, rc * 2 * lensScale, rc * 2);
                    if (rc * 2 * lensScale < th * 1.8f)
                        g.FillEllipse(bRed, cx - th / 2, -rc - th / 2, th, rc * 2 + th);      // 옆에서 보면 렌즈는 세로 눈금
                    else g.DrawEllipse(ring, cx - rc * lensScale, -rc, rc * 2 * lensScale, rc * 2);
                    if (p.GlassGlow > .05f)
                        using (var scan = new Pen(Color.FromArgb((int)(220 * gl), 255, 255, 255), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        {
                            float q = ((p.TuftPhase * .5f) % 1f + 1f) % 1f, yy = -rc + 2 + q * (rc * 2 - 4), hw = (float)Math.Sqrt(Math.Max(0, rc * rc - yy * yy)) - 2;
                            if (hw > 1) g.DrawLine(scan, cx - hw * lensScale, yy, cx + hw * lensScale, yy);
                        }
                }
                // 코다리
                g.DrawLine(ring, -zg * sn - (lensX - rc) * cs, 0, -zg * sn + (lensX - rc) * cs, 0);
                // 안경다리 앞부분(곧은 막대 + 끝의 갈고리)과, 뒤로 뻗은 긴 다리
                for (int s = -1; s <= 1; s += 2)
                {
                    float x0 = s * (lensX + rOut), x1 = s * (lensX + rOut + stub);
                    float sx0 = x0 * cs - zg * sn, sx1 = x1 * cs - zg * sn;
                    g.DrawLine(ring, sx0, yBar, sx1, yBar);
                    g.DrawLine(ring, sx1, yBar - th / 2, sx1, yBar + hook);
                    // 뒤로 뻗은 다리(몸이 돌았을 때 보인다): 가까운 쪽은 항상, 먼 쪽은 거의 정면일 때만
                    float zEnd = zg - 3.5f * D;
                    float depthMid = x1 * sn + (zg + zEnd) / 2 * cs;
                    if (depthMid > 0 || Math.Abs(sn) < .1f)
                    {
                        float ex = x1 * cs - zEnd * sn;
                        if (Math.Abs(ex - sx1) > 1f) g.DrawLine(ring, sx1, yBar, ex, yBar);
                    }
                }
                // 스마트 글래스 돌출부: 한쪽 렌즈 안쪽의 작은 막대(좌우로 뒤집혀도 오른쪽에 남는다)
                int ps = c.Mirror ? -1 : 1;
                float pcx = ps * lensX * cs - zg * sn;
                g.DrawLine(ring, pcx + ps * 1.2f * cs, yBar, pcx + ps * (rc - th * .2f) * lensScale, yBar);
            }
            g.Restore(st);
        }

        private static void DrawHolo(Graphics g, Pose p)
        {
            // 스마트 안경이 띄운 작은 화면: 줄이 차례로 채워진다
            float t = p.Holo;
            var panel = new RectangleF(58, -92, 62, 46);
            using (var path = RoundRect(panel, 6))
            using (var fill = new SolidBrush(Color.FromArgb(120, 150, 215, 255)))
            using (var edge = new Pen(Color.FromArgb(220, 90, 160, 230), 3f))
            { g.FillPath(fill, path); g.DrawPath(edge, path); }
            using (var bar = new Pen(Color.FromArgb(235, 255, 255, 255), 3.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < 4; i++)
                {
                    float len = 12 + (float)((Math.Sin(t * 3 + i * 1.7) * .5 + .5) * 30) * (i == 0 ? .6f : 1f);
                    g.DrawLine(bar, panel.Left + 8, panel.Top + 8 + i * 9.5f, panel.Left + 8 + len, panel.Top + 8 + i * 9.5f);
                }
            float blink = (float)(Math.Sin(t * 6) * .5 + .5);
            using (var dot = new SolidBrush(Color.FromArgb((int)(255 * blink), 255, 255, 255))) g.FillEllipse(dot, panel.Right - 10, panel.Top + 5, 5, 5);
        }

        private static void DrawPuff(Graphics g, float q, float shift)
        {
            // 후~ 내쉬는 한숨: 부리 옆에서 작은 구름 세 개가 퍼지며 사라진다(참조 그림 2)
            float a = Math.Max(0, 1 - q);
            using (var edge = new Pen(Color.FromArgb((int)(200 * a), 90, 90, 100), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < 3; i++)
                {
                    float x = -52 + shift * .3f - q * 22 - i * 7 * q, y = 14 - q * 8 + (i - 1) * 7 * q, r = 4 + i * 1.5f + q * 3;
                    g.DrawArc(edge, x - r, y - r, r * 2, r * 2, 60, 250);
                }
        }

        private static void DrawTears(Graphics g, Ctx c)
        {
            float t = c.P.Tears;
            for (int side = 0; side < 2; side++)
            {
                float x = (side == 0 ? -1 : 1) * 1.5f * D, z = (float)Math.Sqrt(HeadR * HeadR - x * x);
                float cx = x * c.HCos - z * c.HSin, depth = x * c.HSin + z * c.HCos;
                if (depth < .14f * z) continue;
                float dir = side == 0 ? -1 : 1;
                using (var streak = new Pen(Color.FromArgb(190, 150, 205, 255), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(streak, cx + dir * 1, -6, cx + dir * 4, 12 + 5 * (float)Math.Sin(t * 9 + side));
                for (int i = 0; i < 2; i++)
                {
                    float q = (t * 1.5f + i * .5f + side * .27f) % 1f;
                    float px = cx + dir * (3 + q * 14), py = 2 + q * 66, r = 5 - 2f * q;
                    int a = (int)(235 * (1 - q * q));
                    using (var drop = new SolidBrush(Color.FromArgb(a, 120, 190, 255)))
                    using (var rim = new Pen(Color.FromArgb(a, 60, 110, 180), 2.4f))
                    { g.FillEllipse(drop, px - r, py - r * 1.3f, r * 2, r * 2.6f); g.DrawEllipse(rim, px - r, py - r * 1.3f, r * 2, r * 2.6f); }
                }
            }
        }

        private static void DrawSweat(Graphics g, float t)
        {
            float q = t % 1.2f / 1.2f;
            float x = 58 + 5 * q, y = -52 + q * 40; int a = (int)(235 * (1 - q * q * q));
            using (var drop = new SolidBrush(Color.FromArgb(a, 170, 214, 255)))
            using (var rim = new Pen(Color.FromArgb(a, 70, 120, 190), 3f))
            {
                var pts = new[] { new PointF(x, y - 14), new PointF(x + 7, y + 1), new PointF(x, y + 8), new PointF(x - 7, y + 1) };
                var path = new GraphicsPath(); path.AddCurve(pts, 0f); path.CloseFigure();
                g.FillPath(drop, path); g.DrawPath(rim, path); path.Dispose();
            }
        }

        private static void DrawAnger(Graphics g, Pose p)
        {
            // 만화식 💢 : 머리 옆에서 두근거리며 커졌다 작아진다
            float[][] spots = { new[] { 62f, -58f }, new[] { -62f, -44f } };
            float pulse = 1 + .12f * (float)Math.Sin(p.Anger * 40);
            for (int i = 0; i < 2; i++)
            {
                float s = (i == 0 ? 11f : 9f) * pulse * Math.Min(1f, p.Anger * 1.2f);
                var st = g.Save();
                g.TranslateTransform(spots[i][0], spots[i][1]);
                using (var pen = new Pen(Color.FromArgb(226, 56, 44), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawArc(pen, -s - 3, -s - 3, s + 1, s + 1, 100, 80); g.DrawArc(pen, 2, -s - 3, s + 1, s + 1, 280, 80);
                    g.DrawArc(pen, -s - 3, 2, s + 1, s + 1, 80, -80); g.DrawArc(pen, 2, 2, s + 1, s + 1, 260, -80);
                }
                g.Restore(st);
            }
        }

        // ───────── 근무복(공식 3D 근무복 그림): 흰 셔츠 깃 + 하늘색 조끼 + 파란 목걸이 명찰(로고) ─────────
        // 모두 몸 윤곽 안쪽에 겹쳐 그리므로(몸 윤곽으로 자름) 부기의 비율·형태는 그대로다.
        private static readonly Color cVest = Color.FromArgb(112, 162, 214), cStrap = Color.FromArgb(34, 70, 170);

        private static float BodyHalfAt(float dd)
        {
            for (int i = 0; i + 1 < bodyHalf.Length; i++)
                if (dd <= bodyHalf[i + 1][1])
                {
                    float t = (dd - bodyHalf[i][1]) / (bodyHalf[i + 1][1] - bodyHalf[i][1]);
                    return Pose.Lerp(bodyHalf[i][0], bodyHalf[i + 1][0], Clamp(t, 0, 1));
                }
            return bodyHalf[bodyHalf.Length - 1][0];
        }

        /// <summary>가슴 표면의 점(xd: 가운데에서 옆으로 D 단위, dd: 글래스 선에서 내려온 D 단위) → 화면 좌표. depth &gt; 0 이면 앞쪽이라 보인다.</summary>
        private static PointF Chest(Ctx c, float xd, float dd, out float depth)
        {
            float R = BodyHalfAt(dd) * D, x = xd * D, z = (float)Math.Sqrt(Math.Max(0, R * R - x * x));
            depth = (x * c.BSin + z * c.BCos) / R;
            return c.Map(Axis + x * c.BCos - z * c.BSin, Yd(dd));
        }

        private static PointF CardTop(Ctx c)
        {
            float dp; PointF rest = Chest(c, 0, 14.1f, out dp);
            float k = SmoothStep(c.P.CardShow);
            PointF shown = c.Map(206, 122);
            return new PointF(Pose.Lerp(rest.X, shown.X, k), Pose.Lerp(rest.Y, shown.Y, k));
        }

        private static void DrawOutfit(Graphics g, Ctx c)
        {
            Pose p = c.P;
            float d0, d1, d2;
            PointF vL = Chest(c, -3.0f, 11.0f, out d0), vB = Chest(c, 0, 13.5f, out d1), vR = Chest(c, 3.0f, 11.0f, out d2);
            bool front = d1 > .12f && d0 > -.2f && d2 > -.2f;
            // ① 하늘색 조끼(몸 윤곽 안쪽만): 어깨선에서 밑단까지, 앞쪽엔 V 목선
            var st = g.Save();
            using (var clip = new GraphicsPath()) { clip.AddPolygon(BodyOutline(c)); g.SetClip(clip, CombineMode.Intersect); }
            var poly = new List<PointF> { c.Map(Axis - 8 * D, Yd(11.0f)) };
            if (front) { poly.Add(vL); poly.Add(vB); poly.Add(vR); }
            poly.Add(c.Map(Axis + 8 * D, Yd(11.0f)));
            var hem = new List<PointF>();
            for (int i = 8; i >= -8; i--) { float x = i; hem.Add(c.Map(Axis + x * D, Yd(17.0f + .5f * (1 - x * x / 64f)))); }
            poly.AddRange(hem);
            using (var br = new SolidBrush(cVest)) g.FillPolygon(br, poly.ToArray());
            g.DrawLines(c.Ink, hem.ToArray());
            if (front) g.DrawLines(c.Ink, new[] { vL, vB, vR });
            g.Restore(st);
            // ② 흰 셔츠 깃
            if (front)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float da, db, dc, de;
                    PointF a = Chest(c, sgn * 3.3f, 10.0f, out da), b = Chest(c, sgn * .5f, 10.6f, out db), cc = Chest(c, sgn * .2f, 12.6f, out dc), e = Chest(c, sgn * 3.0f, 11.1f, out de);
                    var quad = new[] { a, b, cc, e };
                    g.FillPolygon(bBody, quad); g.DrawPolygon(c.Ink, quad);
                }
            // ③ 파란 목걸이 끈과 명찰
            PointF top = CardTop(c);
            if (front || p.CardShow > .02f)
                using (var strap = new Pen(cStrap, Math.Max(2.4f, c.Ink.Width * .85f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    { float ds; PointF s0 = Chest(c, sgn * 2.9f, 10.8f, out ds); g.DrawLine(strap, s0, top); }
            if (p.CardShow < .02f && c.BCos > .25f) DrawCard(g, c, top, 1f, c.BCos, p.CardSwing);
        }

        /// <summary>명찰: 하얀 카드에 첨부한 로고 이미지와 글줄. top 은 끈에 매달린 윗부분.</summary>
        private static void DrawCard(Graphics g, Ctx c, PointF top, float scale, float fx, float angle)
        {
            float w = 1.9f * D * scale, h = 2.7f * D * scale;
            var st = g.Save();
            g.TranslateTransform(top.X, top.Y); g.RotateTransform(angle); g.ScaleTransform(Math.Max(.2f, fx), 1);
            using (var clipBr = new SolidBrush(Color.FromArgb(140, 140, 150))) g.FillRectangle(clipBr, -w * .14f, -h * .06f, w * .28f, h * .1f);
            using (var path = RoundRect(new RectangleF(-w / 2, 0, w, h), 2.2f * scale)) { g.FillPath(bBody, path); g.DrawPath(c.Ink, path); }
            float ls = w * .66f;
            LogoArt.Draw(g, new RectangleF(-ls / 2, h * .11f, ls, ls * .98f));
            using (var line = new Pen(Color.FromArgb(150, 150, 162), Math.Max(1.4f, 1.5f * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawLine(line, -w * .3f, h * .70f, w * .3f, h * .70f); g.DrawLine(line, -w * .3f, h * .84f, w * .12f, h * .84f); }
            g.Restore(st);
        }

        /// <summary>번쩍 들어 보여주는 명찰(날개가 모서리를 잡는다).</summary>
        private static void DrawHeldCard(Graphics g, Ctx c)
        {
            float k = SmoothStep(c.P.CardShow);
            DrawCard(g, c, CardTop(c), 1f + 1.1f * k, 1f, c.P.CardSwing - 8f * k);
        }

        // ───────── 짠! 반짝임(그림 9): 주황색 반짝선 두 줄과 별 ─────────
        private static void DrawSparkle(Graphics g, float t)
        {
            float a = .6f + .4f * (float)Math.Sin(t * 9);
            var orange = Color.FromArgb((int)(255 * (.78f + .22f * a)), 240, 148, 24);
            using (var pen = new Pen(orange, 3.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawArc(pen, 58, -86, 34, 16, 195, 110); g.DrawArc(pen, 62, -68, 34, 16, 195, 110); }
            using (var br = new SolidBrush(orange)) DrawStar(g, br, 102 + (float)Math.Sin(t * 5) * 1.5f, -84, 8f * (.8f + .3f * a), t * 20);
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

        // ───────── 로고 깃발: 첨부한 로고 이미지를 천에 그려 펄럭인다 ─────────
        private static void DrawFlag(Graphics g, Ctx c, PointF grip)
        {
            Pose p = c.P;
            double a = p.ToolAng * Math.PI / 180;
            PointF up = new PointF((float)Math.Sin(a), -(float)Math.Cos(a)), side = new PointF((float)Math.Cos(a), (float)Math.Sin(a));
            PointF end = new PointF(grip.X + up.X * 104, grip.Y + up.Y * 104), back = new PointF(grip.X - up.X * 14, grip.Y - up.Y * 14);
            using (var edge = new Pen(cInk, 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var pole = new Pen(Color.FromArgb(196, 198, 206), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawLine(edge, back, end); g.DrawLine(pole, back, end); }
            const int n = 14; const float fw = 52f, fh = 46f;
            var topPts = new PointF[n + 1]; var botPts = new PointF[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, wav = (float)Math.Sin(p.FlagWave - u * 5.5) * 5.5f * u;
                PointF t = new PointF(end.X - up.X * 3 + side.X * u * fw + up.X * wav, end.Y - up.Y * 3 + side.Y * u * fw + up.Y * wav);
                topPts[i] = t; botPts[i] = new PointF(t.X - up.X * fh, t.Y - up.Y * fh);
            }
            for (int i = 0; i < n; i++)
                LogoArt.DrawSlice(g, topPts[i], topPts[i + 1], botPts[i], i / (float)n, (i + 1) / (float)n);
            using (var rim = new Pen(cInk, Math.Max(2.4f, c.Ink.Width * .8f)) { LineJoin = LineJoin.Round })
            {
                g.DrawLines(rim, topPts); g.DrawLines(rim, botPts);
                g.DrawLine(rim, topPts[0], botPts[0]); g.DrawLine(rim, topPts[n], botPts[n]);
            }
        }

        // ───────── 날개: 흰 지느러미 + 검은 끝(도면 5). 옆에서 보면 넓은 날개판(U 모양)으로 보인다 ─────────
        private struct Wing { public PointF S, E, W; public float Width; }

        private static bool WingInFront(Ctx c, int side)
        {
            Pose p = c.P;
            if (side == 0 ? p.WingLTo.HasValue : p.WingRTo.HasValue) return true;
            if (p.WingBack) return false;
            bool near = (side == 1) == (c.Bd >= 0);
            return near && Math.Abs(c.BSin) > .3f;
        }

        private static Wing WingGeom(Ctx c, int side)
        {
            Pose p = c.P; bool right = side == 1; int o = right ? 1 : -1;
            PointF S = c.Map(Axis + o * ShoulderHalf, ShoulderY);
            float A = right ? p.WingRA : p.WingLA, B = right ? p.WingRB : p.WingLB;
            PointF? to = right ? p.WingRTo : p.WingLTo;
            if (to.HasValue) IK(S, c.Map(to.Value), WingL1, WingL2, o, out A, out B);
            PointF E = new PointF(S.X + o * (float)Math.Sin(A * Math.PI / 180) * WingL1, S.Y + (float)Math.Cos(A * Math.PI / 180) * WingL1);
            PointF d = new PointF(o * (float)Math.Sin(B * Math.PI / 180), (float)Math.Cos(B * Math.PI / 180));
            PointF W = new PointF(E.X + d.X * WingL2, E.Y + d.Y * WingL2);
            float ax = c.Map(Axis, ShoulderY).X;
            float bc = c.BCos, back = (right ? p.WingRSweep : p.WingLSweep) * (WingL1 + WingL2) * c.BSin;
            // 몸의 yaw 로 옆으로 퍼진 거리는 줄고(cos), 앞뒤로 뻗은 거리는 옆모습에서 화면 가로로 드러난다(sin)
            Func<PointF, float, PointF> sq = (q, t) => new PointF(ax + (q.X - ax) * bc + back * t, q.Y);
            return new Wing { S = sq(S, 0f), E = sq(E, .5f), W = sq(W, 1f), Width = 1f + 1.3f * c.BSin * c.BSin * (1f - .75f * Math.Min(1f, Math.Abs(right ? p.WingRSweep : p.WingLSweep))) };
        }

        private static void DrawWing(Graphics g, Ctx c, int side)
        {
            Wing w = WingGeom(c, side);
            float wm = w.Width;
            PointF[] band = Geo.Limb(new[] { w.S, w.E, w.W }, new[] { 21f * wm, 20f * wm, 18f * wm }, true, 6);
            g.FillPolygon(bBody, band); g.DrawLines(c.Ink, band);   // 뿌리 쪽은 선을 긋지 않아 몸에서 이어져 보인다
            if (c.P.Outfit)
            {
                // 셔츠 소매의 단(커프스): 검은 끝 바로 앞에 가로선 두 줄. 날개 모양은 그대로다.
                using (var cuff = new Pen(cInk, Math.Max(2f, c.Ink.Width * .7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    foreach (float f in new[] { .27f, .38f })
                    {
                        PointF a0 = f < .5f ? w.S : w.E, a1 = f < .5f ? w.E : w.W;
                        float ff = f < .5f ? f / .5f : (f - .5f) / .5f;
                        PointF pt = new PointF(a0.X + (a1.X - a0.X) * ff, a0.Y + (a1.Y - a0.Y) * ff);
                        float dx = a1.X - a0.X, dy = a1.Y - a0.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (len < 1e-3f) continue;
                        float nx = -dy / len, ny = dx / len, hl = 9.2f * wm;
                        g.DrawLine(cuff, pt.X - nx * hl, pt.Y - ny * hl, pt.X + nx * hl, pt.Y + ny * hl);
                    }
            }
            PointF M = new PointF(w.E.X + (w.W.X - w.E.X) * .1f, w.E.Y + (w.W.Y - w.E.Y) * .1f);
            PointF mid = new PointF((M.X + w.W.X) / 2, (M.Y + w.W.Y) / 2);
            PointF[] tip = Geo.Limb(new[] { M, mid, w.W }, new[] { 18f * wm, 23f * wm, 20f * wm }, true, 6);
            g.FillPolygon(bInk, tip); g.DrawPolygon(c.Ink, tip);
        }

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

        // ───────── 곡괭이(참조 그림 4의 마인크래프트 부기) ─────────
        private static void DrawPickaxe(Graphics g, Ctx c, PointF grip)
        {
            double a = c.P.ToolAng * Math.PI / 180;
            PointF up = new PointF((float)Math.Sin(a), -(float)Math.Cos(a)), side = new PointF((float)Math.Cos(a), (float)Math.Sin(a));
            PointF end = new PointF(grip.X + up.X * 84, grip.Y + up.Y * 84);
            PointF back = new PointF(grip.X - up.X * 12, grip.Y - up.Y * 12);
            using (var edge = new Pen(Color.FromArgb(60, 40, 24), 9f) { StartCap = LineCap.Square, EndCap = LineCap.Square })
            using (var wood = new Pen(Color.FromArgb(150, 104, 58), 5.4f) { StartCap = LineCap.Square, EndCap = LineCap.Square })
            { g.DrawLine(edge, back, end); g.DrawLine(wood, back, end); }
            using (var stone = new SolidBrush(Color.FromArgb(150, 152, 160)))
            using (var shade = new SolidBrush(Color.FromArgb(104, 106, 114)))
            using (var rim = new Pen(Color.FromArgb(52, 52, 60), 2.4f) { LineJoin = LineJoin.Miter })
            {
                float[] off = { -30, -19, -8, 3, 14 }, bend = { 12f, 5f, 0, 5f, 12f };
                for (int i = 0; i < 5; i++)
                {
                    float o = off[i] + 6;
                    PointF ctr = new PointF(end.X + side.X * o - up.X * bend[i], end.Y + side.Y * o - up.Y * bend[i]);
                    const float h = 6.2f;
                    var pts = new[]
                    {
                        new PointF(ctr.X - side.X * h - up.X * h, ctr.Y - side.Y * h - up.Y * h),
                        new PointF(ctr.X + side.X * h - up.X * h, ctr.Y + side.Y * h - up.Y * h),
                        new PointF(ctr.X + side.X * h + up.X * h, ctr.Y + side.Y * h + up.Y * h),
                        new PointF(ctr.X - side.X * h + up.X * h, ctr.Y - side.Y * h + up.Y * h)
                    };
                    g.FillPolygon(i % 2 == 0 ? stone : shade, pts); g.DrawPolygon(rim, pts);
                }
            }
        }

        private static void DrawDebris(Graphics g, Pose p)
        {
            float q = Clamp(p.Debris, 0, 1);
            float[][] bits = { new[] { -1.0f, 1.1f, 6f }, new[] { -.5f, 1.6f, 5f }, new[] { .2f, 1.8f, 7f }, new[] { .8f, 1.3f, 5f }, new[] { 1.3f, .9f, 6f }, new[] { -1.4f, .7f, 4f } };
            for (int i = 0; i < bits.Length; i++)
            {
                float x = p.DebrisX + bits[i][0] * 30 * q, y = p.DebrisY - (bits[i][1] * 40 * q - 70 * q * q), sz = bits[i][2] * (1 - q * .4f);
                int a = (int)(255 * (1 - q * q));
                using (var fill = new SolidBrush(Color.FromArgb(a, i % 2 == 0 ? 150 : 110, i % 2 == 0 ? 152 : 112, i % 2 == 0 ? 160 : 118)))
                using (var rim = new Pen(Color.FromArgb(a, 52, 52, 60), 1.8f))
                { g.FillRectangle(fill, x - sz / 2, y - sz / 2, sz, sz); g.DrawRectangle(rim, x - sz / 2, y - sz / 2, sz, sz); }
            }
        }

        // ───────── 간식 ─────────
        private static void DrawCookie(Graphics g, Ctx c)
        {
            float bite = Clamp(c.P.Cookie, 0, 1);
            PointF at = c.Map(200, 150);
            var st = g.Save();
            g.TranslateTransform(at.X, at.Y);
            using (var cookie = new SolidBrush(Color.FromArgb(240, 192, 128)))
            using (var chip = new SolidBrush(Color.FromArgb(104, 62, 40)))
            {
                float r = 18;
                const int n = 40;
                var pts = new PointF[n];
                for (int i = 0; i < n; i++)
                {
                    double a = i * Math.PI * 2 / n, d = Math.Abs(Math.Atan2(Math.Sin(a + 1.6), Math.Cos(a + 1.6)));
                    double cut = Math.Max(0, 1 - d / (.5 + bite * 2.1)) * bite * (.62 + .22 * Math.Abs(Math.Sin(a * 4.5)));
                    float rr = (float)(r * (1 - cut));
                    pts[i] = new PointF((float)Math.Cos(a) * rr, (float)Math.Sin(a) * rr);
                }
                g.FillPolygon(cookie, pts); g.DrawPolygon(c.Ink, pts);
                if (bite < .9f)
                { g.FillEllipse(chip, -r * .45f, -r * .35f, 5, 5); g.FillEllipse(chip, r * .15f, r * .25f, 4.5f, 4.5f); g.FillEllipse(chip, -r * .15f, -r * .65f, 4, 4); }
            }
            g.Restore(st);
        }

        internal static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }
}
