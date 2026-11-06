using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

using MultiStreamViewer.Models;
using MultiStreamViewer.Views;

namespace MultiStreamViewer.Services;

// 1~4분할 그리드 배치, 2·3분할 모양 전환, 전체화면 16:9 정렬 계산을 전담한다.
public sealed class StreamLayoutController
{
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

    private int layout2ShapeIndex;
    private int layout3ShapeIndex;

    public int CurrentLayout { get; private set; } = 4;

    public StreamAlignment CurrentAlignment { get; private set; } = StreamAlignment.Fill;

    // 2·3분할에서 다음 분할 모양으로 순환한다. 모양을 바꿀 수 없는 분할이면 false를 반환한다.
    public bool TryCycleShape(out string shapeName)
    {
        if (GetCurrentShapes() is not var (shapes, current))
        {
            shapeName = string.Empty;
            return false;
        }

        int next = (current + 1) % shapes.Length;

        if (CurrentLayout == 2)
            layout2ShapeIndex = next;
        else
            layout3ShapeIndex = next;

        shapeName = shapes[next].Name;
        return true;
    }

    // 오버레이 메뉴의 분할 모양 버튼에 표시할 (다음 아이콘, 툴팁)을 반환한다.
    public (string Icon, string ToolTip) GetShapeButtonLabel()
    {
        if (GetCurrentShapes() is not var (shapes, current))
        {
            return ("◫", "분할 모양 (2·3분할에서 바꿀 수 있어요)");
        }

        var next = shapes[(current + 1) % shapes.Length];
        return (next.Icon, $"{next.Name} (현재: {shapes[current].Name})");
    }

    // 전체화면 정렬 모드를 다음 단계로 순환하고 바뀐 정렬 이름을 반환한다.
    public string CycleAlignment()
    {
        CurrentAlignment = NextAlignment(CurrentAlignment);
        return GetAlignmentName(CurrentAlignment);
    }

    // 오버레이 메뉴의 전체화면 정렬 버튼에 표시할 (다음 아이콘, 툴팁)을 반환한다.
    public (string Icon, string ToolTip) GetAlignmentButtonLabel()
    {
        StreamAlignment next = NextAlignment(CurrentAlignment);
        return (
            GetAlignmentIcon(next),
            $"{GetAlignmentName(next)} (현재: {GetAlignmentName(CurrentAlignment)})");
    }

    // 지정한 분할 수(1~4)에 맞춰 Grid 행/열과 슬롯 배치를 갱신한다.
    public void ApplyLayout(int layout, Grid streamGrid, IReadOnlyList<StreamView> streamViews)
    {
        CurrentLayout = layout;

        streamGrid.RowDefinitions.Clear();
        streamGrid.ColumnDefinitions.Clear();

        switch (layout)
        {
            case 1:
                Apply1Layout(streamGrid, streamViews);
                break;

            case 2:
                Apply2Layout(streamGrid, streamViews);
                break;

            case 3:
                Apply3Layout(streamGrid, streamViews);
                break;

            default:
                Apply4Layout(streamGrid, streamViews);
                break;
        }
    }

    // 전체화면 여백 정렬 설정에 맞춰 StreamGrid 크기와 정렬을 조정한다.
    public void ApplyAlignment(bool isFullscreen, Grid rootGrid, Grid streamGrid)
    {
        if (!isFullscreen || CurrentAlignment == StreamAlignment.Fill)
        {
            streamGrid.ClearValue(FrameworkElement.WidthProperty);
            streamGrid.ClearValue(FrameworkElement.HeightProperty);
            streamGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            streamGrid.VerticalAlignment = VerticalAlignment.Stretch;
            return;
        }

        double availableWidth = rootGrid.ActualWidth;
        double availableHeight = rootGrid.ActualHeight;

        if (availableWidth <= 0 || availableHeight <= 0)
            return;

        double aspect = GetContentAspect(CurrentLayout);

        double width = availableWidth;
        double height = width / aspect;

        if (height > availableHeight)
        {
            height = availableHeight;
            width = height * aspect;
        }

        streamGrid.Width = width;
        streamGrid.Height = height;
        streamGrid.HorizontalAlignment = HorizontalAlignment.Center;
        streamGrid.VerticalAlignment = CurrentAlignment switch
        {
            StreamAlignment.Top => VerticalAlignment.Top,
            StreamAlignment.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center
        };
    }

    private ((string Icon, string Name)[] Shapes, int Current)? GetCurrentShapes() =>
        CurrentLayout switch
        {
            2 => (Layout2Shapes, layout2ShapeIndex),
            3 => (Layout3Shapes, layout3ShapeIndex),
            _ => null
        };

    // 슬롯 칸 하나는 16:9로 본다. 2분할은 가로 나란히면 32:9, 세로 쌓기면 16:18이다.
    private double GetContentAspect(int layout) =>
        layout == 2
            ? (layout2ShapeIndex == 1 ? 16.0 / 18.0 : 32.0 / 9.0)
            : 16.0 / 9.0;

    private void Apply1Layout(Grid streamGrid, IReadOnlyList<StreamView> streamViews)
    {
        streamGrid.RowDefinitions.Add(new RowDefinition());
        streamGrid.ColumnDefinitions.Add(new ColumnDefinition());

        PlaceStreamView(streamViews[0], 0, 0);
        UpdateSlotVisibility(streamViews, 0);
    }

    private void Apply2Layout(Grid streamGrid, IReadOnlyList<StreamView> streamViews)
    {
        bool vertical = layout2ShapeIndex == 1;

        streamGrid.RowDefinitions.Add(new RowDefinition());
        streamGrid.ColumnDefinitions.Add(new ColumnDefinition());

        if (vertical)
            streamGrid.RowDefinitions.Add(new RowDefinition());
        else
            streamGrid.ColumnDefinitions.Add(new ColumnDefinition());

        PlaceStreamView(streamViews[0], 0, 0);
        PlaceStreamView(streamViews[1], vertical ? 1 : 0, vertical ? 0 : 1);

        UpdateSlotVisibility(streamViews, 0, 1);
    }

    private void Apply3Layout(Grid streamGrid, IReadOnlyList<StreamView> streamViews)
    {
        streamGrid.RowDefinitions.Add(new RowDefinition());
        streamGrid.RowDefinitions.Add(new RowDefinition());
        streamGrid.ColumnDefinitions.Add(new ColumnDefinition());
        streamGrid.ColumnDefinitions.Add(new ColumnDefinition());

        switch (layout3ShapeIndex)
        {
            case 1: // 위 2 · 아래 1
                PlaceStreamView(streamViews[0], 0, 0);
                PlaceStreamView(streamViews[1], 0, 1);
                PlaceStreamView(streamViews[2], 1, 0, 1, 2);
                break;

            case 2: // 왼쪽 1 · 오른쪽 2
                PlaceStreamView(streamViews[0], 0, 0, 2, 1);
                PlaceStreamView(streamViews[1], 0, 1);
                PlaceStreamView(streamViews[2], 1, 1);
                break;

            case 3: // 왼쪽 2 · 오른쪽 1
                PlaceStreamView(streamViews[0], 0, 0);
                PlaceStreamView(streamViews[1], 1, 0);
                PlaceStreamView(streamViews[2], 0, 1, 2, 1);
                break;

            default: // 위 1 · 아래 2
                PlaceStreamView(streamViews[0], 0, 0, 1, 2);
                PlaceStreamView(streamViews[1], 1, 0);
                PlaceStreamView(streamViews[2], 1, 1);
                break;
        }

        UpdateSlotVisibility(streamViews, 0, 1, 2);
    }

    private static void Apply4Layout(Grid streamGrid, IReadOnlyList<StreamView> streamViews)
    {
        streamGrid.RowDefinitions.Add(new RowDefinition());
        streamGrid.RowDefinitions.Add(new RowDefinition());
        streamGrid.ColumnDefinitions.Add(new ColumnDefinition());
        streamGrid.ColumnDefinitions.Add(new ColumnDefinition());

        PlaceStreamView(streamViews[0], 0, 0);
        PlaceStreamView(streamViews[1], 0, 1);
        PlaceStreamView(streamViews[2], 1, 0);
        PlaceStreamView(streamViews[3], 1, 1);

        UpdateSlotVisibility(streamViews, 0, 1, 2, 3);
    }

    private static void PlaceStreamView(
        StreamView streamView,
        int row,
        int column,
        int rowSpan = 1,
        int columnSpan = 1)
    {
        streamView.Visibility = Visibility.Visible;

        Grid.SetRow(streamView, row);
        Grid.SetColumn(streamView, column);
        Grid.SetRowSpan(streamView, rowSpan);
        Grid.SetColumnSpan(streamView, columnSpan);
    }

    private static void UpdateSlotVisibility(
        IReadOnlyList<StreamView> streamViews,
        params int[] visibleIndexes)
    {
        for (int i = 0; i < streamViews.Count; i++)
        {
            bool visible = Array.IndexOf(visibleIndexes, i) >= 0;
            streamViews[i].Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }
    }

    private static string GetAlignmentName(StreamAlignment align) =>
        align switch
        {
            StreamAlignment.Top => "위에 붙이기",
            StreamAlignment.Center => "가운데",
            StreamAlignment.Bottom => "아래에 붙이기",
            _ => "꽉 채우기"
        };

    private static string GetAlignmentIcon(StreamAlignment align) =>
        align switch
        {
            StreamAlignment.Top => "⤒",
            StreamAlignment.Center => "↕",
            StreamAlignment.Bottom => "⤓",
            _ => "⛶"
        };

    private static StreamAlignment NextAlignment(StreamAlignment align) =>
        (StreamAlignment)(((int)align + 1) % Enum.GetValues<StreamAlignment>().Length);
}
