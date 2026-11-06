namespace MultiStreamViewer.Models;

// 방송 화면 위 원형 오버레이 메뉴의 기능 버튼 종류 (배치 순서: 위, 오른쪽, 아래, 왼쪽)
public enum OverlayFeature
{
    Favorites = 0, // 위 (★ 즐겨찾기)
    Align = 1,     // 오른쪽 (↕ 전체화면 정렬)
    Swap = 2,      // 아래 (⇄ 슬롯 위치 바꾸기)
    Shape = 3      // 왼쪽 (◫ 분할 화면 모양)
}
