namespace JinHyung.Data
{
    /// <summary><see cref="GameRoot"/> 의 FrontWars-io 조각. 공용 파일에는 이 게임의 표 이름을 적지 않는다.</summary>
    public sealed partial class GameRoot
    {
        public FrontWarsConfigDataContainer FrontWarsConfigDataContainer
        {
            get { return GetContainer<FrontWarsConfigDataContainer>(); }
        }

        public FrontWarsUnitDataContainer FrontWarsUnitDataContainer
        {
            get { return GetContainer<FrontWarsUnitDataContainer>(); }
        }

        public FrontWarsTerrainDataContainer FrontWarsTerrainDataContainer
        {
            get { return GetContainer<FrontWarsTerrainDataContainer>(); }
        }

        public FrontWarsNukeDataContainer FrontWarsNukeDataContainer
        {
            get { return GetContainer<FrontWarsNukeDataContainer>(); }
        }

        public FrontWarsDifficultyDataContainer FrontWarsDifficultyDataContainer
        {
            get { return GetContainer<FrontWarsDifficultyDataContainer>(); }
        }

        public FrontWarsPersonalityDataContainer FrontWarsPersonalityDataContainer
        {
            get { return GetContainer<FrontWarsPersonalityDataContainer>(); }
        }

        public FrontWarsAttitudeDataContainer FrontWarsAttitudeDataContainer
        {
            get { return GetContainer<FrontWarsAttitudeDataContainer>(); }
        }

        public FrontWarsSingleplayerDataContainer FrontWarsSingleplayerDataContainer
        {
            get { return GetContainer<FrontWarsSingleplayerDataContainer>(); }
        }

        public FrontWarsTutorialDataContainer FrontWarsTutorialDataContainer
        {
            get { return GetContainer<FrontWarsTutorialDataContainer>(); }
        }

        public FrontWarsNationDataContainer FrontWarsNationDataContainer
        {
            get { return GetContainer<FrontWarsNationDataContainer>(); }
        }

        public FrontWarsBotNameDataContainer FrontWarsBotNameDataContainer
        {
            get { return GetContainer<FrontWarsBotNameDataContainer>(); }
        }

        public FrontWarsTextDataContainer FrontWarsTextDataContainer
        {
            get { return GetContainer<FrontWarsTextDataContainer>(); }
        }
    }
}
