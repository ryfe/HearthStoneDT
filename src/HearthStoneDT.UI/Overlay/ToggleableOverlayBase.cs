using System.Windows;

namespace HearthStoneDT.UI.Overlay
{
    public class ToggleableOverlayBase : Window
    {
        private bool _interactive;

        protected void ApplyClickThrough()
        {
            WindowStyles.SetClickThrough(this, !_interactive);
        }

        public void SetInteractive(bool interactive)
        {
            _interactive = interactive;
            ApplyClickThrough();
        }
    }
}
