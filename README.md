# MultiStreamViewer

CHZZK(치지직), Twitch, SOOP, 씨미(CIME), YouTube 라이브 방송을 한 화면에서 최대 4분할로 동시에 볼 수 있는 Windows용 데스크톱 앱입니다. WPF와 WebView2를 기반으로 만들어졌습니다.

## 주요 기능

- **1 / 2 / 3 / 4분할 레이아웃**: 상단 버튼으로 화면 분할 수를 즉시 전환
- **분할 모양 변경**: 2분할은 가로/세로, 3분할은 위1·아래2 / 위2·아래1 / 왼쪽1·오른쪽2 / 왼쪽2·오른쪽1 중에서 선택
- **슬롯 위치 바꾸기**: 두 슬롯을 차례로 클릭하면 방송을 끊지 않고 서로 자리를 교환
- **즐겨찾기**: 방송 주소를 추가/삭제하고, 목록에서 고른 뒤 열 슬롯을 클릭해 바로 연결 (`%AppData%\MultiStreamViewer\favorites.json`에 저장)
- **링크 추출**: 상단에서 슬롯을 고르고 `현재 링크` / `처음 링크`를 누르면 그 슬롯의 주소를 주소창에 채우고 클립보드에 복사 (슬롯 위치를 바꿨다면 바뀐 자리 기준)
- **자동 플랫폼 인식**: 입력한 URL을 보고 CHZZK / Twitch / SOOP / 씨미 / YouTube를 자동으로 구분
- **전체화면 모드**: 분할된 화면 그대로 작업 표시줄까지 가리는 완전한 전체화면으로 전환, 멀티 모니터 및 서로 다른 DPI 환경 지원
  - Esc 또는 화면 맨 위로 마우스를 가져가면 나타나는 ✕ 버튼으로 해제
  - **전체화면 정렬**: 16:10 같은 모니터에서 생기는 위/아래 공백을 위에 붙이기 / 가운데 / 아래에 붙이기 / 꽉 채우기 중에서 선택
- **세션 공유**: 4개 슬롯이 하나의 WebView2 프로필을 공유해 로그인 상태와 쿠키를 함께 사용
- **CHZZK 권한 팝업 자동 처리**: 로컬 네트워크 액세스 권한을 자동으로 허용하고, 브라우저 권한 설정 안내 모달을 자동으로 닫음

## 오버레이 메뉴

방송 화면 위 정중앙(1분할은 오른쪽 중앙)에 마우스를 가져가면 반투명 `☰` 버튼이 나타납니다. 누르면 네 방향으로 기능 버튼이 펼쳐지고, 마우스가 멀어지면 다시 사라집니다.

| 위치 | 기능 |
|---|---|
| 위 | 즐겨찾기 (★) |
| 오른쪽 | 전체화면 정렬 (↕) |
| 아래 | 슬롯 위치 바꾸기 (⇄) |
| 왼쪽 | 분할 모양 변경 (◫) |

정렬과 분할 모양 버튼은 지금 상태가 아니라 누르면 바뀔 **다음 상태**를 아이콘으로 보여주고, 툴팁에 현재 상태가 표시됩니다. 슬롯 선택 중에는 버튼이 ✕(취소)로 바뀌며 Esc로도 취소할 수 있습니다.

## 요구 사항

- Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [WebView2 런타임](https://developer.microsoft.com/microsoft-edge/webview2/) (대부분의 Windows 11 시스템에 기본 설치되어 있음)

## 빌드 및 실행

```bash
dotnet build MultiStreamViewer.sln
dotnet run --project MultiStreamViewer/MultiStreamViewer.csproj
```

또는 Visual Studio에서 `MultiStreamViewer.sln`을 열어 F5로 실행합니다. 앱이 실행 중이면 exe가 잠겨 빌드가 실패하므로 먼저 종료해야 합니다.

## 프로젝트 구조

```
MultiStreamViewer/
├── MainWindow.xaml(.cs)          # 메인 창: 분할 레이아웃/모양, 전체화면, 연결·해제, 링크 추출, 오버레이 기능 연결
├── Views/
│   ├── StreamView.xaml(.cs)      # 방송 1개를 표시하는 슬롯 (WebView2 래핑)
│   ├── OverlayMenuWindow         # 방송 화면 위에 뜨는 반투명 원형 메뉴
│   ├── SlotPickerWindow          # 슬롯을 클릭해 고르는 투명 선택 창 (위치 바꾸기, 즐겨찾기 열기)
│   ├── FullscreenExitWindow      # 전체화면에서 화면 맨 위에 나타나는 ✕ 버튼
│   ├── FavoritesWindow           # 즐겨찾기 목록 (추가/삭제/선택)
│   └── WindowNative.cs           # 오버레이 창들이 공유하는 Win32 호출
├── Models/
│   ├── StreamInfo.cs             # 슬롯별 방송 상태 모델
│   └── Favorite.cs               # 즐겨찾기 항목
├── Services/
│   ├── PlatformDetector.cs       # URL을 보고 플랫폼 판별
│   └── FavoritesStore.cs         # 즐겨찾기 JSON 저장/불러오기
└── Extensions/                   # WebView2에 설치되는 확장 프로그램 (네이버 영상 재생 보조, Edge용만 설치)
```

WebView2는 별도의 창(HWND)으로 그려져서 같은 창 안의 WPF 요소를 위에 겹칠 수 없습니다. 방송 화면 위에 뜨는 메뉴, 슬롯 선택 창, 전체화면 ✕ 버튼은 모두 메인 창이 소유하는 투명한 별도 창으로 만들어져 있습니다.
