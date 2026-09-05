using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 레벨 하나를 «시뮬이 바로 쓸 모양»으로 펼쳐 둔 것.
    ///
    /// <para>
    /// ★★ <b>충돌 도형은 여기 없다</b> (패스 ①-c). 블록·골대·블롭·공의 충돌은
    /// <b>유니티 2D 물리(Box2D)</b> 가 프리팹의 콜라이더로 푼다 — 원본이 Box2D 이기 때문이다.
    /// 여기 남은 사각형은 <b>골인 감지기 둘뿐</b>이다. 감지기는 원본에서도 물리체가 아니다
    /// [4회차 실측 — W1L1 물리체 38 = 블록 34 + 공 + 블롭 + 골대판정 2. 감지기는 그 안에 없다].
    /// </para>
    ///
    /// <para>
    /// ★ <b>여기서 좌표를 만들지 않는다.</b> 블록 중심은 데이터 그대로 쓴다 —
    /// 격자 인덱스로 재구성하면 W1L1 의 <c>+2</c> 어긋남(원본 이상 #3)이 반올림돼 사라진다
    /// (`04_UIUX규칙.md` 2-e 이관 지침).
    /// </para>
    ///
    /// <para>
    /// 골대는 원본에서 6개 오브젝트의 묶음이지만 <b>묶음 내부 상대 위치가 전 레벨에서 같다</b>
    /// [★ 7회차 실측 — W1L1~L5 <b>전수</b>, 20개 값이 상수 2개로 접혔다].
    /// 그래서 <b>중심 하나만</b> 레벨 데이터로 두고 나머지는 설정의 고정 오프셋으로 편다 —
    /// <b>부품 좌표를 레벨마다 들면 진실이 두 곳으로 갈린다.</b>
    /// </para>
    /// </summary>
    public sealed class BlumgiLevelRuntime
    {
        public BlumgiLevelData Level { get; private set; }

        public BlumgiConfigData Config { get; private set; }

        /// <summary>파워 기전. <b>레벨마다 다르지 않다</b> — 설정의 전역 상수로 만든다 [3회차 정정].</summary>
        public BlumgiPowerModel PowerModel { get; private set; }

        /// <summary>
        /// 블록 «중심» 전수 (world px). <b>순서는 데이터 순서 그대로</b>다.
        /// ⚠ 크기는 여기 없다 — 콜라이더는 프리팹이 든다 (84 × 58.983, 격자 피치 50 과 «다르다»).
        /// </summary>
        public IReadOnlyList<BlumgiVec2> BlockCenters { get; private set; }

        /// <summary>
        /// 골인 감지기 (위). <b>겹치면 «득점 자격»이 켜진다</b> — 끄는 조건은 없다 [6회차 실측 EVENT#17].
        /// ⚠ 원본에서 이건 <c>spr_ballDetector</c> 라는 <b>보이지도 물리도 없는 전용 오브젝트</b>다.
        /// 4회차가 잡은 <c>spr_BasketCollision</c>(림 2개)은 <b>공이 튕기는 실체</b>지 판정자가 아니다.
        /// </summary>
        public BlumgiAabb DetectorTop { get; private set; }

        /// <summary>골인 감지기 (아래) <c>spr_ballDetector2</c>. <b>여기 «닿기 시작»하는 순간</b>이 골이다 [EVENT#18].</summary>
        public BlumgiAabb DetectorBottom { get; private set; }

        /// <summary>
        /// 이 레벨의 감지기 좌표가 <b>유도값</b>인가.
        /// ★ <b>월드1 5레벨은 전부 <c>false</c></b> — 7회차가 다섯 레벨을 나란히 떠 오프셋이
        /// 소수점까지 같음을 확인했다. 채점기가 이걸 화면에 찍는다 —
        /// <b>유도값을 실측처럼 보이게 두지 않는다</b> (월드2 가 오면 다시 <c>true</c> 가 생긴다).
        /// </summary>
        public bool DetectorOffsetDerived { get; private set; }

        /// <summary>
        /// ★★★ <b>발사 시작 위치는 «상수»가 아니라 «함수»다</b> [12회차 실측] —
        /// <see cref="LaunchPositionAt"/> 로 홀드를 넣어 얻는다.
        ///
        /// <para>
        /// 여기 있던 <c>LaunchPosition</c> 속성(레벨 데이터 <c>LaunchX/LaunchY</c>)은 <b>지웠다.</b>
        /// 홀드가 길수록 블롭이 납작해져 <b>발사점이 9.7 px 내려간다</b> — 한 점만 재면 함수도 상수로 보인다.
        /// </para>
        /// </summary>
        public BlumgiVec2 LaunchOrigin
        {
            get { return BallRestPosition; }
        }

        /// <summary>발사 방향 단위벡터. <b>홀드와 무관한 레벨 상수</b>다 [실측 §5-0].</summary>
        public BlumgiVec2 LaunchDirection { get; private set; }

        /// <summary>
        /// 블롭 머리 위 <b>«보이는» 공</b>(<c>spr_BallVisu</c>)의 자리.
        ///
        /// <para>
        /// ⚠ <b>물리 공이 여기 있는 것이 아니다</b> [6회차 실측] — 발사 전 물리 공은
        /// <c>BlumgiConfigData.BallParkX/Y</c> = (0, 10000) 에서 자유낙하한다.
        /// 이 값은 <b>연출이 쓸 자리</b>다.
        /// </para>
        /// </summary>
        public BlumgiVec2 BallRestPosition { get; private set; }

        /// <summary>발사 전 물리 공의 대기 자리 (0, 10000) [6회차 실측]. <b>경기장 밖</b>이다.</summary>
        public BlumgiVec2 BallParkPosition { get; private set; }

        /// <summary>
        /// ★★★ <b>발사대(블롭)의 «정착한» b2Body 원점</b> — <b>레벨 데이터가 아니라 «유도값»이다</b>
        /// [★ 20회차 실측 · 5레벨 잔차 0 · 재발방지 #102 · #53].
        ///
        /// <code>
        /// bodyY = (깔고 앉은 블록 행 y) − 블록반높이(29.49153) − 정착간격(0.75)
        /// </code>
        ///
        /// <para>
        /// <b>왜 데이터가 아닌가.</b> 발사대는 <c>iv[0]/iv[1]</c> 에 적힌 자리에 authored 되지만
        /// <b>dynamic body</b> 라 그대로 떨어져 아래 블록 위에 얹힌다. 그 정착 위치가
        /// 다섯 레벨 전부 위 식으로 <b>잔차 0.00053 px</b> 안이다 (실측이 소수 3자리다):
        /// L1 627→596.759 · L2 975→944.759 · L3 375→344.759 · L4 425→394.759 · <b>L5 375→344.759</b>.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>이전에는 다섯 값을 레벨 표에 적어 두었고, 못 잰 W1L5 한 칸을 반올림
        /// <c>345.0</c> 으로 채워 0.241 px 어긋나 있었다.</b> 기전으로 옮기면 못 잰 레벨도 맞고,
        /// <b>원본 실수까지 저절로 따라온다</b> — 원본이상 #3(W1L1 블록 행 <c>627 = 625 + 2</c>)이
        /// 발사대 y <c>596.759</c> 에 그대로 물려 있다 (§18-j).
        /// </para>
        ///
        /// <para>
        /// x 는 <c>iv[0]</c> 그대로다 — 수평 접촉이 없어 안 움직인다 [5/5 실측].
        /// </para>
        /// </summary>
        public BlumgiVec2 LauncherBodyPosition { get; private set; }

        /// <summary>
        /// 발사대가 <b>깔고 앉은 블록 행</b>의 y (world px). 유도의 «입력»이라 밖에서 검산할 수 있게 연다.
        /// 못 찾았으면 <c>double.NaN</c> 이다 — 그때는 <see cref="LauncherBodyPosition"/> 이
        /// authored y 로 떨어진다(폴백을 조용히 만들지 않는다).
        /// </summary>
        public double LauncherSupportRowY { get; private set; }

        /// <summary>
        /// ★★ <b>정착 «간격»</b> (world px) <c>= 2 × 폴리곤 스킨 − 선형 슬롭 = 0.75</c> —
        /// 정착한 몸이 상대 면보다 이만큼 <b>«위»에 뜬 채로 멈춘다</b>.
        ///
        /// <para>
        /// <b>왜 이 식인가</b> <c>추정(기전)</c>. Box2D 접촉 솔버가 다루는 <c>separation</c> 은
        /// <b>기하 거리에서 두 폴리곤의 스킨 합(0.5 + 0.5 = 1.0)을 뺀 값</b>이고,
        /// 위치 솔버는 그것을 <c>−b2_linearSlop</c>(−0.25) 로 민다.
        /// ⇒ <b>기하 거리 = 1.0 − 0.25 = 0.75 px</b> 가 남는다. 「파고든다」가 아니라 «뜬다»다.
        /// </para>
        ///
        /// <para>
        /// ★ <b>검산</b> [20회차 실측 5레벨] — 이 식으로 유도한 발사대 y 가 원본 실측과
        /// <b>5/5 모두 0.00053 px 안</b>이다 (596.75847 ↔ 596.759 …).
        /// 남는 0.0005 px 는 <b>위치 솔버가 6반복으로 완전히 수렴하지 않는 잔차</b>로 읽는다 —
        /// ⚠ 이 잔차를 없애려고 <b>0.7495 같은 «맞춘 상수»를 박지 않는다</b> (재발방지 #48).
        /// 원본 실측 자체가 소수 3자리라 그 아래는 «분해할 수 있는 값»이 아니다.
        /// </para>
        ///
        /// <para>
        /// ★ 이 상수는 발사대 말고 <b>다른 정착 판정의 근거</b>이기도 하다 —
        /// 「블록 위에 놓인 것」의 기대 y 는 전부 이 식이다.
        /// </para>
        /// </summary>
        public double RestingSeparation
        {
            get { return 2.0 * Config.PolygonSkinThickness - Config.LinearSlop; }
        }

        public BlumgiLevelRuntime(
            BlumgiLevelData level,
            IReadOnlyList<BlumgiBlockData> blocks,
            BlumgiConfigData config)
        {
            Level = level;
            Config = config;
            PowerModel = new BlumgiPowerModel(config);

            LaunchDirection = BlumgiVec2.FromAngleUpPositive(level.LaunchAngleDeg);
            BallRestPosition = new BlumgiVec2(level.BallRestX, level.BallRestY);
            BallParkPosition = new BlumgiVec2(config.BallParkX, config.BallParkY);
            DetectorOffsetDerived = level.DetectorOffsetDerived;

            int count = blocks == null ? 0 : blocks.Count;
            var centers = new List<BlumgiVec2>(count);

            for (int i = 0; i < count; i++)
            {
                BlumgiBlockData b = blocks[i];

                if (b == null)
                    continue;

                centers.Add(new BlumgiVec2(b.X, b.Y));
            }

            BlockCenters = centers;

            ResolveLauncherBody();

            DetectorTop = BlumgiAabb.FromCenterSize(
                level.GoalRimX + config.DetectorTopOffsetX,
                level.GoalRimY + config.DetectorTopOffsetY,
                config.DetectorSize,
                config.DetectorSize);

            DetectorBottom = BlumgiAabb.FromCenterSize(
                level.GoalRimX + config.DetectorBottomOffsetX,
                level.GoalRimY + config.DetectorBottomOffsetY,
                config.DetectorSize,
                config.DetectorSize);
        }

        /// <summary>
        /// ★★★ 발사대가 <b>어느 블록 행 위에 정착하는가</b>를 찾아 body 원점을 유도한다
        /// [★ 20회차 실측 — 5레벨 잔차 0].
        ///
        /// <para>
        /// authored 자리(<c>iv[0]</c>, <c>iv[1]</c>)에서 <b>아래로</b> 떨어뜨려(원본 y 는 아래가 +)
        /// <b>가로로 겹치는 첫 블록 행</b>을 만나는 것과 같다. 가로 겹침은
        /// <c>|블록x − 발사대x| &lt; (블롭 반폭 + 블록 반폭)</c> 이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>실제로 떨어뜨려 재지 않는다.</b> 물리로 정착시키면 프레임 수·초기 속도에 따라
        /// 값이 흔들리고 <b>레벨을 세우는 시점에 이미 필요한 값</b>이라 쓸 수 없다.
        /// 여기서는 «어디에 앉는가»만 기하로 풀고, 침투량은 <see cref="RestingPenetration"/> 로 유도한다.
        /// </para>
        /// </summary>
        private void ResolveLauncherBody()
        {
            double launcherX = Level.LauncherBodyX;
            double homeY = Level.LauncherHomeY;

            // 가로로 «겹치는» 조건 — 두 상자의 반폭 합보다 중심 x 차가 작아야 한다.
            double reach = (Config.BlobCollisionWidth + Config.BlockCollisionWidth) * 0.5;

            double supportY = double.NaN;

            for (int i = 0; i < BlockCenters.Count; i++)
            {
                BlumgiVec2 b = BlockCenters[i];

                if (b.Y < homeY)
                    continue;                                   // authored 자리보다 «위»는 못 받친다

                if (System.Math.Abs(b.X - launcherX) >= reach)
                    continue;                                   // 가로로 안 겹친다

                if (double.IsNaN(supportY) || b.Y < supportY)
                    supportY = b.Y;                             // 가장 «먼저 만나는» 행
            }

            LauncherSupportRowY = supportY;

            if (double.IsNaN(supportY))
            {
                // 폴백을 «조용히» 만들지 않는다 — 받칠 것이 없으면 데이터가 이상한 것이다.
                Log.Error($"{Level.Code}: 발사대({launcherX}, {homeY}) 를 받치는 블록 행이 없다 — authored y 를 그대로 쓴다");
                LauncherBodyPosition = new BlumgiVec2(launcherX, homeY);
                return;
            }

            LauncherBodyPosition = new BlumgiVec2(
                launcherX,
                supportY - Config.BlockCollisionHeight * 0.5 - RestingSeparation);
        }

        /// <summary>
        /// ★★★ <b>이 홀드로 쐈을 때의 발사점</b> (world px)
        /// [★ 14회차-b 실측 · <b>5레벨 38점 · 잔차 y ≤ 0.066 px · x ≤ 0.002 px</b> — 12회차 W1L3 5점을 대체].
        ///
        /// <code>
        /// 발사점 = 정지 머리공 중심 + (LaunchTweenOffsetX, LaunchTweenOffsetY) · sin(π/2 · min(1, 홀드초/1.5))
        /// </code>
        ///
        /// <para>
        /// <b>왜 정지 머리공에서 나오나.</b> 12회차 모델의 절편이 <c>spr_BallVisu</c> bbox 중심과
        /// <b>0.07 px</b> 로 붙는다 — 블롭이 바닥 고정으로 납작해지면서 그 위의 머리공이 따라 내려가고,
        /// <b>발사는 그 자리에서 걸린다.</b>
        /// </para>
        ///
        /// <para>
        /// ★★★ <b>[14회차-b] 「레벨별인가 전역인가」가 닫혔다 — 트윈은 «전역»이고 레벨은 «절편»만 갖는다.</b>
        /// 절편을 각 레벨의 정지 머리공 중심으로 <b>고정</b>하고 진폭만 하나로 맞추니 5레벨 38점이
        /// 0.066 px 안에 들어왔다. ⇒ 레벨 데이터에 <c>Launch*</c> 컬럼을 <b>되살릴 이유가 영구히 없다</b>.
        /// </para>
        ///
        /// <para>
        /// ⚠ 시계는 <see cref="BlumgiSquashTween"/> <b>한 곳</b>이다 — 블롭·머리공 연출과 같은 시계다.
        /// 여기에 <c>1.5</c> 나 <c>sin</c> 을 다시 적으면 진실이 갈린다.
        /// </para>
        /// </summary>
        public BlumgiVec2 LaunchPositionAt(double holdMs)
        {
            double eased = BlumgiSquashTween.Ease(holdMs / 1000.0);

            return new BlumgiVec2(LaunchOrigin.X + Config.LaunchTweenOffsetX * eased,
                                  LaunchOrigin.Y + Config.LaunchTweenOffsetY * eased);
        }

        /// <summary>
        /// 데이터에서 레벨 하나를 꺼내 펼친다. 조회는 <see cref="GameRoot"/> 한 곳을 지난다.
        /// 없으면 <c>null</c> — <b>폴백 레벨을 만들지 않는다.</b>
        /// </summary>
        public static BlumgiLevelRuntime Build(string levelCode)
        {
            BlumgiConfigData config = GameRoot.Instance.BlumgiConfigDataContainer?.Config;
            BlumgiLevelData level = GameRoot.Instance.BlumgiLevelDataContainer?.GetByCode(levelCode);

            if (config == null || level == null)
                return null;

            IReadOnlyList<BlumgiBlockData> blocks = GameRoot.Instance.BlumgiBlockDataContainer?.GetGroup(levelCode);

            return new BlumgiLevelRuntime(level, blocks, config);
        }
    }
}
