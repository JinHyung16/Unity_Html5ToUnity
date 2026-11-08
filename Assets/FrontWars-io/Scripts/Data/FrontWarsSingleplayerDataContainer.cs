using System.Text;

namespace JinHyung.Data
{
    /// <summary>싱글 기본값 표 — 1행.</summary>
    public class FrontWarsSingleplayerDataContainer : DictionaryContainer<int, FrontWarsSingleplayerData>
    {
        private const int OriginRowCount = 1;

        public override string Name
        {
            get { return "FrontWarsSingleplayerTable"; }
        }

        public FrontWarsSingleplayerData Defaults
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

            FrontWarsSingleplayerData d = Defaults;

            if (d == null)
            {
                errorMessage = sb.AppendLine("Id 1 행이 없다").ToString();
                return false;
            }

            if (d.Bots < 0 || d.Bots > d.BotsMax)
                sb.AppendLine($"봇 기본 {d.Bots} 이 0~{d.BotsMax} 밖이다");

            if (d.NumberOfTeams < 2)
                sb.AppendLine($"팀 수 {d.NumberOfTeams} — 2 이상이어야 한다");

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        public override void AfterAllTableLoaded()
        {
            FrontWarsSingleplayerData d = Defaults;
            FrontWarsDifficultyDataContainer difficulties = DataManager.Instance.GetContainer<FrontWarsDifficultyDataContainer>();

            if (d != null && difficulties != null && difficulties.ContainsKey(d.Difficulty) == false)
                JinHyung.Core.Log.Error($"싱글 기본 난이도 «{d.Difficulty}» 가 난이도 표에 없다");
        }
    }
}
