using System.Text;

namespace JinHyung.Data
{
    /// <summary>관계 이름 표 — 4행. 표 순서가 판정 순서다.</summary>
    public class FrontWarsAttitudeDataContainer : DictionaryContainer<string, FrontWarsAttitudeData>
    {
        private const int OriginRowCount = 4;

        public override string Name
        {
            get { return "FrontWarsAttitudeTable"; }
        }

        /// <summary>관계 값에서 이름을 고른다.</summary>
        public FrontWarsAttitudeData FromRelation(double relation)
        {
            for (int i = 0; i < AllValues.Count; i++)
            {
                if (relation < AllValues[i].RelationBelow)
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

            for (int i = 0; i < AllValues.Count; i++)
            {
                if (AllValues[i].Order != i)
                    sb.AppendLine($"{AllValues[i].Code}: 순번 {AllValues[i].Order} 이 표 순서 {i} 와 다르다");

                if (i > 0 && AllValues[i].RelationBelow <= AllValues[i - 1].RelationBelow)
                    sb.AppendLine($"{AllValues[i].Code}: 문턱이 앞 행보다 크지 않다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
