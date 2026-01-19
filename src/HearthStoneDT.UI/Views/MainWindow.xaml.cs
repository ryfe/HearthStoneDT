using System.Windows;
using System.Windows.Input;

namespace HearthStoneDT.UI.Views
{
    public partial class MainWindow : Window
    {
        private DeckWindow? _deck;
        private bool _deckInteractive;

        public MainWindow()
        {
            InitializeComponent();
            PreviewKeyDown += OnKeyDown;
        }

        public void SetDeckOverlay(DeckWindow deck)
        {
            _deck = deck;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_deck == null)
                return;

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.O)
            {
                _deckInteractive = !_deckInteractive;
                _deck.SetInteractive(_deckInteractive);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                _deckInteractive = false;
                _deck.SetInteractive(false);
                e.Handled = true;
            }
        }
    }
}
