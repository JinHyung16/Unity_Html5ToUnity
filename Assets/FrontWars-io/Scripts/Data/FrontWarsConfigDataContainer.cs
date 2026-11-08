using System.Text;

namespace JinHyung.Data
{
    /// <summary>FrontWars 밸런스 상수 표 — 1행.</summary>
    public class FrontWarsConfigDataContainer : DictionaryContainer<int, FrontWarsConfigData>
    {
        private const int OriginRowCount = 1;

        public override string Name
        {
            get { return "FrontWarsConfigTable"; }
        }

        /// <summary>유일한 설정 행. 없으면 null — 폴백을 만들지 않는다.</summary>
        public FrontWarsConfigData Config
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

            FrontWarsConfigData c = Config;

            if (c == null)
            {
                errorMessage = sb.AppendLine("Id 1 행이 없다").ToString();
                return false;
            }

            // 0 이면 그 규칙이 통째로 죽는 값들
            if (c.SpawnPhaseTurnsSingleplayer <= 0) sb.AppendLine("싱글 스폰 단계 턴이 0 이하다");
            if (c.WinPercentFFA <= 0 || c.WinPercentFFA > 100) sb.AppendLine($"FFA 승리 % {c.WinPercentFFA} 범위 밖");
            if (c.WinPercentTeam <= 0 || c.WinPercentTeam > 100) sb.AppendLine($"팀 승리 % {c.WinPercentTeam} 범위 밖");
            if (c.WinCheckIntervalTicks <= 0) sb.AppendLine("승리 검사 주기가 0 이하다 — 판이 안 끝난다");
            if (c.GoldPerTickHuman <= 0 || c.GoldPerTickBot <= 0) sb.AppendLine("틱당 골드가 0 이하다");
            if (c.AttackAmountDivisorHuman <= 0 || c.AttackAmountDivisorBot <= 0) sb.AppendLine("공격 병력 나눔수가 0 이하다");
            if (c.DonationDivisor <= 0) sb.AppendLine("기부 나눔수가 0 이하다");
            if (c.BoatMaxNumber <= 0) sb.AppendLine("배 최대 수가 0 이하다 — 해상 공격이 안 된다");
            if (c.SamHitChance <= 0.0 || c.SamHitChance > 1.0) sb.AppendLine("SAM 명중 확률 범위 밖");
            if (c.RelationDecayPerTick <= 0.0) sb.AppendLine("관계 감소량이 0 이하다");

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
