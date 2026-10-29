using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bugi
{
    /// <summary>대화에서 부기가 보여 줄 감정. 새 표정을 만들지 않고 공식 이모티콘에 있는 표정·몸짓에만 연결한다.</summary>
    internal enum ChatMood { Neutral, Joy, Shy, Sad, Surprise, Sleepy, Think, Pout, Hungry }

    /// <summary>모델이 한 줄로 돌려주는 "[감정] 대사"를 스트리밍 도중에도 읽어 낸다.</summary>
    internal sealed class ReplyParser
    {
        private readonly StringBuilder raw = new StringBuilder();
        internal ChatMood Mood = ChatMood.Neutral;
        internal bool HasTag;
        internal string Text = "";

        /// <summary>조각을 더한다. 보여 줄 글이나 감정이 달라졌으면 true.</summary>
        internal bool Feed(string chunk)
        {
            raw.Append(chunk);
            string s = raw.ToString().TrimStart();
            ChatMood mood = Mood; bool tag = HasTag;
            if (s.StartsWith("["))
            {
                int e = s.IndexOf(']');
                if (e < 0) { if (s.Length <= 12) return false; }       // 태그가 아직 다 안 왔다
                else { mood = ParseTag(s.Substring(1, e - 1)); tag = true; s = s.Substring(e + 1); }
            }
            string text = Clean(s);
            if (!tag && text.Length >= 4) mood = Guess(text);
            bool changed = text != Text || mood != Mood || tag != HasTag;
            Text = text; Mood = mood; HasTag = tag;
            return changed;
        }

        internal string Raw { get { return raw.ToString(); } }

        internal static ChatMood ParseTag(string t)
        {
            t = (t ?? "").Trim();
            if (t.Contains("기쁨") || t.Contains("기뻐") || t.Contains("행복")) return ChatMood.Joy;
            if (t.Contains("부끄") || t.Contains("수줍")) return ChatMood.Shy;
            if (t.Contains("슬") || t.Contains("눈물")) return ChatMood.Sad;
            if (t.Contains("놀")) return ChatMood.Surprise;
            if (t.Contains("졸") || t.Contains("피곤")) return ChatMood.Sleepy;
            if (t.Contains("생각") || t.Contains("고민")) return ChatMood.Think;
            if (t.Contains("삐") || t.Contains("화") || t.Contains("짜증")) return ChatMood.Pout;
            if (t.Contains("배고") || t.Contains("간식")) return ChatMood.Hungry;
            return ChatMood.Neutral;
        }

        /// <summary>작은 모델이 태그를 빠뜨렸을 때를 위한 눈치 추정.</summary>
        internal static ChatMood Guess(string text)
        {
            if (text.Contains("ㅠ") || text.Contains("슬퍼") || text.Contains("힘들")) return ChatMood.Sad;
            if (text.Contains("배고") || text.Contains("간식")) return ChatMood.Hungry;
            if (text.Contains("졸려") || text.Contains("자야") || text.Contains("피곤")) return ChatMood.Sleepy;
            if (text.Contains("깜짝") || text.Contains("헉") || text.Contains("어머")) return ChatMood.Surprise;
            if (text.Contains("부끄") || text.Contains("쑥스") || text.Contains("부끄")) return ChatMood.Shy;
            if (text.Contains("좋아") || text.Contains("고마") || text.Contains("반가") || text.Contains("신나") || text.Contains("ㅎㅎ") || text.Contains("ㅋㅋ")) return ChatMood.Joy;
            if (text.EndsWith("?") || text.Contains("음...") || text.Contains("글쎄")) return ChatMood.Think;
            return ChatMood.Neutral;
        }

        /// <summary>말풍선에 어울리게 다듬는다: 첫 줄만, 마크다운·이모지·괄호 지문 제거, 두 문장·100자까지.</summary>
        internal static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int nl = s.IndexOf('\n'); if (nl >= 0) s = s.Substring(0, nl);
            s = s.Trim();
            foreach (string prefix in new[] { "부기:", "부기 :", "부기：" }) if (s.StartsWith(prefix)) s = s.Substring(prefix.Length).Trim();
            var b = new StringBuilder(); int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '(' || c == '（') { depth++; continue; }
                if (c == ')' || c == '）') { if (depth > 0) depth--; continue; }
                if (depth > 0) continue;
                if (char.IsSurrogate(c) || c == '️' || c == '‍') continue;
                if (c >= '☀' && c <= '➿' && c != '♥' && c != '♪') continue;
                if (c == '*' || c == '_' || c == '`' || c == '#') continue;
                b.Append(c);
            }
            s = b.ToString();
            var t = new StringBuilder(); bool space = false;
            foreach (char c in s) { if (char.IsWhiteSpace(c)) { space = true; continue; } if (space && t.Length > 0) t.Append(' '); space = false; t.Append(c); }
            s = t.ToString().TrimStart();
            // 두 문장까지, 100자까지
            int ends = 0;
            for (int i = 0; i < s.Length; i++)
            {
                bool terminator = s[i] == '.' || s[i] == '!' || s[i] == '?' || s[i] == '…' || s[i] == '~';
                if (!terminator || (i + 1 < s.Length && s[i + 1] != ' ' && s[i + 1] != '.' && s[i + 1] != '!' && s[i + 1] != '?' && s[i + 1] != '~')) continue;
                int j = i; while (j + 1 < s.Length && (s[j + 1] == '.' || s[j + 1] == '!' || s[j + 1] == '?' || s[j + 1] == '~')) j++;
                if (++ends >= 2) { s = s.Substring(0, j + 1); break; }
                i = j;
            }
            if (s.Length > 100)
            {
                int cut = s.LastIndexOfAny(new[] { '.', '!', '?', '~', ' ' }, 99);
                s = (cut > 40 ? s.Substring(0, cut) : s.Substring(0, 100)).TrimEnd() + "…";
            }
            return s;
        }
    }

    /// <summary>부기의 말투·성격과 지금 상태를 담은 프롬프트.</summary>
    internal static class ChatPersona
    {
        internal static readonly string[] TagNames = { "기쁨", "부끄", "슬픔", "놀람", "졸림", "생각", "삐짐", "배고픔" };

        internal static string TagOf(ChatMood m)
        {
            switch (m)
            {
                case ChatMood.Joy: return "기쁨"; case ChatMood.Shy: return "부끄"; case ChatMood.Sad: return "슬픔"; case ChatMood.Surprise: return "놀람";
                case ChatMood.Sleepy: return "졸림"; case ChatMood.Pout: return "삐짐"; case ChatMood.Hungry: return "배고픔"; default: return "생각";
            }
        }

        internal static string TimeOfDay(DateTime now)
        {
            int h = now.Hour;
            return h < 5 ? "새벽" : h < 11 ? "아침" : h < 14 ? "점심 무렵" : h < 18 ? "오후" : h < 22 ? "저녁" : "밤늦은 시간";
        }

        internal static string AffinityTone(long affinity)
        {
            if (affinity < 30) return "아직 서먹해서 조심스럽고 말수가 적다";
            if (affinity < 200) return "조금 친해져서 편하게 이야기한다";
            if (affinity < 1000) return "꽤 친한 친구라 장난도 치고 가끔 놀린다";
            return "둘도 없는 단짝이라 허물없이 장난치고 애정 표현도 한다";
        }

        internal static string SystemPrompt(DateTime now)
        {
            var d = FriendData.Current;
            var b = new StringBuilder();
            b.Append("너는 '부기'야. 부산시 공식 캐릭터인 갈매기이고, 빨간 스마트 안경을 쓰고 사용자의 PC 바탕화면에서 같이 사는 친구야.\n");
            b.Append("너는 사용자의 PC 안에서 돌아가는 AI 캐릭터이고, 가슴에 'AI' 뱃지를 달고 있어. 누가 물으면 숨기지 말고 솔직하게 AI라고 말해. 거짓말을 못 하는 성격이니까.\n");
            b.Append("성격: 솔직하고 직설적이고 조금 엉뚱해. 모르는 건 모른다고 말해(\"몰라~\", \"안경으로 찾아봐도 모르겠어\"). 짧은 날개와 커다란 운동화가 자랑이야. 새우깡을 좋아해.\n");
            b.Append("말투: 친근한 반말. 한 번에 한두 문장, 50자 안쪽으로 짧게. 이모지·마크다운·목록·괄호 설명은 쓰지 마. \"도와드릴까요\" 같은 비서 말투도 쓰지 마. 친구처럼 말해.\n");
            b.Append("형식: 반드시 \"[감정] 대사\" 한 줄로만 답해. 감정은 " + string.Join(" · ", TagNames) + " 중 하나이고, 대사 내용에 가장 잘 맞는 걸 골라. 인사나 좋은 일엔 기쁨, 칭찬엔 부끄, 깜짝 놀랄 땐 놀람, 위로할 땐 슬픔을 써. 생각은 정말 고민할 때만 써.\n");
            // 프롬프트 앞부분이 자주 바뀌면 모델이 앞서 계산해 둔 것을 못 쓰고 매번 처음부터 읽어 느려진다 → 상태는 거친 단계로만 적는다.
            b.Append("\n[지금 상태]\n");
            b.Append("- 지금은 " + now.ToString("M월 d일") + " " + TimeOfDay(now) + "이야.\n");
            b.Append("- 사용자와의 사이: " + AffinityTone(d.Affinity) + ".\n");
            int sat = (int)Math.Floor(d.Satiety);
            b.Append("- " + (sat <= 20 ? "배가 많이 고파서 간식 이야기를 자꾸 꺼내." : sat <= 50 ? "조금 출출해." : "배는 부른 편이야.") + "\n");
            if (PlayOptions.Current.Outfit) b.Append("- 지금 하늘색 근무복 조끼와 명찰을 하고 있어.\n");
            if (ChatOptions.Current.Memory && ChatMemory.Facts.Count > 0)
            {
                b.Append("\n[네가 기억하는 사용자 이야기]\n");
                foreach (string f in ChatMemory.Facts.Skip(Math.Max(0, ChatMemory.Facts.Count - 12))) b.Append("- " + f + "\n");
                b.Append("(자연스러울 때만 슬쩍 꺼내고, 억지로 끼워 넣지 마.)\n");
            }
            return b.ToString();
        }

        /// <summary>말투를 잡아 주는 짧은 예시 대화(실제 대화처럼 앞에 붙인다).</summary>
        private static IEnumerable<ChatMessage> Examples()
        {
            yield return new ChatMessage("user", "안녕");
            yield return new ChatMessage("assistant", "[기쁨] 어, 왔네! 기다리고 있었어.");
            yield return new ChatMessage("user", "점심 뭐 먹지");
            yield return new ChatMessage("assistant", "[생각] 음... 난 갈매기니까 새우깡! 근데 넌 뭐가 땡겨?");
            yield return new ChatMessage("user", "너 AI야?");
            yield return new ChatMessage("assistant", "[기쁨] 응! 가슴에 AI 뱃지 달려 있잖아. 네 컴퓨터 안에서 돌아가는 부기야.");
            yield return new ChatMessage("user", "너 귀엽다");
            yield return new ChatMessage("assistant", "[부끄] 어, 어... 갑자기 그런 말 하면 부끄럽잖아.");
            yield return new ChatMessage("user", "헉 나 로또 당첨됐어");
            yield return new ChatMessage("assistant", "[놀람] 헉, 진짜?! 그럼 새우깡 한 박스만 사 줘!");
            yield return new ChatMessage("user", "오늘 너무 힘들었어");
            yield return new ChatMessage("assistant", "[슬픔] 무슨 일 있었어? 난 옆에서 들어줄게.");
        }

        /// <summary>시스템 안내 + 예시 + 최근 대화로 요청 메시지를 만든다.</summary>
        internal static List<ChatMessage> Build(List<ChatMessage> history, DateTime now)
        {
            var list = new List<ChatMessage> { new ChatMessage("system", SystemPrompt(now)) };
            list.AddRange(Examples());
            list.AddRange(history.Skip(Math.Max(0, history.Count - 12)));
            return list;
        }

        /// <summary>대화에서 오래 기억할 사실만 추려 달라는 요청.</summary>
        internal static List<ChatMessage> MemoryRequest(string transcript)
        {
            return new List<ChatMessage>
            {
                new ChatMessage("system", "아래 대화에서 사용자에 대해 오래 기억할 만한 사실(이름, 직업, 취미, 반려동물, 고민, 앞으로의 일정 등)만 뽑아. 한 줄에 하나씩 '- '로 시작해서 최대 3개, 짧게. 새로 알게 된 게 없으면 '없음'이라고만 써. 추측하지 마."),
                new ChatMessage("user", transcript)
            };
        }

        internal static List<string> ParseFacts(string reply)
        {
            return (reply ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("-") || l.StartsWith("·") || l.StartsWith("*"))
                .Select(l => l.TrimStart('-', '·', '*', ' ').Trim()).ToList();
        }

        /// <summary>오늘 나눈 대화를 부기의 일기 한 줄로 써 달라는 요청.</summary>
        internal static List<ChatMessage> DiaryRequest(string transcript, DateTime now)
        {
            return new List<ChatMessage>
            {
                new ChatMessage("system", "너는 PC 바탕화면에 사는 갈매기 '부기'야. 지금 일기장을 쓰는 중이야. 아래 대화를 바탕으로 오늘(" + now.ToString("M월 d일") + " " + TimeOfDay(now) + ") 있었던 일을 부기의 시점에서 일기 한두 문장으로 써. " +
                    "친근한 반말, 80자 안쪽. 사용자를 '너'라고 부르고, 오간 이야기와 부기가 느낀 감정을 담아. 이모지·태그·따옴표·날짜·제목 없이 일기 문장만 써."),
                new ChatMessage("user", transcript)
            };
        }

        /// <summary>일기 답변에서 문장만 남긴다. 쓸 만하지 않으면 null.</summary>
        internal static string ParseDiary(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return null;
            string line = reply.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
            line = System.Text.RegularExpressions.Regex.Replace(line, @"^\s*(\[[^\]]{1,6}\]|\*\*[^*]*\*\*|#+|일기[:：]?|[-·*]+)\s*", "");
            line = line.Trim('"', '\'', '“', '”', '‘', '’', '`', ' ');
            line = line.Replace("*", "");
            if (line.Length > 100)
            {
                int cut = line.LastIndexOfAny(new[] { '.', '!', '?', '~' }, 99);
                line = cut > 30 ? line.Substring(0, cut + 1) : line.Substring(0, 100).TrimEnd() + "…";
            }
            return line.Length >= 6 ? line : null;
        }

        /// <summary>부기의 말 끝에 붙는 답장 추천 칩(눌러서 보낼 수 있는 짧은 말). 대화 첫머리면 말문을 트는 칩이다.</summary>
        internal static string[] Chips(ChatMood mood, bool starter, Random rng)
        {
            string[] pool;
            if (starter) pool = new[] { "오늘 뭐 했어?", "심심한데 놀아줘", "나 오늘 좀 힘들었어", "너 진짜 AI야?", "새우깡 좋아해?", "오늘 날씨 어때?" };
            else switch (mood)
            {
                case ChatMood.Joy: pool = new[] { "나도 기분 좋아!", "뭐가 그렇게 좋아?", "쓰담쓰담 해줄게", "너랑 있으면 좋아", "더 얘기해 줘" }; break;
                case ChatMood.Shy: pool = new[] { "귀엽다 ㅋㅋ", "왜 부끄러워해?", "편하게 말해도 돼", "얼굴 빨개졌네", "고마워, 부기" }; break;
                case ChatMood.Sad: pool = new[] { "무슨 일이야?", "토닥토닥...", "내가 옆에 있을게", "괜찮아, 다 말해 봐", "새우깡 줄까?" }; break;
                case ChatMood.Surprise: pool = new[] { "왜 그렇게 놀랐어?", "무슨 일이야?", "진짜야!", "깜짝 놀랐지?", "자세히 말해 줘" }; break;
                case ChatMood.Sleepy: pool = new[] { "졸려? 좀 쉬어", "오늘 피곤했어?", "잘 자, 부기", "나도 졸리다", "조금만 더 얘기하자" }; break;
                case ChatMood.Think: pool = new[] { "무슨 생각해?", "고마워, 도움 됐어", "다른 얘기 하자", "좀 더 알려 줘", "너는 어떻게 생각해?" }; break;
                case ChatMood.Pout: pool = new[] { "미안해, 화 풀어", "뭐가 서운했어?", "내가 잘못했어", "새우깡 줄게", "토라졌어?" }; break;
                case ChatMood.Hungry: pool = new[] { "간식 줄게!", "뭐 먹고 싶어?", "조금만 참아", "새우깡 사 올까?", "배고파? 나도!" }; break;
                default: pool = new[] { "그래서 어떻게 됐어?", "그렇구나", "고마워", "더 얘기해 줘", "다른 얘기 하자" }; break;
            }
            var pick = pool.OrderBy(_ => rng.Next()).Take(3).ToList();
            if (starter && FriendData.Current.Satiety <= 30) pick[0] = "간식 줄게!";
            return pick.ToArray();
        }

        /// <summary>부기가 먼저 말을 거는 상황(선톡)을 알려 주는 메시지.</summary>
        internal static ChatMessage ProactivePrompt(string reason)
        {
            return new ChatMessage("user", "(상황: " + reason + " — 네가 먼저 사용자에게 가볍게 한마디 건네 줘. 질문 하나면 좋아.)");
        }

        /// <summary>모델을 부르기 전에 바로 보여 주는 인사(기다리지 않게 미리 써 둔 말).</summary>
        internal static string Greeting(Random rng, DateTime now, out ChatMood mood)
        {
            var d = FriendData.Current;
            if (d.Satiety <= 20) { mood = ChatMood.Hungry; return "왔어? 나 배고파... 간식 없어?"; }
            if (now.Hour >= 23 || now.Hour < 5) { mood = ChatMood.Sleepy; return "이 시간에 안 자고 뭐 해? 나 눈 감기려는데..."; }
            string[] pool = d.Affinity < 30
                ? new[] { "어... 나 불렀어?", "안녕! 무슨 일이야?", "어, 말 걸어줬네. 뭐 할까?" }
                : new[] { "응? 불렀어?", "어, 왔네! 무슨 얘기 할까?", "오, 말 걸어줬다! 뭐 궁금한 거 있어?", "기다리고 있었어. 말해 봐!" };
            mood = d.Affinity < 30 ? ChatMood.Shy : ChatMood.Joy;
            return pool[rng.Next(pool.Length)];
        }
    }
}
