using System.Windows;

namespace BtcWidget.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        // Sizes to content, but never taller than the screen: the content scrolls instead.
        MaxHeight = SystemParameters.WorkArea.Height - 24;
    }
}
