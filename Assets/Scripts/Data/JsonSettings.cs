using Newtonsoft.Json;

namespace JinHyung.Data
{
    /// <summary>
    /// JSON 역직렬화 설정.
    ///
    /// <para>
    /// ★ <b>라이브러리 접점 ①.</b> 이 프로젝트에서 <c>Newtonsoft</c> 이름이 나오는 파일은
    /// <b>이 파일과 <see cref="DataContainer{TKey,TValue}"/> 둘뿐</b>이어야 한다.
    /// 세 번째 파일에 나오면 격리가 깨진 것이다 — 그때부터는 라이브러리를 못 바꾼다.
    /// </para>
    ///
    /// <para>
    /// 행 클래스에 <c>[JsonProperty]</c> 를 붙이지 않는다.
    /// <b>JSON 키와 프로퍼티 이름을 같게 맞추면</b> 속성 없이도 붙는다.
    /// </para>
    /// </summary>
    public static class JsonSettings
    {
        public static readonly JsonSerializerSettings Default = new JsonSerializerSettings
        {
            // ★ JSON 에 있는데 클래스에 없는 키를 만나면 «에러»다.
            //    조용히 무시하면 「안 옮긴 필드」가 어떤 검증에도 안 걸린다 —
            //    이관에서 가장 조용한 결함이 바로 이것이다.
            MissingMemberHandling = MissingMemberHandling.Error,

            // 「값이 없다(null)」와 「0이다」를 구분해야 폴백 계수 판정이 무너지지 않는다.
            NullValueHandling = NullValueHandling.Include,

            // 날짜 문자열을 임의로 DateTime 으로 바꾸지 않는다 — 원본 문자열 그대로 든다.
            DateParseHandling = DateParseHandling.None,
        };
    }
}
