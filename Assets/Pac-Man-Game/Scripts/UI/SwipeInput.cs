using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 화면을 밀면 그 방향을 알린다. 원본의 <b>방향키/WASD 를 대신</b>한다 (확정 A).
    ///
    /// <para>
    /// ⚠ <b>원본 입력을 «바꾼 것»이라 「의도된 차이」로 등재돼 있다.</b>
    /// 모바일에 키보드가 없어서지, 원본이 이랬던 것이 아니다.
    /// </para>
    ///
    /// <para>
    /// ★ 원본의 <c>trySetDirection</c> 은 방향을 <b>예약만</b> 한다 —
    /// 실제 전환은 팩맨이 정렬을 보고 결정한다. 스와이프도 <b>예약까지만</b> 한다.
    /// 그래서 「모퉁이 조금 앞에서 미리 밀어 두면 돌아간다」는 원본 조작감이 그대로 산다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 화면 전체를 덮는 <b>투명 판</b>에 붙인다 — 보드 위 어디를 밀어도 먹혀야 한다.
    /// </para>
    /// </summary>
    public class SwipeInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler
    {
        /// <summary>
        /// 이만큼 밀어야 «스와이프»로 친다 (화면 짧은 변 대비 비율).
        /// 너무 작으면 탭이 방향으로 읽히고, 너무 크면 빠른 조작이 씹힌다.
        /// </summary>
        [SerializeField] private float _thresholdRatio = 0.03f;

        /// <summary>방향이 정해질 때. 원본 <c>trySetDirection(dx, dy)</c> 에 대응한다.</summary>
        public event Action<int, int> OnDirection;

        /// <summary>첫 입력. 원본은 여기서 배경음을 켠다 (브라우저 제약) — 우리도 시점을 맞춘다.</summary>
        public event Action OnFirstInput;

        private Vector2 _start;
        private bool _dragging;
        private bool _fired;
        private bool _firstInputSent;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_firstInputSent)
                return;

            _firstInputSent = true;
            OnFirstInput?.Invoke();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _start = eventData.position;
            _dragging = true;
            _fired = false;
        }

        /// <summary>
        /// ⚠ <b>드래그가 «끝날 때»가 아니라 문턱을 넘는 «순간»에 방향을 준다.</b>
        /// 손을 뗄 때까지 기다리면 조작이 한 박자 늦는다 — 실시간 게임에서는 그게 곧 체감 지연이다.
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging == false || _fired)
                return;

            Vector2 delta = eventData.position - _start;
            float threshold = Mathf.Min(Screen.width, Screen.height) * _thresholdRatio;

            if (delta.magnitude < threshold)
                return;

            _fired = true;

            // 축 하나만 고른다 — 원본에 대각선 방향이 없다.
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                OnDirection?.Invoke(delta.x > 0f ? 1 : -1, 0);
            else
                OnDirection?.Invoke(0, delta.y > 0f ? -1 : 1);   // ⚠ 화면 위 = 격자 y 감소
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
        }

        private void OnDisable()
        {
            _dragging = false;
            _fired = false;
        }
    }
}
