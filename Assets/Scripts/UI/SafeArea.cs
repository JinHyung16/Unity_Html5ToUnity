using UnityEngine;

namespace JinHyung.UI
{
    /// <summary>
    /// 붙은 <c>RectTransform</c> 을 <b>안전 영역</b>에 맞춘다.
    ///
    /// <para>
    /// 원본(브라우저)에는 이 개념이 없다 — <b>플랫폼이 요구하는 것</b>이라 이관하면서 더한다.
    /// 「원본에 없으니 안 한다」가 아니다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>배경(덮개)에는 붙이지 않는다.</b> 창의 구조는 이렇게 간다 —
    /// <c>Window → BG(화면 꽉 채움) · SafeArea(그 안에 나머지 UI)</c>.
    /// 배경까지 안전 영역 안으로 넣으면 <b>노치 기기에서 가장자리에 빈 띠</b>가 생긴다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b><c>OnEnable</c> 한 번으로는 부족하다.</b> 기기 회전·해상도 변경·에디터 게임뷰 크기 변경에
    /// <c>safeArea</c> 가 바뀐다. 회전 이벤트를 신뢰할 수 없는 기기가 있어 <b>값이 달라졌는지 지켜본다</b> —
    /// 비교는 실제 적용보다 훨씬 싸다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>에디터에서는 안전 영역이 화면 전체라 아무 일도 안 일어난다.</b>
    /// 기기나 Device Simulator 에서만 값이 달라진다 — 그래서 「있는지」는 <b>검사가 기계로 확인</b>한다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTF;

        /// <summary>
        /// 안전 영역을 적용할 변. <b>세로 게임은 보통 상·하면 충분하다</b> —
        /// 좌우까지 물리면 화면이 괜히 좁아진다.
        /// </summary>
        [SerializeField]
        private SafeAreaUtil.ApplySideType _applySide = SafeAreaUtil.ApplySideType.Vertical;

        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        private void Awake()
        {
            if (_rectTF == null)
                _rectTF = transform as RectTransform;
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea == _lastSafeArea
                && Screen.width == _lastWidth
                && Screen.height == _lastHeight)
            {
                return;
            }

            Apply();
        }

        private void Apply()
        {
            if (_rectTF == null)
                _rectTF = transform as RectTransform;

            if (_rectTF == null)
                return;

            _lastSafeArea = Screen.safeArea;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            SafeAreaUtil.ApplySafeArea(_rectTF, _applySide);
        }
    }
}
