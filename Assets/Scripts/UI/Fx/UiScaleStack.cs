using System.Collections.Generic;
using UnityEngine;

namespace JinHyung.UI.Fx
{
    /// <summary>
    /// 같은 오브젝트에 붙은 <see cref="IUiScaleFactor"/> 전부를 곱해 <c>localScale</c> 에 넣는다.
    ///
    /// <para>
    /// 펄스·호버·상태가 각자 <c>localScale</c> 을 쓰면 마지막에 쓴 것이 이긴다.
    /// 그래서 «몫»만 내고 여기서 한 번에 곱한다 — 원본 UI 도 <c>hover × pulse</c> 를 곱해서 한 번 넣는다.
    /// </para>
    ///
    /// <para>몫을 내는 컴포넌트가 스스로 이걸 붙인다(<c>RequireComponent</c>). 사람이 따로 붙일 일은 없다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiScaleStack : MonoBehaviour
    {
        [SerializeField] private Vector3 _baseScale = Vector3.one;

        private readonly List<IUiScaleFactor> _factors = new List<IUiScaleFactor>(4);
        private bool _collected;

        /// <summary>곱하기 전의 기준 배율. 프리팹이 든다.</summary>
        public Vector3 BaseScale
        {
            get { return _baseScale; }
            set { _baseScale = value; }
        }

        /// <summary>지금 곱해진 결과 — 검사가 «숨쉬는지» 잰다.</summary>
        public float CurrentFactor { get; private set; } = 1f;

        private void OnEnable()
        {
            _collected = false;
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        /// <summary>컴포넌트가 늦게 붙었을 때 다시 모은다.</summary>
        public void Invalidate()
        {
            _collected = false;
        }

        public void Apply()
        {
            if (_collected == false)
            {
                GetComponents(_factors);
                _collected = true;
            }

            float factor = 1f;

            // ★ «전용» 몫이 켜져 있으면 그것만 쓴다 — 원본의 상태형 배율(선택 카드)은 곱이 아니라 덮어쓰기다.
            for (int i = 0; i < _factors.Count; i++)
            {
                var behaviour = _factors[i] as Behaviour;

                if (behaviour != null && behaviour.enabled == false)
                    continue;

                if (_factors[i] is IUiScaleExclusive exclusive && exclusive.ExclusiveActive)
                {
                    CurrentFactor = exclusive.ScaleFactor;
                    transform.localScale = _baseScale * CurrentFactor;
                    return;
                }
            }

            for (int i = 0; i < _factors.Count; i++)
            {
                var behaviour = _factors[i] as Behaviour;

                if (behaviour != null && behaviour.enabled == false)
                    continue;

                factor *= _factors[i].ScaleFactor;
            }

            CurrentFactor = factor;
            transform.localScale = _baseScale * factor;
        }
    }
}
