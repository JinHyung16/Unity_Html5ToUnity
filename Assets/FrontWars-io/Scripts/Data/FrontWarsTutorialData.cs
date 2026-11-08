namespace JinHyung.Data
{
    /// <summary>튜토리얼 판 설정 `[소스] BVamDUD6.js:tutorial:!0` — 1행.</summary>
    public class FrontWarsTutorialData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        public string Map { get; set; }
        public bool CompactMap { get; set; }
        public string Difficulty { get; set; }
        public string GameMode { get; set; }
        public string EconomyMode { get; set; }
        public bool DisableNations { get; set; }
        public int Bots { get; set; }
        public bool InfiniteGold { get; set; }
        public bool InfiniteTroops { get; set; }
        public bool InstantBuild { get; set; }
        public bool DonateGold { get; set; }
        public bool DonateTroops { get; set; }

        /// <summary>금지 유닛 코드들, `;` 로 나눈다.</summary>
        public string DisabledUnits { get; set; }

        public int SpawnPhaseTurns { get; set; }
        public bool DisableAlliances { get; set; }

        /// <summary>시작 병력 설정값 — 실제 병력은 이 값 × 10 `[소스] startingTroops*10`.</summary>
        public int StartingTroops { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
