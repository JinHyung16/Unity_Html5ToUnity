namespace JinHyung.Data
{
    /// <summary>
    /// <see cref="GameRoot"/> 의 Blumgi Bounce 조각.
    /// 공용 <c>GameRoot.cs</c> 에는 특정 게임의 테이블 이름을 적지 않는다.
    /// </summary>
    public sealed partial class GameRoot
    {
        public BlumgiConfigDataContainer BlumgiConfigDataContainer
        {
            get { return GetContainer<BlumgiConfigDataContainer>(); }
        }

        public BlumgiLevelDataContainer BlumgiLevelDataContainer
        {
            get { return GetContainer<BlumgiLevelDataContainer>(); }
        }

        public BlumgiBlockDataContainer BlumgiBlockDataContainer
        {
            get { return GetContainer<BlumgiBlockDataContainer>(); }
        }
    }
}
