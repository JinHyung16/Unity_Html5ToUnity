namespace JinHyung.UI.Fx
{
    /// <summary>
    /// 같은 오브젝트의 배율에 «곱해지는» 한 몫. 펄스 · 호버 · 상태가 각각 제 몫을 내고
    /// <see cref="UiScaleStack"/> 이 곱해서 <c>localScale</c> 에 넣는다 — 원본도 <c>hover × pulse</c> 곱이다.
    /// </summary>
    public interface IUiScaleFactor
    {
        float ScaleFactor { get; }
    }

    /// <summary>
    /// <b>켜지면 다른 몫을 덮는</b> 배율 — 곱이 아니라 «상태»인 자리를 위한 것.
    ///
    /// <para>
    /// ★ 전부가 곱은 아니다. 원본에는 두 종류가 있다 —
    /// ① <b>곱</b>: 시작 버튼 <c>hover × pulse</c> (숨쉬는 위에 호버가 얹힌다)
    /// ② <b>상태</b>: 선택 카드 <c>scale.set(선택 ? 1.08 : 1)</c> — 호버 값을 «덮어쓴다».
    /// 둘을 구별하지 않고 전부 곱하면 고른 카드가 <c>1.08 × 1.04 = 1.123</c> 로 커진다 [회차 12 실측].
    /// </para>
    /// </summary>
    public interface IUiScaleExclusive : IUiScaleFactor
    {
        /// <summary>지금 이 몫이 «덮고 있나».</summary>
        bool ExclusiveActive { get; }
    }
}
