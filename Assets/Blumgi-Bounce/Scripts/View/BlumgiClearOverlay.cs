using System;
using System.Collections;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 클리어 연출. <b>타이밍이 전부 실측</b>이다 (05_연출 §2 · 릴리즈 t=0 · 140 ms 격자 62장).
    ///
    /// <list type="table">
    /// <item><term>골인 → 연출 시작</term><description>≤ 140 ms (같은 프레임 격자)</description></item>
    /// <item><term>흰 플래시</term><description><b>497.4 ms · 알파 0.700 → 0</b> [23회차 · <c>GetOpacity()</c> rAF 폴링]</description></item>
    /// <item><term><c>YES!</c></term><description>골 <b>«그 프레임»에 생성</b> → <b>레벨 전이(+2 216.9 ms)가 지운다</b> · 상승은 <b>등가속 −300 px/s²</b> [23회차]</description></item>
    /// <item><term>컨페티</term><description>골 <b>+204.2 ms</b> 에 100개 + 캐논 2문 [23회차 5레벨 199.8~208.7] · <b>자연 소멸이 없다</b></description></item>
    /// <item><term><b>레벨 전이</b></term><description>골 <b>+2 216.9 ms</b> 의 «컷» [23회차 5레벨 2 208~2 254 ms]</description></item>
    /// <item><term>전이 플래시</term><description>전이를 덮는 잔광 — <b>알파 0.700 → 0 · 496.4 ms</b> · 레이어가 «골 플래시와 다르다»</description></item>
    /// </list>
    ///
    /// <para>
    /// ★★ <b>[정정 · 23회차] 전환은 «흰색으로 꽉 덮었다가 걷히는 막»이 아니다.</b>
    /// 레이아웃 이름이 <b>+2 216.9 ms 에 «컷»으로 바뀌고</b>, 두 번째 <c>FXflash</c>(알파 0.700 → 0)가
    /// 그 컷을 <b>덮어 주는 잔광</b>이다. 예전 구현은 「흰색이 완전히 덮인 뒤 갈아 끼우기」라
    /// <b>전이가 504 ms 늦고 화면이 원본보다 하얬다.</b>
    /// ⚠ 원본은 <c>FXflash</c> 를 <b>골에는 <c>Tuto</c> 레이어 · 전이에는 <c>UIIngame</c> 레이어</b>에 만든다 [실측] —
    /// 우리는 둘 다 이 창(팝업) 안이라 <b>「골 플래시는 <c>YES!</c>·컨페티 «아래»,
    /// 전이 플래시는 그 «위»」</b>라는 순서로 그 차이를 옮긴다 (프리팹 자식 순서).
    /// </para>
    ///
    /// <para>
    /// ★ <b>[해소 · 8회차] 「컨페티 소멸 시점 미측정」의 답은 «소멸이 없다» 였다</b> —
    /// 가려진 것이 아니라 <b>전환이 곧 소멸</b>이다 (골 +2 216.9 ms 에 100 → 0). 그래서
    /// <see cref="StopAll"/> 이 컨페티를 지우는 지금 구조가 원본과 <b>같은 기전</b>이다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>「클리어 판정 시각」과 「다음 레벨 로드 시각」은 다른 시각</b>이다 —
    /// 사이에 약 2.2 s 의 연출 구간이 들어간다. 콘솔 <c>Level_Started</c> 가 <b>골인이 아니라
    /// 다음 레벨 시작</b>에 찍히는 것이 이것 때문이고, 선행 회차의 「입력 없이 자동 클리어」 오해의 정체다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>[정정 · 17회차] <c>YES!</c> 의 움직임은 «스쿼시 진동 + 가속 상승» 둘뿐이다.</b>
    /// 오버슛·회전·페이드아웃은 <b>관측되지 않았다</b> — 투명도는 1.0 고정이고 화면 위로 빠져나간다.
    /// 색은 <see cref="BlumgiRainbowText"/> 가 돌리는데, <b>도는 것은 «외곽선»</b>이고 채움은 흰색 고정이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <c>YES!</c> 는 원본에서 <b>스프라이트다</b> [17회차 실측 — 클래스 이름이 <c>Sprite</c> 라
    /// 이름으로 못 찾았을 뿐이고 소스 <b>266 × 116</b> · 표시 <b>459 × 200</b> 이다] —
    /// 우리는 <b>색이 매 프레임 바뀌므로 텍스트로 그린다</b>. 서체 자체가 대체(의도된 차이 #5)라
    /// 스프라이트로 굽든 텍스트로 그리든 자형은 원본과 다르다. <b>PD 등재 요청 항목이다.</b>
    /// </para>
    /// </summary>
    public sealed class BlumgiClearOverlay : BaseWindow
    {
        /// <summary>창 식별자. 경로는 <c>Resources</c> 기준이다.</summary>
        public static readonly WindowKey<BlumgiClearOverlay> Key =
            new WindowKey<BlumgiClearOverlay>("UI/Window/BlumgiClearOverlay");

        // ── 실측 타이밍 (초 · 골인 = t 0).
        //    ★ 23회차가 «5레벨 각 1골»을 rAF 로 채록(4,814프레임)해 아래 값을 전부 다시 확정했다.
        //      L1~L5 가 «같다» — 갈리는 것은 림 반짝임(FX·FXChling)뿐이고 그것은 «그 샷의 접촉 이력»이다.
        //    ⚠ W1L5 가 달라 보이는 것은 «레벨 연출»이 아니라 전이 «후» Map 화면 것이다 [실측 · 05 §2-1-b].
        [Header("타이밍 [실측 · 8회차 정밀화 → 17회차 rAF 확정 → 23회차 5레벨 확정 · 골인 = t 0]")]

        /// <summary>
        /// 골 플래시 수명 — <b>497.4 ms</b> [정정 · 23회차 · 5레벨 499.3~509.1 ms].
        /// </summary>
        [SerializeField] private float _flashSeconds = 0.4974f;

        /// <summary>
        /// ★★ <b>골 플래시의 시작 알파 — <c>0.700</c></b> [해소 · 23회차 · <c>GetOpacity()</c> rAF 폴링 24,798행].
        ///
        /// <para>
        /// 예전에는 <b>1.0 에서 시작</b>했다 — 「알파를 읽을 경로를 못 찾았다」(`05 §6`)라서 개형만 옮긴 것이다.
        /// 실측은 <b>0.700 → 0.000 단조 감쇠</b> (56 ms 0.675 · 172 ms 0.506 · 289 ms 0.266 · 406 ms 0.063).
        /// ⇒ <b>1.0 으로 시작하면 화면이 원본보다 «43 % 더 하얘진다».</b>
        /// </para>
        /// </summary>
        [SerializeField] private float _flashStartAlpha = 0.700f;

        /// <summary>
        /// <c>YES!</c> 등장 지연 — <b>0</b> [정정 · 17회차].
        /// 생성·소멸 타임라인이 <b>골 «그 프레임»에 <c>Sprite</c>(= <c>YES!</c>) 0 → 1</b> 이었다 [실측].
        /// 8회차의 0.07 s 는 140 ms 격자에서 온 근사다.
        /// </summary>
        [SerializeField] private float _yesDelaySeconds;

        /// <summary>
        /// <c>YES!</c> 가 떠 있는 시간 — <b>2.2169 s</b> [정정 · 23회차 · 5레벨 2 208.0~2 254.2 ms].
        /// <b>수명으로 사라지지 않는다</b> — <b>레벨 전이가 지운다</b> [실측] ⇒ 전이 시각과 «같은 값»이다.
        /// 1회차 픽셀의 1.55 s 는 «화면 위로 빠져나가 안 보이게 된 시각»을 수명으로 읽은 것이다.
        /// </summary>
        [SerializeField] private float _yesSeconds = 2.2169f;

        /// <summary>
        /// 컨페티 생성 지연 — <b>+204.2 ms</b> [정정 · 23회차 · 5레벨 199.8~208.7 ms 중앙].
        /// 17회차의 214.2 ms 는 <b>n=1</b> 이었다.
        /// </summary>
        [SerializeField] private float _confettiDelaySeconds = 0.2042f;

        /// <summary>
        /// ★★ <b>2번째 <c>FXwinLight</c> 지연 — 컨페티와 «같은 이벤트 행»이다</b>
        /// [실측 · 23회차 5레벨 채록에서 <c>FXconfettis 0→100</c> · <c>FXConfettisCones 0→2</c> ·
        /// <c>FXwinLight 1→2</c> 가 <b>항상 같은 프레임</b>에 찍혔다 — 199.8 ~ 208.7 ms].
        ///
        /// <para>
        /// ⚠ 값이 같다고 <see cref="_confettiDelaySeconds"/> 를 «돌려 쓰지» 않는다 —
        /// 둘이 같은 것은 <b>원본 시트의 한 줄이라는 «사실»</b>이지 우리 구현의 결합이 아니고,
        /// 재측정에서 갈리면 각각 고쳐야 한다.
        /// </para>
        /// </summary>
        [SerializeField] private float _secondWinLightDelaySeconds = 0.2042f;

        /// <summary>
        /// ★★ <b>레벨 전이 시각 — 골 +2 216.9 ms</b> [실측 · 23회차 · 5레벨 2 208.0~2 254.2 ms].
        ///
        /// <para>
        /// ⚠ <b>[정정 · 23회차] 예전에는 «흰색이 화면을 완전히 덮은 뒤»(2.213 + 0.508 = 2.721 s)에 갈아 끼웠다.</b>
        /// 실측은 <b>레이아웃 이름이 +2 216.9 ms 에 바뀌고</b>, 그 «컷»을 <b>두 번째 <c>FXflash</c> 가 덮어 준다</b> —
        /// 그 플래시도 <b>0.700 → 0.001 로 «옅어지는» 잔광</b>이지 «흰색으로 꽉 덮는 막»이 아니다.
        /// ⇒ 예전 구조는 <b>전이가 504 ms 늦고 화면이 원본보다 하얘졌다.</b>
        /// </para>
        /// </summary>
        [SerializeField] private float _transitionDelaySeconds = 2.2169f;

        /// <summary>전이 플래시 수명 [실측 · 23회차 uid 1001449 · +2 216.6 → +2 713.0 ms].</summary>
        [SerializeField] private float _transitionFlashSeconds = 0.4964f;

        /// <summary>
        /// 전이 플래시의 시작 알파 — <b>0.700</b> [실측 · 골 플래시와 <b>같은 개형</b>].
        /// ⚠ <b>「덮는 막」이 아니다</b> — 1.0 으로 채우면 원본에 없는 «흰 화면»이 생긴다.
        /// </summary>
        [SerializeField] private float _transitionFlashStartAlpha = 0.700f;

        // ── ★ `YES!` 의 «스쿼시»와 «상승»
        //    ★★ [해소 · 23회차] 「상승 곡선의 정밀 모델 미측정」이 닫혔다 — 스쿼시가 섞인 중심 y 를
        //       높이 보정항 (h − 200) 으로 «디트렌드»하니 5레벨이 전부 등가속 −300 px/s² 에 붙었다
        //       (잔차 max 1.6~3.2 px · 등속 모형은 잔차 120 px 로 배제). 1,323점 회귀다.

        [Header("YES! 스쿼시 [실측 w 409↔509 · h 250↔150 · w+h = 659 고정]")]

        /// <summary>스쿼시 기준 폭 (원본 world px) [실측 생성 시 459 × 200 = 정확히 중간].</summary>
        [SerializeField] private float _yesBaseWidthWorld = 459f;

        [SerializeField] private float _yesBaseHeightWorld = 200f;

        /// <summary>폭 진폭 [실측 459 ± 50]. 높이는 <c>659 − 폭</c> 이라 따로 두지 않는다 (둘레 보존).</summary>
        [SerializeField] private float _yesSquashAmplitudeWorld = 50f;

        /// <summary>스쿼시 주기 [실측 399.6 ms · 전주기 표본 9].</summary>
        [SerializeField] private float _yesSquashSeconds = 0.3996f;

        [Header("YES! 상승 — ★ 등가속 [해소 · 23회차 · 5레벨 1,323점 · 잔차 ≤ 3.2 px]")]

        /// <summary>
        /// ★★ <b>위로 가속 <c>300.0 px/s²</c></b> (원본 y-down 으로는 <c>a = −300.0</c>) [실측 · 23회차].
        ///
        /// <para>
        /// 5레벨 회귀가 <b>−300.12 / −299.91 / −299.63 / −299.98 / −300.01</b> 로 <b>5/5 가 −300 에 붙는다</b> ⇒
        /// 원본 수는 <b>−300 px/s²</b> 다 (<c>재발방지 #105</c> — 관측 소수를 박지 않고 «붙는 정수»를 쓴다).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>등속(선형) 모형은 잔차 120 px 로 «배제»됐다.</b> 예전 구현은 등가속이긴 했으나
        /// <b>가속을 «두 끝점 + 초속 170»에서 풀어</b> ≈ 127 px/s² 를 쓰고 있었다 — <b>실측의 2.4 분의 1</b>이다.
        /// (그 끝점 근사 자체가 17회차 n=1 값이었다.)
        /// </para>
        /// </summary>
        [SerializeField] private float _yesRiseAccelWorld = 300f;

        /// <summary>
        /// 초기 «상승» 속도 (원본 world px/s · 위가 +) — <b>−5.4</b> [실측 5레벨 5.17~5.55 px/s «아래»로].
        /// <b>사실상 정지에서 출발한다</b>.
        /// </summary>
        [SerializeField] private float _yesRiseStartSpeedWorld = -5.4f;

        /// <summary>
        /// ★ <c>YES!</c> 스프라이트의 <b>원점 y</b> — <b>0.7071</b> [실측 · 23회차 유도].
        ///
        /// <para>
        /// 스쿼시로 높이가 변하면 <b>bbox 중심이 원점을 기준으로 움직인다</b>:
        /// <c>bbox중심 = 원점 + (0.5 − orgY)·h</c>. 5레벨 회귀의 보정계수가
        /// <b><c>k = −0.2071</c> 로 5/5 동일</b>이라 <c>orgY = 0.5 + 0.2071 = 0.7071</c> 이다.
        /// </para>
        ///
        /// <para>
        /// ★ 우리 <c>RectTransform</c> 은 피벗이 <c>(0.5, 0.5)</c> 라 <b>중심이 곧 bbox 중심</b>이다 ⇒
        /// 원점의 등가속 운동에 <b>이 오프셋을 «더해» 준다</b>. 계수를 상수로 박지 않고 원점에서 «유도»하는 이유는
        /// <c>재발방지 #53</c> — <b>기전을 옮기고 파생 규칙은 검산으로만 쓴다</b>.
        /// </para>
        ///
        /// <para>
        /// 검산 — 생성 시 bbox 중심 y 가 598.12 이고 <c>h = 200</c> 이므로
        /// 원점 y = 598.12 + 0.2071 × 200 = <b>639.5</b> = <c>layout._scrollY</c> (화면 정중앙)다. ✔
        /// </para>
        /// </summary>
        [SerializeField] private float _yesSpriteOriginY = 0.7071f;

        /// <summary>
        /// world px → UI px. 캔버스 기준 1080 / 원본 논리 1280 = <b>0.84375</b>
        /// (<c>BlumgiPrefabBuilder.UiScale</c> 과 같은 값이다).
        /// </summary>
        [SerializeField] private float _uiPixelsPerWorldPixel = 0.84375f;

        /// <summary>
        /// <c>YES!</c> 를 켜고 끄는 «그 오브젝트».
        ///
        /// <para>
        /// ★★ <b>[결함 · ②-d 에서 잡음] 예전에는 <c>_yesText.gameObject</c> 를 켰다 — 그런데
        /// 그것은 «Yes» 뿌리의 «자식»이고, 굽는 도구가 <b>뿌리를 꺼 둔 채</b> 저장한다.
        /// 자식만 켜면 <b>화면에 아무것도 안 나온다</b> — <c>YES!</c> 가 패스 ② 이후 «한 번도 안 떴다».</b>
        /// 수치 검사는 전부 통과했다 (타이밍도 색 순환도 «돌고» 있었다) — <b>보이지 않았을 뿐이다.</b>
        /// 그래서 켜고 끄는 대상은 <b>포즈·알파를 든 뿌리</b>여야 한다.
        /// </para>
        /// </summary>
        private GameObject YesRoot
        {
            get
            {
                if (_yesRect != null)
                    return _yesRect.gameObject;

                return _yesText == null ? null : _yesText.gameObject;
            }
        }

        [Header("요소")]

        /// <summary>
        /// <b>골 플래시</b> (원본 <c>FXflash</c> #1 · 레이어 <c>Tuto</c>) — 프리팹에서 <b>맨 아래 자식</b>이다.
        /// </summary>
        [SerializeField] private Image _flash;

        /// <summary>
        /// <b>전이 플래시</b> (원본 <c>FXflash</c> #2 · 레이어 <c>UIIngame</c>) — 프리팹에서 <b>맨 위 자식</b>이다.
        /// ⚠ 이름이 <c>_fade</c> 인 것은 <b>「흰색 페이드 막」으로 읽던 시절의 흔적</b>이다 —
        /// 23회차 실측 뒤로 이것은 <b>«덮개»가 아니라 «잔광»</b> 이다 (알파 0.700 → 0).
        /// </summary>
        [SerializeField] private Image _fade;
        [SerializeField] private TextMeshProUGUI _yesText;
        [SerializeField] private RectTransform _yesRect;

        /// <summary>
        /// ⚠ 알파를 <c>TextMeshProUGUI.color</c> 로 다루면 <see cref="BlumgiRainbowText"/> 가
        /// <b>매 프레임 색을 덮어써</b> 페이드가 사라진다. 그래서 알파는 <c>CanvasGroup</c> 이 든다.
        /// </summary>
        [SerializeField] private CanvasGroup _yesGroup;
        [SerializeField] private BlumgiConfettiBurst _confetti;

        /// <summary>
        /// ★ <b>골인 빛줄기 2개 · 텔레포트</b> (원본 <c>FXwinLight</c> ×2 · <c>FXteleport</c>) —
        /// <b>패스 ②-h 에서 «새로» 만든 것</b>이다. 컨페티와 같은 이유로 <b>월드 오브젝트</b>라 배선이 넣는다.
        /// </summary>
        [SerializeField] private BlumgiGoalBurst _goalBurst;

        /// <summary>흰색 페이드가 화면을 덮은 순간. <b>여기서 다음 레벨을 올린다</b> — 그래야 «비쳐 드는» 원본과 같다.</summary>
        public event Action FadeCovered;

        /// <summary>연출이 완전히 끝났다.</summary>
        public event Action Finished;

        private Coroutine _routine;

        /// <summary>
        /// 굽는 도구가 잡아 둔 <c>YES!</c> 의 «제자리».
        ///
        /// <para>
        /// ★★ <b>[결함 · ②-d 에서 잡음] 예전 <see cref="ApplyYesPose"/> 는 <c>anchoredPosition</c> 을
        /// «덮어썼다» — 등장~유지 구간의 오프셋이 <c>(0,0)</c> 이라, 뜨는 순간 <b>화면 위쪽 끝으로 튀어
        /// 반쯤 잘린 채</b> 그려졌다. 이탈 오프셋은 <b>제자리에 «더하는» 값</b>이지 절대 좌표가 아니다.
        /// </para>
        /// </summary>
        private Vector2 _yesBasePosition;

        private bool _yesBaseCaptured;

        /// <summary>
        /// 골인 순간에 부른다.
        /// ⚠ <b>[정정 · 8회차] 컨페티 자리를 인자로 받지 않는다</b> — 「골대 부근에서 위로 솟구친다」는
        /// 1회차 오독이었고, 실체는 <b>화면 밖 좌우 바닥의 고정 캐논 2문</b>이다
        /// (<see cref="BlumgiConfettiBurst"/> 주석).
        /// </summary>
        public void Play()
        {
            CaptureYesBase();

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(Run());
        }

        /// <summary>
        /// 컨페티는 <b>월드 오브젝트</b>다 — Resources 프리팹이 씬 오브젝트를 미리 물 수 없으므로
        /// 배선(패스 ③)이 넣는다.
        /// </summary>
        public void SetConfetti(BlumgiConfettiBurst confetti)
        {
            _confetti = confetti;
        }

        /// <summary>
        /// 골인 빛줄기·텔레포트도 <b>월드 오브젝트</b>다 — 컨페티와 같은 이유로 배선(패스 ③)이 넣는다.
        /// ⚠ 자리(골대 좌표)는 <b>배선이 <see cref="BlumgiGoalBurst.SetAnchorWorld"/> 로 미리 세워 둔다</b> —
        /// 이 창은 «언제»만 알고 «어디»는 모른다.
        /// </summary>
        public void SetGoalBurst(BlumgiGoalBurst goalBurst)
        {
            _goalBurst = goalBurst;
        }

        public void StopAll()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            SetAlpha(_flash, 0f);
            SetAlpha(_fade, 0f);

            SetYesShown(false);

            if (_confetti != null)
                _confetti.Stop();

            if (_goalBurst != null)
                _goalBurst.Stop();
        }

        private IEnumerator Run()
        {
            StopAllVisualsOnly();

            float t = 0f;
            bool confettiStarted = false;
            bool secondWinLightStarted = false;
            bool yesStarted = false;
            bool transitioned = false;

            // ── ★ 골 «그 프레임» — 원본 이벤트 한 줄이 넷을 같이 켠다 [실측 · 전부 0 ms]:
            //    FXflash 0→1 · FXteleport 0→1 · FXwinLight 0→1 · Sprite(YES!) 0→1.
            //    (플래시와 YES! 는 이 루프가 아래에서 켠다 — 여기서는 «새로 만든» 둘만 켠다.)
            if (_goalBurst != null)
                _goalBurst.Play();

            float yesStart = _yesDelaySeconds;
            float yesEnd = yesStart + _yesSeconds;
            float transitionStart = _transitionDelaySeconds;
            float total = transitionStart + _transitionFlashSeconds;

            while (t < total)
            {
                t += Time.unscaledDeltaTime;

                // ── 골 플래시 — 등장 즉시 «0.700», 497 ms 에 걸쳐 0 으로 [실측 23회차 GetOpacity()].
                SetAlpha(_flash, _flashSeconds <= 0f
                                     ? 0f
                                     : _flashStartAlpha * Mathf.Clamp01(1f - t / _flashSeconds));

                // ── 컨페티
                if (confettiStarted == false && t >= _confettiDelaySeconds)
                {
                    confettiStarted = true;

                    if (_confetti != null)
                        _confetti.Play();
                }

                // ── ★ 2번째 빛줄기 — 원본에서 «컨페티와 같은 이벤트 행»이다 [실측].
                if (secondWinLightStarted == false && t >= _secondWinLightDelaySeconds)
                {
                    secondWinLightStarted = true;

                    if (_goalBurst != null)
                        _goalBurst.PlaySecondWinLight();
                }

                // ── YES!
                if (yesStarted == false && t >= yesStart)
                {
                    yesStarted = true;
                    SetYesShown(true);
                }

                if (yesStarted && _yesRect != null)
                {
                    float u = Mathf.Clamp01((t - yesStart) / Mathf.Max(_yesSeconds, 0.0001f));
                    ApplyYesPose(u);
                }

                if (t >= yesEnd)
                    SetYesShown(false);

                // ── ★★ 레벨 전이 — «컷»이고, 두 번째 플래시가 그 컷을 «덮어 준다» [정정 · 23회차].
                //    예전에는 흰색이 «완전히 덮인 뒤»에 갈아 끼웠다 (504 ms 늦고 화면이 더 하얬다).
                if (transitioned == false && t >= transitionStart)
                {
                    transitioned = true;
                    FadeCovered?.Invoke();
                }

                float since = t - transitionStart;

                float transitionFlash = transitioned == false || _transitionFlashSeconds <= 0f
                    ? 0f
                    : _transitionFlashStartAlpha
                      * Mathf.Clamp01(1f - since / _transitionFlashSeconds);

                SetAlpha(_fade, transitionFlash);

                yield return null;
            }

            if (transitioned == false)
                FadeCovered?.Invoke();

            SetAlpha(_fade, 0f);

            _routine = null;
            Finished?.Invoke();
        }

        /// <summary>
        /// ★★★ <b>[전면 교체 · 17회차] <c>YES!</c> 의 포즈는 «등장 오버슛 → 유지 → 이탈 + 페이드»가 아니다.</b>
        ///
        /// <para>
        /// rAF 260 프레임 채록이 낸 실체는 <b>① 둘레 보존 스쿼시 진동 + ② 가속 상승</b> 둘뿐이고,
        /// <b>투명도는 1.0 고정 — 페이드아웃이 «없다»</b>. 화면 위로 빠져나간 뒤 <b>레벨 전이가 지운다</b>.
        /// 회전도 관측되지 않았다.
        /// </para>
        ///
        /// <list type="bullet">
        /// <item>스쿼시 — <c>w 409 ↔ 509</c> · <c>h 250 ↔ 150</c> · <b><c>w + h = 659</c> 로 항상 일정</b> ·
        ///       주기 <b>399.6 ms</b> · 생성 시 <c>459 × 200</c> = 정확히 중간(= 위상 0)</item>
        /// <item>상승 — ★★ <b>등가속 <c>−300.0 px/s²</c></b> · <c>v₀ ≈ +5.4 px/s (아래)</c> = <b>정지에서 출발</b>
        ///       [해소 · 23회차 · 5레벨 1,323점 · 잔차 max 1.6~3.2 px]</item>
        /// <item>스쿼시가 중심 y 를 흔드는 것은 <b>원점이 <c>0.7071</c> 이기 때문</b>이다 —
        ///       «보정 상수»가 아니라 <b>기전</b>이라 원점에서 유도해 쓴다 (<c>재발방지 #53</c>)</item>
        /// </list>
        ///
        /// <para>
        /// ★★ <b>[해소 · 23회차] 「상승의 정밀 모델은 미측정」이 닫혔다.</b> 스쿼시가 섞인 중심 y 를
        /// 높이 보정항 <c>(h − 200)</c> 으로 디트렌드하니 <b>5레벨 전부 −300 px/s² 에 붙었다</b>.
        /// <b>등속 모형은 잔차 120 px 로 «배제»</b>됐다.
        /// </para>
        /// </summary>
        private void ApplyYesPose(float u)
        {
            float t = u * Mathf.Max(_yesSeconds, 0.0001f);

            // ── ① 둘레 보존 스쿼시. w 가 커지는 만큼 h 가 작아진다 (w + h 고정).
            float wave = _yesSquashSeconds <= 0f
                ? 0f
                : Mathf.Sin(t / _yesSquashSeconds * Mathf.PI * 2f);

            float widthWorld = _yesBaseWidthWorld + _yesSquashAmplitudeWorld * wave;
            float heightWorld = _yesBaseHeightWorld - _yesSquashAmplitudeWorld * wave;

            _yesRect.localScale = new Vector3(widthWorld / _yesBaseWidthWorld,
                                              heightWorld / _yesBaseHeightWorld,
                                              1f);

            // ── ② ★ 등가속 상승 [실측 −300.0 px/s²]. «원점»의 운동이다.
            float rise = RiseWorldAt(t);

            // ── ③ ★ 스쿼시가 만드는 «중심 오프셋». 원점 0.7071 짜리 오브젝트가 높이 h 로 눌리면
            //       원본(y 아래가 +)에서 bbox 중심이 원점 대비 (0.5 − orgY)·h 만큼 어긋난다.
            //       우리 피벗은 (0.5, 0.5) 라 «중심 = bbox 중심» 이고, 축이 «위가 +» 라 부호를 뒤집는다
            //       ⇒ (orgY − 0.5)·(h − 기준h). 상수 0.2071 을 박지 않고 원점에서 «유도»한다 (재발방지 #53).
            float centerOffsetWorld = (_yesSpriteOriginY - 0.5f) * (heightWorld - _yesBaseHeightWorld);

            _yesRect.anchoredPosition = _yesBasePosition
                                        + new Vector2(0f, (rise + centerOffsetWorld) * _uiPixelsPerWorldPixel);

            // ★ 회전·페이드는 «관측되지 않았다» — 넣지 않는다.
            _yesRect.localRotation = Quaternion.identity;

            if (_yesGroup != null)
                _yesGroup.alpha = 1f;
        }

        /// <summary>
        /// t 초에 <c>YES!</c> «원점»이 위로 올라간 거리 (원본 world px). <b>검사가 이것을 읽는다</b>.
        ///
        /// <para>
        /// ★ <b>[전면 교체 · 23회차] 등가속 그대로다</b> — <c>v₀·t + ½·a·t²</c> 이고
        /// <c>a = +300 px/s² (위로)</c> · <c>v₀ = −5.4 px/s</c> 는 <b>둘 다 실측</b>이다.
        /// 예전처럼 <b>「잰 끝점에서 a 를 푸는」 것이 아니다</b> — 그 방식은 17회차 n=1 끝점에 매달려 있었고
        /// 실제 가속의 <b>2.4 분의 1</b>(≈127 px/s²)을 냈다.
        /// </para>
        /// </summary>
        public float RiseWorldAt(float seconds)
        {
            return _yesRiseStartSpeedWorld * seconds
                   + 0.5f * _yesRiseAccelWorld * seconds * seconds;
        }

        private void StopAllVisualsOnly()
        {
            SetAlpha(_flash, _flashStartAlpha);
            SetAlpha(_fade, 0f);

            SetYesShown(false);
        }

        /// <summary>제자리를 «포즈가 한 번이라도 돌기 전»에 잡는다 — 그 뒤에 잡으면 이미 덮인 값이다.</summary>
        private void CaptureYesBase()
        {
            if (_yesBaseCaptured || _yesRect == null)
                return;

            _yesBasePosition = _yesRect.anchoredPosition;
            _yesBaseCaptured = true;
        }

        private void SetYesShown(bool shown)
        {
            GameObject root = YesRoot;

            if (root != null && root.activeSelf != shown)
                root.SetActive(shown);
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null)
                return;

            Color c = image.color;
            c.a = alpha;
            image.color = c;
            image.enabled = alpha > 0.001f;
        }
    }
}
