using System.Text;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// 사탕 테이블. 원본 <c>candyColors</c>(<c>script.js:24~31</c>) 6행.
    /// </summary>
    public class CandyDataContainer : DictionaryContainer<int, CandyData>
    {
        /// <summary>원본 <c>candyColors.length</c> 와 같아야 한다 (<c>script.js:24~31</c> 에서 센 값).</summary>
        public const int OriginRowCount = 6;

        public override string Name
        {
            get { return "CandyTable"; }
        }

        /// <summary>
        /// 원본 인덱스로 뽑는다. 원본 <c>candyColors[randomColor]</c> (<c>script.js:42</c>) 에 대응한다.
        /// <b><c>Id</c> 가 아니라 순서로 찾는다</b> — 원본이 인덱스로 다루기 때문이다.
        /// </summary>
        public CandyData GetByOriginIndex(int index)
        {
            if (AllValues.IsValidIndex(index) == false)
                return null;

            return AllValues[index];
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            // ★ 행 수 어설션 — 게이트 G1 의 통과 증명이다.
            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            for (int i = 0; i < AllValues.Count; i++)
            {
                CandyData v = AllValues[i];

                if (string.IsNullOrEmpty(v.Code))
                    sb.AppendLine($"Id {v.Id} 의 Code 가 비었다");

                if (string.IsNullOrEmpty(v.SpriteAddress))
                    sb.AppendLine($"Id {v.Id} 의 SpriteAddress 가 비었다");

                // ★ 순서가 곧 원본 인덱스다. Id 가 1부터 순서대로가 아니면 GetByOriginIndex 가 어긋난다.
                if (v.Id != i + 1)
                    sb.AppendLine($"{i}번째 행의 Id 가 {v.Id} 다 — 1부터 순서대로여야 한다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
