using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HearthStoneDT.UI.Overlay
{
    internal static class WindowStyles
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public static void SetClickThrough(Window window, bool clickThrough)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var style = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (clickThrough)
                SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            else
                SetWindowLong(hwnd, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
        }
    }
}
