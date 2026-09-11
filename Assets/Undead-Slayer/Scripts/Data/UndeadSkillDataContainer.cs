using System.Text;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// 스킬 표 — 원본 정의 배열 <b>4종 전수</b> [소스 <c>Yh</c>].
    ///
    /// <para>
    /// ⚠ <b>행 수가 곧 «분모»다.</b> 원본 배열 길이보다 적으면 <b>그만큼 미이관</b>이고,
    /// 그 사실이 어느 검사에도 안 걸린 적이 있다 (<c>재발방지 #174</c>).
    /// </para>
    /// </summary>
    public class UndeadSkillDataContainer : DictionaryContainer<string, UndeadSkillData>
    {
        /// <summary>원본 스킬 수 [소스 — 정의 배열 길이].</summary>
        public const int OriginRowCount = 4;

        public override string Name
        {
            get { return "UndeadSkillTable"; }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 스킬 {OriginRowCount}종");

            for (int i = 0; i < AllValues.Count; i++)
            {
                UndeadSkillData v = AllValues[i];

                if (v.Code.IsNullOrEmpty())
                    sb.AppendLine($"Id {v.Id}: Code 가 비었다");

                if (v.NameKey.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 이름 문구 키가 비었다");

                if (v.IconAddress.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 아이콘 주소가 비었다");

                if (v.CooldownSeconds <= 0.0)
                    sb.AppendLine($"{v.Code}: 쿨다운이 0 이다 — 원본은 전부 양수다");

                if (v.ActiveSeconds <= 0.0)
                    sb.AppendLine($"{v.Code}: 효과 지속이 0 이다");

                if (v.DesktopKey.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 단축키가 비었다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
