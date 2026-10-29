using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Bugi
{
    internal sealed class CharInfo
    {
        public string Key, Name;
        public Color Accent;                  // 말풍선·꽃 장식 등에 어울리는 색
    }

    /// <summary>
    /// 부기의 크기와 기준 치수. 그림은 Rig.cs 가 부위별로 그린다
    /// (이미지 한 장을 변형하는 방식이 아니라, 날개·다리·꼬리·눈·부리가 따로 움직인다).
    /// </summary>
    internal sealed class CharacterArt
    {
        internal static readonly CharInfo[] Infos =
        {
            new CharInfo { Key = "bugi", Name = "부기", Accent = Color.FromArgb(232, 60, 52) },
        };
        internal static int Count { get { return Infos.Length; } }
        internal static float Scale = 1f;
        /// <summary>서 있을 때 키(귀끝~발바닥, 화면 픽셀). 배율 1 기준.</summary>
        internal const float BaseHeight = 112f;

        private static readonly Dictionary<int, CharacterArt> cache = new Dictionary<int, CharacterArt>();
        internal static CharacterArt Get(int id)
        {
            id = ((id % Count) + Count) % Count;
            CharacterArt a;
            if (!cache.TryGetValue(id, out a)) cache[id] = a = new CharacterArt(id);
            return a;
        }
        internal static void Reset(float scale) { Scale = scale; cache.Clear(); }

        /// <summary>같은 캐릭터를 배율을 더 키워 그린다(알림 부기). 걸어 다니는 부기는 늘 배율 1.</summary>
        internal static CharacterArt Zoomed(int id, float zoom)
        {
            id = ((id % Count) + Count) % Count;
            return new CharacterArt(id, zoom);
        }

        public readonly int Id;
        public readonly CharInfo Info;
        public readonly float Zoom;       // 걷는 부기 대비 확대 배율
        public readonly float K;          // 기준 그림 1픽셀 = 화면 K 픽셀
        public readonly int W, H;         // 서 있는 모습의 상자(걷기 기준) — 이동·충돌 기준
        public readonly int HeadRoom;       // 머리 위에 남겨 두는 여유(화면 맨 위 보정용)
        /// <summary>장식·말풍선 치수의 기준 단위(화면 배율 × 확대 배율).</summary>
        public float Unit { get { return Scale * Zoom; } }

        private CharacterArt(int id, float zoom = 1f)
        {
            Id = id; Info = Infos[id]; Zoom = zoom;
            H = (int)Math.Round(BaseHeight * Scale * zoom);
            K = H / Rig.RefH;
            W = (int)Math.Round(150 * K);
            HeadRoom = (int)Math.Round(H * .08f);
        }

        internal static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        /// <summary>투명하지 않은 부분의 경계.</summary>
        internal static Rectangle Bounds(Bitmap b, int threshold = 30)
        {
            int l = b.Width, t = b.Height, r = -1, bot = -1;
            var data = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var bytes = new byte[data.Stride * b.Height]; System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                for (int y = 0; y < b.Height; y++)
                    for (int x = 0; x < b.Width; x++)
                        if (bytes[y * data.Stride + x * 4 + 3] > threshold) { if (x < l) l = x; if (y < t) t = y; if (x > r) r = x; if (y > bot) bot = y; }
            }
            finally { b.UnlockBits(data); }
            return r < l ? new Rectangle(0, 0, b.Width, b.Height) : Rectangle.FromLTRB(l, t, r + 1, bot + 1);
        }

        /// <summary>트레이·창 아이콘용 작은 그림.</summary>
        internal static Icon MakeIcon(int id)
        {
            try
            {
                int n = Math.Max(16, (int)Math.Round(16 * Scale));
                using (Bitmap bmp = IconBitmap(n))
                {
                    IntPtr hicon = bmp.GetHicon();
                    Icon icon = (Icon)Icon.FromHandle(hicon).Clone(); Native.DestroyIcon(hicon); return icon;
                }
            }
            catch { return SystemIcons.Application; }
        }

        /// <summary>얼굴(머리카락·빨간 안경·부리)이 잘 보이도록 머리를 확대한 아이콘 그림.</summary>
        internal static Bitmap IconBitmap(int n)
        {
            var bmp = new Bitmap(n, n, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Quality(g); g.Clear(Color.Transparent);
                // 머리: 기준 치수 y 4~154 가 정사각형에 들어오도록 발 위치를 아래로 내린다
                float k = (n - 1.5f) / 150f;
                g.TranslateTransform(n / 2f, (Rig.RefFootY - 4f) * k + .75f);
                var pose = new Pose { Turn = -.25f };
                Rig.Draw(g, pose, k, false);
            }
            return bmp;
        }
    }
}
