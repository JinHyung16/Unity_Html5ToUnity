using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.UI.Fx
{
    /// <summary>
    /// «고름/평소» 같은 상태에 따른 배율·알파. 원본 카드는 고르면 <b>×1.08 · 알파 1</b>, 평소 <b>×1 · 알파 .9</b>.
    /// 값은 <b>프리팹</b>이 들고, 창은 <see cref="SetSelected"/> 만 부른다.
    ///
    /// <para>배율은 <see cref="UiScaleStack"/> 이 다른 몫(호버 등)과 곱한다. 알파는 <see cref="Graphic"/> 색의 a 에만 손댄다 —
    /// 틴트(rgb)는 데이터(카드 축 색)가 정하므로 건드리지 않는다.</para>
    /// </summary>
    [RequireComponent(typeof(UiScaleStack))]
    public sealed class UiStateScale : MonoBehaviour, IUiScaleExclusive
    {
        [SerializeField] private float _selectedScale = 1.08f;
        [SerializeField] private float _idleScale = 1f;
        [SerializeField] private float _selectedAlpha = 1f;
        [SerializeField] private float _idleAlpha = 0.9f;

        [Tooltip("알파를 받을 그래픽. 비우면 같은 오브젝트의 Graphic")]
        [SerializeField] private Graphic _target;

        public bool Selected { get; private set; }

        public float ScaleFactor
        {
            get { return Selected ? _selectedScale : _idleScale; }
        }

        /// <summary>고른 동안에는 호버 몫을 <b>덮는다</b> [소스 — <c>scale.set(선택 ? 1.08 : 1)</c>].</summary>
        public bool ExclusiveActive
        {
            get { return Selected; }
        }

        public void Configure(float selectedScale, float idleScale, float selectedAlpha, float idleAlpha, Graphic target = null)
        {
            _selectedScale = selectedScale;
            _idleScale = idleScale;
            _selectedAlpha = selectedAlpha;
            _idleAlpha = idleAlpha;
            _target = target;
        }

        public void SetSelected(bool selected)
        {
            Selected = selected;
            ApplyAlpha();
        }

        private void OnEnable()
        {
            ApplyAlpha();
        }

        private void ApplyAlpha()
        {
            if (_target == null)
                _target = GetComponent<Graphic>();

            if (_target == null)
                return;

            Color color = _target.color;
            color.a = Selected ? _selectedAlpha : _idleAlpha;
            _target.color = color;
        }
    }
}
