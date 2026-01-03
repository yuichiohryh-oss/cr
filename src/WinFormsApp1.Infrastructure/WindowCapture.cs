using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace WinFormsApp1.Infrastructure;

public sealed class WindowCapture
{
    public Bitmap? CaptureClient(string windowTitle)
    {
        IntPtr hWnd = Win32.FindWindow(null, windowTitle);
        if (hWnd == IntPtr.Zero)
        {
            return null;
        }

        return CaptureClient(hWnd);
    }

    public Bitmap? CaptureClient(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return null;
        }

        if (!Win32.GetClientRect(hWnd, out var rect))
        {
            return null;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var pt = new Win32.POINT { X = 0, Y = 0 };
        if (!Win32.ClientToScreen(hWnd, ref pt))
        {
            return null;
        }

        var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(pt.X, pt.Y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        }

        return bmp;
    }
}
