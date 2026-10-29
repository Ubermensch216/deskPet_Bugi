using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Bugi
{
    /// <summary>AI 대화 설정 (부기_대화.ini). 로컬 Ollama 와만 이야기하는 것이 기본이다.</summary>
    internal sealed class ChatOptions
    {
        internal bool Enabled = true;
        internal string Endpoint = "http://localhost:11434";
        internal string Model = "gemma4:e2b";
        internal string KeepAlive = "30m";
        internal bool Hotkey = true;          // Ctrl+Alt+B 로 말 걸기
        internal bool Sound = false;          // 말할 때 뽁뽁 소리
        internal bool Memory = true;          // 대화에서 알게 된 사실을 기억하기
        internal bool Proactive = false;      // 부기가 먼저 말 걸기
        internal int ProactiveMinutes = 40;   // 먼저 말 거는 평균 간격(분)

        internal static ChatOptions Current = new ChatOptions();
        internal const string Name = "부기_대화.ini";
        internal static string FilePath { get { return Path.Combine(Paths.Dir, Name); } }

        internal static void Load()
        {
            var o = Current = new ChatOptions();
            try
            {
                if (!File.Exists(FilePath)) return;
                var d = Settings.ReadIni(FilePath); string v; int n;
                Func<string, bool, bool> flag = (k, def) => d.TryGetValue(k, out v) ? v != "0" : def;
                o.Enabled = flag("Enabled", o.Enabled); o.Hotkey = flag("Hotkey", o.Hotkey); o.Sound = flag("Sound", o.Sound);
                o.Memory = flag("Memory", o.Memory); o.Proactive = flag("Proactive", o.Proactive);
                if (d.TryGetValue("Endpoint", out v) && v.Length > 0) o.Endpoint = v.TrimEnd('/');
                if (d.TryGetValue("Model", out v) && v.Length > 0) o.Model = v;
                if (d.TryGetValue("KeepAlive", out v) && v.Length > 0) o.KeepAlive = v;
                if (d.TryGetValue("ProactiveMinutes", out v) && int.TryParse(v, out n)) o.ProactiveMinutes = Math.Max(5, Math.Min(480, n));
            }
            catch { }
        }

        internal bool Save()
        {
            Func<bool, string> b = x => x ? "1" : "0";
            var t = new StringBuilder("; 부기 AI 대화 설정 (로컬 Ollama)\r\n");
            t.Append("Enabled=" + b(Enabled) + "\r\nEndpoint=" + Endpoint + "\r\nModel=" + Model + "\r\nKeepAlive=" + KeepAlive + "\r\n");
            t.Append("Hotkey=" + b(Hotkey) + "\r\nSound=" + b(Sound) + "\r\nMemory=" + b(Memory) + "\r\n");
            t.Append("Proactive=" + b(Proactive) + "\r\nProactiveMinutes=" + ProactiveMinutes + "\r\n");
            try { Directory.CreateDirectory(Paths.Dir); File.WriteAllText(FilePath, t.ToString(), new UTF8Encoding(true)); return true; }
            catch { return false; }
        }
    }

    /// <summary>부기가 대화에서 알게 된 사용자 이야기(부기_기억.txt, 한 줄에 하나). 사용자가 보고 지울 수 있다.</summary>
    internal static class ChatMemory
    {
        internal const int MaxFacts = 30;
        internal const string Name = "부기_기억.txt";
        private static List<string> facts;
        internal static string FilePath { get { return Path.Combine(Paths.Dir, Name); } }

        internal static List<string> Facts
        {
            get
            {
                if (facts != null) return facts;
                facts = new List<string>();
                try { if (File.Exists(FilePath)) facts.AddRange(File.ReadAllLines(FilePath, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0)); }
                catch { }
                return facts;
            }
        }

        internal static void Reload() { facts = null; }

        internal static bool Save()
        {
            try { Directory.CreateDirectory(Paths.Dir); File.WriteAllLines(FilePath, Facts.ToArray(), new UTF8Encoding(true)); return true; }
            catch { return false; }
        }

        /// <summary>같은 이야기가 이미 있으면 넣지 않는다. 가득 차면 가장 오래된 것부터 잊는다.</summary>
        internal static int Add(IEnumerable<string> newFacts)
        {
            int added = 0;
            foreach (string raw in newFacts)
            {
                string f = (raw ?? "").Trim().TrimStart('-', '*', '·', ' ').Trim();
                if (f.Length < 3 || f.Length > 80 || f == "없음") continue;
                if (Facts.Any(x => x == f || x.Contains(f) || f.Contains(x))) continue;
                Facts.Add(f); added++;
            }
            while (Facts.Count > MaxFacts) Facts.RemoveAt(0);
            if (added > 0) Save();
            return added;
        }

        internal static void RemoveAt(int index) { if (index >= 0 && index < Facts.Count) { Facts.RemoveAt(index); Save(); } }
        internal static void Clear() { Facts.Clear(); Save(); }
    }
}
