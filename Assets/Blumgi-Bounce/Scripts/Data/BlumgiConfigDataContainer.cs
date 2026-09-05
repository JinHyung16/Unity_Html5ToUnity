using System.Text;

namespace JinHyung.Data
{
    /// <summary>Blumgi Bounce 전역 설정 — 행 1개짜리 표.</summary>
    public class BlumgiConfigDataContainer : DictionaryContainer<int, BlumgiConfigData>
    {
        private const int OriginRowCount = 1;

        /// <summary>원본 실측 중력 (`02_시스템_홀드샷.md` §4-1). 어설션용이다.</summary>
        private const double OriginGravity = 1500.0;

        public override string Name
        {
            get { return "BlumgiConfigTable"; }
        }

        /// <summary>이 게임의 유일한 설정 행. 없으면 <c>null</c> — 폴백을 만들지 않는다.</summary>
        public BlumgiConfigData Config
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

            BlumgiConfigData config = Config;

            if (config == null)
            {
                errorMessage = sb.AppendLine("Id 1 행이 없다").ToString();
                return false;
            }

            // ★ 중력은 이 게임 물리의 뿌리다. 여기가 틀리면 골든이 전부 어긋나는데
            //    화면은 「좀 다르게 날아가네」로만 보인다.
            if (System.Math.Abs(config.GravityY - OriginGravity) > OriginGravity * 0.01)
                sb.AppendLine($"중력 {config.GravityY} — 원본 실측 {OriginGravity} (±1%)");

            // 원본 실측: 수평 가속 0 (공기저항·바람 없음).
            if (config.AccelerationX != 0.0)
                sb.AppendLine($"수평 가속 {config.AccelerationX} — 원본 실측 0");

            if (config.BallRadius <= 0.0)
                sb.AppendLine("공 반지름이 0 이하다");

            if (config.PhysicsStepSeconds <= 0.0 || config.PhysicsStepSeconds > config.MaxDeltaTime)
                sb.AppendLine($"물리 스텝 {config.PhysicsStepSeconds} 이 dt 상한 {config.MaxDeltaTime} 과 어긋난다");

            // ★★ 「콜라이더 = 격자」로 되돌아가는 것을 막는다.
            //    격자 50 으로 콜라이더를 깔면 이웃 사이에 틈이 생겨 «공이 블록을 통과»한다.
            //    그 증상은 단발 궤적 검사에 안 잡히고 클리어 판정에서만 갈린다 — 그래서 여기서 막는다.
            if (config.BlockCollisionWidth <= config.BlockGridPitch)
            {
                sb.AppendLine($"블록 콜라이더 폭 {config.BlockCollisionWidth} 가 격자 피치 {config.BlockGridPitch} 이하다 " +
                              "— 4회차 실측은 84 다. 격자 크기로 깔면 이웃 사이 틈으로 공이 빠진다");
            }

            if (config.BlockCollisionHeight <= config.BlockGridPitch)
            {
                sb.AppendLine($"블록 콜라이더 높이 {config.BlockCollisionHeight} 가 격자 피치 {config.BlockGridPitch} 이하다 " +
                              "— 4회차 실측은 58.983051321428576 다");
            }

            // ★ 반발 «혼합»은 max 다 [4회차 실측]. 그러니 공이 부딪히는 모든 것의 실효 반발이 공의 값이 된다.
            //   블록 반발을 0 이 아닌 값으로 «올려» 맞추려는 시도를 여기서 잡는다 — 그건 실측을 지어내는 것이다.
            if (config.BlockRestitution != 0.0)
                sb.AppendLine($"블록 반발 {config.BlockRestitution} — 원본 실측은 0 이다 (실효 0.7 은 max 혼합이 만든다)");

            if (System.Math.Abs(config.BallRestitution - 0.7) > 1e-9)
                sb.AppendLine($"공 반발 {config.BallRestitution} — 4회차 실측 0.7 (검산 5건 0.698~0.701)");

            if (config.BallAngularDamping <= 0.0)
                sb.AppendLine("공 각감쇠가 0 이하다 — 실측 0.01. 공은 «구른다»");

            // ★★ 골대 6부품의 상수 오프셋 — 7회차가 W1L1~L5 전수로 «소수점까지» 같음을 확인한 값이다.
            //    「보기 좋게」 반올림하거나 부호를 뒤집으면 감지기가 조용히 몇 px 옮겨 가고,
            //    증상은 «클리어 판정»에서만 나온다. 그래서 여기서 못 박는다.
            AppendOffsetError(sb, "위 감지기 x", config.DetectorTopOffsetX, -0.70);
            AppendOffsetError(sb, "위 감지기 y", config.DetectorTopOffsetY, -10.53);
            AppendOffsetError(sb, "아래 감지기 x", config.DetectorBottomOffsetX, 0.0);
            AppendOffsetError(sb, "아래 감지기 y", config.DetectorBottomOffsetY, 100.0);
            AppendOffsetError(sb, "골대 기둥 x", config.GoalPostOffsetX, 75.0);
            AppendOffsetError(sb, "골대 기둥 y", config.GoalPostOffsetY, 0.0);
            AppendOffsetError(sb, "골대 그물 x", config.GoalNetOffsetX, 0.0);
            AppendOffsetError(sb, "골대 그물 y", config.GoalNetOffsetY, 88.0);

            if (System.Math.Abs(config.DetectorSize - 50.0) > 1e-9)
                sb.AppendLine($"감지기 한 변 {config.DetectorSize} — 7회차 실측 50 (5레벨 전부)");

            // ★★★ 발사점 트윈 진폭 — 여기가 0 이 되면 <b>발사점이 다시 «상수»로 돌아간다</b> [12회차].
            //    그 상태의 증상은 골든 점 몇 개로는 안 잡히고 «밸런싱 창의 한쪽 끝»에서만 보인다
            //    (패스 ①-g 에서 W1L3 창 하한이 원본 17.34 vs 우리 26.96 으로 벌어진 것이 그것이다).
            //    ⚠ 컬럼을 지우거나 0 으로 두는 «조용한 되돌림»을 여기서 막는다.
            //    ★ [14회차-b] 진폭이 «전역»으로 확정되며 값이 정정됐다 — 5레벨 38점 · 잔차 y ≤ 0.066 px.
            //      (0.264, 9.721) 은 12회차가 W1L3 5점만 보고 낸 값이고 저 force 2점이 비율을 끌어내렸다.
            AppendOffsetError(sb, "발사점 트윈 진폭 x", config.LaunchTweenOffsetX, 0.2665);
            AppendOffsetError(sb, "발사점 트윈 진폭 y", config.LaunchTweenOffsetY, 9.8386);

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        /// <summary>
        /// 골대 부품 오프셋 한 칸. <b>기준은 <c>07 §9-a</c> 실측표</b>이고, 이 파일의 기대값은 그 표를 그대로 옮긴 것이다.
        /// </summary>
        private static void AppendOffsetError(StringBuilder sb, string what, double actual, double expected)
        {
            if (System.Math.Abs(actual - expected) <= 1e-9)
                return;

            sb.AppendLine($"{what} 오프셋 {actual} — 실측 정본 {expected} (골대 6부품은 `07 §9-a` · 발사점 트윈은 `07 §8-5-b`)");
        }
    }
}
