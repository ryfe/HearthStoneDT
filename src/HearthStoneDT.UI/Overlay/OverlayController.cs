using System;
using System.Windows;
using System.Windows.Input;
using HearthStoneDT.UI.Views;
using HearthStoneDT.UI.Decks;

namespace HearthStoneDT.UI.Overlay
{
    /// <summary>
    /// 오버레이(현재는 Deck만) 생성/수명/전역 핫키/클릭스루 토글을 관리.
    /// MainWindow는 이 컨트롤러에 "요청"만 한다.
    /// </summary>
    public sealed class OverlayController : IDisposable
    {
        // 핫키 ID
        private const int HK_TOGGLE_DECK = 1001;

        // Modifier flags (Win32)
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private GlobalHotKeyHost? _hotKeyHost;

        private DeckWindow? _deckWindow;
        public CardDb CardDb { get; } = new CardDb();
        public void Initialize()
        {
            // 메시지용 HWND 생성 + 핫키 등록
            _hotKeyHost = new GlobalHotKeyHost();
            _hotKeyHost.HotKeyPressed += OnHotKeyPressed;

            // Ctrl + Shift + Q
            Register(HK_TOGGLE_DECK, MOD_CONTROL | MOD_SHIFT, Key.Q);
        }

        public void ShowDeck()
        {
            EnsureDeckWindow();

            if (!_deckWindow!.IsVisible)
                _deckWindow.Show();

            _deckWindow.Activate();

        }

        public void ToggleDeckInteractivity()
        {
            EnsureDeckWindow();

            // ✅ HWND 생성/보장
            if (!_deckWindow!.IsVisible)
                _deckWindow.Show();

            _deckWindow.Activate();
            _deckWindow.ToggleInteractive();

        }

        private void EnsureDeckWindow()
        {
            if (_deckWindow != null) return;

            _deckWindow = new DeckWindow();
            _deckWindow.Closed += (_, _) =>
            {
                _deckWindow = null;
            };

            // 기본 클릭스루
           _deckWindow.ToggleInteractive();

            // 최초 위치는 DeckWindow 쪽에서 결정하는 게 맞음.
            // (나중에 DeckWindow가 저장 위치 복원/기본 오른쪽 배치 처리)
        }

        private void OnHotKeyPressed(int id)
        {
            // 전역 핫키 이벤트는 UI 스레드에서 실행되도록 보장
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (id == HK_TOGGLE_DECK)
                {
                    ToggleDeckInteractivity();
                    return;
                }
            });
        }

        private void Register(int id, uint modifiers, Key key)
        {
            if (_hotKeyHost == null)
                throw new InvalidOperationException("OverlayController.Initialize() 먼저 호출해야 합니다.");

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

            if (!GlobalHotKeyHost.RegisterHotKey(_hotKeyHost.Handle, id, modifiers, vk))
            {
                // 이미 다른 프로그램이 점유 중일 수 있음
                // (핫키 충돌 시 바꾸면 됨)
                throw new InvalidOperationException($"전역 핫키 등록 실패: id={id}");
            }
        }
 // 추가

        public async Task ShowDeckAsync(DeckDefinition deck)
        {
            EnsureDeckWindow();

            if (!_deckWindow!.IsVisible)
                _deckWindow.Show();

            await CardDb.EnsureLoadedAsync();

            await _deckWindow.SetDeckAsync(deck, CardDb);

            _deckWindow.Activate();
            _deckWindow.SetInteractive(false);
        }

        /// <summary>
        /// 로그 이벤트로 덱 카운트를 갱신한다. DeckWindow가 아직 없으면(오버레이를 한 번도 띄운 적 없으면)
        /// 일단 무시한다. (원하면 EnsureDeckWindow()로 생성만 해도 되지만, 사용자가 원치 않을 수 있어 보수적으로 둠)
        /// </summary>
        public void ApplyCardRemovedFromDeck(string cardId, int entityId)
        {
            if (_deckWindow == null)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                _deckWindow.RemoveFromDeck(cardId, entityId);
            });
        }

        public void ApplyCardAddedToDeck(string cardId, int entityId)
        {
            if (_deckWindow == null)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                _deckWindow.AddToDeck(cardId, entityId, CardDb);
            });
        }


        public void Dispose()
        {
            if (_hotKeyHost == null) return;

            GlobalHotKeyHost.UnregisterHotKey(_hotKeyHost.Handle, HK_TOGGLE_DECK);

            _hotKeyHost.Dispose();
            _hotKeyHost = null;
        }
    }
}
