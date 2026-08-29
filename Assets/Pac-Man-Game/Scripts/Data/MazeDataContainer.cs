using System.Text;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// 미로 테이블. 원본 <c>levelLayout</c> 31행.
    /// </summary>
    public class MazeDataContainer : DictionaryContainer<int, MazeData>
    {
        /// <summary>원본 <c>rows</c> (<c>script.js:9</c>) 에서 센 값.</summary>
        public const int OriginRowCount = 31;

        /// <summary>원본 <c>cols</c> — 모든 행이 이 길이여야 한다.</summary>
        public const int OriginColCount = 28;

        public override string Name
        {
            get { return "MazeTable"; }
        }

        /// <summary>
        /// <c>row</c> 번째 행. <b><c>Id</c> 가 아니라 순서로 찾는다</b> —
        /// 원본이 <c>levelLayout[r]</c> 로 읽기 때문이다.
        /// </summary>
        public string GetRow(int row)
        {
            if (AllValues.IsValidIndex(row) == false)
                return null;

            return AllValues[row].Row;
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            // ★ 행 수 어설션 — 역직렬화가 조용히 비면 여기서만 잡힌다.
            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            for (int i = 0; i < AllValues.Count; i++)
            {
                MazeData v = AllValues[i];

                if (string.IsNullOrEmpty(v.Row))
                {
                    sb.AppendLine($"행 {i} 이 비었다");
                    continue;
                }

                if (v.Row.Length != OriginColCount)
                    sb.AppendLine($"행 {i} 길이 {v.Row.Length} — 원본 {OriginColCount}");

                for (int c = 0; c < v.Row.Length; c++)
                {
                    char ch = v.Row[c];

                    // 원본이 쓰는 문자는 넷뿐이다. 다른 것이 들어오면 맵이 통째로 어긋난다.
                    if (ch != '0' && ch != '1' && ch != '2' && ch != '3')
                        sb.AppendLine($"행 {i} 열 {c} 에 모르는 문자 '{ch}'");
                }
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
