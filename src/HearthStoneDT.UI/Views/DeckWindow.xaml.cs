
using System.IO;
using System.Windows;
using System.Windows.Input;
using HearthStoneDT.UI.Decks;
using HearthStoneDT.UI.Overlay;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Data;
using HearthStoneDT.UI.Logs;




namespace HearthStoneDT.UI.Views
{
    public partial class DeckWindow : ToggleableOverlayBase
    {
        private readonly ObservableCollection<DeckCard> _cards = new();
        private ICollectionView? _cardsView;
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
                foreach (var kv in dbfCounts)
                {
                    int dbfId = kv.Key;
                    int count = kv.Value;

                    if (cardDb.TryGet(dbfId, out var info))
                        _cards.Add(new DeckCard
                        {
                            DbfId = dbfId,
                            CardId = info.CardId,
                            Name = info.NameKo,
                            Cost = info.Cost,
                            Count = count,
                            Rarity = info.Rarity
                        });
                    else
                        _cards.Add(new DeckCard
                        {
                            DbfId = dbfId,
                            CardId = "",
                            Name = $"알 수 없는 카드(dbfId={dbfId})",
                            Cost = 0,
                            Count = count
                        });
                }

                var sorted = _cards.OrderBy(x => x.Cost).ThenBy(x => x.Name).ToList();
                _cards.Clear();
                foreach (var c in sorted)
                    _cards.Add(c);

                DeckCountText.Text = $"{_cards.Sum(x => x.Count)}장";

                int totalCards = sorted.Sum(x => x.Count);
                DeckCountText.Text = $"{totalCards}장";
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
        
        public void RemoveFromDeck(string cardId)
        {
            DebugLog.Write($"[REMOVE_CALL] cardId={cardId}");
            var item = _cards.FirstOrDefault(x => x.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                DebugLog.Write($"[REMOVE_FAIL] not found cardId={cardId}");
                return;
            }

            item.Count--;
            if (item.Count <= 0) _cards.Remove(item);

            DebugLog.Write($"[REMOVE_OK] cardId={cardId} newCount={(item.Count)} remainingTotal={_cards.Sum(x => x.Count)}");

            // DeckCard가 INotifyPropertyChanged가 아니라서 강제 갱신
            _cardsView?.Refresh();

            DeckCountText.Text = $"{_cards.Sum(x => x.Count)}장";
        }

        public void AddToDeck(string cardId, CardDb cardDb)
        {
            var item = _cards.FirstOrDefault(x => x.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.Count++;
                _cardsView?.Refresh();
                DeckCountText.Text = $"{_cards.Sum(x => x.Count)}장";
                return;
            }

            if (!cardDb.TryGetByCardId(cardId, out var info))
                return; // DB에 없으면 추가 못함(또는 Unknown으로 추가)

            _cards.Add(new DeckCard
            {
                CardId = info.CardId,
                Name = info.NameKo,
                Cost = info.Cost,
                Count = 1,
                Rarity = info.Rarity
            });

            _cardsView?.Refresh();

            DeckCountText.Text = $"{_cards.Sum(x => x.Count)}장";
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

