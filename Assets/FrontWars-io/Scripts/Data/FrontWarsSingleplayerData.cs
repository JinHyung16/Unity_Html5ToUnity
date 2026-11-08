namespace JinHyung.Data
{
    /// <summary>
    /// 싱글 설정 창의 기본값 `[소스] BVamDUD6.js:selectedDifficulty` — 1행.
    /// <para>골드 배율 · 시작 골드 · 게임 시간은 기본 «꺼짐»이고, 켜면 여기 값으로 시작한다. 네이션 수 기본은 맵 기본값이라 표에 두지 않는다.</para>
    /// </summary>
    public class FrontWarsSingleplayerData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        public string Map { get; set; }
        public string Difficulty { get; set; }
        public string GameMode { get; set; }
        public string EconomyMode { get; set; }
        public int NumberOfTeams { get; set; }
        public int Bots { get; set; }
        public int BotsMax { get; set; }
        public int NationsMax { get; set; }
        public bool InstantBuild { get; set; }
        public bool DonateGold { get; set; }
        public bool DonateTroops { get; set; }
        public bool InfiniteGold { get; set; }
        public bool InfiniteTroops { get; set; }
        public bool CompactMap { get; set; }
        public bool WaterNukes { get; set; }
        public bool RandomSpawn { get; set; }
        public bool DisableAlliances { get; set; }
        public double GoldMultiplier { get; set; }
        public int StartingGoldMillions { get; set; }
        public int GameLengthMinutes { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
