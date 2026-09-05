using TMPro;
using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// ★ <b>글자마다 위상차를 둔 세로 파동</b>. WELCOME 문자가 이것이다.
    ///
    /// <para>
    /// <b>정지 캡처에는 절대 안 찍힌다</b> — 캡처만 보면 「그냥 글자」로 옮기게 된다 (UIUX.md 「캡처에 안 찍히는 것 둘」).
    /// 원본은 문자열 안에 <c>[offsetY=…]</c> 를 <b>글자마다</b> 넣고 <b>1/60 초마다</b> 다시 만든다 [실측 런타임 문자열].
    /// </para>
    ///
    /// <para>
    /// ★★★ <b>[24회차 실측] 식이 닫혔다</b> (rAF 504프레임 · 4.2 s · 7채널 전수 · 정본 <c>04 §4-e-2</c> · <c>07 §17-c-3</c>):
    /// <code>
    /// offsetY_i(t) = 5.000 · sin( 250 °/s · t_q  +  50.00° · i )
    /// t_q = floor(t × 60) / 60                     ← ★ 60 Hz 계단
    /// </code>
    /// 주기 <b>1.44 s</b>(= 360/250 · 스캔 최적 1.4399) · 진폭 <b>5.000</b>(7/7 이 4.9989~4.9995) ·
    /// 글자당 위상 <b>+50.00°</b>(6/6 이 49.99~50.01) · <b>파형 = 사인</b>(잔차 RMS 0.064 px = 진폭의 1.3 %).
    /// 같은 잣대의 대조군이 골 화살표다 — 사인 0.021 vs 삼각파 0.153 (<c>05 §4-a</c>).
    /// </para>
    ///
    /// <para>
    /// ★★ <b>화면에는 «60 Hz 계단»으로 나온다.</b> 시트 조건이 <c>Every 0.0166666… s</c> 이고,
    /// 실제로 값이 <b>바뀐 간격이 중앙 16.7 ms</b>(n = 252 변화 / 4.2 s)다 — rAF 8.3 ms 라
    /// <b>같은 값이 2 프레임 유지</b>되는 것이 그대로 관측됐다.
    /// ⚠ <b>매 프레임 갱신하면 원본보다 «부드럽다».</b> 그래서 시간을 <see cref="UpdateHz"/> 로 계단화한 뒤 식에 넣는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>위상 부호에 주의</b> — <b>뒤 글자일수록 «늦다»</b>(+50°·i). 부호를 뒤집으면 파도가 <b>반대 방향</b>으로 흐른다.
    /// 원본 y 는 <b>아래가 +</b> 라 유니티로 옮길 때 <b>부호를 한 번 더</b> 뒤집는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 같은 문자열에 <b>외곽선 두께 8 world</b>(<c>[lineThickness=8][outlineback=rgb(0,0,0)]</c>)가
    /// <b>글자마다 개별로</b> 걸려 있다 [실측] — 그건 TMP 머티리얼이 든다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>[해소 · 25회차] world 진폭 → px 배율은 <c>Main</c> 의 <c>1.6875</c> 다.</b>
    ///
    /// <para>
    /// 레이어 뷰포트를 직접 떠서 닫혔다 [실측] — 기준 레이어(<c>BG</c>·<c>FG</c>·<c>UI</c>)가
    /// <b>2276.766 × 1280</b> world 인데 <c>Main</c> 은 <b>1138.382 × 640</b> 이라 비가 <b>정확히 2.000000</b> 이다.
    /// ⇒ 1920×1080 에서 기준 <c>0.84375</c> · <c>Main</c> <b><c>1.6875</c></b>.
    /// 스크린샷 픽셀로 독립 검산도 됐다 — <c>Main</c> 인 <c>PLAYERButton</c> 두 장이
    /// <b>440 world = 322 px</b>(예측 323.1 · 오차 0.35 %).
    /// ⇒ <b>진폭 5.000 world = 8.4375 px @1920</b> (같은 오브젝트 #558341 의 외곽선 8 world = 13.5 px).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>크레딧은 여기 묶이지 않는다.</b> <c>TextUI</c> #904361 은 <c>FG</c> 레이어라
    /// <c>[size=35]/[size=30]</c> 이 <b>0.84375</b> 를 타 <b>29.53 / 25.31 px</b> 다 —
    /// 셋을 한 배율로 묶으면 크레딧이 «2배»가 된다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class BlumgiWaveText : MonoBehaviour
    {
        /// <summary>
        /// ★ 원본이 문자열을 다시 만드는 <b>속도</b> [실측 24회차 — 시트 <c>Every 1/60 s</c> · 갱신 간격 중앙 16.7 ms].
        /// <b>이 값이 0 이면 매 프레임이 되어 원본보다 부드러워진다.</b>
        /// </summary>
        private const float UpdateHz = 60f;

        /// <summary>
        /// ★ <c>Main</c> 레이어의 world → 1920×1080 px 배율 [실측 25회차 · 뷰포트 <b>1138.382 × 640</b>].
        /// 기준 레이어(<c>1080/1280 = 0.84375</c>)의 <b>정확히 2배</b>다.
        /// </summary>
        private const float MainLayerPixelScale = 1080f / 640f;   // 1.6875

        /// <summary>글자당 위상 [실측 24회차 — 6/6 채널이 49.99~50.01°]. 0~1 스케일이라 50/360 이다.</summary>
        private const float MeasuredPhasePerCharacter = 50f / 360f;   // 0.1388889

        [Header("파동 [24회차 실측 · 사인 · 1.44 s · 진폭 5.000 · 글자당 +50.00°]")]
        [SerializeField] private float _amplitudeWorld = 5f;

        // 360 / 250 °/s = 1.44 s. ⚠ 옛 값 1.4 은 «2프레임 관측 시절»의 어림이었다.
        [SerializeField] private float _periodSeconds = 1.44f;

        /// <summary>
        /// 글자 하나당 밀리는 위상(0~1). ★ <b>뒤 글자가 «늦다»</b> — 부호를 뒤집으면 파도가 반대로 흐른다.
        /// ⚠ 옛 값 0.14 는 「7글자가 한 파장에 얹혀 있다」는 어림이었다. 실측은 <b>정확히 50°</b> 다.
        /// </summary>
        [SerializeField] private float _phasePerCharacter = MeasuredPhasePerCharacter;

        private TMP_Text _text;
        private float _time;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            _time = 0f;
        }

        private void LateUpdate()
        {
            if (_text == null || _periodSeconds <= 0f)
                return;

            _time += Time.unscaledDeltaTime;

            _text.ForceMeshUpdate();
            TMP_TextInfo info = _text.textInfo;

            if (info == null || info.characterCount == 0)
                return;

            // ★★ world 진폭을 «캔버스 픽셀»로 옮긴다.
            //   [정정 · 25회차 실측] `WELCOME` 은 `Main` 레이어라 배율이 기준 레이어의 «정확히 2배»다 —
            //   1080/640 = 1.6875. 예전 값 1080/1280 = 0.84375 는 «기준 레이어» 배율이었다.
            //   ⇒ 진폭 5.000 world = 8.4375 px @1920.
            float amplitude = _amplitudeWorld * MainLayerPixelScale;

            // ★★ 60 Hz «계단». 원본은 시트 `Every 1/60 s` 로 문자열을 다시 만들어서
            //    같은 offsetY 가 rAF 두 프레임 동안 유지된다 [실측 24회차 — 갱신 간격 중앙 16.7 ms].
            //    여기서 시간을 계단화하지 않으면 우리 쪽이 «원본보다 부드럽다» — 수치가 아니라 «보이는 것»이 틀린다.
            float steppedTime = UpdateHz > 0f
                ? Mathf.Floor(_time * UpdateHz) / UpdateHz
                : _time;

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];

                if (character.isVisible == false)
                    continue;

                // ★ 위상은 «뒤 글자일수록 +» 다 [실측 24회차 · 6/6 이 +50.00°].
                //   여기 부호를 뒤집으면 파도가 «반대 방향»으로 흐른다 — 개수·진폭 검사로는 안 잡힌다.
                float phase = (steppedTime / _periodSeconds + i * _phasePerCharacter) * Mathf.PI * 2f;

                // ⚠ 원본 y 는 «아래가 +» 다 — offsetY 양수가 아래로 내려간다. 부호를 뒤집는다.
                var offset = new Vector3(0f, -Mathf.Sin(phase) * amplitude, 0f);

                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;

                Vector3[] vertices = info.meshInfo[material].vertices;

                for (int v = 0; v < 4; v++)
                    vertices[vertex + v] += offset;
            }

            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                _text.UpdateGeometry(info.meshInfo[m].mesh, m);
            }
        }

        /// <summary>
        /// 배선이 값을 넣는 자리. 프리팹을 다시 굽지 않고 바꿀 수 있다.
        /// ⚠ <b>60 Hz 계단은 여기로 안 받는다</b> — 원본 시트의 <c>Every 1/60 s</c> 는 «상수»라 갈릴 값이 아니다.
        /// </summary>
        public void Configure(float amplitudeWorld, float periodSeconds, float phasePerCharacter)
        {
            _amplitudeWorld = amplitudeWorld;
            _periodSeconds = periodSeconds;
            _phasePerCharacter = phasePerCharacter;
        }
    }
}
