
using System.IO;
using System.Windows;
using System.Windows.Input;
using HearthStoneDT.UI.Decks;
using HearthStoneDT.UI.Overlay;
using System.ComponentModel;


namespace HearthStoneDT.UI.Views
{
    public partial class DeckWindow : ToggleableOverlayBase
    {
        private readonly CardDb _cardDb = new();
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

                var list = new List<DeckCard>();
                foreach (var kv in dbfCounts)
                {
                    int dbfId = kv.Key;
                    int count = kv.Value;

                    if (cardDb.TryGet(dbfId, out var info))
                        list.Add(new DeckCard { Name = info.NameKo, Cost = info.Cost, Count = count, Rarity = info.Rarity });
                    else
                        list.Add(new DeckCard { Name = $"알 수 없는 카드(dbfId={dbfId})", Cost = 0, Count = count });
                }

                var sorted = list.OrderBy(x => x.Cost).ThenBy(x => x.Name).ToList();
                OverlayCardList.ItemsSource = sorted;

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

        private void PlaceDefaultRight()
        {
            var area = SystemParameters.WorkArea;
            const double margin = 16;

            Left = area.Right - Width - margin;
            Top = area.Top + margin;
        }
    }
}

