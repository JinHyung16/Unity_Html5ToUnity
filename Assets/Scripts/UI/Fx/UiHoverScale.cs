using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JinHyung.UI.Fx
{
    /// <summary>
    /// 포인터가 올라가면 배율·틴트를 바꾼다 (원본 <c>pointerenter</c>/<c>pointerleave</c>).
    /// 값은 <b>프리팹</b>이 든다 — 「시작」 ×1.06 + 틴트, 카드 ×1.04, 부활 ×1.02 처럼 버튼마다 다르다.
    ///
    /// <para>배율은 <see cref="UiScaleStack"/> 이 다른 몫과 곱한다. 틴트는 <see cref="Graphic"/> 색에 직접 넣는다 —
    /// 유니티 <c>Button</c> 의 색 전환(Transition)은 끄고 쓴다. 두 쪽이 같은 색을 쓰면 서로 덮는다.</para>
    /// </summary>
    [RequireComponent(typeof(UiScaleStack))]
    public sealed class UiHoverScale : MonoBehaviour, IUiScaleFactor, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float _hoverScale = 1.04f;

        [Tooltip("올라갔을 때 틴트를 바꾸나")]
        [SerializeField] private bool _useTint;

        [SerializeField] private Color _hoverTint = Color.white;

        [Tooltip("틴트를 받을 그래픽. 비우면 같은 오브젝트의 Graphic")]
        [SerializeField] private Graphic _target;

        private Color _normalTint = Color.white;
        private bool _normalCached;

        public bool Hovered { get; private set; }

        public event Action<bool> OnHoverChanged;

        public float ScaleFactor
        {
            get { return Hovered ? _hoverScale : 1f; }
        }

        public void Configure(float hoverScale)
        {
            _hoverScale = hoverScale;
            _useTint = false;
        }

        public void Configure(float hoverScale, Color hoverTint, Graphic target = null)
        {
            _hoverScale = hoverScale;
            _useTint = true;
            _hoverTint = hoverTint;
            _target = target;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Set(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Set(false);
        }

        private void OnDisable()
        {
            Set(false);
        }

        private void Set(bool hovered)
        {
            if (Hovered == hovered)
                return;

            Hovered = hovered;
            ApplyTint();
            OnHoverChanged?.Invoke(hovered);
        }

        private void ApplyTint()
        {
            if (_useTint == false)
                return;

            if (_target == null)
                _target = GetComponent<Graphic>();

            if (_target == null)
                return;

            if (_normalCached == false)
            {
                _normalTint = _target.color;
                _normalCached = true;
            }

            _target.color = Hovered ? _hoverTint : _normalTint;
        }
    }
}
