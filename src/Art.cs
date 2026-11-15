using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Bugi
{
    internal sealed class CharInfo
    {
        public string Key, Name, Title;       // 키, 이름, 칭호
        public Color Accent;                  // 말풍선·꽃 장식 등에 어울리는 색
        public string[] Traits;               // 특징 설명(도감)
    }

    /// <summary>
    /// 부기의 크기와 기준 치수. 그림은 Rig.cs 가 부위별로 그린다
    /// (이미지 한 장을 변형하는 방식이 아니라, 날개·다리·꼬리·눈·부리가 따로 움직인다).
    /// </summary>
    internal sealed class CharacterArt
    {
        internal static readonly CharInfo[] Infos =
        {
            new CharInfo
            {
                Key = "bugi",
                Name = "부기",
                Title = "부산 소통 캐릭터 · 부산갈매기",
                Accent = Color.FromArgb(232, 60, 52),
                Traits = new[]
                {
                    "부산 시조 갈매기를 모티프로 한 소통 캐릭터",
                    "붉은 뿔테 안경과 통통한 날개",
                    "가이드북 동작: 두두 등장 · Busan is good · 하트 안기",
                    "옷차림: 하늘색 조끼 · 크로스백 · 바이저 · 선글라스"
                }
            },
            new CharInfo
            {
                Key = "bukangyee",
                Name = "부캉이",
                Title = "부산 소통 캐릭터 · 부산 상어",
                Accent = Color.FromArgb(40, 132, 235),
                Traits = new[]
                {
                    "부산 바다에서 온 통통하고 씩씩한 꼬마 상어",
                    "까만 백팩 · 등지느러미 · 노란 테 선글라스",
                    "시그니처 동작: 부산 최고! · 파도 깃발 · 액션캠 셀카",
                    "홍보대사 어깨띠를 두르면 안내 · 배꼽인사 · 응원"
                }
            },
        };
        internal static int Count { get { return Infos.Length; } }
        internal static float Scale = 1f;
        /// <summary>화면 DPI 배율(설정의 캐릭터 크기를 곱하기 전). 알림 부기 · 트레이 아이콘 · 입력칸처럼 크기 설정과 상관없는 것에 쓴다.</summary>
        internal static float DpiScale = 1f;
        /// <summary>설정의 캐릭터 크기 비율(1 = 100%).</summary>
        internal static float SizeFactor { get { return DpiScale > 0 ? Scale / DpiScale : 1f; } }
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
        internal static void Reset(float scale, float size = 1f) { DpiScale = scale; Scale = scale * size; cache.Clear(); }

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
            W = (int)Math.Round((id == 1 ? 214 : 150) * K);
            HeadRoom = (int)Math.Round(H * .12f);   // 안전모(키 상자 위로 약 .11H 솟음)가 화면 맨 위에서도 잘리지 않을 만큼
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
                int n = Math.Max(16, (int)Math.Round(16 * DpiScale));
                using (Bitmap bmp = IconBitmap(id, n))
                {
                    IntPtr hicon = bmp.GetHicon();
                    Icon icon = (Icon)Icon.FromHandle(hicon).Clone(); Native.DestroyIcon(hicon); return icon;
                }
            }
            catch { return SystemIcons.Application; }
        }

        internal static Bitmap IconBitmap(int id, int n)
        {
            var bmp = new Bitmap(n, n, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Quality(g); g.Clear(Color.Transparent);
                if (id == 1)
                {
                    // 부캉이 머리(가로 184 · 등지느러미 포함)가 아이콘 칸 안에 들어오도록 확대
                    float k = (n - 1.5f) / 204f;
                    g.TranslateTransform(n / 2f, (BukangyeeRig.RefFootY - 14f) * k + .75f);
                    var pose = new Pose { Turn = -.12f, Eyes = EyeStyle.Happy, Beak = .6f, Blush = .5f };
                    BukangyeeRig.Draw(g, pose, k, false);
                }
                else
                {
                    // 부기 머리: 기준 치수 y 4~154 가 정사각형에 들어오도록 발 위치를 아래로 내린다
                    float k = (n - 1.5f) / 150f;
                    g.TranslateTransform(n / 2f, (Rig.RefFootY - 4f) * k + .75f);
                    var pose = new Pose { Turn = -.25f };
                    Rig.Draw(g, pose, k, false);
                }
            }
            return bmp;
        }

        /// <summary>얼굴(머리카락·빨간 안경·부리)이 잘 보이도록 머리를 확대한 아이콘 그림.</summary>
        internal static Bitmap IconBitmap(int n)
        {
            return IconBitmap(0, n);
        }
    }
}
