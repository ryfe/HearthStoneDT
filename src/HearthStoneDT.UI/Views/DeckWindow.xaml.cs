
using System.IO;
using System.Windows;
using System.Windows.Input;
using HearthStoneDT.UI.Decks;
using HearthStoneDT.UI.Overlay;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Data;
using HearthStoneDT.UI.Logs;
using System.Collections.Generic;
using System.Linq;




namespace HearthStoneDT.UI.Views
{
    public partial class DeckWindow : ToggleableOverlayBase
    {
        private sealed class DeckInstance
        {
            public string CardId = ""; // may be empty until revealed
            public int EntityId = 0;   // 0 = unassigned
            public bool InDeck = true;
        }

        private readonly ObservableCollection<DeckCard> _cards = new();
        private ICollectionView? _cardsView;
        // Instance-level tracking to prevent double-remove and to backfill unknown entity ids.
        private readonly List<DeckInstance> _instances = new();
        private readonly Dictionary<int, DeckInstance> _instanceByEntityId = new();
        private const string PlacementKey = "deck_window";

        public DeckWindow()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                // 1️⃣ 저장된 위치 복원 시도
                bool restored = WindowPlacementStore.TryLoad(this, PlacementKey);

                // 2️⃣ 저장된 위치가 없으면 → 기본 위치(오른쪽)
                if (!restored)
                {
                    PlaceDefaultRight();
                }

                // 3️⃣ 기본 상태는 클릭스루
                SetInteractive(false);
                 _cardsView = CollectionViewSource.GetDefaultView(_cards);
                _cardsView.SortDescriptions.Clear();
                _cardsView.SortDescriptions.Add(new SortDescription(nameof(DeckCard.Cost), ListSortDirection.Ascending));
                _cardsView.SortDescriptions.Add(new SortDescription(nameof(DeckCard.Name), ListSortDirection.Ascending));

                OverlayCardList.ItemsSource = _cardsView;
            };

            Closing += OnClosing;
        }
        public async Task SetDeckAsync(DeckDefinition deck, CardDb cardDb)
        {
            try
            {
                DeckNameText.Text = deck.Name;

                if (string.IsNullOrWhiteSpace(deck.DeckCode))
                {
                    DeckCountText.Text = "(DeckCode 없음)";
                    OverlayCardList.ItemsSource = null;
                    return;
                }

                await cardDb.EnsureLoadedAsync();

                var dbfCounts = DeckCodeDecoder.DecodeToDbfCounts(deck.DeckCode);

                _cards.Clear();
                _instances.Clear();
                _instanceByEntityId.Clear();
                foreach (var kv in dbfCounts)
                {
                    int dbfId = kv.Key;
                    int count = kv.Value;

                    if (cardDb.TryGet(dbfId, out var info))
                    {
                        _cards.Add(new DeckCard
                        {
                            DbfId = dbfId,
                            CardId = info.CardId,
                            Name = info.NameKo,
                            Cost = info.Cost,
                            Count = count,
                            Rarity = info.Rarity
                        });
                        for (int i = 0; i < count; i++)
                            _instances.Add(new DeckInstance { CardId = info.CardId, EntityId = 0, InDeck = true });
                    }
                    else
                    {
                        _cards.Add(new DeckCard
                        {
                            DbfId = dbfId,
                            CardId = "",
                            Name = $"알 수 없는 카드(dbfId={dbfId})",
                            Cost = 0,
                            Count = count
                        });
                        // Unknown dbfId: keep instances without cardId.
                        for (int i = 0; i < count; i++)
                            _instances.Add(new DeckInstance { CardId = "", EntityId = 0, InDeck = true });
                    }
                }

                var sorted = _cards.OrderBy(x => x.Cost).ThenBy(x => x.Name).ToList();
                _cards.Clear();
                foreach (var c in sorted)
                    _cards.Add(c);

                // Initialize counts from instance list
                RecomputeCounts();
            }
            catch (Exception ex)
            {
                File.WriteAllText(@"C:\HearthStoneDT\deckwindow_setdeck_crash.txt", ex.ToString());
                throw;
            }
        }
        private void DeckWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            WindowPlacementStore.Save(this, PlacementKey);
        }
        
        public void RemoveFromDeck(string cardId, int entityId)
        {
            DebugLog.Write($"[REMOVE_CALL] cardId={cardId} entity={entityId}");

            // 1) Prefer exact entity match (de-dupe by entity)
            if (entityId != 0 && _instanceByEntityId.TryGetValue(entityId, out var exact))
            {
                if (!exact.InDeck)
                {
                    DebugLog.Write($"[REMOVE_SKIP_DUP] entity={entityId} cardId={exact.CardId}");
                    return;
                }
                exact.InDeck = false;
                RecomputeCounts();
                DebugLog.Write($"[REMOVE_OK] exact entity={entityId} cardId={exact.CardId}");
                return;
            }

            // 2) Find a best-effort instance to remove
            DeckInstance? target = null;

            if (!string.IsNullOrWhiteSpace(cardId))
            {
                // Prefer an unassigned instance with matching cardId
                target = _instances.FirstOrDefault(i => i.InDeck && i.EntityId == 0 &&
                    i.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));

                // Fallback: any in-deck instance with matching cardId
                target ??= _instances.FirstOrDefault(i => i.InDeck &&
                    i.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
            }

            // Ultimate fallback: remove any unassigned in-deck instance (unknown card removal)
            target ??= _instances.FirstOrDefault(i => i.InDeck && i.EntityId == 0);

            if (target == null)
            {
                DebugLog.Write($"[REMOVE_FAIL] no instance to remove cardId={cardId} entity={entityId}");
                return;
            }

            if (entityId != 0)
            {
                target.EntityId = entityId;
                _instanceByEntityId[entityId] = target;
            }

            // If the instance had no CardId but we now know it, backfill
            if (string.IsNullOrWhiteSpace(target.CardId) && !string.IsNullOrWhiteSpace(cardId))
                target.CardId = cardId;

            target.InDeck = false;
            RecomputeCounts();
            DebugLog.Write($"[REMOVE_OK] cardId={cardId} entity={entityId}");
        }

        public void AddToDeck(string cardId, int entityId, CardDb cardDb)
        {
            DebugLog.Write($"[ADD_CALL] cardId={cardId} entity={entityId}");

            // 1) If entity exists, just mark as in-deck
            if (entityId != 0 && _instanceByEntityId.TryGetValue(entityId, out var existing))
            {
                existing.InDeck = true;
                if (string.IsNullOrWhiteSpace(existing.CardId) && !string.IsNullOrWhiteSpace(cardId))
                    existing.CardId = cardId;
                RecomputeCounts();
                DebugLog.Write($"[ADD_OK] existing entity={entityId} cardId={existing.CardId}");
                return;
            }

            // 2) Create a new instance
            var inst = new DeckInstance
            {
                CardId = cardId ?? "",
                EntityId = entityId,
                InDeck = true
            };
            _instances.Add(inst);
            if (entityId != 0)
                _instanceByEntityId[entityId] = inst;

            // Ensure the UI list has this cardId
            if (!string.IsNullOrWhiteSpace(cardId))
            {
                var item = _cards.FirstOrDefault(x => x.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    if (cardDb.TryGetByCardId(cardId, out var info))
                    {
                        _cards.Add(new DeckCard
                        {
                            CardId = info.CardId,
                            Name = info.NameKo,
                            Cost = info.Cost,
                            Count = 0,
                            Rarity = info.Rarity
                        });
                    }
                }
            }

            RecomputeCounts();
            DebugLog.Write($"[ADD_OK] cardId={cardId} entity={entityId}");
        }

        private void RecomputeCounts()
        {
            // Recompute per-card counts from instances.
            foreach (var c in _cards)
            {
                if (string.IsNullOrWhiteSpace(c.CardId))
                    continue;
                c.Count = _instances.Count(i => i.InDeck &&
                    i.CardId.Equals(c.CardId, StringComparison.OrdinalIgnoreCase));
            }

            _cardsView?.Refresh();

            var total = _instances.Count(i => i.InDeck);
            DeckCountText.Text = $"{total}장";
        }


        private void PlaceDefaultRight()
        {
            var area = SystemParameters.WorkArea;
            const double margin = 16;

            Left = area.Right - Width - margin;
            Top = area.Top + margin;
        }
    }
}

