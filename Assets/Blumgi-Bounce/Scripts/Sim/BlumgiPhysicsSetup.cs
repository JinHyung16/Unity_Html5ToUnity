using System.Text;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 유니티 2D 물리(Box2D) 월드를 <b>원본 Box2D 월드와 같은 눈금</b>으로 세운다.
    ///
    /// <para>
    /// ★★ <b>확정 G 는 폐기됐다</b> (원장 4회차 · PD 판정). 「물리 엔진 혼입 금지」의 이유는
    /// «원본에 없는 거동이 엔진에서 새로 생기는 것»을 막기 위함인데, <b>이 게임은 원본 자체가 Box2D</b> 이고
    /// <b>유니티 2D 물리도 Box2D</b> 다. 여기서는 자체 적분기를 쓰는 쪽이 <b>오히려 원본에서 멀어진다</b> —
    /// 접선 속도비가 −0.56 ~ +3.54 로 흩어지고 각속도 부호가 매 충돌 바뀌는 것이
    /// <b>마찰 × 회전 관성의 결합</b>이라 「반사 벡터로 튕기는 점」 모델로는 원리적으로 재현되지 않는다 [4회차 실측].
    /// </para>
    ///
    /// <para>
    /// ★ <b>스케일이 파라미터의 일부다.</b> Box2D 는 미터를 전제한 절대 상수를 갖는다 —
    /// 접촉 슬롭과 <b>반발이 죽는 속도 임계</b>. 원본이 <c>worldScale 0.02</c>(1 m = 50 px)로 도니
    /// 우리도 <b>1 유닛 = 1 m = 50 px</b> 이어야 «같은 지점에서 안 튀기 시작»한다
    /// (<see cref="BlumgiUnits.WorldPixelsPerUnit"/>).
    /// </para>
    ///
    /// <para>
    /// ★★ <b>솔버 반복 횟수는 «맞춘다» — 원본이 기본값이 아니었다</b> [13회차 실측].
    /// 원본은 <b>속도 12 · 위치 6</b> 이고 유니티 2D 기본은 <b>8 / 3</b>(위치 반복이 <b>절반</b>)이다.
    /// 「유니티 기본 = Box2D 기본 = 원본」이라는 예전 전제가 <b>틀렸다</b> — Construct 3 이 올려 둔 값이다.
    /// 독립 3경로가 같은 답을 냈다 (behavior 속성 · <c>world.Step</c> 후킹 2349회 · 소스 덤프).
    /// </para>
    ///
    /// <para>
    /// ★ <b>확인만 하면 되는 두 가지</b>(13회차가 원본을 재서 「우리와 같다」로 닫은 것) —
    /// ① <b>반발이 죽는 속도</b>: 원본 브래킷 36.7 &lt; v_th ≤ 52.4 px/s 가 Box2D 표준 <b>1.0 m/s</b> 를 감싼다.
    /// 우리 <see cref="Box2DBounceThreshold"/> 가 이미 1 이다 — <see cref="Audit"/> 가 «확인 항목»으로 잰다.
    /// ② <b>물리 스텝</b>: 원본 <c>_steppingMode = 1</c> ⇒ 프레임 실제 dt(중앙값 <b>8.300 ms</b>) ·
    /// 상한 <b>1/30 s</b> · <b>프레임당 Step 정확히 1회</b> · 서브스텝 없음. 우리는 1/120 고정 · 하위스텝 꺼짐이다.
    /// </para>
    /// </summary>
    public static class BlumgiPhysicsSetup
    {
        /// <summary>
        /// Box2D 표준 반발 속도 임계 (m/s). 유니티 <c>Physics2D.bounceThreshold</c> 기본값과 같다.
        /// <b>이것이 같다는 사실이 «스케일을 맞춘 이유»의 검산</b>이다.
        /// </summary>
        public const float Box2DBounceThreshold = 1f;

        /// <summary>
        /// Box2D 폴리곤 스킨 (m) = <c>2 × b2_linearSlop</c>. 유니티 <c>defaultContactOffset</c> 기본값과 같고,
        /// 확정 E 개정 스케일에서 <b>0.5 px</b> 다 — 4회차가 fixture 에서 읽은 스킨과 일치한다 [실측].
        /// </summary>
        public const float Box2DPolygonSkin = 0.01f;

        /// <summary>
        /// ★★ <b>잠듦(sleep) 임계 3상수 — <c>b2_timeToSleep</c></b> (초).
        ///
        /// <para>
        /// <b>원본은 잠든다</b> [19회차 실측] — <c>W1L1</c> 미골인 샷이 한 블록 위에서 <b>698 프레임(5810 ms)</b> 을
        /// 머물다 <c>IsAwake</c> 가 <b>1 → 0</b> 이 된다. 공 <c>IsSleepingAllowed = true</c> ·
        /// 월드 <c>GetAllowSleeping() = true</c> 이고, <b>정지 시 38 바디 전부가 잠들 수 있는 상태</b>다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>값 자체는 <c>추정(열거)</c> 다</b> — <c>b2World</c> 메서드 <b>전수 53개</b> · <c>b2Body</c> <b>68개</b> 에
        /// <c>Get/SetTimeToSleep</c> 이 <b>없고</b>, C3 Physics <c>_sdkInst</c> 속성 <b>21개 전수</b>에도
        /// 잠듦 항목이 <b>0개</b>다 [19회차 실측]. <b>게임이 바꿀 자리가 없으므로 Box2D 2.3.x 기본 상수가 쓰인다</b>
        /// (부정 사실은 열거로만 닫힌다 — 재발방지 #57).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>「정답지가 통과하도록」 고르지 마라</b> (#48). 이 셋은 <b>유니티 2D 기본값과 이미 같다</b> —
        /// 여기서 «넣는» 이유는 값을 바꾸려는 것이 아니라 <b>다음 회차에 누가 되돌려 놔도 <see cref="Audit"/> 가 잡게</b> 하려는 것이다.
        /// </para>
        /// </summary>
        public const float Box2DTimeToSleep = 0.5f;

        /// <summary>
        /// <c>b2_linearSleepTolerance</c> (m/s) — 개정 스케일에서 <b>0.5 px/s</b>. <c>추정(열거)</c>
        /// (<see cref="Box2DTimeToSleep"/> 와 같은 근거).
        /// </summary>
        public const float Box2DLinearSleepTolerance = 0.01f;

        /// <summary>
        /// <c>b2_angularSleepTolerance</c> (°/s) — <b>2 °/s = 0.0349 rad/s</b>. <c>추정(열거)</c>
        /// (<see cref="Box2DTimeToSleep"/> 와 같은 근거).
        /// </summary>
        public const float Box2DAngularSleepTolerance = 2f;

        /// <summary>
        /// ★★★ <b>반발 «혼합 규칙»</b> — Box2D <c>b2MixRestitution(e₁, e₂) = max(e₁, e₂)</c>.
        ///
        /// <para>
        /// <b>이것이 왜 결정적인가</b> — 원본 블록 fixture 의 반발은 <b>0</b> 이고 공은 <b>0.7</b> 이다 [19회차 실측].
        /// 규칙이 «평균»이었다면 실효 반발이 <b>0.35</b> 가 되어 사슬이 통째로 달라진다.
        /// <c>max</c> 라서 <b>실효 0.7</b> 이고, 그래서 15회차 정답지의 <c>e_n</c> 이 <b>0.692 ~ 0.700</b> 으로 나온다.
        /// </para>
        ///
        /// <para>
        /// 유니티 <c>PhysicsMaterialCombine2D.Maximum</c> 이 정확히 그 규칙이다 [엔진 되읽음].
        /// </para>
        /// </summary>
        public const PhysicsMaterialCombine2D Box2DBounceCombine = PhysicsMaterialCombine2D.Maximum;

        /// <summary>
        /// <b>마찰 «혼합 규칙»</b> — Box2D <c>b2MixFriction(f₁, f₂) = √(f₁ · f₂)</c> (기하 평균).
        ///
        /// <para>
        /// ⚠ 유니티 <c>PhysicsMaterialCombine2D.Mean</c> 은 이름과 달리 <b>«기하» 평균</b>이다
        /// (엔진 문서 원문: <i>Uses a Geom[e]tric Mean algorithm</i>) — <b>Box2D 와 같은 식</b>이다.
        /// </para>
        ///
        /// <para>
        /// ★ 이 게임은 <b>공 · 블록 · 림 · 블롭의 마찰이 전부 0.5</b> 라 어떤 규칙을 써도 실효값이 0.5 다 —
        /// <b>그래서 «지금은» 안 갈린다.</b> 그래도 규칙을 못 박아 두는 이유는, 마찰이 다른 물체가 하나라도
        /// 생기는 순간 <b>조용히 갈리기 때문</b>이다 (#82 — 지금 효과가 없어도 잰 값은 넣는다).
        /// </para>
        /// </summary>
        public const PhysicsMaterialCombine2D Box2DFrictionCombine = PhysicsMaterialCombine2D.Mean;

        /// <summary>
        /// 전역 물리 설정을 데이터에서 넣는다. <b>중력은 여기서만 환산한다</b> —
        /// 데이터는 원본 px/s² 그대로고 엔진에는 ÷ 50 한 m/s² 가 들어간다 (1500 → <b>30</b>).
        /// </summary>
        public static void Apply(BlumgiConfigData config)
        {
            if (config == null)
                return;

            // ⚠ y 부호가 뒤집힌다 — 원본은 아래가 +y 이고 유니티는 위가 +y 다.
            Physics2D.gravity = BlumgiUnits.ToVector(config.AccelerationX, config.GravityY);

            Physics2D.bounceThreshold = Box2DBounceThreshold;
            Physics2D.defaultContactOffset = Box2DPolygonSkin;

            // ★★ 잠듦 임계 — 원본도 «잠든다» [19회차 실측 · 698프레임 뒤 IsAwake 1 → 0].
            //   값은 추정(열거) Box2D 2.3.x 기본이고 유니티 기본과 «이미 같다» — 되돌림을 막으려고 못 박는다.
            Physics2D.timeToSleep = Box2DTimeToSleep;
            Physics2D.linearSleepTolerance = Box2DLinearSleepTolerance;
            Physics2D.angularSleepTolerance = Box2DAngularSleepTolerance;

            // ★ 솔버 반복 — 원본 12 / 6 [13회차 실측]. 유니티 기본 8 / 3 을 «데이터로» 덮는다.
            Physics2D.velocityIterations = config.SolverVelocityIterations;
            Physics2D.positionIterations = config.SolverPositionIterations;

            Time.fixedDeltaTime = (float)config.PhysicsStepSeconds;
        }

        /// <summary>
        /// 설정이 «실제로 들어갔는지» 기계로 확인한다. 「넣었다」가 아니라 «됐다»를 판정한다.
        /// 어긋난 항목을 줄로 돌려주고, 다 맞으면 빈 문자열이다.
        /// </summary>
        public static string Audit(BlumgiConfigData config)
        {
            var sb = new StringBuilder();

            if (config == null)
                return "설정 데이터가 없다";

            Vector2 expectedGravity = BlumgiUnits.ToVector(config.AccelerationX, config.GravityY);

            if ((Physics2D.gravity - expectedGravity).sqrMagnitude > 1e-6f)
                sb.AppendLine($"중력 {Physics2D.gravity} — 기대 {expectedGravity} (원본 {config.GravityY} px/s² ÷ {BlumgiUnits.WorldPixelsPerUnit})");

            if (Mathf.Abs(Physics2D.bounceThreshold - Box2DBounceThreshold) > 1e-6f)
                sb.AppendLine($"반발 임계 {Physics2D.bounceThreshold} — Box2D 표준 {Box2DBounceThreshold}");

            // ★ 스케일 검산 — 임계가 «원본 px 로 몇이냐»가 데이터의 추정값과 같아야 한다.
            double thresholdPx = BlumgiUnits.ToWorldLength(Physics2D.bounceThreshold);

            if (System.Math.Abs(thresholdPx - config.BounceThresholdSpeed) > 1e-6)
            {
                sb.AppendLine($"반발 임계가 원본 눈금으로 {thresholdPx} px/s — 데이터 {config.BounceThresholdSpeed} px/s. " +
                              "스케일(1 유닛 = 50 px)이 어긋났다는 뜻이다");
            }

            if (Mathf.Abs(Physics2D.defaultContactOffset - Box2DPolygonSkin) > 1e-6f)
                sb.AppendLine($"접촉 오프셋 {Physics2D.defaultContactOffset} — Box2D 폴리곤 스킨 {Box2DPolygonSkin}");

            // ★★ 잠듦 임계 3상수 — 원본도 잠든다 [19회차]. 값은 추정(열거)라 «표시를 잃지 않게» 문구에 적는다.
            if (Mathf.Abs(Physics2D.timeToSleep - Box2DTimeToSleep) > 1e-6f)
                sb.AppendLine($"잠듦 대기 {Physics2D.timeToSleep}s — Box2D b2_timeToSleep {Box2DTimeToSleep}s [추정(열거)]");

            if (Mathf.Abs(Physics2D.linearSleepTolerance - Box2DLinearSleepTolerance) > 1e-6f)
            {
                sb.AppendLine($"선형 잠듦 허용 {Physics2D.linearSleepTolerance} m/s — " +
                              $"Box2D b2_linearSleepTolerance {Box2DLinearSleepTolerance} m/s [추정(열거)]");
            }

            if (Mathf.Abs(Physics2D.angularSleepTolerance - Box2DAngularSleepTolerance) > 1e-6f)
            {
                sb.AppendLine($"각 잠듦 허용 {Physics2D.angularSleepTolerance} °/s — " +
                              $"Box2D b2_angularSleepTolerance {Box2DAngularSleepTolerance} °/s [추정(열거)]");
            }

            if (Mathf.Abs(Time.fixedDeltaTime - (float)config.PhysicsStepSeconds) > 1e-6f)
                sb.AppendLine($"고정 스텝 {Time.fixedDeltaTime} — 데이터 {config.PhysicsStepSeconds}");

            // ★★★ 반발 «혼합 규칙» — 값을 그대로 넣어도 규칙이 다르면 결과가 갈린다.
            //   블록 0 · 공 0.7 이므로 Box2D 의 max 규칙에서 «실효 0.7» 이다.
            //   ⚠ 평균 규칙이었다면 0.35 가 되어 사슬이 통째로 달라진다 — 그래서 «실효값»을 엔진에게 물어본다 (#78).
            float effectiveBounce = PhysicsMaterial2D.GetCombinedValues((float)config.BallRestitution,
                                                                       (float)config.BlockRestitution,
                                                                       Box2DBounceCombine,
                                                                       Box2DBounceCombine);

            if (Mathf.Abs(effectiveBounce - (float)config.BallRestitution) > 1e-6f)
            {
                sb.AppendLine($"공×블록 실효 반발 {effectiveBounce} — 원본 Box2D max(e₁,e₂) = {config.BallRestitution} " +
                              $"(공 {config.BallRestitution} · 블록 {config.BlockRestitution}). 혼합 규칙이 어긋났다");
            }

            // 마찰은 Box2D 가 √(f₁·f₂) 다. 이 게임은 전부 0.5 라 «지금은» 어떤 규칙이든 같지만,
            // 규칙이 바뀌면 마찰이 다른 물체가 생기는 순간 조용히 갈린다.
            float effectiveFriction = PhysicsMaterial2D.GetCombinedValues((float)config.BallFriction,
                                                                         (float)config.BlockFriction,
                                                                         Box2DFrictionCombine,
                                                                         Box2DFrictionCombine);

            double box2DFriction = System.Math.Sqrt(config.BallFriction * config.BlockFriction);

            if (System.Math.Abs(effectiveFriction - box2DFriction) > 1e-6)
            {
                sb.AppendLine($"공×블록 실효 마찰 {effectiveFriction} — 원본 Box2D √(f₁·f₂) = {box2DFriction:0.######}. " +
                              "혼합 규칙이 어긋났다 (유니티 Mean 은 «기하» 평균이라 원래 같아야 한다)");
            }

            // ★ 솔버 반복 — 원본 12/6 [13회차 실측]. 유니티 기본 8/3 이 되돌아오면 여기서 잡힌다.
            if (Physics2D.velocityIterations != config.SolverVelocityIterations)
            {
                sb.AppendLine($"솔버 속도 반복 {Physics2D.velocityIterations} — 원본 {config.SolverVelocityIterations} " +
                              "(유니티 기본 8 로 되돌아갔다는 뜻이다)");
            }

            if (Physics2D.positionIterations != config.SolverPositionIterations)
            {
                sb.AppendLine($"솔버 위치 반복 {Physics2D.positionIterations} — 원본 {config.SolverPositionIterations} " +
                              "(유니티 기본 3 은 원본의 «절반»이다)");
            }

            // ★ 발사 각속도가 데이터에 «있는지» — 0 이면 4회차 오귀속으로 되돌아간 것이다 [13회차 실측].
            if (System.Math.Abs(config.LaunchAngularVelocityDegreesPerSecond) < 1e-9)
            {
                sb.AppendLine("발사 각속도가 0 이다 — 원본은 −200 °/s 다 (4회차 「발사 직후 0」은 오귀속으로 확정됐다)");
            }

            // ★ 확인 항목 — 원본 스텝 모형과 «같은가». 하위스텝이 켜지면 조용히 다른 엔진이 된다.
            if (Physics2D.simulationMode != SimulationMode2D.FixedUpdate)
                sb.AppendLine($"시뮬레이션 모드 {Physics2D.simulationMode} — 원본은 프레임마다 정확히 1 스텝이다");

            return sb.ToString();
        }
    }
}
