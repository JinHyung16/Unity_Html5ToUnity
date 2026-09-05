using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 카메라 연출. ★ <b>카메라 배율/위치는 «레벨 데이터»가 아니라 «연출»이다</b> [UIUX 2-f 이관 지침].
    /// 정착 배율은 5레벨 전부 1.0 이고, 움직이는 것은 아래 둘뿐이다.
    ///
    /// <para>
    /// ★★★ <b>[정정 · 8회차] 1·2회차의 「줌 1.2936 → 1.0 단조」는 화면 크기비로 잰 근사였다.</b>
    /// 8회차가 시트에서 트윈 자체를 읽었다 [실측]:
    /// <c>System.SetLayoutScale(tweenManager.Value("zoom"))</c> · 값 <b>0.8 → 1.0</b> ·
    /// <b>2.0 s</b> · <b>easeOutElastic</b>. 겉보기 배율로는 1/0.8 = <b>1.25</b> 라
    /// 예전의 1.29 와 «크기»는 비슷했지만, <b>단조가 아니라 탄성 진동</b>이다 —
    /// 실측 궤적이 0.806 → (1 통과 t≈140) → <b>최대 1.075</b>(t≈264) → <b>최소 0.974</b>(t≈570) →
    /// 1.009(t≈880) → 0.997(t≈1150) → 1.000 이다.
    /// <b>단조 이징(예전 <c>Smooth</c>)으로 만들면 «되돌아 튕기는» 구간이 통째로 사라진다.</b>
    /// </para>
    ///
    /// <para>
    /// ★★★ <b>[정정 · 17회차] 줌은 «트윈으로 내려가지 않는다 — 한 프레임 만에 점프»한다.</b>
    /// rAF 매 프레임 채록에서 <b>골 프레임 1.00 → 그 다음 프레임(+8.7 ms) 0.81</b> 이었다 [실측].
    /// 즉 시작이 <b>불연속</b>이고 그 뒤가 탄성 복귀다 — 피크 <b>+7 % @ +224 ms</b>,
    /// 골 <b>−3 % @ +539 ms</b>, 진동 주기 <b>≈ 0.6 s</b>(= easeOutElastic 2 s 의 표준 주기 0.3 × 2.0).
    /// <b>8회차 모형(0.8 → 1.0 · 2 s · easeOutElastic)이 그대로 이 표본과 맞는다</b> —
    /// 같은 시각을 넣으면 1.064 / 0.975 가 나온다(실측 1.07 / 0.97). <b>계수를 고치지 않았다</b> (재발방지 #48).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>그래서 「시작값 0.8 을 못 읽는다」는 우리 검사의 흔들림은 «원본이 불연속»이었기 때문이다.</b>
    /// 시작값을 절대값으로 읽으려 하지 말고 <b>한 프레임 낙차</b>(<see cref="ZoomDeltaThisFrame"/>)로 잰다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>[보강 · 17회차] 셰이크가 줌보다 «한 프레임 먼저» 시작한다</b> [실측 —
    /// 셰이크 +6835.8 ms(골 프레임) · 줌 +6844.5 ms]. 그리고 <b>골인에도 줌·셰이크가 돈다</b> —
    /// 예전에는 레벨 시작에만 걸어 뒀는데, 17회차가 <b>골 프레임에 도는 것을 직접 쟀다</b>
    /// (셰이크 지속 <b>398.6 ms</b> · 진폭 관측 ±15 px). 레벨 시작 쪽은 1·2회차 화면 관측이 근거라 <b>둘 다 남긴다</b>.
    /// </para>
    ///
    /// <para>
    /// ★★★ <b>[정정 · 8회차] 셰이크는 «줌»이 아니라 «스크롤»이다.</b>
    /// 액션이 <c>Cam[ScrollTo].Shake(20, 0.4, 0)</c> 라 흔들리는 것은 <c>layout._scrollX/_scrollY</c> 이고
    /// <b>카메라 배율은 안 건드린다</b> [실측]. 그리고 정현파가 아니라 <b>매 프레임 무작위</b>다 —
    /// 실측 <c>sx</c> 628.7~657.2(기준 640) ⇒ <b>최대 ±17.2 px</b>, <b>t ≈ 350 ms 에 ±2 px 이내</b>로 잦아든다.
    /// 예전의 「줌 ±3 %」는 <b>±20 px / 1280 px = ±1.6 %</b> 로 정정되고, 그마저 배율이 아니라 위치다.
    /// </para>
    /// </summary>
    public sealed class BlumgiCameraDirector : MonoBehaviour
    {
        /// <summary>줌 트윈의 시작값 [8회차 실측 · <c>TweenValue("zoom", 0.8 → 1)</c>].</summary>
        private const float PunchStartZoom = 0.8f;

        /// <summary>줌 트윈 지속 [실측 2.0 s]. ⚠ 예전 값 0.4 s 는 «1 로 처음 도달하는 시각»을 지속으로 오독한 것이다.</summary>
        private const float PunchSeconds = 2f;

        /// <summary>
        /// 셰이크 크기 (원본 world px) [실측 · 액션 인자 <c>Shake(20, …)</c>].
        /// 화면 실측 최대 편차는 ±17.2 px 로, 무작위 추출이 20 을 다 안 채운 것과 정합한다.
        /// </summary>
        private const float ShakeMagnitudeWorld = 20f;

        /// <summary>셰이크 지속 [실측 · 액션 인자 <c>Shake(…, 0.4, …)</c>]. 350 ms 에 ±2 px 이내 = 선형 감쇠와 정합.</summary>
        private const float ShakeSeconds = 0.4f;

        [SerializeField] private Camera _camera;

        private float _baseSize;
        private Vector3 _basePosition;

        private float _punchTimer = -1f;

        /// <summary>
        /// ★ <b>줌을 «다음 프레임»에 시작한다</b> — 원본이 골 프레임에는 1.00 이고
        /// 그 다음 프레임에 0.81 로 «점프»한다 [17회차 실측]. 셰이크는 골 프레임에 바로 시작한다.
        /// </summary>
        private bool _punchPending;

        private float _shakeTimer = -1f;
        private int _shakeFrame;

        private float _lastZoomValue = 1f;

        /// <summary>지금 줌 트윈 값 (0.8 → 1.0). <b>검사가 이것을 읽어 탄성 진동을 채점한다</b>.</summary>
        public float ZoomValue { get; private set; } = 1f;

        /// <summary>지금 셰이크 변위 (원본 world px). 같은 이유로 노출한다.</summary>
        public Vector2 ShakeOffsetWorld { get; private set; }

        /// <summary>
        /// 이번 프레임에 줌이 «얼마나 뛰었나» (부호 있는 변화량).
        /// ★ <b>검사가 «불연속 점프»를 이것으로 잰다</b> — 절대 시작값은 원리적으로 프레임 위상에 걸린다.
        /// </summary>
        public float ZoomDeltaThisFrame { get; private set; }

        /// <summary>셰이크가 도는 중인가. 검사가 «줌보다 먼저 시작한다»를 이것으로 본다.</summary>
        public bool IsShaking
        {
            get { return _shakeTimer >= 0f; }
        }

        /// <summary>줌 펀치가 도는 중인가 (다음 프레임 예약 상태는 «아직 아니다»).</summary>
        public bool IsZoomPunching
        {
            get { return _punchTimer >= 0f; }
        }

        private void Awake()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();

            CaptureBase();
        }

        /// <summary>
        /// 지금 카메라 값을 «정착값»으로 잡는다.
        /// ⚠ 배선(패스 ③)이 카메라를 레벨 위치로 옮긴 <b>뒤에</b> 불러야 한다 — 안 그러면 원위치로 되돌린다.
        /// </summary>
        public void CaptureBase()
        {
            if (_camera == null)
                return;

            _baseSize = _camera.orthographicSize;
            _basePosition = _camera.transform.position;
        }

        /// <summary>레벨이 올라간 순간. <b>확대된 채 시작해 탄성으로 정착</b>한다.</summary>
        public void PlayLevelStartPunch()
        {
            _punchTimer = 0f;
            _punchPending = false;
        }

        /// <summary>
        /// ★★ <b>골인 «그 프레임»</b> [17회차 실측].
        /// <b>셰이크는 지금 · 줌은 다음 프레임</b> — 원본이 정확히 한 프레임 어긋나 있다.
        /// </summary>
        public void PlayGoalPunch()
        {
            PlayImpactShake();
            _punchPending = true;
        }

        /// <summary>공이 무언가에 부딪힌 순간.</summary>
        public void PlayImpactShake()
        {
            _shakeTimer = 0f;
        }

        private void LateUpdate()
        {
            if (_camera == null)
                return;

            float size = _baseSize;
            Vector3 position = _basePosition;

            // ★ 예약된 줌은 «이 프레임부터» 돈다 — 골 프레임에는 1.00 이어야 하기 때문이다.
            if (_punchPending)
            {
                _punchPending = false;
                _punchTimer = 0f;
            }

            // ── 줌 펀치. 레이아웃 배율 z 는 «겉보기 배율 1/z» 라, 직교 크기는 z 를 그대로 «곱한다».
            //    z = 0.8 이면 보이는 범위가 0.8배 = 1.25배 확대다.
            if (_punchTimer >= 0f)
            {
                // ★★ [정정 · 17회차] «먼저 그리고 나서 시간을 민다».
                //
                //   예전에는 dt 를 «먼저» 더해서 t = 0 을 한 번도 안 그렸다. 그래서 로드 프레임의
                //   dt 가 크면(수십~수백 ms) 첫 그림이 이미 0.89 까지 복귀해 있었고,
                //   «한 프레임 점프»가 프레임 간격에 따라 흐물흐물해졌다 — 검사가 흔들리던 진짜 자리다.
                //   원본은 시작 프레임에 «시작값 그대로»를 그린다 [실측 골 다음 프레임 0.81].
                float t = _punchTimer / PunchSeconds;

                // ⚠ OutElastic 은 1 을 넘나든다 — Lerp 를 클램프하면 진동이 사라진다.
                ZoomValue = Mathf.LerpUnclamped(PunchStartZoom, 1f, BlumgiEase.OutElastic(t));

                _punchTimer += Time.unscaledDeltaTime;

                if (_punchTimer >= PunchSeconds)
                {
                    _punchTimer = -1f;
                    ZoomValue = 1f;
                }

                size = _baseSize * ZoomValue;
            }
            else
            {
                ZoomValue = 1f;
            }

            // ── 착탄 셰이크. «스크롤»만 흔든다 — 배율은 건드리지 않는다 [실측].
            if (_shakeTimer >= 0f)
            {
                _shakeTimer += Time.unscaledDeltaTime;

                if (_shakeTimer >= ShakeSeconds)
                {
                    _shakeTimer = -1f;
                    ShakeOffsetWorld = Vector2.zero;
                }
                else
                {
                    // 매 프레임 «무작위» 오프셋 [실측 — 정현파가 아니다]. 선형 감쇠.
                    float decay = 1f - _shakeTimer / ShakeSeconds;
                    _shakeFrame++;

                    float x = (Hash(_shakeFrame * 2) * 2f - 1f) * ShakeMagnitudeWorld * decay;
                    float y = (Hash(_shakeFrame * 2 + 1) * 2f - 1f) * ShakeMagnitudeWorld * decay;

                    ShakeOffsetWorld = new Vector2(x, y);

                    position += new Vector3(BlumgiUnits.ToUnits(x), BlumgiUnits.ToUnits(y), 0f);
                }
            }
            else
            {
                ShakeOffsetWorld = Vector2.zero;
            }

            _camera.orthographicSize = size;
            _camera.transform.position = position;

            ZoomDeltaThisFrame = ZoomValue - _lastZoomValue;
            _lastZoomValue = ZoomValue;
        }

        /// <summary>
        /// 프레임 번호로 결정되는 의사난수.
        /// ⚠ <b>연출 전용이라 판정에 안 들어간다</b> — 그래도 캡처 대조가 되려면 결정적이어야 해서
        /// <c>UnityEngine.Random</c>(전역 상태)이 아니라 순수 함수를 쓴다 (CLAUDE.md 「무작위는 주입식」의 정신).
        /// </summary>
        private static float Hash(int i)
        {
            float v = Mathf.Sin(i * 12.9898f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }
    }
}
