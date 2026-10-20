using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MultiStreamViewer.Views;

public partial class OverlayMenuWindow : Window
{
    // 기능 버튼 개수 (최대 6개까지 늘려도 원형 배치가 유지된다)
    private const int FeatureCount = 4;

    private const double ToggleSize = 44;
    private const double FeatureSize = 38;

    // 토글 중심에서 기능 버튼 중심까지의 거리
    private const double Radius = 62;

    // 창 가장자리 여백 (버튼이 잘리지 않도록)
    private const double Pad = 4;

    // 오른쪽 가장자리에서 띄울 간격 (1분할 배치용, DIP)
    private const double EdgeMargin = 8;

    private const double WindowSize = 2 * (Radius + FeatureSize / 2 + Pad);

    // 버튼 순서는 AngleOf 기준(4개: 위, 오른쪽, 아래, 왼쪽)이며 아이콘과 툴팁만 먼저 붙여둔 상태다.
    private static readonly (string Icon, string Tip)[] FeatureLabels =
    {
        ("★", "즐겨찾기"),            // 위     - 4번
        ("↕", "전체화면 정렬"),        // 오른쪽 - 1번 (위/가운데/아래)
        ("⇄", "슬롯 위치 바꾸기"),     // 아래   - 2번
        ("◫", "분할 화면 모양")        // 왼쪽   - 5번
    };

    private readonly List<Button> featureButtons = new();

    private bool isExpanded;

    // 나중에 각 기능을 연결할 때 사용하는 버튼 목록 (배치 순서는 AngleOf 참고)
    // 기능 버튼을 눌렀을 때 (인덱스는 FeatureButtons 순서)
    public event Action<int>? FeatureClicked;

    // 버튼 모양(아이콘/툴팁)을 상태에 맞게 바꾼다.
    public void SetFeatureLabel(int index, string icon, string tip)
    {
        featureButtons[index].Content = icon;
        featureButtons[index].ToolTip = tip;
    }

    // 기능 버튼 원래 아이콘/툴팁으로 되돌린다.
    public void ResetFeatureLabel(int index)
    {
        SetFeatureLabel(index, FeatureLabels[index].Icon, FeatureLabels[index].Tip);
    }

    // true면 메뉴를 펼친 채 고정해서 보여주고, false면 다시 마우스 위치에 따라 숨는다.
    public void SetPinned(bool pinned)
    {
        isPinned = pinned;

        if (pinned)
        {
            SetShown(true);
            SetExpanded(true);
        }
    }

    // 기능 버튼을 접는다 (가운데 토글만 남는다).
    public void Collapse()
    {
        SetExpanded(false);
    }

    public void BringToTop()
    {
        WindowNative.BringToTop(new WindowInteropHelper(this).Handle);
    }

    // 소유자를 다른 창으로 바꿔, 그 창보다 항상 위에 그려지게 한다.
    public void SetOwnerWindow(Window owner)
    {
        WindowNative.SetOwner(
            new WindowInteropHelper(this).Handle,
            new WindowInteropHelper(owner).Handle);
    }

    public ReadOnlyCollection<Button> FeatureButtons => featureButtons.AsReadOnly();

    public OverlayMenuWindow()
    {
        InitializeComponent();

        Width = WindowSize;
        Height = WindowSize;

        BuildButtons();

        StartHoverWatch();
    }

    private void BuildButtons()
    {
        double center = WindowSize / 2;

        var style = (Style)FindResource("OverlayButtonStyle");

        for (int i = 0; i < FeatureCount; i++)
        {
            double angle = AngleOf(i);

            double dx = Radius * Math.Cos(angle);
            double dy = Radius * Math.Sin(angle);

            var button = new Button
            {
                Width = FeatureSize,
                Height = FeatureSize,
                Style = style,
                Content = i < FeatureLabels.Length ? FeatureLabels[i].Icon : null,
                ToolTip = i < FeatureLabels.Length ? FeatureLabels[i].Tip : null,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                Visibility = Visibility.Collapsed,
                // 접힌 상태에서는 토글 위치(중심)에서 출발한다.
                RenderTransform = new TranslateTransform(-dx, -dy),
                Tag = new Point(dx, dy)
            };

            int index = i;

            button.Click += (_, _) => FeatureClicked?.Invoke(index);

            Canvas.SetLeft(button, center + dx - FeatureSize / 2);
            Canvas.SetTop(button, center + dy - FeatureSize / 2);

            Root.Children.Add(button);
            featureButtons.Add(button);
        }

        // 토글은 나중에 추가해 기능 버튼보다 위에 그려진다.
        var toggle = new Button
        {
            Width = ToggleSize,
            Height = ToggleSize,
            Content = "☰",
            ToolTip = "메뉴",
            Style = style
        };

        toggle.Click += ToggleButton_Click;

        Canvas.SetLeft(toggle, center - ToggleSize / 2);
        Canvas.SetTop(toggle, center - ToggleSize / 2);

        Root.Children.Add(toggle);
    }

    // 4개: 위/오른쪽/아래/왼쪽 (12시부터 시계 방향)
    // 6개: 오른쪽에서 시작해 60°씩 시계 방향 (좌우 1개씩, 나머지는 위아래로 2개씩)
    // 그 외 개수: 위쪽(12시)에서 시계 방향으로 균등 배치
    private static double AngleOf(int index)
    {
        double degrees =
            FeatureCount switch
            {
                4 => -90 + 90 * index,
                6 => 60 * index,
                _ => -90 + 360.0 / FeatureCount * index
            };

        return degrees * Math.PI / 180;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // 클릭해도 활성화되지 않게 해서, 메인 창이 포커스를 유지하도록 한다 (Esc 전체화면 해제 유지).
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        SetWindowLong(
            hwnd,
            GWL_EXSTYLE,
            exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    // streamAreaPx: 방송 영역의 화면 좌표(물리 픽셀)
    // 1분할은 오른쪽 중앙, 2~4분할은 정중앙에 둔다.
    public void Reposition(Rect streamAreaPx, int layout)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        if (hwnd == IntPtr.Zero)
            return;

        DpiScale dpi = VisualTreeHelper.GetDpi(this);

        double width = Width * dpi.DpiScaleX;
        double height = Height * dpi.DpiScaleY;

        double x = layout == 1
            ? streamAreaPx.Right - width - EdgeMargin * dpi.DpiScaleX
            : streamAreaPx.Left + (streamAreaPx.Width - width) / 2;

        double y = streamAreaPx.Top + (streamAreaPx.Height - height) / 2;

        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            (int)Math.Round(x),
            (int)Math.Round(y),
            (int)Math.Round(width),
            (int)Math.Round(height),
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    // =========================================================
    // 마우스가 메뉴 근처에 있을 때만 보이게 한다.
    //
    // 안 보이는 동안(투명)에는 이 창이 마우스 이벤트를 받지 못하므로
    // 커서 위치를 주기적으로 직접 확인한다.
    // =========================================================

    // 접힌 상태에서 토글 주변 몇 DIP 안으로 들어오면 나타날지
    private const double ShowZoneRadius = ToggleSize / 2 + 20;

    // 펼친 상태에서 버튼 바깥으로 이만큼 벗어나면 사라진다
    private const double ExpandedZoneRadius = Radius + FeatureSize / 2 + 12;

    // 커서가 벗어난 뒤 이 횟수(×80ms)만큼 연속으로 밖에 있어야 사라진다 (깜빡임 방지)
    private const int HideDelayTicks = 5;

    private readonly System.Windows.Threading.DispatcherTimer hoverTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(80)
    };

    private bool isShown;

    // 슬롯 선택 같은 모드 중에는 마우스가 멀어져도 메뉴를 펼친 채로 계속 보여준다.
    private bool isPinned;

    private int outsideTicks;

    private void StartHoverWatch()
    {
        Root.Opacity = 0;
        Root.IsHitTestVisible = false;

        hoverTimer.Tick += (_, _) => UpdateHoverState();
        hoverTimer.Start();
    }

    private void UpdateHoverState()
    {
        if (isPinned)
        {
            outsideTicks = 0;

            SetShown(true);

            return;
        }

        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        if (hwnd == IntPtr.Zero ||
            !GetCursorPos(out POINT cursor) ||
            !GetWindowRect(hwnd, out RECT rect))
        {
            return;
        }

        DpiScale dpi = VisualTreeHelper.GetDpi(this);

        double centerX = (rect.Left + rect.Right) / 2.0;
        double centerY = (rect.Top + rect.Bottom) / 2.0;

        double distance =
            Math.Sqrt(
                Math.Pow(cursor.X - centerX, 2) +
                Math.Pow(cursor.Y - centerY, 2));

        double zone =
            (isExpanded ? ExpandedZoneRadius : ShowZoneRadius) * dpi.DpiScaleX;

        if (distance <= zone)
        {
            outsideTicks = 0;

            SetShown(true);
        }
        else if (isShown && ++outsideTicks >= HideDelayTicks)
        {
            if (isExpanded)
                SetExpanded(false);

            SetShown(false);
        }
    }

    private void SetShown(bool shown)
    {
        if (isShown == shown)
            return;

        isShown = shown;

        Root.IsHitTestVisible = shown;

        Root.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(shown ? 1 : 0, TimeSpan.FromMilliseconds(120)));
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SetExpanded(!isExpanded);
    }

    private void SetExpanded(bool expanded)
    {
        if (isExpanded == expanded)
            return;

        isExpanded = expanded;

        foreach (Button button in featureButtons)
        {
            var offset = (Point)button.Tag;
            var transform = (TranslateTransform)button.RenderTransform;

            if (isExpanded)
            {
                button.Visibility = Visibility.Visible;

                // 토글 위치에서 제자리로 튀어나온다.
                Animate(transform, TranslateTransform.XProperty, -offset.X, 0, null);
                Animate(transform, TranslateTransform.YProperty, -offset.Y, 0, null);
            }
            else
            {
                // 토글 위치로 빨려 들어간 뒤 숨긴다.
                Animate(transform, TranslateTransform.XProperty, 0, -offset.X, null);
                Animate(
                    transform,
                    TranslateTransform.YProperty,
                    0,
                    -offset.Y,
                    () =>
                    {
                        if (!isExpanded)
                            button.Visibility = Visibility.Collapsed;
                    });
            }
        }
    }

    private static void Animate(
        TranslateTransform transform,
        DependencyProperty property,
        double from,
        double to,
        Action? completed)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        if (completed != null)
            animation.Completed += (_, _) => completed();

        transform.BeginAnimation(property, animation);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

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

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
