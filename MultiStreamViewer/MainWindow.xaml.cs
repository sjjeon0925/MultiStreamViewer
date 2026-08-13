using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using MultiStreamViewer.Views;

namespace MultiStreamViewer;

public partial class MainWindow : Window
{
    private int currentLayout = 4;

    // 프로그램 전체에서 항상 유지되는 4개의 방송 슬롯
    private readonly List<StreamView> streamViews = new();

    // 전체화면 모드 여부 (분할 레이아웃은 그대로 두고 창 테두리/툴바/상태바만 감춘다)
    private bool isFullscreen;

    // 전체화면 모드 진입 전 창 상태(해제 시 복원용)
    private WindowStyle savedWindowStyle;
    private ResizeMode savedResizeMode;
    private WindowState savedWindowState;
    private double savedLeft;
    private double savedTop;
    private double savedWidth;
    private double savedHeight;
    private GridLength savedTopBarHeight;
    private GridLength savedBottomStatusBarHeight;


    public MainWindow()
    {
        InitializeComponent();

        savedTopBarHeight = TopBarRow.Height;
        savedBottomStatusBarHeight = BottomStatusBarRow.Height;

        CreateStreamViews();

        SetLayout(4);

        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }


    // =========================================================
    // 작업 표시줄까지 덮는 진짜 전체화면
    //
    // WindowState.Maximized는 모니터마다 다른 DPI 배율(예: 1번 모니터
    // 100%, 2번 모니터 125%)에서 Windows의 WM_GETMINMAXINFO 좌표 처리가
    // 모니터별로 다르게 동작해 창이 화면 밖으로 밀려나는 문제가 있었다.
    // 대신 창을 Normal 상태로 두고, 모니터의 실제 픽셀 영역(작업 표시줄
    // 포함)을 이 창이 그리는 DPI 기준으로 직접 변환해 Left/Top/Width/
    // Height에 넣는다. 이렇게 하면 모니터별 DPI가 달라도 항상 그 모니터의
    // 물리적인 화면 전체를 정확히 덮는다.
    // =========================================================

    private void ApplyFullMonitorBounds()
    {
        IntPtr hwnd =
            new WindowInteropHelper(this).Handle;

        IntPtr monitor =
            MonitorFromWindow(
                hwnd,
                MONITOR_DEFAULTTONEAREST);

        if (monitor == IntPtr.Zero)
            return;

        var monitorInfo = new MONITORINFO
        {
            cbSize = Marshal.SizeOf<MONITORINFO>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return;

        RECT monitorRect = monitorInfo.rcMonitor;

        Matrix transform =
            PresentationSource.FromVisual(this) is HwndSource hwndSource
                ? hwndSource.CompositionTarget.TransformFromDevice
                : Matrix.Identity;

        Point topLeft =
            transform.Transform(
                new Point(monitorRect.Left, monitorRect.Top));

        Point bottomRight =
            transform.Transform(
                new Point(monitorRect.Right, monitorRect.Bottom));

        Left = topLeft.X;
        Top = topLeft.Y;
        Width = bottomRight.X - topLeft.X;
        Height = bottomRight.Y - topLeft.Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }


    // =========================================================
    // Esc로 전체화면 모드 해제
    // =========================================================

    private void MainWindow_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Escape &&
            isFullscreen)
        {
            ExitFullscreenMode();

            e.Handled = true;
        }
    }


    // =========================================================
    // 전체화면 버튼
    // =========================================================

    private void FullscreenButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        EnterFullscreenMode();
    }


    // =========================================================
    // 전체화면 모드 (분할된 모습 그대로, 툴바/상태바/창 테두리만 제거)
    // =========================================================

    private void EnterFullscreenMode()
    {
        if (isFullscreen)
            return;

        isFullscreen = true;

        savedWindowStyle = WindowStyle;
        savedResizeMode = ResizeMode;
        savedWindowState = WindowState;
        savedLeft = Left;
        savedTop = Top;
        savedWidth = Width;
        savedHeight = Height;

        // Visibility만 Collapsed로 바꾸면 고정 Height(96/32)로 예약된
        // 행 공간이 그대로 남아 분할된 웹뷰가 화면을 다 채우지 못한다.
        // 행 높이 자체를 0으로 줄여야 방송 영역(가운데 행)이 그 공간까지 차지한다.
        TopBarRow.Height = new GridLength(0);

        BottomStatusBarRow.Height = new GridLength(0);

        StreamGrid.Margin =
            new Thickness(0);

        WindowStyle = WindowStyle.None;

        ResizeMode = ResizeMode.NoResize;

        WindowState = WindowState.Normal;

        ApplyFullMonitorBounds();
    }


    private void ExitFullscreenMode()
    {
        if (!isFullscreen)
            return;

        isFullscreen = false;

        TopBarRow.Height = savedTopBarHeight;

        BottomStatusBarRow.Height = savedBottomStatusBarHeight;

        StreamGrid.Margin =
            new Thickness(3);

        WindowStyle = savedWindowStyle;

        ResizeMode = savedResizeMode;

        Left = savedLeft;

        Top = savedTop;

        Width = savedWidth;

        Height = savedHeight;

        WindowState = savedWindowState;
    }


    // =========================================================
    // 4개의 StreamView를 프로그램 시작 시 한 번만 생성
    // =========================================================

    private void CreateStreamViews()
    {
        streamViews.Clear();

        for (int i = 0; i < 4; i++)
        {
            var streamView = new StreamView(i);

            streamViews.Add(streamView);

            StreamGrid.Children.Add(streamView);
        }
    }


    // =========================================================
    // 분할 버튼
    // =========================================================

    private void LayoutButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button button &&
            int.TryParse(
                button.Tag?.ToString(),
                out int layout))
        {
            SetLayout(layout);
        }
    }


    // =========================================================
    // 레이아웃 변경
    // =========================================================

    private void SetLayout(int layout)
    {
        currentLayout = layout;

        StreamGrid.RowDefinitions.Clear();

        StreamGrid.ColumnDefinitions.Clear();


        switch (layout)
        {
            case 1:
                Set1Layout();
                break;

            case 2:
                Set2Layout();
                break;

            case 3:
                Set3Layout();
                break;

            case 4:
                Set4Layout();
                break;
        }


        StatusText.Text =
            $"{layout}분할 모드";
    }


    // =========================================================
    // 1분할
    // =========================================================

    private void Set1Layout()
    {
        StreamGrid.RowDefinitions.Add(
            new RowDefinition());

        StreamGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        SetStreamView(
            0,
            0,
            0);

        HideUnusedStreams(0);
    }


    // =========================================================
    // 2분할
    // =========================================================

    private void Set2Layout()
    {
        StreamGrid.RowDefinitions.Add(
            new RowDefinition());

        StreamGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        StreamGrid.ColumnDefinitions.Add(
            new ColumnDefinition());


        SetStreamView(
            0,
            0,
            0);

        SetStreamView(
            1,
            0,
            1);


        HideUnusedStreams(
            0,
            1);
    }


    // =========================================================
    // 3분할
    //
    // ┌──────────────┐
    // │      1       │
    // ├───────┬──────┤
    // │   2   │  3   │
    // └───────┴──────┘
    // =========================================================

    private void Set3Layout()
    {
        StreamGrid.RowDefinitions.Add(
            new RowDefinition());

        StreamGrid.RowDefinitions.Add(
            new RowDefinition());


        StreamGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        StreamGrid.ColumnDefinitions.Add(
            new ColumnDefinition());


        // 슬롯 1: 위 전체
        SetStreamView(
            0,
            0,
            0,
            1,
            2);


        // 슬롯 2: 아래 왼쪽
        SetStreamView(
            1,
            1,
            0);


        // 슬롯 3: 아래 오른쪽
        SetStreamView(
            2,
            1,
            1);


        HideUnusedStreams(
            0,
            1,
            2);
    }


    // =========================================================
    // 4분할
    //
    // ┌───────┬───────┐
    // │   1   │   2   │
    // ├───────┼───────┤
    // │   3   │   4   │
    // └───────┴───────┘
    // =========================================================

    private void Set4Layout()
    {
        StreamGrid.RowDefinitions.Add(
            new RowDefinition());

        StreamGrid.RowDefinitions.Add(
            new RowDefinition());


        StreamGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        StreamGrid.ColumnDefinitions.Add(
            new ColumnDefinition());


        SetStreamView(
            0,
            0,
            0);

        SetStreamView(
            1,
            0,
            1);

        SetStreamView(
            2,
            1,
            0);

        SetStreamView(
            3,
            1,
            1);


        HideUnusedStreams(
            0,
            1,
            2,
            3);
    }


    // =========================================================
    // 기존 StreamView를 Grid에 다시 배치
    // =========================================================

    private void SetStreamView(
    int streamIndex,
    int row,
    int column,
    int rowSpan = 1,
    int columnSpan = 1)
    {
        StreamView streamView =
            streamViews[streamIndex];

        streamView.Visibility =
            Visibility.Visible;

        Grid.SetRow(
            streamView,
            row);

        Grid.SetColumn(
            streamView,
            column);

        Grid.SetRowSpan(
            streamView,
            rowSpan);

        Grid.SetColumnSpan(
            streamView,
            columnSpan);
    }


    // =========================================================
    // 사용하지 않는 StreamView 숨김
    // =========================================================

    private void HideUnusedStreams(
        params int[] visibleIndexes)
    {
        for (int i = 0; i < streamViews.Count; i++)
        {
            bool visible =
                Array.IndexOf(
                    visibleIndexes,
                    i) >= 0;

            streamViews[i].Visibility =
                visible
                    ? Visibility.Visible
                    : Visibility.Hidden;
        }
    }


    // =========================================================
    // 방송 연결
    // =========================================================

    private async void ConnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string url =
            UrlTextBox.Text.Trim();


        if (string.IsNullOrWhiteSpace(url))
        {
            StatusText.Text =
                "방송 URL을 입력하세요.";

            return;
        }


        int slotIndex =
            SlotSelector.SelectedIndex;


        if (slotIndex < 0 ||
            slotIndex >= streamViews.Count)
        {
            StatusText.Text =
                "슬롯을 선택하세요.";

            return;
        }


        StreamView streamView =
            streamViews[slotIndex];


        await streamView.NavigateAsync(
            url);


        if (streamView.Info.IsConnected)
        {
            StatusText.Text =
                $"슬롯 {slotIndex + 1} 연결: " +
                $"{streamView.Info.Platform}";
        }
        else
        {
            StatusText.Text =
                $"슬롯 {slotIndex + 1} 연결 실패";
        }
    }


    // =========================================================
    // 방송 해제
    // =========================================================

    private void DisconnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        int slotIndex =
            SlotSelector.SelectedIndex;


        if (slotIndex < 0 ||
            slotIndex >= streamViews.Count)
        {
            StatusText.Text =
                "슬롯을 선택하세요.";

            return;
        }


        streamViews[slotIndex].Stop();


        StatusText.Text =
            $"슬롯 {slotIndex + 1} 연결 해제";
    }
}