using System.Text;

namespace JinHyung.Data
{
    /// <summary>핵 표 — 3행.</summary>
    public class FrontWarsNukeDataContainer : DictionaryContainer<string, FrontWarsNukeData>
    {
        private const int OriginRowCount = 3;

        public override string Name
        {
            get { return "FrontWarsNukeTable"; }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            for (int i = 0; i < AllValues.Count; i++)
            {
                FrontWarsNukeData n = AllValues[i];

                if (n.InnerRadius <= 0 || n.OuterRadius < n.InnerRadius)
                    sb.AppendLine($"{n.Code}: 반경 inner {n.InnerRadius} · outer {n.OuterRadius} 이 맞지 않는다");

                if (n.AiCostMarkupPercent < 100)
                    sb.AppendLine($"{n.Code}: 인지 비용 가산 {n.AiCostMarkupPercent}% — 100 미만이면 쏠수록 싸진다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
