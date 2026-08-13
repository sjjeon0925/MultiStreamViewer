using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Web.WebView2.Core;

using MultiStreamViewer.Models;
using MultiStreamViewer.Services;

namespace MultiStreamViewer.Views;

public partial class StreamView : UserControl
{
    public StreamInfo Info { get; private set; }


    public StreamView(int slotIndex)
    {
        InitializeComponent();

        Info = new StreamInfo
        {
            SlotIndex = slotIndex
        };

        StreamTitle.Text =
            $"슬롯 {slotIndex + 1}";

        PlatformText.Text =
            "대기";

        StreamStatus.Text =
            "준비됨";

        Loaded += StreamView_Loaded;

        Browser.NavigationCompleted +=
            Browser_NavigationCompleted;
    }

    private async void StreamView_Loaded(
    object sender,
    RoutedEventArgs e)
    {
        try
        {
            var options = new CoreWebView2EnvironmentOptions
            {
                AreBrowserExtensionsEnabled = true
            };

            // 슬롯 4개가 로그인/쿠키를 공유하도록 프로필(userDataFolder)은 다시 하나로 합친다.
            // (한때 슬롯별로 분리했던 이유였던 권한 요청 뒤섞임 문제는, 요청을 리액티브하게
            // 받는 대신 GrantLocalNetworkAccessAsync에서 CDP로 능동적으로 미리 부여하는
            // 방식으로 바뀌면서 더 이상 필요하지 않게 되었다.)
            var environment =
                await CoreWebView2Environment.CreateAsync(
                    null,
                    null,
                    options);

            await Browser.EnsureCoreWebView2Async(environment);

            // 로컬 네트워크 액세스(LNA) 등 사이트가 요청하는 권한을 자동 허용한다.
            // 임베디드 WebView2에는 주소창 옆 권한 팝업 UI가 없어서
            // 앱이 대신 응답해주지 않으면 CHZZK가 계속 권한 안내를 띄운다.
            // (원인 진단용으로 어떤 권한이 실제로 요청되는지 로그를 남긴다.)
            Browser.CoreWebView2.PermissionRequested += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[권한 요청] kind={args.PermissionKind}, uri={args.Uri}, userInitiated={args.IsUserInitiated}, before={args.State}");

                args.State = CoreWebView2PermissionState.Allow;
                args.SavesInProfile = true;

                System.Diagnostics.Debug.WriteLine(
                    $"[권한 요청] -> Allow로 응답, SavesInProfile=true");
            };

            await InstallNaverVideoPluginAsync();

            await GrantLocalNetworkAccessAsync();

            StreamStatus.Text =
                "준비됨";
        }
        catch (Exception ex)
        {
            StreamStatus.Text =
                "WebView2 오류";

            System.Diagnostics.Debug.WriteLine(
                $"WebView2 초기화 오류: {ex}");
        }
    }

    // navigator.permissions.query({name:'local-network-access'})가 'prompt'를 반환하는 걸로
    // 봐서 이 Chromium 엔진은 LNA를 정식으로 지원하지만, WebView2의 PermissionRequested
    // 이벤트에는 아직 연결되어 있지 않다(요청이 와도 이벤트가 안 뜸). 대신 CDP(Chrome
    // DevTools Protocol)의 Browser.grantPermissions로 직접 origin에 권한을 부여해본다.
    private async Task GrantLocalNetworkAccessAsync()
    {
        if (Browser.CoreWebView2 == null)
            return;

        try
        {
            string parameters = """
                {
                    "origin": "https://chzzk.naver.com",
                    "permissions": ["localNetworkAccess"]
                }
                """;

            await Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Browser.grantPermissions",
                parameters);

            System.Diagnostics.Debug.WriteLine(
                "[LNA] Browser.grantPermissions(localNetworkAccess) 호출 성공");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LNA] Browser.grantPermissions 실패: {ex}");
        }
    }

    // 프로세스당 한 번만 설치를 시도한다 (슬롯 4개가 동시에 중복 설치를 시도하지 않도록).
    private static readonly SemaphoreSlim ExtensionInstallLock = new(1, 1);
    private static bool _extensionsInstalled;

    private async Task InstallNaverVideoPluginAsync()
    {
        if (Browser.CoreWebView2 == null)
            return;

        string extensionsRoot =
            Path.Combine(
                AppContext.BaseDirectory,
                "Extensions");

        if (!Directory.Exists(extensionsRoot))
            return;

        await ExtensionInstallLock.WaitAsync();

        try
        {
            if (_extensionsInstalled)
                return;

            var alreadyInstalled =
                (await Browser.CoreWebView2.Profile.GetBrowserExtensionsAsync())
                    .Select(e => e.Id)
                    .ToHashSet();

            foreach (string pluginDir in Directory.GetDirectories(extensionsRoot))
            {
                string manifestPath =
                    Path.Combine(pluginDir, "manifest.json");

                if (!File.Exists(manifestPath))
                    continue;

                try
                {
                    var extensionInfo =
                        await Browser.CoreWebView2.Profile.AddBrowserExtensionAsync(
                            pluginDir);

                    System.Diagnostics.Debug.WriteLine(
                        $"확장 설치 완료: {extensionInfo.Name} ({Path.GetFileName(pluginDir)}, id={extensionInfo.Id})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"확장 설치 실패 ({Path.GetFileName(pluginDir)}): {ex}");
                }
            }

            _extensionsInstalled = true;
        }
        finally
        {
            ExtensionInstallLock.Release();
        }
    }

    public async Task NavigateAsync(string url)
    {
        if (Browser.CoreWebView2 == null)
        {
            await Browser.EnsureCoreWebView2Async();
        }


        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out Uri? uri))
        {
            StreamStatus.Text =
                "잘못된 URL";

            return;
        }


        if (uri.Scheme != Uri.UriSchemeHttps &&
            uri.Scheme != Uri.UriSchemeHttp)
        {
            StreamStatus.Text =
                "지원하지 않는 URL";

            return;
        }


        string platform =
            PlatformDetector.Detect(url);


        //if (platform == "Unknown")
        //{
        //    StreamStatus.Text =
        //        "지원하지 않는 플랫폼";

        //    return;
        //}


        Info.Url = url;

        Info.Platform = platform;

        Info.Title = uri.Host;

        Info.IsConnected = true;


        PlatformText.Text =
            platform;

        StreamStatus.Text =
            "연결 중...";


        Browser.Source = uri;
    }


    public void Stop()
    {
        try
        {
            if (Browser.CoreWebView2 != null)
            {
                Browser.CoreWebView2.Navigate(
                    "about:blank");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"WebView2 종료 오류: {ex}");
        }


        Info.Url =
            string.Empty;

        Info.Platform =
            string.Empty;

        Info.Title =
            string.Empty;

        Info.IsConnected =
            false;


        PlatformText.Text =
            "대기";

        StreamTitle.Text =
            $"슬롯 {Info.SlotIndex + 1}";

        StreamStatus.Text =
            "준비됨";
    }


    private async void Browser_NavigationCompleted(
        object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            StreamStatus.Text =
                "페이지 로딩 실패";

            return;
        }


        StreamStatus.Text =
            "연결됨";

        if (Info.Platform == "CHZZK")
        {
            await DismissChzzkPermissionModalAsync();
        }
    }

    // CHZZK가 "브라우저 권한 설정 안내" 모달을 띄우면 자동으로 확인 버튼을 눌러준다.
    // 임베디드 WebView2에는 CHZZK가 안내하는 주소창 옆 권한 팝업이 없어서
    // 사용자가 수동으로 닫아줄 방법이 없기 때문.
    private async Task DismissChzzkPermissionModalAsync()
    {
        if (Browser.CoreWebView2 == null)
            return;

        // 화질을 바꿀 때마다 이 모달이 다시 뜨기 때문에, 최초 1회만 닫고 끝내지 않고
        // 페이지가 살아있는 동안 계속 감시한다. 단, 전체 문서를 매번 다시 스캔하면
        // 채팅창처럼 자주 갱신되는 페이지에서 성능 문제가 생길 수 있으므로
        // 새로 추가된 노드 안에서만 찾는다.
        string script = """
            (() => {
                if (window.__chzzkPermissionWatcherInstalled) return;
                window.__chzzkPermissionWatcherInstalled = true;

                function tryDismissWithin(root) {
                    if (!root || typeof root.querySelectorAll !== 'function') return false;

                    const marker = [...root.querySelectorAll('*')]
                        .find(el =>
                            el.children.length === 0 &&
                            el.textContent.trim() === '브라우저 권한 설정 안내'
                        );

                    if (!marker) return false;

                    let node = marker;

                    for (let i = 0; i < 8 && node; i++) {
                        node = node.parentElement;

                        if (!node) break;

                        const button = [...node.querySelectorAll('button')]
                            .find(b => b.textContent.trim() === '확인');

                        if (button) {
                            button.click();
                            return true;
                        }
                    }

                    return false;
                }

                tryDismissWithin(document.body);

                const observer = new MutationObserver((mutations) => {
                    for (const mutation of mutations) {
                        for (const added of mutation.addedNodes) {
                            if (added.nodeType === 1 && tryDismissWithin(added)) {
                                return;
                            }
                        }
                    }
                });

                observer.observe(document.body, {
                    childList: true,
                    subtree: true
                });
            })();
            """;

        try
        {
            await Browser.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"권한 안내 자동 닫기 실패: {ex}");
        }
    }
}
