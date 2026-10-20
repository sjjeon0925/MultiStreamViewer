using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace MultiStreamViewer.Views;

// 방송 영역 위에 얹는 투명 오버레이 창들이 같이 쓰는 Win32 호출
internal static class WindowNative
{
    // 클릭해도 활성화되지 않게 해서, 메인 창이 포커스를 유지하도록 한다 (Esc 처리 유지).
    public static void SetNoActivate(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        SetWindowLong(
            hwnd,
            GWL_EXSTYLE,
            exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    // 화면 좌표(물리 픽셀)로 위치와 크기를 정한다.
    public static void SetBounds(IntPtr hwnd, Rect boundsPx)
    {
        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            (int)Math.Round(boundsPx.Left),
            (int)Math.Round(boundsPx.Top),
            (int)Math.Round(boundsPx.Width),
            (int)Math.Round(boundsPx.Height),
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    // 같은 소유자(메인 창)를 가진 다른 오버레이 창들보다 위로 올린다.
    public static void BringToTop(IntPtr hwnd)
    {
        SetWindowPos(
            hwnd,
            HWND_TOP,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // 소유자 창을 바꾼다. 소유된 창은 항상 소유자 위에 그려지므로,
    // 다른 오버레이 위에 올리고 싶은 창의 소유자를 그 오버레이로 지정하면 z순서가 보장된다.
    public static void SetOwner(IntPtr hwnd, IntPtr ownerHwnd)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, ownerHwnd);
        else
            SetWindowLong(hwnd, GWLP_HWNDPARENT, (int)ownerHwnd);
    }

    // 마우스 커서의 화면 좌표(물리 픽셀)
    public static bool TryGetCursor(out Point cursorPx)
    {
        bool ok = GetCursorPos(out POINT p);

        cursorPx = new Point(p.X, p.Y);

        return ok;
    }

    // 창의 화면 좌표(물리 픽셀)
    public static bool TryGetWindowBounds(IntPtr hwnd, out Rect boundsPx)
    {
        bool ok = GetWindowRect(hwnd, out RECT r);

        boundsPx = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

        return ok;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    private const int GWLP_HWNDPARENT = -8;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private static readonly IntPtr HWND_TOP = IntPtr.Zero;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
