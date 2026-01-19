using System;
using System.Windows;
using HearthStoneDT.UI.Overlay;

namespace HearthStoneDT.UI
{
    public partial class App : Application
    {
        public static OverlayController Overlays { get; private set; } = new OverlayController();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 전역 핫키 + 오버레이 관리자 시작
            Overlays.Initialize();

            var main = new HearthStoneDT.UI.Views.MainWindow();
            main.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 핫키 해제
            Overlays.Dispose();
            base.OnExit(e);
        }
    }
}
