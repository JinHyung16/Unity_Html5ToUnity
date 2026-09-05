using System.Collections.Generic;
using System.Text;

namespace JinHyung.Data
{
    /// <summary>
    /// 블록 배치 표 — <b>월드1 5레벨 합 462행</b> [UIUX 2회차 전수 실측].
    ///
    /// <para>
    /// ★ <b>행 수 어설션이 이 컨테이너의 존재 이유다.</b> 배치가 몇 칸 빠져도 화면은 그럴듯하고
    /// 골든도 「좀 다르게 튀네」로만 보인다 — 숫자로 잡지 않으면 아무 데도 안 걸린다.
    /// </para>
    /// </summary>
    public class BlumgiBlockDataContainer : BlumgiGroupContainer<BlumgiBlockData>
    {
        /// <summary>5레벨 합 [실측].</summary>
        public const int OriginRowCount = 462;

        /// <summary>레벨별 행 수 [실측]. 합이 맞아도 레벨별로 어긋날 수 있어 따로 센다.</summary>
        private static readonly Dictionary<string, int> OriginCountByLevel = new Dictionary<string, int>
        {
            { "W1L1", 34 },
            { "W1L2", 82 },
            { "W1L3", 47 },
            { "W1L4", 112 },
            { "W1L5", 187 },
        };

        public override string Name
        {
            get { return "BlumgiBlockTable"; }
        }

        protected override string GetGroupKey(BlumgiBlockData value)
        {
            return value.LevelCode;
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            foreach (var pair in OriginCountByLevel)
            {
                int actual = GetGroupCount(pair.Key);

                if (actual != pair.Value)
                    sb.AppendLine($"{pair.Key} 행 수 {actual} — 원본 {pair.Value}");
            }

            AppendOriginAnomalyErrors(sb);

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        /// <summary>
        /// ★ <b>원본 이상 2건이 «살아 있는지»를 검사한다</b> (원장 「원본 이상 목록」 #3 · #4).
        ///
        /// <para>
        /// 보통 검증은 「이상이 없는지」를 보는데 여기서는 반대다 —
        /// 누가 선의로 격자를 정돈하면 <b>원본과 달라지는데 아무 데도 안 걸리기</b> 때문이다.
        /// 고치려면 원장 「의도된 차이」에 등재하고 이 검사부터 지운다.
        /// </para>
        /// </summary>
        private void AppendOriginAnomalyErrors(StringBuilder sb)
        {
            // #3 — W1L1 의 y 627 무리. 정합값이면 625 여야 한다.
            if (HasBlock("W1L1", 100.0, 627.0) == false)
                sb.AppendLine("원본 이상 #3 이 사라졌다 — W1L1 (100, 627) 이 없다 (+2 어긋남을 «그대로» 재현해야 한다)");

            // #4 — W1L2 (1225, 375) 에 블록이 «둘».
            if (CountBlock("W1L2", 1225.0, 375.0) != 2)
                sb.AppendLine("원본 이상 #4 가 사라졌다 — W1L2 (1225, 375) 의 블록이 2개가 아니다");
        }

        private bool HasBlock(string levelCode, double x, double y)
        {
            return CountBlock(levelCode, x, y) > 0;
        }

        private int CountBlock(string levelCode, double x, double y)
        {
            IReadOnlyList<BlumgiBlockData> list = GetGroup(levelCode);

            if (list == null)
                return 0;

            int count = 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].X == x && list[i].Y == y)
                    count++;
            }

            return count;
        }
    }
}
