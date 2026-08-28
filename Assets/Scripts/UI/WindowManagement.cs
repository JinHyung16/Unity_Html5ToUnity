using System.Collections.Generic;
using JinHyung.Core;
using UnityEngine;

namespace JinHyung.UI
{
    /// <summary>
    /// 창을 만들고 캐시하고 겹침 순서를 든다. UI 루트·카메라의 소유자다.
    ///
    /// <para>
    /// 창은 <b>한 번 만들면 파괴하지 않고 캐시</b>한다 — 원본이 매번 DOM 을 다시 그리더라도
    /// 우리는 유지 모드다. 그래서 <b>닫을 때 상태를 되돌리는 책임</b>이 창에게 있다
    /// (<see cref="BaseWindow.OnClosing"/>).
    /// </para>
    ///
    /// <para>
    /// ★ <b>겹침은 밴드별 전용 부모로 고정한다.</b> 한 부모에 섞어 만들면
    /// 만드는 순서가 그대로 형제 순서가 되어 「선이 노드 위로」 같은 결함이 난다.
    /// </para>
    /// </summary>
    public class WindowManagement : MonoSingleton<WindowManagement>
    {
        private readonly Dictionary<string, BaseWindow> _windows = new Dictionary<string, BaseWindow>(16);
        private readonly Dictionary<EWindowType, Transform> _bandRoots = new Dictionary<EWindowType, Transform>(4);

        private Transform _uiRoot;
        private Camera _uiCamera;

        /// <summary>ScreenSpace-Camera 캔버스가 물고 있는 카메라.</summary>
        public Camera UICamera
        {
            get { return _uiCamera; }
        }

        /// <summary>
        /// UI 루트와 카메라를 물린다. <c>GameInitialize</c> 가 <b>가장 먼저</b> 부른다 —
        /// 이게 안 되면 창을 만들 자리가 없다.
        /// </summary>
        public void BindEnvironment(Transform uiRoot, Camera uiCamera)
        {
            if (uiRoot == null)
            {
                Log.Error("UI 루트가 null 이다. 창을 만들 자리가 없다.");
                return;
            }

            _uiRoot = uiRoot;
            _uiCamera = uiCamera;
            _bandRoots.Clear();
        }

        /// <summary>창을 준다. 없으면 만들어서 준다. <b>만든 직후는 닫힌 상태다.</b></summary>
        public T GetWindow<T>(WindowKey<T> key)
            where T : BaseWindow
        {
            if (key == null)
            {
                Log.Error("WindowKey 가 null 이다.");
                return null;
            }

            if (_windows.TryGetValue(key.Path, out var cached))
                return cached as T;

            return Create(key);
        }

        /// <summary>
        /// <b>이미 만들어진 창만</b> 찾는다. 없으면 <c>null</c> 이고 <b>만들지 않는다.</b>
        /// 「닫기」처럼 없으면 할 일도 없는 경우에 쓴다 — 닫으려고 창을 만들면 안 된다.
        /// </summary>
        public BaseWindow FindCreated(IWindowKey key)
        {
            if (key == null)
                return null;

            _windows.TryGetValue(key.Path, out var window);
            return window;
        }

        /// <summary>같은 밴드 안에서 맨 앞으로 보낸다. <b>여는 순서가 곧 위아래다.</b></summary>
        public void BringToFront(BaseWindow window)
        {
            if (window == null)
                return;

            window.CachedTransform.SetAsLastSibling();
        }

        private T Create<T>(WindowKey<T> key)
            where T : BaseWindow
        {
            if (_uiRoot == null)
            {
                Log.Error("UI 루트가 안 물렸다. BindEnvironment 를 먼저 부른다.");
                return null;
            }

            var prefab = Resources.Load<GameObject>(key.Path);

            if (prefab == null)
            {
                // ⚠ 폴백을 깔지 않는다. 없으면 오류다 — 폴백은 어서트를 무력화한다.
                Log.Error($"창 프리팹이 없다: Resources/{key.Path}");
                return null;
            }

            var component = prefab.GetComponent<T>();

            if (component == null)
            {
                Log.Error($"프리팹에 {typeof(T).Name} 컴포넌트가 없다: Resources/{key.Path}");
                return null;
            }

            // 밴드 부모 아래에 바로 만든다 — 만든 뒤 옮기면 그 사이에 한 프레임이 스친다.
            var instance = Instantiate(component, GetBandRoot(component.WindowType), false);
            instance.name = typeof(T).Name;
            instance.SetEnable(false);

            _windows.Add(key.Path, instance);
            return instance;
        }

        /// <summary>밴드마다 전용 부모를 만든다. 형제 순서가 곧 밴드 순서다.</summary>
        private Transform GetBandRoot(EWindowType windowType)
        {
            if (_bandRoots.TryGetValue(windowType, out var cached) && cached != null)
                return cached;

            var go = new GameObject(windowType.ToString());
            var rect = go.AddComponent<RectTransform>();

            rect.SetParent(_uiRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // enum 값이 곧 순서다. 낮은 밴드가 먼저 오면 높은 밴드가 뒤로 밀리므로
            // 어느 순서로 만들어져도 결과는 같다.
            rect.SetSiblingIndex((int)windowType);

            _bandRoots[windowType] = rect;
            return rect;
        }

        protected override void OnDestroy()
        {
            _windows.Clear();
            _bandRoots.Clear();
            _uiRoot = null;
            _uiCamera = null;

            base.OnDestroy();
        }
    }
}
