using System.Windows;
using System.Windows.Controls;

using MultiStreamViewer.Views;

namespace MultiStreamViewer.Services;

// 창 테두리/상하단 바를 숨기고 모니터 전체(작업 표시줄 포함)를 덮는 전체화면 진입 및 복원을 관리한다.
public sealed class FullscreenController
{
    private readonly Window window;
    private readonly RowDefinition topBarRow;
    private readonly RowDefinition bottomStatusBarRow;
    private readonly Grid streamGrid;

    private readonly GridLength initialTopBarHeight;
    private readonly GridLength initialBottomStatusBarHeight;

    private WindowStyle savedWindowStyle;
    private ResizeMode savedResizeMode;
    private WindowState savedWindowState;
    private double savedLeft;
    private double savedTop;
    private double savedWidth;
    private double savedHeight;

    public bool IsFullscreen { get; private set; }

    public FullscreenController(
        Window window,
        RowDefinition topBarRow,
        RowDefinition bottomStatusBarRow,
        Grid streamGrid)
    {
        this.window = window;
        this.topBarRow = topBarRow;
        this.bottomStatusBarRow = bottomStatusBarRow;
        this.streamGrid = streamGrid;

        initialTopBarHeight = topBarRow.Height;
        initialBottomStatusBarHeight = bottomStatusBarRow.Height;
    }

    public bool Enter()
    {
        if (IsFullscreen)
            return false;

        IsFullscreen = true;

        savedWindowStyle = window.WindowStyle;
        savedResizeMode = window.ResizeMode;
        savedWindowState = window.WindowState;
        savedLeft = window.Left;
        savedTop = window.Top;
        savedWidth = window.Width;
        savedHeight = window.Height;

        topBarRow.Height = new GridLength(0);
        bottomStatusBarRow.Height = new GridLength(0);
        streamGrid.Margin = new Thickness(0);

        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowState = WindowState.Normal;

        if (WindowNative.TryGetFullMonitorDipBounds(window, out Rect monitorBounds))
        {
            window.Left = monitorBounds.Left;
            window.Top = monitorBounds.Top;
            window.Width = monitorBounds.Width;
            window.Height = monitorBounds.Height;
        }

        return true;
    }

    public bool Exit()
    {
        if (!IsFullscreen)
            return false;

        IsFullscreen = false;

        topBarRow.Height = initialTopBarHeight;
        bottomStatusBarRow.Height = initialBottomStatusBarHeight;
        streamGrid.Margin = new Thickness(3);

        window.WindowStyle = savedWindowStyle;
        window.ResizeMode = savedResizeMode;
        window.Left = savedLeft;
        window.Top = savedTop;
        window.Width = savedWidth;
        window.Height = savedHeight;
        window.WindowState = savedWindowState;

        return true;
    }
}
