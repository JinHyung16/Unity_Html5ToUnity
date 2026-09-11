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
        /// 최고 기록 [소스 <c>guiBestScore</c>] — <b>대기 화면 전용이 아니라 «항상» 떠 있다</b>.
        /// <para>좌하단이 최고 레벨 · 우하단이 최고 시간. 여백 8 · 글자 14 · 검정 + 흰 테두리 3 · 굵게.</para>
        /// </summary>
        [SerializeField] private TMP_Text _bestLevelText;

        [SerializeField] private TMP_Text _bestTimeText;

        /// <summary>
        /// 스킬 슬롯 넷 [소스 <c>skillHud</c>] — <b>가진 것만 보이고</b>, 자리는 <b>오른쪽으로 정렬</b>된다.
        /// <para>⚠ 슬롯 순서는 <see cref="EUndeadSkill"/> 이고, 안 가진 것은 «칸까지» 빠진다.</para>
        /// </summary>
        [SerializeField] private UndeadSkillSlot[] _skillSlots;

        /// <summary>슬롯이 붙는 자리 — 가진 개수에 따라 폭이 변해 오른쪽 정렬을 다시 잡는다.</summary>
        [SerializeField] private RectTransform _skillRoot;

        /// <summary>
        /// 게이지. <b>목표를 0 으로 주지 않는다</b> — 관측 범위를 넘으면 데이터가 없다는 뜻이라
        /// 그 자리는 배선이 판정한다.
        /// </summary>
        /// <summary>
        /// 스킬 슬롯을 매 프레임 갱신한다 [소스 <c>skillHud.update</c>].
        ///
        /// <para>
        /// ★ <b>가진 것만</b> 보이고 <b>왼쪽부터 붙여</b> 놓는다 — 슬롯 사이 62, 오른쪽 여백 12
        /// [소스 <c>x = 62·i</c> · <c>container.x = 화면오른쪽 − 12 − 전체폭</c>].
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>[사고 · #174]</b> 이 창은 스킬 시스템이 통째로 미이관인 채로 여러 회차를 통과했다.
        /// 「슬롯이 있나」가 아니라 <b>「가진 스킬 수만큼 보이나」</b>를 검사가 세야 한다.
        /// </para>
        /// </summary>
        public void SetSkills(UndeadSimulation simulation)
        {
            if (_skillSlots == null || simulation == null)
                return;

            int shown = 0;

            for (int i = 0; i < _skillSlots.Length; i++)
            {
                if (_skillSlots[i] == null)
                    continue;

                EUndeadSkill skill = _skillSlots[i].Skill;
                bool owned = simulation.OwnsSkill(skill);

                _skillSlots[i].Apply(owned, simulation.SkillCooldownRemaining(skill), simulation.SkillReady(skill));

                if (owned == false)
                    continue;

                // 가진 것끼리 «빈칸 없이» 붙는다 — 안 가진 슬롯은 자리를 안 차지한다
                var rect = _skillSlots[i].transform as RectTransform;

                if (rect != null)
                    rect.anchoredPosition = new Vector2(SkillSlotStride * shown * CanvasScale, 0f);

                shown++;
            }

            if (_skillRoot != null)
            {
                float width = shown > 0 ? SkillSlotSize * shown + SkillSlotGap * (shown - 1) : 0f;
                _skillRoot.sizeDelta = new Vector2(width * CanvasScale, SkillSlotSize * CanvasScale);
            }
        }

        /// <summary>슬롯 한 변 [소스 <c>zd = 54</c>].</summary>
        private const float SkillSlotSize = 54f;

        /// <summary>슬롯 사이 간격 [소스 8].</summary>
        private const float SkillSlotGap = 8f;

        /// <summary>슬롯이 놓이는 간격 [소스 <c>x = 62·i</c>].</summary>
        private const float SkillSlotStride = 62f;

        public void SetGauge(int value, int goal)
        {
            if (_gaugeText != null)
                _gaugeText.text = $"{value}/{goal}";

            if (_gaugeFill == null)
                return;

            // ★★ 채움은 «폭이 자란다» [소스 — progressBarFill.width = 남은 폭 × 진행률].
            //   ⚠ 그림을 «자르는» 방식(Filled)이 아니다 — 그러면 오른쪽 둥근 마감이 잘린다.
            float ratio = goal > 0 ? Mathf.Clamp01((float)value / goal) : 0f;
            RectTransform bar = _gaugeFill.rectTransform.parent as RectTransform;
            float inner = bar != null ? Mathf.Max(0f, bar.rect.width - _gaugeFill.rectTransform.offsetMin.x * 2f) : 0f;
            float width = inner * ratio;

            _gaugeFill.rectTransform.sizeDelta = new Vector2(width, _gaugeFill.rectTransform.sizeDelta.y);

            // 0 이면 아예 안 그린다 [소스 — width 가 0 이면 visible = false]
            _gaugeFill.enabled = width > 0f;
        }

        /// <summary>타이머. 원본 표기는 <c>mm:ss</c> 다 [실측 — <c>00:02</c>].</summary>
        public void SetElapsed(double seconds)
        {
            if (_timerText == null)
                return;

            int total = Mathf.Max(0, Mathf.FloorToInt((float)seconds));
            _timerText.text = $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>
        /// 최고 기록을 쓴다 [소스 <c>updateBestLevelDisplay</c> · <c>updateBestTimeDisplay</c>].
        /// <para>형식은 <c>「문구: 값」</c> 이고 시간은 <c>MM:SS</c> 다.</para>
        /// </summary>
        public void SetBestRecord(string levelLabel, int bestLevel, string timeLabel, double displaySeconds)
        {
            if (_bestLevelText != null)
                _bestLevelText.text = $"{levelLabel}: {bestLevel}";

            if (_bestTimeText == null)
                return;

            int total = (int)displaySeconds;
            _bestTimeText.text = $"{timeLabel}: {total / 60:00}:{total % 60:00}";
        }

        /// <summary>
        /// 레벨 <b>두 자리부터 글자가 작아진다</b> [소스 — <c>fontSize = level &gt; 9 ? 14 : 18</c>].
        /// <para>⚠ 안 줄이면 두 자리부터 숫자가 배지 밖으로 삐져나온다.</para>
        /// <para>★ 배율은 프리팹이 든다 — <b>한 자리 크기를 기준으로 «비»만 곱한다</b>. 화면 환산을 창이 다시 계산하지 않는다.</para>
        /// </summary>
        private const float LevelFontTwoDigitRatio = 14f / 18f;

        /// <summary>프리팹에 구워진 «한 자리» 글자 크기 — 처음 한 번 기억한다.</summary>
        private float _levelFontOneDigit;

        public void SetLevel(int level)
        {
            if (_levelText == null)
                return;

            if (_levelFontOneDigit <= 0f)
                _levelFontOneDigit = _levelText.fontSize;

            _levelText.text = level.ToString();
            _levelText.fontSize = level > 9
                ? _levelFontOneDigit * LevelFontTwoDigitRatio
                : _levelFontOneDigit;
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

        /// <summary>
        /// [소스 <c>fd</c>] — 1000 미만은 <c>N + 단위</c> · 10km 미만은 소수 한 자리 · 그 위는 정수 km.
        ///
        /// <para>
        /// ⚠ <b>단위 글자는 «문구 표»에서 온다</b> [소스 <c>metersShort</c>].
        /// 코드에 <c>"m"</c> 을 박으면 <b>번역이 안 따라간다</b> — 실제로 박혀 있었고,
        /// 표의 <c>metersShort</c> 는 <b>읽는 곳이 없는 채로</b> 남아 있었다 (<c>재발방지 #174</c>).
        /// </para>
        /// </summary>
        private static string FormatDistance(int meters)
        {
            int m = Mathf.Max(1, meters);

            if (m < 1000)
                return m + MetersShort;

            float km = m / 1000f;
            return km < 10f ? $"{km:F1}km" : $"{Mathf.RoundToInt(km)}km";
        }

        /// <summary>미터 단위 글자 — 문구 표에서 한 번만 읽어 둔다.</summary>
        private static string MetersShort
        {
            get
            {
                if (_metersShort == null)
                    _metersShort = JinHyung.Data.GameRoot.Instance.UndeadTextDataContainer.Ko("metersShort");

                return _metersShort;
            }
        }

        private static string _metersShort;

        private const float DesignHeight = 580f;

        /// <summary>
        /// 원본 논리 px → 캔버스 px. <b>프리팹 빌더의 <c>Scale</c> 과 같은 수</b>여야 한다 —
        /// 다르면 구운 자리와 런타임이 옮기는 자리가 어긋난다.
        /// </summary>
        private const float CanvasScale = 1080f / DesignHeight;
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
