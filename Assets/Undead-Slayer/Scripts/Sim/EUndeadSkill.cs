namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 액티브 스킬 — <b>과제를 끝내면 보상으로 하나씩 얻는다</b> [소스 <c>Yh</c> 정의 배열].
    ///
    /// <para>
    /// ⚠ 순서가 <b>보상 순서</b>이자 HUD 슬롯 순서다 [소스 <c>Vh</c> 의 <c>order</c>].
    /// </para>
    /// </summary>
    public enum EUndeadSkill
    {
        None = -1,

        /// <summary>돌진 — 350ms 동안 300 만큼 밀고 나간다. 지나친 적을 한 번씩 때린다.</summary>
        Dash = 0,

        /// <summary>화염 자취 — 8초 동안 지나온 길에 불을 남긴다.</summary>
        BlazingTrail = 1,

        /// <summary>겨울 파동 — 4초 동안 <b>적이 통째로 멈춘다</b>.</summary>
        WinterPulse = 2,

        /// <summary>섬광 이동 — 10초 동안 <b>시뮬 배율이 2배</b>가 된다.</summary>
        FlashMove = 3,
    }

    /// <summary>과제 하나의 상태 [소스 <c>taskRecords[id].status</c>].</summary>
    public enum EUndeadTaskStatus
    {
        /// <summary>아직 어느 NPC 에게도 안 붙었다.</summary>
        Unassigned = 0,

        /// <summary>NPC 가 들고 있고 진행 중이다 — <c>goal</c> 이 정해져 있다.</summary>
        Active = 1,

        /// <summary>목표를 넘겼다 — 아직 보상을 안 받았다.</summary>
        Completed = 2,

        /// <summary>보상을 받았다 — 그 슬롯이 비고 다음 과제가 배정된다.</summary>
        Claimed = 3,
    }
}
