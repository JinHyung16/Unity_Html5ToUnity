namespace JinHyung.Data
{
    /// <summary>
    /// 레벨 게이지 목표의 <b>«식»</b> — 표가 아니라 <b>계수 한 벌</b>이다 [소스 직독 · 회차 9].
    ///
    /// <para>
    /// 원본은 목표를 행으로 들고 있지 않고 <b>레벨에서 계산</b>한다:
    /// <c>2 → 15</c> · <c>3 → 40</c> · 그 밖에는
    /// <c>round(30 × t × (1 + ⌊t/5⌋) × (1 + 0.5·⌊t/10⌋))</c>.
    /// </para>
    ///
    /// <para>
    /// ★ 그래서 <b>「레벨 11+ 목표 미측정」이 원리적으로 사라진다</b> — 표를 못 채운 게 아니라
    /// 애초에 표가 아니었다. 관측치 <c>15 · 40 · 120 · 300 · 540 · 1350</c> 이 전부 이 식과 맞는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>관측한 값을 행으로 다시 적지 않는다</b> — 식과 표를 같이 두면 둘이 갈릴 때
    /// 어느 쪽이 정본인지 사라진다(재발방지 #105 「유도되는 값을 상수로 박지 않는다」).
    /// </para>
    /// </summary>
    public class UndeadLevelData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>레벨 2 에 닿는 데 필요한 값 — 식 밖의 «예외» 둘 중 하나 [소스].</summary>
        public int Level2Goal { get; set; }

        /// <summary>레벨 3 에 닿는 데 필요한 값 [소스].</summary>
        public int Level3Goal { get; set; }

        /// <summary>기본 계수 [소스 — 30].</summary>
        public double BaseFactor { get; set; }

        /// <summary>이 레벨마다 «단계»가 하나 오른다 [소스 — 5].</summary>
        public int TierEvery { get; set; }

        /// <summary>단계 하나가 더하는 배수 [소스 — 1].</summary>
        public double TierStep { get; set; }

        /// <summary>이 레벨마다 «가파른 단계»가 하나 오른다 [소스 — 10].</summary>
        public int SteepEvery { get; set; }

        /// <summary>가파른 단계 하나가 더하는 배수 [소스 — 0.5].</summary>
        public double SteepStep { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
