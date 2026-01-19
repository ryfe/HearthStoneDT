using System;
using System.Windows;
using HearthStoneDT.UI.Decks;

namespace HearthStoneDT.UI.Views
{
    public partial class AddDeckWindow : Window
    {
        private readonly DeckStore _store = new DeckStore();

        // Main에서 결과를 받기 위한 속성(선택)
        public DeckDefinition? SavedDeck { get; private set; }

        public AddDeckWindow()
        {
            InitializeComponent();
            StatusText.Text = "";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var raw = RawDeckTextBox.Text;
                var deck = DeckParser.Parse(raw);

                deck.SavedAt = DateTime.Now;

                SavedDeck = _store.AddOrReplace(deck);

                StatusText.Text = $"저장됨: {SavedDeck.Name} (카드 {SavedDeck.Cards.Count}종)";
                DialogResult = true;   // 모달로 열었을 때 결과 반환
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
                MessageBox.Show(ex.ToString(), "덱 저장 실패");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
