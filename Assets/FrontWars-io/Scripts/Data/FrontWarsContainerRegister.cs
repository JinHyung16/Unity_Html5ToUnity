namespace JinHyung.Data
{
    /// <summary>
    /// 이 게임이 쓰는 컨테이너 목록. 등록 순서 = 로드 · 검증 순서다.
    /// <para>⚠ 여기 등록하지 않으면 JSON 이 있어도 아무도 안 읽는다 — 그런데 에러도 안 난다.</para>
    /// </summary>
    public static class FrontWarsContainerRegister
    {
        /// <summary>등록하는 표 수 — 검사가 이 수와 대조한다.</summary>
        public const int TableCount = 12;

        public static void RegisterAll()
        {
            DataManager.Instance.Register(new FrontWarsConfigDataContainer());
            DataManager.Instance.Register(new FrontWarsUnitDataContainer());
            DataManager.Instance.Register(new FrontWarsTerrainDataContainer());
            DataManager.Instance.Register(new FrontWarsNukeDataContainer());
            DataManager.Instance.Register(new FrontWarsDifficultyDataContainer());
            DataManager.Instance.Register(new FrontWarsPersonalityDataContainer());
            DataManager.Instance.Register(new FrontWarsAttitudeDataContainer());
            DataManager.Instance.Register(new FrontWarsSingleplayerDataContainer());
            DataManager.Instance.Register(new FrontWarsTutorialDataContainer());
            DataManager.Instance.Register(new FrontWarsNationDataContainer());
            DataManager.Instance.Register(new FrontWarsBotNameDataContainer());
            DataManager.Instance.Register(new FrontWarsTextDataContainer());
        }
    }
}
