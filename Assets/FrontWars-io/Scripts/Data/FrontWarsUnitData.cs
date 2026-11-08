namespace JinHyung.Data
{
    /// <summary>
    /// 유닛 한 종류 `[소스] Cq3gr8XO.js:unitInfo(e){`. 순서는 원본 enum 선언 순서다.
    ///
    /// <para>
    /// 비용 규칙 — n = <see cref="CostCountCodes"/> 종류마다 min(보유 수, 지은 수) 의 합:
    /// <c>Free</c> 0 · <c>Fixed</c> CostBase · <c>Linear</c> min(CostCap, (n+1)×CostBase) · <c>Doubling</c> min(CostCap, 2^n×CostBase).
    /// 규칙 이름을 표에 두고 식은 시뮬 한 곳에 둔다.
    /// </para>
    /// </summary>
    public class FrontWarsUnitData : IData, IDataKey<string>
    {
        public int Order { get; set; }

        /// <summary>원본 enum 키 (`TransportShip` · `AtomBomb` …).</summary>
        public string Code { get; set; }

        /// <summary>원본 enum 표시 문자열 (`Transport` · `Atom Bomb` …) — 명령 · 이벤트가 이 문자열을 쓴다.</summary>
        public string DisplayName { get; set; }

        /// <summary>Free · Fixed · Linear · Doubling.</summary>
        public string CostRule { get; set; }

        public long CostBase { get; set; }

        public long CostCap { get; set; }

        /// <summary>비용의 n 을 셀 종류 코드들, `;` 로 나눈다.</summary>
        public string CostCountCodes { get; set; }

        /// <summary>땅 위에 짓는가 — 칸 주인이 바뀌면 넘어간다.</summary>
        public bool TerritoryBound { get; set; }

        /// <summary>0 이면 체력 없음.</summary>
        public int MaxHealth { get; set; }

        public int Damage { get; set; }

        /// <summary>건설 틱. −1 이면 건설 단계 없이 바로 완성.</summary>
        public int ConstructionTicks { get; set; }

        public bool Upgradable { get; set; }

        public bool CanBuildTrainStation { get; set; }

        public bool Experimental { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
