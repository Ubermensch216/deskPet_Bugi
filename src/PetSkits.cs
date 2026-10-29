using System;
using System.Drawing;

namespace Bugi
{
    /// <summary>가이드북 p27~36 기본 응용동작(2D)에서 가져온 동작. 쉬는 자세(RestPose.Skit)와 AI 대화 감정에서 함께 쓴다.</summary>
    internal enum Skit { Hello, Laugh, HeartHug, Hero, Cheer, Proud, Present, Blank, Good, Okay, Busy, Busan, Travel, Pat }

    internal sealed partial class Pet
    {
        internal Skit skit = Skit.Hello;
        private Skit lastSkit = Skit.Pat;

        /// <summary>메뉴·도움말에 보이는 이름과 가이드북 쪽수.</summary>
        internal static string SkitTitle(Skit s)
        {
            switch (s)
            {
                case Skit.Hello: return "안녕 (p27)";
                case Skit.Laugh: return "ㅋㅋ 웃음 (p27)";
                case Skit.HeartHug: return "하트 안기 (p29)";
                case Skit.Hero: return "두두 등장 (p29)";
                case Skit.Cheer: return "응원 (p30)";
                case Skit.Proud: return "뿌듯 반짝 (p30)";
                case Skit.Present: return "발표 (p31)";
                case Skit.Blank: return "네..? (p31)";
                case Skit.Good: return "Good! (p32)";
                case Skit.Okay: return "OKAY! (p33)";
                case Skit.Busy: return "분주분주 (p33)";
                case Skit.Busan: return "Busan is good (p34)";
                case Skit.Travel: return "여행 (p34)";
                default: return "토닥토닥 (p35)";
            }
        }

        internal static double SkitSecondsOf(Skit s) { return SkitSeconds(s); }

        private static double SkitSeconds(Skit s)
        {
            switch (s)
            {
                case Skit.Hello: return 3.6; case Skit.Laugh: return 4.2; case Skit.HeartHug: return 4.6; case Skit.Hero: return 4.4;
                case Skit.Cheer: return 5.0; case Skit.Proud: return 3.8; case Skit.Present: return 5.6; case Skit.Blank: return 4.0; case Skit.Good: return 3.6;
                case Skit.Okay: return 4.6; case Skit.Busy: return 5.6; case Skit.Busan: return 5.0; case Skit.Travel: return 5.0; default: return 4.4;
            }
        }

        private Skit ChooseSkit(IWorld w)
        {
            Skit s = (Skit)w.Rng.Next(Enum.GetValues(typeof(Skit)).Length);
            if (s == lastSkit) s = (Skit)w.Rng.Next(Enum.GetValues(typeof(Skit)).Length);
            return s;
        }

        internal void BeginSkit(IWorld w, Skit s)
        {
            skit = lastSkit = s;
            BeginRestPose(w, RestPose.Skit, SkitSeconds(s));
        }

        // 자세 한 가지를 모두 같은 틀로 만든다: 서 있는 기본 자세 위에 동작을 얹고, 처음과 끝은 부드럽게 들어가고 나온다.
        private static PointF Pt(double x, double y) { return new PointF((float)x, (float)y); }

        private static void SkitFace(Pose P, EyeStyle eyes, float beak = 0) { P.Eyes = eyes; P.OpenL = P.OpenR = 1; P.Beak = beak; P.Look = 0; }

        /// <summary>t: 동작이 시작된 뒤 흐른 시간, dur: 동작의 길이(대화에서는 아주 큰 값).</summary>
        private void SkitPose(Pose P, ref Look L, Skit sk, double t, double dur)
        {
            IdleBase(P, animT, false);
            double e = Window(t, .05, dur - .1, .45), sw = Math.Sin(t * 6); float f = F(e);
            L.FaceRight = false;
            P.PropT = F(t); P.SfxT = F(t);
            switch (sk)
            {
                case Skit.Hello: // p27: 한쪽 날개를 번쩍 들어 흔들고 한쪽 다리를 차올리며 "안녕"
                    SkitFace(P, EyeStyle.Dot, F(.18 * (.5 + .5 * Math.Sin(t * 8)) * e));
                    P.WingLA = Lerp(P.WingLA, 150, e); P.WingLB = Lerp(P.WingLB, 128 + Math.Sin(t * 12) * 30, e);
                    P.FootRY = F(-15 * e); P.FootRX = F(-6 * e); P.FootRRot = F(-38 * e);
                    P.Lean = F(P.Lean + 4 * e); P.HeadTilt += 5 * f; P.Look = Lerp(P.Look, .2, e);
                    P.Sfx = "안녕"; P.SfxAt = Pt(92, 66); P.SfxSize = 24;
                    L.HopY = F(-Math.Abs(sw) * 3 * S * e);
                    break;
                case Skit.Laugh: // p27: 몸을 앞으로 접고 흔들며 눈을 감고 깔깔(ㅋ 표시가 둘러쌈)
                {
                    double k = Ease(t / .5) * (1 - Ease((t - (dur - .5)) / .5)), jig = Math.Sin(t * 22);
                    SkitFace(P, EyeStyle.Happy, F(.55 + .3 * Math.Sin(t * 22)));
                    L.Angle = F(-34 * k + jig * 2.5 * k); L.HopY = F(-Math.Abs(jig) * 2 * S * k);
                    P.Squash = F(.03 * k); P.Blush = F(.3 * k);
                    P.WingLTo = Pt(Lerp(138, 178, k), Lerp(214, 214, k)); P.WingRA = F(60 + 40 * Math.Abs(jig) * k); P.WingRB = F(50 + 36 * Math.Abs(jig) * k);
                    P.FootRY = F(-10 * k * (jig > 0 ? 1 : .3)); P.FootRRot = F(-24 * k);
                    P.Marks = F(t); P.GlassTilt += F(jig * 5 * k); P.Tuft = F(P.Tuft - 10 * k); P.TuftWave = 8; P.TuftPhase = F(t * 20);
                    break;
                }
                case Skit.HeartHug: // p29: 커다란 하트를 안고 한쪽 다리를 살짝 차올림, 하트가 떠오름
                    SkitFace(P, EyeStyle.Dot, .12f * f);
                    P.Prop = PropKind.Heart;
                    P.WingLTo = Pt(Lerp(138, 160, e), Lerp(214, 218, e)); P.WingRTo = Pt(Lerp(262, 240, e), Lerp(214, 218, e));
                    P.FootLY = F(-12 * e); P.FootLRot = F(-42 * e); P.FootLX = F(-4 * e);
                    P.HeadTilt += F(7 * e); P.Lean = F(P.Lean - 3 * e); P.Hearts = F(t);
                    L.HopY = F(-Math.Abs(Math.Sin(t * 3.4)) * 2 * S * e);
                    break;
                case Skit.Hero: // p29: "b" 입가리개와 망토, 두 손을 허리에 올리고 우뚝 서서 "두두 등장"
                    SkitFace(P, EyeStyle.Dot);
                    P.Cape = true; P.Mask = true;
                    P.WingLTo = Pt(Lerp(138, 150, e), Lerp(214, 226, e)); P.WingRTo = Pt(Lerp(262, 250, e), Lerp(214, 226, e));
                    P.FootLX = F(-8 * e); P.FootRX = F(8 * e); P.Brow = F(.22 * e); P.Look = 0;
                    P.Lean = F(P.Lean - 2 * e); P.Tuft = F(P.Tuft + 14 * e); P.TuftWave = 6;
                    P.Sfx = "두두 등장"; P.SfxAt = Pt(200, -22); P.SfxSize = 20;
                    break;
                case Skit.Cheer: // p30: 노란 응원 술을 흔들며 다리를 번갈아 차올리고 반짝반짝
                {
                    bool left = ((int)(t / .5)) % 2 == 0; double q = (t % .5) / .5, up = Math.Sin(Math.PI * q);
                    SkitFace(P, EyeStyle.Happy, F(.45 * up));
                    P.Prop = PropKind.Pompom; P.Stars = F(t);
                    P.WingLA = Lerp(P.WingLA, left ? 150 : 100, e); P.WingLB = Lerp(P.WingLB, left ? 150 : 96, e);
                    P.WingRA = Lerp(P.WingRA, left ? 100 : 150, e); P.WingRB = Lerp(P.WingRB, left ? 96 : 150, e);
                    if (left) { P.FootRY = F(-14 * up * e); P.FootRRot = F(-36 * up * e); } else { P.FootLY = F(-14 * up * e); P.FootLRot = F(-36 * up * e); }
                    P.Lean = F((left ? -3 : 3) * e); P.HeadTilt += F((left ? -5 : 5) * e); P.Blush = F(.3 * e);
                    L.HopY = F(-up * 6 * S * e); P.Tuft = F(P.Tuft - 8 * up); P.TuftWave = 8; P.TuftPhase = F(t * 14);
                    break;
                }
                case Skit.Proud: // p30: 날개를 가슴 앞에 모으고 으쓱, 별이 반짝
                    SkitFace(P, EyeStyle.Dot);
                    P.WingLTo = Pt(Lerp(138, 172, e), Lerp(214, 180, e)); P.WingRTo = Pt(Lerp(262, 228, e), Lerp(214, 180, e));
                    P.Stars = F(t); P.Gy = F(-.25 * e); P.HeadTilt += F(-3 * e); P.Squash = F(-.02 * e);
                    P.BodyY += F(-1.5 * e - Math.Abs(Math.Sin(t * 2.4)) * 1.2 * e);
                    break;
                case Skit.Present: // p31: 티셔츠와 청바지 차림으로 그림판을 가리키며 설명
                    SkitFace(P, EyeStyle.Dot, F(.2 * Math.Max(0, Math.Sin(t * 7)) * e));
                    P.Casual = true; P.Prop = PropKind.Board;
                    P.WingRTo = Pt(Lerp(262, 298, e), F(Lerp(214, 146, e) + Math.Sin(t * 2.2) * 6 * e)); P.WingLTo = Pt(Lerp(138, 152, e), Lerp(214, 204, e));
                    P.Look = Lerp(0, .55, e); P.Gx = Lerp(0, .9, e); P.HeadTilt += F(4 * e); P.Stars = F(t);
                    break;
                case Skit.Blank: // p31: 몸이 회색으로 비어 버린 듯 멍해서 "네..?" 하고 되묻는다
                    SkitFace(P, EyeStyle.Dot);
                    P.Gray = true; P.WingLA = 14; P.WingRA = 14; P.WingLB = 8; P.WingRB = 8;
                    P.HeadTilt += F(5 * e); P.Look = Lerp(0, .15, e); P.Gy = F(.1 * e);
                    P.Sfx = "네.. ?"; P.SfxAt = Pt(200, -30); P.SfxSize = 18;
                    break;
                case Skit.Good: // p32: 날개를 부리 옆으로 올려 엄지척, "Good!"
                    SkitFace(P, EyeStyle.Dot, .22f * f);
                    P.WingLTo = Pt(Lerp(138, 160, e), Lerp(214, 128 + Math.Sin(t * 5) * 3, e));
                    P.HeadTilt += F(-6 * e); P.Lean = F(P.Lean + 3 * e); P.Stars = F(t);
                    P.Sfx = "Good!"; P.SfxAt = Pt(268, 20); P.SfxSize = 28;
                    L.HopY = F(-Math.Abs(Math.Sin(t * 5)) * 2.5 * S * e);
                    break;
                case Skit.Okay: // p33: 감독 의자에 앉아 선글라스를 끼고 확성기로 "OKAY!"
                    SitDown(P);
                    SkitFace(P, EyeStyle.Dot, F(.6 * Math.Max(0, Math.Sin(t * 6)) * e));
                    P.Chair = true; P.Prop = PropKind.Megaphone; P.Shades = true; P.GlassDrop = F(Ease(t / .3)); P.GlassGlow = 0;
                    P.WingLTo = Pt(Lerp(138, 152, e), Lerp(214, 156, e)); P.WingRA = 36; P.WingRB = 30;
                    P.Look = Lerp(0, -.2, e); P.HeadTilt += F(-4 * e);
                    P.Sfx = "OKAY!"; P.SfxAt = Pt(52, 56); P.SfxSize = 36; P.SfxStyle = 1;
                    break;
                case Skit.Busy: // p33: 노트북을 보며 연필을 쓱쓱, 머리 위에 "분주분주"
                    SitDown(P);
                    SkitFace(P, EyeStyle.Dot);
                    P.Prop = PropKind.Laptop; P.Gy = .9f; P.Gx = 0;
                    P.WingRTo = Pt(F(Lerp(262, 230, e) + Math.Sin(t * 13) * 4 * e), F(Lerp(214, 214, e) + Math.Cos(t * 13) * 3 * e)); P.WingLTo = Pt(Lerp(138, 168, e), Lerp(214, 218, e));
                    P.HeadTilt = F(-3 + Math.Sin(t * 13) * 1.2); P.HeadY += F(3 * e);
                    P.Sfx = "분주분주분주분주분주\n분주분주분주분주분주"; P.SfxAt = Pt(200, -34); P.SfxSize = 14;
                    break;
                case Skit.Busan: // p34: 목걸이 명찰을 걸고 "Busan is good" 표지판 옆을 신나게 걷는다
                {
                    double ph = t * 5.4;
                    SkitFace(P, EyeStyle.Dot, .15f * f);
                    P.Lanyard = true; P.Prop = PropKind.Sign; P.Stars = F(t);
                    float dx, dy, rot;
                    Foot(ph / Tau, 8, 10, out dx, out dy, out rot); P.FootLX += dx * f; P.FootLY += dy * f; P.FootLRot += rot * f;
                    Foot(ph / Tau + .5, 8, 10, out dx, out dy, out rot); P.FootRX += dx * f; P.FootRY += dy * f; P.FootRRot += rot * f;
                    P.WingLA = Lerp(P.WingLA, 100 + Math.Sin(ph) * 18, e); P.WingRA = Lerp(P.WingRA, 70 - Math.Sin(ph) * 18, e);
                    P.Lean = F(P.Lean + Math.Sin(ph) * 3 * e); P.Look = Lerp(0, -.15, e); P.CardSwing = F(Math.Sin(ph) * 7);
                    break;
                }
                case Skit.Travel: // p34: 밀짚모자와 선글라스, 지도를 펼쳐 들고 가방을 끌고 여행
                {
                    double ph = t * 4.2;
                    SkitFace(P, EyeStyle.Dot, .12f * f);
                    P.Hat = true; P.Shades = true; P.GlassDrop = F(Ease(t / .3)); P.Prop = PropKind.Map;
                    P.WingLTo = Pt(Lerp(138, 158, e), Lerp(214, 200, e)); P.WingRTo = Pt(Lerp(262, 248, e), Lerp(214, 200, e));
                    float dx, dy, rot;
                    Foot(ph / Tau, 7, 8, out dx, out dy, out rot); P.FootLX += dx * f; P.FootLY += dy * f; P.FootLRot += rot * f;
                    Foot(ph / Tau + .5, 7, 8, out dx, out dy, out rot); P.FootRX += dx * f; P.FootRY += dy * f; P.FootRRot += rot * f;
                    P.Lean = F(P.Lean + Math.Sin(ph) * 2.5 * e); P.HeadTilt += F(Math.Sin(ph) * 2 * e);
                    break;
                }
                default: // Pat — p35 토닥토닥: 날개를 앞으로 내밀어 살살 토닥인다
                    SkitFace(P, EyeStyle.Happy, .1f * f);
                    P.WingRTo = Pt(Lerp(262, 238, e), F(Lerp(214, 176, e) + Math.Sin(t * 8) * 10 * e));
                    P.HeadTilt += F(6 * e); P.Blush = F(.35 * e); P.Hearts = F(t * .6);
                    P.Sfx = "토닥토닥"; P.SfxAt = Pt(300, 60); P.SfxSize = 22;
                    break;
            }
            // 처음엔 소품 없이 짧게 서 있다가 시작하도록 효과(글자·하트)는 동작이 본격적으로 나올 때만 보인다
            if (e < .12) P.Sfx = null;
            if (P.Prop != PropKind.None || P.Cape || P.Chair || P.Sfx != null || P.Hearts >= 0 || P.Stars >= 0 || P.Marks >= 0) L.Decor |= Decor.Prop;
        }

        /// <summary>AI 대화의 감정 → 가이드북 응용동작. 없으면 false.</summary>
        internal static bool MoodSkit(ChatMood m, out Skit s)
        {
            switch (m)
            {
                case ChatMood.Greet: s = Skit.Hello; return true;
                case ChatMood.Laugh: s = Skit.Laugh; return true;
                case ChatMood.Love: s = Skit.HeartHug; return true;
                case ChatMood.Hero: s = Skit.Hero; return true;
                case ChatMood.Cheer: s = Skit.Cheer; return true;
                case ChatMood.Proud: s = Skit.Proud; return true;
                case ChatMood.Explain: s = Skit.Present; return true;
                case ChatMood.Praise: s = Skit.Good; return true;
                case ChatMood.Okay: s = Skit.Okay; return true;
                case ChatMood.Busy: s = Skit.Busy; return true;
                case ChatMood.Busan: s = Skit.Busan; return true;
                case ChatMood.Travel: s = Skit.Travel; return true;
                case ChatMood.Comfort: s = Skit.Pat; return true;
            }
            s = Skit.Hello; return false;
        }
    }
}
