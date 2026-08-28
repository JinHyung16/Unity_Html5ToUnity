using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 배경을 <b>타일링</b>한다. 원본 <c>body</c> 의
    /// <c>background-repeat: repeat</c> · <c>background-size: auto</c> (<c>style.css:2~3</c>) 에 대응한다.
    ///
    /// <para>
    /// ⚠ <b>텍스처를 늘리지 않는다.</b> 원본 1500×1000 을 그대로 두고
    /// <b>표시 배율(×1.6)만</b> 먹인다 — 확대 리샘플링은 화질만 깎고 얻는 것이 없다.
    /// </para>
    ///
    /// <para>
    /// 타일 수는 화면 크기에서 나오므로 <c>uvRect</c> 를 런타임에 계산한다.
    /// 텍스처의 <c>wrapMode</c> 가 <c>Repeat</c> 여야 이음매가 안 보인다 (임포트 설정에서 지정).
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class TiledBackground : MonoBehaviour
    {
        /// <summary>04_UIUX규칙.md 의 환산 배율. <b>여기 한 곳만 통과한다.</b></summary>
        [SerializeField] private float _scale = 1.6f;

        private RawImage _rawImage;
        private RectTransform _rect;
        private Vector2 _lastSize;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _rect = transform as RectTransform;
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (_rect == null)
                return;

            Vector2 size = _rect.rect.size;

            if (size == _lastSize)
                return;

            Apply();
        }

        private void Apply()
        {
            if (_rawImage == null || _rawImage.texture == null || _rect == null)
                return;

            Vector2 size = _rect.rect.size;

            if (size.x <= 0f || size.y <= 0f)
                return;

            float tileWidth = _rawImage.texture.width * _scale;
            float tileHeight = _rawImage.texture.height * _scale;

            _rawImage.uvRect = new Rect(0f, 0f, size.x / tileWidth, size.y / tileHeight);
            _lastSize = size;
        }
    }
}
