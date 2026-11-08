namespace JinHyung.Data
{
    /// <summary>
    /// 핵 폭발 반경 `[소스] Cq3gr8XO.js:nukeMagnitudes(` · 네이션 인지 비용 가산 `[소스] Kf={`.
    /// </summary>
    public class FrontWarsNukeData : IData, IDataKey<string>
    {
        /// <summary>AtomBomb · HydrogenBomb · MIRVWarhead.</summary>
        public string Code { get; set; }

        /// <summary>이 반경 안은 확정 파괴.</summary>
        public int InnerRadius { get; set; }

        /// <summary>inner~outer 사이는 1/2 확률 파괴.</summary>
        public int OuterRadius { get; set; }

        /// <summary>네이션이 쏠 때마다 인지 비용에 곱하는 % (탄두는 해당 없음 = 100).</summary>
        public int AiCostMarkupPercent { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
