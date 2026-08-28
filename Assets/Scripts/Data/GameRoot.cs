namespace JinHyung.Data
{
    /// <summary>
    /// 데이터 조회의 <b>단일 창구</b>.
    ///
    /// <para>
    /// 게임 코드는 <c>DataManager.Instance.GetContainer&lt;T&gt;()</c> 를 직접 부르지 않는다.
    /// 여기 프로퍼티만 쓴다 — 그래야 「어떤 테이블이 쓰이는가」가 <b>파일 하나로 보인다.</b>
    /// </para>
    ///
    /// <para>
    /// ★ <c>partial</c> 이다. <b>게임별 컨테이너 프로퍼티는 게임 폴더의 partial 에 적는다</b> —
    /// 공용 파일에 특정 게임의 테이블 이름을 적으면 다음 게임에서 오염이 된다.
    /// </para>
    ///
    /// <code>
    /// // Assets/&lt;게임명&gt;/Scripts/GameRoot.&lt;게임명&gt;.cs
    /// namespace JinHyung.Data
    /// {
    ///     public sealed partial class GameRoot
    ///     {
    ///         public CandyDataContainer CandyDataContainer
    ///         {
    ///             get { return GetContainer&lt;CandyDataContainer&gt;(); }
    ///         }
    ///     }
    /// }
    /// </code>
    /// </summary>
    public sealed partial class GameRoot
    {
        private static GameRoot _instance;

        public static GameRoot Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GameRoot();

                return _instance;
            }
        }

        private GameRoot()
        {
        }

        private static T GetContainer<T>()
            where T : class, IDataContainer
        {
            return DataManager.Instance.GetContainer<T>();
        }
    }
}
