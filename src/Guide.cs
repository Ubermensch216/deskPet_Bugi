using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Bugi
{
    /// <summary>
    /// 부산시 소통캐릭터 부기 베이직 가이드북(26.04, ref/bugi-guidebook.pdf)의 기준값과 자동 점검.
    /// 캐릭터를 그리는 코드(Rig*.cs)의 색·선·비율은 여기 값을 쓰고, `bugi.exe --guide-test` 가 어긋남을 경고한다.
    /// 자세한 규정과 쪽수는 docs/GUIDE_RULES.md 에 있다.
    /// </summary>
    internal static class BugiGuide
    {
        // p12 전용색상(CMYK → sRGB). 부리·다리는 같은 노랑, 선과 신발은 K100 검정이다.
        internal static readonly Color Red = Color.FromArgb(237, 26, 35);        // 0 100 100 0   안경 · 동백꽃
        internal static readonly Color Yellow = Color.FromArgb(255, 212, 0);     // 0 15 100 0    부리 · 다리
        internal static readonly Color Green = Color.FromArgb(0, 177, 105);       // 80 0 80 0     동백잎
        internal static readonly Color Black = Color.FromArgb(33, 29, 31);       // 0 0 0 100     선 · 신발 · 눈 · 날개 끝 · 꼬리 · 머리카락
        internal static readonly Color White = Color.White;                      // 몸통 · 날개 · 신발 밑창 · 신발끈
        // p22 보조색(로고타입): 짙은 빨강 0 100 100 60 — 망토 안쪽 같은 그늘색에 쓴다
        internal static readonly Color DarkRed = Color.FromArgb(110, 18, 24);
        // p13 배경색(CMYK → sRGB, 가이드북 쪽을 그려 찍은 값): 부기를 올려 두는 바탕. 노랑 0 25 100 0 · 빨강 0 100 100 25 · 민트 70 0 50 0 · 파랑 100 70 0 0
        internal static readonly Color BgYellow = Color.FromArgb(254, 193, 11), BgRed = Color.FromArgb(186, 18, 24), BgMint = Color.FromArgb(56, 186, 156), BgBlue = Color.FromArgb(0, 90, 170);

        /// <summary>선 굵기: 캐릭터 전체 키(머리카락 끝~밑창)의 약 1%(p17 라인규정: 얇은 선·굵기가 다른 선은 금지).</summary>
        internal const float OutlineRatioMin = .0075f, OutlineRatioMax = .014f;

        /// <summary>눈 지름 D 기준 비율(규격 시트 1): 눈 사이 3D(중심 간격), 부리 너비 3.6D, 안경 전체 너비 5.5D.</summary>
        internal const float EyeGapD = 3f, BeakWidthD = 3.6f, GlassWidthD = 5.5f, RatioTolerance = .12f;

        /// <summary>p16 공식 표정 여덟 가지(슬픔·윙크·놀람·먹방·흐뭇·평온·화남·수면)에 쓰는 눈 모양. Spiral 은 숨은 반응(어질어질) 전용이다.</summary>
        internal static readonly EyeStyle[] AllowedEyes = { EyeStyle.Dot, EyeStyle.Half, EyeStyle.Closed, EyeStyle.Happy, EyeStyle.Squint, EyeStyle.Wide, EyeStyle.Spiral };

        // ───────── 자동 점검 ─────────
        /// <summary>어긋난 항목(경고)과 통과한 항목(정보)을 돌려준다.</summary>
        internal static void Check(List<string> warns, List<string> infos)
        {
            const float k = 4f;
            using (var bmp = new Bitmap(700, 1300, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                CharacterArt.Quality(g); g.Clear(Color.White);
                g.TranslateTransform(350, 1250);
                Rig.Draw(g, new Pose(), k, false);
                Func<int, int, Color> px = (x, y) => bmp.GetPixel(Math.Max(0, Math.Min(bmp.Width - 1, x)), Math.Max(0, Math.Min(bmp.Height - 1, y)));
                Func<float, int> sx = rx => (int)Math.Round(350 + (rx - Rig.RefFootX) * k);
                Func<float, int> sy = ry => (int)Math.Round(1250 + (ry - Rig.RefFootY) * k);

                // 1) 색: 흰 바탕이 아닌 픽셀은 공식 색(또는 두 공식 색 사이의 안티앨리어싱 번짐)이어야 한다
                Color[] official = { Red, Yellow, Green, Black, White };
                int total = 0, off = 0; Color worst = Color.Empty; double worstD = 0;
                for (int y = 0; y < bmp.Height; y += 2)
                    for (int x = 0; x < bmp.Width; x += 2)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (c.R > 250 && c.G > 250 && c.B > 250) continue;
                        total++;
                        double d = double.MaxValue;
                        for (int i = 0; i < official.Length; i++)
                            for (int j = i; j < official.Length; j++) d = Math.Min(d, DistToSegment(c, official[i], official[j]));
                        if (d > 14) { off++; if (d > worstD) { worstD = d; worst = c; } }
                    }
                if (total > 0 && off / (double)total > .004)
                    warns.Add(string.Format("색: 공식 색(빨강·노랑·초록·검정·흰색)이 아닌 픽셀 {0:P1} (가장 먼 색 RGB {1},{2},{3}) — 가이드 p12 전용색상", off / (double)total, worst.R, worst.G, worst.B));
                else infos.Add("색: 몸·안경·부리·다리·신발이 공식 전용색상(p12)을 따름");

                // 부리와 다리는 같은 노랑
                Color beak = px(sx(200), sy(Rig.HeadLocalOrigin.Y + 5)), leg = Color.White; double legBest = 999;
                for (int y = sy(256); y < sy(272); y++) for (int x = sx(170); x < sx(230); x++) { Color c = px(x, y); double d = Dist(c, Yellow); if (d < legBest) { legBest = d; leg = c; } }
                if (Dist(beak, Yellow) > 20 || legBest > 20)
                    warns.Add(string.Format("부리·다리 노랑이 공식 노랑(0 15 100 0)과 다름: 부리 {0},{1},{2} 다리 {3},{4},{5} (p17 컬러규정)", beak.R, beak.G, beak.B, leg.R, leg.G, leg.B));
                else infos.Add("부리와 다리는 같은 공식 노랑");

                // 2) 선 굵기: 몸 가운데 높이에서 왼쪽 윤곽선의 두께 / 키
                int yy = sy(Rig.RefFootY - 150), run = 0, best = 0;
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = px(x, yy);
                    if (c.R < 110 && c.G < 110 && c.B < 110) run++; else { if (run > 0 && best == 0) best = run; run = 0; }
                }
                float ratio = best / k / (Rig.RefFootY - 23f);
                if (best == 0 || ratio < OutlineRatioMin || ratio > OutlineRatioMax)
                    warns.Add(string.Format("선 굵기가 키의 {0:P2} — 가이드는 약 1%({1:P2}~{2:P2}), 굵기가 다른 선 금지 (p17 라인규정)", ratio, OutlineRatioMin, OutlineRatioMax));
                else infos.Add(string.Format("선 굵기 키의 {0:P2}", ratio));

                // 3) 비율(규격 시트 1·2): 눈 지름 D 기준
                float D = 12f * k;
                int eyeY = sy(Rig.HeadLocalOrigin.Y - 11);
                var blobs = new List<int[]>(); int start = -1;
                for (int x = 0; x <= bmp.Width; x++)
                {
                    bool dark = x < bmp.Width && px(x, eyeY).R < 90 && px(x, eyeY).G < 90 && Math.Abs(x - 350) < 12 * k * 5;
                    if (dark && start < 0) start = x; else if (!dark && start >= 0) { blobs.Add(new[] { start, x - 1 }); start = -1; }
                }
                blobs = blobs.Where(b => b[1] - b[0] > D * .5f && b[1] - b[0] < D * 1.6f).ToList();
                if (blobs.Count != 2) warns.Add("정면에서 눈 두 개(검은 점)를 찾지 못함 (규격 시트 1)");
                else
                {
                    float dia = ((blobs[0][1] - blobs[0][0]) + (blobs[1][1] - blobs[1][0])) / 2f + 1, gap = ((blobs[1][0] + blobs[1][1]) - (blobs[0][0] + blobs[0][1])) / 2f;
                    if (Math.Abs(dia / D - 1) > RatioTolerance || Math.Abs(gap / dia - EyeGapD) > EyeGapD * RatioTolerance)
                        warns.Add(string.Format("눈 비율: 지름 {0:0.00}D, 눈 중심 간격 {1:0.00}D — 규격은 지름 1D, 간격 3D (규격 시트 1)", dia / D, gap / dia));
                    else infos.Add(string.Format("눈: 지름 {0:0.00}D, 간격 {1:0.00}D", dia / D, gap / dia));
                }
                int beakY = sy(Rig.HeadLocalOrigin.Y + 12.4f);
                int bl = -1, br = -1;
                for (int x = 0; x < bmp.Width; x++) { Color c = px(x, beakY - (int)(D * .55f)); if (Dist(c, Yellow) < 30) { if (bl < 0) bl = x; br = x; } }
                // 노란 면만 재므로 가로줄 위 .55D 높이에서 줄어드는 비율(.92)과 윤곽선 두께를 보정한다
                float beakW = bl < 0 ? 0 : (br - bl + 1) / D / .92f + 2.8f / 12f;
                if (Math.Abs(beakW - BeakWidthD) > BeakWidthD * .18f) warns.Add(string.Format("부리 너비 {0:0.00}D — 규격은 약 3.6D (규격 시트 1)", beakW));
                else infos.Add(string.Format("부리 너비 {0:0.00}D", beakW));
                int gl = int.MaxValue, gr = -1;
                for (int y = sy(Rig.HeadLocalOrigin.Y - 82); y < sy(Rig.HeadLocalOrigin.Y - 56); y++)
                    for (int x = 0; x < bmp.Width; x++) if (Dist(px(x, y), Red) < 40) { if (x < gl) gl = x; if (x > gr) gr = x; }
                float glassW = gr < 0 ? 0 : (gr - gl + 1) / D;
                if (Math.Abs(glassW - GlassWidthD) > GlassWidthD * .12f) warns.Add(string.Format("안경 전체 너비 {0:0.00}D — 규격은 약 5.5D (규격 시트 2)", glassW));
                else infos.Add(string.Format("안경 전체 너비 {0:0.00}D", glassW));
            }

            // 4) 눈 모양·자세 한도는 코드 상수로 점검
            infos.Add("표정(p16): 눈 모양 " + string.Join("·", AllowedEyes.Select(e => e.ToString())) + " 만 사용");
            float sq = Rig.MaxSquash, tilt = Rig.MaxHeadTilt;
            if (sq > .05f || tilt > 12f) warns.Add("규격 가드가 느슨해졌어요: 몸 눌림 " + sq + " (최대 0.05), 머리 기울임 " + tilt + " (최대 12°) — p17 형태규정");
            else infos.Add("규격 가드: 몸 눌림 ≤5%, 머리 기울임 ≤12°");
        }

        private static double Dist(Color a, Color b)
        { int r = a.R - b.R, g = a.G - b.G, bl = a.B - b.B; return Math.Sqrt(r * r + g * g + bl * bl); }

        /// <summary>색 c 에서 a-b 를 잇는 선분(두 색을 섞은 색들)까지의 RGB 거리.</summary>
        private static double DistToSegment(Color c, Color a, Color b)
        {
            double ax = a.R, ay = a.G, az = a.B, bx = b.R - ax, by = b.G - ay, bz = b.B - az;
            double len2 = bx * bx + by * by + bz * bz;
            double t = len2 < 1e-6 ? 0 : Math.Max(0, Math.Min(1, ((c.R - ax) * bx + (c.G - ay) * by + (c.B - az) * bz) / len2));
            double dx = c.R - (ax + bx * t), dy = c.G - (ay + by * t), dz = c.B - (az + bz * t);
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>`bugi.exe --guide-test [폴더]`: 점검 결과를 화면에 쓰고(경고는 WARNING) guide_check.txt 로 남긴다. 경고가 있으면 종료 코드 2.</summary>
        internal static int Run(string dir)
        {
            var warns = new List<string>(); var infos = new List<string>();
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
            Check(warns, infos);
            var sb = new StringBuilder();
            sb.AppendLine("부기 가이드북 점검 (docs/GUIDE_RULES.md)");
            foreach (string s in infos) sb.AppendLine("  OK       " + s);
            foreach (string s in warns) sb.AppendLine("  WARNING  " + s);
            sb.AppendLine(warns.Count == 0 ? "가이드북에 어긋나는 항목이 없어요." : "※ 가이드북에 어긋나는 항목 " + warns.Count + "개 — 확인이 필요해요.");
            Console.Write(sb.ToString());
            try { if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "guide_check.txt"), sb.ToString(), new UTF8Encoding(true)); } } catch { }
            return warns.Count == 0 ? 0 : 2;
        }
    }
}
