using System.Collections.Generic;
using System.Threading.Tasks;
using JinHyung.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace JinHyung.Data
{
    /// <summary>
    /// 아트를 어드레서블로 읽는다.
    ///
    /// <para>
    /// ★ <b>어드레서블 접점은 여기와 <see cref="DataManager"/> 둘뿐</b>이어야 한다.
    /// 게임 코드는 「어디서 왔는지」를 몰라야 로드 방식을 바꿀 수 있다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>UI 프리팹은 여기로 오지 않는다.</b> UI 프리팹만 <c>Resources</c> 다
    /// (확정표 10-c). 그건 <c>WindowKey</c> · <c>PrefabAuto</c> 가 든다.
    /// </para>
    /// </summary>
    public static class ArtLoader
    {
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(16);

        private static readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>(8);

        public static async Task<Sprite> LoadSpriteAsync(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                Log.Error("스프라이트 주소가 비었다.");
                return null;
            }

            if (_spriteCache.TryGetValue(address, out Sprite cached))
                return cached;

            AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(address);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                // ⚠ 폴백 도형을 만들지 않는다. 없으면 오류다.
                Log.Error($"스프라이트를 못 읽었다: {address}");
                return null;
            }

            _spriteCache[address] = handle.Result;
            return handle.Result;
        }

        /// <summary>
        /// 여러 장을 <b>한꺼번에</b> 읽는다 — 핸들을 전부 «먼저» 띄우고 한 번에 기다린다.
        ///
        /// <para>
        /// ⚠ 한 장씩 <c>await</c> 하면 «장수 × (한 번 왕복)» 이 된다. 시트 컷을 다 합치면 수백 장이라
        /// 에디터에서 첫 화면이 몇 초씩 늦게 뜬다 [사고 — 회차 11]. 여기서는 핸들이 «동시에» 돈다.
        /// </para>
        /// </summary>
        public static async Task<Sprite[]> LoadSpritesAsync(IReadOnlyList<string> addresses)
        {
            if (addresses == null)
                return System.Array.Empty<Sprite>();

            var result = new Sprite[addresses.Count];
            var pendingIndex = new List<int>(addresses.Count);
            var pendingHandle = new List<AsyncOperationHandle<Sprite>>(addresses.Count);

            for (int i = 0; i < addresses.Count; i++)
            {
                string address = addresses[i];

                if (string.IsNullOrEmpty(address))
                {
                    Log.Error("스프라이트 주소가 비었다.");
                    continue;
                }

                if (_spriteCache.TryGetValue(address, out Sprite cached))
                {
                    result[i] = cached;
                    continue;
                }

                pendingIndex.Add(i);
                pendingHandle.Add(Addressables.LoadAssetAsync<Sprite>(address));
            }

            var tasks = new Task[pendingHandle.Count];

            for (int i = 0; i < pendingHandle.Count; i++)
                tasks[i] = pendingHandle[i].Task;

            await Task.WhenAll(tasks);

            for (int i = 0; i < pendingHandle.Count; i++)
            {
                AsyncOperationHandle<Sprite> handle = pendingHandle[i];
                string address = addresses[pendingIndex[i]];

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Log.Error($"스프라이트를 못 읽었다: {address}");
                    continue;
                }

                _spriteCache[address] = handle.Result;
                result[pendingIndex[i]] = handle.Result;
            }

            return result;
        }

        /// <summary>
        /// 월드 <b>프리팹</b>을 읽는다. UI 프리팹은 여기로 오지 않는다 — 그건 <c>Resources</c> 다 (확정표 10-c).
        ///
        /// <para>
        /// ⚠ <b>씬에 프리팹을 «박아 두지» 않는다.</b> 씬이 직접 참조하면 그 프리팹이
        /// <b>어드레서블 번들과 씬 양쪽에 실린다</b> — 로드/해제 단위가 무너진다 (ResourceRule).
        /// </para>
        /// </summary>
        public static async Task<GameObject> LoadPrefabAsync(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                Log.Error("프리팹 주소가 비었다.");
                return null;
            }

            if (_prefabCache.TryGetValue(address, out GameObject cached))
                return cached;

            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(address);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                // ⚠ 폴백 프리팹을 만들지 않는다. 없으면 오류다 — 대체가 서면 「돌아가는데 원본이 아닌」 상태가 된다.
                Log.Error($"프리팹을 못 읽었다: {address}");
                return null;
            }

            _prefabCache[address] = handle.Result;
            return handle.Result;
        }

        /// <summary>이미 읽어 둔 프리팹. 동기 주입점(<c>BlumgiPrefabLoader</c> 같은)이 이걸 쓴다.</summary>
        public static GameObject FindLoadedPrefab(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;

            _prefabCache.TryGetValue(address, out GameObject prefab);
            return prefab;
        }

        public static void Clear()
        {
            _spriteCache.Clear();
            _prefabCache.Clear();
        }
    }
}
