using JinHyung.Data;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 한 발 — 홀드 차지 → 발사 → 비행 → 골인/재장전 판정.
    ///
    /// <para>
    /// ★★ <b>공의 «움직임»은 여기서 적분하지 않는다.</b> 확정 G(자체 적분기)는 4회차 실측으로 폐기됐다 —
    /// 원본은 Construct 3 의 <b>Box2D</b> 위에서 돌고, 접선 속도비가 −0.56 ~ +3.54 로 흩어지며
    /// 각속도 부호가 매 충돌 바뀐다. 그건 <b>마찰 × 회전 관성의 결합</b>이라
    /// 「반사 벡터로 튕기는 점」 모델로는 <b>원리적으로</b> 재현되지 않는다.
    /// 그래서 <b>유니티 2D 물리(= 같은 Box2D)</b> 에 <b>실측 파라미터를 그대로</b> 넣는다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>여기 남는 계산은 전부 <c>double</c> 이다</b> — 파워 모델(<see cref="BlumgiPowerModel"/>)과
    /// 데이터 환산이 그것이다. 엔진 물리는 float 지만 <b>우리가 «만드는» 수는 double 로 만든다.</b>
    /// ★ <b>무작위를 쓰지 않는다.</b> 필요해지면 <c>JinHyung.Core.RandomUtil</c>(주입식)만 쓴다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>검증용 진입점(<c>DebugSetXxx</c>)을 두지 않았다.</b> 채점기도 <see cref="BeginHold"/> ·
    /// <see cref="Tick"/> · <see cref="EndHold"/> 라는 <b>실제 경로</b>를 그대로 지난다.
    /// </para>
    /// </summary>
    public sealed class BlumgiShotSimulation
    {
        /// <summary>
        /// 홀드 시간 비교용 여유(ms). <b>거동을 바꾸는 값이 아니다</b> —
        /// <c>dt</c> 를 여러 번 더하면 300ms 홀드가 299.99999999999994 로 남는데,
        /// 그것 때문에 「발사 최소 홀드」 경계에서 발사가 통째로 안 되는 것을 막는다.
        /// </summary>
        private const double HoldEpsilonMs = 1e-6;

        private readonly BlumgiLevelRuntime _level;
        private readonly BlumgiConfigData _config;
        private readonly BlumgiBallBody _ball;

        /// <summary>
        /// 원본 공 인스턴스변수 <c>canScore</c>[1] — <b>윗 감지기를 «지난 적 있다»</b> [6회차 실측 EVENT#17].
        /// <b>켜기만 하고 끄지 않는다</b> — 겹침이 끊겨도 유지된다(실측: 두 번 이탈해도 1 유지).
        /// 리셋은 <b>발사할 때</b> 한 번뿐이다 (<c>eControls</c> EVENT#38).
        /// </summary>
        private bool _canScore;

        /// <summary>
        /// 원본 공 인스턴스변수 <c>hasScored</c>[2] — 중복 득점 방지.
        /// ⚠ <b>리셋 액션이 없다</b> [실측] — 레이아웃이 다시 깔려 <b>공 인스턴스가 새로 만들어질 때만</b> 0 이다.
        /// 그래서 <see cref="ResetShot"/>(= 재장전)에서 <b>건드리지 않는다.</b> 이 시뮬 객체 한 개 = 공 인스턴스 한 개다.
        /// </summary>
        private bool _hasScored;

        /// <summary>
        /// 아랫 감지기와 «지금» 겹쳐 있나 — <b>「충돌 시작」 트리거를 이산 표본에서 내기 위한 것</b>이다.
        /// 규칙이 아니라 <b>표본화 장치</b>라 발사·재장전에서 같이 지운다.
        /// </summary>
        private bool _touchingBottomDetector;

        public BlumgiShotSimulation(BlumgiLevelRuntime level, BlumgiBallBody ball)
        {
            _level = level;
            _config = level.Config;
            _ball = ball;

            // ★ 공 «인스턴스»가 새로 생긴 자리다 — 원본에서 hasScored 가 0 이 되는 유일한 시점이다.
            _hasScored = false;

            ResetShot();
        }

        public BlumgiLevelRuntime Level
        {
            get { return _level; }
        }

        public EBlumgiShotState State { get; private set; }

        /// <summary>공의 지금 위치 (원본 world px · <b>아래가 +y</b>). 엔진에서 되읽어 환산한 값이다.</summary>
        public BlumgiVec2 BallPosition
        {
            get
            {
                if (_ball == null || _ball.Body == null)
                    return _level.BallParkPosition;

                BlumgiUnits.ToWorld(_ball.Body.position, out double x, out double y);
                return new BlumgiVec2(x, y);
            }
        }

        /// <summary>공의 지금 속도 (원본 world px/s).</summary>
        public BlumgiVec2 BallVelocity
        {
            get
            {
                if (_ball == null || _ball.Body == null)
                    return BlumgiVec2.Zero;

                BlumgiUnits.ToWorld(_ball.Body.linearVelocity, out double x, out double y);
                return new BlumgiVec2(x, y);
            }
        }

        /// <summary>공의 각속도(도/s). <b>공은 구른다</b> — 회전이 접선 거동을 만든다 [4회차 실측].</summary>
        public double BallAngularVelocityDeg
        {
            get { return _ball == null || _ball.Body == null ? 0.0 : _ball.Body.angularVelocity; }
        }

        /// <summary>누르고 있는 시간(초). 이 게임의 <b>유일한 입력</b>이다 — 조준은 없다 [실측 §6].</summary>
        public double HoldSeconds { get; private set; }

        /// <summary>발사 순간부터의 경과(초).</summary>
        public double FlightSeconds { get; private set; }

        /// <summary>발사 순간 속도 (world px/s). 골든 대조용이라 <b>바뀌지 않는다</b>.</summary>
        public BlumgiVec2 LaunchVelocity { get; private set; }

        /// <summary>
        /// ★ 이 발이 <b>실제로 떠난 자리</b> (world px). <b>레벨 상수가 아니다</b> —
        /// 홀드에 따라 최대 9.7 px 내려간다 [12회차 실측]. 발사 전에는 <see cref="BlumgiVec2.Zero"/> 다.
        /// </summary>
        public BlumgiVec2 LaunchPosition { get; private set; }

        /// <summary>충돌 횟수. 「궤적을 자유비행 구간까지만 채점한다」를 가르는 값이다 (합격 기준 5-b).</summary>
        public int CollisionCount
        {
            get { return _ball == null ? 0 : _ball.CollisionCount; }
        }

        /// <summary>공이 아직 시뮬 안에 있나. 화면 밖 한참 아래로 가면 내린다.</summary>
        public bool IsBallActive
        {
            get
            {
                if (State == EBlumgiShotState.Scored)
                    return false;

                return BallPosition.Y <= _config.BallOutOfPlayY;
            }
        }

        /// <summary>
        /// 공을 <b>대기 자리로 되돌린다</b>. <b>실패 패널티는 없다</b> [실측] — 같은 레벨을 그대로 이어 한다.
        ///
        /// <para>
        /// ★★ <b>발사대가 아니라 (0, 10000) 이다</b> [6회차 실측]. 원본은 미발사 공을 경기장에 두지 않는다 —
        /// 거기서 그대로 낙하하다가 <b>발사가 텔레포트로 데려온다</b>. 공을 발사대에 «놓아두면»
        /// 발사하지 않은 공이 굴러다니다 골이 된다 (패스 ①-c 위양성 2건).
        /// </para>
        ///
        /// <para>
        /// ⚠ <c>_canScore</c> 를 여기서 지우지 않는다 — 원본의 리셋 시점은 <b>발사</b> 한 곳뿐이다.
        /// <c>_hasScored</c> 는 <b>어디서도</b> 지우지 않는다 (공 인스턴스 = 이 객체가 새로 만들어질 때만 0).
        /// </para>
        /// </summary>
        public void ResetShot()
        {
            State = EBlumgiShotState.Ready;
            LaunchVelocity = BlumgiVec2.Zero;
            LaunchPosition = BlumgiVec2.Zero;
            HoldSeconds = 0.0;
            FlightSeconds = 0.0;
            _touchingBottomDetector = false;

            _ball?.Park(BlumgiUnits.ToVector(_level.BallParkPosition.X, _level.BallParkPosition.Y));
        }

        /// <summary>누르기 시작. 화면 어디를 눌렀는지는 <b>받지 않는다</b> — 발사에 영향이 없다 [실측 §6].</summary>
        public void BeginHold()
        {
            if (State != EBlumgiShotState.Ready)
                return;

            State = EBlumgiShotState.Charging;
            HoldSeconds = 0.0;
        }

        /// <summary>
        /// 손을 뗀다 → 발사. 홀드가 모자라면 <b>발사 자체가 일어나지 않는다</b>.
        ///
        /// <para>
        /// ★ <b>「임펄스 없이 놓인다」가 아니다</b> [6회차 실측 정정]. 실홀드 3ms 로 떼고 20초를 봤더니
        /// 공은 <b>(0, y) 에서 계속 떨어지기만</b> 했다 — 경기장에 들어오지도, 감지기에 닿지도 않았고
        /// 전역변수도 하나도 안 바뀌었다. 즉 <b>공을 데려오는 텔레포트가 아예 안 걸린다.</b>
        /// 3회차의 「118ms 이하는 위로 안 뜬다」와 같은 사건을 반대편에서 본 것이다.
        /// </para>
        /// </summary>
        public void EndHold()
        {
            if (State != EBlumgiShotState.Charging)
                return;

            double holdMs = HoldSeconds * 1000.0;
            FlightSeconds = 0.0;

            if (holdMs < _config.LaunchHoldThresholdMs - HoldEpsilonMs)
            {
                // 발사가 «없다» — 공은 대기 자리에서 그대로 떨어지고, 판은 다시 누를 수 있는 상태로 돌아간다.
                State = EBlumgiShotState.Ready;
                LaunchVelocity = BlumgiVec2.Zero;
                LaunchPosition = BlumgiVec2.Zero;
                return;
            }

            // ★ EVENT#38 — 발사가 걸리는 «그때» 득점 자격을 지운다. 원본의 유일한 리셋 시점이다.
            _canScore = false;
            _touchingBottomDetector = false;

            // ★ 표를 찾지 않는다 — 원본 전역변수의 램프·클램프를 그대로 계산한다 (`BlumgiPowerModel`).
            double speed = _level.PowerModel.Speed(holdMs);

            LaunchVelocity = _level.LaunchDirection * speed;

            // ★★ 발사 «위치»도 홀드의 함수다 [12회차 실측] — 블롭이 눌린 만큼 머리공이 내려가고
            //    발사가 그 자리에서 걸린다. 여기를 상수로 두면 «창의 한쪽 끝»에서만 크게 어긋난다.
            LaunchPosition = _level.LaunchPositionAt(holdMs);

            // ★ 환산은 여기 «한 줄»에서만 일어난다 — 데이터는 px, 엔진은 미터다.
            // ★★ 각속도도 «같은 프레임»에 붙는다 [13회차 실측 · 저작값 −200 °/s · force·레벨 무관 상수].
            //    부호 반전은 BlumgiUnits 가 한다 — 원본은 y 아래가 +, 유니티는 위가 + 라 향이 뒤집힌다.
            _ball?.Launch(BlumgiUnits.ToVector(LaunchPosition.X, LaunchPosition.Y),
                          BlumgiUnits.ToVector(LaunchVelocity.X, LaunchVelocity.Y),
                          BlumgiUnits.ToAngularVelocityDegrees(_config.LaunchAngularVelocityDegreesPerSecond));

            State = EBlumgiShotState.Flying;
        }

        /// <summary>
        /// 한 스텝 진행. <b>공을 움직이는 것은 엔진</b>이고 여기서는 «상태»만 본다 —
        /// 홀드 적산 · 골인 판정 · 화면 밖 판정.
        ///
        /// <para>
        /// ⚠ <b>물리 스텝 한 번에 한 번씩 부른다.</b> 골인 감지기는 물리체가 아니라
        /// 우리가 겹침을 재는 사각형이라, 스텝보다 성기게 부르면 <b>빠른 공이 감지기를 지나쳐 버린다</b>.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>원본에는 «정지 게이트»가 없다</b> — 홀드 중에도 다른 개체가 계속 움직이는 것이 실측됐다.
        /// 여기 <c>if</c> 를 하나 넣으면 원본과 갈린다.
        /// </para>
        /// </summary>
        public void Tick(double deltaTime)
        {
            if (deltaTime <= 0.0)
                return;

            double dt = deltaTime > _config.MaxDeltaTime ? _config.MaxDeltaTime : deltaTime;

            if (State == EBlumgiShotState.Charging)
            {
                HoldSeconds += dt;
                return;
            }

            if (State != EBlumgiShotState.Flying &&
                State != EBlumgiShotState.Missed)
                return;

            FlightSeconds += dt;

            if (IsBallActive == false)
                return;

            if (CheckGoal())
            {
                State = EBlumgiShotState.Scored;
                _ball?.Freeze();
                return;
            }

            if (State != EBlumgiShotState.Missed && BallPosition.Y > _config.ReloadTriggerY)
            {
                // 「화면 밖으로 떨어졌다」 = 재장전 신호. 공 자체는 계속 떨어진다 [실측] —
                // 원본도 낡은 공이 살아 있는 채로 새 공이 생긴다.
                State = EBlumgiShotState.Missed;
            }
        }

        /// <summary>
        /// 골인 판정 — <b>원본 이벤트 두 개를 그대로 옮긴 것</b>이다 [6회차 실측 · <c>eGameplay</c> 「ball score」].
        ///
        /// <code>
        /// EVENT#17 «매 틱»   공이 윗 감지기와 «겹침»            → canScore = true   (켜기만 · 끄기 없음)
        /// EVENT#18 «트리거»  공이 아랫 감지기와 «충돌 시작»
        ///                    AND canScore AND NOT hasScored    → 골
        /// 발사 시           canScore = false
        /// </code>
        ///
        /// <para>
        /// ⚠ <b>여기 없는 조건을 넣지 마라.</b> 원본 조건을 전수로 읽었고
        /// <b>하강 방향 조건도 · 속도 임계도 «없다»</b> — 상승 중(vy −485)에도 래치가 켜지는 것이 실측됐다.
        /// 속도 300 비교는 <b>블록 찌그러짐·림 애니 전용</b>이다.
        /// 두 사건이 <b>같은 프레임일 필요도 없고</b>, 겹침이 끊겨도 래치는 안 풀린다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>「트리거」를 「겹쳐 있음」으로 바꾸지 마라.</b> 아랫 감지기 판정 영역은 윗 것과
        /// y 26px 구간이 겹치므로, «닿아 있음»으로 읽으면 통과하지 않은 공도 골이 된다 (패스 ①-c 결함).
        /// </para>
        /// </summary>
        private bool CheckGoal()
        {
            // ★ 감지 도형은 «물리 원»이 아니라 87×87 사각이다 — C3 는 스프라이트 충돌 폴리곤으로 판정한다.
            double half = _config.BallDetectionBoxSize * 0.5;
            BlumgiVec2 position = BallPosition;

            // EVENT#17 — 매 틱 · 켜기만 한다.
            if (_level.DetectorTop.OverlapsBox(position, half))
                _canScore = true;

            // EVENT#18 — «닿기 시작»한 프레임에만 성립한다.
            bool touching = _level.DetectorBottom.OverlapsBox(position, half);
            bool began = touching && _touchingBottomDetector == false;

            _touchingBottomDetector = touching;

            if (began == false || _canScore == false || _hasScored)
                return false;

            _hasScored = true;
            return true;
        }
    }
}
