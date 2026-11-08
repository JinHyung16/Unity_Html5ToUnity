using System.Text;

namespace JinHyung.Data
{
    /// <summary>네이션 성격 표 — 5행. 표 순서가 뽑기 순서다.</summary>
    public class FrontWarsPersonalityDataContainer : DictionaryContainer<string, FrontWarsPersonalityData>
    {
        private const int OriginRowCount = 5;

        public override string Name
        {
            get { return "FrontWarsPersonalityTable"; }
        }

        /// <summary>난수 0~99 로 성격을 고른다 `[소스] function ep(`.</summary>
        public FrontWarsPersonalityData Pick(int roll)
        {
            for (int i = 0; i < AllValues.Count; i++)
            {
                if (roll < AllValues[i].PickBelow)
                    return AllValues[i];
            }

            return AllValues.Count == 0 ? null : AllValues[AllValues.Count - 1];
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            int previous = 0;

            for (int i = 0; i < AllValues.Count; i++)
            {
                FrontWarsPersonalityData p = AllValues[i];

                if (p.PickBelow <= previous)
                    sb.AppendLine($"{p.Code}: 뽑기 문턱 {p.PickBelow} 이 앞 행 {previous} 보다 크지 않다 — 그 성격이 안 뽑힌다");

                previous = p.PickBelow;

                if (p.AttackRateMul <= 0.0 || p.EconomyRatioMul <= 0.0 || p.SamRatioMul <= 0.0)
                    sb.AppendLine($"{p.Code}: 배율이 0 이하다");
            }

            if (previous != 100)
                sb.AppendLine($"마지막 뽑기 문턱 {previous} — 100 이어야 난수 0~99 가 전부 덮인다");

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
