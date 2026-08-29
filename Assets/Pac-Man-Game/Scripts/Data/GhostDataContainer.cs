using System.Text;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>유령 테이블. 원본 <c>ghosts</c> 2행.</summary>
    public class GhostDataContainer : DictionaryContainer<int, GhostData>
    {
        /// <summary>원본 <c>ghosts.length</c> (<c>script.js:68~71</c>).</summary>
        public const int OriginRowCount = 2;

        public override string Name
        {
            get { return "GhostTable"; }
        }

        /// <summary>원본 배열 순서로 뽑는다 — 원본이 <c>ghosts[0]</c>·<c>ghosts[1]</c> 로 다룬다.</summary>
        public GhostData GetByOriginIndex(int index)
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

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            for (int i = 0; i < AllValues.Count; i++)
            {
                GhostData v = AllValues[i];

                if (string.IsNullOrEmpty(v.ColorHex))
                    sb.AppendLine($"Id {v.Id} 의 ColorHex 가 비었다");

                // 원본은 축 정렬 방향만 준다 (대각선이 없다).
                if (v.DirX != 0 && v.DirY != 0)
                    sb.AppendLine($"Id {v.Id} 의 시작 방향이 대각선이다");

                if (v.DirX == 0 && v.DirY == 0)
                    sb.AppendLine($"Id {v.Id} 의 시작 방향이 없다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
