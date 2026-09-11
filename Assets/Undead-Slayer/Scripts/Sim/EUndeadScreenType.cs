namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 이 게임의 화면 종류. <b>원본 플로우 그래프 그대로</b>다
    /// (`01_게임플로우.md` — 원본에 없는 화면을 여기 추가하지 않는다).
    /// </summary>
    public enum EUndeadScreenType
    {
        /// <summary>바이옴 진입 — 맵 위에 「시작」 버튼 하나.</summary>
        Ready,

        /// <summary>전투.</summary>
        Battle,

        /// <summary>레벨 업 — 카드 3장. ⚠ <b>이 동안 게임이 정지한다</b> [실측].</summary>
        LevelUp,

        /// <summary>과제 완료 — 「사원으로」 / 「계속」.</summary>
        TaskComplete,

        /// <summary>새 무기 해금 — 「확인」.</summary>
        WeaponUnlock,

        /// <summary>사망 — 「부활?」 + 카운트다운 8.</summary>
        Revive,

        /// <summary>
        /// <b>로비</b> — 과제 NPC 둘과 포털 둘이 있는 방 [소스 <c>lobby</c> 씬].
        ///
        /// <para>
        /// ⚠ <b>처음부터 열리지 않는다</b> [소스 <c>isLobbyUnlocked</c>] —
        /// <b>부활 없이 한 번 죽어야</b> 열린다. 그전에는 바로 <see cref="Ready"/> 다.
        /// </para>
        ///
        /// <para>
        /// ⚠⚠ <b>맨 «뒤»에 둔다.</b> 열거형의 <b>첫 값이 곧 «시작 화면»</b>이라
        /// (플로우가 <c>default</c> 로 서고 <b>같은 화면으로의 재진입은 무시</b>한다),
        /// 앞에 끼우면 <b>로비로 «전이»가 통째로 안 먹는다</b> — 그런데 오류는 안 난다.
        /// </para>
        /// </summary>
        Lobby,
    }
}
