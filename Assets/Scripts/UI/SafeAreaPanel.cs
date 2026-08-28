using UnityEngine;

namespace JinHyung.UI
{
    /// <summary>
    /// 자기 <c>RectTransform</c> 을 기기의 <b>안전 영역</b>에 맞춘다.
    ///
    /// <para>
    /// 노치·펀치홀·제스처 바가 있는 기기에서 <b>콘텐츠가 가려지거나 잘린다.</b>
    /// 원본(브라우저)에는 이 개념이 없으므로 <b>이관하면서 우리가 더해야 하는 것</b>이다 —
    /// 「원본에 없으니 안 한다」가 아니라 <b>플랫폼이 요구하는 것</b>이라 넣는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>배경(덮개)에는 붙이지 않는다.</b> 배경은 화면을 꽉 채워야 하고,
    /// 안전 영역 안으로 들어가야 하는 것은 <b>글자·버튼 같은 콘텐츠</b>다.
    /// </para>
    ///
    /// <para>
    /// 에디터에서는 <c>Screen.safeArea</c> 가 화면 전체라 아무 일도 안 일어난다 —
    /// <b>기기나 Device Simulator 에서만 값이 달라진다.</b>
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        private void Awake()
        {
            _rect = transform as RectTransform;
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            // 회전·해상도 변경·기기 시뮬레이터 전환에서 값이 바뀐다.
            if (Screen.safeArea == _lastSafeArea
                && Screen.width == _lastWidth
                && Screen.height == _lastHeight)
                return;

            Apply();
        }

        private void Apply()
        {
            if (_rect == null)
                _rect = transform as RectTransform;

            if (_rect == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = Screen.safeArea;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            // 앵커로 맞춘다 — 크기를 직접 넣으면 해상도가 바뀔 때마다 다시 계산해야 한다.
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;

            _lastSafeArea = safe;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
        }
    }
}
