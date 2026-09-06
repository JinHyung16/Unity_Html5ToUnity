using System.Collections.Generic;
using System.Text;

namespace JinHyung.Data
{
    /// <summary>
    /// 적 종류 표 [소스 직독 · 회차 9]. <b>스폰 우선순위 순으로</b> 정렬해 내준다.
    /// </summary>
    public class UndeadEnemyDataContainer : DictionaryContainer<int, UndeadEnemyData>
    {
        /// <summary>원본 <c>enemyConfigs</c> 의 종류 수.</summary>
        private const int OriginRowCount = 5;

        private readonly List<UndeadEnemyData> _byPriority = new List<UndeadEnemyData>(8);

        public override string Name
        {
            get { return "UndeadEnemyTable"; }
        }

        /// <summary>
        /// 스폰 우선순위 순서 — <b>예산을 이 순서로 쓴다</b> [소스].
        /// </summary>
        public IReadOnlyList<UndeadEnemyData> ByPriority
        {
            get
            {
                if (_byPriority.Count == AllValues.Count)
                    return _byPriority;

                _byPriority.Clear();
                _byPriority.AddRange(AllValues);
                _byPriority.Sort((a, b) => a.SpawnPriority.CompareTo(b.SpawnPriority));
                return _byPriority;
            }
        }

        public UndeadEnemyData GetByCode(string code)
        {
            for (int i = 0; i < AllValues.Count; i++)
            {
                if (AllValues[i].Code == code)
                    return AllValues[i];
            }

            return null;
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 enemyConfigs {OriginRowCount}종");

            var priorities = new HashSet<int>();

            for (int i = 0; i < AllValues.Count; i++)
            {
                UndeadEnemyData e = AllValues[i];

                if (string.IsNullOrEmpty(e.Code) || string.IsNullOrEmpty(e.TextureCode))
                    sb.AppendLine($"{e.Id}행: 코드/텍스처 코드가 비었다");

                if (e.MaxHpBase <= 0)
                    sb.AppendLine($"{e.Code}: 체력 {e.MaxHpBase} — 0 이하면 스폰되는 즉시 죽는다");

                if (e.BaseMoveSpeedWorld <= 0.0)
                    sb.AppendLine($"{e.Code}: 이동 속도가 0 이하다 — 히어로에게 영영 안 온다");

                if (e.SpawnCostBase <= 0)
                    sb.AppendLine($"{e.Code}: 스폰 비용 {e.SpawnCostBase} — 0 이하면 «무한히» 쏟아진다");

                if (e.BudgetMultiplierEarly <= 0.0 || e.BudgetMultiplierLate <= 0.0)
                    sb.AppendLine($"{e.Code}: 예산 배수가 0 이하다 — 예산이 안 차 영영 안 나온다");

                if (e.SpriteScaleBase <= 0.0)
                    sb.AppendLine($"{e.Code}: 표시 배율이 0 이하다");

                if (e.SpriteScaleMax < e.SpriteScaleBase)
                    sb.AppendLine($"{e.Code}: 배율 상한 {e.SpriteScaleMax} 이 기본 {e.SpriteScaleBase} 보다 작다");

                if (priorities.Add(e.SpawnPriority) == false)
                    sb.AppendLine($"{e.Code}: 스폰 우선순위 {e.SpawnPriority} 가 다른 종류와 겹친다 — 순서가 안 정해진다");
            }

            // ★ «가장 싼 적»은 반드시 맨 뒤여야 한다 — 앞에 있으면 예산을 다 먹어
            //   비싼 적이 영영 안 나온다. (원본 순서가 «비용 내림차순»은 아니다 — 박쥐가 망령보다 앞이다.)
            IReadOnlyList<UndeadEnemyData> ordered = ByPriority;

            if (ordered.Count > 1)
            {
                UndeadEnemyData last = ordered[ordered.Count - 1];

                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    if (ordered[i].SpawnCostBase >= last.SpawnCostBase)
                        continue;

                    sb.AppendLine($"가장 싼 적이 맨 뒤가 아니다 — {ordered[i].Code}({ordered[i].SpawnCostBase}) 가 "
                                  + $"{last.Code}({last.SpawnCostBase}) 보다 먼저 예산을 쓴다");
                    break;
                }
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
