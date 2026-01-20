using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HearthStoneDT.UI.Decks;

namespace HearthStoneDT.UI.Views
{
    public partial class MainWindow : Window
    {
        private readonly DeckStore _store = new DeckStore();
        private readonly CardDb _cardDb = new CardDb();
        private List<DeckDefinition> _decks = new();

        public MainWindow()
        {
            InitializeComponent();
            ReloadDecks();
        }

        private void ReloadDecks()
        {
            _decks = _store.LoadAll()
                          .OrderByDescending(d => d.SavedAt)
                          .ToList();

            DeckList.ItemsSource = _decks;
            CardList.ItemsSource = null;

            StatusText.Text = $"저장된 덱: {_decks.Count}개";
        }

        private void OpenAddDeck_Click(object sender, RoutedEventArgs e)
        {
            var w = new AddDeckWindow { Owner = this };

            if (w.ShowDialog() == true)
            {
                ReloadDecks();

                // 방금 저장한 덱 자동 선택(이름 기준)
                if (w.SavedDeck != null)
                {
                    var target = _decks.FirstOrDefault(d => d.Name == w.SavedDeck.Name);
                    if (target != null)
                        DeckList.SelectedItem = target;
                }
            }
        }

        private void DeleteDeck_Click(object sender, RoutedEventArgs e)
        {
            var selected = DeckList.SelectedItem as DeckDefinition;
            if (selected == null)
                return;

            var ok = MessageBox.Show($"'{selected.Name}' 삭제할까?", "삭제", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            if (!ok) return;

            _store.DeleteByName(selected.Name);
            ReloadDecks();
        }
        private async void DeckList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var d = DeckList.SelectedItem as DeckDefinition;
            if (d == null) return;

            // 로그 세션 시작 시(LoadingScreen Gameplay.Start) 오버레이 재설정에 사용
            App.CurrentSelectedDeck = d;

            if (string.IsNullOrWhiteSpace(d.DeckCode))
            {
                StatusText.Text = "덱 코드가 없습니다.";
                return;
            }

            StatusText.Text = "카드 DB 로딩 중...";
            await _cardDb.EnsureLoadedAsync();

            var dict = DeckCodeDecoder.DecodeToDbfCounts(d.DeckCode);

            var list = new List<DeckCard>();
            foreach (var kv in dict)
            {
                int dbfId = kv.Key;
                int count = kv.Value;

                if (_cardDb.TryGet(dbfId, out var info))
                {
                    list.Add(new DeckCard { Name = info.NameKo, Cost = info.Cost, Count = count });
                    // 여기서 info.CardId도 같이 들고 싶으면 DeckCard 모델을 확장해
                }
                else
                {
                    list.Add(new DeckCard { Name = $"알 수 없는 카드 (dbfId={dbfId})", Cost = 0, Count = count });
                }
            }

            // 정렬(코스트 -> 이름)
            list.Sort((a, b) =>
            {
                int c = a.Cost.CompareTo(b.Cost);
                return c != 0 ? c : string.Compare(a.Name, b.Name, System.StringComparison.Ordinal);
            });

            CardList.ItemsSource = list;
            StatusText.Text = $"카드 {list.Sum(x => x.Count)}장 / {list.Count}종";
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            ReloadDecks();
        }
        private async void ShowOnDeckOverlay_Click(object sender, RoutedEventArgs e)
        {
            var d = DeckList.SelectedItem as DeckDefinition;
            if (d == null) return;

            try
            {
                await App.Overlays.ShowDeckAsync(d);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Show Deck 실패");
            }
        }


    }

    // 빠른 삭제/저장용(DeckStore를 크게 안 뜯기 위해 임시로 확장)
}
