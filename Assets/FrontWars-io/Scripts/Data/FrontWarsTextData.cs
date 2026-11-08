namespace JinHyung.Data
{
    /// <summary>
    /// 화면 문구 한 줄 — 키 · 한국어.
    /// <para>
    /// 원본 ko.json 을 옮기지 않았다 (R11 · 의도된 차이 9). 짧은 이름표(병력 · 골드 · 공격)는 누구나 쓰는 낱말이라 같고,
    /// 문장 문구는 우리가 새로 썼다. 자리 표시 {0} {1} 은 코드가 채운다.
    /// </para>
    /// </summary>
    public class FrontWarsTextData : IData, IDataKey<string>
    {
        public string Key { get; set; }

        public string Ko { get; set; }
    }
}
