using System.ComponentModel;
using System.Windows;
using HearthStoneDT.UI.Overlay;
using System.Windows.Input;
namespace HearthStoneDT.UI.Views
{
    public partial class DeckWindow : ToggleableOverlayBase
    {
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

