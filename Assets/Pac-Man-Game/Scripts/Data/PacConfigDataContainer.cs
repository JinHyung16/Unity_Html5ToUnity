using System.Text;

namespace JinHyung.Data
{
    /// <summary>설정 테이블. <b>행이 하나뿐</b>이라 <see cref="Data"/> 로 읽는다.</summary>
    public class PacConfigDataContainer : DictionaryContainer<int, PacConfigData>
    {
        private const int ConfigId = 1;
        private const int ExpectedRowCount = 1;

        public override string Name
        {
            get { return "PacConfigTable"; }
        }

        /// <summary>없으면 <c>null</c> — <b>폴백을 주지 않는다</b>. 조회 실패는 터뜨린다.</summary>
        public PacConfigData Data
        {
            get { return Get(ConfigId); }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != ExpectedRowCount)
                sb.AppendLine($"행 수 {Count} — 설정 테이블은 {ExpectedRowCount}행이어야 한다");

            PacConfigData d = Data;

            if (d == null)
            {
                sb.AppendLine($"Id {ConfigId} 행이 없다");
                errorMessage = sb.ToString();
                return errorMessage.Length == 0;
            }

            // 원본에서 센 값과 다르면 판이 통째로 어긋난다.
            if (d.Cols != MazeDataContainer.OriginColCount)
                sb.AppendLine($"Cols {d.Cols} — 원본 {MazeDataContainer.OriginColCount}");

            if (d.Rows != MazeDataContainer.OriginRowCount)
                sb.AppendLine($"Rows {d.Rows} — 원본 {MazeDataContainer.OriginRowCount}");

            if (d.PacSpeed <= 0f || d.GhostSpeed <= 0f)
                sb.AppendLine("속도가 0 이하다");

            if (d.StartLives <= 0)
                sb.AppendLine("시작 목숨이 0 이하다");

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
