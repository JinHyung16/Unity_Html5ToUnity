namespace JinHyung.Data
{
    /// <summary>
    /// Blumgi Bounce 전역 설정 — <b>레벨을 넘어 같은 값</b>만 여기 있다.
    /// 레벨마다 다른 것(발사대·발사각·파워 곡선·테마색)은 <see cref="BlumgiLevelData"/> 다.
    ///
    /// <para>
    /// ★ <b>단위는 «원본 world px» 그대로 적는다.</b> 유니티 유닛(=미터)으로의 환산은
    /// <c>BlumgiUnits</c> 한 곳에서만 한다 (확정 E 개정 — 1 유닛 = 1 m = 원본 50 px).
    /// 여기에 미터를 적기 시작하면 «어느 컬럼이 원본 값인지»가 갈리지 않는다.
    /// <b>y 는 아래가 +</b> 다 (원본 Construct 3 좌표계 그대로).
    /// </para>
    ///
    /// <para>
    /// ★★ <b>물리 파라미터는 «잰» 것이 아니라 «읽은» 것이다</b> (학습 4회차).
    /// 원본은 Construct 3 의 Box2D <c>Physics</c> behavior 위에서 돌고, 인스턴스 속성에
    /// 반발·마찰·밀도·감쇠·충돌 도형·월드 중력이 전부 노출돼 있었다.
    /// <b>골든이 통과하도록 이 값을 고르지 않는다</b> — 고치려면 다시 «읽어» 와야 한다.
    /// </para>
    /// </summary>
    public class BlumgiConfigData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>
        /// 중력 (world px/s², <b>+y = 아래</b>). 원본 실측 <b>1500</b>.
        ///
        /// <para>
        /// 두 독립 경로가 같은 값을 냈다 — ① 자유비행 2차 최소제곱 10회 이상(1491~1509) [3회차]
        /// ② Box2D 월드 중력 <b>(0, 30) m/s²</b> ÷ <c>worldScale 0.02</c> = <b>1500 px/s²</b> [4회차 · 읽은 값].
        /// 유니티에는 <c>BlumgiPhysicsSetup</c> 이 <b>÷ 50 해서 30</b> 으로 넣는다.
        /// </para>
        /// </summary>
        public double GravityY { get; set; }

        /// <summary>수평 가속. 원본 실측 <b>0</b> — 공기저항·바람이 없다 (같은 절).</summary>
        public double AccelerationX { get; set; }

        // ───────────────────────────────────────────── 파워 기전 (전역 · 3회차 실측)
        //
        // ★ 이 넷이 33점짜리 피팅 표를 통째로 대체한다. 원본 런타임 전역변수를 «직접 읽은» 값이다
        //   (`02_시스템_홀드샷.md` §4-2). 레벨 데이터에 파워 관련 컬럼을 다시 만들지 마라 —
        //   「레벨별 파워 곡선·캡」은 2회차의 오판이었고 3회차가 뒤집었다.

        /// <summary>홀드 시작 순간의 <c>forceShoot_P1</c>. 원본 전역 <c>forceShootStart</c> = <b>10</b> [실측].</summary>
        public double ForceShootStart { get; set; }

        /// <summary>홀드 중 힘이 차오르는 속도(초당). 원본 전역 <c>forceShootOffset</c> = <b>55</b> [실측 · 램프 시계열과 정확히 일치].</summary>
        public double ForceShootRatePerSecond { get; set; }

        /// <summary>힘의 하드 클램프. <b>100</b> [실측] — 여기서 멈추므로 <b>더 오래 눌러도 세지지 않는다</b>.</summary>
        public double ForceShootMax { get; set; }

        // ⚠ 여기 있던 `SpeedPerForce`(20.95) 는 «지웠다» (패스 ①-h).
        //   ★ 12회차가 진짜 발사 프레임을 직독해 «정확값 21.0272» 를 냈고, 그 값이
        //     `1 / (m · worldScale)` — 즉 «질량에서 나오는 것»임을 확인했다 [실측 5/5 · 소수 4자리].
        //   상수로 남겨 두면 밀도·반지름을 고쳤을 때 파워만 옛 값으로 남는다.
        //   ⇒ `BlumgiPowerModel.SpeedPerForce` 가 BallDensity · BallRadius 에서 «계산»한다.
        //   ⚠ 여기에 숫자를 되돌려 놓지 마라 — 두 곳에 두면 어느 쪽이 진실인지 갈린다.

        /// <summary>
        /// ★★★ <b>발사점 홀드 트윈 진폭 x</b> (world px) — <b>0.2665</b>
        /// [★ 14회차-b 실측 · <b>5레벨 38점</b> · 잔차 x ≤ 0.002 px].
        ///
        /// <para>
        /// <b>발사 «위치»는 상수가 아니라 홀드의 함수다.</b> 홀드가 길수록 블롭이 납작해지고
        /// 그 위에 얹힌 머리공이 따라 내려간다 — 발사점은 <b>그 머리공 자리에서 나온다</b>.
        /// </para>
        ///
        /// <code>
        /// 발사점 = (BallRestX, BallRestY) + (LaunchTweenOffsetX, LaunchTweenOffsetY) · sin(π/2 · min(1, 홀드초/1.5))
        /// </code>
        ///
        /// <para>
        /// 근거 — 절편을 <b>각 레벨의 정지 머리공(<c>spr_BallVisu</c>) bbox 중심으로 «고정»</b>하고
        /// (피팅 자유도가 아니다) 진폭만 하나로 맞추면 <b>5레벨 38점이 잔차 y ≤ 0.066 px 안</b>에 들어온다
        /// [14회차-b]. 절편 자체도 자유 피팅했을 때 머리공 중심과 <b>|Δ| ≤ 0.07 px</b> 로 붙는다.
        /// </para>
        ///
        /// <para>
        /// ★★ <b>[해소 · 14회차-b] 「W1L3 에서만 실측」이 끝났다 — 진폭은 «전역»이다.</b>
        /// W1L1 6 · W1L2 6 · W1L3 16 · W1L4 5 · W1L5 5 점(<c>holdSec ≤ 1.5</c>)으로 다시 맞춰
        /// <b>레벨별 잔차 최대치가 5레벨 전부 0.07 px 미만</b>이었다. 12회차 값 <c>(0.264, 9.721)</c> 은
        /// <b>W1L3 5점 중 저 force 2점이 비율을 끌어내린 것</b>이라 <c>(0.2665, 9.8386)</c> 으로 정정한다
        /// (Δy +0.118 px = 채점 허용치 ±5 px 의 2.4 % — <b>효과가 작아도 실측이라 넣는다</b> · 재발방지 #82).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>「레벨마다 다른가」는 절편을 «고정하고» 물어야 갈린다</b> — 레벨마다 자유 피팅하면
        /// 진폭이 9.87 · 9.87 · 11.14 · 10.67 로 나와 <b>「레벨별이다」로 읽힌다</b>(자유도가 「다르다」를 만든다).
        /// </para>
        /// </summary>
        public double LaunchTweenOffsetX { get; set; }

        /// <summary>
        /// ★★★ <b>발사점 홀드 트윈 진폭 y</b> (world px, 아래가 +) — <b>9.8386</b>
        /// [★ 14회차-b 실측 · <b>5레벨 38점</b> · 잔차 y max 0.066 px · 중앙 0.022 px].
        /// 홀드 0 에서 <c>y = BallRestY</c> · 홀드 ≥1.5 s 에서 <c>+9.8386</c> 다.
        ///
        /// <para>
        /// ⚠ <b>진폭의 «분해»는 미측정</b>이다 — 블롭 스쿼시(높이 −15)와 머리공 확대(94→112.8)
        /// 두 트윈의 합성이라 각각의 기여를 못 갈랐다. 합성값만 실측이다.
        /// </para>
        ///
        /// <para>
        /// ★★ <b>[14회차-b] 포화 «뒤»(holdSec &gt; 1.5)에서는 이 모델이 «깨진다» — 드리프트가 아니라 튐이다.</b>
        /// <c>holdSec 1.516~1.633</c> 의 26점에서 <c>dy</c> 가 <b>9.866 «또는» 10.88~11.66 두 갈래</b>로 나오고,
        /// <b>완전 포화(벽시계 1.9 s · 2.6 s)에서는 정확히 트윈 종료값 9.866 으로 되돌아온다.</b>
        /// <b>원인은 미측정</b>(스쿼시 뒤 별도 연출인지 블롭 body 정착 진동인지 못 갈랐다).
        /// ⚠ <b>이관 영향은 없다</b> — <c>holdSec 1.5 = force 92.5</c> 인데 5레벨 클리어 창 상한이
        /// 전부 force 77 이하라 그 구간은 애초에 창 밖이다. 그래서 <c>min(1, …)</c> 클램프를 그대로 둔다.
        /// </para>
        /// </summary>
        public double LaunchTweenOffsetY { get; set; }

        /// <summary>
        /// ★★★ <b>발사 순간의 각속도</b> — <b>원본 좌표계(y 아래가 +)의 «저작값» °/s</b>. 값은 <b>−200</b> 이다.
        ///
        /// <para>
        /// ★ <b>이것은 되푼 값이 아니라 «직독»이다</b> [13회차 실측]. 두 경로가 소수 8자리로 만난다 —
        /// ① 발사 «그» 프레임의 <c>body.GetAngularVelocity()</c> = <b>−3.4906585216522217 rad/s</b>
        /// ② 이벤트 시트의 저작 파라미터 <c>[eControls] behavior=Physics params=[-200]</c>
        /// (Construct 3 Physics 의 「각속도 설정」은 <b>도/초</b> 단위다).
        /// </para>
        ///
        /// <para>
        /// <b>force 무관 · 레벨 무관 상수</b>다 — force 34.7~93.0(2.7배) · 5레벨 · 16샷에서 <b>표준편차 0</b> [실측].
        /// ω 는 텔레포트·속도와 <b>같은 프레임</b>에 붙고, 발사 «직전» 프레임은 <b>정확히 0</b> 이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>부호를 그대로 엔진에 넣지 않는다.</b> 원본은 y 아래가 + 이고 유니티는 위가 + 라
        /// 축 뒤집기가 <b>회전 방향을 반전</b>시킨다 — 환산은
        /// <see cref="BlumgiUnits.ToAngularVelocityDegrees"/> «한 곳»만 지난다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>「발사 직후 ω = 0」은 오귀속이었다</b> (4회차) — 발사~첫 충돌 사이에 접촉이
        /// <b>15샷 전부 0건</b>인데 ω 는 내내 −3.49 다 [13회차 실측]. 여기 <b>0 을 되돌려 놓지 마라.</b>
        /// </para>
        /// </summary>
        public double LaunchAngularVelocityDegreesPerSecond { get; set; }

        /// <summary>
        /// 이 홀드(ms) «미만»이면 공이 발사되지 않고 그대로 굴러떨어진다.
        ///
        /// <para>
        /// <b>전역이다</b> — 레벨 데이터가 아니다. 실측 경계는 «실» 홀드 <b>118 &lt; t ≤ 130ms</b>
        /// (W1L1 20ms 격자 × 각 3회, 판정 「릴리즈 후 30px 이상 올라가는가」) [3회차 §4-4].
        /// 여기 넣은 값은 그 구간의 중앙이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>이것이 게임의 「발사 임계값」인지는 미측정이다.</b> 3회차는 <b>임계가 없고
        /// «중력을 이기는 지점»일 뿐</b>이라는 쪽으로 추정한다 (30px 상승 기준이
        /// <c>|v0|·sin60°</c> 의 상승고 30px 지점과 일치한다). 다만 홀드 0ms 에서 공이
        /// <b>408ms 동안 «완전히 정지»</b>해 있는 것은 그 추정으로 설명되지 않는다 —
        /// force 10 이 곧바로 걸렸다면 즉시 움직였어야 한다 (`02 §5`).
        /// 그래서 <b>임계를 남기되 값을 데이터로 둔다.</b> 갈리면 이 한 줄만 고친다.
        /// </para>
        /// </summary>
        public double LaunchHoldThresholdMs { get; set; }

        /// <summary>
        /// 가변 dt 상한(초). <b>홀드 적산에만</b> 쓴다 — 물리 적분은 엔진이 고정 스텝으로 돈다.
        /// ⚠ <b>추정</b> — 원본(Construct 3)의 dt 상한은 <b>미측정</b>이다.
        /// Construct 3 기본 「최소 프레임레이트 30fps」에서 온 값이라 원본 실측이 나오면 갈아 끼운다.
        /// </summary>
        public double MaxDeltaTime { get; set; }

        /// <summary>
        /// 물리 고정 스텝(초) — <c>Time.fixedDeltaTime</c> 및 채점기의 <c>Simulate</c> 폭.
        ///
        /// <para>
        /// 원본은 rAF 가변 dt 로 돌고 실측 폭이 <b>7.2~9.5ms</b> 였다 [3회차] — 그 한가운데인
        /// <b>1/120초</b> 를 고정 격자로 쓴다. 고정 스텝이면 우리가 원본보다 «더» 결정적이 되는데
        /// 원장은 이를 결함으로 보지 않는다 (원본 이상 #2 가설).
        /// ⚠ Box2D 는 <b>스텝 폭이 반발 안정화에 관여</b>한다 — 여기를 임의로 키우면 파리티가 갈린다.
        /// </para>
        /// </summary>
        public double PhysicsStepSeconds { get; set; }

        /// <summary>
        /// ★ 솔버 <b>속도 반복 횟수</b> — 원본 <b>12</b> [13회차 실측]. <b>유니티 2D 기본은 8</b> 이다.
        ///
        /// <para>
        /// 독립 3경로가 같은 답을 냈다 — ① behavior 속성 <c>_velocityIterations</c>
        /// ② <c>world.Step(dt, vi, pi)</c> 프로토타입 후킹 <b>2349회 전부 12</b> ③ 소스 덤프.
        /// <b>Box2D 기본값(8/3)이 아니라 Construct 3 이 올려 둔 값</b>이라 「기본값이니 안 건드린다」가 틀렸다.
        /// </para>
        /// </summary>
        public int SolverVelocityIterations { get; set; }

        /// <summary>
        /// ★ 솔버 <b>위치 반복 횟수</b> — 원본 <b>6</b> [13회차 실측]. <b>유니티 2D 기본은 3</b> — <b>절반</b>이다.
        ///
        /// <para>
        /// 위치 반복이 모자라면 <b>겹친 블록에서 관통 해소가 덜 되고 다음 스텝에 다시 접촉</b>한다 —
        /// 「접촉 구간이 길어진다」·「같은 블록을 계속 다시 친다」는 우리 쪽 관측과 직접 맞는 자리다.
        /// </para>
        /// </summary>
        public int SolverPositionIterations { get; set; }

        // ───────────────────────────────────────────── 물리체 (4회차 · Box2D 속성을 «읽었다»)
        //
        // ★ 아래 값은 전부 원본 인스턴스의 `Physics` behavior 속성 · Box2D fixture 실물이다.
        //   ⚠ 골든이 통과하도록 «고르지» 않는다 — 값을 바꾸려면 원본을 다시 읽어야 한다.

        /// <summary>
        /// 공 충돌 반지름 (world px). Box2D fixture <c>b2CircleShape.m_radius = 0.87 m</c> → <b>43.5 px</b> [4회차 실측].
        /// ⚠ 표시 bbox 절반(44)이 아니다 — 1 회차의 44 는 «그림»에서 잰 값이었다.
        /// </summary>
        public double BallRadius { get; set; }

        /// <summary>공 밀도 <b>1</b> [실측]. 반지름 0.87 m 와 합쳐 질량 ≈ 2.378 kg · 관성 ≈ 0.900 kg·m².</summary>
        public double BallDensity { get; set; }

        /// <summary>공 반발 <b>0.7</b> [실측]. ⚠ 패스 ① 의 «추정 0.9» 는 틀렸다.</summary>
        public double BallRestitution { get; set; }

        /// <summary>공 마찰 <b>0.5</b> [실측]. 접선 거동은 이 값 «혼자»가 아니라 <b>회전 관성과의 결합</b>이다.</summary>
        public double BallFriction { get; set; }

        /// <summary>공 선형 감쇠 <b>0</b> [실측].</summary>
        public double BallLinearDamping { get; set; }

        /// <summary>
        /// 공 각 감쇠 <b>0.01</b> [실측]. <c>preventRotation = false</c> 라 <b>공이 구른다</b> —
        /// 관측 각속도 −7.4 ~ +10.9 rad/s. 회전을 잠그면 접선 거동이 원리적으로 재현되지 않는다.
        /// </summary>
        public double BallAngularDamping { get; set; }

        /// <summary>
        /// 블록 충돌 상자 폭 <b>84</b> [4회차 실측 · <c>_worldInfo._w</c> 34개 전수 고유값].
        /// ⚠ <b>격자 피치(<see cref="BlockGridPitch"/> = 50)와 다르다</b> — 아래 주석.
        /// </summary>
        public double BlockCollisionWidth { get; set; }

        /// <summary>블록 충돌 상자 높이 <b>58.983051321428576</b> [4회차 실측]. 58 도 50 도 아니다.</summary>
        public double BlockCollisionHeight { get; set; }

        /// <summary>
        /// 블록 <b>배치</b> 격자 피치 <b>50</b> [실측].
        ///
        /// <para>
        /// ★★ <b>콜라이더 크기와 반드시 따로 둔다.</b> 피치 50 · 상자 84×58.983 이라
        /// <b>이웃 블록이 가로 34 px · 세로 8.98 px 겹친다.</b> 격자 크기로 콜라이더를 깔면
        /// <b>틈이 생겨 공이 블록 사이로 빠진다</b> — 그리고 그 증상은 단발 궤적 검사에 안 잡힌다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 이 값은 <b>배치에 쓰지 않는다</b> — 블록 중심은 <c>BlumgiBlockTable</c> 의 좌표를 그대로 쓴다.
        /// 격자로 재구성하면 원본 이상 #3(W1L1 원점 +2)이 반올림돼 사라진다. 여기 있는 것은 <b>검산용</b>이다.
        /// </para>
        /// </summary>
        public double BlockGridPitch { get; set; }

        /// <summary>블록 반발 <b>0</b> [실측]. ⚠ 그런데 «공이 블록에 튀는» 실효 반발은 <b>0.7</b> 이다 — 혼합 규칙이 <c>max</c> 다.</summary>
        public double BlockRestitution { get; set; }

        /// <summary>블록 마찰 <b>0.5</b> [실측]. 혼합 <c>sqrt(0.5×0.5) = 0.5</c>.</summary>
        public double BlockFriction { get; set; }

        /// <summary>골대 충돌체 반지름. <c>spr_BasketCollision</c> Box2D 원 <c>0.40219 m</c> → <b>20.11 px</b> [4회차 실측].</summary>
        public double HoopPostRadius { get; set; }

        /// <summary>골대 반발 <b>0.7</b> [실측].</summary>
        public double HoopRestitution { get; set; }

        /// <summary>골대 마찰 <b>0.5</b> [실측].</summary>
        public double HoopFriction { get; set; }

        /// <summary>
        /// 블롭 충돌 상자 폭 <b>87.13590823980422</b> [16회차 실측 · <b>시트 원문 직독</b>].
        ///
        /// <para>
        /// ★ 4회차의 <c>87.14</c> 는 이 값의 반올림이었다. 16회차가 이벤트 시트에서
        /// <c>TweenTwoProperties(slimeSquash, 1, 87.13590823980422, 50, 0.25, …)</c> 를 그대로 읽었다.
        /// <b>검산</b> — 밀도 100 × (87.13590823980422 / 50 m × 1 m) = <b>174.2718</b> 로
        /// 런타임 질량 <b>174.272</b> 와 소수 3자리까지 같다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 물리 fixture AABB(<c>88.136 × 51.005</c>)는 <b>이 값 + 폴리곤 스킨 0.5 px 사방</b>이다 —
        /// 스킨은 <see cref="BlumgiPhysicsSetup.Box2DPolygonSkin"/> 이 이미 넣는다. <b>여기에 더하지 마라.</b>
        /// </para>
        /// </summary>
        public double BlobCollisionWidth { get; set; }

        /// <summary>블롭 충돌 상자 높이 <b>50</b> [4회차 실측 · 16회차 4레벨 전수 재확인].</summary>
        public double BlobCollisionHeight { get; set; }

        /// <summary>
        /// 블롭 밀도 <b>100</b> [실측 · 16회차 4레벨 전수 재확인] — 공(1)의 100배라 공에 밀리지 않는다.
        /// <para>⚠ 이제 <b>공은 블롭과 아예 안 부딪힌다</b> (<see cref="BlumgiBallBody.ExcludeFromCollision"/>).
        /// 그래도 이 값은 그대로 둔다 — <b>블롭은 블록과 계속 부딪히고</b> 거기서 질량이 쓰인다.</para>
        /// </summary>
        public double BlobDensity { get; set; }

        /// <summary>
        /// 블롭 반발 <b>0.2</b> [실측 · 16회차 4레벨 전수 재확인].
        /// <para>⚠ 「실효 반발 <c>max(0.7, 0.2) = 0.7</c>」은 <b>«공과» 부딪힐 때의 이야기였다</b> —
        /// 그 쌍은 이제 원본대로 배제된다. 지금 이 값이 섞이는 상대는 <b>블록(0)</b> 이다.</para>
        /// </summary>
        public double BlobRestitution { get; set; }

        /// <summary>블롭 마찰 <b>0.5</b> [실측 · 16회차 4레벨 전수 재확인].</summary>
        public double BlobFriction { get; set; }

        /// <summary>
        /// 반발이 죽는 속도 임계 (world px/s). Box2D 표준 <c>b2_velocityThreshold = 1 m/s</c> → <b>50 px/s</b>.
        ///
        /// <para>
        /// ⚠ <b>런타임에서 직접 읽지 못했다</b> — 표준 Box2D 기본값이라 «추정»이다 (4회차 §4 미측정 1번).
        /// 유니티 기본 <c>Physics2D.bounceThreshold</c> 도 1(유닛/s)이라 <b>확정 E 개정 스케일에서 자동으로 맞는다</b> —
        /// 이 컬럼은 그것이 «우연히 맞은 것»이 아님을 코드가 확인하기 위해 둔다.
        /// </para>
        /// </summary>
        public double BounceThresholdSpeed { get; set; }

        /// <summary>
        /// ★★ 폴리곤 <b>스킨 두께</b> (world px) <b>0.5</b> — Box2D <c>b2_polygonRadius</c>.
        ///
        /// <para>
        /// [4회차 실측] 원본 fixture AABB 가 bbox 보다 <b>사방 0.5 px</b> 넓다 (20회차 재확증 —
        /// W1L5 발사대 bbox <c>[56.432, 294.759, 143.568, 344.759]</c> ↔ fixture AABB
        /// <c>[55.932, 294.259, 144.068, 345.259]</c>). 유니티 쪽 대응은
        /// <c>Physics2D.defaultContactOffset</c> 0.01 유닛 = <b>같은 0.5 px</b> 다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 이 컬럼은 «엔진에 넣는 값»이 아니라 <b>정착 침투 여유를 «유도»하는 근거</b>다
        /// (<see cref="BlumgiLevelRuntime.RestingPenetration"/>). 엔진 쪽 주입은
        /// <c>BlumgiPhysicsSetup</c> 이 따로 한다.
        /// </para>
        /// </summary>
        public double PolygonSkinThickness { get; set; }

        /// <summary>
        /// ★★ Box2D <b>선형 슬롭</b> (world px) <b>0.25</b> — <c>b2_linearSlop</c> 0.005 m ÷ <c>worldScale</c> 0.02.
        ///
        /// <para>
        /// 위치 솔버가 <c>separation</c> 을 <b>이 값만큼 «음수»로 남겨 두고 멈춘다</b>.
        /// 스킨과 합쳐 <c>2 × 스킨 − 슬롭 = 0.75 px</c> 이 되고, 그것이 <b>정착한 발사대가
        /// 블록 윗면 위로 «떠 있는» 간격</b>이다
        /// (<see cref="BlumgiLevelRuntime.RestingSeparation"/> · ★ 20회차 실측 5레벨 잔차 0.00053 px).
        /// </para>
        ///
        /// <para>
        /// ⚠ <c>추정(열거)</c> — 원본에서 이 상수를 읽는 접근자가 없다(19회차 §18-g 와 같은 성질).
        /// Box2D 2.3.x 기본값이고, <b>유도식이 5레벨에서 0.00053 px 안으로 맞는 것</b>이 그 간접 증거다.
        /// </para>
        /// </summary>
        public double LinearSlop { get; set; }

        /// <summary>
        /// 골대 충돌 기둥의 림 중심 기준 x 오프셋 (좌 −, 우 +) <b>75</b>.
        /// <b>5레벨 전수 실측 · 소수점까지 동일</b> [7회차 §9-a].
        /// </summary>
        public double GoalPostOffsetX { get; set; }

        /// <summary>
        /// 골대 충돌 기둥의 y 오프셋 <b>0</b> — 기둥 중심이 림 중심과 <b>같은 높이</b>다 [7회차 실측 5/5].
        ///
        /// <para>
        /// ⚠ <b>패스 ①-e 에서 −0.5 → 0 으로 바뀌었다.</b> 골대 기준점(<c>GoalRimY</c>)을
        /// 7회차가 뜬 <c>spr_BasketTop</c> <b>bbox 중심</b>으로 맞추면서 프레임이 0.5px 옮겨졌다 —
        /// <b>부품의 «절대» 자리는 바뀌지 않는다</b>(레벨 데이터의 <c>GoalRimY</c> 도 같이 0.5 내렸다).
        /// 이렇게 두면 여섯 상수가 전부 <c>07 §9-a</c> 실측표의 숫자와 <b>글자 그대로 같아진다.</b>
        /// </para>
        /// </summary>
        public double GoalPostOffsetY { get; set; }

        /// <summary>골대 그물(<c>spr_BasketBottom</c>) x 오프셋 <b>0</b> [7회차 실측 5/5].</summary>
        public double GoalNetOffsetX { get; set; }

        /// <summary>
        /// 골대 그물 y 오프셋 <b>+88</b> (그림 164 × 140) [7회차 실측 5/5].
        /// <b>프리팹 빌더가 이 값을 읽는다</b> — 그림 자리를 두 곳에 적지 않기 위해서다.
        /// </summary>
        public double GoalNetOffsetY { get; set; }

        /// <summary>골인 감지기 한 변. <c>spr_ballDetector</c> · <c>spr_ballDetector2</c> 둘 다 <b>50×50</b> [6회차 실측 bbox].</summary>
        public double DetectorSize { get; set; }

        /// <summary>
        /// 공이 <b>감지</b>에 쓰는 사각 한 변 <b>87</b> [6회차 실측].
        ///
        /// <para>
        /// ⚠ <b>물리 도형과 다르다.</b> 부딪히는 것은 원(<see cref="BallRadius"/> 43.5)이고
        /// 감지되는 것은 <b>87×87 사각</b>이다 — C3 는 겹침/충돌 트리거에 스프라이트 충돌 폴리곤을 쓴다.
        /// 엔진 <c>TestOverlap</c> 4066 샘플에서 두 모델이 갈리는 7프레임 전부 사각이 맞았다.
        /// </para>
        /// </summary>
        public double BallDetectionBoxSize { get; set; }

        /// <summary>
        /// 위 감지기 오프셋 x. 골대 중심 기준 <b>−0.70</b>
        /// [★ <b>7회차 실측 — W1L1~L5 다섯 레벨 전부 소수점까지 동일</b>].
        /// </summary>
        public double DetectorTopOffsetX { get; set; }

        /// <summary>
        /// 위 감지기 오프셋 y <b>−10.53</b> [★ <b>7회차 실측 5/5</b>].
        ///
        /// <para>
        /// ⚠ 원본 <c>_x/_y</c> 를 «좌표»로 읽으면 안 된다 — 감지기는 <c>spr_BasketTop</c> 의
        /// <b>자식이라 그 값이 부모 상대</b>다. <b>bbox 가 정본</b>이다.
        /// 다만 <b>이 오프셋만큼은</b> 부모 상대값과 결과가 같다 — 그래서 5레벨에서 «상수처럼» 보인다.
        /// </para>
        ///
        /// <para>
        /// ★★ <b>「W1L1 만 실측이고 나머지는 유도」는 6회차의 한계였다 — 7회차가 5레벨을 나란히 떠서
        /// 20개 값이 상수 2개로 접히는 것을 확인했다.</b> <see cref="BlumgiLevelData.DetectorOffsetDerived"/>
        /// 는 이제 <b>전 레벨 <c>false</c></b> 다. 되살리려면 실측 근거부터 가져와라.
        /// </para>
        /// </summary>
        public double DetectorTopOffsetY { get; set; }

        /// <summary>아래 감지기 오프셋 x <b>0</b> [7회차 실측 5/5].</summary>
        public double DetectorBottomOffsetX { get; set; }

        /// <summary>아래 감지기 오프셋 y <b>+100</b> [7회차 실측 5/5 · 정수로 일치].</summary>
        public double DetectorBottomOffsetY { get; set; }

        /// <summary>
        /// 발사 «전» 공이 놓여 있는 자리 x — <b>0</b> [6회차 실측].
        ///
        /// <para>
        /// ★★ <b>원본은 미발사 공을 경기장에 두지 않는다.</b> 레벨이 깔리면 물리 공은
        /// <c>(0, 10000)</c> · 속도 0 에서 시작해 <b>화면 밖으로 계속 낙하</b>한다
        /// (홀드 3ms 뒤 20초 관찰: x 고정 0 · y 32520 → 248293 · 감지기 겹침 0 · 전역 변화 0).
        /// 발사가 <c>Teleport</c> 로 공을 «데려온다».
        /// </para>
        ///
        /// <para>
        /// ⚠ 블롭 머리 위에 보이는 공은 <b>다른 오브젝트</b>(<c>spr_BallVisu</c>)다 —
        /// 그 자리는 <see cref="BlumgiLevelData.BallRestX"/> 다. 물리 공을 거기 두면
        /// <b>발사하지 않은 공이 굴러다니다 골이 된다</b> (패스 ①-c 위양성 2건의 뿌리).
        /// </para>
        /// </summary>
        public double BallParkX { get; set; }

        /// <summary>발사 전 공의 대기 y — <b>10000</b> [6회차 실측]. 여기서 그대로 자유낙하한다.</summary>
        public double BallParkY { get; set; }

        /// <summary>
        /// 이 y 를 넘어가면 「화면 밖으로 떨어졌다」 = <b>재장전</b>.
        /// 실측 보이는 world 범위가 y ∈ [−0.5, 1279.5] 이라 그 하단이다 (`02_시스템_홀드샷.md` §1).
        /// </summary>
        public double ReloadTriggerY { get; set; }

        /// <summary>
        /// 낡은 공을 시뮬에서 내리는 y.
        /// ⚠ 원본의 <b>공 파괴 임계 y 는 미측정</b>이다 — y = 23384 까지 살아 있는 것이 실측됐고
        /// 골든 최종 좌표는 y = 36524 까지 간다. 그 «위»의 값이라 실측과 모순되지 않는다.
        /// </summary>
        public double BallOutOfPlayY { get; set; }

        // ⚠ 여기 있던 `ReloadDelaySeconds` 는 «지웠다» (패스 ①-f).
        //   8회차가 「자동 재장전은 없다 — 기준점은 «다음 홀드를 누른 프레임»」을 실측했고,
        //   패스 ②-d 가 그 타이머를 걷어내면서 «아무도 안 읽는 값»이 됐다.
        //   안 읽는 값이 데이터에 남으면 다음 사람이 «있는 규칙»으로 읽는다 — 그래서 데이터에서도 뺐다.

        /// <summary>설계 뷰포트 폭 1280 [실측 <c>_originalViewportWidth</c>].</summary>
        public double DesignViewportWidth { get; set; }

        /// <summary>설계 뷰포트 높이 1280. <b>세로가 고정되는 축</b>이다 (scale outer).</summary>
        public double DesignViewportHeight { get; set; }

        /// <summary>야자수 실루엣 색. <b>테마를 따라가지 않는다</b> — 5레벨 전부 같다 [05_연출 §1 실측].</summary>
        public string PalmColorHex { get; set; }

        /// <summary>골대 림(밝은쪽). 전 레벨 동일 [05_연출 §1].</summary>
        public string GoalRimLightColorHex { get; set; }

        /// <summary>골대 림(그늘쪽). 전 레벨 동일.</summary>
        public string GoalRimShadeColorHex { get; set; }

        /// <summary>골 그물. 전 레벨 동일.</summary>
        public string GoalNetColorHex { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
