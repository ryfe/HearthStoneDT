using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace HearthStoneDT.UI.Overlay
{
    /// <summary>
    /// WPF Window 없이도 전역 핫키를 받을 수 있는 메시지용 HWND 호스트.
    /// </summary>
    public sealed class GlobalHotKeyHost : IDisposable
    {
        private readonly HwndSource _source;
        private readonly HwndSourceHook _hook;
        private bool _disposed;

        public IntPtr Handle => _source.Handle;

        public event Action<int>? HotKeyPressed;

        public GlobalHotKeyHost()
        {
            var parameters = new HwndSourceParameters("HearthStoneDT.HotKeyHost")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0, // WS_POPUP
                ExtendedWindowStyle = 0x00000080 // WS_EX_TOOLWINDOW
            };

            _source = new HwndSource(parameters);
            _hook = WndProc;
            _source.AddHook(_hook);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                HotKeyPressed?.Invoke(id);
                handled = true;
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _source.RemoveHook(_hook);
            _source.Dispose();
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
