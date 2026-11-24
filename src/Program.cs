using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("부기")]
[assembly: System.Reflection.AssemblyProduct("부기")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace Bugi
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            // --lang ko|en : 화면 언어를 고정한다(점검용). 나머지 인자 번호가 밀리지 않게 여기서 뺀다.
            string lang = null; var rest = new System.Collections.Generic.List<string>();
            for (int a = 0; a < args.Length; a++)
            {
                if (args[a] == "--lang" && a + 1 < args.Length) { lang = args[++a]; continue; }
                if (args[a].StartsWith("--lang=")) { lang = args[a].Substring(7); continue; }
                rest.Add(args[a]);
            }
            args = rest.ToArray();
            if (lang != null) Loc.Force(lang);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length >= 2 && args[0] == "--make-icon") { DevTools.MakeIcon(args[1]); return; }
            if (args.Length >= 2 && args[0] == "--i18n-test") { Environment.Exit(DevTools.I18nTest(args[1])); return; }
            if (args.Length >= 2 && args[0] == "--render-test") { DevTools.RenderSheet(args[1], args.Length >= 3 ? float.Parse(args[2]) : Native.SystemDpiScale()); return; }
            if (args.Length >= 2 && args[0] == "--spec-test") { CharacterArt.Reset(1f); SpecTools.Render(args[1]); return; }
            if (args.Length >= 1 && args[0] == "--guide-test") { CharacterArt.Reset(1f); Environment.Exit(BugiGuide.Run(args.Length >= 2 ? args[1] : null)); return; }
            if (args.Length >= 2 && args[0] == "--selftest") { Environment.Exit(DevTools.SelfTest(args[1])); return; }
            if (args.Length >= 2 && args[0] == "--ui-test") { Environment.Exit(DevTools.UiTest(args[1])); return; }
            if (args.Length >= 2 && args[0] == "--smoke") { Environment.Exit(DevTools.Smoke(args[1])); return; }
            if (args.Length >= 2 && args[0] == "--note-test") { Environment.Exit(DevTools.NoteTest(args[1])); return; }
            if (args.Length >= 2 && args[0] == "--alert-test") { Environment.Exit(DevTools.AlertTest(args[1], args.Length >= 3 ? args[2].Replace("\\n", "\n") : null)); return; }
            if (args.Length >= 2 && args[0] == "--note-shots") { Environment.Exit(DevTools.NoteShots(args[1])); return; }
            if (args.Length >= 2 && args[0] == "--chat-live") { Environment.Exit(DevTools.ChatLive(args[1])); return; }
            Loc.Load();   // 저장된 언어 → 없으면 Windows 표시 언어 (--lang 이 있으면 그쪽이 우선)
            int cliExit;
            if (ApiServer.TryForwardCliCommand(args, out cliExit)) { Environment.Exit(cliExit); return; }
            bool created;
            using (var mutex = new Mutex(true, "Bugi_DeskFriend_single_instance", out created))
            {
                if (!created)
                {
                    MessageBox.Show(Loc.T("부기가 이미 실행 중이야.\n작업표시줄 알림 영역의 친구 아이콘을 우클릭해서 ‘화면 가운데로 다시 부르기’를 눌러줘."), Loc.T("부기"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { FeatureLog.Write(e.Exception); };
                try
                {
                    PlayOptions.Load(); FriendData.Load();
                    CharacterArt.Reset(Native.SystemDpiScale(), PlayOptions.Current.PetSize);
                    var app = new App(); app.Notes = new NoteManager(app); app.Notes.Start(); app.StartApi();
                    Application.Run(app);
                }
                catch (Exception error)
                {
                    FeatureLog.Write(error);
                    MessageBox.Show(Loc.T("친구를 화면에 표시하지 못했어.\n\n") + error.Message + Loc.T("\n\n기록: ") + Path.Combine(Paths.Dir, "부기_오류기록.txt"), Loc.T("부기"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                GC.KeepAlive(mutex);
            }
        }
    }

    /// <summary>화면 없이 돌려 보는 가짜 세계 (자체 점검·렌더링 확인용).</summary>
    internal sealed class FakeWorld : IWorld
    {
        public List<Rectangle> Areas { get; set; }
        public Random Rng { get; set; }
        public double Now { get; set; }
        public FakeWorld() { Areas = new List<Rectangle> { new Rectangle(0, 0, 1280, 720) }; Rng = new Random(7); }
    }

    internal static partial class DevTools
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern void mouse_event(int flags, int dx, int dy, int data, UIntPtr extra);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, int flags, UIntPtr extra);

        /// <summary>부기 얼굴로 PNG 압축 .ico 를 만든다(빌드할 때 한 번 쓰임).</summary>
        internal static void MakeIcon(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (int n in sizes)
                using (Bitmap bmp = CharacterArt.IconBitmap(n))
                using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (var fs = File.Create(path))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte b = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
                    bw.Write(b); bw.Write(b); bw.Write((byte)0); bw.Write((byte)0);
                    bw.Write((ushort)1); bw.Write((ushort)32); bw.Write((uint)pngs[i].Length); bw.Write((uint)offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] p in pngs) bw.Write(p);
            }
        }

        /// <summary>캐릭터별·상태별 모습을 한 장의 시트 PNG 로 저장한다.</summary>
        internal static void RenderSheet(string dir, float scale)
        {
            Directory.CreateDirectory(dir);
            CharacterArt.Reset(scale);
            PlayOptions.Current = new PlayOptions();
            DesktopSurfaces.TestProvider = () => new List<SurfaceWindow>(); DesktopSurfaces.TestMouse = () => new Point(-9999, -9999);
            FriendData.Current = new FriendData();
            var states = new List<KeyValuePair<string, Action<Pet, FakeWorld>>>
            {
                K("걷기", (p, w) => { p.SetState(PetState.Walk, w); p.Update(w, .4, null, 0); }),
                K("서기", (p, w) => p.SetState(PetState.Pause, w)),
                K("앉기", (p, w) => p.Demonstrate(w, "sit")),
                K("눕기", (p, w) => { p.Demonstrate(w, "lie"); w.Now += 3; p.Update(w, 3, null, 0); }),
                K("곡괭이", (p, w) => { p.Demonstrate(w, "mine"); for (int i = 0; i < 52; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("검색", (p, w) => { p.Demonstrate(w, "search"); for (int i = 0; i < 150; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("머리긁적", (p, w) => { p.Demonstrate(w, "scratch"); for (int i = 0; i < 60; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("팔짱", (p, w) => { p.Demonstrate(w, "cross"); for (int i = 0; i < 60; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("인사", (p, w) => { p.Demonstrate(w, "wave"); for (int i = 0; i < 60; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("발끈", (p, w) => { p.StartSquish(w, .6, .24, PetState.Walk, false); w.Now += .25; p.Update(w, .25, null, 0); }),
                K("근무복", (p, w) => { PlayOptions.Current.Outfit = true; p.SetState(PetState.Pause, w); }),
                K("제복", (p, w) => { PlayOptions.Current.Outfit = true; PlayOptions.Current.OutfitStyle = OutfitStyle.Uniform; p.SetState(PetState.Pause, w); }), K("구조복", (p, w) => { PlayOptions.Current.Outfit = true; PlayOptions.Current.OutfitStyle = OutfitStyle.Rescue; p.SetState(PetState.Pause, w); }),
                K("정장", (p, w) => { PlayOptions.Current.Outfit = true; PlayOptions.Current.OutfitStyle = OutfitStyle.Suit; p.SetState(PetState.Pause, w); }), K("안전복", (p, w) => { PlayOptions.Current.Outfit = true; PlayOptions.Current.OutfitStyle = OutfitStyle.Safety; p.SetState(PetState.Pause, w); }),
                K("구명조끼", (p, w) => { PlayOptions.Current.Outfit = true; PlayOptions.Current.OutfitStyle = OutfitStyle.LifeJacket; p.SetState(PetState.Pause, w); }), K("아이템", (p, w) => { PlayOptions.Current.AccBag = PlayOptions.Current.AccVisor = true; p.SetState(PetState.Pause, w); }), K("선글라스", (p, w) => { PlayOptions.Current.AccSunglasses = true; p.SetState(PetState.Pause, w); }),
                K("안내", (p, w) => { p.Demonstrate(w, "guide"); for (int i = 0; i < 125; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("배꼽인사", (p, w) => { p.Demonstrate(w, "bow"); for (int i = 0; i < 115; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("셀카", (p, w) => { p.Demonstrate(w, "selfie"); for (int i = 0; i < 105; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("부산최고", (p, w) => { p.Demonstrate(w, "idcard"); for (int i = 0; i < 90; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("파도깃발", (p, w) => { p.Demonstrate(w, "flag"); for (int i = 0; i < 90; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("멍...", (p, w) => { p.Demonstrate(w, "daydream"); for (int i = 0; i < 60; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("안녕", (p, w) => SkitShot(p, w, Skit.Hello)), K("ㅋㅋ", (p, w) => SkitShot(p, w, Skit.Laugh)), K("하트", (p, w) => SkitShot(p, w, Skit.HeartHug)),
                K("두두등장", (p, w) => SkitShot(p, w, Skit.Hero)), K("응원", (p, w) => SkitShot(p, w, Skit.Cheer)), K("뿌듯", (p, w) => SkitShot(p, w, Skit.Proud)),
                K("발표", (p, w) => SkitShot(p, w, Skit.Present)), K("Good", (p, w) => SkitShot(p, w, Skit.Good)), K("OKAY", (p, w) => SkitShot(p, w, Skit.Okay)),
                K("분주분주", (p, w) => SkitShot(p, w, Skit.Busy)), K("Busan", (p, w) => SkitShot(p, w, Skit.Busan)), K("여행", (p, w) => SkitShot(p, w, Skit.Travel)),
                K("토닥토닥", (p, w) => SkitShot(p, w, Skit.Pat)),
                K("낮잠", (p, w) => p.SetState(PetState.Sleep, w)),
                K("눌림", (p, w) => { p.StartSquish(w, .4, .24, PetState.Walk, false); w.Now += .2; p.Update(w, .2, null, 0); }),
                K("우는중", (p, w) => p.Demonstrate(w, "cry")),
                K("배고픔", (p, w) => p.Demonstrate(w, "weep")),
                K("힘없음-한숨", (p, w) => { FriendData.Current.Satiety = 8; p.SetState(PetState.Pause, w); for (int i = 0; i < 30; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("힘없음-배웅킴", (p, w) => { FriendData.Current.Satiety = 8; p.Demonstrate(w, "weak_clutch"); for (int i = 0; i < 40; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("힘없음-훌쩍", (p, w) => { FriendData.Current.Satiety = 8; p.Demonstrate(w, "weak_sob"); for (int i = 0; i < 40; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("힘없음-쓰러짐", (p, w) => { FriendData.Current.Satiety = 3; p.Demonstrate(w, "weak_collapse"); for (int i = 0; i < 120; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("힘없음-멍", (p, w) => { FriendData.Current.Satiety = 3; p.Demonstrate(w, "weak_gaze"); for (int i = 0; i < 40; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("폴짝", (p, w) => { p.Demonstrate(w, "hop"); p.Update(w, .25, null, 0); }),
                K("슬라이딩", (p, w) => p.Demonstrate(w, "slide")),
                K("어질어질", (p, w) => p.Demonstrate(w, "dizzy")),
                K("퍼덕낙하", (p, w) => p.Demonstrate(w, "fall")),
                K("벽타기", (p, w) => p.Demonstrate(w, "wall")),
                K("천장", (p, w) => p.Demonstrate(w, "ceiling")),
                K("먹는중", (p, w) => { FriendData.Current.Satiety = 40; p.Feed(w); for (int i = 0; i < 45; i++) { w.Now += .02; p.Update(w, .02, null, 0); } }),
                K("꽃다발", (p, w) => p.StartFlower(w, 100)),
                K("말풍선", (p, w) => p.Cheer(w, "점심 먹으러 가자!", 8)),
                K("대화-듣는중", (p, w) => ChatDemo(p, w, ChatPhase.Listening, ChatMood.Neutral, "", 30, 8)),
                K("대화-생각중", (p, w) => ChatDemo(p, w, ChatPhase.Thinking, ChatMood.Think, "", 70, 0)),
                K("대화-기쁨", (p, w) => ChatDemo(p, w, ChatPhase.Talking, ChatMood.Joy, "어, 왔네! 기다리고 있었어.", 40, 0)),
                K("대화-부끄", (p, w) => ChatDemo(p, w, ChatPhase.Talking, ChatMood.Shy, "그, 그런 말 하면 부끄럽잖아...", 90, 0)),
                K("대화-슬픔", (p, w) => ChatDemo(p, w, ChatPhase.Talking, ChatMood.Sad, "무슨 일 있었어? 난 옆에서 들어줄게.", 90, 0)),
                K("대화-놀람", (p, w) => ChatDemo(p, w, ChatPhase.Talking, ChatMood.Surprise, "헉, 진짜? 깜짝 놀랐잖아!", 10, 0)),
                K("대화-졸림", (p, w) => ChatDemo(p, w, ChatPhase.Talking, ChatMood.Sleepy, "이 시간에 안 자고 뭐 해?", 100, 0)),
                K("대화-생각", (p, w) => ChatDemo(p, w, ChatPhase.Talking, ChatMood.Think, "음... 난 갈매기니까 새우깡!", 90, 0)),
                K("대화-삐짐", (p, w) => ChatDemo(p, w, ChatPhase.Talking, ChatMood.Pout, "흥, 몰라! 나 삐졌어.", 70, 0)),
                K("대화-배고픔", (p, w) => ChatDemo(p, w, ChatPhase.Talking, ChatMood.Hungry, "나 배고파... 간식 없어?", 90, 0)),
                K("대화-근무복", (p, w) => { PlayOptions.Current.Outfit = true; ChatDemo(p, w, ChatPhase.Talking, ChatMood.Joy, "명찰도 달았어!", 40, 0); }),
                K("대화-타이핑", (p, w) => ChatDemo(p, w, ChatPhase.Listening, ChatMood.Neutral, "", 30, 30)),
            };
            int cell = (int)Math.Round(150 * scale), cols = 8, rows = (int)Math.Ceiling(states.Count / (double)cols);
            for (int c = 0; c < CharacterArt.Count; c++)
            {
                using (var sheet = new Bitmap(cell * cols, cell * rows, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(sheet))
                using (var font = new Font("Malgun Gothic", 9f * scale))
                {
                    g.Clear(Color.FromArgb(206, 226, 214)); g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    for (int i = 0; i < states.Count; i++)
                    {
                        var w = new FakeWorld(); w.Areas[0] = new Rectangle(0, 0, 1280, 720);
                        var pet = new Pet(w, c, 640, 720) { Area = w.Areas[0] };
                        pet.SetState(PetState.Pause, w);
                        pet.X = 640; pet.Y = 720;
                        PlayOptions.Current.Outfit = false; PlayOptions.Current.OutfitStyle = OutfitStyle.Vest; PlayOptions.Current.AccBag = PlayOptions.Current.AccVisor = PlayOptions.Current.AccSunglasses = false;
                        FriendData.Current.Satiety = 100;
                        states[i].Value(pet, w);
                        PointF origin; using (Bitmap b = Pet.Render(pet.GetLook(w.Now), out origin))
                        {
                            int cx = (i % cols) * cell, cy = (i / cols) * cell;
                            float f = Math.Min(1f, Math.Min((cell - 10f) / b.Width, (cell - 30f) / b.Height));
                            g.DrawImage(b, cx + (cell - b.Width * f) / 2, cy + cell - 18 * scale - b.Height * f, b.Width * f, b.Height * f);
                            g.DrawString(CharacterArt.Infos[c].Name + " · " + states[i].Key, font, Brushes.Black, cx + 4, cy + cell - 18 * scale);
                        }
                    }
                    sheet.Save(Path.Combine(dir, "sheet_" + CharacterArt.Infos[c].Key + ".png"), ImageFormat.Png);
                }
            }
            // 벽·천장 타기 한 주기(약 0.6초)를 10컷으로
            // 시간 흐름에 따른 연속 컷(캐릭터별 한 장): 걷기·벽 오르기/내리기·천장·낙하·점프·기지개·멍때리기·잠자기
            for (int c = 0; c < CharacterArt.Count; c++)
            {
                var rowDefs = new List<Tuple<string, Action<Pet, FakeWorld>, double, double>>
                {
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("걷기", (p, w) => { p.Y = 720; p.SetState(PetState.Walk, w); }, 0.55, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("벽 오르기", (p, w) => p.Demonstrate(w, "wall"), 0.55, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("벽 내려오기", (p, w) => p.Demonstrate(w, "walldown"), 0.55, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("천장", (p, w) => p.Demonstrate(w, "ceiling"), 0.55, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("낙하", (p, w) => p.Demonstrate(w, "fall"), 2.6, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("점프", (p, w) => { p.Y = 720; p.Demonstrate(w, "hop"); }, 0.7, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("기지개", (p, w) => { p.Y = 720; p.Demonstrate(w, "stretch"); }, 4.2, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("멍때리기", (p, w) => { p.Y = 720; p.Demonstrate(w, "daydream"); }, 3.6, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("잠자기", (p, w) => { p.Y = 720; p.Demonstrate(w, "sleep"); }, 3.6, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("곡괭이질", (p, w) => { p.Y = 720; p.Demonstrate(w, "mine"); }, 3.4, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("검색", (p, w) => { p.Y = 720; p.Demonstrate(w, "search"); }, 8.0, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("안내", (p, w) => { p.Y = 720; p.Demonstrate(w, "guide"); }, 5.6, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("배꼽인사", (p, w) => { p.Y = 720; p.Demonstrate(w, "bow"); }, 4.6, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("응원", (p, w) => { p.Y = 720; p.Demonstrate(w, "skit:Cheer"); }, 5.0, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("ㅋㅋ", (p, w) => { p.Y = 720; p.Demonstrate(w, "skit:Laugh"); }, 4.2, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("여행", (p, w) => { p.Y = 720; p.Demonstrate(w, "skit:Travel"); }, 5.0, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("간식", (p, w) => { p.Y = 720; FriendData.Current.Satiety = 40; p.Feed(w); }, 3.4, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("힘없는걷기", (p, w) => { p.Y = 720; FriendData.Current.Satiety = 8; p.SetState(PetState.Walk, w); }, 1.2, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("탈진쓰러짐", (p, w) => { p.Y = 720; FriendData.Current.Satiety = 3; p.Demonstrate(w, "weak_collapse"); }, 4.0, 0),
                    Tuple.Create<string, Action<Pet, FakeWorld>, double, double>("도망", (p, w) => { p.Y = 720; p.Demonstrate(w, "slide"); }, 0.8, 0),
                };
                int cw = (int)Math.Round(100 * scale), ch = (int)Math.Round(92 * scale), frameCols = 12;
                using (var strip = new Bitmap(cw * frameCols, ch * rowDefs.Count, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(strip))
                using (var font = new Font("Malgun Gothic", 8f * scale))
                {
                    g.Clear(Color.FromArgb(206, 226, 214)); g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    for (int r = 0; r < rowDefs.Count; r++)
                    {
                        var w = new FakeWorld { Rng = new Random(5) };
                        var pet = new Pet(w, c, 640, 720) { Area = w.Areas[0] };
                        pet.SetState(PetState.Pause, w); pet.X = 640; pet.Y = 720;
                        FriendData.Current.Satiety = 100;
                        rowDefs[r].Item2(pet, w);
                        g.DrawString(rowDefs[r].Item1, font, Brushes.Black, 2, r * ch + 2);
                        double dt = rowDefs[r].Item3 / frameCols;
                        for (int i = 0; i < frameCols; i++)
                        {
                            for (double s2 = 0; s2 < dt - 1e-6; s2 += 0.02) { w.Now += 0.02; pet.Update(w, 0.02, null, 0); }
                            PointF origin; using (Bitmap b = Pet.Render(pet.GetLook(w.Now), out origin))
                            {
                                float f = Math.Min(1f, Math.Min((cw - 4f) / b.Width, (ch - 14f) / b.Height));
                                g.DrawImage(b, i * cw + (cw - b.Width * f) / 2, r * ch + ch - 2 - b.Height * f, b.Width * f, b.Height * f);
                            }
                        }
                    }
                    strip.Save(Path.Combine(dir, "motion_" + CharacterArt.Infos[c].Key + ".png"), ImageFormat.Png);
                }
            }            Console.WriteLine("saved " + dir);
        }

        internal static double SkitLen(Skit s) { return Pet.SkitSecondsOf(s); }

        /// <summary>가이드북 응용동작을 한창일 때(1.6초 뒤)의 모습으로 보여 준다.</summary>
        internal static void SkitShot(Pet p, FakeWorld w, Skit s)
        {
            p.Demonstrate(w, "skit:" + s);
            for (int i = 0; i < 80; i++) { w.Now += .02; p.Update(w, .02, null, 0); }
        }

        /// <summary>AI 대화 자세를 그림으로 확인하려고, 대화를 시작시킨 뒤 steps 만큼(0.02초씩) 시간을 흘려 보낸다.</summary>
        internal static void ChatDemo(Pet p, FakeWorld w, ChatPhase phase, ChatMood mood, string text, int steps, int typedLen)
        {
            p.BeginChat(w, 1);
            if (phase == ChatPhase.Thinking) p.ChatThink();
            else if (phase == ChatPhase.Talking) p.ChatSay(mood, text, true);
            p.ChatTyping(0); if (typedLen > 0) p.ChatTyping(typedLen);
            for (int i = 0; i < steps; i++) { w.Now += .02; p.Update(w, .02, null, 0); }
        }

        /// <summary>실제 Ollama 와 끝까지 이어 본다: (1) 말투·감정 태그 품질, (2) 더블클릭 대신 직접 말 걸기 → 입력칸 → 생각 → 스트리밍 말풍선을 화면 캡처로 남긴다.</summary>
        internal static int ChatLive(string dir)
        {
            Directory.CreateDirectory(dir);
            Paths.StorageOverride = Path.Combine(dir, "data");
            var log = new StringBuilder(); int failures = 0;
            Action<double> pump = secs => { var end = DateTime.UtcNow.AddSeconds(secs); while (DateTime.UtcNow < end) { Application.DoEvents(); Thread.Sleep(15); } };
            PlayOptions.Load(); FriendData.Load(); CharacterArt.Reset(Native.SystemDpiScale()); ChatOptions.Load();
            var opts = ChatOptions.Current;
            log.AppendLine("Probe: " + LlmClient.Probe(opts));

            // (1) 품질
            foreach (string q in new[] { "안녕", "오늘 너무 피곤해", "너 누구야?", "점심 뭐 먹을까", "서울 인구가 몇 명이야?", "너 AI야?", "니체가 누구야?", "아인슈타인은 뭐 한 사람이야?", "광합성이 뭐야?", "오늘 날씨 어때?" })
            {
                var hist = new List<ChatMessage> { new ChatMessage("user", q) };
                var parser = new ReplyParser(); var req = new ChatRequest(); var sw = System.Diagnostics.Stopwatch.StartNew(); long first = -1;
                string err = LlmClient.Stream(opts, ChatPersona.Build(hist, DateTime.Now), t => { if (first < 0) first = sw.ElapsedMilliseconds; parser.Feed(t); }, req);
                log.AppendLine(string.Format("Q: {0}\n   -> [{1}] {2}   (첫 글자 {3}ms, 전체 {4}ms, err={5})\n   raw: {6}", q, parser.Mood, parser.Text, first, sw.ElapsedMilliseconds, err, parser.Raw.Replace("\n", "\\n")));
                if (err != null || parser.Text.Length == 0) failures++;
            }

            // (1b) 대화 모드별 말투: 같은 질문에 모드마다 어떻게 답하는지
            foreach (string q in new[] { "요즘 일이 너무 많아서 지쳐", "중력이 뭐야?", "야식 먹고 싶다" })
                for (int mi = 0; mi < ChatMode.All.Length; mi++)
                {
                    ChatOptions.Current.Mode = mi; var mode = ChatMode.All[mi];
                    var parser = new ReplyParser(mode.MaxSentences, mode.MaxChars); var req = new ChatRequest();
                    string err = LlmClient.Stream(opts, ChatPersona.Build(new List<ChatMessage> { new ChatMessage("user", q) }, DateTime.Now), t => parser.Feed(t), req, mode.NumPredict, mode.Temperature);
                    log.AppendLine(string.Format("[{0}] Q: {1} -> [{2}] {3}  ({4}자, err={5})", mode.Name, q, parser.Mood, parser.Text, parser.Text.Length, err));
                    if (err != null || parser.Text.Length == 0) failures++;
                }
            ChatOptions.Current.Mode = 0;
            // (2) 화면
            var app = new App(true); app.Spawn(0); app.Chat.TestIgnoreClicks = true;       // 점검 도중 실제로 마우스를 눌러도 대화가 끝나지 않게
            Pet pet = app.LiveWindows[0].Pet;
            for (int i = 0; i < 60 && !pet.CanChat; i++) pump(.25);      // 날개를 퍼덕이며 내려와 착지할 때까지
            pump(.6);
            Action<string> shot = name =>
            {
                Rectangle r = Screen.PrimaryScreen.Bounds;
                int w = 820, h = 420; int x = (int)Math.Max(r.Left, Math.Min(r.Right - w, pet.X - w / 2)), y = (int)Math.Max(r.Top, Math.Min(r.Bottom - h, pet.Y - h + 90));
                using (var b = new Bitmap(w, h)) using (var g = Graphics.FromImage(b)) { g.CopyFromScreen(x, y, 0, 0, new Size(w, h)); b.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png); }
            };
            app.StartChat(pet); pump(.9); shot("live_1_hello");
            log.AppendLine("after start: " + app.Chat.DebugState + " badge=" + pet.ChatPresence.ToString("0.00") + " state=" + pet.State);
            if (pet.State != PetState.Chat || !pet.ChatActive) failures++;
            app.Chat.TestType("오늘 너무 피곤해"); pump(.5); shot("live_2_typing");
            app.Chat.TestSubmit(); pump(.35); shot("live_3_thinking"); log.AppendLine("thinking: " + app.Chat.DebugState);
            string lastState = ""; int n = 0;
            for (int i = 0; i < 40; i++)
            {
                pump(.25); string s = app.Chat.DebugState;
                if (s != lastState) { log.AppendLine("t+" + (.35 + (i + 1) * .25).ToString("0.00") + " " + s); lastState = s; }
                if (i == 8 || i == 16) shot("live_4_talking_" + (n++));
            }
            shot("live_5_done");
            if (!lastState.Contains("[") && lastState.Length == 0) failures++;
            if (!app.Chat.DebugState.Contains("/")) { log.AppendLine("대화가 끝났거나 응답 없음"); }
            // 답장 칩: 부기가 말을 끝내면 알약 위에 뜨고, 하나를 누르면 그 말이 그대로 전달된다
            string[] chipList = null; for (int i = 0; i < 40 && chipList == null; i++) { pump(.25); chipList = app.Chat.TestChips; }
            log.AppendLine("칩: " + (chipList == null ? "(없음)" : string.Join(" | ", chipList)));
            if (chipList == null || chipList.Length == 0) { log.AppendLine("FAIL 답장 칩이 안 떴어요"); failures++; }
            else
            {
                pump(.5); shot("live_5b_chips");
                int before = app.Chat.History.Count;
                if (!app.Chat.TestPickChip(1)) { log.AppendLine("FAIL 칩 누르기 실패"); failures++; }
                pump(.3);
                if (app.Chat.History.Count != before + 1 || app.Chat.History[app.Chat.History.Count - 1].Content != chipList[0] || app.Chat.TestChips != null) { log.AppendLine("FAIL 칩이 말로 전달되지 않음"); failures++; }
                for (int i = 0; i < 40 && app.Chat.DebugState.Contains("streaming") || app.Chat.DebugState.StartsWith("Thinking"); i++) pump(.25);
                pump(.5);
            }
            // 대화창 밖 클릭: 알약 안을 누르면 계속되고, 바깥을 누르면 대화가 끝난다(실제 마우스를 잠깐 움직인다)
            Rectangle ib = app.Chat.InputBounds ?? Rectangle.Empty;
            if (ib.IsEmpty) { log.AppendLine("FAIL 입력칸 없음"); failures++; }
            else
            {
                app.Chat.TestIgnoreClicks = false;
                Point inside = new Point(ib.Left + ib.Width / 2, ib.Bottom - 26), outside = new Point(Screen.PrimaryScreen.Bounds.Left + 40, Screen.PrimaryScreen.Bounds.Top + 40);
                Cursor.Position = inside; pump(.15); mouse_event(2, 0, 0, 0, UIntPtr.Zero); pump(.05); mouse_event(4, 0, 0, 0, UIntPtr.Zero); pump(.4);
                log.AppendLine("알약 안 클릭 후 활성: " + app.Chat.Active);
                if (!app.Chat.Active) { log.AppendLine("FAIL 입력칸 안을 눌렀는데 대화가 끝남"); failures++; }
                Cursor.Position = outside; pump(.15); mouse_event(2, 0, 0, 0, UIntPtr.Zero); pump(.05); mouse_event(4, 0, 0, 0, UIntPtr.Zero); pump(.6);
                log.AppendLine("바깥 클릭 후 활성: " + app.Chat.Active);
                if (app.Chat.Active) { log.AppendLine("FAIL 바깥을 눌렀는데 대화가 안 끝남"); failures++; }
            }
            app.Chat.End(); pump(.6); shot("live_6_end");
            log.AppendLine("after end: " + app.Chat.DebugState + " badge=" + pet.ChatPresence.ToString("0.00") + " state=" + pet.State);
            log.AppendLine("history: " + string.Join(" | ", app.Chat.History.Select(m => m.Role + ": " + m.Content).ToArray()));
            // (2b) 일기: 대화가 끝나면 한 줄이 일기장에 쌓인다
            for (int i = 0; i < 80 && ChatDiary.Entries.Count == 0; i++) pump(.25);
            ChatDiary.Reload(); log.AppendLine("일기: " + string.Join(" / ", ChatDiary.Entries.Select(e => e.When.ToString("HH:mm") + " " + e.Text).ToArray()));
            if (ChatDiary.Entries.Count == 0) { log.AppendLine("FAIL 일기가 안 쓰임"); failures++; }
            // (3) 기억 추려내기: 대화 → 사실 한두 줄
            string memReply = LlmClient.Complete(opts, ChatPersona.MemoryRequest("사용자: 나 이번 주 금요일에 면접 있어\n부기: [놀람] 헉, 어디 면접이야?\n사용자: 네이버 백엔드 개발자 면접이야. 우리 집 고양이 이름은 두부야\n부기: [기쁨] 두부? 이름 귀엽다! 면접 잘 될 거야.\n"), 120, .2);
            var memFacts = ChatPersona.ParseFacts(memReply);
            log.AppendLine("기억 추출 raw: " + (memReply ?? "(null)").Replace("\n", " / ") + "  => " + memFacts.Count + "개");
            if (memFacts.Count == 0) failures++;
            // (4) 먼저 말 걸기: 입력칸 없이 말풍선만 뜨고, 끝나면 스스로 돌아간다
            ChatOptions.Current.Enabled = true; app.Settings.NapOn = false; pet.SetState(PetState.Pause, app); pump(.3);
            if (!app.Chat.StartProactive(app.Now)) { log.AppendLine("FAIL 선톡 시작 못 함"); failures++; }
            else
            {
                string ls = ""; double t0 = DateTime.UtcNow.Ticks / 1e7;
                for (int i = 0; i < 90 && app.Chat.Active; i++)
                {
                    pump(.2); string s = app.Chat.DebugState; if (s != ls) { log.AppendLine("선톡 " + ((DateTime.UtcNow.Ticks / 1e7) - t0).ToString("0.0") + "s " + s); ls = s; }
                    if (s.StartsWith("Talking") && s.Length > 22) shot("live_9_proactive");
                }
                if (app.Chat.Active) { log.AppendLine("FAIL 선톡이 끝나지 않음"); failures++; app.Chat.End(); }
            }
            // 설정 페이지와 기억 창 모양
            try
            {
                var sf = new SettingsForm(app, 5); sf.Show(); pump(.4);
                using (var b = new Bitmap(sf.Width, sf.Height)) { sf.DrawToBitmap(b, new Rectangle(0, 0, sf.Width, sf.Height)); b.Save(Path.Combine(dir, "live_7_settings.png"), ImageFormat.Png); }
                sf.Close();
                ChatMemory.Add(new[] { "- 이름은 민수", "- 고양이 두부를 키운다" });
                var mf = new ChatMemoryForm(app.TrayIconImage); mf.Show(); pump(.3);
                using (var b = new Bitmap(mf.Width, mf.Height)) { mf.DrawToBitmap(b, new Rectangle(0, 0, mf.Width, mf.Height)); b.Save(Path.Combine(dir, "live_8_memory.png"), ImageFormat.Png); }
                mf.Close(); ChatMemory.Clear();
                var df = new ChatDiaryForm(app.TrayIconImage); df.Show(); pump(.3);
                using (var b = new Bitmap(df.Width, df.Height)) { df.DrawToBitmap(b, new Rectangle(0, 0, df.Width, df.Height)); b.Save(Path.Combine(dir, "live_10_diary.png"), ImageFormat.Png); }
                df.Close();
            }
            catch (Exception e) { log.AppendLine("FAIL 설정·기억 창: " + e); failures++; }
            log.AppendLine(failures == 0 ? "ALL OK" : failures + " FAILED");
            File.WriteAllText(Path.Combine(dir, "chat_live_result.txt"), log.ToString(), new UTF8Encoding(true));
            app.Quit();
            return failures == 0 ? 0 : 1;
        }

        /// <summary>근무복을 입힌 부기를 실제 바탕화면에 잠깐 띄워 화면 아래 띠를 저장한다(마우스는 건드리지 않음, 단일 실행 잠금과 무관).</summary>
        internal static int Smoke(string dir)
        {
            Directory.CreateDirectory(dir);
            Paths.StorageOverride = Path.Combine(dir, "data");
            PlayOptions.Load(); PlayOptions.Current.Outfit = true; FriendData.Load(); CharacterArt.Reset(Native.SystemDpiScale());
            var app = new App(true); app.Spawn(0);
            var end = DateTime.UtcNow.AddSeconds(11);
            while (DateTime.UtcNow < end) { Application.DoEvents(); Thread.Sleep(15); }
            Rectangle r = Screen.PrimaryScreen.Bounds; int h = 260;
            using (var b = new Bitmap(r.Width, h)) using (var g = Graphics.FromImage(b))
            { g.CopyFromScreen(new Point(r.Left, r.Bottom - h - 50), Point.Empty, new Size(r.Width, h)); b.Save(Path.Combine(dir, "smoke_strip.png"), ImageFormat.Png); }
            return 0;
        }

        /// <summary>실제 창들을 만들어 띄우고 그림으로 남긴다 (레이아웃·실행 오류 확인).</summary>
        internal static int UiTest(string dir)
        {
            Directory.CreateDirectory(dir);
            Paths.StorageOverride = Path.Combine(dir, "data");
            var log = new StringBuilder(); int failures = 0;
            Action<string, Action> step = (name, a) => { try { a(); log.AppendLine("OK   " + name); } catch (Exception e) { failures++; log.AppendLine("FAIL " + name + " " + e); } };
            Action<double> pump = secs => { var end = DateTime.UtcNow.AddSeconds(secs); while (DateTime.UtcNow < end) { Application.DoEvents(); Thread.Sleep(15); } };
            Action<Form, string> shot = (f, name) =>
            {
                f.Show(); pump(.4);
                using (var b = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height)); b.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png); }
            };
            Action<string> screen = name =>
            {
                Rectangle r = Screen.PrimaryScreen.Bounds;
                using (var b = new Bitmap(r.Width, r.Height)) using (var g = Graphics.FromImage(b)) { g.CopyFromScreen(r.Location, Point.Empty, r.Size); b.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png); }
            };
            PlayOptions.Load(); FriendData.Load(); CharacterArt.Reset(Native.SystemDpiScale());
            App app = null;
            step("앱 생성(펫 4마리)", () => { app = new App(true); app.Spawn(0); app.Spawn(1); app.Spawn(2); app.Spawn(3); pump(1); });
            for (int page = 0; page < SettingsForm.PageNames.Length; page++)
            {
                int p = page;
                step("통합 설정창: " + SettingsForm.PageNames[p], () => { var f = new SettingsForm(app, p); shot(f, "ui_settings_" + p); f.Close(); });
            }
            step("통합 설정창: 알림 · 초과근무 알림 탭", () =>
            {
                var now = DateTime.Now; app.Overtime.SetWatch(true, now); app.Overtime.SetBasic("09:00", "18:00"); app.Overtime.EnsureToday(now); app.Overtime.SetRange(10, 23);
                var f = new SettingsForm(app, 2); f.SelectAlarmTab(1); shot(f, "ui_settings_overtime");
                // 기본근무 시간을 고치면 시간표 줄이 곧바로 달라져야 한다(09~18 → 10~16)
                var pickers = new List<DateTimePicker>(); OvertimeBoard board = null;
                Action<Control> walk = null; walk = c => { foreach (Control k in c.Controls) { var dp = k as DateTimePicker; if (dp != null && dp.CustomFormat == "HH:mm") pickers.Add(dp); var bd = k as OvertimeBoard; if (bd != null) board = bd; walk(k); } };
                walk(f);
                if (pickers.Count != 4 || board == null) throw new Exception("overtime controls not found");
                int rows0 = board.RowCount; DateTime d0 = DateTime.Today;
                pickers[1].Value = d0.AddHours(16); pickers[0].Value = d0.AddHours(10);
                int rows1 = board.RowCount;
                pickers[0].Value = d0.AddHours(9); pickers[1].Value = d0.AddHours(18);
                int rows2 = board.RowCount; f.Close();
                if (rows0 != 15 || rows1 != 18 || rows2 != 15) throw new Exception("rows " + rows0 + "/" + rows1 + "/" + rows2);
                // 미사용 시간대(00:00 ~ 06:00)를 정하면 그 줄이 곧바로 빠지고, 같게 되돌리면 다시 나타난다
                var f2 = new SettingsForm(app, 2); f2.SelectAlarmTab(1);
                pickers.Clear(); board = null; walk(f2);
                if (pickers.Count != 4 || board == null) throw new Exception("overtime unused controls not found " + pickers.Count);
                pickers[3].Value = d0.AddHours(6);   // 미사용 끝 (표시 순서: 기본 시작·끝, 미사용 시작·끝)
                int rows3 = board.RowCount;
                pickers[3].Value = d0; int rows4 = board.RowCount; f2.Close();
                if (rows3 != 9 || rows4 != 15) throw new Exception("unused rows " + rows3 + "/" + rows4);
            });
            step("초과근무 알림: 알림 속 부기를 누르면 '기록됨'", () =>
            {
                var st = app.Overtime; DateTime n = DateTime.Now; int h = n.Hour;
                st.SetWatch(true, n); st.EnsureToday(n); st.MarkRecorded(h, false, n);
                var a = app.ShowOvertimeAlert(new OvertimeEvent { Hour = h, Message = OvertimeState.HourLabel(h) + " 초과근무 기록할 시간이야!" });
                if (a == null) throw new Exception("alert not shown");
                pump(.3);
                if (st.Recorded.ContainsKey(h)) throw new Exception("recorded before click");
                Action<Control, MouseButtons> up = (w, b) => typeof(Control).GetMethod("OnMouseUp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(w, new object[] { new MouseEventArgs(b, 1, 10, 10, 0) });
                up(a, MouseButtons.Left); pump(.2);
                if (!st.Recorded.ContainsKey(h)) throw new Exception("not recorded after click");
                if (!a.IsDisposed && a.Visible) throw new Exception("alert still open");
                // 오른쪽 단추로 닫으면 기록하지 않는다
                st.MarkRecorded(h, false, n);
                var b2 = app.ShowOvertimeAlert(new OvertimeEvent { Hour = h, Message = OvertimeState.HourLabel(h) + " 초과근무 기록할 시간이야!" });
                pump(.3); up(b2, MouseButtons.Right); pump(.2);
                if (st.Recorded.ContainsKey(h)) throw new Exception("recorded by right click");
                st.SetWatch(false, n);
            });
            step("알람 만들기 창: 취소 단추로 닫힘", () =>
            {
                var f = new AlarmComposer(app.TrayIconImage, a => true); f.Show(); pump(.2);
                Func<Form, Button> cancelOf = w => w.Controls.OfType<Button>().First(b => b.Text == "취소");
                cancelOf(f).PerformClick(); pump(.2);
                if (!f.IsDisposed && f.Visible) throw new Exception("composer still open after cancel");
                var g = new AlarmComposer(app.TrayIconImage, a => true); g.Show(); pump(.2);
                g.CancelButton.PerformClick(); pump(.2);   // Esc 가 누르는 단추
                if (!g.IsDisposed && g.Visible) throw new Exception("composer still open after Esc");
                var r = new ReminderForm((d, rule) => { }); r.Show(); pump(.2);
                cancelOf(r).PerformClick(); pump(.2);
                if (!r.IsDisposed && r.Visible) throw new Exception("reminder form still open after cancel");
            });
            step("펫 전원: 끄면 펫 창이 숨고 켜면 돌아옴", () =>
            {
                bool before = PlayOptions.Current.PetPower;
                try
                {
                    app.SetPetPower(true); pump(.2);
                    if (app.PetOff || app.LiveWindows.Count == 0 || app.LiveWindows.Any(w => !w.Visible)) throw new Exception("power on: pets not visible");
                    app.SetPetPower(false); pump(.2);
                    if (!app.PetOff || !app.AllHidden || app.LiveWindows.Any(w => w.Visible)) throw new Exception("power off: pets still visible");
                    app.SetPetPower(true); pump(.2);
                    if (app.PetOff || app.LiveWindows.Any(w => !w.Visible)) throw new Exception("power back on: pets hidden");
                }
                finally { if (PlayOptions.Current.PetPower != before) app.SetPetPower(before); }
            });
            step("트레이 메뉴", () => { var m = typeof(App).GetMethod("BuildMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); using (var menu = (ContextMenuStrip)m.Invoke(app, new object[] { null })) if (menu.Items.Count < 8) throw new Exception("menu items " + menu.Items.Count); });
            step("정각 카드 + 가장자리 빛", () =>
            {
                var n = new HourlyNotice(app.TrayIconImage); n.SetMessage(App.HourlyMessage(DateTime.Now, true, "13:00", "점심 먹으러 가자!", false, "18:00", "x"));
                n.EnableEdgeGlow(true, 1, 1, 1); n.Show(); pump(1.2); screen("ui_notice_glow");
                using (var card = new Bitmap(n.Width, n.Height)) { n.DrawToBitmap(card, new Rectangle(0, 0, n.Width, n.Height)); card.Save(Path.Combine(dir, "ui_notice.png"), ImageFormat.Png); }
                if (!n.GlowRunning) throw new Exception("glow not running"); n.Close(); pump(.3);
            });
            step("펫 걷기 관찰", () => { pump(20); screen("ui_pets"); });
            step("실제 마우스: 클릭·드래그·던지기", () =>
            {
                PlayOptions.Current.Escape = false; PlayOptions.Current.Idle = false;   // 커서를 가까이 대도 도망가지 않게
                PetWindow target = app.LiveWindows.First(w => w.Pet.State == PetState.Walk || w.Pet.State == PetState.Pause);
                // 투명한 곳은 클릭이 통과하므로, 지금 그려진 그림에서 불투명한 점(가운데에 가장 가까운 곳)을 찾아 누른다.
                Func<Point> center = () =>
                {
                    PointF o; using (Bitmap b = Pet.Render(target.Pet.GetLook(app.Now), out o))
                    {
                        Point best = new Point(b.Width / 2, b.Height / 2); double bestD = double.MaxValue;
                        for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++)
                            if (b.GetPixel(x, y).A > 240) { double d = Math.Pow(x - b.Width / 2.0, 2) + Math.Pow(y - b.Height * .55, 2); if (d < bestD) { bestD = d; best = new Point(x, y); } }
                        return new Point(target.Left + best.X, target.Top + best.Y);
                    }
                };
                Action<int> mouse = flags => mouse_event(flags, 0, 0, 0, UIntPtr.Zero);
                Action<Point> go = p => { Cursor.Position = p; pump(.03); };
                // 클릭
                target.Pet.SupportWindow = IntPtr.Zero; target.Pet.Y = target.Pet.Area.Bottom; target.Pet.SetState(PetState.Pause, app); pump(.25);
                go(center()); pump(.1); mouse(2); pump(.05); mouse(4); pump(.1);
                if (target.Pet.State != PetState.Squish && target.Pet.State != PetState.TouchCry && target.Pet.State != PetState.Hop) throw new Exception("click did not react: " + target.Pet.State);
                pump(2.5);
                // 드래그 → 던지기
                target.Pet.SupportWindow = IntPtr.Zero; target.Pet.Y = target.Pet.Area.Bottom; target.Pet.SetState(PetState.Pause, app); pump(.25);
                Point start = center(); go(start); pump(.1); mouse(2); pump(.05);
                for (int i = 1; i <= 8; i++) { go(new Point(start.X + i * 14, start.Y - i * 5)); pump(.012); }
                if (target.Pet.State != PetState.Drag) throw new Exception("drag not started: " + target.Pet.State);
                mouse(4); pump(.05);
                if (target.Pet.State != PetState.Thrown && target.Pet.State != PetState.Falling) throw new Exception("not thrown: " + target.Pet.State);
                pump(1.2); screen("ui_thrown");
                // 우클릭 메뉴
                app.ShowPetMenu(target, new Point(Screen.PrimaryScreen.WorkingArea.Width / 2, 300)); pump(.6); screen("ui_menu");
                keybd_event(0x1B, 0, 0, UIntPtr.Zero); keybd_event(0x1B, 0, 2, UIntPtr.Zero); pump(.4);
            });
            step("종료", () => { app.Quit(); });
            log.AppendLine(failures == 0 ? "ALL OK" : failures + " FAILED");
            File.WriteAllText(Path.Combine(dir, "uitest_result.txt"), log.ToString(), new UTF8Encoding(true));
            return failures == 0 ? 0 : 1;
        }

        private static KeyValuePair<string, Action<Pet, FakeWorld>> K(string name, Action<Pet, FakeWorld> a)
        { return new KeyValuePair<string, Action<Pet, FakeWorld>>(name, a); }

        /// <summary>저장·친밀도 계산·상태 전이 등을 화면 없이 점검. 실패가 있으면 1 을 돌려준다.</summary>
        internal static int SelfTest(string dir)
        {
            Directory.CreateDirectory(dir);
            Paths.StorageOverride = Path.Combine(dir, "data");
            var log = new StringBuilder(); int failures = 0;
            Action<bool, string> check = (ok, name) => { log.AppendLine((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; };
            try
            {
                CharacterArt.Reset(1f);
                DesktopSurfaces.TestProvider = () => new List<SurfaceWindow>(); DesktopSurfaces.TestMouse = () => new Point(-9999, -9999);

                // 설정 저장·불러오기
                var s = new Settings { LunchTime = "11:30", LeaveMsg = "가자", NapMinutes = 7, EdgeGlowTheme = 3, QuietWeekdays = true, QuietStart = 22, QuietEnd = 6, Roster = "1,3" };
                check(s.Save(), "설정 저장");
                var s2 = Settings.Load();
                check(s2.LunchTime == "11:30" && s2.LeaveMsg == "가자" && s2.NapMinutes == 7 && s2.EdgeGlowTheme == 3 && s2.QuietStart == 22 && s2.Roster == "1,3", "설정 불러오기 일치");
                check(Settings.ParseTime("9:05") == new TimeSpan(9, 5, 0) && !Settings.ParseTime("25:00").HasValue && !Settings.ParseTime("abc").HasValue, "시간 해석");
                var quiet = new Settings { QuietWeekdays = true, QuietStart = 9, QuietEnd = 18 };
                check(!quiet.HourAllowed(new DateTime(2026, 10, 21, 10, 0, 0)) && quiet.HourAllowed(new DateTime(2026, 10, 24, 10, 0, 0)) && quiet.HourAllowed(new DateTime(2026, 10, 21, 20, 0, 0)), "평일 제외 시간대");
                check(App.HourlyMessage(new DateTime(2026, 10, 21, 12, 0, 0), true, "12:00", "점심!", true, "18:00", "퇴근!") == "지금은 오후 12시야!\n점심!", "정각 문구+점심 병합");
                check(App.HourlyText(new DateTime(2026, 10, 21, 0, 0, 0)) == "지금은 오전 12시야!", "정각 문구 0시");
                check(OllamaSetup.IsTooOldError("pull model manifest: 412: \n\nThe model you are attempting to pull requires a newer version of Ollama.")
                      && !OllamaSetup.IsTooOldError("pull model manifest: file does not exist") && !OllamaSetup.IsTooOldError(null), "AI 설치: 오래된 Ollama 오류 알아보기");

                var o = new PlayOptions { Walls = false, Speed = 1.5, ClickMode = ClickReaction.Cry, HungerMinutes = 30, PetOpacityPercent = 70, PetSizePercent = 150, FixedX = 123.5, FixedY = 456.25, PetPower = false };
                PlayOptions.Current = o; check(o.Save(), "움직임 설정 저장");
                PlayOptions.Load(); var l = PlayOptions.Current;
                check(!l.Walls && l.Speed == 1.5 && l.ClickMode == ClickReaction.Cry && l.HungerMinutes == 30 && l.PetOpacityPercent == 70 && l.PetSizePercent == 150 && l.FixedX == 123.5 && l.FixedY == 456.25, "움직임 설정 불러오기 일치");
                check(!l.PetPower && new PlayOptions().PetPower, "펫 전원: 기본은 켜짐, 꺼 둔 상태는 저장·불러오기로 유지");

                // 친구 기록
                PlayOptions.Current = new PlayOptions { HungerMinutes = 60 };
                var d = FriendData.Current = new FriendData();
                d.AdvanceLife(60);
                check(d.Affinity == 1 && d.TogetherSeconds == 60, "함께한 1분 = 친밀도 +1");
                check(Math.Abs(d.Satiety - (100 - 60 * 80.0 / 3600)) < 1e-6, "포만감 감소량");
                d.Satiety = 78; check(d.TrySnack() && d.Satiety == 100 && d.Affinity == 6, "간식: 포만감 +25(최대 100), 친밀도 +5");
                check(!d.TrySnack(), "배가 꽉 차면 간식 거절");
                d.Satiety = 80; check(!d.TrySnack(), "포만감 80 이상이면 간식 거절");
                d.Affinity = 250; d.CelebratedHundreds = 1; check(d.TakeMilestone() && d.CelebratedHundreds == 2 && !d.TakeMilestone(), "100 단위 꽃다발 한 번씩");
                d.Satiety = 10; check(d.Hungry && d.VeryHungry && !d.Starving && Math.Abs(d.HungerSpeedScale - .42) < 1e-6, "포만감 10 이하 = 매우 배고픔(속도 42%)");
                d.Satiety = 15; check(d.Hungry && !d.VeryHungry && Math.Abs(d.HungerSpeedScale - .75) < 1e-6, "포만감 20 이하 = 배고픔(속도 75%)");
                d.Satiety = 5; check(d.Hungry && d.VeryHungry && d.Starving && d.HungerSpeedScale == 0, "포만감 5 이하 = 탈진(속도 0)");
                d.Satiety = 100; check(!d.Hungry && !d.VeryHungry && !d.Starving && d.HungerSpeedScale == 1, "포만감 충분 = 정상 속도");
                PlayOptions.Current.HungerMinutes = 0; d.Satiety = 100; check(d.Hungry, "0분 = 항상 우는 표정");
                PlayOptions.Current.HungerMinutes = 60;
                d.Todos.Add(new TodoItem { Text = "첫 할 일", Done = true }); d.Todos.Add(new TodoItem { Text = "둘째", Done = false });
                d.Notes = "메모\n두 줄"; d.Discover("dizzy");
                check(d.Save(), "기록 저장");
                FriendData.Load(); var r = FriendData.Current;
                check(r.Affinity == d.Affinity && r.Todos.Count == 2 && r.Todos[0].Done && !r.Todos[1].Done && r.Notes == "메모\n두 줄" && r.Discoveries.Contains("dizzy"), "기록 불러오기 일치(옛 할 일·메모·비밀)");
                d.Save(); check(File.Exists(FriendData.DataPath + ".bak"), "이전 기록 .bak 유지");
                FriendData.Current = new FriendData();

                // 스프라이트
                for (int c = 0; c < CharacterArt.Count; c++)
                {
                    var a = CharacterArt.Get(c);
                    var probe = new Pet(new FakeWorld(), c, 640, 400);
                    PointF org; Pet.Look stand = probe.GetLook(0);
                    using (Bitmap bmp = Pet.Render(stand, out org))
                    {
                        Rectangle ink = CharacterArt.Bounds(bmp);
                        check(a.W > 30 && a.H > 30 && ink.Height > a.H * .8 && ink.Width > a.W * .4, CharacterArt.Infos[c].Name + " 그림 생성");
                    }
                }

                // 상태 전이
                PlayOptions.Current = new PlayOptions { Escape = false, Idle = false, Windows = false };
                var w = new FakeWorld(); var pet = new Pet(w, 1, 640, 40);
                var all = new List<Pet> { pet }; var seen = new HashSet<PetState>();
                Action<double> run = secs => { for (double t = 0; t < secs; t += .03) { w.Now += .03; pet.Update(w, .03, all, 0); seen.Add(pet.State); } };
                run(.3); check(pet.State == PetState.Falling, "시작은 날개 퍼덕 낙하");
                run(25); check(pet.Y == w.Areas[0].Bottom && pet.State != PetState.Falling, "바닥에 착지");
                pet.Poke(w); check(pet.State == PetState.Squish, "첫 클릭 = 눌리기"); run(1);
                pet.Poke(w); check(pet.State == PetState.TouchCry, "둘째 클릭 = 달달 떨며 울기"); run(3);
                pet.Poke(w); check(pet.State == PetState.Hop, "셋째 클릭 = 폴짝"); run(1.5);
                FriendData.Current.Satiety = 50; pet.Feed(w); check(pet.State == PetState.Eating && FriendData.Current.Satiety == 75, "간식 먹기"); run(4);
                pet.BeginDrag(w, pet.X, pet.Y - 20); check(pet.State == PetState.Drag, "드래그 시작");
                for (int i = 0; i < 6; i++) { w.Now += .016; pet.DragTo(w, pet.X + pet.DragPx + 30, pet.Y + pet.DragPy - 4); }
                pet.EndDrag(w); check(pet.State == PetState.Thrown, "빠르게 놓으면 던지기"); run(.8); run(25);
                check(seen.Contains(PetState.Thrown) && pet.State != PetState.Thrown && pet.Y <= w.Areas[0].Bottom + .1, "던진 뒤 날개 퍼덕이며 착지");
                pet.Demonstrate(w, "dizzy"); check(pet.State == PetState.Dizzy && FriendData.Current.Discoveries.Contains("dizzy"), "어질어질"); run(5);
                pet.SetState(PetState.Pause, w); pet.FallAsleep(w); check(pet.State == PetState.Sleep, "낮잠"); pet.WakeUp(w); check(pet.State == PetState.Waking, "깨어남");
                for (int i = 0; i < 3; i++) { pet.BeginDrag(w, pet.X, pet.Y - 20); for (int k = 0; k < 6; k++) { w.Now += .016; pet.DragTo(w, pet.X + pet.DragPx + 30, pet.Y + pet.DragPy - 4); } pet.EndDrag(w); run(.6); }
                check(seen.Contains(PetState.PilotRise) && FriendData.Current.Discoveries.Contains("pilot"), "비밀 조종사: 10초 안 3번 던지기");
                run(20); check(pet.State == PetState.Ceiling || seen.Contains(PetState.Ceiling), "조종사가 천장까지 올라감");

                // 오래 돌려서 벽·천장·쉬기가 모두 나오는지
                PlayOptions.Current = new PlayOptions { Escape = false, Idle = true };
                var w2 = new FakeWorld(); w2.Rng = new Random(3); var p2 = new Pet(w2, 2, 300, 40); var all2 = new List<Pet> { p2 }; var seen2 = new HashSet<PetState>();
                for (double t = 0; t < 600; t += .03) { w2.Now += .03; p2.Update(w2, .03, all2, 0); seen2.Add(p2.State); }
                check(seen2.Contains(PetState.Walk) && seen2.Contains(PetState.Pause) && seen2.Contains(PetState.Wall) && seen2.Contains(PetState.Ceiling) && seen2.Contains(PetState.Resting), "10분 시뮬레이션: 걷기·쉬기·벽·천장·앉기 모두 등장");
                check(p2.X >= -50 && p2.X <= 1330 && p2.Y >= -50 && p2.Y <= 770, "화면 밖으로 나가지 않음");

                // 벽 타기 ON · 천장 타기 OFF · 졸졸 ON: 벽 꼭대기에서 떨어진 뒤 가장자리에 갇히지 않고 화면 안쪽으로 걸어 나옴
                PlayOptions.Current = new PlayOptions { Walls = true, Ceiling = false, Windows = true, Idle = true, Escape = false, Follow = true };
                DesktopSurfaces.TestMouse = () => new Point(640, 360);
                try
                {
                    foreach (int side in new[] { 1, -1 })
                    {
                        var wn = new FakeWorld { Rng = new Random(11 + side) };
                        double startX = side > 0 ? wn.Areas[0].Right - 40 : wn.Areas[0].Left + 40;
                        var pn = new Pet(wn, 0, startX, wn.Areas[0].Bottom) { Area = wn.Areas[0], Dir = side };
                        pn.SetState(PetState.Walk, wn);
                        bool climbed = false, fellInside = false, escapedEdge = false, allInsideX = true;
                        for (double t = 0; t < 45; t += .03)
                        {
                            wn.Now += .03; pn.Update(wn, .03, null, 0);
                            if (pn.State == PetState.Wall) climbed = true;
                            if (climbed && pn.State == PetState.Falling)
                            {
                                bool insideX = pn.X >= wn.Areas[0].Left + pn.SideReach - .5 && pn.X <= wn.Areas[0].Right - pn.SideReach + .5;
                                allInsideX &= insideX;
                                if (insideX && pn.Dir == -side) fellInside = true;
                            }
                            if (fellInside && pn.State == PetState.Walk && (side > 0 ? pn.X < wn.Areas[0].Right - 150 : pn.X > wn.Areas[0].Left + 150))
                                escapedEdge = true;
                        }
                        check(climbed && fellInside && allInsideX && escapedEdge, "벽 타기 ON + 천장 타기 OFF: 꼭대기 낙하 후 가장자리에 갇히지 않고 안쪽으로 이동(" + side + ")");
                    }
                }
                finally { DesktopSurfaces.TestMouse = null; }

                // 포만감 매우 낮음(10 이하 / 5 이하): 속도 감소 · 벽타기 금지 · 제자리 정지 · 힘없는 액션
                PlayOptions.Current = new PlayOptions { Walls = true, Ceiling = true, Windows = true, Escape = true, Idle = true };
                var wl = new FakeWorld { Rng = new Random(7) };
                var pl = new Pet(wl, 0, 640, 720) { Area = wl.Areas[0], Dir = 1 };
                FriendData.Current.Satiety = 100; pl.SetState(PetState.Walk, wl);
                double xStart = pl.X; for (double t = 0; t < 1.0; t += .02) { wl.Now += .02; pl.Update(wl, .02, null, 0); }
                double distFull = Math.Abs(pl.X - xStart);
                FriendData.Current.Satiety = 15; pl.X = 640; pl.Dir = 1; pl.SetState(PetState.Walk, wl);
                for (double t = 0; t < 1.0; t += .02) { wl.Now += .02; pl.Update(wl, .02, null, 0); }
                double distHungry = Math.Abs(pl.X - 640);
                FriendData.Current.Satiety = 10; pl.X = 640; pl.Dir = 1; pl.SetState(PetState.Walk, wl);
                for (double t = 0; t < 1.0; t += .02) { wl.Now += .02; pl.Update(wl, .02, null, 0); }
                double distWeak = Math.Abs(pl.X - 640);
                check(distHungry < distFull * .82 && distWeak < distHungry * .65 && distWeak > 2, "포만감 단계별 이동 속도 감소 (20 이하→10 이하)");
                pl.X = wl.Areas[0].Right - 40; pl.Dir = 1; pl.SetState(PetState.Walk, wl);
                var seenLow = new HashSet<PetState>(); var seenWeakRests = new HashSet<Pet.RestPose>();
                for (double t = 0; t < 40; t += .05) { wl.Now += .05; pl.Update(wl, .05, null, 0); seenLow.Add(pl.State); if (pl.State == PetState.Resting) seenWeakRests.Add(pl.restPose); }
                check(!seenLow.Contains(PetState.Wall) && !seenLow.Contains(PetState.Ceiling) && !seenLow.Contains(PetState.WindowClimb), "포만감 10 이하: 벽·천장·창 타기 안 함");
                check(seenWeakRests.Count > 0 && seenWeakRests.All(Pet.IsWeakRestPose), "포만감 10 이하: 힘없는 쉬기 동작만 선택");
                pl.Demonstrate(wl, "wall");
                check(pl.State != PetState.Wall, "포만감 10 이하: 벽 타기 시범도 거절");
                FriendData.Current.Satiety = 100; pl.Demonstrate(wl, "wall"); check(pl.State == PetState.Wall, "벽 타던 중 포만감 떨어지면 낙하 준비");
                FriendData.Current.Satiety = 8; wl.Now += .05; pl.Update(wl, .05, null, 0);
                check(pl.State == PetState.Falling, "벽 타던 중 포만감 10 이하가 되면 떨어짐");
                pl.Y = 720; pl.SetState(PetState.Pause, wl);
                FriendData.Current.Satiety = 5; pl.X = 500; pl.SetState(PetState.Walk, wl);
                check(pl.State == PetState.Pause, "포만감 5 이하: 걷기 진입 시 제자리에 멈춤");
                var seenStarv = new HashSet<PetState>(); var seenStarvRests = new HashSet<Pet.RestPose>();
                for (double t = 0; t < 45; t += .05) { wl.Now += .05; pl.Update(wl, .05, null, 0); seenStarv.Add(pl.State); if (pl.State == PetState.Resting) seenStarvRests.Add(pl.restPose); }
                check(Math.Abs(pl.X - 500) < 1e-6 && !seenStarv.Contains(PetState.Walk) && !seenStarv.Contains(PetState.Wall) && seenStarvRests.Count > 0 && seenStarvRests.All(Pet.IsWeakRestPose), "포만감 5 이하: 한 곳에 멈춰 힘없는 동작만 반복");
                pl.Feed(wl); check(pl.State == PetState.Eating && FriendData.Current.Satiety == 30, "간식 주면 먹고 포만감 회복");
                for (double t = 0; t < 4; t += .05) { wl.Now += .05; pl.Update(wl, .05, null, 0); }
                check(pl.State == PetState.Walk, "포만감 회복 후 다시 걷기");
                FriendData.Current = new FriendData();

                // 근무복: 설정 저장 · 동작 4가지 · 규격 가드
                PlayOptions.Current = new PlayOptions { Outfit = true };
                PlayOptions.Current.Save(); PlayOptions.Load();
                check(PlayOptions.Current.Outfit, "근무복 설정 저장·불러오기");
                PlayOptions.Current = new PlayOptions { Escape = false, Idle = false, Windows = false };
                var wo = new FakeWorld(); var po = new Pet(wo, 0, 640, 720) { Area = wo.Areas[0] };
                po.SetState(PetState.Pause, wo); po.X = 640; po.Y = 720;
                foreach (string kind in new[] { "guide", "bow" })
                {
                    po.Demonstrate(wo, kind);
                    bool outfitOn = true, drew = true, entered = po.State == PetState.Resting;
                    for (double t = 0; t < 4; t += .1)
                    {
                        wo.Now += .1; po.Update(wo, .1, null, 0);
                        Pet.Look lk = po.GetLook(wo.Now);
                        outfitOn &= lk.Pose.Outfit && lk.Work;
                        try { PointF oo; using (Bitmap bo = Pet.Render(lk, out oo)) drew &= bo.Width > 10; } catch { drew = false; }
                    }
                    check(entered && outfitOn && drew, "근무복 동작: " + kind);
                    for (double t = 0; t < 12; t += .1) { wo.Now += .1; po.Update(wo, .1, null, 0); }
                    check(po.State != PetState.Resting && !po.WearsOutfit, "근무복 동작이 끝나면 다시 벗음: " + kind);
                }
                // 부캉이 전용 시그니처 · 홍보대사 동작 점검
                var bk = new Pet(wo, 1, 640, 720) { Area = wo.Areas[0] };
                bk.SetState(PetState.Pause, wo); bk.X = 640; bk.Y = 720;
                foreach (var sig in Signatures.All[1])
                {
                    bk.RequestSignature(wo, sig);
                    bool entered = bk.State == PetState.Resting, drew = true;
                    for (double t = 0; t < 2.2; t += .1)
                    {
                        wo.Now += .1; bk.Update(wo, .1, null, 0);
                        try { PointF oo; using (Bitmap bo = Pet.Render(bk.GetLook(wo.Now), out oo)) drew &= bo.Width > 10 && bo.Width < 4000; } catch { drew = false; }
                    }
                    check(entered && drew, "부캉이 시그니처: " + sig.Name);
                    for (double t = 0; t < 6; t += .1) { wo.Now += .1; bk.Update(wo, .1, null, 0); }
                }
                check(ChatPersona.SystemPrompt(new DateTime(2026, 1, 5, 10, 3, 0), 1).Contains("부캉이"), "부캉이 AI 대화 페르소나");
                Func<float, int> inkH = sq =>
                { using (var bm = new Bitmap(300, 400)) using (var gg = Graphics.FromImage(bm)) { CharacterArt.Quality(gg); gg.Clear(Color.Transparent); gg.TranslateTransform(150, 380); Rig.Draw(gg, new Pose { Squash = sq }, 1.2f, false); return CharacterArt.Bounds(bm).Height; } };
                check(inkH(.4f) == inkH(.05f) && inkH(-.4f) == inkH(-.05f), "규격 가드: 몸이 눌리거나 늘어나는 정도 5% 한도");

                // 가이드북(docs/GUIDE_RULES.md): 색·선·비율 점검과 응용동작(p27~36) 열네 가지
                var gw = new List<string>(); var gi = new List<string>(); BugiGuide.Check(gw, gi);
                check(gw.Count == 0, "가이드북 점검(색·선 굵기·비율)" + (gw.Count == 0 ? "" : ": " + string.Join(" / ", gw)));
                PlayOptions.Current.Outfit = false;
                foreach (Skit sk in Enum.GetValues(typeof(Skit)))
                {
                    po.Demonstrate(wo, "skit:" + sk);
                    bool entered = po.State == PetState.Resting, drew = true, outfitOff = true;
                    for (double t = 0; t < SkitLen(sk) + .5; t += .1)
                    {
                        wo.Now += .1; po.Update(wo, .1, null, 0);
                        Pet.Look lk = po.GetLook(wo.Now);
                        outfitOff &= !lk.Work;
                        try { PointF oo; using (Bitmap bo = Pet.Render(lk, out oo)) drew &= bo.Width > 10 && bo.Width < 4000; } catch { drew = false; }
                    }
                    check(entered && drew && outfitOff && po.State != PetState.Resting, "가이드북 응용동작: " + sk);
                }
                // 근무복 종류(p18)와 아이템(p20): 설정 저장 · 모든 조합이 그려짐
                PlayOptions.Current = new PlayOptions { Outfit = true, OutfitStyle = OutfitStyle.Safety, AccBag = true, AccVisor = true, AccSunglasses = true };
                PlayOptions.Current.Save(); PlayOptions.Load();
                check(PlayOptions.Current.OutfitStyle == OutfitStyle.Safety && PlayOptions.Current.AccBag && PlayOptions.Current.AccVisor && PlayOptions.Current.AccSunglasses, "근무복 종류·아이템 설정 저장·불러오기");
                bool stylesOk = true;
                foreach (OutfitStyle os in Enum.GetValues(typeof(OutfitStyle)))
                    foreach (bool acc in new[] { false, true })
                    {
                        PlayOptions.Current = new PlayOptions { Outfit = true, OutfitStyle = os, AccBag = acc, AccVisor = acc, AccSunglasses = acc };
                        foreach (float turn in new[] { 0f, -.5f, -1f })
                        {
                            Pet.Look lk = po.GetLook(wo.Now); lk.Pose.Turn = turn;
                            try { PointF oo; using (Bitmap bo = Pet.Render(lk, out oo)) stylesOk &= bo.Width > 10 && lk.Pose.OutfitKind == (int)os; } catch { stylesOk = false; }
                        }
                    }
                check(stylesOk, "근무복 6종 × 아이템 장착/해제 × 방향이 모두 그려짐");
                PlayOptions.Current = new PlayOptions { Escape = false, Idle = false, Windows = false };
                var rpa = new ReplyParser(); rpa.Feed("[인사] 안녕! 오랜만이야.");
                var rpb = new ReplyParser(); rpb.Feed("[기쁨] 이번 주말에 부산 여행 가자!");
                var rpc = new ReplyParser(); rpc.Feed("[슬픔] 부산 얘기를 하니 눈물 나.");
                Skit sk0;
                check(rpa.Mood == ChatMood.Greet && rpb.Mood == ChatMood.Travel && rpc.Mood == ChatMood.Sad, "AI 대화: 감정 태그와 화제(여행·부산)가 응용동작 감정으로 이어지고, 슬픔은 덮어쓰지 않음");
                bool allMapped = true;
                foreach (ChatMood cm in new[] { ChatMood.Greet, ChatMood.Laugh, ChatMood.Love, ChatMood.Cheer, ChatMood.Praise, ChatMood.Comfort, ChatMood.Proud, ChatMood.Busy, ChatMood.Travel, ChatMood.Explain, ChatMood.Hero, ChatMood.Okay, ChatMood.Busan }) allMapped &= Pet.MoodSkit(cm, out sk0);
                check(allMapped && !Pet.MoodSkit(ChatMood.Joy, out sk0), "AI 대화 감정마다 가이드북 응용동작이 정해져 있음");
                var pc = new Pet(wo, 0, 640, 720) { Area = wo.Areas[0] }; pc.SetState(PetState.Pause, wo); pc.X = 640; pc.Y = 720;
                bool chatOk = true;
                foreach (ChatMood cm in Enum.GetValues(typeof(ChatMood)))
                {
                    pc.BeginChat(wo, 1); pc.ChatThink(); pc.ChatSay(cm, "테스트 말풍선이야.", true);
                    for (int i = 0; i < 60; i++) { wo.Now += .02; pc.Update(wo, .02, null, 0); }
                    try { PointF oo; using (Bitmap bo = Pet.Render(pc.GetLook(wo.Now), out oo)) chatOk &= bo.Width > 10 && bo.Width < 4000; } catch { chatOk = false; }
                    pc.EndChat(wo);
                }
                check(chatOk, "AI 대화: 모든 감정(응용동작 포함)이 그려짐");

                // 위치 고정
                PlayOptions.Current = new PlayOptions { PositionFixed = true };
                var w3 = new FakeWorld(); var p3 = new Pet(w3, 0, 500, 300); var all3 = new List<Pet> { p3 };
                double px = p3.X, py = p3.Y; for (double t = 0; t < 20; t += .03) { w3.Now += .03; p3.Update(w3, .03, all3, 0); }
                check(Math.Abs(p3.X - px) < 1 && Math.Abs(p3.Y - py) < 1 || p3.Y <= w3.Areas[0].Bottom, "위치 고정 중에는 자동 이동 없음");

                // 두 마리 충돌 분리
                PlayOptions.Current = new PlayOptions { Escape = false, Idle = false, Windows = false };
                var w4 = new FakeWorld(); var a1 = new Pet(w4, 0, 500, 700) { Area = w4.Areas[0] }; var a2 = new Pet(w4, 1, 505, 700) { Area = w4.Areas[0] };
                a1.SetState(PetState.Pause, w4); a2.SetState(PetState.Pause, w4); a1.Y = a2.Y = 720;
                App.ResolveCompanionContacts(w4, new List<Pet> { a1, a2 }, null);
                check(a2.ContactBody(0).Left - a1.ContactBody(0).Right > -1 || Math.Abs(a1.X - a2.X) > a1.Art.W * .6, "겹친 친구는 서로 떨어짐");

                // 여러 마리가 오래 돌아다니며 마주쳐도 서로 엉킨 채 오래 머물지 않음 (마우스 없음 · 마우스를 피해 도망 · 마우스를 따라감)
                {
                    double worst = 0; string worstInfo = "";
                    foreach (int mode in new[] { 0, 1, 2 })
                        foreach (int count in new[] { 2, 3, 5 })
                            foreach (int seed in new[] { 1, 2, 3, 4 })
                            {
                                PlayOptions.Current = new PlayOptions { Escape = mode == 1, Follow = mode == 2, Idle = true, Windows = false, Walls = seed % 2 == 0 };
                                int mouseX = 200 + seed * 170; double clock = 0;
                                DesktopSurfaces.TestMouse = mode == 0 ? (Func<Point>)(() => new Point(-9999, -9999)) : () => new Point(mouseX + (int)(Math.Sin(clock / 9) * (seed % 2 == 0 ? 250 : 0)), 690);
                                var wt = new FakeWorld { Rng = new Random(seed) };
                                if (count == 5) wt.Areas[0] = new Rectangle(0, 0, 900, 720);
                                var tall = new List<Pet>();
                                for (int k = 0; k < count; k++) tall.Add(new Pet(wt, k % CharacterArt.Count, 100 + k * 130, 720) { Area = wt.Areas[0] });
                                var stuck = new double[count, count];
                                for (double t = 0; t < 1800; t += .03)
                                {
                                    wt.Now += .03; clock = wt.Now;
                                    var old = tall.ToDictionary(q => q, q => new PointF((float)q.X, (float)q.Y));
                                    var before = tall.Select(q => q.X).ToArray();
                                    foreach (Pet q in tall) q.Update(wt, .03, tall, 0);
                                    App.ResolveCompanionContacts(wt, tall, old);
                                    for (int i = 0; i < count; i++)
                                        for (int j2 = i + 1; j2 < count; j2++)
                                        {
                                            Pet pa = tall[i], pb = tall[j2];
                                            bool skip = pa.IsGhost(wt.Now) || pb.IsGhost(wt.Now) || pa.State == PetState.Falling || pb.State == PetState.Falling || pa.State == PetState.Wall || pb.State == PetState.Wall || pa.State == PetState.WindowClimb || pb.State == PetState.WindowClimb;
                                            double near = (pa.Art.W + pb.Art.W) * .85 * .5 * 1.3 + 6;
                                            bool moved = Math.Abs(pa.X - before[i]) + Math.Abs(pb.X - before[j2]) > .3;
                                            bool ovl = pa.ContactBody(wt.Now).IntersectsWith(pb.ContactBody(wt.Now));
                                            bool jam = !skip && Math.Abs(pa.X - pb.X) < near && (ovl || (!moved && (pa.State == PetState.Walk || pb.State == PetState.Walk)));
                                            stuck[i, j2] = jam ? stuck[i, j2] + .03 : 0;
                                            if (stuck[i, j2] > worst) { worst = stuck[i, j2]; worstInfo = "모드" + mode + " " + count + "마리 seed " + seed + " [" + pa.Art.Id + "(" + pa.State + ")+" + pb.Art.Id + "(" + pb.State + ")]"; }
                                        }
                                }
                            }
                    DesktopSurfaces.TestMouse = null;
                    log.AppendLine("  [정보] 붙은 채 정지 최장 " + worst.ToString("F1") + "초 " + worstInfo);
                    check(worst < 4, "여러 마리가 붙은 채 오래 정지하지 않음 (최장 " + worst.ToString("F1") + "초)");
                }

                // ── AI 대화 ──
                var j = Json.Parse("{\"message\":{\"role\":\"assistant\",\"content\":\"[기쁨] \\\"안녕\\\"\\n\\u0041\"},\"done\":false,\"n\":-1.5e2}");
                check(Json.Str(Json.Get(j, "message"), "content") == "[기쁨] \"안녕\"\nA" && !Json.Flag(j, "done") && (double)Json.Get(j, "n") == -150, "JSON 읽기(이스케이프·숫자)");
                check(Json.Parse("{깨진") == null, "깨진 JSON 은 null");
                check(Json.Quote("a\"b\n") == "\"a\\\"b\\n\"", "JSON 문자열 만들기");
                var rp = new ReplyParser(); bool c1 = rp.Feed("[") ; rp.Feed("기"); rp.Feed("쁨"); bool c2 = rp.Feed("] 안녕"); rp.Feed("! 반가워. 또 왔네! 네 번째 문장");
                check(!c1 && c2 && rp.Mood == ChatMood.Joy && rp.Text == "안녕! 반가워. 또 왔네!", "감정 태그가 토큰으로 쪼개져 와도 읽고, 세 문장까지만 남김: " + rp.Text);
                check(ReplyParser.Clean("**좋아** 😀 (웃으며) 그래\n부기: 둘째 줄") == "좋아 그래", "마크다운·이모지·괄호 지문·둘째 줄 제거: " + ReplyParser.Clean("**좋아** 😀 (웃으며) 그래\n부기: 둘째 줄"));
                var rq = new ReplyParser(); rq.Feed("힘들었겠다 ㅠㅠ");
                check(rq.Mood == ChatMood.Sad && !rq.HasTag, "태그가 없으면 글에서 감정을 짐작");
                check(ReplyParser.Clean(new string('가', 300)).Length <= ReplyParser.MaxChars + 1, "너무 긴 말은 잘라 냄");
                string sys1 = ChatPersona.SystemPrompt(new DateTime(2026, 1, 5, 10, 3, 0)), sys2 = ChatPersona.SystemPrompt(new DateTime(2026, 1, 5, 10, 47, 0));
                check(sys1 == sys2 && sys1.Contains("AI"), "시스템 프롬프트는 같은 시간대에 안 바뀜(캐시 유지) · AI 라고 밝힘");
                ChatOptions.Current = new ChatOptions { Model = "test:1b", Sound = true, Proactive = true, ProactiveMinutes = 15, Hotkey = false }; ChatOptions.Current.Save(); ChatOptions.Load();
                check(ChatOptions.Current.Model == "test:1b" && ChatOptions.Current.Sound && ChatOptions.Current.Proactive && ChatOptions.Current.ProactiveMinutes == 15 && !ChatOptions.Current.Hotkey, "AI 대화 설정 저장·불러오기");
                // EXAONE: 대화 상대·키 저장(키는 암호화돼 파일에 평문이 없어야 함) · 요청 본문 · 한 줄 끊기
                ChatOptions.Current = new ChatOptions { Provider = "exaone", ExaoneKey = "test-key-123", ExaoneEffort = "high" }; ChatOptions.Current.Save(); ChatOptions.Load();
                check(ChatOptions.Current.IsExaone && ChatOptions.Current.ExaoneKey == "test-key-123" && ChatOptions.Current.ExaoneEffort == "high" && !File.ReadAllText(ChatOptions.FilePath).Contains("test-key-123"), "EXAONE 설정 저장·불러오기(키 암호화)");
                string exBody = ExaoneClient.Body(ChatOptions.Current, new List<ChatMessage> { new ChatMessage("system", "s"), new ChatMessage("user", "안녕") }, true, 170, .85);
                object exJ = Json.Parse(exBody);
                check(Json.Str(exJ, "model") == "k-exaone_v2.5" && Json.Flag(exJ, "stream") && (double)Json.Get(exJ, "max_tokens") > 170 && Json.Str(exJ, "reasoning_effort") == "high" && ((List<object>)Json.Get(exJ, "messages")).Count == 2, "EXAONE 요청 본문: " + exBody);
                var lc = new ExaoneClient.LineCutter(); string cutText = lc.Feed("\n\n") + lc.Feed("[기쁨] 안") + lc.Feed("녕!\n부기: 또") + lc.Feed("더");
                check(cutText == "[기쁨] 안녕!" && lc.Ended, "EXAONE 답: 앞 빈 줄 건너뛰고 첫 줄에서 끊기: " + cutText);
                check(ExaoneClient.Probe(new ChatOptions { Provider = "exaone" , ExaoneKey = "" }) == ProbeResult.NoKey || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EXAONE_API_KEY")), "EXAONE 키가 없으면 NoKey");
                ChatOptions.Current = new ChatOptions();
                ChatMemory.Reload(); ChatMemory.Clear(); int added = ChatMemory.Add(new[] { "- 이름은 민수", "- 이름은 민수야", "없음", "- 고양이를 키운다" });
                check(added == 2 && ChatMemory.Facts.Count == 2, "기억: 중복·'없음' 거르고 저장 (" + added + ")");
                ChatMemory.Reload(); check(ChatMemory.Facts.Count == 2, "기억 파일 다시 읽기"); ChatMemory.Clear();
                // 일기장: 정리·중복·저장·삭제, 모델 답변에서 문장만 남기기, 답장 칩
                ChatDiary.Reload(); ChatDiary.Clear(); var day = new DateTime(2026, 10, 29, 21, 30, 0);
                check(ChatDiary.Add(day, "오늘 너랑 점심 얘기를 했어.	새우깡 먹고 싶다") && !ChatDiary.Add(day.AddHours(1), "오늘 너랑 점심 얘기를 했어. 새우깡 먹고 싶다") && !ChatDiary.Add(day, "짧아") && !ChatDiary.Add(day, new string('가', 200)), "일기: 탭 정리 · 같은 날 같은 문장 · 너무 짧거나 긴 글 거르기");
                ChatDiary.Reload(); check(ChatDiary.Entries.Count == 1 && ChatDiary.Entries[0].When == day && ChatDiary.Entries[0].Text.StartsWith("오늘 너랑"), "일기 파일 다시 읽기");
                ChatDiary.RemoveAt(0); ChatDiary.Reload(); check(ChatDiary.Entries.Count == 0, "일기 지우기");
                check(ChatPersona.ParseDiary("**일기:** \"오늘은 네가 말 걸어줘서 기뻤어!\"\n둘째 줄") == "오늘은 네가 말 걸어줘서 기뻤어!" && ChatPersona.ParseDiary("[기쁨] 오늘 네가 와서 신났어") == "오늘 네가 와서 신났어" && ChatPersona.ParseDiary("") == null && ChatPersona.ParseDiary("네") == null, "일기 답변에서 문장만 남김");
                var chipRng = new Random(3); bool chipsOk = true;
                foreach (ChatMood cm in Enum.GetValues(typeof(ChatMood))) { var cs = ChatPersona.Chips(cm, false, chipRng); chipsOk &= cs.Length >= 2 && cs.Length <= 3 && cs.Distinct().Count() == cs.Length && cs.Sum(c => c.Length) <= 17 && cs.All(c => c.Length > 1); }
                var starters = ChatPersona.Chips(ChatMood.Joy, true, chipRng);
                check(chipsOk && starters.Length >= 2 && ChatPersona.Chips(ChatMood.Neutral, true, chipRng).All(c => c.Length > 1), "답장 칩: 감정마다 서로 다른 짧은 말 2~3개(알약 폭 안) · 첫머리엔 말문 트는 칩");
                var co = new ChatOptions { Chips = false, Diary = false }; co.Save(); ChatOptions.Load();
                check(!ChatOptions.Current.Chips && !ChatOptions.Current.Diary, "칩·일기 설정 저장·불러오기"); new ChatOptions().Save(); ChatOptions.Load();
                // 대화 모드 5가지: 구성 · 프롬프트 반영 · 길이 상한 · 저장
                check(ChatMode.All.Length == 5 && ChatMode.All.Select(x => x.Key).Distinct().Count() == 5 && ChatMode.All.All(x => x.Name.Length > 0 && x.Desc.Length > 0 && x.Cue.Length > 0 && x.Style.Length > 20 && x.MaxSentences >= 1 && x.MaxChars >= 30), "대화 모드: 다섯 가지가 빠짐없이 정의됨");
                var modePrompts = new List<string>(); var keepMode = ChatOptions.Current.Mode; bool modeOk = true;
                for (int mi = 0; mi < ChatMode.All.Length; mi++)
                {
                    ChatOptions.Current.Mode = mi; var built = ChatPersona.Build(new List<ChatMessage> { new ChatMessage("user", "안녕") }, new DateTime(2026, 1, 5, 10, 3, 0));
                    string sys0 = built[0].Content; modePrompts.Add(sys0);
                    modeOk &= sys0.Contains(ChatMode.All[mi].Style) && built[built.Count - 1].Content == "안녕" && (mi == 0 || (built.Count == 1 + 4 + ChatMode.All[mi].Examples.Length + 1 && built.Contains(ChatMode.All[mi].Examples[0])));
                    modeOk &= ChatPersona.SystemPrompt(new DateTime(2026, 1, 5, 10, 47, 0)) == sys0;
                }
                ChatOptions.Current.Mode = keepMode;
                check(modeOk && modePrompts.Distinct().Count() == 5, "대화 모드: 모드마다 시스템 프롬프트와 예시가 달라지고 같은 시간대에선 안 바뀜");
                string longText = "첫째 문장이야. 둘째 문장이야. 셋째 문장이야. 넷째 문장이야. 다섯째 문장이야.";
                check(ReplyParser.Clean(longText, ChatMode.All[4].MaxSentences, ChatMode.All[4].MaxChars) == "첫째 문장이야." && ReplyParser.Clean(longText, ChatMode.All[2].MaxSentences, ChatMode.All[2].MaxChars) == "첫째 문장이야. 둘째 문장이야. 셋째 문장이야. 넷째 문장이야.", "대화 모드: 모드마다 문장 수 상한이 다름(간결 1 · 설명 4)");
                ChatOptions.Current = new ChatOptions { Mode = 3 }; ChatOptions.Current.Save(); ChatOptions.Load(); bool m3 = ChatOptions.Current.Mode == 3 && ChatMode.Current.Key == "humor";
                File.WriteAllText(ChatOptions.FilePath, "Mode=99" + Environment.NewLine); ChatOptions.Load();
                check(m3 && ChatOptions.Current.Mode == 0 && ChatMode.Current.Key == "friend", "대화 모드: 저장·불러오기, 이상한 값은 친구 모드"); new ChatOptions().Save(); ChatOptions.Load();
                // 말 걸기 단축키: 규칙(3키 이상 · 겹침 금지) · 저장 · 입력란이 키를 하나씩 받아 + 를 보이며 기다리는 동작
                string hkWhy;
                check(ChatHotkey.Valid(ChatHotkey.Ctrl | ChatHotkey.Alt, (uint)'B', out hkWhy) && ChatHotkey.Valid(ChatHotkey.Ctrl | ChatHotkey.Alt | ChatHotkey.Shift, (uint)Keys.F5, out hkWhy)
                    && !ChatHotkey.Valid(ChatHotkey.Ctrl, (uint)'B', out hkWhy) && !ChatHotkey.Valid(0, (uint)'B', out hkWhy) && !ChatHotkey.Valid(ChatHotkey.Ctrl | ChatHotkey.Alt, 0, out hkWhy)
                    && !ChatHotkey.Valid(ChatHotkey.Ctrl | ChatHotkey.Alt, (uint)'N', out hkWhy) && !ChatHotkey.Valid(ChatHotkey.Ctrl | ChatHotkey.Shift, (uint)'F', out hkWhy), "단축키 규칙: 보조키 2개 + 글쇠(3키 이상) · 메모 단축키와 겹치면 거절");
                check(ChatHotkey.Text(ChatHotkey.Ctrl | ChatHotkey.Alt | ChatHotkey.Shift, (uint)Keys.D7) == "Ctrl + Alt + Shift + 7" && ChatHotkey.Text(ChatHotkey.Ctrl | ChatHotkey.Alt, (uint)'B', "+") == "Ctrl+Alt+B", "단축키 글 만들기");
                ChatOptions.Current = new ChatOptions { HotkeyMods = ChatHotkey.Ctrl | ChatHotkey.Shift | ChatHotkey.Alt, HotkeyKey = (uint)Keys.F9 }; ChatOptions.Current.Save(); ChatOptions.Load();
                bool hkSaved = ChatOptions.Current.HotkeyMods == (ChatHotkey.Ctrl | ChatHotkey.Shift | ChatHotkey.Alt) && ChatOptions.Current.HotkeyKey == (uint)Keys.F9;
                File.WriteAllText(ChatOptions.FilePath, "HotkeyMods=2" + Environment.NewLine + "HotkeyKey=66" + Environment.NewLine); ChatOptions.Load();
                check(hkSaved && ChatOptions.Current.HotkeyMods == ChatHotkey.DefaultMods && ChatOptions.Current.HotkeyKey == ChatHotkey.DefaultKey, "단축키 저장·불러오기, 규칙에 안 맞는 저장값은 기본값으로"); new ChatOptions().Save(); ChatOptions.Load();
                using (var hb = new HotkeyBox(ChatHotkey.DefaultMods, ChatHotkey.DefaultKey))
                {
                    var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance; var cmd = typeof(HotkeyBox).GetMethod("ProcessCmdKey", bf);
                    Func<Keys, bool> press = k => { var m = new System.Windows.Forms.Message(); return (bool)cmd.Invoke(hb, new object[] { m, k }); };
                    string lastHint = null; hb.Hint += t => lastHint = t; bool changed = false; hb.Changed += () => changed = true;
                    typeof(HotkeyBox).GetMethod("OnGotFocus", bf).Invoke(hb, new object[] { EventArgs.Empty });
                    string t0 = hb.Text; press(Keys.ControlKey); string t1 = hb.Text; press(Keys.Menu); string t2 = hb.Text;
                    check(t1 == "Ctrl + " && t2 == "Ctrl + Alt + " && !changed, "단축키 입력란: 키를 누를 때마다 + 를 붙여 다음 키를 기다림 (" + t0 + " → " + t1 + " → " + t2 + ")");
                    press(Keys.ShiftKey); press(Keys.Back); string t3 = hb.Text; press(Keys.K);
                    check(t3 == "Ctrl + Alt + " && hb.Mods == (ChatHotkey.Ctrl | ChatHotkey.Alt) && hb.Key == (uint)Keys.K && hb.Text == "Ctrl + Alt + K" && changed, "단축키 입력란: Backspace 로 방금 키 취소, 글쇠로 확정");
                    typeof(HotkeyBox).GetMethod("OnGotFocus", bf).Invoke(hb, new object[] { EventArgs.Empty }); press(Keys.ControlKey); press(Keys.B);
                    check(lastHint != null && lastHint.Contains("3개") && hb.Key == (uint)Keys.K && hb.Text.StartsWith("키를"), "단축키 입력란: 키 2개만으로는 확정하지 않고 3개 이상 안내");
                    press(Keys.Escape); check(hb.Text == "Ctrl + Alt + K", "단축키 입력란: Esc 로 취소하면 원래 값");
                }
                var cw = new FakeWorld(); var cp = new Pet(cw, 0, 640, 720) { Area = cw.Areas[0] }; cp.SetState(PetState.Pause, cw); cp.X = 640; cp.Y = 720;
                check(cp.BeginChat(cw, 1) && cp.State == PetState.Chat && cp.ChatActive, "대화 시작: 자세가 대화로 바뀜");
                cp.ChatThink(); cp.ChatSay(ChatMood.Joy, "안녕! 반가워", false);
                for (int i = 0; i < 8; i++) { cw.Now += .02; cp.Update(cw, .02, null, 0); }
                var chatLook = cp.GetLook(cw.Now);
                check(chatLook.Ai && chatLook.BubbleReveal > 0 && chatLook.BubbleReveal < 7, "대화 중: AI 표시가 붙고 말풍선 글자가 한 자씩 찍힘 (" + chatLook.BubbleReveal + ")");
                for (int i = 0; i < 100; i++) { cw.Now += .02; cp.Update(cw, .02, null, 0); }
                check(cp.GetLook(cw.Now).BubbleReveal == -1, "다 찍히면 말풍선 전체 표시");
                cp.EndChat(cw); for (int i = 0; i < 60; i++) { cw.Now += .02; cp.Update(cw, .02, null, 0); }
                check(!cp.ChatActive && cp.State != PetState.Chat && cp.ChatPresence < .01f && !cp.GetLook(cw.Now).Ai, "대화가 끝나면 AI 표시가 사라지고 다시 돌아다님");
                var fp = new Pet(cw, 0, 640, 100) { Area = cw.Areas[0] };
                check(!fp.BeginChat(cw, 1), "낙하 중에는 대화 시작 안 함");

                NoteSelfTests(check, dir);
                OvertimeState.SelfTests(check);
                OvertimeUiTests.Run(check);
                ApiSelfTests(check);
            }
            catch (Exception e) { log.AppendLine("EXCEPTION " + e); failures++; }
            log.AppendLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            File.WriteAllText(Path.Combine(dir, "selftest_result.txt"), log.ToString(), new UTF8Encoding(true));
            Console.Write(log.ToString());
            return failures == 0 ? 0 : 1;
        }

        private static void ApiSelfTests(Action<bool, string> check)
        {
            check(ApiServer.IsAllowedOrigin(null) && ApiServer.IsAllowedOrigin(""), "API Origin: 데스크톱 앱(Origin 없음) 허용");
            check(ApiServer.IsAllowedOrigin("chrome-extension://abcdefghijklmnop") && ApiServer.IsAllowedOrigin("edge-extension://123456"), "API Origin: 브라우저 확장(*.sAIde) 허용");
            check(!ApiServer.IsAllowedOrigin("https://evil.example.com") && !ApiServer.IsAllowedOrigin("http://localhost:3000"), "API Origin: 일반 웹페이지 차단");

            var app = new App(true);
            app.Notes = new NoteManager(app);
            app.Notes.Start(false);
            app.Settings.ApiPort = 19485;
            app.StartApi();
            // 테스트 중 HTTP 요청이 오면 백그라운드에서 UI 큐를 비워 준다
            bool pump = true;
            var pumper = new Thread(delegate () { while (pump) { try { app.Api.Tick(); } catch { } Thread.Sleep(5); } }) { IsBackground = true };
            pumper.Start();
            try
            {
                check(app.Api != null && app.Api.IsRunning && app.Api.Port >= 1024, "API 서버 시작 (127.0.0.1:" + (app.Api != null ? app.Api.Port : 0) + ")");
                string baseUrl = "http://127.0.0.1:" + app.Api.Port;

                Func<string, string, string, string, KeyValuePair<int, string>> call = (method, path, body, origin) =>
                {
                    var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(baseUrl + path);
                    req.Method = method; req.Timeout = 5000; req.Proxy = null;
                    if (!string.IsNullOrEmpty(origin)) req.Headers["Origin"] = origin;
                    if (body != null)
                    {
                        byte[] b = new UTF8Encoding(false).GetBytes(body);
                        req.ContentType = "application/json; charset=utf-8"; req.ContentLength = b.Length;
                        using (var s = req.GetRequestStream()) s.Write(b, 0, b.Length);
                    }
                    try
                    {
                        using (var resp = (System.Net.HttpWebResponse)req.GetResponse())
                        using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                            return new KeyValuePair<int, string>((int)resp.StatusCode, sr.ReadToEnd());
                    }
                    catch (System.Net.WebException wex)
                    {
                        var r = wex.Response as System.Net.HttpWebResponse;
                        if (r != null) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return new KeyValuePair<int, string>((int)r.StatusCode, sr.ReadToEnd());
                        throw;
                    }
                };

                var h = call("GET", "/api/health", null, null);
                check(h.Key == 200 && Json.Flag(Json.Parse(h.Value), "ok"), "GET /api/health 응답 확인");

                var forbidden = call("POST", "/api/alert", "{\"message\":\"침입\"}", "https://malicious.example.com");
                check(forbidden.Key == 403, "일반 웹 Origin 403 차단 확인");

                var extOk = call("POST", "/api/alert", "{\"message\":\"공문 도착!\",\"style\":\"bubble\",\"seconds\":5}", "chrome-extension://onnara-saide-ext");
                object extJson = Json.Parse(extOk.Value);
                string alertId = Json.Str(extJson, "alertId");
                check(extOk.Key == 200 && Json.Flag(extJson, "ok") && !string.IsNullOrEmpty(alertId), "POST /api/alert (브라우저 확장 Origin) 성공");

                var alertGet = call("GET", "/api/alert/" + alertId, null, null);
                check(alertGet.Key == 200 && Json.Str(Json.Parse(alertGet.Value), "message") == "공문 도착!", "GET /api/alert/{id} 상태 조회");

                var noteCreate = call("POST", "/api/notes", "{\"content\":\"# 회의 메모\\n- [ ] 안건 정리 #업무\",\"color\":\"yellow\",\"open\":false}", null);
                object ncJson = Json.Parse(noteCreate.Value);
                string noteId = Json.Str(Json.Get(ncJson, "note"), "id");
                check(noteCreate.Key == 201 && !string.IsNullOrEmpty(noteId), "POST /api/notes 메모 생성 성공");

                var noteList = call("GET", "/api/notes?tag=업무", null, null);
                check(noteList.Key == 200 && noteList.Value.Contains("회의 메모"), "GET /api/notes 태그 검색 확인");

                var notePatch = call("PATCH", "/api/notes/" + noteId, "{\"append\":\"- [ ] 추가 항목\"}", null);
                check(notePatch.Key == 200 && notePatch.Value.Contains("추가 항목"), "PATCH /api/notes/{id} 본문 덧붙이기 확인");

                var noteRemind = call("POST", "/api/notes/" + noteId + "/remind", "{\"minutes\":30}", null);
                check(noteRemind.Key == 200 && Json.Flag(Json.Parse(noteRemind.Value), "ok"), "POST /api/notes/{id}/remind 알림 설정 확인");

                var noteDel = call("DELETE", "/api/notes/" + noteId, null, null);
                check(noteDel.Key == 200 && Json.Flag(Json.Parse(noteDel.Value), "ok"), "DELETE /api/notes/{id} 메모 삭제 확인");

                var actList = call("GET", "/api/pet/actions", null, null);
                check(actList.Key == 200 && actList.Value.Contains("Cheer") && actList.Value.Contains("mine"), "GET /api/pet/actions 동작 목록 조회");

                var actRun = call("POST", "/api/pet/action", "{\"action\":\"skit:Cheer\",\"say\":\"결재 완료!\"}", null);
                check(actRun.Key == 200 && Json.Flag(Json.Parse(actRun.Value), "ok") && app.LiveWindows[0].Pet.Bubble == "결재 완료!", "POST /api/pet/action 캐릭터 동작·말풍선 유도 확인");
            }
            finally
            {
                pump = false;
                try { pumper.Join(200); } catch { }
                try { if (app.Api != null) app.Api.Dispose(); } catch { }
                try { if (app.Notes != null) app.Notes.Suspend(); } catch { }
                foreach (var w in app.LiveWindows.ToArray()) { try { w.Close(); w.Dispose(); } catch { } }
            }
        }
    }
}
