namespace JinHyung.Data
{
    /// <summary>
    /// 사탕 한 종류.
    ///
    /// <para>
    /// 원본: <c>script.js:24~31</c> 의 <c>candyColors</c> — <b>URL 문자열 6개짜리 배열</b>이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b><c>Code</c>(색 이름)는 원본에 없던 값이다.</b> 원본 코드에는 <c>"red"</c> 같은 식별자가
    /// 존재하지 않고 <b>인덱스로만</b> 다룬다 — URL 파일명에서 우리가 뽑아 만든 이름이다.
    /// 다음 회차에 「원본에 색 이름이 있었다」로 인용하면 오염이다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>JSON 의 행 순서가 원본 배열 순서다.</b> 원본은 <c>candyColors[i]</c> 로 뽑으므로
    /// 우리도 <c>AllValues[i]</c> 가 같은 사탕이어야 한다.
    /// <b><c>Id</c> 로 찾지 않는다</b> — <c>Id</c> 는 1부터라 원본 인덱스(0부터)와 한 칸 어긋난다.
    /// 두 벌 번호를 만들지 않으려고 <c>OriginIndex</c> 컬럼을 두지 않았다.
    /// </para>
    /// </summary>
    public class CandyData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>색 이름. 원본 URL 파일명에서 뽑았다 (원본에는 없는 값).</summary>
        public string Code { get; set; }

        /// <summary>스프라이트 어드레서블 주소. 아트는 Artist 가 원본 70×70 을 112×112 로 다시 굽는다.</summary>
        public string SpriteAddress { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
