namespace JinHyung.Data
{
    /// <summary>
    /// <see cref="GameRoot"/> 의 게임별 조각.
    ///
    /// <para>
    /// 공용 <c>GameRoot.cs</c> 에는 <b>특정 게임의 테이블 이름을 적지 않는다</b> —
    /// 다음 게임에서 오염이 되기 때문이다. 게임별 프로퍼티는 이 partial 에 모은다.
    /// </para>
    /// </summary>
    public sealed partial class GameRoot
    {
        public CandyDataContainer CandyDataContainer
        {
            get { return GetContainer<CandyDataContainer>(); }
        }

        public GameConfigDataContainer GameConfigDataContainer
        {
            get { return GetContainer<GameConfigDataContainer>(); }
        }
    }
}
