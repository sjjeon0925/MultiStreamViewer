namespace MultiStreamViewer.Models;

// 전체화면에서 16:10 등 모니터 비율 차이로 생기는 여백 정렬 방식
public enum StreamAlignment
{
    Fill = 0,   // 화면 전체를 채움 (기본)
    Top = 1,    // 위에 붙임 (공백은 아래)
    Center = 2, // 가운데 (공백은 위아래로 나눔)
    Bottom = 3  // 아래에 붙임 (공백은 위)
}

// 슬롯 클릭 선택 모드 종류
public enum PickMode
{
    None = 0,
    Swap = 1,
    OpenFavorite = 2
}
