using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HearthStoneDT.UI.Overlay;
using HearthStoneDT.UI.Services;
using HearthStoneDT.UI.Decks;

namespace HearthStoneDT.UI
{
    public partial class App : Application
    {
        public static OverlayController Overlays { get; private set; } = new OverlayController();
        public static GameLogService Logs { get; private set; } = new GameLogService();

        // MainWindow에서 선택한 덱을 전역으로 공유(세션 시작 시 오버레이 재설정 용)
        public static DeckDefinition? CurrentSelectedDeck { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var logDir = HearthStoneDT.UI.Logs.HearthstoneLogPathResolver.ResolveLogDirectory();
            if (logDir == null)
            {
                // 여기서 메시지 박스/로그 찍고 return 하는게 좋음
                MessageBox.Show("Hearthstone 로그 폴더를 찾지 못했습니다.");
                return;
            }
            // ✅ UI 스레드 예외
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            // ✅ 비-UI 스레드 예외
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            Overlays.Initialize();

            // 로그 서비스 시작(Logs 폴더가 없으면 예외 -> crash.txt로 기록됨)
            try
            {
                Logs.BindAutoDeckReset(Overlays, () => CurrentSelectedDeck);
                Logs.Start();
                // 안정 모드: 게임 세션 시작(Gameplay.Start)마다 선택된 덱으로 오버레이를 재초기화

                // Power.log 기반 덱 변화(드로우/서치/덱복귀)를 오버레이에 반영
                Logs.CardRemovedFromDeck += cardId => Overlays.ApplyCardRemovedFromDeck(cardId);
                Logs.CardAddedToDeck += cardId => Overlays.ApplyCardAddedToDeck(cardId);
                
            }
            catch (Exception ex)
            {
                LogException(ex);
            }

            var main = new HearthStoneDT.UI.Views.MainWindow();
            main.Show();
        }

        private static void LogException(Exception ex)
        {
            var path = @"C:\HearthStoneDT\crash.txt";
            File.AppendAllText(path, $"[{DateTime.Now}] {ex}\r\n\r\n");
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            MessageBox.Show(e.Exception.ToString(), "Crash");
            e.Handled = true; // 일단 안 죽게
        }

        private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LogException(ex);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _ = Logs.StopAsync(); } catch { /* ignore */ }
            Overlays.Dispose();
            base.OnExit(e);
        }
    }
}
