using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 위아래로 «떠다니는» 연출. 골 지시 화살표가 이것이다 [UIUX 2-d-3].
    ///
    /// <para>
    /// ★★★ <b>[정정 · 8회차] 「진폭·주기 미측정」이 닫혔다 — 그리고 1회차의 「진동 없음」은 앨리어싱이었다.</b>
    /// 원본은 <c>ArrowRestart</c> 에 붙은 <b>Sine 비헤이비어</b>이고 상수를 그대로 읽었다 [실측 §3-a]:
    /// <c>movement = 1</c>(수직) · <c>wave = 0</c>(정현파) · <b><c>_period</c> = 0.25 s</b> ·
    /// <b><c>_mag</c> = 10 px</b> · <c>_initialValue</c> = 788.
    /// 화면 실측(bbox y 796.19~816.11 ⇒ 진폭 9.96 · 극값 간격 125 ms × 2 = 250 ms)과 <b>양쪽이 일치</b>한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>왜 1회차가 「진동 없음(미측정)」으로 남겼나</b> — 캡처 격자가 <b>140 ms</b> 였다.
    /// <b>표본 간격이 주기(250 ms)의 절반보다 크면 진동이 «정지»로 보인다.</b>
    /// 「연출이 없다」가 <b>재는 간격 때문에</b> 나온 결론이었던 것이다.
    /// </para>
    ///
    /// <para>
    /// ★ 시간축은 <see cref="Time.unscaledDeltaTime"/> 다 — 클리어 연출로 게임이 멈춰도 배경 연출은 돌아야 한다.
    /// </para>
    /// </summary>
    public sealed class BlumgiFloatMotion : MonoBehaviour
    {
        // 8회차 실측값을 기본값으로 굽는다 — 배선이 Configure 로 덮어도 같은 값이 들어간다.
        [Header("Sine [8회차 실측 · 진폭 10 px · 주기 0.25 s]")]
        [SerializeField] private float _amplitudeWorld = 10f;

        [SerializeField] private float _periodSeconds = 0.25f;
        [SerializeField] private float _phase01;

        private Vector3 _origin;
        private float _time;

        private void Awake()
        {
            _origin = transform.localPosition;
        }

        private void OnEnable()
        {
            _time = _phase01 * _periodSeconds;
        }

        private void Update()
        {
            if (_periodSeconds <= 0f)
                return;

            _time += Time.unscaledDeltaTime;

            float t = _time / _periodSeconds * Mathf.PI * 2f;
            float y = Mathf.Sin(t) * BlumgiUnits.ToUnits(_amplitudeWorld);

            transform.localPosition = _origin + new Vector3(0f, y, 0f);
        }

        /// <summary>배선이 값을 넣는 자리. 상수를 고치러 프리팹을 다시 굽지 않아도 된다.</summary>
        public void Configure(float amplitudeWorld, float periodSeconds, float phase01)
        {
            _amplitudeWorld = amplitudeWorld;
            _periodSeconds = periodSeconds;
            _phase01 = Mathf.Repeat(phase01, 1f);
            _time = _phase01 * _periodSeconds;
        }

        /// <summary>부모가 위치를 옮겼을 때 기준점을 다시 잡는다.</summary>
        public void ResetOrigin()
        {
            _origin = transform.localPosition;
        }
    }
}
