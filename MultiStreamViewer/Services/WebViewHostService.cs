using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MultiStreamViewer.Services;

// 4개 방송 슬롯이 공유하는 WebView2 환경 초기화, 확장 프로그램 설치, 치지직(CHZZK) 전용 권한/모달 처리를 담당한다.
public static class WebViewHostService
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static CoreWebView2Environment? sharedEnvironment;
    private static bool extensionsInstalled;

    private const string ChzzkModalDismissScript = """
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

    // 슬롯의 WebView2 컨트롤을 공유 환경으로 초기화하고 권한 및 확장 설정을 적용한다.
    public static async Task InitializeWebViewAsync(WebView2 browser)
    {
        CoreWebView2Environment environment = await GetOrCreateEnvironmentAsync();

        await browser.EnsureCoreWebView2Async(environment);

        ConfigurePermissions(browser.CoreWebView2);

        await EnsureNaverVideoPluginInstalledAsync(browser.CoreWebView2);

        await GrantChzzkLocalNetworkAccessAsync(browser.CoreWebView2);
    }

    // CHZZK 페이지에서 "브라우저 권한 설정 안내" 모달이 뜨면 자동으로 확인 버튼을 눌러 닫는다.
    public static async Task DismissChzzkPermissionModalAsync(CoreWebView2? coreWebView)
    {
        if (coreWebView == null)
            return;

        try
        {
            await coreWebView.ExecuteScriptAsync(ChzzkModalDismissScript);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"권한 안내 자동 닫기 실패: {ex}");
        }
    }

    private static async Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync()
    {
        if (sharedEnvironment != null)
            return sharedEnvironment;

        await InitLock.WaitAsync();
        try
        {
            if (sharedEnvironment != null)
                return sharedEnvironment;

            var options = new CoreWebView2EnvironmentOptions
            {
                AreBrowserExtensionsEnabled = true
            };

            sharedEnvironment = await CoreWebView2Environment.CreateAsync(
                null,
                null,
                options);

            return sharedEnvironment;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static void ConfigurePermissions(CoreWebView2 coreWebView)
    {
        coreWebView.PermissionRequested += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine(
                $"[권한 요청] kind={args.PermissionKind}, uri={args.Uri}, userInitiated={args.IsUserInitiated}, before={args.State}");

            if (!PlatformDetector.IsChzzkUri(args.Uri))
                return;

            args.State = CoreWebView2PermissionState.Allow;
            args.SavesInProfile = true;

            System.Diagnostics.Debug.WriteLine(
                "[권한 요청] -> Allow로 응답, SavesInProfile=true");
        };
    }

    private static async Task GrantChzzkLocalNetworkAccessAsync(CoreWebView2 coreWebView)
    {
        try
        {
            const string parameters = """
                {
                    "origin": "https://chzzk.naver.com",
                    "permissions": ["localNetworkAccess"]
                }
                """;

            await coreWebView.CallDevToolsProtocolMethodAsync(
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

    private static async Task EnsureNaverVideoPluginInstalledAsync(CoreWebView2 coreWebView)
    {
        if (extensionsInstalled)
            return;

        string pluginDir = Path.Combine(
            AppContext.BaseDirectory,
            "Extensions",
            "NaverVideoPluginEdge");

        if (!Directory.Exists(pluginDir) ||
            !File.Exists(Path.Combine(pluginDir, "manifest.json")))
        {
            return;
        }

        await InitLock.WaitAsync();
        try
        {
            if (extensionsInstalled)
                return;

            var installedNames = (await coreWebView.Profile.GetBrowserExtensionsAsync())
                .Select(e => e.Name)
                .ToHashSet(StringComparer.Ordinal);

            try
            {
                var extensionInfo = await coreWebView.Profile.AddBrowserExtensionAsync(pluginDir);

                System.Diagnostics.Debug.WriteLine(
                    $"확장 설치 완료: {extensionInfo.Name} (id={extensionInfo.Id}, 기존={installedNames.Count}개)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"확장 설치 실패 ({Path.GetFileName(pluginDir)}): {ex}");
            }

            extensionsInstalled = true;
        }
        finally
        {
            InitLock.Release();
        }
    }
}
