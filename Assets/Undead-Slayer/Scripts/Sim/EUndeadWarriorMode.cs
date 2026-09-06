namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 전사의 표시 상태 [소스 <c>setMode</c> — <c>"lay"</c> · <c>"idle"</c> · <c>"run"</c>].
    ///
    /// <para>
    /// ★ <b>모드는 시뮬이 든다.</b> 뷰가 「위치가 바뀌었나」로 추론하면 프레임 순서에 따라 흔들리고,
    /// 같은 판단이 두 곳에 생긴다. 원본도 <c>setMode</c> 로 «상태»를 들고 그림을 고른다.
    /// </para>
    ///
    /// <para>⚠ 모드마다 <b>시트도 재생 속도도 다르다</b> — lay 3fps · idle/run 9fps [소스 <c>animationSpeed</c>].</para>
    /// </summary>
    public enum EUndeadWarriorMode
    {
        /// <summary>구조되기 전 — 쓰러져 있다.</summary>
        Lay,

        /// <summary>구조된 뒤 멈춰 있다.</summary>
        Idle,

        /// <summary>구조된 뒤 히어로를 따라 달린다.</summary>
        Run,
    }
}
