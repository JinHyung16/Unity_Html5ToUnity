namespace JinHyung.Data
{
    /// <summary>
    /// 데이터 한 행의 표식.
    ///
    /// <para>
    /// 행 클래스는 <b>순수 데이터만</b> 든다 — 계산·조회·상태 변경이 들어가면
    /// 「데이터가 로직을 갖는」 상태가 되고, 그때부터 원본과 대조할 기준이 사라진다.
    /// </para>
    /// </summary>
    public interface IData
    {
    }
}
