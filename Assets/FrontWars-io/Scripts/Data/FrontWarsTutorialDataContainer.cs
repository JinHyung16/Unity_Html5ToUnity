using System.Text;

namespace JinHyung.Data
{
    /// <summary>튜토리얼 설정 표 — 1행.</summary>
    public class FrontWarsTutorialDataContainer : DictionaryContainer<int, FrontWarsTutorialData>
    {
        private const int OriginRowCount = 1;

        /// <summary>원본 설정 검증 범위 `[소스] spawnPhaseTurns:Y().int().min(30).max(600)`.</summary>
        private const int SpawnPhaseMin = 30;
        private const int SpawnPhaseMax = 600;

        public override string Name
        {
            get { return "FrontWarsTutorialTable"; }
        }

        public FrontWarsTutorialData Tutorial
        {
            get { return Get(1); }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            FrontWarsTutorialData t = Tutorial;

            if (t == null)
            {
                errorMessage = sb.AppendLine("Id 1 행이 없다").ToString();
                return false;
            }

            if (t.SpawnPhaseTurns < SpawnPhaseMin || t.SpawnPhaseTurns > SpawnPhaseMax)
                sb.AppendLine($"스폰 단계 {t.SpawnPhaseTurns} — 원본 허용 {SpawnPhaseMin}~{SpawnPhaseMax}");

            if (t.StartingTroops < 100 || t.StartingTroops > 100000)
                sb.AppendLine($"시작 병력 설정 {t.StartingTroops} — 원본 허용 100~100000");

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
