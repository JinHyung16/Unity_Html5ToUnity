using System.Text;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// 문구 표 — 원본 한국어 로케일 <b>60키 전수</b> [실측].
    ///
    /// <para>
    /// ⚠ <b>키가 없으면 «오류»다.</b> 폴백으로 키 이름을 그리지 않는다 —
    /// 폴백은 어서트를 무력화하고, 그 자리는 영원히 안 고쳐진다 (<c>SKILL.md</c>).
    /// </para>
    /// </summary>
    public class UndeadTextDataContainer : DictionaryContainer<string, UndeadTextData>
    {
        /// <summary>원본 로케일 키 수 [실측 — <c>ko-*.js</c> export 전수].</summary>
        public const int OriginRowCount = 60;

        public override string Name
        {
            get { return "UndeadTextTable"; }
        }

        /// <summary>
        /// 문구를 꺼낸다. <b>없으면 오류를 찍고 <c>null</c></b> — 호출부에 폴백을 깔지 않는다.
        /// </summary>
        public string Ko(string code)
        {
            UndeadTextData data = Get(code);

            if (data == null)
            {
                Core.Log.Error($"{Name}: 문구 키가 없다 — {code}");
                return null;
            }

            return data.Ko;
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 로케일 {OriginRowCount}");

            for (int i = 0; i < AllValues.Count; i++)
            {
                UndeadTextData v = AllValues[i];

                if (v.Code.IsNullOrEmpty())
                    sb.AppendLine($"Id {v.Id}: Code 가 비었다");

                if (v.Ko.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 한국어 표기가 비었다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
