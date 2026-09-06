using UnityEngine;
using UnityEngine.InputSystem;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 이동 입력. <b>확정표 B — 터치 가상 조이스틱 + WASD 병행</b>.
    ///
    /// <para>
    /// 원본은 서비스 페이지 표기상 「WASD 키 또는 조이스틱」이고, 실측에서 <b>WASD 가 동작</b>했다.
    /// 모바일 조이스틱은 원본을 «못 본» 부분이라(데스크톱만 실측) <b>우리가 만드는 것</b>이다 —
    /// 그 사실은 확정표 B(입력 대체)로 등재돼 있다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>정규화하지 않고 «날 것»으로 넘긴다.</b> 대각 정규화는 시뮬이 한다 —
    /// 두 곳에서 하면 대각 속도가 √2 로 갈린다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>y 부호를 여기서 뒤집는다.</b> 화면 위(+y)는 원본 world 에서 −y 다.
    /// </para>
    /// </summary>
    public sealed class UndeadMoveInput : MonoBehaviour
    {
        /// <summary>조이스틱 중심에서 이 거리(px) 이상 끌면 최대 입력이다.</summary>
        [SerializeField] private float _joystickRadiusPixels = 120f;

        private bool _dragging;
        private Vector2 _origin;

        /// <summary>지금 프레임의 이동 입력. 길이는 0~1 이고 <b>정규화는 시뮬이 한다</b>.</summary>
        public UndeadVec2 Read()
        {
            Vector2 keyboard = ReadKeyboard();

            if (keyboard.sqrMagnitude > 0f)
                return new UndeadVec2(keyboard.x, -keyboard.y);

            Vector2 touch = ReadPointerDrag();
            return new UndeadVec2(touch.x, -touch.y);
        }

        private static Vector2 ReadKeyboard()
        {
            Keyboard k = Keyboard.current;

            if (k == null)
                return Vector2.zero;

            var v = Vector2.zero;

            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;

            return v;
        }

        private Vector2 ReadPointerDrag()
        {
            Pointer p = Pointer.current;

            if (p == null)
                return Vector2.zero;

            if (p.press.isPressed == false)
            {
                _dragging = false;
                return Vector2.zero;
            }

            Vector2 now = p.position.ReadValue();

            if (_dragging == false)
            {
                _dragging = true;
                _origin = now;
                return Vector2.zero;
            }

            Vector2 delta = now - _origin;
            float max = Mathf.Max(1f, _joystickRadiusPixels);
            return Vector2.ClampMagnitude(delta / max, 1f);
        }
    }
}
