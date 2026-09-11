using System.Text;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// 바이옴 표 — <b>원본이 지원하는 두 개 전수</b> [소스 <c>jl = [1, 2]</c>].
    ///
    /// <para>
    /// ⚠ 원본은 그 목록에 없는 번호로 장면을 열면 <b>예외를 던진다</b>
    /// [소스 <c>throw new Error(`Unsupported game biome`)</c>] — 우리도 조용히 1 로 떨어뜨리지 않고
    /// <see cref="Get"/> 가 <c>null</c> 을 준다.
    /// </para>
    /// </summary>
    public class UndeadBiomeDataContainer : DictionaryContainer<int, UndeadBiomeData>
    {
        /// <summary>원본이 지원하는 바이옴 수 [소스 <c>jl</c> 길이].</summary>
        public const int OriginRowCount = 2;

        public override string Name
        {
            get { return "UndeadBiomeTable"; }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 바이옴 {OriginRowCount}개");

            for (int i = 0; i < AllValues.Count; i++)
            {
                UndeadBiomeData v = AllValues[i];

                if (v.Code.IsNullOrEmpty())
                    sb.AppendLine($"Id {v.Id}: Code 가 비었다");

                if (v.NameKey.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 이름 문구 키가 비었다");

                if (v.TilesetCode.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 타일셋 코드가 비었다 — 지형이 통째로 빈다");

                if (v.MediumObjectCode.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 중형 오브젝트 코드가 비었다");

                // ⚠ 시작 자리가 (0,0) 이면 «안 적은 것»이다 — 원본은 둘 다 양수다
                if (v.HeroStartX == 0.0 && v.HeroStartY == 0.0)
                    sb.AppendLine($"{v.Code}: 시작 자리가 (0,0) 이다 — 원본은 (1620,1010)·(640,610)");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
