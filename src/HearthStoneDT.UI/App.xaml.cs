using System.Windows;
using HearthStoneDT.UI.Views;

namespace HearthStoneDT.UI
{
    public partial class App : Application
    {

        private DeckWindow? _deck;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var main = new HearthStoneDT.UI.Views.MainWindow();

            main.Show();

            _deck = new DeckWindow();
            _deck.Show();

            main.SetDeckOverlay(_deck);
        }
    }
}
