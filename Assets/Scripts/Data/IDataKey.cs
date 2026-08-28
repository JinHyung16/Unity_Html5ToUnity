namespace JinHyung.Data
{
    /// <summary>
    /// 행이 자기 키를 스스로 안다.
    ///
    /// <para>
    /// 컨테이너가 「어느 컬럼이 키인가」를 몰라도 되게 만든다 —
    /// 키가 바뀌면 <b>행 클래스 한 곳만</b> 고친다.
    /// </para>
    /// </summary>
    public interface IDataKey<out TKey>
    {
        TKey Key { get; }
    }
}
