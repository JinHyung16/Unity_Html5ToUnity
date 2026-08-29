namespace JinHyung.Data
{
    /// <summary>
    /// 유령 한 마리의 «시작 상태». 원본 <c>ghosts</c> 배열 (<c>script.js:68~71</c>) 2행이다.
    ///
    /// <para>
    /// ⚠ 원본 유령 객체에는 <b>속도가 없다</b> — 이동 함수 안에 6 이 박혀 있다.
    /// 그래서 여기에도 속도가 없고, <see cref="PacConfigData.GhostSpeed"/> 가 정본이다.
    /// </para>
    /// </summary>
    public class GhostData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        public int StartCol { get; set; }
        public int StartRow { get; set; }

        public int DirX { get; set; }
        public int DirY { get; set; }

        /// <summary>원본 <c>color</c> 를 `#` 없이 적은 것. 유령은 «모양이 같고 색만 다르다».</summary>
        public string ColorHex { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
