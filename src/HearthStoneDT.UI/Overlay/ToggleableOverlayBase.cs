using System.Windows;
using System.Windows.Interop;

namespace HearthStoneDT.UI.Overlay
{
    public class ToggleableOverlayBase : Window
{
    public bool IsInteractive { get; private set; }

    public void ToggleInteractive()
    {
        SetInteractive(!IsInteractive);
    }

    public void SetInteractive(bool interactive)
    {
        IsInteractive = interactive;
        WindowStyles.SetClickThrough(this, !interactive);
    }
}

}
