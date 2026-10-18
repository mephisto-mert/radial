using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace RadialLauncher.Services.Input;

public interface IHotkeyAndSnippetService
{
    IntPtr LastTargetWindow { get; }
    void CaptureTargetWindow();
    void SimulateHotkey(string hotkeySpec);
    void PasteSnippet(string text);
}

public class HotkeyAndSnippetService : IHotkeyAndSnippetService
{
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public IntPtr LastTargetWindow { get; private set; }

    public void CaptureTargetWindow()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || !IsWindow(foreground))
        {
            return;
        }

        GetWindowThreadProcessId(foreground, out uint pid);
        if (pid != 0 && pid != (uint)Environment.ProcessId)
        {
            LastTargetWindow = foreground;
        }
    }

    public void SimulateHotkey(string hotkeySpec)
    {
        if (string.IsNullOrWhiteSpace(hotkeySpec))
        {
            return;
        }

        RestoreTargetWindowFocus(25);

        string[] parts = hotkeySpec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var vks = new List<byte>(parts.Length);
        foreach (string part in parts)
        {
            if (TryResolveVirtualKey(part, out byte vk))
            {
                vks.Add(vk);
            }
        }

        if (vks.Count == 0)
        {
            return;
        }

        for (int i = 0; i < vks.Count; i++)
        {
            uint flags = IsExtendedKey(vks[i]) ? KEYEVENTF_EXTENDEDKEY : 0;
            keybd_event(vks[i], 0, flags, UIntPtr.Zero);
        }

        for (int i = vks.Count - 1; i >= 0; i--)
        {
            uint flags = KEYEVENTF_KEYUP | (IsExtendedKey(vks[i]) ? KEYEVENTF_EXTENDEDKEY : 0);
            keybd_event(vks[i], 0, flags, UIntPtr.Zero);
        }
    }

    public void PasteSnippet(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.Invoke(() => System.Windows.Clipboard.SetText(text));
        }
        else
        {
            System.Windows.Clipboard.SetText(text);
        }

        RestoreTargetWindowFocus(35);

        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private void RestoreTargetWindowFocus(int delayMs)
    {
        if (LastTargetWindow != IntPtr.Zero && IsWindow(LastTargetWindow))
        {
            SetForegroundWindow(LastTargetWindow);
            Thread.Sleep(delayMs);
        }
    }

    private static bool IsExtendedKey(byte vk) =>
        vk is 0x5B or 0x5C or 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2D or 0x2E;

    private static bool TryResolveVirtualKey(string token, out byte vk)
    {
        vk = 0;
        string normalized = token.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        if (normalized.Length == 1)
        {
            char c = normalized[0];
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
            {
                vk = (byte)c;
                return true;
            }
        }

        if (normalized.StartsWith('F') &&
            int.TryParse(normalized.AsSpan(1), out int fNum) &&
            fNum is >= 1 and <= 24)
        {
            vk = (byte)(0x70 + (fNum - 1));
            return true;
        }

        vk = normalized switch
        {
            "CTRL" or "CONTROL" or "LCTRL" or "RCTRL" => 0x11,
            "SHIFT" or "LSHIFT" or "RSHIFT" => 0x10,
            "ALT" or "MENU" or "LALT" or "RALT" => 0x12,
            "WIN" or "LWIN" or "WINDOWS" or "META" => 0x5B,
            "RWIN" => 0x5C,
            "TAB" => 0x09,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" or "SPACEBAR" => 0x20,
            "BACKSPACE" or "BACK" or "BS" => 0x08,
            "DELETE" or "DEL" => 0x2E,
            "INSERT" or "INS" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" or "PGUP" => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "LEFT" or "ARROWLEFT" => 0x25,
            "UP" or "ARROWUP" => 0x26,
            "RIGHT" or "ARROWRIGHT" => 0x27,
            "DOWN" or "ARROWDOWN" => 0x28,
            "PRINTSCREEN" or "PRTSC" or "SNAPSHOT" => 0x2C,
            "PLUS" or "OEMPLUS" or "=" => 0xBB,
            "MINUS" or "OEMMINUS" or "-" => 0xBD,
            "COMMA" or "," => 0xBC,
            "PERIOD" or "." => 0xBE,
            "SLASH" or "/" => 0xBF,
            "SEMICOLON" or ";" => 0xBA,
            "OPENBRACKET" or "[" => 0xDB,
            "BACKSLASH" or "\\" => 0xDC,
            "CLOSEBRACKET" or "]" => 0xDD,
            "QUOTE" or "'" => 0xDE,
            "TILDE" or "BACKTICK" or "`" => 0xC0,
            _ => (byte)0
        };

        return vk != 0;
    }
}
