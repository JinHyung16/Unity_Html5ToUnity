namespace JinHyung.Data
{
    /// <summary>
    /// 맵 네이션 한 명 — 이름 · 국기 · 스폰 기준 좌표는 맵 manifest(OpenFront 와 FrontWars 동일),
    /// 강도는 FrontWars 에만 있는 값이다 `[실측] frontwars.io/maps/australia/manifest.json`.
    /// <para>강도는 네이션 시작 병력에 곱해진다 `[소스] Cq3gr8XO.js:startManpower(` — nation.strength ?? 1.</para>
    /// </summary>
    public class FrontWarsNationData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>맵 id (Australia …).</summary>
        public string Map { get; set; }

        public string Name { get; set; }

        public string Flag { get; set; }

        /// <summary>스폰 기준 칸 — 네이션은 이 ±25 안에서 자리를 고른다 `[소스] randomLand(){`.</summary>
        public int X { get; set; }

        public int Y { get; set; }

        public int Strength { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
