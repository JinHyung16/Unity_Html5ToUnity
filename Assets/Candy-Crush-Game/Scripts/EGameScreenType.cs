namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 이 게임의 화면 종류.
    ///
    /// <para>
    /// 원본에는 화면이 <b>둘뿐</b>이다 (<c>HtmlToUnityLogic/01_게임플로우.md</c>).
    /// <b>원본에 없는 화면을 여기 추가하지 않는다</b> — 결과·일시정지·설정·로딩 화면이 원본에 없다.
    /// </para>
    ///
    /// <para>
    /// 종료 팝업은 화면이 아니라 <c>Popup</c> 밴드의 창이다 — 화면 전이를 만들지 않는다.
    /// </para>
    /// </summary>
    public enum EGameScreenType
    {
        None = 0,       // 아직 아무 화면도 안 열렸다
        ModeSelect = 1, // 원본 #modeSelection
        Game = 2,       // 원본 .scoreBoard + .grid (둘이 같이 켜지고 같이 꺼진다)
    }
}
