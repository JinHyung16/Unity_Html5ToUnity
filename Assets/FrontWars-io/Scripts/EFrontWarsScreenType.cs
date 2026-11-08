namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 화면 종류 — 학습 문서 `01_게임플로우.md` 「화면 전이 그래프」 에서 온다.
    /// <para>⚠ 원본에 없는 화면을 더하지 않는다. 판 끝 · 설정 · 설명 화면은 그 단위 학습 뒤에 추가한다.</para>
    /// </summary>
    public enum EFrontWarsScreenType
    {
        /// <summary>메뉴 — 싱글 플레이 · 튜토리얼 · 설명.</summary>
        Menu,

        /// <summary>싱글 설정 창 — 맵 탭 · 규칙 탭.</summary>
        SingleplayerSetup,

        /// <summary>판 — 스폰 단계부터 승리까지.</summary>
        Game
    }
}
