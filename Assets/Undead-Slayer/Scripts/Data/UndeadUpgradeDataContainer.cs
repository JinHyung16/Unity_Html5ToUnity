using System.Text;

namespace JinHyung.Data
{
    /// <summary>
    /// 레벨업 카드 풀 [소스 직독 · 회차 9].
    ///
    /// <para>
    /// ★ <b>가중치는 «상태에 따라» 변한다</b> — 그래서 표에는 <b>가중치의 «갈래»</b>만 두고
    /// 실제 값은 여기서 계산한다. 표에 숫자를 박으면 「번개가 없으면 0」이 사라진다.
    /// </para>
    /// </summary>
    public class UndeadUpgradeDataContainer : DictionaryContainer<int, UndeadUpgradeData>
    {
        /// <summary>원본 <c>ol</c> 풀의 장수.</summary>
        private const int OriginRowCount = 8;

        public override string Name
        {
            get { return "UndeadUpgradeTable"; }
        }

        /// <summary>
        /// 지금 상태에서 이 카드가 <b>뽑힐 가중치</b>. <c>0</c> 이면 <b>후보에 아예 안 든다</b> [소스].
        /// </summary>
        public static double Weight(UndeadUpgradeData data, int lightnings, int lightningDamage, int level)
        {
            if (data == null)
                return 0.0;

            switch (data.WeightKind)
            {
                case EUndeadWeightKind.Constant1:
                    return 1.0;

                case EUndeadWeightKind.Constant05:
                    return 0.5;

                case EUndeadWeightKind.LightningCount:
                    return lightnings < 1 ? 0.0 : 1.0 / (1.0 + lightnings) * 0.25;

                case EUndeadWeightKind.LightningOwned075:
                    return lightnings < 1 ? 0.0 : 0.75;

                case EUndeadWeightKind.LightningDamage:
                    if (lightnings < 1)
                        return 0.0;

                    return lightningDamage < 3 && level >= 6 ? 1.5 : 0.75;

                default:
                    return 0.0;
            }
        }

        public UndeadUpgradeData GetByCode(string code)
        {
            for (int i = 0; i < AllValues.Count; i++)
            {
                if (AllValues[i].Code == code)
                    return AllValues[i];
            }

            return null;
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 풀 {OriginRowCount}장");

            for (int i = 0; i < AllValues.Count; i++)
            {
                UndeadUpgradeData u = AllValues[i];

                if (string.IsNullOrEmpty(u.Code) || string.IsNullOrEmpty(u.IconAddress))
                    sb.AppendLine($"{u.Id}행: 코드/아이콘 주소가 비었다");

                if (string.IsNullOrEmpty(u.ColorHex) || u.ColorHex.Length != 6)
                    sb.AppendLine($"{u.Code}: 카드 색(ColorHex)이 비었거나 6자리가 아니다");

                // ⚠ 문구는 «키»여야 한다 — 한국어를 그대로 넣으면 다른 언어에서 바뀌지 않는다.
                if (string.IsNullOrEmpty(u.TargetTextKey) || string.IsNullOrEmpty(u.StatTextKey))
                    sb.AppendLine($"{u.Code}: 문구 키가 비었다");

                // 「지금 값의 배수」로 붙는 갈래는 Amount 를 안 쓴다 — 0 이어도 된다.
                bool usesAmount = u.ApplyKind == EUndeadApplyKind.AddToStat;

                if (usesAmount && u.Amount <= 0.0)
                    sb.AppendLine($"{u.Code}: 더하는 양이 {u.Amount} 다 — 카드를 먹어도 아무것도 안 변한다");
            }

            // ★ 「처음엔 4장만 뜬다」가 지켜지는지 — 번개 4종은 번개가 없으면 가중치 0 이어야 한다.
            int firstRunCandidates = 0;

            for (int i = 0; i < AllValues.Count; i++)
            {
                if (Weight(AllValues[i], 0, 2, 1) > 0.0)
                    firstRunCandidates++;
            }

            if (firstRunCandidates != 4)
                sb.AppendLine($"번개가 0 일 때 후보가 {firstRunCandidates}장 — 원본은 4장이다 "
                              + "(번개 4종은 얻기 전엔 가중치 0)");

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
