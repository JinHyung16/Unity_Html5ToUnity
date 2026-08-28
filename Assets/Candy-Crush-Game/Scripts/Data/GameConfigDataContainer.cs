using System.Text;

namespace JinHyung.Data
{
    /// <summary>
    /// 설정 테이블. <b>행이 하나뿐</b>이라 <see cref="Data"/> 로 읽는다.
    /// </summary>
    public class GameConfigDataContainer : DictionaryContainer<int, GameConfigData>
    {
        private const int ConfigId = 1;
        private const int ExpectedRowCount = 1;

        public override string Name
        {
            get { return "GameConfigTable"; }
        }

        /// <summary>행 하나짜리 설정. 없으면 <c>null</c> — 폴백을 주지 않는다.</summary>
        public GameConfigData Data
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

            GameConfigData data = Data;

            if (data == null)
            {
                sb.AppendLine($"Id {ConfigId} 행이 없다");
            }
            else
            {
                // 원본에서 센 값들 (script.js:16 · 130 · 157 · 195 · 198)
                if (data.BoardWidth != 8)
                    sb.AppendLine($"BoardWidth {data.BoardWidth} — 원본 8");

                if (data.ScoreMatchThree != 3)
                    sb.AppendLine($"ScoreMatchThree {data.ScoreMatchThree} — 원본 3");

                if (data.ScoreMatchFour != 4)
                    sb.AppendLine($"ScoreMatchFour {data.ScoreMatchFour} — 원본 4");

                if (data.TimedSeconds != 120)
                    sb.AppendLine($"TimedSeconds {data.TimedSeconds} — 원본 120");

                if (data.TickIntervalMs != 100)
                    sb.AppendLine($"TickIntervalMs {data.TickIntervalMs} — 원본 100");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
