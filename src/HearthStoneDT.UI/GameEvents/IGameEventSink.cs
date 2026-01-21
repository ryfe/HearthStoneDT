namespace HearthStoneDT.UI.GameEvents
{
    public interface IGameEventSink
    {
    void OnCardRemovedFromDeck(string cardId, int entityId);
    void OnCardAddedToDeck(string cardId, int entityId);
    }
}
