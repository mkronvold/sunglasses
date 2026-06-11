using System.Runtime.InteropServices;
using static Sunglasses.Platform.Win32Interop;

namespace Sunglasses.Platform;

/// <summary>
/// Installs low-level mouse and keyboard hooks to capture global hotkeys:
///   - RAlt + MouseWheel        -> coarse transparency adjustment (raises <see cref="AdjustRequested"/>)
///   - RAlt + RCtrl + MouseWheel -> fine transparency adjustment
///   - RAlt + RCtrl + Q          -> exit (raises <see cref="ExitRequested"/>)
///
/// Hooks are installed on the thread that calls <see cref="Install"/>, which must
/// have a running message loop (i.e. the WPF UI thread). Callbacks therefore arrive
/// on the UI thread and are kept lightweight.
/// </summary>
public sealed class GlobalHookService : IDisposable
{
    private const double CoarseStep = 0.05;
    private const double FineStep = 0.01;

    // Keep delegate references alive for the lifetime of the hooks to prevent GC.
    private readonly HookProc _mouseProc;
    private readonly HookProc _keyboardProc;

    private IntPtr _mouseHook = IntPtr.Zero;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private bool _disposed;

    /// <summary>Raised with the signed opacity delta to apply (e.g. +0.05 or -0.01).</summary>
    public event Action<double>? AdjustRequested;

    /// <summary>Raised when the exit hotkey (RAlt+RCtrl+Q) is pressed.</summary>
    public event Action? ExitRequested;

    public GlobalHookService()
    {
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
    }

    public void Install()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GlobalHookService));
        }

        IntPtr hMod = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to install low-level mouse hook (Win32 error {err}).");
        }

        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
        if (_keyboardHook == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            // Roll back the mouse hook so we don't leak it on partial failure.
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
            throw new InvalidOperationException($"Failed to install low-level keyboard hook (Win32 error {err}).");
        }
    }

    /// <summary>
    /// Removes and reinstalls both hooks. Windows can silently drop low-level
    /// hooks (e.g. if a callback exceeds the system timeout, which can happen
    /// across session lock/unlock or resume from sleep); calling this on those
    /// events restores hotkey handling.
    /// </summary>
    public void Reinstall()
    {
        if (_disposed)
        {
            return;
        }

        Unhook();
        Install();
    }

    private void Unhook()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_MOUSEWHEEL)
        {
            bool rAlt = IsKeyDown(VK_RMENU);
            if (rAlt)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                // High word of mouseData is the signed wheel delta.
                int wheel = (short)((data.mouseData >> 16) & 0xffff);
                int direction = Math.Sign(wheel);

                if (direction != 0)
                {
                    bool fine = IsKeyDown(VK_RCONTROL);
                    double step = fine ? FineStep : CoarseStep;
                    // Wheel up brightens (less dimming), wheel down dims (more dimming).
                    AdjustRequested?.Invoke(-direction * step);
                }

                // Consume the event so the scroll doesn't reach other applications.
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            // While Alt is held, key presses arrive as WM_SYSKEYDOWN.
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (data.vkCode == VK_Q && IsKeyDown(VK_RMENU) && IsKeyDown(VK_RCONTROL))
                {
                    ExitRequested?.Invoke();
                    return (IntPtr)1;
                }
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Unhook();
    }
}
