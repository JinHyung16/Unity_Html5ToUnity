using System;
using UnityEngine;

namespace JinHyung.UI.Fx
{
    /// <summary>펄스의 «식». 이징 프리셋이 아니라 <b>원본이 쓴 식</b>을 고른다 — 새 원본이 오면 식을 하나 «추가»한다.</summary>
    public enum EUiPulseKind
    {
        /// <summary><c>1 + 0.5·(sin(t·ω)+1)·A</c> — 1 ↔ 1+A. 캐주얼 「시작」 버튼의 «또잉또잉».</summary>
        SinePulse,

        /// <summary><c>1 + A·sin(t·ω)</c> — 1−A ↔ 1+A. 「선택」 버튼처럼 살짝 흔들리는 것.</summary>
        SineWobble,

        /// <summary>커브가 곧 식이다 — 가로 0~1 이 한 주기, 세로가 배율.</summary>
        Curve,
    }

    /// <summary>
    /// 항상 숨쉬는 배율. 식은 <see cref="EUiPulseKind"/>, 값은 <b>프리팹</b>이 든다 (아트가 눈으로 맞추는 값이라 코드 자리가 아니다).
    ///
    /// <para>
    /// 다른 몫(호버·상태)과는 <see cref="UiScaleStack"/> 이 곱한다. 시간은 밀리초 — 원본 식들이 ms 단위라
    /// 소스 상수(<c>0.005</c> 같은 rad/ms)를 «환산 없이» 그대로 넣기 위해서다.
    /// </para>
    ///
    /// <para>
    /// 정지 프리뷰·검수용 <see cref="SetPhase01"/> — 골(0)과 마루(0.5)에 «앉혀» 캡처를 결정적으로 만든다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(UiScaleStack))]
    public sealed class UiScalePulse : MonoBehaviour, IUiScaleFactor
    {
        [SerializeField] private EUiPulseKind _kind = EUiPulseKind.SinePulse;

        [Tooltip("진폭 A — SinePulse 는 1↔1+A, SineWobble 은 1±A")]
        [SerializeField] private float _amplitude = 0.1f;

        [Tooltip("각속도 ω (rad/ms). 주기 = 2π/ω ms")]
        [SerializeField] private float _speedPerMs = 0.005f;

        [Tooltip("시작 위상 (rad)")]
        [SerializeField] private float _startPhase;

        [Tooltip("Curve 일 때 — 가로 0~1 이 한 주기, 세로가 배율")]
        [SerializeField] private AnimationCurve _curve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Curve 일 때 한 주기 (ms)")]
        [SerializeField] private float _curvePeriodMs = 1000f;

        private double _elapsedMs;
        private bool _frozen;

        public float ScaleFactor { get; private set; } = 1f;

        /// <summary>한 주기 (ms). 검사가 «반 주기 뒤에 값이 다른가»를 잴 때 쓴다.</summary>
        public float PeriodMs
        {
            get { return _kind == EUiPulseKind.Curve ? _curvePeriodMs : (float)(2.0 * Math.PI / Math.Max(1e-6, _speedPerMs)); }
        }

        /// <summary>프리팹 빌더·데이터가 값을 넣는 자리. 인스펙터 값과 같은 것을 코드로 넣을 뿐이다.</summary>
        public void Configure(EUiPulseKind kind, float amplitude, float speedPerMs, float startPhase = 0f)
        {
            _kind = kind;
            _amplitude = amplitude;
            _speedPerMs = speedPerMs;
            _startPhase = startPhase;
            Evaluate();
        }

        public void ConfigureCurve(AnimationCurve curve, float periodMs)
        {
            _kind = EUiPulseKind.Curve;
            _curve = curve;
            _curvePeriodMs = periodMs;
            Evaluate();
        }

        /// <summary>처음부터 다시 숨쉰다 — 창을 열 때 부른다 (원본도 열릴 때 <c>elapsed = 0</c>).</summary>
        public void Restart()
        {
            _elapsedMs = 0.0;
            _frozen = false;
            Evaluate();
        }

        /// <summary>정지 프리뷰용 — 한 주기 안의 자리(0~1)에 앉히고 멈춘다. <see cref="Restart"/> 로 푼다.</summary>
        public void SetPhase01(float t)
        {
            _elapsedMs = Mathf.Repeat(t, 1f) * PeriodMs;
            _frozen = true;
            Evaluate();
        }

        private void OnEnable()
        {
            Restart();
        }

        private void Update()
        {
            if (_frozen)
                return;

            _elapsedMs += Time.deltaTime * 1000.0;
            Evaluate();
        }

        private void Evaluate()
        {
            switch (_kind)
            {
                case EUiPulseKind.SinePulse:
                    ScaleFactor = (float)(1.0 + 0.5 * (Math.Sin(_elapsedMs * _speedPerMs + _startPhase) + 1.0) * _amplitude);
                    break;
                case EUiPulseKind.SineWobble:
                    ScaleFactor = (float)(1.0 + _amplitude * Math.Sin(_elapsedMs * _speedPerMs + _startPhase));
                    break;
                default:
                    ScaleFactor = _curve.Evaluate(Mathf.Repeat((float)(_elapsedMs / Math.Max(1e-3, _curvePeriodMs)), 1f));
                    break;
            }
        }
    }
}
