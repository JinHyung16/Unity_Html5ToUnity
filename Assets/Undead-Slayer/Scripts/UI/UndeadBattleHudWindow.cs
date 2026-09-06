using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 전투 HUD. <b>원본 실측 배치 그대로</b>다 (`04_UIUX규칙.md` · 세로 580 기준).
    ///
    /// <para>
    /// ★ 원본에서 «히어로가 움직여도 화면 좌표가 안 변하는» 노드를 골라 잰 것이다 —
    /// 좌상단 보석 아이콘 · 상단 게이지 바 · 게이지 문구 · 타이머 · 우상단 레벨 배경 · 레벨 숫자,
    /// 그리고 <b>좌하단 «과제 포인터»</b>(화살표 + 거리 <c>NNm</c>).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>정정</b> — 회차 1~6 에는 「<c>NNm</c> 은 월드를 따라 움직이므로 HUD 가 아니다」로 적혀 있었다.
    /// <b>틀렸다.</b> 부모가 화면좌표 <b>(58, 519)</b> 에 «고정»이고, 좌표가 조금씩 움직여 보인 것은
    /// 내가 «bbox 중심»을 읽었기 때문이다 — 자릿수가 늘면(<c>89m</c> → <c>104m</c>) 중심이 오른쪽으로 밀린다.
    /// <b>「좌표가 움직인다」를 「월드다」로 읽지 않는다</b> [회차 7 실측 — 부모 사슬 직독].
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>Window 에는 게임 로직이 없다.</b> 값을 받아 표기만 한다 — 위로 올릴 일은 <c>event</c> 로만 올린다.
    /// </para>
    /// </summary>
    public sealed class UndeadBattleHudWindow : BaseWindow
    {
        public static readonly WindowKey<UndeadBattleHudWindow> Key =
            new WindowKey<UndeadBattleHudWindow>("UI/Window/UndeadBattleHudWindow");

        [Header("Gauge")]
        [SerializeField] private Image _gaugeFill;
        [SerializeField] private TMP_Text _gaugeText;

        [Header("Timer")]
        [SerializeField] private TMP_Text _timerText;

        [Header("Level")]
        [SerializeField] private TMP_Text _levelText;

        [Header("Quest")]
        [SerializeField] private RectTransform _questArrow;
        [SerializeField] private TMP_Text _questDistanceText;

        /// <summary>
        /// 게이지. <b>목표를 0 으로 주지 않는다</b> — 관측 범위를 넘으면 데이터가 없다는 뜻이라
        /// 그 자리는 배선이 판정한다.
        /// </summary>
        public void SetGauge(int value, int goal)
        {
            if (_gaugeText != null)
                _gaugeText.text = $"{value}/{goal}";

            if (_gaugeFill != null)
                _gaugeFill.fillAmount = goal > 0 ? Mathf.Clamp01((float)value / goal) : 0f;
        }

        /// <summary>타이머. 원본 표기는 <c>mm:ss</c> 다 [실측 — <c>00:02</c>].</summary>
        public void SetElapsed(double seconds)
        {
            if (_timerText == null)
                return;

            int total = Mathf.Max(0, Mathf.FloorToInt((float)seconds));
            _timerText.text = $"{total / 60:00}:{total % 60:00}";
        }

        public void SetLevel(int level)
        {
            if (_levelText != null)
                _levelText.text = level.ToString();
        }

        /// <summary>
        /// 과제 포인터 [소스 <c>bd.update</c> · 회차 12 덤프로 정정 — 자리는 고정이 아니다] — 대상이 화면 «밖»일 때만 보이고, 화면 가장자리(가로 −58 · 세로 −48 안쪽)를
        /// 따라 대상 방향에 놓인다. 자리·회전은 <c>1 − .0025^(dt/16.667)</c> 로 따라가고, 화살표는 자기 축으로 <c>7·sin(.007t)</c>
        /// 떠다니며, 거리 글자는 화살표 중심에서 회전축 기준 −10 에 «똑바로» 선다. 거리는 1000m 부터 km 다 [소스 <c>fd</c>].
        /// <para>⚠ 각도는 원본 좌표계(y 아래)로 받아 <b>y 뒤집기 한 번</b>만 한다.</para>
        /// </summary>
        /// <param name="allowed">퀘스트가 켜져 있고 레벨업 카드가 안 떠 있다</param>
        /// <param name="dx">카메라 중심 → 대상 (원본 픽셀 · y 아래)</param>
        /// <param name="dy">카메라 중심 → 대상 (원본 픽셀 · y 아래)</param>
        public void SetQuest(bool allowed, float dx, float dy, int meters, float dt)
        {
            if (_questArrow == null)
                return;

            RectTransform root = (RectTransform)transform;
            float s = root.rect.height / DesignHeight;            // 캔버스 단위 / 원본 픽셀
            float halfW = root.rect.width / s * 0.5f;             // 원본 픽셀 기준 반폭
            float halfH = DesignHeight * 0.5f;
            bool onScreen = Mathf.Abs(dx) <= halfW && Mathf.Abs(dy) <= halfH;
            bool visible = allowed && onScreen == false;

            _questArrow.gameObject.SetActive(visible);

            if (_questDistanceText != null)
                _questDistanceText.gameObject.SetActive(visible);

            if (visible == false)
            {
                _pointerX = 0f;   // 안 보일 때는 화면 가운데로 되돌아간다 [소스 onResize]
                _pointerY = 0f;
                return;
            }

            float p = halfW - EdgeMarginX;
            float m = halfH - EdgeMarginY;
            float f = 1f / Mathf.Max(Mathf.Abs(dx) / p, Mathf.Abs(dy) / m);
            float goalX = dx * f;                                  // 화면 가운데 기준 (원본 픽셀 · y 아래)
            float goalY = dy * f;
            float ease = 1f - Mathf.Pow(0.0025f, dt * 1000f / 16.667f);

            _pointerX += (goalX - _pointerX) * ease;
            _pointerY += (goalY - _pointerY) * ease;

            float target = Mathf.Atan2(dy, dx);
            float delta = Mathf.Atan2(Mathf.Sin(target - _pointerRotation), Mathf.Cos(target - _pointerRotation));
            _pointerRotation += delta * ease;

            _floatElapsedMs += dt * 1000f;
            float floatX = FloatAmplitude * Mathf.Sin(FloatSpeed * _floatElapsedMs);
            float cos = Mathf.Cos(_pointerRotation);
            float sin = Mathf.Sin(_pointerRotation);

            // 회전축(원본 y 아래)으로 floatX 만큼 밀린 화살표 · −10 에 선 글자 → 유니티 캔버스(y 위)로
            _questArrow.anchoredPosition = new Vector2((_pointerX + cos * floatX) * s, -(_pointerY + sin * floatX) * s);
            _questArrow.localRotation = Quaternion.Euler(0f, 0f, -_pointerRotation * Mathf.Rad2Deg);

            if (_questDistanceText != null)
            {
                float tx = _pointerX + cos * (floatX + TextOffsetX);
                float ty = _pointerY + sin * (floatX + TextOffsetX);
                _questDistanceText.rectTransform.anchoredPosition = new Vector2(tx * s, -ty * s);
                _questDistanceText.text = FormatDistance(meters);
            }
        }

        /// <summary>[소스 <c>fd</c>] — 1000 미만은 <c>Nm</c> · 10km 미만은 소수 한 자리 · 그 위는 정수 km.</summary>
        private static string FormatDistance(int meters)
        {
            int m = Mathf.Max(1, meters);

            if (m < 1000)
                return $"{m}m";

            float km = m / 1000f;
            return km < 10f ? $"{km:F1}km" : $"{Mathf.RoundToInt(km)}km";
        }

        private const float DesignHeight = 580f;
        private const float EdgeMarginX = 58f;     // [소스 p = h − 58]
        private const float EdgeMarginY = 48f;     // [소스 m = l − 48]
        private const float FloatAmplitude = 7f;   // [소스 visual.x = 7·sin(.007·t)]
        private const float FloatSpeed = 0.007f;
        private const float TextOffsetX = -10f;    // [소스 distanceText.x = −10]

        private float _pointerX;
        private float _pointerY;
        private float _pointerRotation;
        private float _floatElapsedMs;
    }
}
