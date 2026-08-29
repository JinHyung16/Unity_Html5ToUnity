namespace JinHyung.Data
{
    /// <summary>
    /// <see cref="GameRoot"/> 의 게임별 조각.
    /// 공용 <c>GameRoot.cs</c> 에는 특정 게임의 테이블 이름을 적지 않는다.
    /// </summary>
    public sealed partial class GameRoot
    {
        public MazeDataContainer MazeDataContainer
        {
            get { return GetContainer<MazeDataContainer>(); }
        }

        public PacConfigDataContainer PacConfigDataContainer
        {
            get { return GetContainer<PacConfigDataContainer>(); }
        }

        public GhostDataContainer GhostDataContainer
        {
            get { return GetContainer<GhostDataContainer>(); }
        }
    }
}
