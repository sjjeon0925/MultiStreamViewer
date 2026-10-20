using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace MultiStreamViewer.Views;

public partial class SlotPickerWindow : Window
{
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x00));

    // 알파 1짜리 배경: 눈에는 안 보이지만 투명 창이 마우스를 받도록 한다 (완전 투명이면 클릭이 통과함).
    private static readonly Brush HitBrush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

    private const double BorderWidth = 8;

    private readonly Dictionary<int, Border> slotBorders = new();

    private int? selectedSlot;

    // 이 창 위에서 마우스를 "누른" 슬롯. 누른 곳과 뗀 곳이 같아야만 클릭으로 인정한다.
    // (즐겨찾기 창에서 항목을 누르면 창이 닫히며 마우스 버튼을 뗄 때 그 아래에 이 창이 있어서,
    //  뗀 것만으로 슬롯이 선택되어 버리는 문제를 막는다.)
    private int? pressedSlot;

    public event Action<int>? SlotChosen;

    public SlotPickerWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        WindowNative.SetNoActivate(new WindowInteropHelper(this).Handle);
    }

    // areaPx: 방송 영역의 화면 좌표(물리 픽셀)
    // slots: 슬롯 번호와, 방송 영역 기준 DIP 좌표의 칸 위치
    public void Update(Rect areaPx, IReadOnlyList<(int Index, Rect Bounds)> slots)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        if (hwnd == IntPtr.Zero)
            return;

        DpiScale dpi = VisualTreeHelper.GetDpi(this);

        Width = areaPx.Width / dpi.DpiScaleX;
        Height = areaPx.Height / dpi.DpiScaleY;

        WindowNative.SetBounds(hwnd, areaPx);

        Root.Children.Clear();
        slotBorders.Clear();
        pressedSlot = null;

        foreach (var (index, bounds) in slots)
        {
            var border = new Border
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Background = HitBrush,
                BorderThickness = new Thickness(BorderWidth),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            int slot = index;

            border.MouseEnter += (_, _) => RefreshBorder(slot, hovered: true);
            border.MouseLeave += (_, _) =>
            {
                pressedSlot = null;

                RefreshBorder(slot, hovered: false);
            };
            border.MouseLeftButtonDown += (_, _) => pressedSlot = slot;
            border.MouseLeftButtonUp += (_, _) =>
            {
                bool clicked = pressedSlot == slot;

                pressedSlot = null;

                if (clicked)
                    SlotChosen?.Invoke(slot);
            };

            Canvas.SetLeft(border, bounds.Left);
            Canvas.SetTop(border, bounds.Top);

            Root.Children.Add(border);
            slotBorders[index] = border;

            RefreshBorder(index, hovered: border.IsMouseOver);
        }
    }

    // 첫 번째로 고른 슬롯은 마우스가 벗어나도 테두리를 유지한다. null이면 선택 해제.
    public void SetSelected(int? slot)
    {
        int? previous = selectedSlot;

        selectedSlot = slot;

        if (previous.HasValue)
            RefreshBorder(previous.Value, hovered: false);

        if (slot.HasValue)
            RefreshBorder(slot.Value, hovered: true);
    }

    private void RefreshBorder(int slot, bool hovered)
    {
        if (!slotBorders.TryGetValue(slot, out Border? border))
            return;

        border.BorderBrush =
            hovered || selectedSlot == slot
                ? AccentBrush
                : Brushes.Transparent;
    }
}
