namespace JinHyung.Data
{
    /// <summary>업그레이드가 «수치를 어떻게» 바꾸나 [소스 직독 · <c>applyReward</c>].</summary>
    public enum EUndeadApplyKind
    {
        /// <summary><c>수치 += Amount</c>. ⚠ <b>곱셈이 아니다</b> — 두 번 먹으면 1 → 1.3 → 1.6 이다.</summary>
        AddToStat,

        /// <summary><c>수치 += 지금 수치</c> (=2배). 「피해량 +100%」가 이것이다.</summary>
        DoubleStat,

        /// <summary><c>수치 += round(0.5 × 지금 수치)</c>. 「번개 피해 +50%」.</summary>
        AddHalfOfStat,
    }

    /// <summary>카드가 «뽑힐 확률»을 무엇이 정하나 [소스 직독 · <c>weight()</c>].</summary>
    public enum EUndeadWeightKind
    {
        /// <summary>항상 1.</summary>
        Constant1,

        /// <summary>항상 0.5.</summary>
        Constant05,

        /// <summary>번개가 없으면 0, 있으면 <c>1/(1+번개수) × 0.25</c> — <b>많을수록 덜 나온다</b>.</summary>
        LightningCount,

        /// <summary>번개가 없으면 0, 있으면 0.75.</summary>
        LightningOwned075,

        /// <summary>번개가 없으면 0 · 피해 &lt; 3 이고 레벨 ≥ 6 이면 1.5 · 그 밖엔 0.75.</summary>
        LightningDamage,
    }

    /// <summary>
    /// 레벨업 카드 한 장 [소스 직독 · 회차 9].
    ///
    /// <para>
    /// ★★ 원본 풀은 <b>8종</b>이다 — 회차 1~8 에는 4종만 있었다(화면에서 본 것만 옮겼다).
    /// 번개 4종은 <b>번개를 얻기 전에는 가중치가 0</b> 이라 «화면에 안 뜬다» — 그래서 못 봤다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>「안 보였다」와 「없다」는 다른 말이다.</b> 가중치가 0 인 항목은 관측으로 못 찾는다.
    /// </para>
    /// </summary>
    public class UndeadUpgradeData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>원본 옵션 id.</summary>
        public string Code { get; set; }

        /// <summary>카드 «윗줄» 문구 키 (<c>bullets</c> · <c>hero</c> · <c>lightning</c>).</summary>
        public string TargetTextKey { get; set; }

        /// <summary>카드 «아랫줄» 문구 키 (<c>fireRate</c> · <c>damage</c> …).</summary>
        public string StatTextKey { get; set; }

        /// <summary>
        /// 아랫줄에 붙는 표시 (<c>+30%</c>). ⚠ <b>표시일 뿐 계산이 아니다</b> —
        /// 실제 변화는 <see cref="ApplyKind"/> 와 <see cref="Amount"/> 가 정한다.
        /// </summary>
        public string DisplayPercent { get; set; }

        /// <summary>
        /// 표시를 <b>앞에</b> 붙이나 [소스 — <c>al(값, 이름, true)</c>].
        /// <para>「+1 발사체」만 참이고 나머지는 「발사 속도 +30%」처럼 뒤에 붙는다.</para>
        /// </summary>
        public bool DisplayPrefix { get; set; }

        public EUndeadApplyKind ApplyKind { get; set; }

        public double Amount { get; set; }

        public EUndeadWeightKind WeightKind { get; set; }

        public string IconAddress { get; set; }

        /// <summary>
        /// 카드 바탕 틴트 [소스 <c>ol[].color</c>] — 카드마다 색이 다르다 (회차 1 관측 「주황·빨강·청록·초록」의 정체).
        /// <para>읽는 곳: <c>UndeadLevelUpWindow.Show</c> 가 카드 틀 <c>Image.color</c> 에 넣는다.</para>
        /// </summary>
        public string ColorHex { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
