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
    }
}
