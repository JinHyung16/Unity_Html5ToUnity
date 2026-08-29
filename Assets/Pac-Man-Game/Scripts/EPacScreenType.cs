namespace JinHyung.PacMan
{
    /// <summary>
    /// 이 게임의 화면 종류.
    ///
    /// <para>
    /// ⚠ <b>원본에는 화면이 «하나»뿐이다.</b> 시작 화면도 결과 화면도 없고,
    /// 로드되면 바로 게임이며 끝나면 <c>alert</c> 한 줄이다
    /// (<c>01_게임플로우.md</c> 「원본에 없는 화면」).
    /// </para>
    ///
    /// <para>
    /// 그래서 값이 하나다. <b>「나중에 쓸 것 같아서」 화면을 늘리지 않는다</b> —
    /// 늘리는 순간 「원본에 없는 창」이 되고 플로우 diff 가 깨진다.
    /// </para>
    /// </summary>
    public enum EPacScreenType
    {
        None = 0,
        Game = 1,
    }
}
