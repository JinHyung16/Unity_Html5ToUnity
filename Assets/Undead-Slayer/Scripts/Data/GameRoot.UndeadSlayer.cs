namespace JinHyung.Data
{
    /// <summary>
    /// <see cref="GameRoot"/> 의 Undead Slayer 조각.
    /// 공용 <c>GameRoot.cs</c> 에는 특정 게임의 테이블 이름을 적지 않는다.
    /// </summary>
    public sealed partial class GameRoot
    {
        public UndeadConfigDataContainer UndeadConfigDataContainer
        {
            get { return GetContainer<UndeadConfigDataContainer>(); }
        }

        public UndeadTextDataContainer UndeadTextDataContainer
        {
            get { return GetContainer<UndeadTextDataContainer>(); }
        }

        public UndeadLevelDataContainer UndeadLevelDataContainer
        {
            get { return GetContainer<UndeadLevelDataContainer>(); }
        }

        public UndeadEnemyDataContainer UndeadEnemyDataContainer
        {
            get { return GetContainer<UndeadEnemyDataContainer>(); }
        }

        public UndeadUpgradeDataContainer UndeadUpgradeDataContainer
        {
            get { return GetContainer<UndeadUpgradeDataContainer>(); }
        }

        public UndeadSkillDataContainer UndeadSkillDataContainer
        {
            get { return GetContainer<UndeadSkillDataContainer>(); }
        }

        public UndeadTaskDataContainer UndeadTaskDataContainer
        {
            get { return GetContainer<UndeadTaskDataContainer>(); }
        }

        /// <summary>바이옴 표 — <b>1 묘지 · 2 겨울 황무지</b>.</summary>
        public UndeadBiomeDataContainer UndeadBiomeDataContainer
        {
            get { return GetContainer<UndeadBiomeDataContainer>(); }
        }

        public UndeadLobbyMapDataContainer UndeadLobbyMapDataContainer
        {
            get { return GetContainer<UndeadLobbyMapDataContainer>(); }
        }

        public UndeadArtDataContainer UndeadArtDataContainer
        {
            get { return GetContainer<UndeadArtDataContainer>(); }
        }

        public UndeadPopupDataContainer UndeadPopupDataContainer
        {
            get { return GetContainer<UndeadPopupDataContainer>(); }
        }
    }
}
