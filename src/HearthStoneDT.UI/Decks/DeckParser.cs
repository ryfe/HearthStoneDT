using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace HearthStoneDT.UI.Decks
{
    public static class DeckParser
    {
        public static DeckDefinition Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new ArgumentException("덱 텍스트가 비어있습니다.");

            var lines = raw.Replace("\r\n", "\n").Split('\n').Select(x => x.Trim()).ToArray();

            var deck = new DeckDefinition();

            foreach (var line in lines)
            {
                if (line.StartsWith("### "))
                    deck.Name = line.Substring(4).Trim();

                if (line.StartsWith("# 직업:"))
                    deck.Class = line.Substring("# 직업:".Length).Trim();

                if (line.StartsWith("# 대전 방식:"))
                    deck.Mode = line.Substring("# 대전 방식:".Length).Trim();

                // 덱코드: '#' 아닌 줄 중 길고 Base64 느낌인 것
                if (!line.StartsWith("#") && line.Length > 20 && (line[0] == 'A' || line[0] == 'B'))
                    deck.DeckCode = line.Trim();
            }

            if (string.IsNullOrWhiteSpace(deck.Name))
                deck.Name = "이름 없는 덱";

            if (string.IsNullOrWhiteSpace(deck.DeckCode))
                throw new InvalidOperationException("덱 코드(AAECA...로 시작하는 문자열)를 찾지 못했습니다.");

            return deck;
        }
    }
}
