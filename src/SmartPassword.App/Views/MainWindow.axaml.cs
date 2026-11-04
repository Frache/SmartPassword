using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SmartPassword.App.ViewModels;

namespace SmartPassword.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Any input counts as activity for auto-lock.
        AddHandler(PointerMovedEvent, OnActivity, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnActivity, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        if (OperatingSystem.IsWindows())
        {
            Win32Properties.AddWndProcHookCallback(this, WndProc);
            Opened += (_, _) => SessionNotifications.Register(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
            Closed += (_, _) => SessionNotifications.Unregister(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        }
    }

    private void OnActivity(object? sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.RegisterActivity();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.L && DataContext is MainViewModel main)
        {
            main.Lock();
            e.Handled = true;
        }
    }

    /// <summary>Locks the vault when Windows is locked, the session is disconnected, or the PC goes to sleep.</summary>
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var lockNow = msg switch
        {
            SessionNotifications.WmWtsSessionChange => wParam.ToInt64() is SessionNotifications.WtsSessionLock
                or SessionNotifications.WtsConsoleDisconnect or SessionNotifications.WtsRemoteDisconnect,
            SessionNotifications.WmPowerBroadcast => wParam.ToInt64() == SessionNotifications.PbtApmSuspend,
            _ => false,
        };
        if (lockNow)
            (DataContext as MainViewModel)?.Lock();
        return IntPtr.Zero;
    }

    private static class SessionNotifications
    {
        public const uint WmWtsSessionChange = 0x02B1;
        public const uint WmPowerBroadcast = 0x0218;
        public const long WtsConsoleDisconnect = 0x2;
        public const long WtsRemoteDisconnect = 0x4;
        public const long WtsSessionLock = 0x7;
        public const long PbtApmSuspend = 0x4;
        private const int NotifyForThisSession = 0;

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

        public static void Register(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero) WTSRegisterSessionNotification(hWnd, NotifyForThisSession);
        }

        public static void Unregister(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero) WTSUnRegisterSessionNotification(hWnd);
        }
    }
}
