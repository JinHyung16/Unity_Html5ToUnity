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

        /// <summary>주소 목록을 순서대로 읽는다. 실패한 자리는 <c>null</c> 이고 <b>폴백을 만들지 않는다.</b></summary>
        public static async Task<Sprite[]> LoadSpritesAsync(IReadOnlyList<string> addresses)
        {
            if (addresses == null)
                return System.Array.Empty<Sprite>();

            var result = new Sprite[addresses.Count];

            for (int i = 0; i < addresses.Count; i++)
                result[i] = await LoadSpriteAsync(addresses[i]);

            return result;
        }

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

        public static void Clear()
        {
            _spriteCache.Clear();
        }
    }
}
