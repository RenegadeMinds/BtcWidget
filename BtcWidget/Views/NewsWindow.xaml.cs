using System.Windows;
using System.Windows.Input;
using BtcWidget.ViewModels;

namespace BtcWidget.Views;

public partial class NewsWindow : Window
{
    public NewsWindow()
    {
        InitializeComponent();
        // A flyout: goes away when you click elsewhere or press Escape, unless pinned. A context menu or the
        // source box's drop-down keeps the window active, so neither closes it.
        Deactivated += (_, _) => { if (IsLoaded && DataContext is not NewsViewModel { IsPinned: true }) Close(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            // Escape clears a filter you are typing before it closes the popup.
            if (FilterBox.IsKeyboardFocused && FilterBox.Text.Length > 0) { FilterBox.Text = ""; return; }
            Close();
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase) DragMove();
    }
}
