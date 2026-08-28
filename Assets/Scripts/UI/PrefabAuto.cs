using JinHyung.Core;
using UnityEngine;

namespace JinHyung.UI
{
    /// <summary>
    /// UI 컴포넌트 프리팹을 <c>Resources</c> 에서 한 번만 로드해 캐시하고, 부를 때마다 만든다.
    ///
    /// <para>
    /// 컴포넌트 클래스에 <c>static readonly</c> 로 하나 선언해 두고 그것만 쓴다.
    /// </para>
    ///
    /// <code>
    /// public class CellComponent : BaseComponent
    /// {
    ///     public static readonly PrefabAuto&lt;CellComponent&gt; Auto =
    ///         new PrefabAuto&lt;CellComponent&gt;("UI/Component/CellComponent");
    /// }
    /// </code>
    ///
    /// <para>
    /// ⚠ <b>풀링이 없다 — 최소 세트다.</b>
    /// 고정 격자는 시작할 때 전 칸을 만들고 끝까지 들고 있으므로 풀이 필요 없다.
    /// <b>재활용이 일어나는 화면</b>(스크롤 목록)이 오면 그때 반환·리셋을 붙인다 —
    /// 그때는 <c>OnDespawn</c> 에서 <b>구독 해제와 상태 리셋</b>이 같이 와야 한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>리스트성 UI 를 인스펙터에 미리 박지 않는다.</b> 개수가 데이터로 갈리는데
    /// 박아 두면 원본과 다른 수가 고정되고, 그 사실이 어떤 검증에도 안 걸린다.
    /// </para>
    /// </summary>
    public sealed class PrefabAuto<T>
        where T : BaseComponent
    {
        private readonly string _resourcePath;
        private T _prefab;

        public PrefabAuto(string resourcePath)
        {
            _resourcePath = resourcePath;
        }

        /// <summary>UI 계층에 붙여 만든다. 부모 기준으로 자리를 리셋한다.</summary>
        public T CreateForUI(Transform parent)
        {
            if (parent == null)
            {
                Log.Error($"부모가 null 이다: {_resourcePath}");
                return null;
            }

            var prefab = LoadPrefab();

            if (prefab == null)
                return null;

            var instance = Object.Instantiate(prefab, parent, false);
            var rect = instance.CachedRectTransform;

            if (rect != null)
            {
                rect.anchoredPosition3D = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }

            return instance;
        }

        private T LoadPrefab()
        {
            if (_prefab != null)
                return _prefab;

            _prefab = Resources.Load<T>(_resourcePath);

            if (_prefab == null)
            {
                // ⚠ 폴백 도형을 만들지 않는다. 없으면 오류다.
                Log.Error($"프리팹이 없다: Resources/{_resourcePath}");
            }

            return _prefab;
        }
    }
}
