using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Bugi
{
    /// <summary>
    /// `bugi.exe --i18n-test [폴더]` : 화면 언어를 English 로 정하고 실제 창들을 띄워 한글이 남아 있는지 훑어본다.
    /// 사전 항목의 자리표시자({0}…)가 한국어 원문과 같은지, 영어 AI 프롬프트에 한글이 섞이지 않았는지, 영어 감정 태그를 읽어 내는지도 본다.
    /// 창 그림(i18n_*.png)을 폴더에 남겨 글이 잘리지 않았는지 눈으로 볼 수 있게 한다. 문제가 있으면 종료 코드 1.
    /// </summary>
    internal static partial class DevTools
    {
        // 파일 이름(부기_일기.txt)은 언어와 상관없이 그대로 쓰므로 한글이 있어도 된다.
        private static readonly Regex FileNames = new Regex(@"부기_[^\s.,)]*\.(txt|ini|xml)");

        private static bool HasStrayHangul(string s)
        {
            return !string.IsNullOrEmpty(s) && Loc.HasHangul(FileNames.Replace(s, ""));
        }

        /// <summary>컨트롤 트리의 글(Text · 목록 · 메뉴 · 이 프로그램 컨트롤이 들고 있는 글 필드)에서 한글을 찾는다.</summary>
        private static void ScanControl(Control root, string where, List<string> found)
        {
            var seen = new HashSet<object>();
            Action<Control> walk = null;
            walk = c =>
            {
                if (!seen.Add(c)) return;
                Action<string, string> chk = (what, s) => { if (HasStrayHangul(s)) found.Add(where + " · " + c.GetType().Name + "." + what + " = " + s.Replace("\n", "⏎")); };
                // DateTimePicker 의 오전/오후는 Windows 가 표시 언어로 그린다. 언어 고르기 칸의 "한국어"는 일부러 그 나라 글자로 적는다.
                if (!(c is DateTimePicker)) { chk("Text", c.Text); chk("AccessibleName", c.AccessibleName); }
                var combo = c as ComboBox; if (combo != null) foreach (object o in combo.Items) { string it = Convert.ToString(o); if (Array.IndexOf(Loc.NativeNames, it) < 0) chk("Item", it); }
                var list = c as ListBox; if (list != null) foreach (object o in list.Items) chk("Item", Convert.ToString(o));
                var strip = c as ToolStrip; if (strip != null) ScanItems(strip.Items, where, found);
                // 이 프로그램이 직접 그리는 컨트롤은 글을 필드에 들고 있다
                if (c.GetType().Namespace == "Bugi")
                    for (Type t = c.GetType(); t != null && t != typeof(Control) && t != typeof(Form) && t != typeof(Panel); t = t.BaseType)
                        foreach (FieldInfo fi in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                        {
                            object v; try { v = fi.GetValue(c); } catch { continue; }
                            var s1 = v as string; if (s1 != null) { chk("field " + fi.Name, s1); continue; }
                            var en = v as IEnumerable;
                            if (en != null && !(v is Control)) foreach (object item in en) { var s2 = item as string; if (s2 != null) chk("field " + fi.Name, s2); }
                        }
                foreach (Control k in c.Controls) walk(k);
            };
            walk(root);
        }

        private static void ScanItems(ToolStripItemCollection items, string where, List<string> found)
        {
            foreach (ToolStripItem i in items)
            {
                if (HasStrayHangul(i.Text)) found.Add(where + " · menu item = " + i.Text);
                if (HasStrayHangul(i.ToolTipText)) found.Add(where + " · menu tip = " + i.ToolTipText);
                var m = i as ToolStripDropDownItem; if (m != null && m.DropDownItems.Count > 0) ScanItems(m.DropDownItems, where, found);
                var section = i.GetType(); // MenuSection 등 직접 만든 항목의 글 필드
                if (section.Namespace == "Bugi")
                    foreach (FieldInfo fi in section.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    { var s = fi.GetValue(i) as string; if (HasStrayHangul(s)) found.Add(where + " · " + section.Name + "." + fi.Name + " = " + s); }
            }
        }

        internal static int I18nTest(string dir)
        {
            Directory.CreateDirectory(dir);
            Paths.StorageOverride = Path.Combine(dir, "data");
            Loc.Force("en");
            var log = new StringBuilder(); int failures = 0;
            var found = new List<string>();
            Action<string, Action> step = (name, a) => { try { a(); log.AppendLine("OK   " + name); } catch (Exception e) { failures++; log.AppendLine("FAIL " + name + " " + e); } };
            Action<bool, string> check = (ok, msg) => { if (ok) log.AppendLine("OK   " + msg); else { failures++; log.AppendLine("FAIL " + msg); } };
            Action<Form, string> shotForm = (f, name) => { f.Show(); Pump(.4); ScanControl(f, name, found); CaptureForm(f, Path.Combine(dir, "i18n_" + name + ".png")); };

            // ───── 사전 자체 ─────
            var ph = new Regex(@"\{\d+\}");
            int bad = 0;
            foreach (var kv in Loc.Entries)
            {
                if (kv.Value.Length == 0) { bad++; log.AppendLine("FAIL dict empty: " + kv.Key); }
                else if (!ph.Matches(kv.Key).Cast<Match>().Select(m => m.Value).OrderBy(x => x).SequenceEqual(ph.Matches(kv.Value).Cast<Match>().Select(m => m.Value).OrderBy(x => x)))
                { bad++; log.AppendLine("FAIL placeholders differ: " + kv.Key + " -> " + kv.Value); }
                if (HasStrayHangul(kv.Value)) { bad++; log.AppendLine("FAIL Hangul in English entry: " + kv.Key + " -> " + kv.Value); }
                if (!Loc.HasHangul(kv.Key)) { bad++; log.AppendLine("FAIL key without Hangul: " + kv.Key); }
            }
            check(bad == 0, "사전 " + Loc.Entries.Count() + "개: 비었거나 자리표시자가 다르거나 한글이 남은 항목 없음");
            check(Loc.TF("{0}분 뒤", 5) == "In 5 min" && Loc.T("저장") == "Save" && Loc.Retarget("점심 먹으러 가자!", "점심 먹으러 가자!") == "Let's go to lunch!" && Loc.Retarget("Let's go to lunch!", "점심 먹으러 가자!") == "Let's go to lunch!" && Loc.Retarget("내 문구", "점심 먹으러 가자!") == "내 문구", "T · TF · Retarget 동작");

            // ───── 창 ─────
            PlayOptions.Load(); FriendData.Load(); CharacterArt.Reset(Native.SystemDpiScale());
            DesktopSurfaces.TestProvider = () => new List<SurfaceWindow>();
            App app = null;
            step("앱 생성", () => { app = new App(true); app.Spawn(0); app.Spawn(1); Pump(1); });
            for (int p = 0; p < SettingsForm.PageNames.Length; p++)
            {
                int page = p;
                step("설정창 쪽 " + page + " " + SettingsForm.PageNames[page], () => { var f = new SettingsForm(app, page); shotForm(f, "settings_" + page); f.Close(); });
            }
            step("설정창 초과근무 탭", () =>
            {
                var now = DateTime.Now; app.Overtime.SetWatch(true, now); app.Overtime.SetBasic("09:00", "18:00"); app.Overtime.EnsureToday(now); app.Overtime.SetRange(10, 23);
                var f = new SettingsForm(app, 2); f.SelectAlarmTab(1); shotForm(f, "settings_overtime"); f.Close();
            });
            step("설정창 메모 설정 탭", () =>
            {
                var f = new SettingsForm(app, 4); f.SelectMemoTab(1); shotForm(f, "settings_memo_opts"); f.Close();
            });
            step("알람 만들기 창", () => { var f = new AlarmComposer(app.TrayIconImage, a => true); shotForm(f, "alarm_composer"); f.Close(); });
            step("일기장", () => { var f = new ChatDiaryForm(app.TrayIconImage); shotForm(f, "diary"); f.Close(); });
            step("기억 창", () => { var f = new ChatMemoryForm(app.TrayIconImage); shotForm(f, "memory"); f.Close(); });
            step("정각 알림창", () => { var n = new HourlyNotice(app.TrayIconImage); n.SetMessage(App.HourlyText(new DateTime(2026, 11, 12, 15, 0, 0))); shotForm(n, "hourly"); n.Close(); });
            step("우클릭 메뉴", () =>
            {
                MethodInfo build = typeof(App).GetMethod("BuildMenu", BindingFlags.NonPublic | BindingFlags.Instance);
                using (var menu = (ContextMenuStrip)build.Invoke(app, new object[] { null })) { ScanItems(menu.Items, "menu", found); ShotMenuStrip(menu, Path.Combine(dir, "i18n_menu.png")); }
            });
            step("메모 창 · 라이브러리 · 기록 · 알림 만들기", () =>
            {
                var mgr = new NoteManager(app); app.Notes = mgr; mgr.Start(false);
                Note n1 = mgr.CreateNote(NoteSizePreset.Medium, NoteColors.Yellow); Pump(.3);
                NoteWindow w = mgr.WindowOf(n1.Id); ScanControl(w, "note", found); CaptureForm(w, Path.Combine(dir, "i18n_note.png"));
                mgr.ShowLibrary(); Pump(.5);
                foreach (var f in Application.OpenForms.OfType<NoteLibraryForm>().ToList()) { ScanControl(f, "library", found); CaptureForm(f, Path.Combine(dir, "i18n_library.png")); f.Close(); }
                var hist = new NoteHistoryForm(mgr, n1.Id); shotForm(hist, "history"); hist.Close();
                mgr.ShowReminderComposer(n1.Id, w); Pump(.4);
                foreach (var f in Application.OpenForms.OfType<Form>().Where(x => x.GetType().Name.Contains("Reminder")).ToList()) { ScanControl(f, "reminder", found); CaptureForm(f, Path.Combine(dir, "i18n_reminder.png")); f.Close(); }
                mgr.Suspend();
            });
            step("알림 부기 말풍선", () =>
            {
                AlertPetWindow alert = app.ShowAlert(Loc.T("점심시간이야!"));
                Pump(1.2); alert.Close(); Pump(.2);
            });

            // ───── AI 대화(영어 모드) ─────
            Action<string, string> noHangul = (what, s) => check(!HasStrayHangul(s), "영어 AI: " + what + "에 한글이 섞이지 않음");
            step("영어 AI 프롬프트", () =>
            {
                var now = new DateTime(2026, 11, 12, 15, 0, 0);
                FriendData.Load(); ChatOptions.Load();
                for (int mode = 0; mode < ChatMode.All.Length; mode++)
                {
                    ChatOptions.Current.Mode = mode;
                    for (int c = 0; c < 2; c++)
                    {
                        var msgs = ChatPersona.Build(new List<ChatMessage> { new ChatMessage("user", "Hello") }, now, c);
                        string all = string.Join("\n", msgs.Select(m => m.Content));
                        noHangul(ChatMode.All[mode].Key + "/" + c + " 시스템 프롬프트·예시", all);
                        check(msgs[0].Content.Contains(c == 1 ? "Bukangyee" : "Boogi") && msgs[0].Content.Contains("[Mood]") && msgs[0].Content.Contains("Always reply in English"), "영어 시스템 프롬프트에 이름 · 태그 형식 · 영어 지시가 있음 (" + ChatMode.All[mode].Key + "/" + c + ")");
                    }
                }
                ChatOptions.Current.Mode = 0;
                noHangul("기억 요청", string.Join("\n", ChatPersona.MemoryRequest("User: hi").Select(m => m.Content)));
                noHangul("일기 요청", string.Join("\n", ChatPersona.DiaryRequest("User: hi", now, 0).Select(m => m.Content)));
                noHangul("일기 요청(부캉이)", string.Join("\n", ChatPersona.DiaryRequest("User: hi", now, 1).Select(m => m.Content)));
                noHangul("선톡 안내", ChatPersona.ProactivePrompt(Loc.T("점심시간 무렵이야")).Content);
                check(ChatPersona.IsSituation(ChatPersona.ProactivePrompt("x").Content), "영어 선톡 안내를 상황 안내로 알아봄");
                var rng = new Random(3);
                foreach (ChatMood m in Enum.GetValues(typeof(ChatMood))) noHangul("답장 칩 " + m, string.Join("|", ChatPersona.Chips(m, false, rng)));
                noHangul("첫 칩", string.Join("|", ChatPersona.Chips(ChatMood.Neutral, true, rng)));
                string g; ChatMood gm; g = ChatPersona.Greeting(rng, now, out gm); noHangul("인사", g);
                noHangul("정각 문구", App.HourlyText(now));
                noHangul("감정 태그 이름", string.Join("|", ChatPersona.TagNames));
            });
            step("영어 감정 태그 읽기", () =>
            {
                Func<string, ChatMood> mood = s => { var rp = new ReplyParser(); rp.Feed(s); return rp.Mood; };
                check(mood("[Sad] Oh no, that sounds rough.") == ChatMood.Sad, "[Sad] → 슬픔");
                check(mood("[Comfort] I'm right here.") == ChatMood.Comfort, "[Comfort] → 위로");
                check(mood("[Surprise] Whoa, really?!") == ChatMood.Surprise, "[Surprise] → 놀람");
                check(mood("[Greet] Hi there!") == ChatMood.Greet && mood("[Cheer] You can do it!") == ChatMood.Cheer && mood("[Praise] Great job!") == ChatMood.Praise, "[Greet] [Cheer] [Praise]");
                check(mood("[Think] Hmm, let me see.") == ChatMood.Think && mood("[Hungry] Snack time?") == ChatMood.Hungry && mood("[Sleepy] So tired.") == ChatMood.Sleepy, "[Think] [Hungry] [Sleepy]");
                check(mood("[Joy] We should plan a trip to Busan next weekend!") == ChatMood.Travel || mood("[Joy] We should plan a trip to Busan next weekend!") == ChatMood.Busan, "영어 화제어(trip · Busan)로 응용동작을 고름");
                check(mood("Thanks so much, that was great!") == ChatMood.Joy, "태그가 없어도 영어 대사에서 눈치로 기쁨");
                var rp2 = new ReplyParser(); rp2.Feed("[Joy] Boogi: Hello there! Nice to see you. Second sentence here. Third sentence is cut. Fourth one gone.");
                check(rp2.Text.StartsWith("Hello there!") && !rp2.Text.Contains("Fourth"), "영어 말머리를 지우고 문장 수 한도를 지킴: " + rp2.Text);
                check(ChatPersona.ParseDiary("Diary: Today we talked about lunch and I felt happy.") == "Today we talked about lunch and I felt happy.", "영어 일기 앞말 지우기");
                check(ChatMemory.Add(new[] { "None", "none.", "Likes cats and lives in Busan" }) == 1, "기억 'None' 거르기(저장소 건드림)");
            });
            step("영어 모드에서 ini 에 Language=en 저장", () =>
            {
                var s = new Settings(); s.Save();
                check(File.ReadAllText(Settings.FilePath).Contains("Language=en"), "저장 파일에 Language=en");
                Loc.Set("ko"); check(Loc.T("저장") == "저장" && new Settings().LunchMsg == "점심 먹으러 가자!", "한국어로 돌리면 원문"); Loc.Set("en");
            });
            step("설정창에서 언어 전환 (한국어 → English → 한국어)", () =>
            {
                var bf = BindingFlags.NonPublic | BindingFlags.Instance;
                ChatOptions.Current.Enabled = false; ChatOptions.Current.Hotkey = false;   // 저장할 때 단축키 경고창이 뜨지 않게
                Loc.Set("ko");
                for (int round = 0; round < 2; round++)
                {
                    string want = round == 0 ? "en" : "ko";
                    var f = new SettingsForm(app, 3); f.Show(); Pump(.3);
                    var combo = (ComboBox)typeof(SettingsForm).GetField("cLang", bf).GetValue(f);
                    check(combo.SelectedIndex == Array.IndexOf(Loc.Codes, Loc.Code), "언어 칸이 지금 언어(" + Loc.Code + ")를 가리킴");
                    combo.SelectedIndex = Array.IndexOf(Loc.Codes, want);
                    var notesBefore = app.Notes;
                    bool ok = (bool)typeof(SettingsForm).GetMethod("Apply", bf).Invoke(f, null);
                    check(ok && Loc.Code == want && File.ReadAllText(Settings.FilePath).Contains("Language=" + want), "저장하면 언어가 " + want + " 로 바뀌고 Language=" + want + " 가 적힘");
                    check(app.Notes != notesBefore || notesBefore == null, "언어가 바뀌면 메모 창들을 새 언어로 다시 띄움");
                    f.Close(); Pump(.2);
                }
                check(Loc.T("저장") == "저장", "한국어로 돌아옴"); Loc.Set("en");
            });
            step("전환 후 메모·메뉴 다시 만들기", () => { app.OnLanguageChanged(); Pump(.3); });

            // ───── 정리 ─────
            lock (Loc.Missed) { foreach (string m in Loc.Missed.OrderBy(x => x)) { failures++; log.AppendLine("FAIL 사전에 없는 글: " + m.Replace("\n", "⏎")); } }
            foreach (string f in found.Distinct()) { failures++; log.AppendLine("FAIL 한글이 남음: " + f); }
            log.AppendLine(failures == 0 ? "ALL OK" : failures + " 가지 문제");
            File.WriteAllText(Path.Combine(dir, "i18n_result.txt"), log.ToString(), new UTF8Encoding(true));
            Console.WriteLine(log.ToString());
            try { app.Quit(); } catch { }
            return failures == 0 ? 0 : 1;
        }

        private static void ShotMenuStrip(ContextMenuStrip menu, string path)
        {
            menu.Show(new Point(40, 40)); Pump(.5);
            using (var b = new Bitmap(menu.Width, menu.Height))
            { menu.DrawToBitmap(b, new Rectangle(0, 0, menu.Width, menu.Height)); b.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
            menu.Close();
        }
    }
}
