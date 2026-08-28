namespace JinHyung.UI
{
    /// <summary>
    /// 창의 열림 상태.
    ///
    /// <para>
    /// 최소 세트라 둘뿐이다. 여닫기가 <b>즉시</b> 끝나기 때문이다.
    /// 열고 닫는 데 연출(페이드·슬라이드)이 붙어 시간이 걸리기 시작하면
    /// 그때 <c>Opening</c>·<c>Closing</c> 을 추가한다 —
    /// 전이 상태가 없으면 연출 도중에 또 누르는 입력을 막을 수가 없다.
    /// </para>
    /// </summary>
    public enum EWindowState
    {
        Closed = 0, // 닫혀 있다 (GameObject 가 꺼져 있다)
        Opened = 1, // 열려 있다
    }
}
