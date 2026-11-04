using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace SmartPassword.App.Services;

/// <summary>
/// Copies secrets to the clipboard and clears them again after a delay (F-43).
/// On Windows the copy is marked so it is kept out of clipboard history (Win+V) and cloud clipboard.
/// </summary>
public sealed class SecureClipboard(IClipboard? fallback)
{
    public TimeSpan ClearAfter { get; set; } = TimeSpan.FromSeconds(20);

    private uint _ownedSequence;
    private CancellationTokenSource? _pendingClear;

    public async Task CopyAsync(string text)
    {
        _pendingClear?.Cancel();

        if (OperatingSystem.IsWindows())
            _ownedSequence = Win32.SetText(text);
        else if (fallback != null)
            await fallback.SetTextAsync(text);

        var cts = _pendingClear = new CancellationTokenSource();
        try
        {
            await Task.Delay(ClearAfter, cts.Token);
            await ClearAsync();
        }
        catch (TaskCanceledException)
        {
        }
    }

    /// <summary>Clears the clipboard, but only if it still holds what we put there.</summary>
    public async Task ClearAsync()
    {
        _pendingClear?.Cancel();
        if (OperatingSystem.IsWindows())
        {
            if (_ownedSequence != 0 && Win32.GetClipboardSequenceNumber() == _ownedSequence)
                Win32.Clear();
            _ownedSequence = 0;
        }
        else if (fallback != null)
        {
            await fallback.ClearAsync();
        }
    }

    private static class Win32
    {
        private const uint CfUnicodeText = 13;
        private const uint GmemMoveable = 0x0002;

        [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
        [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string lpszFormat);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalFree(IntPtr hMem);

        public static uint SetText(string text)
        {
            Open();
            try
            {
                EmptyClipboard();
                var chars = text.ToCharArray();
                SetData(CfUnicodeText, h =>
                {
                    Marshal.Copy(chars, 0, h, chars.Length);
                    Marshal.WriteInt16(h, chars.Length * 2, 0);
                }, (chars.Length + 1) * 2);
                Array.Clear(chars);

                // Documented opt-outs for clipboard history, cloud clipboard and clipboard monitors.
                SetData(RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing"), h => Marshal.WriteInt32(h, 0), 4);
                SetData(RegisterClipboardFormat("CanIncludeInClipboardHistory"), h => Marshal.WriteInt32(h, 0), 4);
                SetData(RegisterClipboardFormat("CanUploadToCloudClipboard"), h => Marshal.WriteInt32(h, 0), 4);
            }
            finally
            {
                CloseClipboard();
            }
            return GetClipboardSequenceNumber();
        }

        public static void Clear()
        {
            Open();
            try { EmptyClipboard(); }
            finally { CloseClipboard(); }
        }

        private static void Open()
        {
            // Another process may hold the clipboard briefly.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                if (OpenClipboard(IntPtr.Zero)) return;
                Thread.Sleep(20);
            }
            throw new InvalidOperationException("Udklipsholderen er optaget af et andet program.");
        }

        private static void SetData(uint format, Action<IntPtr> fill, int size)
        {
            var handle = GlobalAlloc(GmemMoveable, (UIntPtr)size);
            if (handle == IntPtr.Zero) return;
            var ptr = GlobalLock(handle);
            fill(ptr);
            GlobalUnlock(handle);
            if (SetClipboardData(format, handle) == IntPtr.Zero)
                GlobalFree(handle);
        }
    }
}
