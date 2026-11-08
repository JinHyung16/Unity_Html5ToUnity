namespace JinHyung.Data
{
    /// <summary>
    /// 관계 이름 `[소스] Cq3gr8XO.js:relationFromValue(e){` · 네이션 동맹 점수 가중치 `[소스] Tf={`.
    /// <para>관계 값(−100~100)이 <see cref="RelationBelow"/> 보다 작은 첫 행이 그 이름이다. 마지막 행은 나머지 전부(1000 = 끝없음).</para>
    /// </summary>
    public class FrontWarsAttitudeData : IData, IDataKey<string>
    {
        /// <summary>Hostile · Distrustful · Neutral · Friendly.</summary>
        public string Code { get; set; }

        /// <summary>원본 enum 순번 — 「관계 ≥ 중립」 같은 비교에 쓴다.</summary>
        public int Order { get; set; }

        public int RelationBelow { get; set; }

        public double AllianceWeight { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
