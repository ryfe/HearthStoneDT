namespace HearthStoneDT.UI.GameEvents
{
    public interface IGameEventSink
    {
        void OnCardRemovedFromDeck(string cardId);
        void OnCardAddedToDeck(string cardId);
    }
}
