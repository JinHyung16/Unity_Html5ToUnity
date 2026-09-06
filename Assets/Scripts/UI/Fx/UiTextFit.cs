using TMPro;
using UnityEngine;

namespace JinHyung.UI.Fx
{
    /// <summary>
    /// 글자를 <b>상자 안에 «줄여» 넣는다</b> — <c>배율 = min(상자폭 ÷ 글자폭, 상자높이 ÷ 글자높이, 1)</c>.
    ///
    /// <para>
    /// ★ <b>키우지는 않는다.</b> 상자보다 작으면 그대로 둔다 — 원본이 그렇다.
    /// </para>
    ///
    /// <para>
    /// ⚠⚠ <b>왜 필요한가</b> — 원본은 «거의 모든» 문구를 이렇게 넣는다(시작 버튼 · 제목 · 부제 ·
    /// 버튼 글자 · 최고 기록 · 말풍선). 우리는 글자 크기를 <b>고정</b>으로만 넣어서,
    /// <b>서체를 갈면 문구가 상자를 넘친다</b> — 자형 높이·폭이 서체마다 다르기 때문이다.
    /// 「지금 서체로는 마침 맞는다」는 이관이 아니다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 상자는 <b>캔버스 px</b> 로 넣는다 — 프리팹 빌더가 «원본 px × 환산 배율» 로 넣는다.
    /// </para>
    ///
    /// <para>문구가 바뀌면 다시 잰다 — 남은 시간·레벨처럼 <b>매 프레임 갈리는 글자</b>도 넘치지 않는다.</para>
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class UiTextFit : MonoBehaviour
    {
        [Tooltip("상자 폭 (캔버스 px). 0 이면 폭은 안 본다")]
        [SerializeField] private float _boxWidth;

        [Tooltip("상자 높이 (캔버스 px). 0 이면 높이는 안 본다")]
        [SerializeField] private float _boxHeight;

        private TMP_Text _text;
        private string _lastText;
        private float _lastFontSize = -1f;

        /// <summary><b>쓸 만한 크기</b>를 한 번이라도 쟀나 — 0 이 나온 시도는 «잰 것»으로 치지 않는다.</summary>
        private bool _measured;

        /// <summary>지금 적용된 배율 — <b>검사가 이 값을 본다</b> (1 이면 안 줄인 것이다).</summary>
        public float FitScale { get; private set; } = 1f;

        /// <summary>프리팹 빌더가 값을 넣는 자리. 상자는 <b>캔버스 px</b> 다.</summary>
        public void Configure(float boxWidth, float boxHeight)
        {
            _boxWidth = boxWidth;
            _boxHeight = boxHeight;
            _lastText = null;
            _measured = false;
            Fit();
        }

        private void OnEnable()
        {
            _lastText = null;
            _measured = false;
            Fit();
        }

        private void LateUpdate()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();

            // 글자나 크기가 그대로면 다시 재지 않는다 — 매 프레임 메시를 강제로 만들 이유가 없다.
            // ⚠ [사고] 여기에 «잰 적이 있나»를 안 넣었더니, 캔버스가 아직 자리를 안 잡아 크기가 0 으로
            //   나온 첫 시도를 그대로 캐시하고 <b>영영 다시 안 쟀다</b> — 맞추기가 늘 1.0 이었다.
            if (_measured && _text.text == _lastText && Mathf.Approximately(_text.fontSize, _lastFontSize))
                return;

            Fit();
        }

        private void Fit()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();

            if (_text == null)
                return;

            _lastText = _text.text;
            _lastFontSize = _text.fontSize;

            // ⚠ 재기 «전»에 배율을 1 로 되돌린다 — 줄어든 상태로 재면 계속 줄어든다
            transform.localScale = Vector3.one;

            if (string.IsNullOrEmpty(_text.text))
            {
                FitScale = 1f;
                _measured = true;
                return;
            }

            _text.ForceMeshUpdate();
            Vector2 size = _text.GetRenderedValues(false);

            // 아직 자리를 안 잡았다 — 다음 프레임에 다시 잰다
            if (size.x <= 0f || size.y <= 0f)
                return;

            _measured = true;

            float scale = 1f;

            if (_boxWidth > 0f && size.x > 0f)
                scale = Mathf.Min(scale, _boxWidth / size.x);

            if (_boxHeight > 0f && size.y > 0f)
                scale = Mathf.Min(scale, _boxHeight / size.y);

            FitScale = scale;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
