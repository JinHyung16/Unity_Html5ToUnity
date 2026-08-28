using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JinHyung.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace JinHyung.Data
{
    /// <summary>
    /// 컨테이너를 <b>등록 · 로드 · 검증</b>한다.
    ///
    /// <para>
    /// ⚠ <b>리플렉션 스캔을 쓰지 않는다.</b> 등록 목록을 사람이 들고 있어야
    /// 「무엇이 로드되는가」가 파일 하나로 보인다. 스캔은 안 쓰는 컨테이너까지 만들고
    /// 로드 순서를 숨긴다.
    /// </para>
    /// </summary>
    public sealed class DataManager
    {
        /// <summary><c>Assets/GameData</c> 의 JSON 에 붙이는 어드레서블 라벨.</summary>
        public const string GameDataLabel = "game_data";

        private static DataManager _instance;

        public static DataManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new DataManager();

                return _instance;
            }
        }

        public bool IsInitialized { get; private set; }

        private readonly Dictionary<Type, IDataContainer> _containers = new Dictionary<Type, IDataContainer>(8);

        private DataManager()
        {
        }

        public void Register<T>(T container)
            where T : class, IDataContainer
        {
            if (container == null)
            {
                Log.Error("null 컨테이너를 등록하려 했다.");
                return;
            }

            _containers[typeof(T)] = container;
        }

        public T GetContainer<T>()
            where T : class, IDataContainer
        {
            if (_containers.TryGetValue(typeof(T), out var container))
                return container as T;

            // ⚠ 폴백을 주지 않는다. 등록을 빠뜨린 것이 조용히 「빈 데이터」로 둔갑하면
            //    화면은 그럴듯한데 값만 비는 상태가 된다.
            Log.Error($"등록되지 않은 컨테이너다: {typeof(T).Name}");
            return null;
        }

        /// <summary>런타임 경로. 라벨 하나로 전 JSON 을 읽는다.</summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitialized)
                return;

            var jsonByName = await LoadJsonTextByNameAsync(cancellationToken);
            LoadAll(jsonByName);
        }

        /// <summary>
        /// ★ <b>하네스·에디터 전용 우회 경로.</b> 어드레서블도 재생도 없이 텍스트를 직접 넣는다.
        ///
        /// <para>
        /// <b>이걸 반드시 같이 만든다.</b> 재생을 눌러야만 도는 데이터는
        /// 골든 대조·헤드리스 검증에서 못 쓴다 — 그러면 그 계통은 한 번도 수치로 판정되지 않는다.
        /// </para>
        /// </summary>
        public void InitializeFromJson(IReadOnlyDictionary<string, string> jsonByName)
        {
            IsInitialized = false;
            LoadAll(jsonByName);
        }

        public void Clear()
        {
            foreach (var container in _containers.Values)
                container.Clear();

            IsInitialized = false;
        }

        private void LoadAll(IReadOnlyDictionary<string, string> jsonByName)
        {
            if (jsonByName == null)
            {
                Log.Error("JSON 텍스트 맵이 null 이다.");
                return;
            }

            foreach (var container in _containers.Values)
            {
                if (jsonByName.TryGetValue(container.Name, out var text) == false)
                {
                    Log.Error($"JSON 이 없다: {container.Name}");
                    continue;
                }

                container.LoadJson(text);
            }

            ValidateAll();
            AfterAllLoaded();

            IsInitialized = true;
        }

        private void ValidateAll()
        {
            foreach (var container in _containers.Values)
            {
                if (container.Validate(out var error) == false)
                    Log.Error($"'{container.Name}' 검증 실패:{Environment.NewLine}{error}");
            }
        }

        private void AfterAllLoaded()
        {
            foreach (var container in _containers.Values)
                container.AfterAllTableLoaded();
        }

        /// <summary>
        /// ★ <b>로드 출처를 부르는 유일한 곳.</b> 어드레서블에서 Resources 로 바꾸려면
        /// 이 메서드 하나만 고친다 — 컨테이너는 어디서 왔는지 몰라야 한다.
        /// </summary>
        private async Task<Dictionary<string, string>> LoadJsonTextByNameAsync(CancellationToken cancellationToken)
        {
            var map = new Dictionary<string, string>(8);

            var handle = Addressables.LoadAssetsAsync<TextAsset>(
                GameDataLabel,
                asset =>
                {
                    if (asset != null)
                        map[asset.name] = asset.text;
                });

            await handle.Task;
            cancellationToken.ThrowIfCancellationRequested();

            if (handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                Log.Error($"어드레서블 라벨 로드 실패: {GameDataLabel}");

            Addressables.Release(handle);
            return map;
        }
    }
}
