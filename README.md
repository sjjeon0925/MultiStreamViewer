# MultiStreamViewer

CHZZK(치지직), Twitch, SOOP, 씨미(CIME), YouTube 라이브 방송을 한 화면에서 최대 4분할로 동시에 볼 수 있는 Windows용 데스크톱 앱입니다. WPF와 WebView2를 기반으로 만들어졌습니다.

## 주요 기능

- **1 / 2 / 3 / 4분할 레이아웃**: 상단 버튼으로 화면 분할 수를 즉시 전환
- **자동 플랫폼 인식**: 입력한 URL을 보고 CHZZK / Twitch / SOOP / 씨미 / YouTube를 자동으로 구분
- **전체화면 모드**: 분할된 화면 그대로 작업 표시줄까지 가리는 완전한 전체화면으로 전환 (Esc로 해제), 멀티 모니터 및 서로 다른 DPI 환경 지원
- **세션 공유**: 4개 슬롯이 하나의 WebView2 프로필을 공유해 로그인 상태와 쿠키를 함께 사용
- **CHZZK 권한 팝업 자동 처리**: 로컬 네트워크 액세스 권한을 자동으로 허용하고, 브라우저 권한 설정 안내 모달을 자동으로 닫음

## 요구 사항

- Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [WebView2 런타임](https://developer.microsoft.com/microsoft-edge/webview2/) (대부분의 Windows 11 시스템에 기본 설치되어 있음)

## 빌드 및 실행

```bash
dotnet build MultiStreamViewer.sln
dotnet run --project MultiStreamViewer/MultiStreamViewer.csproj
```

또는 Visual Studio에서 `MultiStreamViewer.sln`을 열어 F5로 실행합니다.

## 프로젝트 구조

```
MultiStreamViewer/
├── MainWindow.xaml(.cs)     # 메인 창: 분할 레이아웃, 전체화면 토글, 방송 연결/해제
├── Views/StreamView.xaml(.cs)  # 방송 1개를 표시하는 슬롯 (WebView2 래핑)
├── Models/StreamInfo.cs     # 슬롯별 방송 상태 모델
├── Services/PlatformDetector.cs  # URL을 보고 플랫폼 판별
└── Extensions/              # WebView2에 설치되는 확장 프로그램 (네이버 영상 재생 보조)
```
