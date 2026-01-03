using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace WinFormsApp1.Infrastructure;

public sealed class WindowInfo
{
    public WindowInfo(IntPtr hwnd, string title, string processName, Rectangle rect)
    {
        Hwnd = hwnd;
        Title = title;
        ProcessName = processName;
        Rect = rect;
    }

    public IntPtr Hwnd { get; }
    public string Title { get; }
    public string ProcessName { get; }
    public Rectangle Rect { get; }

    public override string ToString()
    {
        int width = Math.Max(0, Rect.Width);
        int height = Math.Max(0, Rect.Height);
        return $"[{Title}] ({width}x{height}) - {ProcessName}";
    }
}

public sealed class WindowEnumerator
{
    private const int MinWidth = 160;
    private const int MinHeight = 120;

    public IReadOnlyList<WindowInfo> GetOpenWindows()
    {
        var results = new List<WindowInfo>();
        Win32.EnumWindows((hWnd, _) =>
        {
            if (!Win32.IsWindowVisible(hWnd))
            {
                return true;
            }

            int length = Win32.GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return true;
            }

            var sb = new StringBuilder(length + 1);
            if (Win32.GetWindowText(hWnd, sb, sb.Capacity) <= 0)
            {
                return true;
            }

            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            if (!Win32.GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            var bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            if (bounds.Width < MinWidth || bounds.Height < MinHeight)
            {
                return true;
            }

            string processName = "unknown";
            try
            {
                Win32.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != 0)
                {
                    using Process process = Process.GetProcessById((int)pid);
                    processName = process.ProcessName;
                }
            }
            catch
            {
            }

            results.Add(new WindowInfo(hWnd, title, processName, bounds));
            return true;
        }, IntPtr.Zero);

        return results;
    }
}
