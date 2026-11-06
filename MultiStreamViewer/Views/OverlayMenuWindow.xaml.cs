using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

using MultiStreamViewer.Models;

namespace MultiStreamViewer.Views;

public partial class OverlayMenuWindow : Window
{
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

    // 접힌 상태에서 토글 주변 몇 DIP 안으로 들어오면 나타날지
    private const double ShowZoneRadius = ToggleSize / 2 + 20;

    // 펼친 상태에서 버튼 바깥으로 이만큼 벗어나면 사라진다
    private const double ExpandedZoneRadius = Radius + FeatureSize / 2 + 12;

    // 커서가 벗어난 뒤 이 횟수(×80ms)만큼 연속으로 밖에 있어야 사라진다 (깜빡임 방지)
    private const int HideDelayTicks = 5;

    // 버튼 순서는 OverlayFeature 열거형(위, 오른쪽, 아래, 왼쪽)과 일치한다.
    private static readonly (string Icon, string Tip)[] DefaultFeatureLabels =
    {
        ("★", "즐겨찾기"),        // OverlayFeature.Favorites (위)
        ("↕", "전체화면 정렬"),   // OverlayFeature.Align (오른쪽)
        ("⇄", "슬롯 위치 바꾸기"), // OverlayFeature.Swap (아래)
        ("◫", "분할 화면 모양")   // OverlayFeature.Shape (왼쪽)
    };

    private readonly List<Button> featureButtons = new();

    private readonly DispatcherTimer hoverTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(80)
    };

    private bool isExpanded;
    private bool isShown;
    private bool isPinned;
    private int outsideTicks;

    public event Action<OverlayFeature>? FeatureClicked;

    public ReadOnlyCollection<Button> FeatureButtons => featureButtons.AsReadOnly();

    public OverlayMenuWindow()
    {
        InitializeComponent();

        Width = WindowSize;
        Height = WindowSize;

        BuildButtons();
        StartHoverWatch();

        Closed += (_, _) => hoverTimer.Stop();
    }

    public void SetFeatureLabel(OverlayFeature feature, string icon, string tip)
    {
        int index = (int)feature;
        if (index < 0 || index >= featureButtons.Count)
            return;

        featureButtons[index].Content = icon;
        featureButtons[index].ToolTip = tip;
    }

    public void ResetFeatureLabel(OverlayFeature feature)
    {
        int index = (int)feature;
        if (index < 0 || index >= DefaultFeatureLabels.Length)
            return;

        SetFeatureLabel(feature, DefaultFeatureLabels[index].Icon, DefaultFeatureLabels[index].Tip);
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        WindowNative.SetNoActivate(new WindowInteropHelper(this).Handle);
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

        WindowNative.SetBounds(hwnd, new Rect(x, y, width, height));
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
                Content = i < DefaultFeatureLabels.Length ? DefaultFeatureLabels[i].Icon : null,
                ToolTip = i < DefaultFeatureLabels.Length ? DefaultFeatureLabels[i].Tip : null,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                Visibility = Visibility.Collapsed,
                RenderTransform = new TranslateTransform(-dx, -dy),
                Tag = new Point(dx, dy)
            };

            OverlayFeature feature = (OverlayFeature)i;
            button.Click += (_, _) => FeatureClicked?.Invoke(feature);

            Canvas.SetLeft(button, center + dx - FeatureSize / 2);
            Canvas.SetTop(button, center + dy - FeatureSize / 2);

            Root.Children.Add(button);
            featureButtons.Add(button);
        }

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

    private static double AngleOf(int index)
    {
        double degrees = FeatureCount switch
        {
            4 => -90 + 90 * index,
            6 => 60 * index,
            _ => -90 + 360.0 / FeatureCount * index
        };

        return degrees * Math.PI / 180;
    }

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
            !WindowNative.TryGetCursor(out Point cursor) ||
            !WindowNative.TryGetWindowBounds(hwnd, out Rect windowBounds))
        {
            return;
        }

        DpiScale dpi = VisualTreeHelper.GetDpi(this);

        double centerX = windowBounds.Left + windowBounds.Width / 2.0;
        double centerY = windowBounds.Top + windowBounds.Height / 2.0;

        double distance = Math.Sqrt(
            Math.Pow(cursor.X - centerX, 2) +
            Math.Pow(cursor.Y - centerY, 2));

        double zone = (isExpanded ? ExpandedZoneRadius : ShowZoneRadius) * dpi.DpiScaleX;

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

                Animate(transform, TranslateTransform.XProperty, -offset.X, 0, null);
                Animate(transform, TranslateTransform.YProperty, -offset.Y, 0, null);
            }
            else
            {
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
}
