using System.Collections.Generic;
using System.Text;

namespace JinHyung.Data
{
    /// <summary>난이도 표 — 4행.</summary>
    public class FrontWarsDifficultyDataContainer : DictionaryContainer<string, FrontWarsDifficultyData>
    {
        private const int OriginRowCount = 4;

        /// <summary>원본 행동 13개 이름 `[소스] this.plays={`.</summary>
        private static readonly HashSet<string> KnownPlays = new HashSet<string>
        {
            "claimFallout", "purgeBots", "strikeBack", "honorAllies", "punishTraitor", "consumeAfk", "turnOnAlly",
            "joinPileOn", "settleGrudge", "finishCrippled", "pickOnWeakest", "raidNearestIsland", "supportTeam"
        };

        /// <summary>난이도별 행동 개수 [소스].</summary>
        private static readonly Dictionary<string, int> PlayCounts = new Dictionary<string, int>
        {
            { "Easy", 7 }, { "Medium", 11 }, { "Hard", 13 }, { "Impossible", 13 }
        };

        public override string Name
        {
            get { return "FrontWarsDifficultyTable"; }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            foreach (KeyValuePair<string, int> expect in PlayCounts)
            {
                FrontWarsDifficultyData d = Get(expect.Key);

                if (d == null)
                {
                    sb.AppendLine($"{expect.Key} 행이 없다");
                    continue;
                }

                string[] plays = string.IsNullOrEmpty(d.Plays) ? new string[0] : d.Plays.Split(';');

                if (plays.Length != expect.Value)
                    sb.AppendLine($"{d.Code}: 행동 {plays.Length}개 — 원본 {expect.Value}개");

                var seen = new HashSet<string>();

                foreach (string play in plays)
                {
                    if (KnownPlays.Contains(play) == false)
                        sb.AppendLine($"{d.Code}: 모르는 행동 «{play}»");

                    if (seen.Add(play) == false)
                        sb.AppendLine($"{d.Code}: 행동 «{play}» 가 두 번 있다");
                }

                if (d.NationAttackRateMaxExclusive <= d.NationAttackRateMin)
                    sb.AppendLine($"{d.Code}: 행동 주기 범위가 비었다");

                if (d.BotSweepMax < d.BotSweepMin || d.BotSweepMin <= 0)
                    sb.AppendLine($"{d.Code}: 봇 소탕 폭 범위가 틀렸다");

                if (d.NationStartTroops <= 0 || d.NationMaxTroopsMul <= 0.0)
                    sb.AppendLine($"{d.Code}: 네이션 병력 값이 0 이하다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
