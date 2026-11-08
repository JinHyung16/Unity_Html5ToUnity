namespace JinHyung.Data
{
    /// <summary>
    /// 네이션 성격 `[소스] Cq3gr8XO.js:Balanced:{ · function ep(`.
    /// <para>뽑기: 난수 0~99 가 <see cref="PickBelow"/> 보다 작은 첫 행 (표 순서대로).</para>
    /// </summary>
    public class FrontWarsPersonalityData : IData, IDataKey<string>
    {
        public string Code { get; set; }

        /// <summary>누적 문턱 — 32 · 49 · 66 · 83 · 100.</summary>
        public int PickBelow { get; set; }

        public double AttackRateMul { get; set; }
        public double TriggerRatioDelta { get; set; }
        public double ReserveRatioDelta { get; set; }
        public double ExpandRatioDelta { get; set; }
        public double EconomyRatioMul { get; set; }
        public double SamRatioMul { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
