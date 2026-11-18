using System;
using System.Collections.Generic;

namespace Bugi
{
    /// <summary>시그니처 기술 정의: 이름, 설명, 재생 시간, 가이드북 응용동작 또는 전용 시연 키.</summary>
    internal sealed class SigDef
    {
        public string Name, Desc;
        public double Dur;
        public Skit Skit;
        public string DemoKind;
        public SigDef(string name, double dur, string desc, Skit skit)
        {
            Name = name;
            Dur = dur;
            Desc = desc;
            Skit = skit;
        }
        public SigDef(string name, double dur, string desc, string demoKind)
        {
            Name = name;
            Dur = dur;
            Desc = desc;
            DemoKind = demoKind;
        }
        public string Label { get { return "★ " + Name; } }
    }

    /// <summary>캐릭터별 대표 시그니처 행동 정의.</summary>
    internal static class Signatures
    {
        private static SigDef[][] all; private static string allLang;
        internal static SigDef[][] All { get { if (all == null || allLang != Loc.Code) { allLang = Loc.Code; all = BuildAll(); } return all; } }
        private static SigDef[][] BuildAll() { return new SigDef[][] {
            // 0: 부기
            new[]
            {
                new SigDef(Loc.T("두두 등장"), 4.4, Loc.T("망토와 입가리개를 두르고 양손을 허리에 올리며 늠름하게 '두두 등장'"), Skit.Hero),
                new SigDef("Busan is good", 5.0, Loc.T("목걸이 명찰을 걸고 'Busan is good' 표지판 옆을 신나게 행진"), Skit.Busan),
                new SigDef(Loc.T("하트 안기"), 4.6, Loc.T("커다란 하트를 포근하게 안고 다리를 차올리며 주변에 하트를 띄움"), Skit.HeartHug),
            },
            // 1: 부캉이
            new[]
            {
                new SigDef(Loc.T("부산 최고!"), 5.2, Loc.T("홍보대사 어깨띠를 두르고 두 엄지를 번쩍 들어 '부산 최고!' 응원"), "idcard"),
                new SigDef(Loc.T("파도 깃발 응원"), 5.6, Loc.T("부산 파도 깃발을 높이 들고 폴짝폴짝 신나게 흔들며 응원"), "flag"),
                new SigDef(Loc.T("액션캠 셀카"), 4.6, Loc.T("액션캠을 높이 들고 플래시를 터뜨리며 찰칵! 셀카 한 컷"), "selfie"),
            },
        }; }

        internal static SigDef Get(int charId, int idx)
        {
            int c = Math.Max(0, Math.Min(All.Length - 1, charId));
            var arr = All[c];
            return arr[Math.Max(0, Math.Min(arr.Length - 1, idx))];
        }
    }
}
