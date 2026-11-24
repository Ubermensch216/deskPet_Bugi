using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace Bugi
{
    /// <summary>
    /// AI(Ollama + 모델) 가 없는 PC 에서 설정창의 "AI 자동 설치" 가 부르는 곳. 일꾼 스레드에서 부른다.
    ///   서버 확인 → (없으면) 설치 파일 내려받기 · 서명 확인 · 조용히 설치 → 서버 켜기 → (없으면) 모델 받기
    /// 큰 내려받기 전에는 반드시 confirm 으로 사용자 동의를 받는다.
    /// </summary>
    internal static class OllamaSetup
    {
        internal const string InstallerUrl = "https://ollama.com/download/OllamaSetup.exe";
        internal const string Cancelled = "cancelled";
        /// <summary>Pull 이 돌려주는 내부 신호: 이 PC 의 Ollama 가 오래돼서 이 모델을 받을 수 없다(서버 오류 412).</summary>
        private const string NeedsUpdate = "needs-update";

        // 여유 공간 기준(2026-11 실측: 설치 파일 1.73 GB, 설치된 Ollama 3.1 GB, gemma4:e2b 4.6 GB). 다른 모델은 크기를 미리 알 수 없어서 기본 모델 기준으로 본다.
        private const long InstallerBytes = 2L << 30, InstalledBytes = 7L << 29, ModelBytes = 5L << 30;   // 2 GB · 3.5 GB · 5 GB

        /// <summary>모델 받기 오류가 "Ollama 가 오래됐다"는 뜻인지. 예: "pull model manifest: 412: … requires a newer version of Ollama …"</summary>
        internal static bool IsTooOldError(string err)
        {
            return err != null && (err.IndexOf("newer version", StringComparison.OrdinalIgnoreCase) >= 0 || err.Contains(": 412"));
        }

        /// <summary>자동 설치는 이 PC 의 서버(localhost)에만 쓴다. 다른 PC 의 서버에는 설치할 수 없다.</summary>
        internal static bool IsLocalEndpoint(string endpoint)
        {
            Uri u;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out u)) return false;
            return u.IsLoopback || u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>설치된 ollama.exe 를 찾는다. 없으면 null.</summary>
        internal static string FindExe()
        {
            var dirs = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama")
            };
            foreach (string d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)) if (d.Trim().Length > 0) dirs.Add(d.Trim());
            foreach (string d in dirs)
                try { string p = Path.Combine(d, "ollama.exe"); if (File.Exists(p)) return p; } catch { }
            return null;
        }

        /// <summary>
        /// 끝까지 해 본다. 성공하면 null, 사용자가 멈추면 <see cref="Cancelled"/>, 실패하면 이유를 돌려준다.
        /// report(글, 퍼센트(-1 이면 모름), 취소 가능 여부) · confirm(내려받기 안내글) → 계속할지.
        /// </summary>
        internal static string Run(ChatOptions o, ChatRequest h, Action<string, int, bool> report, Func<string, bool> confirm)
        {
            report(Loc.T("지금 상태를 확인하는 중..."), -1, true);
            ProbeResult r = OllamaClient.Probe(o);
            if (r == ProbeResult.Ok) return null;
            bool agreed = false, installedNow = false;

            if (r == ProbeResult.Offline)
            {
                string exe = FindExe();
                if (exe == null)
                {
                    string space = CheckSpace(true, true);
                    if (space != null) return space;
                    long free = FreeBytes(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                    if (!confirm(Loc.T("이 PC 에는 Ollama 가 없어요.\n\n")
                        + Loc.TF("① Ollama 설치 파일(공식 ollama.com)을 내려받아 설치하고\n② AI 모델 '{0}' 을 받아요.\n\n", o.Model)
                        + Loc.TF("용량이 커서(합쳐서 수 GB 일 수 있어요) 시간이 걸려요. 인터넷 연결이 필요하고, 사용자 폴더의 여유 공간은 약 {0} 이에요.\n계속할까요?", FmtBytes(free)))) return Cancelled;
                    agreed = true; installedNow = true;
                    string err = DownloadAndInstall(h, report, false);
                    if (err != null) return err;
                    exe = FindExe();
                    if (exe == null) return Loc.T("설치는 끝났는데 Ollama 를 찾지 못했어요. 컴퓨터를 다시 켠 뒤 다시 눌러 주세요.");
                }
                if (h.Cancelled) return Cancelled;
                string serr = WaitServer(o, exe, h, report);
                if (serr != null) return serr;
                r = OllamaClient.Probe(o);
            }

            if (r == ProbeResult.ModelMissing)
            {
                if (!agreed)
                {
                    string space = CheckSpace(false, true);
                    if (space != null) return space;
                    if (!confirm(Loc.TF("AI 모델 '{0}' 을 내려받아요.\n\n용량이 커서(수 GB 일 수 있어요) 시간이 걸려요. 인터넷 연결이 필요해요.\n계속할까요?", o.Model))) return Cancelled;
                }
                string perr = Pull(o, h, report);
                if (perr == NeedsUpdate)
                {
                    // 이미 깔려 있던 Ollama 가 오래된 경우: 같은 공식 설치 파일로 덮어 설치(업데이트)한 뒤 한 번 더 받는다.
                    string tooOld = Loc.TF("Ollama 가 오래돼서 AI 모델 '{0}' 을 받지 못했어요. ollama.com 에서 새 버전을 설치한 뒤 다시 눌러 주세요.", o.Model);
                    if (installedNow) return tooOld;   // 방금 최신으로 깔았는데도 안 되면 되풀이하지 않는다
                    string space = CheckSpace(true, false);
                    if (space != null) return space;
                    if (!confirm(Loc.TF("이 PC 의 Ollama 가 오래돼서 AI 모델 '{0}' 을 받을 수 없어요.\n\nOllama 를 새 버전으로 바꿀까요?\n공식 설치 파일(약 2 GB)을 내려받아 덮어 설치해요. 받아 둔 모델과 설정은 그대로이고, 설치하는 동안 Ollama 가 잠깐 꺼져요.", o.Model))) return tooOld;
                    string err = DownloadAndInstall(h, report, true);
                    if (err != null) return err;
                    string exe = FindExe();
                    if (exe == null) return Loc.T("설치는 끝났는데 Ollama 를 찾지 못했어요. 컴퓨터를 다시 켠 뒤 다시 눌러 주세요.");
                    string serr = WaitServer(o, exe, h, report);
                    if (serr != null) return serr;
                    perr = Pull(o, h, report);
                    if (perr == NeedsUpdate) return tooOld;
                }
                if (perr != null) return perr;
            }

            report(Loc.T("마무리 확인 중..."), -1, false);
            return OllamaClient.Probe(o) == ProbeResult.Ok ? null : Loc.T("설치는 했는데 아직 연결이 안 돼요. 잠시 뒤 '연결 확인'을 눌러 보세요.");
        }

        // ───────── 설치 파일 받기 → 서명 확인 → 설치 ─────────
        /// <summary>새로 설치할 때와 오래된 Ollama 를 덮어 설치할 때(upgrade) 함께 쓴다. 괜찮으면 null.</summary>
        private static string DownloadAndInstall(ChatRequest h, Action<string, int, bool> report, bool upgrade)
        {
            string dir = Path.Combine(Path.GetTempPath(), "BugiOllamaSetup_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(dir);
                string setup = Path.Combine(dir, "OllamaSetup.exe");
                string err = Download(h, InstallerUrl, setup, report);
                if (err != null) return err;
                if (h.Cancelled) return Cancelled;
                report(Loc.T("설치 파일의 서명을 확인하는 중..."), -1, true);
                err = VerifySignature(setup);
                if (err != null) return err;
                if (upgrade) StopOllama(report);   // 켜져 있는 Ollama 가 파일을 붙잡고 있으면 덮어 쓰지 못한다
                return Install(setup, report);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        /// <summary>덮어 설치하기 전에 켜져 있는 Ollama(트레이 앱 · 서버 · 모델 실행기)를 끈다. 끄지 못한 것은 설치 프로그램에 맡긴다.</summary>
        private static void StopOllama(Action<string, int, bool> report)
        {
            report(Loc.T("켜져 있는 Ollama 를 잠깐 끄는 중..."), -1, false);
            foreach (string name in new[] { "ollama app", "ollama" })   // 트레이 앱을 먼저 꺼야 서버를 다시 켜지 않는다
                foreach (Process p in Process.GetProcessesByName(name))
                    using (p) { try { p.Kill(); p.WaitForExit(5000); } catch { } }
        }

        // ───────── 여유 공간 ─────────
        /// <summary>Ollama 가 모델을 두는 폴더(OLLAMA_MODELS 가 있으면 그곳).</summary>
        private static string ModelsDir()
        {
            string env = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
            if (string.IsNullOrEmpty(env)) env = Environment.GetEnvironmentVariable("OLLAMA_MODELS", EnvironmentVariableTarget.User);
            return !string.IsNullOrEmpty(env) ? env : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollama", "models");
        }

        /// <summary>
        /// 설치(install) · 모델 받기(model)에 필요한 공간이 드라이브마다 있는지 본다. 모자라면 안내글, 넉넉하거나 알 수 없으면 null.
        /// 설치 파일(임시 폴더)과 설치된 Ollama(%LocalAppData%)는 설치하는 동안 함께 있으므로 더해서 본다.
        /// </summary>
        private static string CheckSpace(bool install, bool model)
        {
            var need = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            Action<string, long> add = (path, bytes) =>
            {
                string root; try { root = Path.GetPathRoot(Path.GetFullPath(path)); } catch { return; }
                if (string.IsNullOrEmpty(root)) return;
                long v; need.TryGetValue(root, out v); need[root] = v + bytes;
            };
            if (install)
            {
                add(Path.GetTempPath(), InstallerBytes);
                add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), InstalledBytes);
            }
            if (model) add(ModelsDir(), ModelBytes);
            foreach (var kv in need)
            {
                long free = FreeBytes(kv.Key);
                if (free > 0 && free < kv.Value)
                    return Loc.TF("{0} 드라이브의 여유 공간이 모자라요. (남은 공간 {1} · 필요한 공간 약 {2})\n공간을 비운 뒤 다시 눌러 주세요.", kv.Key.TrimEnd('\\'), FmtBytes(free), FmtBytes(kv.Value));
            }
            return null;
        }

        // ───────── 내려받기 ─────────
        private static string Download(ChatRequest h, string url, string path, Action<string, int, bool> report)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2 (.NET 4.8 기본값이 오래된 경우가 있어서)
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Bugi-DeskPet"; req.Timeout = 30000; req.ReadWriteTimeout = 30000;
                if (req.Proxy != null) req.Proxy.Credentials = CredentialCache.DefaultCredentials;   // 회사 프록시 대응
                h.Attach(req);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (Stream src = resp.GetResponseStream())
                using (FileStream dst = File.Create(path))
                {
                    long total = resp.ContentLength, got = 0; int n, lastPct = -2; var buf = new byte[81920];
                    var tick = Stopwatch.StartNew(); long lastTick = -1000;
                    while ((n = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        if (h.Cancelled) return Cancelled;
                        dst.Write(buf, 0, n); got += n;
                        int pct = total > 0 ? (int)(got * 100 / total) : -1;
                        if (pct != lastPct && tick.ElapsedMilliseconds - lastTick >= 150)
                        {
                            lastPct = pct; lastTick = tick.ElapsedMilliseconds;
                            report(Loc.T("Ollama 설치 파일 내려받는 중... ") + (total > 0 ? pct + "%  (" + FmtBytes(got) + " / " + FmtBytes(total) + ")" : FmtBytes(got)), pct, true);
                        }
                    }
                    if (total > 0 && got != total) return Loc.T("내려받다가 연결이 끊겼어요. 인터넷 연결을 확인하고 다시 눌러 주세요.");
                }
                return null;
            }
            catch (Exception e) { return h.Cancelled ? Cancelled : Loc.T("설치 파일을 내려받지 못했어요. 인터넷 연결을 확인해 주세요.\n(") + e.Message + ")"; }
        }

        // ───────── 서명 확인 (Authenticode) ─────────
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo { public int cbStruct; public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }

        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustData
        {
            public int cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData; public int dwUIChoice; public int fdwRevocationChecks;
            public int dwUnionChoice; public IntPtr pFile; public int dwStateAction; public IntPtr hWVTStateData; public IntPtr pwszURLReference;
            public int dwProvFlags; public int dwUIContext; public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
        private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, IntPtr pWVTData);

        /// <summary>파일에 올바른 디지털 서명이 있는지(변조 · 손상 여부 포함) 본다. 괜찮으면 null.</summary>
        internal static string VerifySignature(string path)
        {
            IntPtr pf = IntPtr.Zero, pd = IntPtr.Zero;
            try
            {
                var fi = new WinTrustFileInfo { cbStruct = Marshal.SizeOf(typeof(WinTrustFileInfo)), pcwszFilePath = path };
                pf = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustFileInfo))); Marshal.StructureToPtr(fi, pf, false);
                var data = new WinTrustData { cbStruct = Marshal.SizeOf(typeof(WinTrustData)), dwUIChoice = 2 /*UI 없음*/, fdwRevocationChecks = 0, dwUnionChoice = 1 /*파일*/, pFile = pf, dwStateAction = 0 };
                pd = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustData))); Marshal.StructureToPtr(data, pd, false);
                int rc = WinVerifyTrust(new IntPtr(-1), new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE"), pd);
                if (rc != 0) return Loc.T("내려받은 설치 파일의 디지털 서명이 올바르지 않아서 설치하지 않았어요. (코드 0x") + rc.ToString("X8") + ")";
                // 서명이 올바르기만 하면 누구 서명이든 통과하므로, 서명한 곳이 Ollama 인지도 본다(2026-11 확인: "CN=Ollama Inc., O=Ollama Inc., …").
                string subject = null;
                try { subject = X509Certificate.CreateFromSignedFile(path).Subject; } catch { }
                if (subject == null || subject.IndexOf("Ollama", StringComparison.OrdinalIgnoreCase) < 0)
                    return Loc.TF("설치 파일에 서명한 곳이 Ollama 가 아니어서 설치하지 않았어요. ({0})", subject ?? "?");
                return null;
            }
            catch (Exception e) { return Loc.T("설치 파일의 서명을 확인하지 못해서 설치하지 않았어요. (") + e.Message + ")"; }
            finally { if (pd != IntPtr.Zero) Marshal.FreeHGlobal(pd); if (pf != IntPtr.Zero) Marshal.FreeHGlobal(pf); }
        }

        // ───────── 설치 · 서버 켜기 ─────────
        private static string Install(string setup, Action<string, int, bool> report)
        {
            report(Loc.T("Ollama 를 설치하는 중... (1~2분 걸려요. 취소할 수 없어요)"), -1, false);
            try
            {
                using (Process p = Process.Start(new ProcessStartInfo(setup, "/SILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true }))
                {
                    if (p == null) return Loc.T("설치 프로그램을 실행하지 못했어요.");
                    // 오래 걸려도 Ollama 가 이미 깔렸으면 계속 간다(safeDoc setup.ps1 과 같게). 남은 설치 프로그램은 그대로 둔다.
                    if (!p.WaitForExit(15 * 60 * 1000)) return FindExe() != null ? null : Loc.T("설치가 너무 오래 걸려서 멈췄어요.");
                    if (p.ExitCode != 0) return Loc.T("설치 프로그램이 오류로 끝났어요. (코드 ") + p.ExitCode + ")";
                }
                return null;
            }
            catch (Win32Exception e) { return e.NativeErrorCode == 1223 ? Loc.T("설치 허용을 취소했어요.") : Loc.T("설치 프로그램을 실행하지 못했어요. (") + e.Message + ")"; }
            catch (Exception e) { return Loc.T("설치 프로그램을 실행하지 못했어요. (") + e.Message + ")"; }
        }

        private static void StartServer(string exe)
        {
            try
            {
                string app = Path.Combine(Path.GetDirectoryName(exe), "ollama app.exe");   // 트레이 앱(서버 포함)
                if (File.Exists(app)) Process.Start(new ProcessStartInfo(app) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(app) });
                else Process.Start(new ProcessStartInfo(exe, "serve") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            }
            catch { }
        }

        /// <summary>서버가 뜰 때까지 기다린다(설치 직후 저절로 켜지기도 해서 몇 초 뒤에도 안 떠 있으면 직접 켠다).</summary>
        private static string WaitServer(ChatOptions o, string exe, ChatRequest h, Action<string, int, bool> report)
        {
            var sw = Stopwatch.StartNew(); bool started = false;
            while (sw.Elapsed.TotalSeconds < 45)
            {
                if (h.Cancelled) return Cancelled;
                if (OllamaClient.Probe(o) != ProbeResult.Offline) return null;
                if (!started && sw.Elapsed.TotalSeconds >= 5) { started = true; StartServer(exe); }
                report(Loc.T("Ollama 를 켜는 중..."), -1, true);
                Thread.Sleep(800);
            }
            return Loc.T("Ollama 를 켜지 못했어요. 시작 메뉴에서 Ollama 를 직접 실행한 뒤 다시 눌러 주세요.");
        }

        // ───────── 모델 받기 ─────────
        /// <summary>/api/pull 을 줄 단위 JSON 으로 받으며 레이어별 진행량을 모아 전체 퍼센트로 보여 준다.</summary>
        private static string Pull(ChatOptions o, ChatRequest h, Action<string, int, bool> report)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(o.Endpoint.TrimEnd('/') + "/api/pull");
                req.Method = "POST"; req.Timeout = 30000; req.ReadWriteTimeout = 180000; req.Proxy = null; req.UserAgent = "Bugi-DeskPet";
                byte[] body = new UTF8Encoding(false).GetBytes("{\"model\":" + Json.Quote(o.Model) + ",\"stream\":true}");
                req.ContentType = "application/json"; req.ContentLength = body.Length;
                h.Attach(req);
                using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
                var totals = new Dictionary<string, double>(); var dones = new Dictionary<string, double>();
                bool success = false; int shown = 0;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (h.Cancelled) return Cancelled;
                        if (line.Length == 0) continue;
                        object j = Json.Parse(line);
                        string err = Json.Str(j, "error"); if (err != null) return IsTooOldError(err) ? NeedsUpdate : Loc.T("모델을 받지 못했어요. (") + err + ")";
                        string status = Json.Str(j, "status") ?? "", digest = Json.Str(j, "digest");
                        if (status == "success") { success = true; continue; }
                        if (digest != null && Json.Get(j, "total") is double)
                        {
                            totals[digest] = (double)Json.Get(j, "total");
                            dones[digest] = Json.Get(j, "completed") is double ? (double)Json.Get(j, "completed") : 0;
                            double t = 0, d = 0; foreach (double v in totals.Values) t += v; foreach (double v in dones.Values) d += v;
                            int pct = t > 0 ? (int)Math.Min(100, d * 100 / t) : -1;
                            if (pct >= 0) { pct = Math.Max(pct, shown); shown = pct; }   // 레이어가 새로 나타나도 막대가 뒤로 가지 않게
                            report(Loc.TF("AI 모델 '{0}' 받는 중... {1}%  ({2} / {3})", o.Model, pct, FmtBytes((long)d), FmtBytes((long)t)), pct, true);
                        }
                        else if (status.StartsWith("pulling manifest")) report(Loc.T("모델 정보를 확인하는 중..."), -1, true);
                        else if (status.StartsWith("verifying")) report(Loc.T("내려받은 모델을 확인하는 중..."), -1, true);
                        else if (status.StartsWith("writing")) report(Loc.T("모델을 저장하는 중..."), -1, true);
                    }
                }
                return success ? null : Loc.T("모델 받기가 중간에 끝났어요. 다시 누르면 이어서 받아요.");
            }
            catch (Exception e)
            {
                var we = e as WebException; var wr = we != null ? we.Response as HttpWebResponse : null;
                if (wr != null && (int)wr.StatusCode == 412) return NeedsUpdate;   // 오래된 Ollama(서버가 오류를 상태 코드로 줄 때)
                return h.Cancelled ? Cancelled : Loc.T("모델을 받는 중에 연결이 끊겼어요. 다시 누르면 이어서 받아요.\n(") + e.Message + ")";
            }
        }

        // ───────── 도우미 ─────────
        private static long FreeBytes(string path)
        {
            try { return new DriveInfo(Path.GetPathRoot(path)).AvailableFreeSpace; } catch { return 0; }
        }

        internal static string FmtBytes(long b)
        {
            if (b >= 1L << 30) return (b / (double)(1L << 30)).ToString("0.0") + " GB";
            if (b >= 1L << 20) return (b / (double)(1L << 20)).ToString("0") + " MB";
            return Math.Max(0, b / 1024) + " KB";
        }
    }
}
