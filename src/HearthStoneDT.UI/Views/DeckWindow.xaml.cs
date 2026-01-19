using HearthStoneDT.UI.Overlay;
using System.Windows.Input;


namespace HearthStoneDT.UI.Views
{
    public partial class DeckWindow : ToggleableOverlayBase
    {
        public DeckWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => SetInteractive(false);

        }

        private void DeckWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 클릭스루 상태면 애초에 이벤트가 안 들어오므로, 여기선 클릭 가능 상태일 때만 실행됨
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
