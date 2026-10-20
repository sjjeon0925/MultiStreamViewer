using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MultiStreamViewer.Views;

public partial class FullscreenExitWindow : Window
{
    // 화면 맨 위에서 이 정도(DIP) 안으로 들어오면 나타난다
    private const double TriggerHeight = 6;

    // 나타난 뒤에는 버튼 주변 이 정도(DIP) 안에 있는 동안 유지된다
    private const double KeepMargin = 16;

    // 커서가 벗어난 뒤 이 횟수(×80ms) 연속으로 밖에 있어야 사라진다 (깜빡임 방지)
    private const int HideDelayTicks = 6;

    private readonly Window mainWindow;

    private readonly DispatcherTimer timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(80)
    };

    private bool isShown;

    private int outsideTicks;

    private Rect lastBounds;

    // 전체화면 종료 요청 (✕ 클릭)
    public event Action? ExitRequested;

    public FullscreenExitWindow(Window owner)
    {
        InitializeComponent();

        mainWindow = owner;

        Owner = owner;

        timer.Tick += (_, _) => UpdateState();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        WindowNative.SetNoActivate(new WindowInteropHelper(this).Handle);
    }

    // 전체화면에 들어갈 때 호출
    public void Begin()
    {
        SetShown(false, animate: false);

        Show();

        timer.Start();
    }

    // 전체화면에서 나올 때 호출
    public void End()
    {
        timer.Stop();

        SetShown(false, animate: false);

        Hide();
    }

    private void UpdateState()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        if (hwnd == IntPtr.Zero ||
            !WindowNative.TryGetCursor(out Point cursor) ||
            !WindowNative.TryGetWindowBounds(
                new WindowInteropHelper(mainWindow).Handle,
                out Rect owner))
        {
            return;
        }

        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;

        // 메인 창(전체화면 = 모니터) 맨 위 가운데에 붙인다.
        var bounds = new Rect(
            owner.Left + (owner.Width - Width * scale) / 2,
            owner.Top,
            Width * scale,
            Height * scale);

        if (bounds != lastBounds)
        {
            WindowNative.SetBounds(hwnd, bounds);

            lastBounds = bounds;
        }

        bool inOwnerColumn =
            cursor.X >= owner.Left &&
            cursor.X < owner.Right;

        bool nearTop =
            inOwnerColumn &&
            cursor.Y >= owner.Top &&
            cursor.Y <= owner.Top + TriggerHeight * scale;

        double keep = KeepMargin * scale;

        bool overButton =
            cursor.X >= bounds.Left - keep &&
            cursor.X <= bounds.Right + keep &&
            cursor.Y >= bounds.Top &&
            cursor.Y <= bounds.Bottom + keep;

        if (nearTop || (isShown && overButton))
        {
            outsideTicks = 0;

            SetShown(true, animate: true);
        }
        else if (isShown && ++outsideTicks >= HideDelayTicks)
        {
            SetShown(false, animate: true);
        }
    }

    private void SetShown(bool shown, bool animate)
    {
        if (isShown == shown && animate)
            return;

        isShown = shown;

        ExitButton.IsHitTestVisible = shown;

        double y = shown ? 0 : -Height;
        double opacity = shown ? 1 : 0;

        if (!animate)
        {
            Slide.BeginAnimation(TranslateTransform.YProperty, null);
            Pill.BeginAnimation(OpacityProperty, null);

            Slide.Y = y;
            Pill.Opacity = opacity;

            return;
        }

        var duration = TimeSpan.FromMilliseconds(140);

        Slide.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(y, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        Pill.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(opacity, duration));
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke();
    }
}
