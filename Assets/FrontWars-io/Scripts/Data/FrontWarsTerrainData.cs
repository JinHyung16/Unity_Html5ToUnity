namespace JinHyung.Data
{
    /// <summary>
    /// 땅 지형 한 종류의 공격 값 `[소스] Cq3gr8XO.js:attackLogic(`.
    /// <para>높이 문턱(10 · 20)은 칸 분류 규칙이라 <c>FrontWarsTerrainMap</c> 코드 상수이고, 여기 <see cref="MinMagnitude"/> 는 대조용이다.</para>
    /// </summary>
    public class FrontWarsTerrainData : IData, IDataKey<string>
    {
        /// <summary>Plains · Highland · Mountain.</summary>
        public string Code { get; set; }

        public int MinMagnitude { get; set; }

        /// <summary>칸마다 공격 병력 손실의 기준 값.</summary>
        public double DefenseValue { get; set; }

        /// <summary>칸마다 쓰는 예산의 기준 값.</summary>
        public double SpeedValue { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
