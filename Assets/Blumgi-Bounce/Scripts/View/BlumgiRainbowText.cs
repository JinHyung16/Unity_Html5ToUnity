using TMPro;
using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// <c>YES!</c> 의 <b>색상환 순환</b>.
    ///
    /// <para>
    /// ★★★ <b>[해소 · 17회차] 「채움이 도는가 외곽선이 도는가」가 닫혔다 — «외곽선»이다.</b>
    /// 소스 텍스처(266 × 116 · 불투명 24,269 px)를 그대로 꺼내 픽셀을 셌다 [실측]:
    /// <b>알파 경계로부터 1 · 2 · 3 px 이 채도 높은 핑크 <c>(255, 102, 154)</c> 100 %</b>
    /// (n = 2,730 · <b>예외 0건</b>)이고 <b>안쪽은 흰색 7,082 px</b> 로 압도적이다.
    /// 색 회전은 <c>worldInfo._instanceEffectList</c> 의 <b><c>AdjustHSL(hue, 1, 1)</c> 인스턴스 이펙트</b>이고
    /// <b>흰색은 채도가 0 이라 색상 회전이 원리적으로 아무 효과가 없다</b> ⇒ <b>도는 것은 외곽선뿐</b>이다.
    /// </para>
    ///
    /// <para>
    /// ⇒ 예전 구현은 <b>정확히 반대</b>였다 (채움이 돌고 외곽선이 검정).
    /// 이제 <b>채움은 흰색 고정 · 외곽선이 돈다</b>.
    /// </para>
    ///
    /// <para>
    /// ★ <b>주기 1007.7 ms</b> [실측 — 이펙트 파라미터가 <c>0 → 1</c> 까지 오르고 즉시 0 으로 되감긴다.
    /// 되감김 2회 관측]. 8회차의 「트윈 1.0 s」와 정합하고, 1회차 픽셀 991 ms 와도 맞는다.
    /// <b>채도·명도 배수는 전 프레임 1.000 고정</b>이라 «hue 만» 돈다.
    /// </para>
    ///
    /// <para>
    /// 기준색은 <b>외곽선 원색 <c>(255, 102, 154)</c></b> 다 — HSV 로 <c>H 339.6° · S 0.600 · V 1.000</c>.
    /// ★ 1회차가 화면에서 채록한 6색(연두 <c>#A7FF74</c> · 청록 <c>#66FFEE</c> · 청보라 <c>#7866FF</c> ·
    /// 마젠타 <c>#FF66EB</c> · 적분홍 <c>#FF6689</c> · 주황 <c>#FFAF66</c>)이 <b>전부 S ≈ 0.6 · V = 1.0</b> 인 것이
    /// <b>「채록된 색이 곧 외곽선이었다」는 교차 검산</b>이다 — 두 실측이 물린다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>사이클 «안»의 이징은 미측정</b>이다 — 파라미터의 초당 기울기가 중앙값 1.084 /s 에
    /// 최소 0 · 최대 2.84 /s 로 <b>균일하지 않다</b>. 이징 «이름»은 시트를 읽어야 나온다.
    /// 지어내지 않고 <b>등속</b>으로 둔다 (재발방지 #48 — 미측정은 미측정으로 표시).
    /// </para>
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class BlumgiRainbowText : MonoBehaviour
    {
        /// <summary>색상환 한 바퀴 [실측 17회차 — 되감김 2회의 간격 1007.7 ms].</summary>
        [Header("색상환 [17회차 실측 · 1.0077 s 에 한 바퀴]")]
        [SerializeField] private float _cycleSeconds = 1.0077f;

        /// <summary>
        /// 외곽선 원색의 색상각 [실측 <c>(255, 102, 154)</c> ⇒ H 339.6° = 0.9433].
        /// <c>AdjustHSL</c> 의 hue 파라미터가 0 일 때의 색이 이것이다.
        /// </summary>
        [SerializeField, Range(0f, 1f)] private float _baseHue01 = 0.9433f;

        /// <summary>외곽선 채도 [실측 <c>(255,102,154)</c> 의 S = 153/255 = 0.600 · 이펙트 배수 1.000 고정].</summary>
        [SerializeField, Range(0f, 1f)] private float _saturation = 0.6f;

        /// <summary>외곽선 명도 [실측 V = 1.000 · 이펙트 배수 1.000 고정].</summary>
        [SerializeField, Range(0f, 1f)] private float _value = 1f;

        /// <summary>
        /// 채움 색 — <b>흰색 고정</b> [실측 내부 픽셀 (255,255,255) 7,082 px 로 압도적 1위].
        /// ⚠ 흰색은 채도 0 이라 <b>색상 회전이 원리적으로 안 먹는다</b> — 그래서 «고정»이 곧 원본 기전이다.
        /// </summary>
        [SerializeField] private Color _fillColor = Color.white;

        /// <summary>
        /// 외곽선 두께 (TMP 정규화 0~1).
        /// ⚠ <b>원본 두께는 «소스 3 px»</b> 이다 [실측 — 경계거리 1~3 px 이 채도높음 100 %,
        /// 화면 배율 1.7256 ⇒ ≈ 5.2 px, 스쿼시로 4.6 ~ 5.7 px 사이에서 숨 쉰다].
        /// <b>TMP 0~1 로의 환산은 미측정</b>이다 — TMP 외곽선은 «안쪽»으로 자라서
        /// 예전 시도의 0.3375 는 획을 통째로 먹었다. 그보다 뚜렷이 작은 값을
        /// <c>추정(근거: 획을 안 먹는 상한)</c> 으로 두고 <b>노출</b>한다.
        /// </summary>
        [SerializeField, Range(0f, 1f)] private float _outlineWidth = 0.2f;

        private TMP_Text _text;
        private float _time;

        /// <summary>지금 색상각(0~1) — <b>이펙트 파라미터 그 값</b>이다. <b>검사가 이것을 읽어 주기를 채점한다</b>.</summary>
        public float Hue01 { get; private set; }

        /// <summary>지금 외곽선 색. <b>검사가 «도는 것이 외곽선»임을 이것으로 본다</b>.</summary>
        public Color OutlineColor { get; private set; } = Color.white;

        /// <summary>지금 채움 색. <b>돌면 안 된다</b> — 검사가 «안 변한다»를 본다.</summary>
        public Color FillColor
        {
            get { return _fillColor; }
        }

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            _time = 0f;
            ApplyOutlineMaterial();
        }

        /// <summary>
        /// ★ <b>외곽선은 «런타임»에 건다.</b> 프리팹에 구우면 <c>fontMaterial</c> 인스턴스가
        /// 에셋이 아니라 저장 때 <c>{fileID: 0}</c> 으로 굳어 <b>화면에 안 나온다</b>
        /// (<c>BlumgiPrefabBuilder.ApplyOutline</c> 진단). 런타임 인스턴스는 그 문제가 없다.
        /// </summary>
        private void ApplyOutlineMaterial()
        {
            if (_text == null || _text.font == null)
                return;

            _text.fontMaterial.EnableKeyword("OUTLINE_ON");
            _text.outlineWidth = _outlineWidth;
        }

        private void Update()
        {
            if (_text == null || _cycleSeconds <= 0f)
                return;

            _time += Time.unscaledDeltaTime;

            Hue01 = Mathf.Repeat(_time / _cycleSeconds, 1f);

            // ★ 채움은 «안 돈다» — 흰색이라 색상 회전이 원리적으로 안 먹는 것을 그대로 옮긴다.
            _text.color = _fillColor;

            OutlineColor = Color.HSVToRGB(Mathf.Repeat(_baseHue01 + Hue01, 1f), _saturation, _value);
            _text.outlineColor = OutlineColor;
        }

        /// <summary>주기를 바꿔야 할 때. 상수를 고치러 프리팹을 다시 굽지 않아도 된다.</summary>
        public void Configure(float cycleSeconds)
        {
            _cycleSeconds = cycleSeconds;
        }
    }
}
