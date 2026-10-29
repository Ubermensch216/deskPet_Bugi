using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Bugi
{
    internal static class FriendUI
    {
        internal static readonly Color Paper = Color.FromArgb(255, 252, 249), Ink = Color.FromArgb(68, 50, 50);
        // 설정창 색: 부산시 상징마크 파랑(LogoArt.Blue)을 바탕으로, 부기의 부리 노랑을 포인트로 쓴다.
        internal static readonly Color Blue = LogoArt.Blue, BlueDark = Color.FromArgb(20, 54, 122), Navy = Color.FromArgb(15, 36, 84), Sheet = Color.FromArgb(248, 250, 255), Gold = Color.FromArgb(255, 214, 10);
        internal static void Setup(Form f, Icon icon)
        {
            f.Font = new Font("Malgun Gothic", 9f); f.BackColor = Paper;
            f.StartPosition = FormStartPosition.CenterScreen; f.AutoScaleMode = AutoScaleMode.Dpi;
            f.MinimizeBox = false; f.MaximizeBox = false; f.FormBorderStyle = FormBorderStyle.FixedDialog;
            if (icon != null) f.Icon = icon;
        }
        internal static Button Button(string text, EventHandler click)
        { var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 3, 6, 3), Margin = new Padding(4) }; b.Click += click; return b; }
        internal static Label Label(string text)
        { return new Label { Text = text, AutoSize = true, ForeColor = Ink, Margin = new Padding(4, 8, 4, 8) }; }
    }

    internal static class ControlExtensions
    {
        internal static T With<T>(this T control, Action<T> action) { action(control); return control; }
    }

    /// <summary>
    /// 통합 설정창. 흩어져 있던 설정(알림 / 움직임·반응 / 친구 기록·생활 / 도우미 창)을 한 곳에 분류했다.
    ///   캐릭터 · 움직임 · 반응 · 생활 · 알림 · 화면·시스템
    /// 왼쪽에서 분류를 고르고, 저장 한 번으로 모든 분류가 함께 저장된다.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        internal static readonly string[] PageNames = { "캐릭터", "움직임", "반응", "생활", "알림", "화면 · 시스템", "메모", "AI 대화" };
        internal static readonly string[] PageHints = { "친구 부르기 · 근무복", "이동 범위 · 속도 · 얌전 모드", "클릭 · 커서 · 숨은 반응", "친밀도 · 간식 · 배고픔", "정각 · 점심 · 퇴근 · 낮잠", "투명도 · 도우미 창 · 자동 시작", "새 메모 기본값 · 단축키", "Ollama · 단축키 · 기억" };
        private static int lastPage;

        private readonly App app;
        private readonly ListBox nav;
        private readonly Panel host;
        private readonly Panel[] pages = new Panel[8];
        private readonly Label status;
        private readonly System.Windows.Forms.Timer live;

        // 캐릭터
        private ListBox rosterList; private CheckBox cOutfit;
        // 움직임
        private CheckBox cWalls, cCeiling, cWindows, cIdle, cShortRest, cPinned, cFocus, cWork, cFull;
        private NumericUpDown nSpeed, nFall, nSlide;
        // 반응
        private ComboBox clickMode; private RadioButton rEscape, rFollow, rIgnore; private NumericUpDown nDelay; private CheckBox cSecrets;
        // 생활
        private NumericUpDown nHunger; private CheckBox cHungerCry, cAffinity;
        private Label lifeSummary, lifeDetail, secretLabel; private ProgressBar satietyBar; private Button snackButton;
        // 알림
        private CheckBox cLunch, cLeave, cNap, cHourly, cPopup, cAlertPet, cGlow, cQuiet;
        private TextBox tLunch, tLunchMsg, tLeave, tLeaveMsg;
        private NumericUpDown nNap, nStart, nEnd;
        private ComboBox glowTheme, glowWidth, glowStrength;
        // 화면·시스템
        private NumericUpDown nOpacity, nMemoOpacity; private CheckBox cMemoTop, cStartup;
        // 메모
        private ComboBox noteColor, noteSize; private Label noteInfo;
        // AI 대화
        private CheckBox cChat, cChatKey, cChatSound, cChatProactive, cChatMemory, cChatChips, cChatDiary; private TextBox tChatModel, tChatUrl; private NumericUpDown nProactive; private Label chatProbe;

        public SettingsForm(App app, int page = -1)
        {
            this.app = app;
            FriendUI.Setup(this, app.TrayIconImage);
            Text = "부기 · 설정"; ClientSize = new Size(760, 640); MinimumSize = new Size(700, 560);
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = false; ShowInTaskbar = true; BackColor = SystemColors.Control;

            // 아래 단추줄
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Color.FromArgb(232, 239, 250) };
            var bSave = new Button { Text = "저장", Width = 92, Height = 32, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            var bApply = new Button { Text = "적용", Width = 92, Height = 32, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            var bCancel = new Button { Text = "닫기", Width = 92, Height = 32, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            status = new Label { AutoSize = false, Left = 14, Top = 8, Width = 330, Height = 36, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left | AnchorStyles.Top };
            bar.Controls.AddRange(new Control[] { status, bSave, bApply, bCancel });
            bar.Resize += delegate { bCancel.Left = bar.Width - 14 - bCancel.Width; bApply.Left = bCancel.Left - 8 - bApply.Width; bSave.Left = bApply.Left - 8 - bSave.Width; bSave.Top = bApply.Top = bCancel.Top = 10; status.Width = Math.Max(0, bSave.Left - 8 - status.Left); };
            Controls.Add(bar);
            bSave.Click += delegate { if (Apply()) Close(); };
            bApply.Click += delegate { Apply(); };
            bCancel.Click += delegate { Close(); };
            AcceptButton = bSave; CancelButton = bCancel;

            // 왼쪽 분류 목록
            nav = new ListBox { Dock = DockStyle.Left, Width = 176, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 54, BorderStyle = BorderStyle.None, IntegralHeight = false, BackColor = FriendUI.Navy, ForeColor = Color.White };
            nav.Items.AddRange(PageNames);
            nav.DrawItem += DrawNav;
            host = new Panel { Dock = DockStyle.Fill, BackColor = FriendUI.Sheet };
            Controls.Add(host); Controls.Add(nav);
            host.BringToFront();

            pages[0] = BuildCharacterPage(); pages[1] = BuildMovePage(); pages[2] = BuildReactPage();
            pages[3] = BuildLifePage(); pages[4] = BuildAlarmPage(); pages[5] = BuildDisplayPage(); pages[6] = BuildMemoPage(); pages[7] = BuildChatPage();
            foreach (var pg in pages) { pg.Dock = DockStyle.Fill; pg.Visible = false; host.Controls.Add(pg); }
            nav.SelectedIndexChanged += delegate { ShowPage(nav.SelectedIndex); };
            nav.SelectedIndex = Math.Max(0, Math.Min(pages.Length - 1, page >= 0 ? page : lastPage));

            live = new System.Windows.Forms.Timer { Interval = 500 };
            live.Tick += delegate { RefreshLive(); };
            live.Start(); RefreshLive();
            FormClosed += delegate { live.Stop(); live.Dispose(); };
        }

        internal void SelectPage(int i) { if (i >= 0 && i < pages.Length) nav.SelectedIndex = i; }

        // ───────── 분류 목록 그리기 ─────────
        private void DrawNav(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(sel ? FriendUI.Blue : nav.BackColor)) e.Graphics.FillRectangle(bg, e.Bounds);
            if (sel) using (var bar = new SolidBrush(FriendUI.Gold)) e.Graphics.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top, 5, e.Bounds.Height);
            using (var f1 = new Font("Malgun Gothic", 11f, FontStyle.Bold)) using (var f2 = new Font("Malgun Gothic", 7.8f))
            {
                string[] glyph = { "☺", "▶", "☝", "♥", "♪", "▣", "✎", "✦" };
                TextRenderer.DrawText(e.Graphics, glyph[e.Index] + "  " + PageNames[e.Index], f1, new Rectangle(e.Bounds.Left + 14, e.Bounds.Top + 7, e.Bounds.Width - 16, 22), Color.White, TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, PageHints[e.Index], f2, new Rectangle(e.Bounds.Left + 14, e.Bounds.Top + 29, e.Bounds.Width - 16, 20), sel ? Color.FromArgb(214, 228, 250) : Color.FromArgb(150, 176, 220), TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }

        private void ShowPage(int i)
        {
            if (i < 0) return; lastPage = i;
            for (int k = 0; k < pages.Length; k++) pages[k].Visible = k == i;
            RefreshLive();
        }

        // ───────── 쪽 만들기 도구 ─────────
        private Panel NewPage(string title, string subtitle, out FlowLayoutPanel flow)
        {
            var page = new Panel { BackColor = FriendUI.Sheet };
            var head = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = FriendUI.Sheet };
            head.Controls.Add(new Label { Text = title, AutoSize = true, Left = 20, Top = 10, Font = new Font("Malgun Gothic", 15f, FontStyle.Bold), ForeColor = FriendUI.BlueDark });
            head.Controls.Add(new Label { Text = subtitle, AutoSize = true, Left = 22, Top = 40, ForeColor = Color.DimGray });
            head.Paint += (s, e) => { using (var pen = new Pen(Color.FromArgb(190, 208, 238), 2)) e.Graphics.DrawLine(pen, 16, head.Height - 2, head.Width - 16, head.Height - 2); };
            flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(18, 6, 12, 12) };
            page.Controls.Add(flow); page.Controls.Add(head);
            head.SendToBack(); flow.BringToFront();
            return page;
        }

        private static GroupBox Group(FlowLayoutPanel parent, string title, int width = 500)
        {
            var g = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 6, 10, 8), Margin = new Padding(2, 8, 2, 4), MinimumSize = new Size(width, 0), ForeColor = FriendUI.BlueDark };
            var inner = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ForeColor = FriendUI.Ink };
            g.Controls.Add(inner); parent.Controls.Add(g);
            g.Tag = inner; return g;
        }
        private static FlowLayoutPanel In(GroupBox g) { return (FlowLayoutPanel)g.Tag; }

        private CheckBox Check(FlowLayoutPanel f, string text, bool value, string hint = null)
        {
            var c = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(3, 3, 3, hint == null ? 3 : 0) };
            f.Controls.Add(c);
            if (hint != null) f.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(24, 0, 3, 5) });
            return c;
        }
        private static Label Note(FlowLayoutPanel f, string text, int maxW = 470)
        { var l = new Label { Text = text, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 3, 3, 3), MaximumSize = new Size(maxW, 0) }; f.Controls.Add(l); return l; }
        private static FlowLayoutPanel Row(FlowLayoutPanel f)
        { var r = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 1, 0, 1), FlowDirection = FlowDirection.LeftToRight }; f.Controls.Add(r); return r; }
        private static Label Cap(string text, int width = 0)
        { var l = new Label { Text = text, AutoSize = width == 0, Margin = new Padding(3, 6, 3, 0), ForeColor = FriendUI.Ink }; if (width > 0) { l.Width = width; l.Height = 22; } return l; }
        private static NumericUpDown Num(decimal min, decimal max, decimal value, int dec = 0, decimal inc = 1, int width = 70)
        { return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = dec, Increment = inc, Value = Math.Max(min, Math.Min(max, value)), Width = width, Margin = new Padding(3) }; }
        private static ComboBox Combo(string[] items, int selected, int width)
        { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Margin = new Padding(3) }; c.Items.AddRange(items); c.SelectedIndex = Math.Max(0, Math.Min(items.Length - 1, selected)); return c; }

        // ───────── 1. 캐릭터 ─────────
        private Panel BuildCharacterPage()
        {
            FlowLayoutPanel f; var page = NewPage("캐릭터", "함께할 친구를 부르고, 옷차림을 골라요", out f);
            var o = PlayOptions.Current;

            var gRoster = Group(f, "지금 화면에 있는 친구들"); var r = In(gRoster);
            var rowA = Row(r);
            rosterList = new ListBox { Width = 250, Height = 92, IntegralHeight = false, Margin = new Padding(3) }; rowA.Controls.Add(rosterList);
            var col = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false }; rowA.Controls.Add(col);
            var bCall = FriendUI.Button(CharacterArt.Count > 1 ? "친구 부르기 ▼" : "친구 한 마리 더 부르기", null);
            if (CharacterArt.Count > 1)
            {
                var callMenu = new ContextMenuStrip();
                callMenu.Items.Add("무작위", null, delegate { app.SpawnFromUi(-1); RefreshRoster(); });
                for (int i = 0; i < CharacterArt.Count; i++) { int id = i; callMenu.Items.Add(CharacterArt.Infos[i].Name, null, delegate { app.SpawnFromUi(id); RefreshRoster(); }); }
                bCall.Click += delegate { callMenu.Show(bCall, new Point(0, bCall.Height)); };
            }
            else bCall.Click += delegate { app.SpawnFromUi(-1); RefreshRoster(); };
            col.Controls.Add(bCall);
            col.Controls.Add(FriendUI.Button("선택한 친구 보내기", delegate { if (rosterList.SelectedIndex >= 0) { app.RemoveByIndex(rosterList.SelectedIndex); RefreshRoster(); } }));
            col.Controls.Add(FriendUI.Button("모두 숨기기 / 보이기", delegate { app.ToggleHidden(); }));
            col.Controls.Add(FriendUI.Button("화면 가운데로 다시 부르기", delegate { app.RecoverVisible(); }));
            Note(r, "마지막에 불러 둔 구성은 다음 실행에도 그대로 나와요.");

            var gOutfit = Group(f, "옷차림"); var d = In(gOutfit);
            cOutfit = Check(d, "근무복 입기 (하늘색 조끼 · 명찰)", o.Outfit, "근무복을 입으면 가끔 안내하기 · 배꼽인사 · 신분증 · 깃발 흔들기를 해요.");
            RefreshRoster();
            return page;
        }

        private void RefreshRoster()
        {
            if (rosterList == null) return;
            var names = app.LiveWindows.Select(w => w.Pet.Art.Info.Name).ToList();
            int sel = rosterList.SelectedIndex;
            if (rosterList.Items.Count == names.Count && names.Select((n, i) => (string)rosterList.Items[i] == n).All(b => b)) return;
            rosterList.Items.Clear(); foreach (var n in names) rosterList.Items.Add(n);
            if (sel >= 0 && sel < names.Count) rosterList.SelectedIndex = sel;
        }

        // ───────── 2. 움직임 ─────────
        private Panel BuildMovePage()
        {
            FlowLayoutPanel f; var page = NewPage("움직임", "어디를 돌아다니고 얼마나 빨리 움직일지 정해요", out f);
            var o = PlayOptions.Current;
            var g1 = Group(f, "돌아다니는 곳"); var a = In(g1);
            cWalls = Check(a, "화면 양옆 벽 타기", o.Walls);
            cCeiling = Check(a, "화면 천장 타기", o.Ceiling);
            cWindows = Check(a, "열린 창 옆을 올라가고 창 위에서 걷기 · 쉬기", o.Windows);
            cFull = Check(a, "전체화면 영상 · 게임을 볼 때 자동으로 숨기기", o.Fullscreen);
            var g2 = Group(f, "쉬는 방법"); var b = In(g2);
            cIdle = Check(b, "혼자 앉아서 쉬기 (앉기 · 잠자기 · 멍때리기 · 기지개 · 곡괭이질 · 검색)", o.Idle);
            cShortRest = Check(b, "앉아있는 시간 줄이기 (6~11초)", o.ShortRest);
            var g3 = Group(f, "조용히 지내기"); var c = In(g3);
            cFocus = Check(c, "얌전 모드: 바닥에만 머물고 도망가지 않기", o.Focus, "벽 · 천장 · 창 타기가 멈춰요.");
            cWork = Check(c, "평일 09:00~18:00에는 자동으로 얌전 모드", o.WorkFocus);
            cPinned = Check(c, "친구 위치 고정 (마우스로 옮길 수는 있어요)", o.PositionFixed);
            var g4 = Group(f, "속도  ·  숫자가 클수록 빨라져요"); var d = In(g4);
            Func<string, decimal, double, NumericUpDown> speedRow = (label, max, value) =>
            { var r = Row(d); r.Controls.Add(Cap(label, 190)); var n = Num(.5m, max, (decimal)value, 1, .1m, 78); r.Controls.Add(n); r.Controls.Add(Cap("배")); return n; };
            nSpeed = speedRow("걷기 · 기어오르기", 2m, o.Speed);
            nFall = speedRow("날개 퍼덕 낙하", 3m, o.FallSpeed);
            nSlide = speedRow("슬라이딩 (커서를 피해 도망)", 3m, o.SlideSpeed);
            return page;
        }

        // ───────── 3. 반응 ─────────
        private Panel BuildReactPage()
        {
            FlowLayoutPanel f; var page = NewPage("반응", "클릭하거나 커서를 가까이 댔을 때 친구가 어떻게 반응할지 골라요", out f);
            var o = PlayOptions.Current;
            var g1 = Group(f, "마우스로 클릭했을 때"); var a = In(g1);
            clickMode = Combo(new[] { "순서대로: 눌리기 → 달달 떨며 울기 → 폴짝", "무작위 반응", "눌리기만", "달달 떨며 울기만", "폴짝 뛰기만", "클릭 반응 끄기" }, (int)o.ClickMode, 400);
            a.Controls.Add(clickMode);
            Note(a, "살짝 놓으면 날개를 퍼덕이며 천천히, 휙 던지면 날아간 뒤 내려와요.");
            var g2 = Group(f, "커서가 가까이 왔을 때"); var b = In(g2);
            rEscape = new RadioButton { Text = "쌩~ 슬라이딩하며 도망가기", AutoSize = true, Checked = o.Escape && !o.Follow, Margin = new Padding(3) };
            rFollow = new RadioButton { Text = "커서를 졸졸 따라오기", AutoSize = true, Checked = o.Follow && !o.Escape, Margin = new Padding(3) };
            rIgnore = new RadioButton { Text = "신경 쓰지 않기", AutoSize = true, Checked = !o.Escape && !o.Follow, Margin = new Padding(3) };
            b.Controls.Add(rEscape); b.Controls.Add(rFollow); b.Controls.Add(rIgnore);
            var r = Row(b); r.Controls.Add(Cap("도망 반응 대기", 130)); nDelay = Num(.05m, 1m, (decimal)o.EscapeDelay, 2, .05m, 78); r.Controls.Add(nDelay); r.Controls.Add(Cap("초"));
            var g3 = Group(f, "숨은 반응"); var c = In(g3);
            cSecrets = Check(c, "숨은 반응 사용", o.Secrets);
            Note(c, "어질어질: 친구 머리 위에서 마우스를 한 바퀴 빙글빙글.\n몰래 군무: 친구 둘 이상일 때 커서를 떨어뜨리고 4초.\n비밀 조종사: 같은 친구를 10초 안에 세 번 휙 던지기.");
            return page;
        }

        // ───────── 4. 생활 ─────────
        private Panel BuildLifePage()
        {
            FlowLayoutPanel f; var page = NewPage("생활", "친밀도와 포만감, 배고픔 · 숨은 반응 도감", out f);
            var o = PlayOptions.Current;
            var g1 = Group(f, "지금 상태"); var a = In(g1);
            lifeSummary = new Label { AutoSize = true, Font = new Font("Malgun Gothic", 13f, FontStyle.Bold), ForeColor = FriendUI.Ink, Margin = new Padding(3, 4, 3, 4) }; a.Controls.Add(lifeSummary);
            var sr = Row(a); sr.Controls.Add(Cap("포만감"));
            satietyBar = new ProgressBar { Width = 220, Height = 18, Minimum = 0, Maximum = 100, Margin = new Padding(3, 7, 3, 3) }; sr.Controls.Add(satietyBar);
            snackButton = FriendUI.Button("간식 주기", delegate { app.FeedFirst(); RefreshLive(); }); sr.Controls.Add(snackButton);
            lifeDetail = new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 3, 3, 3) }; a.Controls.Add(lifeDetail);
            Note(a, "간식은 포만감 +25씩(최대 100). 함께한 시간 1분마다 친밀도도 1씩 쌓여요. 100 단위마다 꽃다발!");
            var g2 = Group(f, "생활 설정"); var b = In(g2);
            var r = Row(b); r.Controls.Add(Cap("간식 없이 배고파지기까지", 190)); nHunger = Num(0, 360, o.HungerMinutes, 0, 1, 78); r.Controls.Add(nHunger); r.Controls.Add(Cap("분"));
            Note(b, "0분으로 설정하면 항상 우는 표정을 볼 수 있어요.");
            cHungerCry = Check(b, "배고프면 계속 우는 표정", o.HungerCry);
            cAffinity = Check(b, "친구 옆에 친밀도 숫자 표시", o.ShowAffinity);
            var g3 = Group(f, "발견한 숨은 반응"); var c = In(g3);
            secretLabel = new Label { AutoSize = true, ForeColor = FriendUI.Ink, MaximumSize = new Size(470, 0), Margin = new Padding(3, 3, 3, 3) }; c.Controls.Add(secretLabel);
            Note(c, "우클릭 → 동작 보기 → 숨은 반응에서도 확인할 수 있어요.");
            return page;
        }

        // ───────── 5. 알림 ─────────
        private Panel BuildAlarmPage()
        {
            FlowLayoutPanel f; var page = NewPage("알림", "정각 · 점심 · 퇴근 알림과 낮잠", out f);
            Settings s = app.Settings;
            var g1 = Group(f, "정각 알림"); var a = In(g1);
            cHourly = Check(a, "정각 알림 (매시간 현재 시각 알려주기)", s.HourlyOn);
            cPopup = Check(a, "별도 정각 알림창 사용 (직접 닫을 때까지 유지)", s.HourlyWindow, "끄면 친구의 말풍선으로 알려줘요.");
            cAlertPet = Check(a, "알림을 화면 가운데 큰 부기로 보여주기 (정각 · 점심 · 퇴근 · 메모)", s.AlertPet, "2배 크기 부기가 네온 빛을 두르고 나와요. 눌러서 닫을 때까지 유지돼요. 끄면 예전 알림창 · 말풍선을 써요. 메모 알림은 늘 큰 부기로 나와요.");
            cGlow = Check(a, "알림창을 닫을 때까지 화면 가장자리 빛 유지", s.EdgeGlow);
            var rc = Row(a); rc.Controls.Add(Cap("빛 색상", 70)); glowTheme = Combo(new[] { "피치 · 핑크 · 보라 · 하늘", "노을", "오로라", "무지개" }, s.EdgeGlowTheme, 190); rc.Controls.Add(glowTheme);
            var rw = Row(a); rw.Controls.Add(Cap("퍼지는 폭", 70)); glowWidth = Combo(new[] { "보통", "넓게", "아주 넓게" }, s.EdgeGlowWidth, 90); rw.Controls.Add(glowWidth);
            rw.Controls.Add(Cap("진하기")); glowStrength = Combo(new[] { "은은하게", "선명하게", "강하게" }, s.EdgeGlowStrength, 90); rw.Controls.Add(glowStrength);
            cQuiet = Check(a, "평일(월~금) 아래 시간에는 정각 알림 끄기", s.QuietWeekdays);
            var rq = Row(a); nStart = Num(0, 23, s.QuietStart, 0, 1, 58); nEnd = Num(0, 23, s.QuietEnd, 0, 1, 58);
            rq.Controls.Add(nStart); rq.Controls.Add(Cap("시 ~")); rq.Controls.Add(nEnd); rq.Controls.Add(Cap("시 (양 끝 정각 포함)"));
            cQuiet.CheckedChanged += delegate { nStart.Enabled = nEnd.Enabled = cQuiet.Checked; }; nStart.Enabled = nEnd.Enabled = cQuiet.Checked;
            a.Controls.Add(FriendUI.Button("정각창 미리보기", delegate
            {
                var n = new HourlyNotice(app.TrayIconImage); n.SetMessage(App.HourlyText(DateTime.Now) + "\n(미리보기)");
                n.EnableEdgeGlow(cGlow.Checked, glowTheme.SelectedIndex, glowWidth.SelectedIndex, glowStrength.SelectedIndex); n.Show();
            }));

            var g2 = Group(f, "점심 · 퇴근 알림"); var b = In(g2);
            cLunch = Check(b, "점심 알림", s.LunchOn);
            var rl = Row(b); rl.Controls.Add(Cap("시간", 40)); tLunch = new TextBox { Text = s.LunchTime, Width = 60, Margin = new Padding(3) }; rl.Controls.Add(tLunch);
            rl.Controls.Add(Cap("문구")); tLunchMsg = new TextBox { Text = s.LunchMsg, Width = 240, Margin = new Padding(3) }; rl.Controls.Add(tLunchMsg);
            rl.Controls.Add(FriendUI.Button("미리보기", delegate { app.PreviewLunch(tLunchMsg.Text); }));
            cLeave = Check(b, "퇴근 알림", s.LeaveOn);
            var rv = Row(b); rv.Controls.Add(Cap("시간", 40)); tLeave = new TextBox { Text = s.LeaveTime, Width = 60, Margin = new Padding(3) }; rv.Controls.Add(tLeave);
            rv.Controls.Add(Cap("문구")); tLeaveMsg = new TextBox { Text = s.LeaveMsg, Width = 240, Margin = new Padding(3) }; rv.Controls.Add(tLeaveMsg);
            rv.Controls.Add(FriendUI.Button("미리보기", delegate { app.PreviewLeave(tLeaveMsg.Text); }));
            Note(b, "시간은 12:00처럼 시:분으로 적어 주세요. 퇴근 10초 전에는 친구들이 화면 가운데로 모여요.");

            var g3 = Group(f, "낮잠"); var c = In(g3);
            cNap = Check(c, "자리를 오래 비우면 낮잠 자기", s.NapOn);
            var rn = Row(c); nNap = Num(1, 600, s.NapMinutes, 0, 1, 70); rn.Controls.Add(nNap); rn.Controls.Add(Cap("분 동안 입력이 없으면 잠들어요"));
            return page;
        }

        // ───────── 6. 화면 · 시스템 ─────────
        private Panel BuildDisplayPage()
        {
            FlowLayoutPanel f; var page = NewPage("화면 · 시스템", "투명도 · 도우미 창 · Windows 시작", out f);
            var o = PlayOptions.Current;
            var g1 = Group(f, "캐릭터 모습"); var a = In(g1);
            var rp = Row(a); rp.Controls.Add(Cap("캐릭터 진하기", 110)); nOpacity = Num(20, 100, o.PetOpacityPercent, 0, 5, 72); rp.Controls.Add(nOpacity); rp.Controls.Add(Cap("%   낮을수록 투명해요"));
            var g2 = Group(f, "도우미 창 (할 일 · 메모 · 타이머)"); var b = In(g2);
            cMemoTop = Check(b, "도우미 창을 맨 앞에 고정", o.MemoTopMost);
            var rm = Row(b); rm.Controls.Add(Cap("창 진하기", 110)); nMemoOpacity = Num(20, 100, o.MemoOpacityPercent, 0, 5, 72); rm.Controls.Add(nMemoOpacity); rm.Controls.Add(Cap("%"));
            var rcol = Row(b);
            rcol.Controls.Add(FriendUI.Button("배경색...", delegate { PickColor(true); }));
            rcol.Controls.Add(FriendUI.Button("글자색...", delegate { PickColor(false); }));
            rcol.Controls.Add(FriendUI.Button("기본 색상으로", delegate { o.HelperBackgroundArgb = FriendUI.Paper.ToArgb(); o.HelperForegroundArgb = FriendUI.Ink.ToArgb(); app.ApplyHelperOptions(); }));
            Note(b, "색상은 누르는 즉시 도우미 창에 적용돼요.");
            var g3 = Group(f, "Windows"); var c = In(g3);
            cStartup = Check(c, "Windows 로그인 시 자동으로 켜기", StartupOption.IsEnabled());
            var g4 = Group(f, "정리"); var d = In(g4);
            d.Controls.Add(FriendUI.Button("설정을 처음 상태로 되돌리기 (기록은 그대로)", delegate
            {
                if (MessageBox.Show(this, "모든 설정을 처음 값으로 되돌릴까요?\n친밀도·할 일·메모 같은 기록은 지워지지 않아요.", "부기", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                app.ResetAllSettings(); status.Text = "처음 설정으로 되돌렸어요."; Close(); app.OpenSettingsPage(lastPage);
            }));
            return page;
        }

        // ───────── 7. 메모 ─────────
        private Panel BuildMemoPage()
        {
            FlowLayoutPanel f; var page = NewPage("메모", "바탕화면 메모의 기본 모습 · 단축키 · 저장 위치", out f);
            var mgr = app.Notes;
            var g1 = Group(f, "새 메모의 기본값"); var a = In(g1);
            var rc = Row(a); rc.Controls.Add(Cap("기본 색", 70));
            noteColor = Combo(NoteColors.All.Select(k => NoteColors.Label(k)).ToArray(), mgr == null ? 0 : Array.IndexOf(NoteColors.All, mgr.DefaultColor), 120); rc.Controls.Add(noteColor);
            var rs = Row(a); rs.Controls.Add(Cap("기본 크기", 70));
            noteSize = Combo(new[] { "작게  240×180", "보통  320×260", "크게  420×360" }, mgr == null ? 1 : (int)mgr.DefaultSize, 150); rs.Controls.Add(noteSize);
            Note(a, "저장한 다음에 만드는 메모부터 적용돼요. 이미 있는 메모의 색과 크기는 그대로예요.");
            var g2 = Group(f, "어디서나 쓰는 단축키"); var b = In(g2);
            Note(b, "Ctrl+Alt+N   새 메모\nCtrl+Shift+F   메모 라이브러리\nCtrl+Shift+P   마지막으로 쓴 메모를 항상 위에 두기\nCtrl+Shift+R   마지막으로 쓴 메모에 1시간 뒤 알림");
            Note(b, "다른 프로그램이 이미 쓰는 조합이면 등록되지 않아요. 트레이·우클릭 메뉴의 새 메모와, 메모 위젯 ⋯ 메뉴의 메모 라이브러리는 늘 쓸 수 있어요.");
            var g3 = Group(f, "메모를 쓰는 중"); var c = In(g3);
            Note(c, "Ctrl+B / I / U   굵게 · 기울임 · 밑줄       Ctrl+Shift+X   취소선\nCtrl+K   링크       Ctrl+Shift+K   코드       Ctrl+1 / 2 / 3   제목\nCtrl+Shift+L   글머리 목록       Ctrl+Shift+C   체크리스트\nCtrl+Enter   커서 줄의 체크박스 토글       Enter   목록 이어쓰기(빈 항목에서 목록 끝)");
            var g4 = Group(f, "내 메모는 어디에"); var d = In(g4);
            noteInfo = Note(d, ""); noteInfo.Text = mgr == null ? "" : mgr.Store.Root + "\n메모 " + mgr.Store.Count.ToString("N0") + "개";
            d.Controls.Add(FriendUI.Button("폴더 열기", delegate { try { if (mgr != null) System.Diagnostics.Process.Start("explorer.exe", "\"" + mgr.Store.Root + "\""); } catch { } }));
            Note(d, "백업은 부기를 끈 뒤 이 폴더를 통째로 복사하면 돼요. 삭제는 되돌릴 수 없어요(휴지통이 없어요).");
            return page;
        }

        // ───────── 8. AI 대화 ─────────
        private Panel BuildChatPage()
        {
            FlowLayoutPanel f; var page = NewPage("AI 대화", "이 PC 의 Ollama 와 이야기해요. 대화하는 동안 부기의 말풍선에 작은 AI 표시가 붙어요", out f);
            var o = ChatOptions.Current;
            var g1 = Group(f, "연결"); var a = In(g1);
            cChat = Check(a, "AI 대화 사용 (더블클릭 · 우클릭 → 말 걸기)", o.Enabled);
            var rm = Row(a); rm.Controls.Add(Cap("모델", 60)); tChatModel = new TextBox { Text = o.Model, Width = 220, Margin = new Padding(3) }; rm.Controls.Add(tChatModel);
            var ru = Row(a); ru.Controls.Add(Cap("서버 주소", 60)); tChatUrl = new TextBox { Text = o.Endpoint, Width = 220, Margin = new Padding(3) }; ru.Controls.Add(tChatUrl);
            var rp = Row(a);
            chatProbe = new Label { AutoSize = true, Margin = new Padding(6, 8, 3, 3), ForeColor = Color.DimGray };
            rp.Controls.Add(FriendUI.Button("연결 확인", delegate { ProbeChat(); })); rp.Controls.Add(chatProbe);
            Note(a, "대화 내용은 위 서버로만 보내져요. 기본값(localhost)이면 이 PC 밖으로 나가지 않아요. 첫 말은 모델을 불러오느라 몇 초 걸릴 수 있어요.");
            var g2 = Group(f, "말 걸기"); var b = In(g2);
            cChatKey = Check(b, "단축키 Ctrl + Alt + B 로 말 걸기", o.Hotkey);
            cChatChips = Check(b, "부기의 말 끝에 답장 추천 칩 보여주기", o.Chips, "눌러서 바로 보낼 수 있는 짧은 말이에요(Ctrl+1~3). 글자를 치기 시작하면 사라져요.");
            cChatSound = Check(b, "말할 때 글자마다 뽁뽁 소리 내기", o.Sound, "기본은 꺼져 있어요.");
            cChatProactive = Check(b, "부기가 먼저 말 걸기 (선톡)", o.Proactive, "컴퓨터를 쓰는 중일 때 가끔 한마디 건네요. 켜면 모델이 메모리에 올라가 있어요.");
            var rq = Row(b); rq.Controls.Add(Cap("평균 간격", 70)); nProactive = Num(5, 480, o.ProactiveMinutes, 0, 5, 70); rq.Controls.Add(nProactive); rq.Controls.Add(Cap("분"));
            var g3 = Group(f, "기억"); var c = In(g3);
            cChatMemory = Check(c, "대화에서 알게 된 이야기를 기억하기", o.Memory, "대화가 끝날 때 사실만 한두 줄로 적어 둬요. 이 PC 의 파일에만 저장돼요.");
            c.Controls.Add(FriendUI.Button("기억 보기 · 지우기...", delegate { new ChatMemoryForm(app.TrayIconImage).ShowDialog(this); }));
            var g4 = Group(f, "부기의 일기장"); var d = In(g4);
            cChatDiary = Check(d, "대화가 끝나면 부기가 일기 한 줄 쓰기", o.Diary, "오늘 나눈 이야기를 부기의 시점에서 한두 문장으로 적어 둬요. 이 PC 의 파일(부기_일기.txt)에만 저장돼요.");
            d.Controls.Add(FriendUI.Button("일기장 열기...", delegate { new ChatDiaryForm(app.TrayIconImage).ShowDialog(this); }));
            return page;
        }

        private void ProbeChat()
        {
            var probe = new ChatOptions { Endpoint = tChatUrl.Text.Trim().TrimEnd('/'), Model = tChatModel.Text.Trim() };
            chatProbe.ForeColor = Color.DimGray; chatProbe.Text = "확인하는 중...";
            new System.Threading.Thread(delegate ()
            {
                ProbeResult r = OllamaClient.Probe(probe);
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        chatProbe.ForeColor = r == ProbeResult.Ok ? Color.FromArgb(30, 130, 70) : Color.Firebrick;
                        chatProbe.Text = r == ProbeResult.Ok ? "연결됐어요. 모델도 있어요." : r == ProbeResult.ModelMissing ? "서버는 켜져 있는데 이 모델이 없어요." : "서버에 연결하지 못했어요. Ollama 가 켜져 있나요?";
                    }));
                }
                catch { }
            }) { IsBackground = true }.Start();
        }

        private void PickColor(bool background)
        {
            var o = PlayOptions.Current;
            using (var chooser = new ColorDialog { FullOpen = true, Color = Color.FromArgb(background ? o.HelperBackgroundArgb : o.HelperForegroundArgb) })
                if (chooser.ShowDialog(this) == DialogResult.OK)
                {
                    int argb = Color.FromArgb(chooser.Color.R, chooser.Color.G, chooser.Color.B).ToArgb();
                    if (background) o.HelperBackgroundArgb = argb; else o.HelperForegroundArgb = argb;
                    app.ApplyHelperOptions();
                }
        }

        // ───────── 실시간 표시 ─────────
        private void RefreshLive()
        {
            if (lifeSummary == null) return;
            var d = FriendData.Current;
            lifeSummary.Text = "♥ 친밀도 " + d.Affinity.ToString("N0") + "   ·   함께 " + FriendData.FormatTogether(d.TogetherSeconds);
            satietyBar.Value = Math.Max(0, Math.Min(100, (int)Math.Floor(d.Satiety)));
            lifeDetail.Text = "포만감 " + (int)Math.Floor(d.Satiety) + " / 100   ·   다음 꽃다발: 친밀도 " + ((d.Affinity / 100 + 1) * 100).ToString("N0") + "   ·   " + (d.CanSnack ? "지금 간식을 줄 수 있어요" : "지금은 배가 꽉 찼어요");
            snackButton.Enabled = d.CanSnack;
            var names = new[] { new[] { "dance", "몰래 하는 군무" }, new[] { "dizzy", "어질어질" }, new[] { "pilot", "비밀 조종사" } };
            secretLabel.Text = "발견한 숨은 반응 " + names.Count(p => d.Discoveries.Contains(p[0])) + " / 3\n" + string.Join("  ·  ", names.Where(p => d.Discoveries.Contains(p[0])).Select(p => p[1]).ToArray());
            RefreshRoster();
        }

        // ───────── 저장 ─────────
        private static string Norm(string v) { TimeSpan t = Settings.ParseTime(v).Value; return string.Format("{0:00}:{1:00}", t.Hours, t.Minutes); }
        private void Warn(string m) { MessageBox.Show(this, m, "부기", MessageBoxButtons.OK, MessageBoxIcon.Asterisk); }

        private bool Apply()
        {
            if (!Settings.ParseTime(tLunch.Text).HasValue) { nav.SelectedIndex = 4; Warn("점심 시간을 12:00처럼 적어 주세요."); tLunch.Focus(); return false; }
            if (!Settings.ParseTime(tLeave.Text).HasValue) { nav.SelectedIndex = 4; Warn("퇴근 시간을 18:00처럼 적어 주세요."); tLeave.Focus(); return false; }
            try { StartupOption.SetEnabled(cStartup.Checked); }
            catch (Exception ex) { nav.SelectedIndex = 5; Warn("자동 시작 설정을 저장하지 못했어요.\n" + ex.Message); return false; }

            var o = PlayOptions.Current;
            o.Outfit = cOutfit.Checked;
            o.Walls = cWalls.Checked; o.Ceiling = cCeiling.Checked; o.Windows = cWindows.Checked; o.Fullscreen = cFull.Checked;
            o.Idle = cIdle.Checked; o.ShortRest = cShortRest.Checked; o.Focus = cFocus.Checked; o.WorkFocus = cWork.Checked; o.PositionFixed = cPinned.Checked;
            o.Speed = (double)nSpeed.Value; o.FallSpeed = (double)nFall.Value; o.SlideSpeed = (double)nSlide.Value;
            o.ClickMode = (ClickReaction)clickMode.SelectedIndex;
            o.Escape = rEscape.Checked; o.Follow = rFollow.Checked; o.EscapeDelay = (double)nDelay.Value; o.Secrets = cSecrets.Checked;
            o.HungerMinutes = (int)nHunger.Value; o.HungerCry = cHungerCry.Checked; o.ShowAffinity = cAffinity.Checked;
            o.PetOpacityPercent = (int)nOpacity.Value;
            o.MemoTopMost = cMemoTop.Checked; o.MemoOpacityPercent = (int)nMemoOpacity.Value;

            Settings s = app.Settings;
            s.HourlyOn = cHourly.Checked; s.HourlyWindow = cPopup.Checked; s.AlertPet = cAlertPet.Checked; s.EdgeGlow = cGlow.Checked;
            s.EdgeGlowTheme = glowTheme.SelectedIndex; s.EdgeGlowWidth = glowWidth.SelectedIndex; s.EdgeGlowStrength = glowStrength.SelectedIndex;
            s.QuietWeekdays = cQuiet.Checked; s.QuietStart = (int)nStart.Value; s.QuietEnd = (int)nEnd.Value;
            s.LunchOn = cLunch.Checked; s.LunchTime = Norm(tLunch.Text); s.LunchMsg = tLunchMsg.Text.Trim();
            s.LeaveOn = cLeave.Checked; s.LeaveTime = Norm(tLeave.Text); s.LeaveMsg = tLeaveMsg.Text.Trim();
            s.NapOn = cNap.Checked; s.NapMinutes = (int)nNap.Value;
            if (s.LunchMsg.Length == 0) s.LunchMsg = "점심 먹으러 가자!";
            if (s.LeaveMsg.Length == 0) s.LeaveMsg = "퇴근이다! 오늘도 수고했어";

            var chat = ChatOptions.Current;
            chat.Enabled = cChat.Checked; chat.Model = tChatModel.Text.Trim().Length > 0 ? tChatModel.Text.Trim() : "gemma4:e2b";
            chat.Endpoint = tChatUrl.Text.Trim().Length > 0 ? tChatUrl.Text.Trim().TrimEnd('/') : "http://localhost:11434";
            chat.Hotkey = cChatKey.Checked; chat.Sound = cChatSound.Checked; chat.Proactive = cChatProactive.Checked; chat.ProactiveMinutes = (int)nProactive.Value; chat.Memory = cChatMemory.Checked; chat.Chips = cChatChips.Checked; chat.Diary = cChatDiary.Checked;

            app.OnSettingsChanged();
            app.ApplyCharacterSettings(); app.ApplyHelperOptions(); app.Chat.ApplyOptions();
            if (app.Notes != null) { app.Notes.DefaultColor = NoteColors.All[Math.Max(0, noteColor.SelectedIndex)]; app.Notes.DefaultSize = (NoteSizePreset)Math.Max(0, noteSize.SelectedIndex); }
            bool ok = s.Save() & o.Save() & chat.Save();
            status.Text = ok ? "저장했어요." : "설정 파일을 저장하지 못했어요. 이번에 켜 있는 동안만 적용돼요.";
            if (!ok) Warn(status.Text);
            return true;
        }
    }
}
