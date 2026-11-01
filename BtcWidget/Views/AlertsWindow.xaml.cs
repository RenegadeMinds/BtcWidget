using System.Windows;

namespace BtcWidget.Views;

public partial class AlertsWindow : Window
{
    public AlertsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // Land in the value box when a line is already chosen: the usual flow is "type 410, Enter".
            if (ValueBox.IsVisible) ValueBox.Focus(); else LineBox.Focus();
        };
    }
}
