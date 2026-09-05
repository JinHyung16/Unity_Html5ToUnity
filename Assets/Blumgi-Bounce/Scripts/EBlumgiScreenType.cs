namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 이 게임의 화면. <b>원본 실측 전이 그래프에 있는 것만</b> 적는다
    /// (`01_게임플로우.md` 「화면 전이 그래프」).
    ///
    /// <para>
    /// ⚠ 원본에 <b>없는</b> 것을 넣지 않는다 — 실측으로 확인된 «없는 화면»이 셋이다:
    /// <b>레벨 선택 화면 없음</b> · <b>일시정지 화면 없음</b> · <b>게임오버 화면 없음</b>
    /// (한 레벨에서 13발 연속 실패해도 화면이 안 바뀌었다).
    /// </para>
    ///
    /// <para>
    /// 2 PLAYERS 화면은 <b>의도된 차이 #1</b> 로 1차 이관 범위 밖이라 여기 없다.
    /// </para>
    /// </summary>
    public enum EBlumgiScreenType
    {
        None = 0,

        /// <summary>WELCOME — 1 PLAYER / (2 PLAYERS 는 숨김) 카드.</summary>
        Welcome = 1,

        /// <summary>인게임 1P 레벨 화면.</summary>
        Game = 2,

        /// <summary>인게임 ⠿ 버튼으로 들어가는 <b>월드</b> 선택 화면 (레벨 선택이 아니다).</summary>
        WorldSelect = 3,
    }
}
