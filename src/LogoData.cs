namespace Bugi
{
    /// <summary>첨부 이미지(ref/bugi-logo.png, 부산 상징 마크 55x54 PNG)를 EXE 에 그대로 내장한 데이터.</summary>
    internal static class LogoData
    {
        internal const string Png =
            "iVBORw0KGgoAAAANSUhEUgAAADcAAAA2CAYAAABjhwHjAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAguSURB" +
            "VGhD7ZlpWFTXGYDfgWHYUQRBcEFRBC2ILGImJmiMG1FBDYgRjcQ1xl1jq0+0iTbWWI2xGmttE1tNKW64VHGLCqKICwqIK+oIIiBx2AVZBqY/cAhzhXBBUPHx" +
            "/TXP933nPvedc+85554j6TD8P2peU3SEgdeJN3LNlTdyzRWJmNFyQVAPWpjIkEl1kUiE2edDrYZSVTnqanchkVTGN++9Qfqjwurl9UKUXMr/goShF4IiLZ+B" +
            "M8NRlVcIU6IQJZcYGoCZsYyF68+hzC1ucO+pyivIyS8lO78YczN9XB0s8Hm7A++4tqmqqVCrORCVQn5RGeN9HLiVksuw+YcpLau/oCi5m7sCMdSXYucbIkw1" +
            "iH7utvj17Yivd0ekupX/1MnYNA6fTSUqLoOHWUUAfPGJO1NHdiPpfh5DZodTXlHnrWohSu76zkCMDaTIJ+1r0DvQ2twQH3l7RvbrhLuTZVU8LknJ7hMKTsam" +
            "13rdZVM9CR7myDVFDkPnHdJ6N+uiyeRsLI14z8MWv76deMvZqioel6Rkb0QyJy4+4MEv4q61dJIHk/2cuHo3G98FR0T3YKPKWbY04P1ebfHz7kifau/R1bvZ" +
            "7DuVzJGYVFIzH2u1EcuK6V6M83Eg4XYWvguOCNM1Ui+5tyfvI03wb1u0MKC/py3D3rWjn7vtr20UOeyPSubY+Qco0vK12jQUjWDsjUf4LzpW5yMqSk44oJib" +
            "6tPXw4ZR/ezp625TVZd0P4+9kfc4FH2f5IyCaldoPFbO6M3YwV2IS1Iy4vOjwrQWouQSQwMwNtRj8cbz9HFtg593x6qcIi2fvZHJhEencPdB4/RQXXw7V45/" +
            "f3tiEjMZ88VxYboKUXLntoykjYVR1fwWe+MRp+MzOHw2lVspucLyF8KCoB7MDnThVkou4788SWb2E2GJuLWlWq2uErt8U8m0lVGsC018aWIA9x9WDkxdO7RE" +
            "T1qzRs1RAfoyXVTlam6n5uHuZMmlbR/yzYzetLMyFpa+ED4d1Z01c+SkK4twG7e71ilFlJyRgRSJBIbNO8yEryJQpOXz0eAuRP8wglUze2NraSRs0mRMGdGN" +
            "xcFuZOeXMHTuIXIKSoQlVYiSq1CDro4EY0MpkZfTeW/6AWaviSb9USFjBnUhZstIVkz3wrqVobBpoxI8zJElE93Jzi9hyOxwsvNrF0OsnAZ9mbTq9/6oZOST" +
            "9jFnbTTpyiLG+Thw4d+j+GqKJzZN0JMThzuxbKoneY9LGTQrvMYBREi95GpiX2Qy8ol7mb8uhszsJ3wy3JFzW0ayZKI7VuaN05NjB3fhyykeFBarGDw7nEc5" +
            "dYvRGHIawk4q8Arewx++P48yt5gpI7pxcesoFge7YW6qLywXTdAQB1bO6E1hsYqBMw+Soaz8YhBDo8lp2H7sDh4fh7Fk0wWy8or5dFR34kP8+f3HPestOXpA" +
            "Z/78mRelZRUMnhX+zNKvLhpdTsNPh2/jPj6MP26OpaCojBn+vyM+xJ/5Y3tgZiwTlj+Df397Vs9+i5LScgbNOtigBXeTyWnYGn4L5zE7WfbPSxQWq5gzxoXE" +
            "0ABmjXauVfLD/vZ8O1eOqryCQbPCuZfesHVqk8tp2HLgJs6BO/nTj5coKS3n83GuJIYGMDPAGWODX0dh33ftWDtXjloNg2aFP9cC/IXJ8XR/5If9N+keuINv" +
            "tsZRpqpg4XhXru8MxL+/PR/06cCGhe+QW1DKwJkHn3shLmrhLPZjtb7oSXWY7OfEogluWnGfuYe4rsjRijWEF9pzQspUFWwKu45TwA7WhCRQWKwC4B+LvZns" +
            "51S1edRQXqqchiclKjbsuIpX8B7WhCTQ3tqEpZM8SAwdzYShjug0cC/xlZDT8LiojA07ruIatIvvQq9gZCBl+TRProQGMGGoo7C8Tl4pOQ25BaWsC02kZ9Bu" +
            "vt91FVMjPZZP8yQhJIDxPg7C8lp5JeU05BSUsPqnBNzHh/G33ddoaSrj6+lexPw4gnEiJF9pOQ1ZecWs2haPnW8IC9efw6qVISume3F84zAmDHWs9SukWcgB" +
            "WJkb4uHUGn09HVIzK6cjh/YtWD7NE/u2ZsJyeNnz3G9h0cIAVwcL5C7WvN+rLZ3b1Sww4y9nOHgmRRiGV0mupamMng6WeDlbMaBXWxztWmrl45Oy2P7zHQCW" +
            "TnTH2FCPz1adJjz6vlZddV6anJmxDJcurZA7W9PPwxaXLq208tcVOUTFZ3D2ykNibygpfFIGwKnNvnS0MWXed2fZE3FPq42QFyZnZCClh4MFcmdrBvVuR3d7" +
            "c6180v08ImLTOB3/kPgkJQVFlTIaOtmaEvl3XwBmrj7DgdM1P4rVaTI5A5kuzp1bIXexZoBXO3p2tdDKK9LyiYrLICI2nfjbSnILSrXy1bFrY0LkZl90JBLm" +
            "r4sh7KRCWFIjjSYn1dWhh0MrvN1s8HazwcOptVY+NfMxR2JSOXslk4TbWWTlFWvla8O+rRlH/voB+jLdeokhVk54EAKgI5FU9kwPa/q62WgdWfF0R/jnCw84" +
            "8/Qxq2sbribaW5sQsWk4elIdFq4/x87jd4Ulv4kouWvbR2NkKGXy16fo0MYEuYs1Pbtaau1Txt1ScuJiGtFXHpKQlCX6gLA22lubcHTDUIwNpCzeeJ7/Hq0c" +
            "KeuDKLk7ez6qdT8eoLSsgmuKbH7JfoJER4KZsYwWxnroy3SFpaLRTMyLNp4ntAFiiJU7uNYHawtDpLo6qMornjn0MzKQYmqkpx18TopLy1nxr8tsC08SpkQj" +
            "Sq65Uvuz9hrwRq658kauufJay/0fkz1PP/A2vMEAAAAASUVORK5CYII=";
    }
}
