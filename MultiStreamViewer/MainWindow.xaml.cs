using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using MultiStreamViewer.Models;
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

        Loaded += (_, _) => CreateOverlayMenu();

        LocationChanged += (_, _) => RepositionOverlayMenu();
        SizeChanged += (_, _) =>
        {
            // 크기 변경 직후에는 아직 배치 전이라 한 박자 늦춰 적용한다.
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                ApplyStreamAlignment);
        };
    }


    // =========================================================
    // 방송 영역 위에 뜨는 반투명 메뉴 (분할 버튼 토글)
    // =========================================================

    private OverlayMenuWindow? overlayMenu;

    // 전체화면에서 화면 맨 위로 마우스를 가져가면 나타나는 종료(✕) 버튼
    private FullscreenExitWindow? fullscreenExit;

    // =========================================================
    // 분할 화면 모양 (2분할 / 3분할만 해당)
    // =========================================================

    private const int FeatureShape = 3;

    // 2분할: 0 가로로 나란히, 1 세로로 쌓기
    private int layout2Shape;

    // 3분할: 0 상1하2, 1 상2하1, 2 좌1우2, 3 좌2우1
    private int layout3Shape;

    private static readonly (string Icon, string Name)[] Layout2Shapes =
    {
        ("◫", "가로 2분할"),
        ("⊟", "세로 2분할")
    };

    private static readonly (string Icon, string Name)[] Layout3Shapes =
    {
        ("⬒", "위 1 · 아래 2"),
        ("⬓", "위 2 · 아래 1"),
        ("◧", "왼쪽 1 · 오른쪽 2"),
        ("◨", "왼쪽 2 · 오른쪽 1")
    };

    // 지금 분할에서 고를 수 있는 모양 목록과 현재 번호 (모양이 하나뿐이면 null)
    private ((string Icon, string Name)[] Shapes, int Current)? CurrentShapes() =>
        currentLayout switch
        {
            2 => (Layout2Shapes, layout2Shape),
            3 => (Layout3Shapes, layout3Shape),
            _ => null
        };

    // 버튼에는 "누르면 바뀔 다음 모양"을 보여준다.
    private void UpdateShapeButton()
    {
        if (CurrentShapes() is not var (shapes, current))
        {
            overlayMenu?.SetFeatureLabel(
                FeatureShape,
                "◫",
                "분할 모양 (2·3분할에서 바꿀 수 있어요)");

            return;
        }

        var next = shapes[(current + 1) % shapes.Length];

        overlayMenu?.SetFeatureLabel(
            FeatureShape,
            next.Icon,
            $"{next.Name} (현재: {shapes[current].Name})");
    }

    private void CycleShape()
    {
        if (CurrentShapes() is not var (shapes, current))
        {
            StatusText.Text = "분할 모양은 2분할, 3분할에서만 바꿀 수 있습니다.";

            return;
        }

        int next = (current + 1) % shapes.Length;

        if (currentLayout == 2)
            layout2Shape = next;
        else
            layout3Shape = next;

        SetLayout(currentLayout);

        StatusText.Text = $"분할 모양: {shapes[next].Name}";
    }

    // 오버레이 기능 버튼 인덱스 (OverlayMenuWindow의 배치 순서와 같다)
    private const int FeatureAlign = 1;

    // =========================================================
    // 전체화면 정렬 (16:10 같은 모니터에서 생기는 위/아래 공백 처리)
    //
    // 방송 화면은 16:9라서 모니터가 그보다 위아래로 길면 공백이 생긴다.
    // 슬롯들이 이루는 영역을 16:9에 맞춰 줄이고, 남는 공백을
    // 한쪽(위/아래)으로 몰거나 양쪽에 나눠 배치한다. 전체화면에서만 적용된다.
    // =========================================================

    private enum StreamAlign
    {
        Fill,    // 화면 전체를 채움 (기본)
        Top,     // 위에 붙임 (공백은 아래)
        Center,  // 가운데 (공백은 위아래로 나눔)
        Bottom   // 아래에 붙임 (공백은 위)
    }

    private StreamAlign streamAlign = StreamAlign.Fill;

    private static string AlignName(StreamAlign align) =>
        align switch
        {
            StreamAlign.Top => "위에 붙이기",
            StreamAlign.Center => "가운데",
            StreamAlign.Bottom => "아래에 붙이기",
            _ => "꽉 채우기"
        };

    private static string AlignIcon(StreamAlign align) =>
        align switch
        {
            StreamAlign.Top => "⤒",
            StreamAlign.Center => "↕",
            StreamAlign.Bottom => "⤓",
            _ => "⛶"
        };

    private static StreamAlign NextAlign(StreamAlign align) =>
        (StreamAlign)(((int)align + 1) % Enum.GetValues<StreamAlign>().Length);

    private void OverlayFeatureClicked(int index)
    {
        // 슬롯 선택 중에는 선택을 시작한 버튼(취소 버튼)만 동작한다.
        if (isPicking)
        {
            if (index == pickFeature)
                CancelPickMode();

            return;
        }

        switch (index)
        {
            case FeatureFavorites:
                OpenFavoritesWindow();
                break;

            case FeatureAlign:
                streamAlign = NextAlign(streamAlign);

                UpdateAlignButton();

                ApplyStreamAlignment();

                StatusText.Text = $"전체화면 정렬: {AlignName(streamAlign)}";
                break;

            case FeatureShape:
                CycleShape();
                break;

            case FeatureSwap:
                StartPickMode(
                    PickMode.Swap,
                    FeatureSwap,
                    "바꿀 첫 번째 슬롯을 선택하세요.");
                break;
        }
    }

    // 버튼에는 지금 상태가 아니라 "누르면 바뀔 다음 상태"를 보여준다.
    private void UpdateAlignButton()
    {
        StreamAlign next = NextAlign(streamAlign);

        overlayMenu?.SetFeatureLabel(
            FeatureAlign,
            AlignIcon(next),
            $"{AlignName(next)} (현재: {AlignName(streamAlign)})");
    }

    // 슬롯 칸 하나는 16:9로 본다. 2분할은 가로로 나란히면 전체가 32:9, 세로로 쌓으면 16:18이다.
    private double ContentAspect(int layout) =>
        layout == 2
            ? (layout2Shape == 1 ? 16.0 / 18.0 : 32.0 / 9.0)
            : 16.0 / 9.0;

    private void ApplyStreamAlignment()
    {
        if (!isFullscreen || streamAlign == StreamAlign.Fill)
        {
            StreamGrid.ClearValue(WidthProperty);
            StreamGrid.ClearValue(HeightProperty);
            StreamGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            StreamGrid.VerticalAlignment = VerticalAlignment.Stretch;

            RepositionOverlayMenu();

            return;
        }

        // 전체화면에서는 위/아래 줄이 높이 0이라 창 전체가 방송 영역 가능 크기다.
        double availableWidth = RootGrid.ActualWidth;
        double availableHeight = RootGrid.ActualHeight;

        if (availableWidth <= 0 || availableHeight <= 0)
            return;

        double aspect = ContentAspect(currentLayout);

        double width = availableWidth;
        double height = width / aspect;

        if (height > availableHeight)
        {
            // 모니터가 이 비율보다 가로로 길면 좌우가 남는다. 이때는 가운데로 둔다.
            height = availableHeight;
            width = height * aspect;
        }

        StreamGrid.Width = width;
        StreamGrid.Height = height;

        StreamGrid.HorizontalAlignment = HorizontalAlignment.Center;

        StreamGrid.VerticalAlignment =
            streamAlign switch
            {
                StreamAlign.Top => VerticalAlignment.Top,
                StreamAlign.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Center
            };

        RepositionOverlayMenu();
    }

    private void CreateOverlayMenu()
    {
        overlayMenu = new OverlayMenuWindow
        {
            Owner = this
        };


        fullscreenExit = new FullscreenExitWindow(this);

        fullscreenExit.ExitRequested += ExitFullscreenMode;

        overlayMenu.FeatureClicked += OverlayFeatureClicked;

        UpdateAlignButton();

        UpdateShapeButton();

        overlayMenu.Show();

        RepositionOverlayMenu();
    }

    // 레이아웃이 다시 배치된 뒤의 방송 영역 좌표가 필요하므로 한 박자 늦춰 실행한다.
    private void RepositionOverlayMenu()
    {
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            () =>
            {
                if (overlayMenu == null || !IsLoaded)
                    return;

                overlayMenu.Reposition(GetStreamAreaPx(), currentLayout);

                UpdateSlotPicker();
            });
    }

    // 방송 영역의 화면 좌표(물리 픽셀)
    private Rect GetStreamAreaPx()
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);

        Point topLeft = StreamGrid.PointToScreen(new Point(0, 0));

        return new Rect(
            topLeft.X,
            topLeft.Y,
            StreamGrid.ActualWidth * dpi.DpiScaleX,
            StreamGrid.ActualHeight * dpi.DpiScaleY);
    }


    // =========================================================
    // 슬롯 위치 바꾸기
    //
    // 오버레이 메뉴의 버튼을 누르면 선택 모드가 되고(버튼은 취소로 바뀜),
    // 보이는 슬롯 중 두 개를 차례로 클릭하면 서로 자리를 바꾼다.
    // WebView2를 다른 부모로 옮기면 페이지가 다시 로드되므로, 화면 요소는 그대로 두고
    // streamViews 목록의 순서만 바꾼 뒤 레이아웃을 다시 적용한다.
    // =========================================================

    private const int FeatureSwap = 2;

    private SlotPickerWindow? slotPicker;

    // 슬롯을 클릭해서 고르는 모드 (위치 바꾸기, 즐겨찾기 열기가 같이 쓴다)
    private enum PickMode
    {
        None,
        Swap,
        OpenFavorite
    }

    private PickMode pickMode = PickMode.None;

    private bool isPicking => pickMode != PickMode.None;

    // 선택 모드를 시작한 기능 버튼 (이 버튼이 취소 버튼으로 바뀐다)
    private int pickFeature;

    private int? swapFirstSlot;

    // 즐겨찾기 열기 모드에서 열 주소
    private Favorite? pendingFavorite;

    private void StartPickMode(
        PickMode mode,
        int feature,
        string hint)
    {
        if (isPicking)
            return;

        pickMode = mode;
        pickFeature = feature;
        swapFirstSlot = null;

        if (slotPicker == null)
        {
            slotPicker = new SlotPickerWindow
            {
                Owner = this
            };

            slotPicker.SlotChosen += OnPickSlotChosen;
        }

        slotPicker.SetSelected(null);

        slotPicker.Show();

        // 선택 창은 방송 영역 전체를 덮으므로, 취소 버튼이 있는 메뉴가 그 위에 그려지도록
        // 메뉴의 소유자를 선택 창으로 바꾼다 (소유된 창은 소유자 위에 그려진다).
        overlayMenu?.SetOwnerWindow(slotPicker);

        overlayMenu?.SetPinned(true);

        overlayMenu?.SetFeatureLabel(feature, "✕", "취소 (Esc)");

        StatusText.Text = hint;

        RepositionOverlayMenu();

        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            () => overlayMenu?.BringToTop());
    }

    private void EndPickMode()
    {
        if (!isPicking)
            return;

        int feature = pickFeature;

        pickMode = PickMode.None;
        swapFirstSlot = null;
        pendingFavorite = null;

        // 소유자인 선택 창을 숨기면 메뉴도 같이 숨겨지므로, 먼저 소유자를 메인 창으로 되돌린다.
        overlayMenu?.SetOwnerWindow(this);

        slotPicker?.Hide();

        overlayMenu?.ResetFeatureLabel(feature);

        // 즐겨찾기 버튼처럼 상태에 따라 모양이 바뀌는 버튼은 다시 맞춘다.
        UpdateAlignButton();
        UpdateShapeButton();

        // 고정을 풀고 가운데 버튼만 남도록 닫는다.
        overlayMenu?.SetPinned(false);
        overlayMenu?.Collapse();
    }

    private void CancelPickMode()
    {
        EndPickMode();

        StatusText.Text = "선택을 취소했습니다.";
    }

    private async void OnPickSlotChosen(int slot)
    {
        switch (pickMode)
        {
            case PickMode.Swap:
                OnSwapSlotChosen(slot);
                break;

            case PickMode.OpenFavorite:
                await OpenFavoriteInSlotAsync(slot);
                break;
        }
    }

    private void OnSwapSlotChosen(int slot)
    {
        if (swapFirstSlot == null)
        {
            swapFirstSlot = slot;

            slotPicker?.SetSelected(slot);

            StatusText.Text = $"슬롯 {slot + 1} 선택됨. 바꿀 두 번째 슬롯을 선택하세요.";

            return;
        }

        int first = swapFirstSlot.Value;

        // 같은 슬롯을 다시 누르면 첫 번째 선택을 취소한다.
        if (first == slot)
        {
            swapFirstSlot = null;

            slotPicker?.SetSelected(null);

            StatusText.Text = "바꿀 첫 번째 슬롯을 선택하세요.";

            return;
        }

        (streamViews[first], streamViews[slot]) =
            (streamViews[slot], streamViews[first]);

        EndPickMode();

        for (int i = 0; i < streamViews.Count; i++)
            streamViews[i].SetSlotIndex(i);

        SetLayout(currentLayout);

        StatusText.Text = $"슬롯 {first + 1} ↔ 슬롯 {slot + 1} 위치를 바꿨습니다.";
    }

    private async Task OpenFavoriteInSlotAsync(int slot)
    {
        Favorite? favorite = pendingFavorite;

        EndPickMode();

        if (favorite == null)
            return;

        await streamViews[slot].NavigateAsync(favorite.Url);

        StatusText.Text = $"슬롯 {slot + 1} 연결: {favorite.Name}";
    }


    // =========================================================
    // 즐겨찾기
    //
    // 오버레이 메뉴의 즐겨찾기 버튼을 누르면 목록 창이 뜬다 (추가/삭제 가능).
    // 항목을 고르면 슬롯 선택 모드로 넘어가고, 슬롯을 클릭하면 그 슬롯에 열린다.
    // =========================================================

    private const int FeatureFavorites = 0;

    private void OpenFavoritesWindow()
    {
        var window = new FavoritesWindow(UrlTextBox.Text.Trim())
        {
            Owner = this
        };

        if (window.ShowDialog() != true ||
            window.Chosen == null)
        {
            // 창을 닫는 동안 메뉴를 접어 둔다.
            overlayMenu?.Collapse();

            return;
        }

        pendingFavorite = window.Chosen;

        StartPickMode(
            PickMode.OpenFavorite,
            FeatureFavorites,
            $"'{window.Chosen.Name}'을(를) 열 슬롯을 선택하세요.");

    }


    // 지금 보이는 슬롯들의 위치(방송 영역 기준 DIP)를 선택 창에 전달한다.
    private void UpdateSlotPicker()
    {
        if (!isPicking || slotPicker == null)
            return;

        var slots = new List<(int Index, Rect Bounds)>();

        for (int i = 0; i < streamViews.Count; i++)
        {
            StreamView view = streamViews[i];

            if (view.Visibility != Visibility.Visible)
                continue;

            Rect bounds =
                view.TransformToAncestor(StreamGrid)
                    .TransformBounds(new Rect(0, 0, view.ActualWidth, view.ActualHeight));

            slots.Add((i, bounds));
        }

        slotPicker.Update(GetStreamAreaPx(), slots);
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
            isPicking)
        {
            CancelPickMode();

            e.Handled = true;

            return;
        }

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

        fullscreenExit?.Begin();

        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            ApplyStreamAlignment);
    }


    private void ExitFullscreenMode()
    {
        if (!isFullscreen)
            return;

        isFullscreen = false;

        fullscreenExit?.End();

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

        ApplyStreamAlignment();
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

        UpdateShapeButton();

        ApplyStreamAlignment();

        RepositionOverlayMenu();
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
    // 2분할 (모양: 가로로 나란히 / 세로로 쌓기)
    // =========================================================

    private void Set2Layout()
    {
        bool vertical = layout2Shape == 1;

        StreamGrid.RowDefinitions.Add(
            new RowDefinition());

        StreamGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        if (vertical)
        {
            StreamGrid.RowDefinitions.Add(
                new RowDefinition());
        }
        else
        {
            StreamGrid.ColumnDefinitions.Add(
                new ColumnDefinition());
        }

        SetStreamView(
            0,
            0,
            0);

        SetStreamView(
            1,
            vertical ? 1 : 0,
            vertical ? 0 : 1);

        HideUnusedStreams(
            0,
            1);
    }


    // =========================================================
    // 3분할 (모양 4가지)
    //
    //  0: 상1하2        1: 상2하1        2: 좌1우2        3: 좌2우1
    // ┌─────────┐    ┌────┬────┐    ┌────┬────┐    ┌────┬────┐
    // │    1    │    │ 1  │ 2  │    │    │ 2  │    │ 1  │    │
    // ├────┬────┤    ├────┴────┤    │ 1  ├────┤    ├────┤ 3  │
    // │ 2  │ 3  │    │    3    │    │    │ 3  │    │ 2  │    │
    // └────┴────┘    └─────────┘    └────┴────┘    └────┴────┘
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


        switch (layout3Shape)
        {
            case 1:
                SetStreamView(0, 0, 0);
                SetStreamView(1, 0, 1);
                SetStreamView(2, 1, 0, 1, 2);
                break;

            case 2:
                SetStreamView(0, 0, 0, 2, 1);
                SetStreamView(1, 0, 1);
                SetStreamView(2, 1, 1);
                break;

            case 3:
                SetStreamView(0, 0, 0);
                SetStreamView(1, 1, 0);
                SetStreamView(2, 0, 1, 2, 1);
                break;

            default:
                SetStreamView(0, 0, 0, 1, 2);
                SetStreamView(1, 1, 0);
                SetStreamView(2, 1, 1);
                break;
        }


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
    // 링크 추출 (상단 슬롯 선택 + 주소창 UI를 재활용)
    //
    // 선택한 슬롯의 주소를 주소창에 채우고 클립보드에도 복사한다.
    // 슬롯 위치를 바꿨다면 지금 그 번호 자리에 있는 방송의 주소가 나온다.
    // =========================================================

    private void CurrentLinkButton_Click(object sender, RoutedEventArgs e)
    {
        ExtractLink(view => view.CurrentUrl, "현재");
    }

    private void InitialLinkButton_Click(object sender, RoutedEventArgs e)
    {
        ExtractLink(view => view.InitialUrl, "처음");
    }

    private void ExtractLink(Func<StreamView, string> selector, string kind)
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

        string url =
            selector(streamViews[slotIndex]);

        if (string.IsNullOrEmpty(url))
        {
            StatusText.Text =
                $"슬롯 {slotIndex + 1}에 연결된 방송이 없습니다.";

            return;
        }

        UrlTextBox.Text = url;

        try
        {
            Clipboard.SetText(url);

            StatusText.Text =
                $"슬롯 {slotIndex + 1} {kind} 링크를 복사했습니다.";
        }
        catch (Exception ex)
        {
            // 다른 프로그램이 클립보드를 잡고 있으면 실패할 수 있다. 주소창에는 채워졌다.
            System.Diagnostics.Debug.WriteLine(
                $"클립보드 복사 실패: {ex}");

            StatusText.Text =
                $"슬롯 {slotIndex + 1} {kind} 링크를 주소창에 채웠습니다 (복사 실패).";
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