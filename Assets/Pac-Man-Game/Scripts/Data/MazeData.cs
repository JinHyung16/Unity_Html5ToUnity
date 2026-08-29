namespace JinHyung.Data
{
    /// <summary>
    /// 미로 한 «행». 원본 <c>levelLayout</c> (<c>script.js:19~50</c>) 의 문자열 한 줄이다.
    ///
    /// <para>
    /// 문자 뜻 — <c>0</c> 빈칸 · <c>1</c> 벽 · <c>2</c> 펠릿 · <c>3</c> 파워펠릿.
    /// </para>
    ///
    /// <para>
    /// ★ <b>행 순서가 곧 <c>row</c> 다.</b> 원본이 <c>levelLayout[r][c]</c> 로 읽으므로
    /// 우리도 <c>AllValues[r]</c> 가 같은 행이어야 한다.
    /// </para>
    /// </summary>
    public class MazeData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>28글자 문자열. 길이가 <c>Cols</c> 와 다르면 Validate 가 잡는다.</summary>
        public string Row { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
