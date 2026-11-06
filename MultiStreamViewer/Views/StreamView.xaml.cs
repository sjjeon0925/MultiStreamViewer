using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Web.WebView2.Core;

using MultiStreamViewer.Models;
using MultiStreamViewer.Services;

namespace MultiStreamViewer.Views;

public partial class StreamView : UserControl
{
    public StreamInfo Info { get; }

    public StreamView(int slotIndex)
    {
        InitializeComponent();

        Info = new StreamInfo
        {
            SlotIndex = slotIndex
        };

        StreamTitle.Text = $"슬롯 {slotIndex + 1}";
        PlatformText.Text = "대기";
        StreamStatus.Text = "준비됨";

        Loaded += StreamView_Loaded;
        Browser.NavigationCompleted += Browser_NavigationCompleted;
    }

    // 이 슬롯에 처음 연결한 주소 (연결 해제 전까지 유지)
    public string InitialUrl => Info.Url;

    // 이 슬롯이 지금 보고 있는 주소 (페이지 안에서 이동했다면 이동한 곳)
    public string CurrentUrl
    {
        get
        {
            string? source = Browser.CoreWebView2?.Source;

            return string.IsNullOrEmpty(source) || source == "about:blank"
                ? Info.Url
                : source;
        }
    }

    // 슬롯 위치가 바뀌었을 때 번호와 제목 표시를 새 위치에 맞춘다.
    public void SetSlotIndex(int slotIndex)
    {
        Info.SlotIndex = slotIndex;
        StreamTitle.Text = $"슬롯 {slotIndex + 1}";
    }

    private async void StreamView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebViewHostService.InitializeWebViewAsync(Browser);
            StreamStatus.Text = "준비됨";
        }
        catch (Exception ex)
        {
            StreamStatus.Text = "WebView2 오류";
            System.Diagnostics.Debug.WriteLine($"WebView2 초기화 오류: {ex}");
        }
    }

    public async Task NavigateAsync(string rawUrl)
    {
        if (Browser.CoreWebView2 == null)
        {
            await WebViewHostService.InitializeWebViewAsync(Browser);
        }

        if (!PlatformDetector.TryNormalizeHttpUri(rawUrl, out Uri? uri, out string normalizedUrl) ||
            uri == null)
        {
            StreamStatus.Text = "잘못된 URL";
            return;
        }

        string platform = PlatformDetector.Detect(normalizedUrl);

        Info.Url = normalizedUrl;
        Info.Platform = platform;
        Info.Title = uri.Host;
        Info.IsConnected = true;

        PlatformText.Text = platform;
        StreamStatus.Text = "연결 중...";

        Browser.Source = uri;
    }

    public void Stop()
    {
        try
        {
            Browser.CoreWebView2?.Navigate("about:blank");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 종료 오류: {ex}");
        }

        Info.Url = string.Empty;
        Info.Platform = string.Empty;
        Info.Title = string.Empty;
        Info.IsConnected = false;

        PlatformText.Text = "대기";
        StreamTitle.Text = $"슬롯 {Info.SlotIndex + 1}";
        StreamStatus.Text = "준비됨";
    }

    private async void Browser_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            StreamStatus.Text = "페이지 로딩 실패";
            return;
        }

        StreamStatus.Text = "연결됨";

        if (Info.Platform == "CHZZK")
        {
            await WebViewHostService.DismissChzzkPermissionModalAsync(Browser.CoreWebView2);
        }
    }
}
