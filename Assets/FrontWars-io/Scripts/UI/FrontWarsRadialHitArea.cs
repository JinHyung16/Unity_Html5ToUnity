using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 원형 메뉴 누름 판 — 누른 자리 · 올린 자리를 이 판 가운데 기준 좌표로 올린다. 조각 고르기(각도 · 반지름)는 창이 한다.
    /// <para>조각 그림은 원형 채우기 이미지라 사각형 전체가 레이캐스트에 잡힌다 — 그래서 조각마다 버튼을 두지 않고 여기 한 판에서 계산한다.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class FrontWarsRadialHitArea : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
    {
        public event Action<Vector2> OnClickLocal;
        public event Action<Vector2> OnHoverLocal;
        public event Action OnExit;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (ToLocal(eventData.position, eventData.pressEventCamera, out Vector2 local))
                OnClickLocal?.Invoke(local);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (ToLocal(eventData.position, eventData.enterEventCamera, out Vector2 local))
                OnHoverLocal?.Invoke(local);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnExit?.Invoke();
        }

        private bool ToLocal(Vector2 screen, Camera camera, out Vector2 local)
        {
            var rect = (RectTransform)transform;
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out local);
            local -= rect.rect.center;
            return ok;
        }
    }
}
