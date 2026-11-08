using System.Collections.Generic;
using System.Text;

namespace JinHyung.Data
{
    /// <summary>유닛 표 — 17종 `[소스] unitInfo(e){`.</summary>
    public class FrontWarsUnitDataContainer : DictionaryContainer<string, FrontWarsUnitData>
    {
        private const int OriginRowCount = 17;

        private static readonly HashSet<string> CostRules = new HashSet<string> { "Free", "Fixed", "Linear", "Doubling" };

        public override string Name
        {
            get { return "FrontWarsUnitTable"; }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 유닛 {OriginRowCount}종");

            var orders = new HashSet<int>();

            for (int i = 0; i < AllValues.Count; i++)
            {
                FrontWarsUnitData u = AllValues[i];

                if (orders.Add(u.Order) == false)
                    sb.AppendLine($"{u.Code}: 순서 {u.Order} 가 겹친다");

                if (CostRules.Contains(u.CostRule) == false)
                    sb.AppendLine($"{u.Code}: 비용 규칙 «{u.CostRule}» 을 모른다");

                if (u.CostRule != "Free" && u.CostBase <= 0)
                    sb.AppendLine($"{u.Code}: 비용 규칙 {u.CostRule} 인데 기본 비용이 0 이하다");

                if ((u.CostRule == "Linear" || u.CostRule == "Doubling") && u.CostCap < u.CostBase)
                    sb.AppendLine($"{u.Code}: 비용 상한 {u.CostCap} 이 기본 {u.CostBase} 보다 작다");

                if (u.TerritoryBound && u.Code != "Construction" && u.ConstructionTicks < 0)
                    sb.AppendLine($"{u.Code}: 땅 위 건물인데 건설 틱이 없다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        public override void AfterAllTableLoaded()
        {
            // 비용 셈에 쓰는 코드가 표에 있는지 교차로 본다
            for (int i = 0; i < AllValues.Count; i++)
            {
                string codes = AllValues[i].CostCountCodes;

                if (string.IsNullOrEmpty(codes))
                    continue;

                foreach (string code in codes.Split(';'))
                {
                    if (ContainsKey(code.Trim()) == false)
                        JinHyung.Core.Log.Error($"{AllValues[i].Code}: 비용 셈 코드 «{code}» 가 유닛 표에 없다");
                }
            }
        }
    }
}
