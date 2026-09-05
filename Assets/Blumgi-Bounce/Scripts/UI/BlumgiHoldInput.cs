using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 이 게임의 <b>유일한 입력</b> — 화면 아무 데나 «누르고 있다 뗀다».
    ///
    /// <para>
    /// ★★ <b>좌표를 위로 올리지 않는다.</b> 원본은 마우스 «위치»가 발사에 영향이 없는
    /// <b>파워 1축</b> 게임이다 (4점 대조로 확정 [Client 1회차 실측]).
    /// 여기서 <c>eventData.position</c> 을 넘기기 시작하면 조준이 생기고, 그게 곧 결함이다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>HUD 버튼보다 «뒤»에 둔다.</b> 이 판은 <c>UIRoot</c> 의 첫 자식이라 창(버튼)보다 먼저 그려진다 —
    /// <c>GraphicRaycaster</c> 는 앞에 있는 것을 먼저 돌려주므로 <b>버튼을 누르면 발사되지 않는다.</b>
    /// (전체화면 판을 버튼 «위»에 깔면 버튼이 통째로 죽는다.)
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>꺼질 때 «떼기»를 흘리지 않는다.</b> 화면이 바뀌는 순간 릴리즈가 나가면
    /// 원본에 없는 발사가 한 번 생긴다 — 누르던 상태만 지운다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlumgiHoldInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>누르기 시작.</summary>
        public event Action HoldBegan;

        /// <summary>손을 뗐다 → 발사.</summary>
        public event Action HoldEnded;

        /// <summary>지금 누르고 있나. 재생 검사가 「홀드가 실제로 접수됐나」를 볼 때 읽는다.</summary>
        public bool IsHolding { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHolding)
                return;

            IsHolding = true;
            HoldBegan?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (IsHolding == false)
                return;

            IsHolding = false;
            HoldEnded?.Invoke();
        }

        private void OnDisable()
        {
            IsHolding = false;
        }
    }
}
