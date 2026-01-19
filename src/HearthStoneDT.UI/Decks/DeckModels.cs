using System;
using System.Collections.Generic;

namespace HearthStoneDT.UI.Decks
{
    public sealed class DeckCard
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public int Cost { get; set; }
    }

    public sealed class DeckDefinition
    {
        public string Name { get; set; } = "";
        public string Class { get; set; } = "";
        public string Mode { get; set; } = "";
        public List<DeckCard> Cards { get; set; } = new();
        public string? DeckCode { get; set; }
        public DateTime SavedAt { get; set; } = DateTime.Now;
    }
}
