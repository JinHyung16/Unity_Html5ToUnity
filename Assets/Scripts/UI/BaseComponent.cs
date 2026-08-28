using UnityEngine;

namespace JinHyung.UI
{
    /// <summary>
    /// UI 계층의 공통 베이스. <c>Transform</c>·<c>RectTransform</c>·<c>GameObject</c> 를 캐시한다.
    ///
    /// <para>
    /// 유니티의 <c>transform</c>·<c>gameObject</c> 접근은 공짜가 아니다.
    /// 격자처럼 개체가 수십~수백 개 도는 화면에서는 이 캐시가 그대로 비용 차이가 된다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>이 클래스는 데이터를 들지 않는다.</b> UI 는 받은 것을 표기만 한다 —
    /// 화면이 상태를 들기 시작하면 원본 데이터와 화면이 갈리고 아무도 진실을 모른다.
    /// </para>
    /// </summary>
    public class BaseComponent : MonoBehaviour
    {
        private Transform _cachedTransform;
        private RectTransform _cachedRectTransform;
        private GameObject _cachedGameObject;

        public Transform CachedTransform
        {
            get
            {
                if (_cachedTransform == null)
                    _cachedTransform = transform;

                return _cachedTransform;
            }
        }

        /// <summary>UI 가 아닌 오브젝트에 붙으면 <c>null</c> 이다.</summary>
        public RectTransform CachedRectTransform
        {
            get
            {
                if (_cachedRectTransform == null)
                    _cachedRectTransform = transform as RectTransform;

                return _cachedRectTransform;
            }
        }

        public GameObject CachedGameObject
        {
            get
            {
                if (_cachedGameObject == null)
                    _cachedGameObject = gameObject;

                return _cachedGameObject;
            }
        }

        public bool IsEnabled
        {
            get { return CachedGameObject.activeSelf; }
        }

        public void SetEnable(bool enable)
        {
            var go = CachedGameObject;

            if (go.activeSelf == enable)
                return;

            go.SetActive(enable);
        }
    }
}
