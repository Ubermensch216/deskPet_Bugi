using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Bugi
{
    /// <summary>
    /// 첨부한 로고 이미지(파란 사각형 안의 하얀 'B' 모양 마크)를 명찰·깃발에 쓰기 위한 도우미.
    /// 이미지 자체는 LogoData 에 내장돼 있어 EXE 한 개로 실행된다.
    /// </summary>
    internal static class LogoArt
    {
        private static Bitmap image;
        internal static readonly Color Blue = Color.FromArgb(30, 77, 164);

        internal static Bitmap Image
        {
            get
            {
                if (image != null) return image;
                try { using (var ms = new MemoryStream(Convert.FromBase64String(LogoData.Png))) image = new Bitmap(ms); }
                catch { image = Fallback(); }
                return image;
            }
        }

        /// <summary>내장 이미지를 못 읽을 때만 쓰는 대체 그림(같은 파란 바탕에 하얀 선).</summary>
        private static Bitmap Fallback()
        {
            var b = new Bitmap(55, 54);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Blue);
                using (var pen = new Pen(Color.White, 2f))
                {
                    g.DrawLines(pen, new[] { new PointF(10, 7), new PointF(10, 46), new PointF(39, 46), new PointF(48, 36), new PointF(10, 17), new PointF(39, 7), new PointF(48, 17), new PointF(35, 30) });
                }
            }
            return b;
        }

        /// <summary>목적 사각형에 로고를 그린다.</summary>
        internal static void Draw(Graphics g, RectangleF r)
        {
            DrawSlice(g, new PointF(r.Left, r.Top), new PointF(r.Right, r.Top), new PointF(r.Left, r.Bottom), 0f, 1f);
        }

        /// <summary>로고의 가로 구간 [u0, u1] (0..1)을 평행사변형(왼위·오른위·왼아래)에 그린다. 깃발이 펄럭일 때 조각조각 이어 붙인다.</summary>
        internal static void DrawSlice(Graphics g, PointF ul, PointF ur, PointF ll, float u0, float u1)
        {
            Bitmap img = Image;
            var src = new RectangleF(img.Width * u0, 0, Math.Max(1f, img.Width * (u1 - u0)), img.Height);
            using (var ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(img, new[] { ul, ur, ll }, src, GraphicsUnit.Pixel, ia);
            }
        }
    }
}
