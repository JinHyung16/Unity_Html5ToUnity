namespace JinHyung.Data
{
    /// <summary>
    /// 문구 한 줄. <b>원본 로케일 모듈을 런타임에서 직독한 것</b>이다 —
    /// 「이런 뜻이겠지」로 지어낸 번역이 아니라 <b>원본이 실제로 쓰는 문자열</b>이다.
    ///
    /// <para>
    /// ★ 이 표가 있으므로 <b>다른 어느 곳에도 화면 문구를 «문자열 리터럴»로 적지 않는다.</b>
    /// 두 벌이 되면 어느 쪽이 원본인지 사라진다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 파라미터 문구가 있다 — <c>taskProgressFeedback</c> 은 <c>{current}</c> · <c>{goal}</c> 을
    /// 치환한다. <b>치환식 그대로 옮긴다.</b>
    /// </para>
    /// </summary>
    public class UndeadTextData : IData, IDataKey<string>
    {
        public int Id { get; set; }

        /// <summary>원본 로케일 키 (<c>start</c> · <c>revive</c> …).</summary>
        public string Code { get; set; }

        /// <summary>한국어 표기 (원본 <c>?iso_lang=ko</c> 기준) [실측].</summary>
        public string Ko { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
