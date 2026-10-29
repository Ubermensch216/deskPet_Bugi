using System;
using System.Drawing;

namespace Bugi
{
    /// <summary>
    /// 상태마다 부기의 자세(Pose)를 만든다. 발은 움직인 거리에 맞춰 걷고(미끄러지지 않음),
    /// 눈 깜빡임·두리번·날개 파닥임·머리카락 흔들림·숨쉬기는 모든 자세에 겹쳐 흐른다.
    /// </summary>
    internal sealed partial class Pet
    {
        // ───────── 공통 도구 ─────────
        private static double Ease(double x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }
        private static float F(double v) { return (float)v; }
        private static double Tau { get { return Math.PI * 2; } }
        private static float Lerp(double a, double b, double t) { return (float)(a + (b - a) * t); }
        /// <summary>a~b 구간에서 1, 양끝 fade 초 동안 부드럽게 0 ↔ 1.</summary>
        private static double Window(double u, double a, double b, double fade)
        { return Ease((u - a) / fade) * (1 - Ease((u - (b - fade)) / fade)); }

        private double Hash01(double seg)
        { double v = Math.Sin(seg * 12.9898 + blinkOffset * 78.233) * 43758.5453; return v - Math.Floor(v); }

        // 걸음 위상(걸음 한 주기 = 발 두 번). 움직인 거리로 올려서 발이 땅에서 미끄러지지 않는다.
        private double gait, lastGaitX, lastGaitY;
        private bool gaitStarted;
        private int pauseAct;

        private void UpdateGait(double dt)
        {
            if (!gaitStarted) { lastGaitX = X; lastGaitY = Y; gaitStarted = true; }
            double moved = Math.Sqrt((X - lastGaitX) * (X - lastGaitX) + (Y - lastGaitY) * (Y - lastGaitY));
            lastGaitX = X; lastGaitY = Y;
            switch (State)
            {
                case PetState.Walk: case PetState.Gather: case PetState.Climb:
                case PetState.Wall: case PetState.WindowClimb: case PetState.Ceiling:
                    gait += Math.Min(moved, 130 * S * Math.Max(dt, .001)) / (40.0 * Art.K);
                    break;
                case PetState.Sliding:
                    gait += dt * 6.5;
                    break;
            }
        }

        /// <summary>눈 깜빡임 정도(1 = 활짝, 0 = 감음). 약 4초에 한 번, 가끔 두 번 연달아.</summary>
        private float BlinkOpen(double t)
        {
            double c = (t + blinkOffset) % 4.3;
            Func<double, double> shape = x => x < 0 ? 1 : x < .05 ? 1 - x / .05 : x < .09 ? 0 : x < .16 ? (x - .09) / .07 : 1;
            double o = shape(c);
            if (((int)((t + blinkOffset) / 4.3) % 3) == 0) o = Math.Min(o, shape(c - 1.1));
            return F(o);
        }

        /// <summary>짧게 파르르 떠는 움직임(주기마다 한 번, 0.4초).</summary>
        private float Twitch(double t, double period, double seed)
        {
            double c = (t + seed) % period;
            if (c > .4) return 0;
            return F(Math.Sin(c * 36) * (1 - c / .4) * 10);
        }

        /// <summary>두리번거리는 시선: 2.4초마다 새 곳을 보고, 고개가 빠르게 돌아간다.</summary>
        private void Wander(Pose P, double t, double range)
        {
            double u = t / 2.4, seg = Math.Floor(u), e = Ease((u - seg) / .12);
            Func<int, double, double, double> pick = (salt, lo, hi) =>
            {
                double a = lo + Hash01(seg - 1 + salt) * (hi - lo), b = lo + Hash01(seg + salt) * (hi - lo);
                return a + (b - a) * e;
            };
            P.Gx = F(pick(0, -1, 1) * range);
            P.Gy = F(pick(100, -.25, .6) * Math.Min(1, range + .3));
        }

        // ───────── 기본 서 있는 자세 ─────────
        private void IdleBase(Pose P, double t, bool lookAround)
        {
            P.Breath = F(Math.Sin(animT * 2.0));
            P.OpenL = P.OpenR = BlinkOpen(animT);
            if (lookAround) Wander(P, t, 1); else { P.Gy = .1f; P.Gx = 0; }
            P.Look = F(P.Gx * .55);
            P.HeadTilt = F(P.Gx * 2.2 + Math.Sin(t * .8) * 1.1);
            P.Tuft = F(Math.Sin(t * 1.3) * 3); P.TuftWave = 5; P.TuftPhase = F(t * 2.3);
            P.GlassTilt = F(Math.Sin(t * 1.3 + .5) * 1.5);
            // 짧은 날개는 쉬지 않고 팔랑거린다
            P.WingLA = F(46 + Math.Sin(t * 2.6 + .4) * 2.6 + Twitch(animT, 6.3, blinkOffset * 1.3) * 1.6);
            P.WingRA = F(46 + Math.Sin(t * 2.6 + 1.1) * 2.6 + Twitch(animT, 8.1, blinkOffset * 2.1 + 1) * 1.6);
            P.WingLB = F(42 + Math.Sin(t * 2.6) * 3.2); P.WingRB = F(42 + Math.Sin(t * 2.6 + .8) * 3.2);
            P.Tail = F(Math.Sin(t * .9) * 3); P.TailWag = F(Math.Sin(t * 1.9) * 6);
        }

        private static void SitDown(Pose P)
        {
            P.Sit = true; P.BodyY = 16; P.Squash = .03f;
            P.FootLX = -4; P.FootRX = 4; P.FootLRot = 0; P.FootRRot = 0;
        }

        // ───────── 걷기 ─────────
        /// <summary>발 한 쪽의 걸음: 땅에서 뒤로 밀고(60%) → 들어 앞으로 내딛는다(40%).</summary>
        private static void Foot(double phase, float S, float H, out float dx, out float dy, out float rot)
        {
            double th = phase - Math.Floor(phase), fwd, lift = 0, r;
            const double stance = .6;
            if (th < stance) { double u = th / stance; fwd = S * (1 - 2 * u); r = 6 - 16 * u; }
            else
            {
                double u = (th - stance) / (1 - stance), e = (1 - Math.Cos(Math.PI * u)) / 2;
                fwd = -S + 2 * S * e; lift = Math.Sin(Math.PI * u); r = -10 + 22 * Math.Sin(Math.PI * Math.Min(1, u * 1.15));
            }
            dx = F(-fwd);                // 그림은 왼쪽(−x)이 앞
            dy = F(-H * lift); rot = F(r);
        }

        /// <summary>뒤뚱뒤뚱: 몸이 디딘 발 쪽으로 기울고, 머리는 반대로 까딱이며, 날개는 번갈아 흔들린다.</summary>
        private void ApplyWalk(Pose P, double g, double ampD, float S, float H, float swing, float lean)
        {
            float amp = F(ampD); if (amp <= .005f) return;
            float dx, dy, rot;
            Foot(g, S, H, out dx, out dy, out rot); P.FootLX += dx * amp; P.FootLY += dy * amp; P.FootLRot += rot * amp;
            Foot(g + .5, S, H, out dx, out dy, out rot); P.FootRX += dx * amp; P.FootRY += dy * amp; P.FootRRot += rot * amp;
            double ph = g * Tau, sn = Math.Sin(ph);
            P.BodyY += F(-Math.Abs(sn) * 3.4) * amp;
            P.BodyX += F(sn * -2.2) * amp;
            P.Lean += (lean + F(sn * 5.5)) * amp;
            P.HeadTilt += F(-sn * 3.4) * amp;
            P.Squash += F(Math.Cos(2 * ph) * .012) * amp;
            P.WingLA += F(sn * swing * .3) * amp; P.WingRA -= F(sn * swing * .3) * amp;
            P.WingLSweep += F(sn * .85) * amp; P.WingRSweep -= F(sn * .85) * amp;   // 옆에서 보면 팔이 앞뒤로 엇갈려 흔들린다
            P.WingLB += F(Math.Sin(ph - .8) * swing * 1.1) * amp; P.WingRB -= F(Math.Sin(ph - .8) * swing * 1.1) * amp;
            P.Tuft += F(Math.Sin(2 * ph - 1.0) * 7) * amp; P.TuftWave += 5 * amp; P.TuftPhase = F(ph * 2);
            P.TailWag += F(sn * 10) * amp;
            P.GlassTilt += F(Math.Sin(2 * ph) * 2.5) * amp;
            P.CardSwing += F(sn * 7) * amp;
            P.Turn = Lerp(P.Turn, -.6, amp); P.Look = Lerp(P.Look, 0, amp); P.Gx = Lerp(P.Gx, -.2, amp);
        }

        // ───────── 걸으며 하는 잔동작 (멈춰 섰을 때) ─────────
        private int ChoosePauseAct(IWorld w)
        {
            double r = w.Rng.NextDouble();
            return r < .28 ? 0 : r < .40 ? 1 : r < .52 ? 2 : r < .63 ? 3 : r < .71 ? 4 : r < .80 ? 5 : r < .90 ? 6 : 7;
        }

        /// <summary>멈춰 서 있는 동안 8초마다 한 번씩 이어지는 잔동작(처음 한 번은 상태가 시작될 때 뽑은 것).</summary>
        private void PauseFidget(Pose P, double stateT)
        {
            int cyc = (int)(stateT / 8.0);
            double u = stateT - cyc * 8.0;
            int act = cyc == 0 ? pauseAct : (int)Math.Floor(Hash01(cyc * 3.7 + 11) * 8) % 8;
            Fidget(P, act, u);
        }

        private void Fidget(Pose P, int act, double u)
        {
            double e = Window(u, 0, 2.6, .35);
            if (e <= .001 || act == 0) return;
            float ef = F(e);
            switch (act)
            {
                case 1: // 머리 긁적(참조 그림 3 오른쪽): 날개를 머리 옆으로 올려 쓱쓱, 눈은 지그시
                    P.WingRTo = new PointF(Lerp(262, 254 + Math.Sin(u * 20) * 3.5 * e, e), Lerp(214, 126 + Math.Cos(u * 20) * 2 * e, e));
                    P.HeadTilt += 9 * ef; P.Look = Lerp(P.Look, .1, e); P.Tuft += F(Math.Sin(u * 20) * 9 * e);
                    if (e > .45) { P.Eyes = EyeStyle.Happy; P.OpenL = P.OpenR = 1; }
                    P.Blush = .35f * ef; P.Beak = F(.1 * e);
                    break;
                case 2: // 팔짱(참조 그림 3 왼쪽): 반쯤 감은 눈으로 으쓱, 발끝을 톡톡
                    P.WingLTo = new PointF(Lerp(138, 214, e), Lerp(214, 168, e));
                    P.WingRTo = new PointF(Lerp(262, 186, e), Lerp(214, 174, e));
                    if (e > .4) { P.Eyes = EyeStyle.Half; P.Brow = .3f * ef; }
                    P.HeadTilt -= 3 * ef; P.Look = Lerp(P.Look, 0, e); P.Gx = 0;
                    P.FootRY = F(-4 * Math.Max(0, Math.Sin(u * 7)) * e); P.FootRRot = F(8 * Math.Max(0, Math.Sin(u * 7)) * e);
                    break;
                case 3: // 스마트 안경 내려쓰기: 고개를 숙여 안경을 눈 위로 툭, 렌즈가 빛나며 화면이 뜬다
                {
                    double drop = u < .3 ? 0 : u < .5 ? Ease((u - .3) / .2) : u < 2.0 ? 1 : u < 2.3 ? 1 - Ease((u - 2.0) / .3) : 0;
                    double hold = Window(u, .35, 2.3, .3);
                    P.GlassDrop = F(drop); P.GlassGlow = F(hold * (.65 + .35 * Math.Sin(u * 9)));
                    if (hold > .01 && u > .65) P.Holo = F(u);
                    P.HeadY += F(5 * Window(u, 0, .6, .25) - 1.5 * hold); P.Squash += F(.035 * Window(u, 0, .6, .25));
                    P.Look = Lerp(P.Look, .55, hold); P.Gx = Lerp(P.Gx, .8, hold);
                    P.WingRA = Lerp(P.WingRA, 128, hold); P.WingRB = Lerp(P.WingRB, 112, hold);
                    P.Brow = F(-.1 * hold);
                    break;
                }
                case 4: // 몸 털기: 몸통이 좌우로 부르르, 머리카락이 휙휙
                {
                    double dec = Math.Max(0, 1 - u / 1.5), s = Math.Sin(u * 36);
                    P.Lean += F(s * 9 * dec); P.HeadTilt += F(-s * 5 * dec); P.Tuft += F(Math.Sin(u * 36 + 1) * 24 * dec);
                    P.WingLA += F(46 * dec * Math.Abs(s)); P.WingRA += F(46 * dec * Math.Abs(s));
                    if (dec > .05) { P.Eyes = EyeStyle.Squint; P.OpenL = P.OpenR = 1; P.Beak = .15f; P.GlassTilt += F(s * 7 * dec); }
                    break;
                }
                case 5: // 고개 까딱까딱(쪼기): 두 번
                {
                    double pk = Math.Pow(Math.Max(0, Math.Sin(Math.PI * u / 1.2)), 2) * (u < 2.4 ? 1 : 0);
                    P.Lean += F(-16 * pk); P.HeadTilt += F(-10 * pk); P.Squash += F(.03 * pk); P.Beak = F(.22 * pk);
                    P.WingLA += F(26 * pk); P.WingRA += F(26 * pk); P.Look = Lerp(P.Look, -.4, e);
                    break;
                }
                case 6: // 날개 쭉 펴고 파닥파닥
                {
                    double fl = .5 + .5 * Math.Sin(u * 12);
                    P.WingLA = F(46 + 85 * e * fl); P.WingRA = P.WingLA; P.WingLB = F(42 + 90 * e * fl); P.WingRB = P.WingLB;
                    P.Squash += F(-.03 * e); P.FootLY = P.FootRY = F(-4 * Math.Max(0, Math.Sin(u * 12)) * e);
                    P.Eyes = EyeStyle.Happy; P.OpenL = P.OpenR = 1; P.Beak = F(.25 * e); P.Tuft += F(-10 * fl * e); P.Look = Lerp(P.Look, 0, e);
                    break;
                }
                case 7: // 안녕(가이드북 p27): 날개를 번쩍 들어 흔들고 한쪽 다리를 차올린다
                    P.WingLA = Lerp(P.WingLA, 150, e); P.WingLB = Lerp(P.WingLB, 128 + Math.Sin(u * 12) * 30, e);
                    P.Eyes = EyeStyle.Dot; P.OpenL = P.OpenR = 1; P.FootRY = F(-15 * e); P.FootRX = F(-6 * e); P.FootRRot = F(-38 * e);
                    P.Beak = F(.18 * (.5 + .5 * Math.Sin(u * 8)) * e); P.Look = Lerp(P.Look, .2, e); P.HeadTilt += 5 * ef; P.Lean += 4 * ef;
                    P.Sfx = e > .3 ? "안녕" : null; P.SfxAt = new PointF(92, 66); P.SfxSize = 24; P.SfxT = F(u);
                    break;
            }
        }

        // ───────── 상태별 자세 ─────────
        private void SquishPose(Pose P, ref Look L)
        {
            double k = Math.Sin(Math.PI * Math.Min(1, StateT / squishDur));
            bool poke = squishAmp > .2;        // 클릭해서 눌린 것(착지·부딪힘보다 크게 눌림)
            if (poke)
            {
                // 콕 찔려 발끈: 몸을 부풀리고 날개를 뻣뻣하게 펴서 쿵쿵 구르며 꽥! + 💢
                IdleBase(P, animT, false);
                double stomp = Math.Sin(StateT * 30);
                P.Squash = F(.05 * k - .02); P.Lean = F(Math.Sin(StateT * 26) * 3 * k);
                P.WingLA = F(28 * k + 12); P.WingRA = F(28 * k + 12); P.WingLB = F(2); P.WingRB = F(2);
                P.Eyes = EyeStyle.Dot; P.Brow = F(.95 * Math.Min(1, k * 2.2)); P.PupilR = F(.85 - .15 * k); P.Gy = 0; P.Gx = 0; P.Look = 0;
                P.Beak = F(.55 * Math.Min(1, k * 2)); P.BeakShake = F(Math.Sin(StateT * 60) * 1.4 * k);
                P.Anger = F(Math.Min(1, StateT * 3 + .15)) * (k > .05 ? 1f : 0f);
                P.FootLY = F(Math.Max(0, stomp) * -6 * k); P.FootRY = F(Math.Max(0, -stomp) * -6 * k);
                P.Tuft = F(Math.Sin(StateT * 40) * 10 * k); P.TuftWave = 12; P.TuftPhase = F(animT * 26);
                P.GlassTilt = F(Math.Sin(StateT * 40) * 4 * k);
                lookJitterX = Math.Sin(StateT * 70) * .5 * S * k;
                return;
            }
            // 착지·부딪힘: 무릎을 굽히듯 납작해지며 날개로 균형을 잡는다
            IdleBase(P, animT, false);
            P.Squash = F(.17 * k * Math.Min(1.6, squishAmp / .15));
            P.WingLA = F(46 + 36 * k); P.WingRA = F(46 + 36 * k); P.WingLB = F(42 + 36 * k); P.WingRB = F(42 + 36 * k);
            P.Eyes = k > .35 ? EyeStyle.Squint : EyeStyle.Dot; P.OpenL = P.OpenR = 1;
            P.Tuft = F(-12 * k); P.GlassTilt = F(-5 * k);
            P.Beak = k > .4 ? .12f : 0f; P.Look = 0;
        }

        private void ThrownPose(Pose P)
        {
            // 날개를 쫙 펴고 허우적: 눈이 휘둥그레지고 꽥 소리 지르며 날아간다
            double sh = Math.Sin(StateT * 22);
            P.Breath = 0; P.Eyes = EyeStyle.Wide; P.PupilR = .75f; P.Gy = -.3f; P.Gx = 0; P.Look = -.15f;
            P.WingLA = F(118 + sh * 14); P.WingRA = F(118 - sh * 14); P.WingLB = F(132 + sh * 12); P.WingRB = F(132 - sh * 12);
            P.FootLX = -6; P.FootRX = 6; P.FootLY = -10; P.FootRY = -8; P.FootLRot = -22; P.FootRRot = -22;
            P.Beak = F(.7 + sh * .12); P.BeakShake = F(sh * .8);
            P.Tuft = F(-22 + sh * 6); P.TuftWave = 12; P.TuftPhase = F(StateT * 24);
            P.GlassTilt = F(sh * 5); P.Tail = -10; P.TailWag = F(sh * 12);
            P.Brow = -.3f; P.Sweat = F(StateT);
        }

        private void FallingPose(Pose P)
        {
            // 짧은 날개를 필사적으로 퍼덕이며 천천히 내려온다(갈매기 체면!): 발은 달랑달랑
            double t = StateT, fl = Math.Sin(t * 27), sway = Math.Sin(t * 2.4);
            P.Breath = F(Math.Sin(t * 3));
            P.OpenL = P.OpenR = BlinkOpen(t); P.Eyes = EyeStyle.Dot; P.Gy = -.7f; P.Gx = F(Math.Sin(t * 1.3) * .25); P.Look = F(Math.Sin(t * 1.1) * .25);
            P.PupilR = .9f;
            float a = F(78 + 50 * fl);
            P.WingLA = P.WingRA = a; P.WingLB = P.WingRB = F(a * .95 + 10);
            P.Squash = F(-.012 + .02 * fl);
            P.FootLX = F(-5 - sway * 3); P.FootRX = F(5 - sway * 3); P.FootLY = F(-6 + Math.Sin(t * 3.1) * 3); P.FootRY = F(-6 + Math.Sin(t * 3.1 + 1.3) * 3);
            P.FootLRot = F(-16 + sway * 6); P.FootRRot = F(-16 - sway * 6);
            P.Tuft = F(-16 + sway * 4); P.TuftWave = 10; P.TuftPhase = F(t * 14); P.GlassTilt = F(-4 + fl * 2);
            P.Beak = .3f; P.Tail = -6; P.TailWag = F(fl * 8); P.HeadTilt = F(sway * 2);
            P.Brow = -.25f;
            if (IsPilotFlight)
            {
                // 비밀 조종사: 안경을 눈 위로 내려쓰고 날개를 곧게 펴서 활강, 씩 웃는다
                P.GlassDrop = 1; P.GlassGlow = .5f; P.Eyes = EyeStyle.Dot; P.Brow = 0; P.Gy = -.2f; P.Gx = 0; P.Look = 0;
                P.WingLA = P.WingRA = F(104 + fl * 6); P.WingLB = P.WingRB = F(100 + fl * 6);
                P.Beak = .35f; P.Tuft = -26; P.TuftWave = 16; P.Squash = -.02f; P.Blush = .2f;
            }
        }

        private void DragPose(Pose P)
        {
            // 들려 올라가서 허우적: 흔들리는 방향의 반대로 몸이 늘어지고 발을 버둥거리며 꽥꽥
            double sw = Math.Max(-1, Math.Min(1, angle / 30)), t = animT;
            double kick = Math.Sin(t * 7.5), flap = Math.Sin(t * 19);
            P.Breath = F(Math.Sin(t * 5));
            P.OpenL = P.OpenR = BlinkOpen(t); P.Eyes = EyeStyle.Wide; P.PupilR = .8f; P.Gy = -.35f; P.Gx = F(-sw * .6); P.Look = F(-sw * .3);
            P.Squash = -.04f;
            P.WingLA = F(100 + flap * 20 + sw * 12); P.WingRA = F(100 - flap * 20 - sw * 12);
            P.WingLB = F(110 + flap * 18); P.WingRB = F(110 - flap * 18);
            P.FootLX = F(-4 + kick * 7 - sw * 3); P.FootRX = F(4 - kick * 7 - sw * 3);
            P.FootLY = F(-12 - kick * 3); P.FootRY = F(-12 + kick * 3); P.FootLRot = F(-18 + kick * 8); P.FootRRot = F(-18 - kick * 8);
            P.Beak = F(.55 + .3 * Math.Sin(t * 9)); P.BeakShake = F(Math.Sin(t * 45) * .7);
            P.Tuft = F(-sw * 24 - 8); P.TuftWave = 12; P.TuftPhase = F(t * 8); P.GlassTilt = F(-sw * 8);
            P.Tail = F(sw * 20); P.TailWag = F(kick * 10);
            P.Sweat = F(t); P.Brow = -.25f;
        }

        private void SleepPose(Pose P, double t)
        {
            // 앉아서 꾸벅꾸벅(참조 그림 2 오른쪽): 머리가 서서히 처지다가 화들짝 튕겨 올라가며 잔상이 남는다
            SitDown(P);
            double cyc = (t % 3.4) / 3.4, x;
            if (cyc < .72) x = Ease(cyc / .72);
            else if (cyc < .80) x = 1 - 1.25 * Ease((cyc - .72) / .08);
            else x = -.25 + .25 * Ease((cyc - .80) / .2);
            P.Eyes = EyeStyle.Closed; P.OpenL = P.OpenR = 0;
            P.Breath = F(Math.Sin(t * 1.7) * 1.4);
            P.HeadTilt = F(13 * x - 2); P.HeadY = F(3 * x); P.Lean = F(x * 2.5); P.Look = F(-.12 + x * .06);
            P.Ghost = cyc >= .70 && cyc < 1 ? F(Math.Max(0, 1 - (cyc - .70) / .3)) : 0f; P.GhostAng = 13;
            P.Beak = 0; P.Blush = .4f; P.Gy = 0;
            P.Tuft = F(x * 5); P.TuftWave = 2; P.TuftPhase = F(t * 1.2); P.GlassTilt = F(x * 6 - 1);
            P.WingLA = F(46 + Math.Sin(t * 1.7) * 1.5); P.WingRA = F(46 + Math.Sin(t * 1.7) * 1.5); P.WingLB = 30; P.WingRB = 30;
            P.TailWag = F(Math.Sin(t * 1.7) * 2);
        }

        private void LiePose(Pose P, double t)
        {
            // 옆으로 누워 쉬기(참조 그림 1): 눈을 지그시 감고 숨을 크게, 발끝은 위로
            P.Eyes = EyeStyle.Closed; P.OpenL = P.OpenR = 0; P.Turn = -.45f;
            P.Breath = F(Math.Sin(t * 1.5) * 1.5); P.Beak = 0; P.Blush = .45f;
            P.HeadTilt = F(Math.Sin(t * .7) * 2.5); P.Tuft = F(Math.Sin(t * 1.1) * 3); P.TuftWave = 3; P.TuftPhase = F(t * 1.3);
            P.WingLA = F(34 + Math.Sin(t * 1.5) * 3); P.WingLB = 26; P.WingRA = F(60 + Math.Sin(t * 1.5 + 1) * 4); P.WingRB = 40;
            P.FootLY = -3; P.FootRY = -6; P.FootLRot = F(10 + Math.Sin(t * .9) * 6); P.FootRRot = F(14 - Math.Sin(t * .9) * 6);
            P.TailWag = F(Math.Sin(t * 1.5) * 3);
        }

        private void StretchPose(Pose P, double q)
        {
            // q: 0..1..0 로 폈다가 접는 정도. 날개를 쭉 올리고 하품하며 몸을 뒤로 젖힌다.
            IdleBase(P, animT, false);
            P.Eyes = q > .25 ? EyeStyle.Squint : EyeStyle.Dot; P.OpenL = P.OpenR = 1;
            float qf = F(q);
            P.WingLA = F(46 + 120 * q); P.WingRA = F(46 + 120 * q); P.WingLB = F(42 + 130 * q); P.WingRB = F(42 + 130 * q);
            P.Squash = -.06f * qf; P.Lean = 5 * qf; P.HeadTilt = 10 * qf; P.Look = 0;
            P.FootLY = P.FootRY = -3 * qf; P.FootLRot = P.FootRRot = -16 * qf;
            P.Beak = F(Math.Max(0, q - .15) * 1.1); P.Tuft = -10 * qf; P.TailWag = -8 * qf; P.Tail = -14 * qf;
            if (q > .6) { P.WingLA += F(Math.Sin(animT * 30) * 2.5); P.WingRA -= F(Math.Sin(animT * 30) * 2.5); P.BodyX = F(Math.Sin(animT * 34) * .8); }
        }

        private void DaydreamPose(Pose P, double t)
        {
            // 멍...(가이드북 p32): 퍼질러 앉아 날개를 늘어뜨리고 옆을 멍하니 바라본다. 눈은 점 그대로, 입은 다문다.
            SitDown(P);
            P.Breath = F(Math.Sin(t * 1.1) * .6);
            P.OpenL = P.OpenR = BlinkOpen(t * .6); P.Eyes = EyeStyle.Dot;
            P.Gx = F(-.75 + Math.Sin(t * .45) * .12); P.Gy = F(.05 + Math.Sin(t * .31) * .1); P.Look = F(-.28 + Math.Sin(t * .45) * .05);
            P.HeadTilt = F(-5 + Math.Sin(t * .7) * 1.5); P.HeadY = -1;
            P.Beak = 0; P.Tuft = F(Math.Sin(t * .9) * 3); P.TuftWave = 3; P.TuftPhase = F(t * 1.4);
            P.WingLA = F(26 + Math.Sin(t * .6) * 2); P.WingRA = F(26 - Math.Sin(t * .6) * 2); P.WingLB = 12; P.WingRB = 12;
            P.Sfx = "멍..."; P.SfxAt = new PointF(212, -4); P.SfxSize = 22; P.SfxT = F(t * .5);
            P.TailWag = F(Math.Sin(t * .8) * 3);
        }

        private void SitIdlePose(Pose P, double t)
        {
            // 앉아서 한숨 돌리기(참조 그림 2 왼쪽): 볼이 발그레하고, 가끔 후~ 한숨, 한 번씩 크게 하품
            IdleBase(P, t, true);
            SitDown(P);
            P.Blush = .4f; P.Squash = .03f;
            P.WingLA = F(42 + Math.Sin(t * 2.0) * 1.8); P.WingRA = F(42 + Math.Sin(t * 2.0 + .3) * 1.8); P.WingLB = 32; P.WingRB = 32;
            P.Tuft = F(Math.Sin(t * 1.1) * 3);
            double pc = (animT + blinkOffset * 2) % 9.0;
            if (pc < 1.5) { P.Puff = F(pc / 1.5); P.Beak = F(.22 * Math.Sin(Math.PI * Math.Min(1, pc / .8))); P.Eyes = pc > .2 ? EyeStyle.Closed : P.Eyes; }
            double yc = (animT + blinkOffset * 2 + 4.5) % 13.0;
            if (yc < 1.8)
            {
                double q = Math.Sin(Math.PI * yc / 1.8);
                P.Beak = F(q * .95); P.Eyes = q > .4 ? EyeStyle.Squint : P.Eyes; P.OpenL = P.OpenR = 1;
                P.HeadTilt = F(P.HeadTilt - 4 * q); P.WingLA += F(24 * q); P.WingRA += F(24 * q); P.Squash -= F(.02 * q);
            }
        }

        private void EatPose(Pose P)
        {
            SitDown(P);
            double chew = Math.Sin(animT * 2.4 * Tau), prog = Math.Min(1, StateT / Math.Max(1, extraDuration));
            P.Breath = F(chew * .3);
            P.Eyes = EyeStyle.Happy; P.OpenL = P.OpenR = 1; P.Beak = F(.3 * Math.Max(0, chew) + .05);
            P.HeadY = F(1 + chew * 1.4); P.HeadTilt = F(chew * 1.6); P.Look = 0;
            P.WingLTo = new PointF(178, 152); P.WingRTo = new PointF(222, 152);
            P.Cookie = F(prog); P.Blush = .7f;
            P.Tuft = F(chew * 3); P.TuftWave = 5; P.TuftPhase = F(animT * 6); P.TailWag = F(Math.Sin(animT * 3.4) * 9);
        }

        private void HungryCryPose(Pose P, double t)
        {
            SitDown(P);
            double sob = Math.Sin(t * 9);
            P.Breath = F(Math.Sin(t * 5) * .8);
            P.Eyes = EyeStyle.Squint; P.OpenL = P.OpenR = 1; P.Look = 0;
            P.Beak = F(.3 + .2 * Math.Abs(sob)); P.BeakShake = F(Math.Sin(t * 40) * 1.1); P.Brow = 0;
            P.HeadTilt = F(-4 + sob * 1.4); P.HeadY = F(3 + Math.Abs(sob) * 1.5);
            // 날개로 눈을 비빈다
            double rub = Math.Sin(t * 7);
            P.WingLTo = new PointF(F(176 + rub * 3), F(118 + Math.Abs(rub) * 3)); P.WingRTo = new PointF(F(224 - rub * 3), F(118 + Math.Abs(rub) * 3));
            P.Tears = F(t); P.Tuft = F(-sob * 3); P.TuftWave = 3; P.TuftPhase = F(t * 4);
            lookJitterX = Math.Sin(t * 21) * .45 * S;
        }

        private void TouchCryPose(Pose P)
        {
            // 놀라 눈이 휘둥그레지고 날개를 가슴에 모아 달달 떤다. 부리가 덜덜.
            double t = StateT, shiver = Math.Sin(t * 52);
            P.Breath = F(Math.Sin(t * 14) * .8);
            P.Eyes = EyeStyle.Wide; P.OpenL = P.OpenR = 1; P.EyeScale = 1.05f; P.PupilR = .65f; P.Gy = -.7f; P.Gx = F(Math.Sin(t * 3.1) * .4); P.Look = F(Math.Sin(t * 3.1) * .2);
            P.Brow = -.8f; P.Beak = F(.3 + .3 * Math.Abs(Math.Sin(t * 18))); P.BeakShake = F(shiver * 1.2);
            P.WingLTo = new PointF(204, 150); P.WingRTo = new PointF(196, 154);
            P.HeadTilt = F(-3 + Math.Sin(t * 9) * 1.6); P.Squash = F(.05 + Math.Abs(shiver) * .006); P.BodyX = F(shiver * .8);
            P.FootLRot = P.FootRRot = 6; P.FootLX = 3; P.FootRX = -3;
            P.Tears = F(t); P.Sweat = F(t);
            P.Tuft = F(shiver * 4); P.TuftWave = 8; P.TuftPhase = F(t * 30); P.GlassTilt = F(shiver * 3);
            P.TailWag = F(shiver * 6);
            if (t < .18) { double q = Math.Sin(t / .18 * Math.PI); P.BodyY += F(-7 * q); P.EyeScale = F(1.05 + .1 * q); P.Squash = F(-.04 * q); }
        }

        private void HopPose(Pose P)
        {
            double t = StateT / extraDuration;
            IdleBase(P, animT, false);
            P.Eyes = EyeStyle.Happy; P.OpenL = P.OpenR = 1; P.Beak = .15f; P.Look = 0; P.Blush = .35f;
            if (t < .16)
            {
                double q = Math.Sin(t / .16 * Math.PI);                     // 웅크리기
                P.Squash = F(.16 * q); P.WingLA = F(46 - 16 * q); P.WingRA = F(46 - 16 * q); P.WingLB = 24; P.WingRB = 24;
                P.Eyes = EyeStyle.Dot; P.Beak = 0; P.Brow = F(.3 * q);
            }
            else if (t < .84)
            {
                double q = (t - .16) / .68, up = Math.Sin(q * Math.PI);     // 공중: 날개를 번쩍
                P.WingLA = F(46 + 110 * up); P.WingRA = F(46 + 110 * up); P.WingLB = F(42 + 110 * up); P.WingRB = F(42 + 110 * up);
                P.Squash = F(-.05 * up); P.FootLY = F(-9 * up); P.FootRY = F(-9 * up); P.FootLRot = P.FootRRot = F(-20 * up); P.FootLX = F(-4 * up); P.FootRX = F(4 * up);
                P.Beak = F(.7 * up); P.Tuft = F(-20 * up); P.TuftWave = 10; P.TuftPhase = F(q * 30);
                P.Lean = F(-3 * Math.Sin(q * Math.PI * 2)); P.GlassTilt = F(-6 * up);
            }
            else
            {
                double q = Math.Sin((t - .84) / .16 * Math.PI);
                P.Squash = F(.14 * q); P.WingLA = F(46 + 30 * q); P.WingRA = F(46 + 30 * q);
            }
        }

        private void CheerPose(Pose P, double now)
        {
            IdleBase(P, animT, false);
            P.Eyes = EyeStyle.Happy; P.OpenL = P.OpenR = 1; P.Blush = .5f; P.Beak = .3f; P.Look = 0;
            if (now < cheerHopUntil)
            {
                double q = StateT % .42 / .42, up = Math.Sin(Math.PI * q);
                P.WingLA = F(46 + 100 * up); P.WingRA = F(46 + 100 * up); P.WingLB = F(42 + 100 * up); P.WingRB = F(42 + 100 * up);
                P.FootLY = F(-8 * up); P.FootRY = F(-8 * up); P.FootLRot = P.FootRRot = F(-14 * up);
                P.Squash = F(q < .12 ? .1 * (1 - q / .12) : -.03 * up);
                P.Tuft = F(-14 * up); P.TuftWave = 8; P.TuftPhase = F(StateT * 20); P.Lean = F(Math.Sin(q * Tau) * 2.5);
            }
            else { P.WingLA = 118; P.WingRA = 118; P.WingLB = 140; P.WingRB = 140; P.Beak = F(.2 + .2 * Math.Abs(Math.Sin(animT * 6))); }
        }

        private void DashPose(Pose P)
        {
            // 쌩~ 도망(참조 그림 5): 눈을 질끈(><) 감고 몸을 앞으로 푹 숙여 날개를 뒤로 젖히고 달린다
            double g = gait, ph = g * Tau, t = animT;
            P.Breath = 0; P.Turn = -.62f; P.Look = -.08f;
            P.Lean = F(-17 + Math.Sin(ph) * 1.8); P.BodyY = F(-Math.Abs(Math.Sin(ph)) * 4 - 3); P.Squash = F(.02 + Math.Cos(2 * ph) * .01);
            P.HeadTilt = F(-4); P.HeadX = -3;
            float dx, dy, rot;
            Foot(g, 34, 26, out dx, out dy, out rot); P.FootLX = dx; P.FootLY = dy; P.FootLRot = rot * 1.3f;
            Foot(g + .5, 34, 26, out dx, out dy, out rot); P.FootRX = dx; P.FootRY = dy; P.FootRRot = rot * 1.3f;
            P.WingLA = F(100 + Math.Sin(ph) * 10); P.WingRA = F(100 - Math.Sin(ph) * 10); P.WingLB = F(112 + Math.Sin(ph - .6) * 12); P.WingRB = F(112 - Math.Sin(ph - .6) * 12);
            P.WingLSweep = F(.95 + Math.Sin(ph) * .08); P.WingRSweep = F(.95 - Math.Sin(ph) * .08);   // 날개를 뒤로 젖히고 달린다
            P.Eyes = EyeStyle.Squint; P.OpenL = P.OpenR = 1; P.Beak = F(.3 + Math.Sin(ph * 2) * .06); P.BeakShake = F(Math.Sin(ph * 3) * .5);
            P.Tuft = F(34 + Math.Sin(ph * 2) * 5); P.TuftWave = 10; P.TuftPhase = F(ph * 3); P.GlassTilt = F(22 + Math.Sin(ph * 2) * 3);
            P.Tail = F(-18); P.TailWag = F(Math.Sin(ph - 1) * 8); P.Sweat = F(t);
        }

        private void ClimbBody(Pose P, bool descending)
        {
            // 벽·천장·창을 타는 자세: 발을 대고 날개를 번갈아 뻗는다(몸 전체를 눕히는 회전은 Look 이 담당)
            IdleBase(P, animT, false);
            P.Gx = -.3f; P.Gy = descending ? .5f : -.5f; P.Eyes = EyeStyle.Dot; P.PupilR = .9f;
            ApplyWalk(P, gait, 1f, 12, 12, 0, 0);
            double ph = ClimbPhase;
            P.WingLA = F(128 + Math.Sin(ph) * 36); P.WingRA = F(128 - Math.Sin(ph) * 36);
            P.WingLB = F(140 + Math.Sin(ph - .6) * 30); P.WingRB = F(140 - Math.Sin(ph - .6) * 30);
            P.WingLSweep = F(-.55 + Math.Sin(ph) * .3); P.WingRSweep = F(-.55 - Math.Sin(ph) * .3);
            P.Turn = -.8f; P.Look = 0; P.Beak = F(.1 + .08 * Math.Max(0, Math.Sin(ph * 2)));
            P.Brow = descending ? 0 : .25f; P.Tuft = F(P.Tuft + 10);
            P.Sweat = descending ? -1 : F(animT);
        }

        private void DizzyPose(Pose P)
        {
            double t = StateT, sw = Math.Sin(t * 5.2);
            P.Eyes = EyeStyle.Spiral; P.SpiralT = F(t * 11);
            P.Breath = F(Math.Sin(t * 4));
            P.BodyX = F(sw * 7); P.Lean = F(sw * 9); P.HeadTilt = F(-sw * 7 + Math.Sin(t * 3) * 3); P.HeadX = F(sw * 3); P.Look = F(sw * .35);
            P.WingLA = F(86 + Math.Sin(t * 5.2 + 1) * 30); P.WingRA = F(86 + Math.Sin(t * 5.2 + 3.1) * 30);
            P.WingLB = F(100 + Math.Sin(t * 5.2) * 24); P.WingRB = F(100 - Math.Sin(t * 5.2) * 24);
            float dx, dy, rot;
            Foot(t * 1.3, 10, 7, out dx, out dy, out rot); P.FootLX = dx; P.FootLY = dy; P.FootLRot = rot * .5f;
            Foot(t * 1.3 + .5, 10, 7, out dx, out dy, out rot); P.FootRX = dx; P.FootRY = dy; P.FootRRot = rot * .5f;
            P.Beak = F(.3 + .25 * Math.Sin(t * 6)); P.BeakShake = F(sw * .6);
            P.Tuft = F(-sw * 18); P.TuftWave = 14; P.TuftPhase = F(t * 8); P.GlassTilt = F(sw * 14);
            P.TailWag = F(-sw * 14);
        }

        private void DancePose(Pose P, double phase)
        {
            double hop = Math.Abs(Math.Sin(phase * Math.PI * 2.5)), sw = Math.Sin(phase * Math.PI * 2.5);
            IdleBase(P, animT, false);
            P.Eyes = EyeStyle.Happy; P.OpenL = P.OpenR = 1; P.Beak = F(.4 + .2 * hop); P.Blush = .6f; P.Look = F(sw * .3);
            bool leftUp = ((int)(phase / .3)) % 2 == 0;
            P.WingLA = leftUp ? 150f : 30f; P.WingRA = leftUp ? 30f : 150f; P.WingLB = leftUp ? 160f : 14f; P.WingRB = leftUp ? 14f : 160f;
            P.Lean = F(sw * 7); P.HeadTilt = F(sw * 8); P.BodyY = F(-hop * 2); P.Squash = F(.04 - hop * .06);
            P.FootLY = F(-hop * 9 * (leftUp ? 1 : .3)); P.FootRY = F(-hop * 9 * (leftUp ? .3 : 1)); P.FootLRot = P.FootRRot = F(-10 * hop);
            P.Tuft = F(sw * 16); P.TuftWave = 12; P.TuftPhase = F(phase * 14); P.GlassTilt = F(sw * 6);
            P.TailWag = F(sw * 18);
        }

        private void FlowerPose(Pose P, double now)
        {
            double t = now - flowerStarted;
            IdleBase(P, animT, false);
            P.Eyes = EyeStyle.Happy; P.OpenL = P.OpenR = 1; P.Beak = .12f; P.Blush = .85f; P.Look = 0;
            P.WingLTo = new PointF(186, 192); P.WingRTo = new PointF(214, 192);
            P.HeadTilt = F(Math.Sin(t * 2.2) * 3.5); P.BodyY = F(-Math.Abs(Math.Sin(t * 3.2)) * 2.5);
            P.Tuft = F(Math.Sin(t * 3.2) * 6); P.TailWag = F(Math.Sin(t * 4) * 12);
        }

        private void WaitPose(Pose P)
        {
            // 퇴근 시간을 기다리며 두리번, 발끝으로 톡톡. 가끔 스마트 안경으로 시간을 확인한다.
            IdleBase(P, animT, true);
            double tap = Math.Max(0, Math.Sin(animT * 6.5)) * (Math.Sin(animT * .9) > 0 ? 1 : 0);
            P.FootRY = F(-4 * tap); P.FootRRot = F(10 * tap);
            double c = (animT + blinkOffset) % 7.0;
            if (c < 2.2) { Fidget(P, 3, c + .0); }
        }

        // ───────── 곡괭이질(참조 그림 4): 번쩍 들어 쾅! 돌가루가 튄다 ─────────
        private void MinePose(Pose P, double t)
        {
            const double period = 1.7;
            double u = t % period;
            IdleBase(P, animT, false);
            P.Tool = ToolKind.Pickaxe; P.Turn = -.5f; P.Look = -.1f; P.Eyes = EyeStyle.Dot; P.Brow = .35f; P.OpenL = P.OpenR = 1;
            PointF ready = new PointF(262, 206), top = new PointF(254, 124), strike = new PointF(242, 212);
            double raiseK = Ease(u / .72), hit = Ease((u - .72) / .1), back = Ease((u - .9) / .8);
            float wx, wy, ang;
            float sweep;
            // 곡괭이는 뒤로 번쩍 들었다가(+) 머리 위를 지나 앞(바라보는 쪽, -)으로 내리친다
            if (u < .72) { wx = Lerp(ready.X, top.X, raiseK); wy = Lerp(ready.Y, top.Y, raiseK); ang = Lerp(20, 42, raiseK); sweep = Lerp(.1, .7, raiseK); }
            else if (u < .9) { wx = Lerp(top.X, strike.X, hit); wy = Lerp(top.Y, strike.Y, hit); ang = Lerp(42, -158, hit); sweep = Lerp(.7, -.9, hit); }
            else { wx = Lerp(strike.X, ready.X, back); wy = Lerp(strike.Y, ready.Y, back); ang = Lerp(-158, 20, back); sweep = Lerp(-.9, .1, back); }
            P.WingRTo = new PointF(wx, wy); P.ToolAng = ang; P.WingRSweep = sweep;
            double wind = u < .72 ? raiseK : (u < .9 ? 1 - hit : 0), impact = u >= .8 && u < .9 ? 1 : 0;
            P.Lean = F(P.Lean + 6 * wind - 12 * (u >= .72 && u < 1.2 ? Math.Sin(Math.PI * Math.Min(1, (u - .72) / .5)) : 0));
            P.Squash = F(-.025 * wind + .08 * (u >= .8 ? Math.Max(0, 1 - (u - .8) / .35) : 0));
            P.WingLA = F(46 + 40 * wind); P.WingLB = F(42 + 40 * wind);
            if (u >= .78 && u < 1.2) { P.Eyes = EyeStyle.Squint; P.Beak = F(.5 * Math.Max(0, 1 - (u - .78) / .3)); }
            if (u >= .8) { P.Debris = F(Math.Min(1, (u - .8) / .7)); P.DebrisX = 158; P.DebrisY = 292; }
            if (impact > 0) P.BodyY += 2;
            P.Tuft = F(P.Tuft - 8 * wind + 10 * (u >= .8 ? Math.Max(0, 1 - (u - .8) / .3) : 0));
        }

        // ───────── 스마트 안경으로 검색(빨간 안경의 본업) ─────────
        private void SearchPose(Pose P, double t)
        {
            SitDown(P);
            P.Sit = false; P.BodyY = 0; P.Squash = 0;
            IdleBase(P, animT, false);
            double e = Window(t % 8.0, 0, 7.4, .6), hold = Window(t % 8.0, .5, 6.6, .4);
            double dropK = Ease(((t % 8.0) - .3) / .25) * (1 - Ease(((t % 8.0) - 6.9) / .3));
            P.GlassDrop = F(dropK); P.GlassGlow = F(hold * (.6 + .4 * Math.Sin(t * 8)));
            if (hold > .05) P.Holo = F(t);
            P.Look = Lerp(P.Look, .6, hold); P.Gx = Lerp(P.Gx, .85, hold); P.Gy = Lerp(P.Gy, -.4, hold);
            P.WingRA = Lerp(P.WingRA, 120 + Math.Sin(t * 5) * 6, hold); P.WingRB = Lerp(P.WingRB, 104, hold);
            P.HeadTilt += F(4 * hold); P.HeadY += F(-1 * hold);
            // "아하!" 하고 눈이 번쩍
            double c = (t % 8.0) - 4.2;
            if (c > 0 && c < 1.0) { P.Eyes = EyeStyle.Happy; P.Beak = F(.5 * Math.Sin(Math.PI * c)); P.Squash = F(-.04 * Math.Sin(Math.PI * c)); P.FootLY = P.FootRY = F(-4 * Math.Sin(Math.PI * c)); }
            P.Brow = F(.12 * hold);
        }

        // ───────── 근무복 동작(공식 3D 근무복 그림 8·9·10 + 첨부한 로고) ─────────
        /// <summary>안내하기(그림 9): 웃는 눈으로 한쪽 날개를 쭉 뻗어 "이쪽으로!" 하고 안내한다. 짠! 반짝임.</summary>
        private void GuidePose(Pose P, double t)
        {
            IdleBase(P, animT, false);
            double e = Window(t, .15, extraDuration - .3, .5), sp = Window(t, .5, extraDuration - .6, .4);
            P.Eyes = e > .3 ? EyeStyle.Happy : EyeStyle.Dot; P.OpenL = P.OpenR = 1;
            P.WingRA = Lerp(P.WingRA, 96 + Math.Sin(t * 4.5) * 5, e); P.WingRB = Lerp(P.WingRB, 100 + Math.Sin(t * 4.5 - .6) * 7, e);
            P.Look = Lerp(P.Look, .4, e); P.HeadTilt += F(6 * e); P.Blush = F(.3 * e);
            P.Beak = F(.12 * e);
            if (sp > .01) P.Sparkle = F(t);
            double hop = t < .5 ? Math.Sin(Math.PI * t / .5) : 0;         // 시작할 때 살짝 폴짝(짠!)
            P.FootLY = P.FootRY = F(-3 * hop); P.BodyY -= F(1.5 * hop);
            P.Tuft += F(5 * e); P.TuftWave += 3;
        }

        /// <summary>배꼽인사(그림 10): 날개를 배 앞에 모으고 눈을 감은 채 몸을 숙였다가 일어난다.</summary>
        private void BowPose(Pose P, double t)
        {
            IdleBase(P, animT, false);
            double wing = Window(t, .1, extraDuration - .3, .4);
            double bow = Ease((t - .7) / .6) * (1 - Ease((t - 2.6) / .7));
            P.Eyes = bow > .1 ? EyeStyle.Closed : EyeStyle.Dot; P.OpenL = P.OpenR = 1; P.Blush = F(.35 * wing);
            P.WingLTo = new PointF(Lerp(138, 181, wing), Lerp(214, 198, wing)); P.WingRTo = new PointF(Lerp(262, 219, wing), Lerp(214, 198, wing));
            P.Turn = Lerp(0, -.45, bow); P.Look = Lerp(P.Look, 0, bow);
            P.Lean = F(P.Lean - 17 * bow); P.HeadTilt = F(P.HeadTilt - 9 * bow); P.HeadY += F(4 * bow); P.Squash = F(.03 * bow);
            P.Tuft += F(-8 * bow); P.CardSwing += F(-12 * bow);
        }
    }
}
