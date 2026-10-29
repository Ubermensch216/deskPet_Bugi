using System;
using System.Drawing;

namespace Bugi
{
    internal enum ChatPhase { None, Listening, Thinking, Talking, Afterglow }

    /// <summary>
    /// AI 와 이야기하는 부기. 채팅창 대신 부기의 몸이 대화를 한다:
    /// 말을 걸면 돌아보고(Listening), 생각하는 동안 안경이 반짝이며(Thinking), 글자가 한 자씩 말풍선에 찍히고 부리가 달싹이며
    /// 감정에 맞는 표정·몸짓(Talking · Afterglow)이 나온다.
    /// </summary>
    internal sealed partial class Pet
    {
        internal ChatPhase Phase = ChatPhase.None;
        internal ChatMood Mood = ChatMood.Neutral;
        /// <summary>입력창이 있는 쪽(+1 오른쪽, -1 왼쪽). 부기가 그쪽을 바라본다.</summary>
        internal int ChatSide = 1;
        /// <summary>글자가 한 자 찍힐 때마다 부른다(말소리 효과음용).</summary>
        internal Action<char> OnChatChar;

        private double chatPresence, chatPhaseT, chatMoodT, chatReveal, chatTypeEnergy;
        private string chatText = "";
        private bool chatStreamDone = true, chatTypeDeleted;
        private int chatTypedLen;

        internal bool ChatActive { get { return Phase != ChatPhase.None; } }
        internal string ChatText { get { return chatText; } }
        internal bool ChatRevealing { get { return chatText.Length > 0 && chatReveal < chatText.Length; } }
        /// <summary>대화 상태가 된 뒤(또는 마지막 말이 끝난 뒤) 흐른 시간.</summary>
        internal double ChatQuietSeconds { get { return Phase == ChatPhase.Listening ? chatPhaseT : 0; } }
        internal float ChatPresence { get { return (float)chatPresence; } }

        /// <summary>지금 말을 걸어도 되는 자세인가(낙하·던져짐·벽/천장 타기 중이 아닐 때).</summary>
        internal bool CanChat
        {
            get
            {
                if (Hidden) return false;
                switch (State)
                {
                    case PetState.Falling: case PetState.Drag: case PetState.Thrown: case PetState.PilotRise: case PetState.Climb:
                    case PetState.Wall: case PetState.Ceiling: case PetState.WindowClimb:
                        return false;
                }
                return true;
            }
        }

        internal bool BeginChat(IWorld w, int side)
        {
            if (!CanChat) return false;
            ChatSide = side >= 0 ? 1 : -1;
            if (State != PetState.Chat) { ReleaseFeatures(w); SetState(PetState.Chat, w); }
            Phase = ChatPhase.Listening; Mood = ChatMood.Neutral; chatPhaseT = 0; chatMoodT = 0;
            chatText = ""; chatReveal = 0; chatStreamDone = true; chatTypeEnergy = 0; chatTypedLen = 0;
            return true;
        }

        internal void EndChat(IWorld w)
        {
            if (Phase == ChatPhase.None) return;
            if (!string.IsNullOrEmpty(chatText) && Bubble == chatText) Bubble = null;
            Phase = ChatPhase.None; chatText = ""; chatReveal = 0; Mood = ChatMood.Neutral;
            if (State == PetState.Chat) { SetState(PlayOptions.Current.PositionFixed ? PetState.Pause : PetState.Walk, w); DelayIdle(w); }
        }

        /// <summary>사용자가 말을 보냈다: 안경을 내려쓰고 생각에 잠긴다.</summary>
        internal void ChatThink()
        {
            if (Phase == ChatPhase.None) return;
            if (!string.IsNullOrEmpty(chatText) && Bubble == chatText) Bubble = null;
            chatText = ""; chatReveal = 0; chatStreamDone = false;
            Phase = ChatPhase.Thinking; chatPhaseT = 0; SetMood(ChatMood.Think);
        }

        /// <summary>모델이 보내 주는 말(스트리밍 중에는 점점 길어진다). done 이면 끝까지 받은 것.</summary>
        internal void ChatSay(ChatMood mood, string text, bool done)
        {
            if (Phase == ChatPhase.None) return;
            if (text == null) text = "";
            if (text.Length > 0 && Phase != ChatPhase.Talking && Phase != ChatPhase.Afterglow) { Phase = ChatPhase.Talking; chatPhaseT = 0; }
            if (text.Length > 0 && Phase == ChatPhase.Afterglow && !text.StartsWith(chatText)) { chatReveal = 0; Phase = ChatPhase.Talking; chatPhaseT = 0; }
            if (text.Length > 0) SetMood(mood);
            if (text.Length < chatText.Length && !text.Equals(chatText)) chatReveal = Math.Min(chatReveal, text.Length);
            chatText = text; chatStreamDone = done;
            if (done && text.Length == 0) { Phase = ChatPhase.Listening; chatPhaseT = 0; SetMood(ChatMood.Neutral); }
        }

        /// <summary>대화에 실패했을 때(Ollama 가 꺼져 있을 때 등): 졸린 얼굴로 사정을 말한다.</summary>
        internal void ChatSayInstant(ChatMood mood, string text)
        {
            if (Phase == ChatPhase.None) return;
            chatReveal = 0; Phase = ChatPhase.Talking; chatPhaseT = 0; SetMood(mood);
            chatText = text ?? ""; chatStreamDone = true;
        }

        private void SetMood(ChatMood m) { if (m != Mood) { Mood = m; chatMoodT = 0; } }

        /// <summary>입력창에서 글자를 치는 중: 부기가 눈으로 따라가며 반응한다.</summary>
        internal void ChatTyping(int length)
        {
            if (Phase != ChatPhase.Listening && Phase != ChatPhase.Afterglow) { chatTypedLen = length; return; }
            if (length != chatTypedLen) { chatTypeDeleted = length < chatTypedLen; chatTypeEnergy = 1; }
            chatTypedLen = length;
        }

        private void UpdateChat(IWorld w, double dt)
        {
            double target = Phase != ChatPhase.None ? 1 : 0;
            if (chatPresence != target) chatPresence += Math.Sign(target - chatPresence) * Math.Min(Math.Abs(target - chatPresence), dt * (target > chatPresence ? 2.6 : 3.5));
            if (Phase == ChatPhase.None) return;
            chatPhaseT += dt; chatMoodT += dt;
            chatTypeEnergy = Math.Max(0, chatTypeEnergy - dt * 1.3);
            if (chatText.Length > 0)
            {
                double rate = Mood == ChatMood.Sleepy ? 9 : (Mood == ChatMood.Joy || Mood == ChatMood.Surprise || Mood == ChatMood.Pout) ? 19 : 15;
                if (chatText.Length - chatReveal > 30) rate *= 1.8;
                int before = (int)chatReveal;
                chatReveal = Math.Min(chatText.Length, chatReveal + rate * dt);
                for (int i = before; i < (int)chatReveal && i < chatText.Length; i++)
                    if (!char.IsWhiteSpace(chatText[i]) && OnChatChar != null) { try { OnChatChar(chatText[i]); } catch { } }
                Bubble = chatText; BubbleUntil = w.Now + 1.5;
                if (chatReveal >= chatText.Length && chatStreamDone && Phase == ChatPhase.Talking) { Phase = ChatPhase.Afterglow; chatPhaseT = 0; }
            }
            if (Phase == ChatPhase.Afterglow && chatPhaseT > 6.5 + chatText.Length * .08)
            {
                if (Bubble == chatText) Bubble = null;
                chatText = ""; chatReveal = 0; Phase = ChatPhase.Listening; chatPhaseT = 0; SetMood(ChatMood.Neutral);
            }
        }

        /// <summary>말풍선에 지금까지 찍힌 글자 수(-1 이면 전부).</summary>
        private int ChatRevealCount
        {
            get
            {
                if (Phase == ChatPhase.None || chatText.Length == 0 || Bubble != chatText) return -1;
                return chatReveal >= chatText.Length ? -1 : (int)chatReveal;
            }
        }

        // ───────── 자세 ─────────
        private void ChatPose(Pose P, ref Look L, double now)
        {
            double t = animT;
            L.FaceRight = false;
            IdleBase(P, t, false);
            int side = ChatSide;
            if (Phase == ChatPhase.Listening || Phase == ChatPhase.None) ListeningPose(P, ref L, t, side);
            else if (Phase == ChatPhase.Thinking) ThinkingPose(P, ref L, t, side);
            else
            {
                double k = Ease(chatMoodT / .3);
                if (Phase == ChatPhase.Afterglow) k *= 1 - Ease((chatPhaseT - 3.6) / 1.6);
                ListeningPose(P, ref L, t, side);
                MoodPose(P, ref L, t, side, k);
            }
        }

        /// <summary>듣는 자세: 말을 거는 쪽을 보고 고개를 갸웃. 글자를 치면 시선이 아래(입력창)로, 지우면 갸웃.</summary>
        private void ListeningPose(Pose P, ref Look L, double t, int side)
        {
            double e = chatTypeEnergy;
            P.Look = F(side * (.3 - .08 * e)); P.Gx = F(side * .7); P.Gy = F(.25 + .4 * e);
            P.HeadTilt = F(P.HeadTilt + side * 2.5 + (chatTypeDeleted ? -side * 4 * e : side * 1.5 * e));
            P.Brow = F(chatTypeDeleted ? -.25 * e : 0);
            P.WingLA += F(Math.Sin(t * 15) * 3.5 * e); P.WingRA += F(Math.Sin(t * 15 + 1) * 3.5 * e);
            P.Tuft = F(P.Tuft + e * 4);
            if (chatTypedLen >= 24) { P.Eyes = EyeStyle.Wide; P.EyeScale = 1.05f; }         // 길게 쓰면 눈이 동그래진다
        }

        private void ThinkingPose(Pose P, ref Look L, double t, int side)
        {
            // 스마트 안경을 내려쓰고 렌즈를 반짝이며 턱을 괸다. 생각 점이 하나씩 떠오른다.
            double e = Ease(chatPhaseT / .35);
            P.GlassDrop = F(e); P.GlassGlow = F(e * (.6 + .4 * Math.Sin(t * 8)));
            if (chatPhaseT > .35) P.Holo = F(t);
            P.Look = F(side * .25); P.Gx = F(side * .6); P.Gy = F(-.55 * e);
            P.WingRTo = new PointF(Lerp(262, 226, e), Lerp(214, 142, e)); P.HeadTilt = F(P.HeadTilt - 4 * e * side); P.Eyes = e > .5 ? EyeStyle.Half : EyeStyle.Dot; P.Brow = F(.15 * e);
            L.Decor |= Decor.Dots; L.DecorPhase = (float)t;
        }

        private void MoodPose(Pose P, ref Look L, double t, int side, double k)
        {
            if (k <= .01) return;
            double mt = chatMoodT;
            switch (Mood)
            {
                case ChatMood.Joy:
                {
                    P.Eyes = EyeStyle.Happy; P.OpenL = P.OpenR = 1; P.Blush = F(.4 * k); P.Look = F(P.Look * (1 - k)); P.Gy = F(P.Gy * (1 - k));
                    double q = (mt % .72) / .72, up = mt < 2.9 ? Math.Sin(Math.PI * q) : 0;
                    L.HopY = F(-up * 9 * S * k);
                    P.WingLA += F(70 * up * k); P.WingRA += F(70 * up * k); P.WingLB += F(60 * up * k); P.WingRB += F(60 * up * k);
                    P.FootLY = P.FootRY = F(-6 * up * k); P.Squash = F(q < .1 ? .08 * (1 - q / .1) : -.02 * up);
                    P.Tuft = F(P.Tuft - 12 * up * k); P.TuftWave = 8; P.TuftPhase = F(t * 18);
                    break;
                }
                case ChatMood.Shy:
                {
                    P.Blush = F(.95 * k); P.Eyes = EyeStyle.Half; P.OpenL = P.OpenR = F(.65 + .2 * Math.Sin(t * 1.3));
                    P.Look = F(-side * .25 * k); P.Gx = F(-side * .8 * k); P.Gy = F(.55 * k);
                    P.HeadTilt = F(P.HeadTilt - side * 5 * k); P.Lean = F(Math.Sin(t * 2.2) * 1.6 * k);
                    if (k > .4) { P.WingLTo = new PointF(204, 150); P.WingRTo = new PointF(196, 154); }
                    P.BeakShake = F(.5 * k);
                    break;
                }
                case ChatMood.Sad:
                    SadOverlay(P, mt); P.Look = F(P.Look * .5);
                    break;
                case ChatMood.Surprise:
                {
                    double q = Math.Max(0, Math.Min(1, 1 - mt / .35));
                    P.Eyes = EyeStyle.Wide; P.OpenL = P.OpenR = 1; P.EyeScale = F(1.06 + .08 * q); P.PupilR = .65f; P.Brow = F(-.35 * k); P.Gy = F(-.3); P.Gx = 0; P.Look = 0;
                    P.BodyY += F(-8 * Math.Sin(Math.PI * Math.Min(1, mt / .35)) * (mt < .35 ? 1 : 0));
                    P.WingLA = F(46 + 36 * k); P.WingRA = F(46 + 36 * k); P.WingLB = F(42 + 30 * k); P.WingRB = F(42 + 30 * k);
                    P.Sweat = F(mt); P.Tuft = F(-10 * q);
                    break;
                }
                case ChatMood.Sleepy:
                {
                    double nod = Math.Sin(t * 1.1), yawn = mt % 6;
                    P.Eyes = EyeStyle.Half; P.OpenL = P.OpenR = F(.35 + .25 * (nod * .5 + .5)); P.Look = 0; P.Gx = 0; P.Gy = F(.3);
                    P.HeadTilt = F(P.HeadTilt + 7 * k * (nod * .5 + .5)); P.HeadY = F(2 * k * (nod * .5 + .5)); P.Blush = F(.35 * k);
                    if (yawn < 1.6) { double y = Math.Sin(Math.PI * yawn / 1.6); P.Beak = Math.Max(P.Beak, F(.8 * y)); P.Eyes = y > .4 ? EyeStyle.Squint : EyeStyle.Half; P.WingLA += F(16 * y); P.WingRA += F(16 * y); }
                    L.Decor |= Decor.Sleepy; L.ZPhase = (int)(t * 20) % 100000; L.ZTime = (float)t;
                    break;
                }
                case ChatMood.Think:
                {
                    P.GlassDrop = F(.9 * k); P.GlassGlow = F(k * (.5 + .4 * Math.Sin(t * 6))); if (k > .5) P.Holo = F(t);
                    P.Eyes = EyeStyle.Half; P.Look = F(side * .3); P.Gx = F(side * .55); P.Gy = F(-.5 * k);
                    P.WingRTo = new PointF(Lerp(262, 226, k), Lerp(214, 142, k)); P.HeadTilt = F(P.HeadTilt - 4 * k * side);
                    break;
                }
                case ChatMood.Pout:
                {
                    P.Eyes = EyeStyle.Dot; P.OpenL = P.OpenR = 1; P.Brow = F(.9 * k); P.PupilR = .85f; P.Blush = F(.25 * k);
                    P.Look = F(-side * .3 * k); P.Gx = F(-side * .6 * k); P.Gy = F(.1);
                    P.Anger = F(Math.Min(1, mt * 3 + .15)) * (mt < 2.4 ? 1f : 0f); P.Squash = F(.03 * k);
                    P.WingLA = F(Lerp(P.WingLA, 24, k)); P.WingRA = F(Lerp(P.WingRA, 24, k)); P.WingLB = F(Lerp(P.WingLB, 2, k)); P.WingRB = F(Lerp(P.WingRB, 2, k));
                    P.Lean = F(Math.Sin(mt * 9) * 1.2 * k); P.Tuft = F(Math.Sin(mt * 22) * 4 * k);
                    break;
                }
                case ChatMood.Hungry:
                    if (k > .3) { Pose h = new Pose(); HungryCryPose(h, mt); CopyFace(P, h); L.FaceRight = false; }
                    break;
                default:
                    P.Eyes = EyeStyle.Dot; P.Blush = F(.12 * k);
                    break;
            }
        }

        /// <summary>배고픈 울음 자세를 통째로 가져온다(앉은 자세·날개·눈물까지).</summary>
        private static void CopyFace(Pose dst, Pose src)
        {
            dst.Sit = src.Sit; dst.BodyY = src.BodyY; dst.Squash = src.Squash; dst.Breath = src.Breath; dst.Eyes = src.Eyes; dst.OpenL = src.OpenL; dst.OpenR = src.OpenR;
            dst.Look = src.Look; dst.Beak = src.Beak; dst.BeakShake = src.BeakShake; dst.Brow = src.Brow; dst.HeadTilt = src.HeadTilt; dst.HeadY = src.HeadY;
            dst.WingLTo = src.WingLTo; dst.WingRTo = src.WingRTo; dst.Tears = src.Tears; dst.Tuft = src.Tuft; dst.TuftWave = src.TuftWave; dst.TuftPhase = src.TuftPhase;
            dst.FootLX = src.FootLX; dst.FootRX = src.FootRX; dst.FootLRot = src.FootLRot; dst.FootRRot = src.FootRRot; dst.Gx = src.Gx; dst.Gy = src.Gy;
        }
    }
}
