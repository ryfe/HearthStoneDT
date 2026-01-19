using System.Windows;
using System.Windows.Input;

namespace HearthStoneDT.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenDeck_Click(object sender, RoutedEventArgs e)
        {
            HearthStoneDT.UI.App.Overlays.ShowDeck();
        }
    }
}
