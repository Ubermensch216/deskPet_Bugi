using System;
using System.Collections.Generic;

namespace Bugi
{
    /// <summary>시그니처 기술 정의: 이름, 설명, 재생 시간, 가이드북 응용동작.</summary>
    internal sealed class SigDef
    {
        public string Name, Desc;
        public double Dur;
        public Skit Skit;
        public SigDef(string name, double dur, string desc, Skit skit)
        {
            Name = name;
            Dur = dur;
            Desc = desc;
            Skit = skit;
        }
        public string Label { get { return "★ " + Name; } }
    }

    /// <summary>부기의 대표 시그니처 행동 정의 (가이드북 p27~36 응용동작 기반).</summary>
    internal static class Signatures
    {
        internal static readonly SigDef[][] All =
        {
            // 0: 부기
            new[]
            {
                new SigDef("두두 등장", 4.4, "망토와 입가리개를 두르고 양손을 허리에 올리며 늠름하게 '두두 등장'", Skit.Hero),
                new SigDef("Busan is good", 5.0, "목걸이 명찰을 걸고 'Busan is good' 표지판 옆을 신나게 행진", Skit.Busan),
                new SigDef("하트 안기", 4.6, "커다란 하트를 포근하게 안고 다리를 차올리며 주변에 하트를 띄움", Skit.HeartHug),
            },
        };

        internal static SigDef Get(int charId, int idx)
        {
            int c = Math.Max(0, Math.Min(All.Length - 1, charId));
            var arr = All[c];
            return arr[Math.Max(0, Math.Min(arr.Length - 1, idx))];
        }
    }
}
