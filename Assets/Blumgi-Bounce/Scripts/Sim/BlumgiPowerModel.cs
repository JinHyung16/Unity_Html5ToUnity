using System;
using JinHyung.Data;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 「홀드 시간 → 발사 속력 |v0|」 — <b>원본의 실제 기전</b>이다. 피팅 곡선이 아니다.
    ///
    /// <para>
    /// ★ <b>학습 3회차가 원본 런타임 «전역 변수»를 직접 읽었다</b> [실측 · RUN C].
    /// 홀드 중 <c>forceShoot_P1</c> 이 <c>forceShootStart(10)</c> 에서 시작해
    /// <c>forceShootOffset(55)</c> /초로 차오르다 <b>100 에서 하드 클램프</b>된다.
    /// </para>
    ///
    /// <para>
    /// ★★★ <b>[정정 · 12회차] 속도 환산 상수 <c>20.95</c> 는 «틀렸다» — 그것은 궤적 피팅 잔재였다.</b>
    /// 12회차가 <b>중력이 한 틱도 안 걸린 «진짜 발사 프레임»</b>을 직독하니
    /// <c>|v0| / force</c> 가 5샷 전부 <b>소수 4자리까지 21.0272</b> 였고, 그 값의 정체가 열렸다:
    /// </para>
    ///
    /// <code>
    /// |v0| = force / (m · worldScale)          m = 공 질량 2.377871513 kg · worldScale = 0.02
    ///      = force / 0.047557430 = 21.0272 · force
    /// ⇒ 게임은 «크기가 정확히 force 인 임펄스»를 조준각 방향으로 준다.
    /// </code>
    ///
    /// <para>
    /// ★★ <b>그래서 여기에 상수를 «박지» 않는다 — <see cref="SpeedPerForce"/> 가 질량에서 «계산»한다.</b>
    /// 질량은 다시 <c>BallDensity · π · r²</c> 라, 반지름이나 밀도를 고치면 파워가 <b>같이 따라간다</b>.
    /// 상수로 두면 「밀도를 고쳤는데 파워는 옛날 값」인 상태가 조용히 생긴다.
    /// 20.95 는 정확값보다 <b>0.37 % 낮다</b> — force 100 에서 <c>|v0|</c> 2095 vs 2102.7 (7.7 px/s).
    /// <b>레벨 무관 전역</b>이다.
    /// </para>
    ///
    /// <code>
    /// force  = min(100, 10 + 55 · 홀드초)          → 포화 홀드 1636ms (실측 1633ms)
    /// |v0|   = 21.0272 · force                     → 캡 2102.7
    /// </code>
    ///
    /// <para>
    /// ⚠ <b>이 값들은 전역이다 — 레벨 데이터가 아니다.</b> 2회차가 「파워 곡선·캡이 레벨마다 다르다」로
    /// 적고 33점짜리 피팅 표를 남겼는데 <b>틀렸다</b>. 같은 홀드를 네 레벨에 쏘면 0.7~1.2% 안에서 같고,
    /// 캡은 L1·L2·L4 가 2097~2098 로 0.05% 안에서 같다 [3회차 §2-3·§2-4].
    /// 달라 보였던 것은 <b>공칭 홀드를 축으로 썼기 때문</b>이다.
    /// <b>표를 되살리지 마라</b> — 기전이 있는데 표를 두면 어느 쪽이 진실인지 갈린다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>원본의 파워는 «누른 프레임 수»로 양자화된다 — 우리는 그것을 재현하지 않는다.</b>
    /// 원본 실측: 릴리즈 직전 force 값이 <b>0.458 (= 55 × 8.33ms)</b> 간격으로 이산적이고,
    /// 1프레임 = <c>|v0|</c> <b>9.6 px/s</b> 다 [3회차 §4-2]. 재현하지 않는 근거 넷 —
    /// <list type="number">
    /// <item><b>원본의 격자 자체가 «가변»이다.</b> 원본 프레임 dt 가 7.2~9.5ms (σ 0.28) 로 흔들린다.
    /// 우리가 8.33ms 고정 격자로 양자화하면 <b>원본에 없던 새 격자</b>를 만드는 것이지 원본을 닮는 것이 아니다</item>
    /// <item><b>우리 엔진의 프레임이 이미 같은 일을 한다.</b> 사람이 누른 시간은 우리 쪽에서도 프레임 경계로 잘린다 —
    /// 여기에 양자화를 또 넣으면 <b>이중 양자화</b>가 된다</item>
    /// <item><b>양자가 채점 허용오차보다 작다.</b> 9.6 px/s 는 캡 2098 대비 <b>0.46%</b> 로,
    /// 골든 허용오차(|v0| ±3% · 궤적 ±2%)보다 한참 아래다</item>
    /// <item>원장 PD 판정: <b>「우리 쪽이 고정 dt 라 원본보다 더 결정적인 것은 결함이 아니다」</b></item>
    /// </list>
    /// ⚠ 대신 <b>지우지도 않았다</b> — <see cref="OriginFrameQuantumSpeed"/> 가 원본의 양자 폭을 그대로 돌려준다.
    /// 「원본 이상 #2(같은 홀드가 갈린다)」의 크기를 재려면 이 값이 필요하다.
    /// </para>
    /// </summary>
    public sealed class BlumgiPowerModel
    {
        /// <summary>원본 프레임 간격(초) — rAF 12017표본의 <b>중앙값 8.33ms</b>, 120Hz [3회차 실측].</summary>
        private const double OriginFrameSeconds = 0.00833;

        private readonly double _forceStart;
        private readonly double _forceRatePerSecond;
        private readonly double _forceMax;
        private readonly double _ballMassKg;
        private readonly double _speedPerForce;

        public BlumgiPowerModel(BlumgiConfigData config)
        {
            _forceStart = config.ForceShootStart;
            _forceRatePerSecond = config.ForceShootRatePerSecond;
            _forceMax = config.ForceShootMax;

            // ★ 환산 스케일은 «데이터»가 아니라 BlumgiUnits 한 곳이 갖는다 (worldScale = 1/50 = 0.02).
            double worldScale = 1.0 / BlumgiUnits.WorldPixelsPerUnit;
            double radiusMeters = config.BallRadius * worldScale;

            // Box2D 원 fixture 질량 = 밀도 × π r². 9회차가 원본에서 읽은 2.377871513 kg 과 같다 [실측].
            _ballMassKg = config.BallDensity * Math.PI * radiusMeters * radiusMeters;

            // ★★ 상수가 아니다 — 질량·스케일에서 «나온다». 12회차 5/5 소수 4자리 일치 (21.0272).
            _speedPerForce = 1.0 / (_ballMassKg * worldScale);
        }

        /// <summary>
        /// 공 질량(kg) — <c>밀도 × π r²</c>. 9회차가 원본 Box2D 에서 «읽은» <b>2.377871513</b> 과 같다 [실측].
        /// 채점기가 엔진 <c>Rigidbody2D.mass</c> 와 이 값을 따로 대조한다 (§0-d).
        /// </summary>
        public double BallMassKg
        {
            get { return _ballMassKg; }
        }

        /// <summary>
        /// 힘 → 속력 환산 <c>k</c>. ★ <b>데이터 컬럼이 아니라 «유도값»이다</b> —
        /// <c>1 / (m · worldScale) = 21.0272</c> [12회차 실측 5/5].
        /// <b>여기에 상수를 되돌려 놓지 마라</b> — 질량이 바뀌면 파워가 따라가야 한다.
        /// </summary>
        public double SpeedPerForce
        {
            get { return _speedPerForce; }
        }

        /// <summary>파워가 클램프에 닿는 홀드(ms). 이론 1636ms · 실측 1633ms (0.2% 일치).</summary>
        public double SaturationHoldMs
        {
            get
            {
                if (_forceRatePerSecond <= 0.0)
                    return 0.0;

                return (_forceMax - _forceStart) / _forceRatePerSecond * 1000.0;
            }
        }

        /// <summary>파워 상한 |v0| — <b>전역 2098 px/s</b> [실측 L1·L2·L4 2097~2098].</summary>
        public double MaxSpeed
        {
            get { return _forceMax * _speedPerForce; }
        }

        /// <summary>
        /// 원본이 «한 프레임» 더 눌렸을 때 벌어지는 |v0| 차 = <b>9.6 px/s</b>.
        /// 우리는 양자화하지 않으므로 이 값은 <b>거동에 쓰이지 않는다</b> — 원본 산포를 «재는» 눈금이다.
        /// </summary>
        public double OriginFrameQuantumSpeed
        {
            get { return _forceRatePerSecond * OriginFrameSeconds * _speedPerForce; }
        }

        /// <summary>홀드 ms 시점의 <c>forceShoot_P1</c>. 원본 변수와 같은 눈금이다.</summary>
        public double ForceAt(double holdMs)
        {
            if (holdMs <= 0.0)
                return _forceStart;

            double force = _forceStart + (_forceRatePerSecond * holdMs / 1000.0);
            return force > _forceMax ? _forceMax : force;
        }

        /// <summary>홀드 ms → 발사 속력 |v0| (world px/s).</summary>
        public double Speed(double holdMs)
        {
            return ForceAt(holdMs) * _speedPerForce;
        }

        /// <summary>
        /// force 를 «홀드 초»로 되돌린다 — <c>(force − 10) / 55</c>.
        ///
        /// <para>
        /// ★ 발사점 트윈이 <b>초</b> 로 도는데(<see cref="BlumgiSquashTween"/>) 스윕·채점기는 <b>force</b> 축으로
        /// 말하기 때문에 그 사이를 잇는 자리다. <b>클램프된 force(100)는 «최소» 홀드를 돌려준다</b> —
        /// 포화 뒤에도 계속 눌렸을 수 있으므로 이 역산은 <b>포화 아래에서만</b> 정확하다.
        /// </para>
        /// </summary>
        public double HoldSecondsAtForce(double force)
        {
            if (_forceRatePerSecond <= 0.0)
                return 0.0;

            double seconds = (force - _forceStart) / _forceRatePerSecond;
            return seconds < 0.0 ? 0.0 : seconds;
        }
    }
}
