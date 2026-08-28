using System;
using UnityEngine;

namespace JinHyung.UI
{
    /// <summary>
    /// <c>Screen.safeArea</c> 를 <c>RectTransform</c> 앵커로 옮겨 준다.
    ///
    /// <para>
    /// 노치·펀치홀·홈 인디케이터를 피해야 하는 <b>변만 골라서</b> 적용한다.
    /// 세로 게임은 보통 <see cref="ApplySideType.Vertical"/>(상·하)만 필요하다 —
    /// <b>좌우까지 물리면 화면이 괜히 좁아진다.</b>
    /// </para>
    /// </summary>
    public static class SafeAreaUtil
    {
        [Flags]
        public enum ApplySideType
        {
            None = 0,
            Top = 1 << 0,
            Bottom = 1 << 1,
            Left = 1 << 2,
            Right = 1 << 3,

            Vertical = Top | Bottom,
            Horizontal = Left | Right,
            All = Vertical | Horizontal,
        }

        public static void ApplySafeArea(RectTransform rect, ApplySideType type)
        {
            if (rect == null)
                return;

            Rect safeArea = Screen.safeArea;
            float width = Screen.width;
            float height = Screen.height;

            // 화면 크기가 0인 순간(초기화 직전 등)에 나누면 NaN 이 앵커에 박혀 UI 가 사라진다.
            if (width <= 0f || height <= 0f)
                return;

            rect.anchorMin = new Vector2(
                (type & ApplySideType.Left) != 0 ? safeArea.xMin / width : 0f,
                (type & ApplySideType.Bottom) != 0 ? safeArea.yMin / height : 0f);

            rect.anchorMax = new Vector2(
                (type & ApplySideType.Right) != 0 ? safeArea.xMax / width : 1f,
                (type & ApplySideType.Top) != 0 ? safeArea.yMax / height : 1f);

            // 앵커만 바꾸고 offset 을 두면 기존 여백이 그대로 남아 결과가 어긋난다.
            // 앵커로 영역을 정한 이상 offset 은 0 이어야 한다.
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
