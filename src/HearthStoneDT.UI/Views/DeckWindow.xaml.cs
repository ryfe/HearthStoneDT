using HearthStoneDT.UI.Overlay;


namespace HearthStoneDT.UI.Views
{
    public partial class DeckWindow : ToggleableOverlayBase
    {
        public DeckWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => SetInteractive(false);
        }
    }
}
