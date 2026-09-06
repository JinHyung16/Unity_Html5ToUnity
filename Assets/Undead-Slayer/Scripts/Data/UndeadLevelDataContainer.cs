using System;
using System.Text;

namespace JinHyung.Data
{
    /// <summary>
    /// 레벨 게이지 목표 — <b>식 한 벌</b>을 든다 (행 1개).
    ///
    /// <para>
    /// ★ 조회는 <see cref="GoalToReach"/> / <see cref="GoalForLevel"/> 로만 한다.
    /// <b>호출부가 식을 다시 쓰지 않게</b> 하려는 것이다 — 두 벌이 되면 반드시 갈린다.
    /// </para>
    /// </summary>
    public class UndeadLevelDataContainer : DictionaryContainer<int, UndeadLevelData>
    {
        private const int OriginRowCount = 1;

        public override string Name
        {
            get { return "UndeadLevelTable"; }
        }

        public UndeadLevelData Formula
        {
            get { return Get(1); }
        }

        /// <summary>
        /// <b>레벨 <paramref name="targetLevel"/> «에 닿는 데» 필요한 값</b> — 원본 <c>hl(t)</c> 그대로다.
        /// </summary>
        public int GoalToReach(int targetLevel)
        {
            UndeadLevelData f = Formula;

            if (f == null || targetLevel <= 0)
                return 0;

            if (targetLevel == 2)
                return f.Level2Goal;

            if (targetLevel == 3)
                return f.Level3Goal;

            double tier = 1.0 + f.TierStep * Math.Floor(targetLevel / (double)f.TierEvery);
            double steep = 1.0 + f.SteepStep * Math.Floor(targetLevel / (double)f.SteepEvery);

            return (int)Math.Round(f.BaseFactor * targetLevel * tier * steep, MidpointRounding.AwayFromZero);
        }

        /// <summary><b>지금 레벨에서 «다음» 레벨까지</b> 필요한 값 — HUD 의 분모다.</summary>
        public int GoalForLevel(int currentLevel)
        {
            return GoalToReach(currentLevel + 1);
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 식은 한 벌이다");

            UndeadLevelData f = Formula;

            if (f == null)
            {
                errorMessage = sb.AppendLine("Id 1 행이 없다").ToString();
                return false;
            }

            if (f.TierEvery <= 0 || f.SteepEvery <= 0)
                sb.AppendLine("단계 주기가 0 이하다 — 0 으로 나눈다");

            if (f.BaseFactor <= 0.0)
                sb.AppendLine("기본 계수가 0 이하다 — 목표가 전부 0 이 되어 레벨이 무한히 오른다");

            // ★★ 「식이 맞나」는 <b>관측치로</b> 확인한다 — 회차 1·5 에 원본 화면에서 읽은 값이다.
            //   ⚠ 이 값들은 «어설션»이지 데이터가 아니다. 표에 다시 적지 않는다.
            AppendGoalError(sb, 2, 15);
            AppendGoalError(sb, 3, 40);
            AppendGoalError(sb, 4, 120);
            AppendGoalError(sb, 5, 300);
            AppendGoalError(sb, 9, 540);
            AppendGoalError(sb, 10, 1350);

            // 목표는 «단조 증가»여야 한다 — 어느 레벨에서 줄면 진행이 뒤로 간다.
            for (int level = 2; level < 40; level++)
            {
                if (GoalToReach(level + 1) >= GoalToReach(level))
                    continue;

                sb.AppendLine($"목표가 레벨 {level} → {level + 1} 에서 줄어든다 "
                              + $"({GoalToReach(level)} → {GoalToReach(level + 1)})");
                break;
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        private void AppendGoalError(StringBuilder sb, int level, int observed)
        {
            int value = GoalToReach(level);

            if (value == observed)
                return;

            sb.AppendLine($"레벨 {level} 목표 {value} — 원본 관측 {observed}");
        }
    }
}
