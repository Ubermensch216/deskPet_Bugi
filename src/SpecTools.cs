using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Bugi
{
    /// <summary>공식 규격 시트(ref/bugi-spec.png)와 나란히 비교하려고 정면·3/4·옆모습과 얼굴 확대를 그린다(개발용).</summary>
    internal static class SpecTools
    {
        internal static void Render(string dir)
        {
            Directory.CreateDirectory(dir);
            Views(dir, false, "spec_views.png");
            Views(dir, true, "spec_views_outfit.png");
            Face(dir);
            Console.WriteLine("saved " + dir);
        }

        /// <summary>회전 도면: 정면 · 3/4 · 옆 (왼쪽을 보는 모습) + 3/4 · 옆 (오른쪽). 점선은 글래스 선·부리 높이·어깨(날개 시작).</summary>
        private static void Views(string dir, bool outfit, string file)
        {
            float k = 2.0f;
            float[] turns = { 0f, -.33f, -1f, .33f, 1f };
            int cw = 430, ch = (int)(Rig.RefH * k) + 40;
            using (var bmp = new Bitmap(cw * turns.Length, ch, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                CharacterArt.Quality(g); g.Clear(Color.White);
                float ground = ch - 18;
                using (var guide = new Pen(Color.FromArgb(120, 120, 120)) { DashStyle = DashStyle.Dot })
                {
                    float gy = ground - (Rig.RefFootY - 23f) * k;          // 글래스 선(G)
                    g.DrawLine(guide, 0, gy, bmp.Width, gy);
                    g.DrawLine(guide, 0, gy + 12 * 6.5f * k, bmp.Width, gy + 12 * 6.5f * k);   // 부리 높이쯤
                    g.DrawLine(guide, 0, gy + 12 * 12.5f * k, bmp.Width, gy + 12 * 12.5f * k); // 어깨(날개 시작)
                    g.DrawLine(Pens.Black, 0, ground, bmp.Width, ground);
                }
                for (int i = 0; i < turns.Length; i++)
                {
                    var st = g.Save();
                    g.TranslateTransform(cw * i + cw / 2f, ground);
                    var pose = new Pose { Turn = turns[i] == 0 ? 0 : -Math.Abs(turns[i]), Look = 0, Outfit = outfit };
                    Rig.Draw(g, pose, k, turns[i] > 0);
                    g.Restore(st);
                }
                bmp.Save(Path.Combine(dir, file), ImageFormat.Png);
            }
        }

        /// <summary>얼굴 확대(눈 지름 1 기준으로 격자): 정면.</summary>
        private static void Face(string dir)
        {
            float k2 = 6f;
            using (var bmp = new Bitmap(760, 900, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                CharacterArt.Quality(g); g.Clear(Color.White);
                // 원점(발)을 화면 아래로 내려 머리가 위쪽에 오도록 한다
                g.TranslateTransform(380, Rig.RefFootY * k2 - 6f * k2 + 10);
                Rig.Draw(g, new Pose(), k2, false);
                g.ResetTransform();
                float d = 12f * k2, gy = (23f - 6f + 10f / k2) * k2;     // 글래스 선 y(화면)
                using (var guide = new Pen(Color.FromArgb(150, 60, 120, 255)) { DashStyle = DashStyle.Dash })
                {
                    for (int n = 0; n <= 8; n++) g.DrawLine(guide, 0, gy + n * d, bmp.Width, gy + n * d);
                    for (int n = -4; n <= 4; n++) g.DrawLine(guide, 380 + n * d, 0, 380 + n * d, bmp.Height);
                }
                bmp.Save(Path.Combine(dir, "spec_face.png"), ImageFormat.Png);
            }
        }
    }
}
