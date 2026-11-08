using System.Collections.Generic;
using System.Text;

namespace JinHyung.Data
{
    /// <summary>맵 네이션 표 — 지금은 호주 7명 (결정 목록 C — 맵 1개).</summary>
    public class FrontWarsNationDataContainer : DictionaryContainer<int, FrontWarsNationData>
    {
        /// <summary>호주 manifest 네이션 수 [실측].</summary>
        private const int AustraliaNationCount = 7;

        private const int MapWidth = 2000;
        private const int MapHeight = 1500;

        private readonly List<FrontWarsNationData> _scratch = new List<FrontWarsNationData>(8);

        public override string Name
        {
            get { return "FrontWarsNationTable"; }
        }

        /// <summary>그 맵의 네이션 — manifest 순서 그대로다.</summary>
        public IReadOnlyList<FrontWarsNationData> ForMap(string map)
        {
            _scratch.Clear();

            for (int i = 0; i < AllValues.Count; i++)
            {
                if (AllValues[i].Map == map)
                    _scratch.Add(AllValues[i]);
            }

            return _scratch;
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            int australia = ForMap("Australia").Count;

            if (australia != AustraliaNationCount)
                sb.AppendLine($"호주 네이션 {australia}명 — 원본 manifest {AustraliaNationCount}명");

            for (int i = 0; i < AllValues.Count; i++)
            {
                FrontWarsNationData n = AllValues[i];

                if (n.Strength <= 0)
                    sb.AppendLine($"{n.Name}: 강도 {n.Strength} — 0 이하면 시작 병력이 0 이 된다");

                if (n.X < 0 || n.Y < 0 || n.X >= MapWidth || n.Y >= MapHeight)
                    sb.AppendLine($"{n.Name}: 좌표 ({n.X},{n.Y}) 가 맵 밖이다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
