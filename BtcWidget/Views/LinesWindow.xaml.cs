using System.Windows;

namespace BtcWidget.Views;

public partial class LinesWindow : Window
{
    public LinesWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // The left pane is a fixed-width column; scale it with the font the dialog was given.
            LeftColumn.Width = new GridLength(FontSize * 31);
            // Note: the first time any WPF TextBox gets keyboard focus, Windows' text-input stack allocates
            // ~60 MB of native memory in this process and never frees it. Measured and accepted; see README.
            SearchBox.Focus();
        };
    }
}
