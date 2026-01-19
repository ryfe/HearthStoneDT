using System.Collections.Generic;
using HearthDb.Deckstrings;

namespace HearthStoneDT.UI.Decks
{
    public static class DeckCodeDecoder
    {
        public static Dictionary<int, int> DecodeToDbfCounts(string deckCode)
        {
            var deck = DeckSerializer.Deserialize(deckCode);
            return deck.CardDbfIds; // { dbfId => count }
        }
    }
}
