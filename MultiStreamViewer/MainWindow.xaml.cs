using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using MultiStreamViewer.Models;
using MultiStreamViewer.Services;
using MultiStreamViewer.Views;

namespace MultiStreamViewer;

public partial class MainWindow : Window
{
    private const int SlotCount = 4;

    private readonly List<StreamView> streamViews = new(SlotCount);
    private readonly StreamLayoutController layoutController = new();
    private readonly FullscreenController fullscreenController;

    private OverlayMenuWindow? overlayMenu;
    private FullscreenExitWindow? fullscreenExit;
    private SlotPickerWindow? slotPicker;

    private PickMode pickMode = PickMode.None;
    private OverlayFeature activePickFeature;
    private int? swapFirstSlot;
    private Favorite? pendingFavorite;

    private bool IsPicking => pickMode != PickMode.None;

    public MainWindow()
    {
        InitializeComponent();

        fullscreenController = new FullscreenController(
            this,
            TopBarRow,
            BottomStatusBarRow,
            StreamGrid);

        CreateStreamViews();
        SetLayout(4);

        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += (_, _) => CreateOverlayWindows();
        LocationChanged += (_, _) => RepositionOverlayMenu();
        SizeChanged += (_, _) =>
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                ApplyStreamAlignment);
        };
    }

    private void CreateStreamViews()
    {
        streamViews.Clear();

        for (int i = 0; i < SlotCount; i++)
        {
            var streamView = new StreamView(i);
            streamViews.Add(streamView);
            StreamGrid.Children.Add(streamView);
        }
    }

    private void CreateOverlayWindows()
    {
        overlayMenu = new OverlayMenuWindow
        {
            Owner = this
        };
        overlayMenu.FeatureClicked += OverlayFeatureClicked;

        fullscreenExit = new FullscreenExitWindow(this);
        fullscreenExit.ExitRequested += ExitFullscreenMode;

        UpdateAlignButton();
        UpdateShapeButton();

        overlayMenu.Show();
        RepositionOverlayMenu();
    }

    // =========================================================
    // 레이아웃 / 모양 / 전체화면 정렬 제어
    // =========================================================

    private void SetLayout(int layout)
    {
        layoutController.ApplyLayout(layout, StreamGrid, streamViews);

        StatusText.Text = $"{layout}분할 모드";

        UpdateShapeButton();
        ApplyStreamAlignment();
        RepositionOverlayMenu();
    }

    private void CycleShape()
    {
        if (!layoutController.TryCycleShape(out string shapeName))
        {
            StatusText.Text = "분할 모양은 2분할, 3분할에서만 바꿀 수 있습니다.";
            return;
        }

        SetLayout(layoutController.CurrentLayout);
        StatusText.Text = $"분할 모양: {shapeName}";
    }

    private void CycleAlignment()
    {
        string alignmentName = layoutController.CycleAlignment();

        UpdateAlignButton();
        ApplyStreamAlignment();

        StatusText.Text = $"전체화면 정렬: {alignmentName}";
    }

    private void ApplyStreamAlignment()
    {
        layoutController.ApplyAlignment(
            fullscreenController.IsFullscreen,
            RootGrid,
            StreamGrid);

        RepositionOverlayMenu();
    }

    private void UpdateShapeButton()
    {
        var (icon, toolTip) = layoutController.GetShapeButtonLabel();
        overlayMenu?.SetFeatureLabel(OverlayFeature.Shape, icon, toolTip);
    }

    private void UpdateAlignButton()
    {
        var (icon, toolTip) = layoutController.GetAlignmentButtonLabel();
        overlayMenu?.SetFeatureLabel(OverlayFeature.Align, icon, toolTip);
    }

    private void RepositionOverlayMenu()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () =>
            {
                if (overlayMenu == null || !IsLoaded)
                    return;

                Rect areaPx = WindowNative.GetElementScreenBoundsPx(this, StreamGrid);
                overlayMenu.Reposition(areaPx, layoutController.CurrentLayout);

                UpdateSlotPicker(areaPx);
            });
    }

    // =========================================================
    // 오버레이 메뉴 및 슬롯 선택 모드 (위치 바꾸기 / 즐겨찾기 열기)
    // =========================================================

    private void OverlayFeatureClicked(OverlayFeature feature)
    {
        if (IsPicking)
        {
            if (feature == activePickFeature)
                CancelPickMode();

            return;
        }

        switch (feature)
        {
            case OverlayFeature.Favorites:
                OpenFavoritesWindow();
                break;

            case OverlayFeature.Align:
                CycleAlignment();
                break;

            case OverlayFeature.Shape:
                CycleShape();
                break;

            case OverlayFeature.Swap:
                StartPickMode(
                    PickMode.Swap,
                    OverlayFeature.Swap,
                    "바꿀 첫 번째 슬롯을 선택하세요.");
                break;
        }
    }

    private void StartPickMode(
        PickMode mode,
        OverlayFeature feature,
        string hint)
    {
        if (IsPicking)
            return;

        pickMode = mode;
        activePickFeature = feature;
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

        // 선택 창 위에 취소 버튼 메뉴가 보이도록 소유자를 선택 창으로 변경
        overlayMenu?.SetOwnerWindow(slotPicker);
        overlayMenu?.SetPinned(true);
        overlayMenu?.SetFeatureLabel(feature, "✕", "취소 (Esc)");

        StatusText.Text = hint;

        RepositionOverlayMenu();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () => overlayMenu?.BringToTop());
    }

    private void EndPickMode()
    {
        if (!IsPicking)
            return;

        OverlayFeature feature = activePickFeature;

        pickMode = PickMode.None;
        swapFirstSlot = null;
        pendingFavorite = null;

        overlayMenu?.SetOwnerWindow(this);
        slotPicker?.Hide();

        overlayMenu?.ResetFeatureLabel(feature);
        UpdateAlignButton();
        UpdateShapeButton();

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
        if (first == slot)
        {
            swapFirstSlot = null;
            slotPicker?.SetSelected(null);
            StatusText.Text = "바꿀 첫 번째 슬롯을 선택하세요.";
            return;
        }

        (streamViews[first], streamViews[slot]) = (streamViews[slot], streamViews[first]);

        EndPickMode();

        for (int i = 0; i < streamViews.Count; i++)
            streamViews[i].SetSlotIndex(i);

        SetLayout(layoutController.CurrentLayout);
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

    private void OpenFavoritesWindow()
    {
        var window = new FavoritesWindow(UrlTextBox.Text.Trim())
        {
            Owner = this
        };

        if (window.ShowDialog() != true || window.Chosen == null)
        {
            overlayMenu?.Collapse();
            return;
        }

        pendingFavorite = window.Chosen;

        StartPickMode(
            PickMode.OpenFavorite,
            OverlayFeature.Favorites,
            $"'{window.Chosen.Name}'을(를) 열 슬롯을 선택하세요.");
    }

    private void UpdateSlotPicker(Rect streamAreaPx)
    {
        if (!IsPicking || slotPicker == null)
            return;

        var slots = new List<(int Index, Rect Bounds)>();

        for (int i = 0; i < streamViews.Count; i++)
        {
            StreamView view = streamViews[i];
            if (view.Visibility != Visibility.Visible)
                continue;

            Rect bounds = view.TransformToAncestor(StreamGrid)
                .TransformBounds(new Rect(0, 0, view.ActualWidth, view.ActualHeight));

            slots.Add((i, bounds));
        }

        slotPicker.Update(streamAreaPx, slots);
    }

    // =========================================================
    // 전체화면 진입 및 해제
    // =========================================================

    private void EnterFullscreenMode()
    {
        if (!fullscreenController.Enter())
            return;

        fullscreenExit?.Begin();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            ApplyStreamAlignment);
    }

    private void ExitFullscreenMode()
    {
        if (!fullscreenController.Exit())
            return;

        fullscreenExit?.End();
        ApplyStreamAlignment();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (IsPicking)
        {
            CancelPickMode();
            e.Handled = true;
        }
        else if (fullscreenController.IsFullscreen)
        {
            ExitFullscreenMode();
            e.Handled = true;
        }
    }

    // =========================================================
    // 상단 툴바 버튼 이벤트
    // =========================================================

    private void LayoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            int.TryParse(button.Tag?.ToString(), out int layout))
        {
            SetLayout(layout);
        }
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        EnterFullscreenMode();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        string url = UrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusText.Text = "방송 URL을 입력하세요.";
            return;
        }

        if (!TryGetSelectedSlot(out int slotIndex, out StreamView? streamView) ||
            streamView == null)
        {
            return;
        }

        await streamView.NavigateAsync(url);

        StatusText.Text = streamView.Info.IsConnected
            ? $"슬롯 {slotIndex + 1} 연결: {streamView.Info.Platform}"
            : $"슬롯 {slotIndex + 1} 연결 실패";
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedSlot(out int slotIndex, out StreamView? streamView) ||
            streamView == null)
        {
            return;
        }

        streamView.Stop();
        StatusText.Text = $"슬롯 {slotIndex + 1} 연결 해제";
    }

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
        if (!TryGetSelectedSlot(out int slotIndex, out StreamView? streamView) ||
            streamView == null)
        {
            return;
        }

        string url = selector(streamView);
        if (string.IsNullOrEmpty(url))
        {
            StatusText.Text = $"슬롯 {slotIndex + 1}에 연결된 방송이 없습니다.";
            return;
        }

        UrlTextBox.Text = url;

        try
        {
            Clipboard.SetText(url);
            StatusText.Text = $"슬롯 {slotIndex + 1} {kind} 링크를 복사했습니다.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"클립보드 복사 실패: {ex}");
            StatusText.Text = $"슬롯 {slotIndex + 1} {kind} 링크를 주소창에 채웠습니다 (복사 실패).";
        }
    }

    private bool TryGetSelectedSlot(out int slotIndex, out StreamView? streamView)
    {
        slotIndex = SlotSelector.SelectedIndex;
        if (slotIndex < 0 || slotIndex >= streamViews.Count)
        {
            streamView = null;
            StatusText.Text = "슬롯을 선택하세요.";
            return false;
        }

        streamView = streamViews[slotIndex];
        return true;
    }
}
