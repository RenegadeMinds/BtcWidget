using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace BtcWidget.Services;

/// <summary>Registers one global hotkey via user32 RegisterHotKey and raises <see cref="Pressed"/>.</summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xB7C1;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [Flags]
    private enum Mod : uint { Alt = 1, Ctrl = 2, Shift = 4, Win = 8, NoRepeat = 0x4000 }

    private readonly HwndSource _source;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyManager(Window window)
    {
        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        _source = HwndSource.FromHwnd(helper.Handle) ?? throw new InvalidOperationException("No HwndSource");
        _source.AddHook(WndProc);
    }

    /// <summary>Parses strings like "Ctrl+Alt+B" or "Win+Shift+F9". Returns false if registration failed.</summary>
    public bool Register(string hotkey)
    {
        Unregister();
        if (!TryParse(hotkey, out var mods, out var key)) return false;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        _registered = RegisterHotKey(_source.Handle, HotkeyId, (uint)(mods | Mod.NoRepeat), vk);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered) UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    private static bool TryParse(string text, out Mod mods, out Key key)
    {
        mods = 0; key = Key.None;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= Mod.Ctrl; break;
                case "alt": mods |= Mod.Alt; break;
                case "shift": mods |= Mod.Shift; break;
                case "win": case "windows": mods |= Mod.Win; break;
                default:
                    if (!Enum.TryParse(raw, ignoreCase: true, out key)) return false;
                    break;
            }
        }
        return key != Key.None;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
    }
}
