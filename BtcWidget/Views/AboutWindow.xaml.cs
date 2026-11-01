using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BtcWidget.Views;

public partial class AboutWindow : Window
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public AboutWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var dark = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
        };
    }
}
