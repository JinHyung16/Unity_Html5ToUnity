using System.Text;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// 떠오르는 문구 표 — <b>원본 팝업 클래스 여섯 갈래를 그대로</b> 옮긴 것 [소스 직독 · 회차 12].
    /// </summary>
    public class UndeadPopupDataContainer : DictionaryContainer<string, UndeadPopupData>
    {
        /// <summary>원본 팝업 클래스 수 [소스 — XP · 적 피해 · 번개 피해 · 회복 · 피격 · 최대 체력 · 화염 소진].</summary>
        public const int OriginRowCount = 7;

        public override string Name
        {
            get { return "UndeadPopupTable"; }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 팝업 {OriginRowCount}");

            for (int i = 0; i < AllValues.Count; i++)
            {
                UndeadPopupData v = AllValues[i];

                if (v.Code.IsNullOrEmpty())
                    sb.AppendLine($"Id {v.Id}: Code 가 비었다");

                if (v.LifetimeMs <= 0.0)
                    sb.AppendLine($"{v.Code}: 수명이 0 이하다");

                if (v.FontSize <= 0.0)
                    sb.AppendLine($"{v.Code}: 글자 크기가 0 이하다");

                if (v.FillHex.IsNullOrEmpty() || v.FillHex.Length != 6)
                    sb.AppendLine($"{v.Code}: FillHex 가 «rrggbb» 가 아니다 — {v.FillHex}");

                if (v.StrokeHex.IsNullOrEmpty() || v.StrokeHex.Length != 6)
                    sb.AppendLine($"{v.Code}: StrokeHex 가 «rrggbb» 가 아니다 — {v.StrokeHex}");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
