namespace JinHyung.UI
{
    /// <summary>
    /// 창이 뜨는 <b>대역(밴드)</b>.
    ///
    /// <para>
    /// <b>같은 밴드 안에서는 나중에 연 것이 위</b>다 — 프레임워크가 여는 순서대로 앞으로 보낸다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>밴드 안 순위 상수를 따로 만들지 않는다.</b>
    /// 「무조건 맨 위」가 필요하면 값을 만들지 말고 <b>밴드를 하나 올린다.</b>
    /// 밴드가 이미 하는 일을 두 번째 축으로 흉내내면, 그 값이 절대 depth 처럼 보이는데
    /// 실제로는 밴드 안 상대 순위라 아무도 못 읽는다.
    /// </para>
    ///
    /// <para>
    /// 최소 세트라 둘뿐이다. 필요해지면 그때 추가한다 (HUD · GlobalPopup · Modal · Toast).
    /// </para>
    /// </summary>
    public enum EWindowType
    {
        Normal = 0, // 기본 전체 화면
        Popup = 1,  // 기본 UI 위에 뜨는 팝업
    }
}
