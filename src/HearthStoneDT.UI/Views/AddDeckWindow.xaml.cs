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
                var originalName = deck.Name;
                deck.SavedAt = DateTime.Now;

                // ✅ 이름 중복 처리: 완전 일치만 검사
                var existing = _store.LoadAll();
                deck.Name = _store.MakeUniqueName(deck.Name, existing);

                // ✅ 덮어쓰기 금지: 무조건 새 덱 추가
                SavedDeck = _store.Add(deck);

                if (SavedDeck.Name != originalName)
                    StatusText.Text = $"이름 중복 → '{SavedDeck.Name}'로 저장됨";
                else
                    StatusText.Text = $"저장됨: {SavedDeck.Name}";
                        DialogResult = true;
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
