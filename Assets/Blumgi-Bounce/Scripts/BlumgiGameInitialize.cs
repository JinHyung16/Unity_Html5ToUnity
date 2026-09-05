using System.Threading;
using System.Threading.Tasks;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 씬에서 <b>유일하게 <c>Awake</c> 를 가진 것</b>. 초기화 «순서»를 한 곳에 못 박는다.
    ///
    /// <para>
    /// ⚠ <b>순서가 사양이다</b> — UI 루트 → 데이터 → 프리팹 → 매니저 → 월드 → 창 조종자 → 첫 화면.
    /// 데이터보다 매니저가 먼저 서면 <b>폴백값으로 초기화</b>되고, 그건 에러도 안 난다.
    /// </para>
    ///
    /// <para>
    /// ★ <b><c>BlumgiContainerRegister.RegisterAll()</c> 의 유일한 게임 호출부가 여기다.</b>
    /// 패스 ① 이 「호출부가 아직 없다」로 남긴 자리다 — 등록하지 않으면 JSON 이 있어도 아무도 안 읽고,
    /// <b>그런데 에러도 안 난다.</b>
    /// </para>
    /// </summary>
    public sealed class BlumgiGameInitialize : MonoBehaviour
    {
        /// <summary>월드 프리팹 주소 전수. 이름 = 파일명 (어드레서블 규약).</summary>
        private static readonly string[] WorldPrefabAddresses =
        {
            BlumgiPhysicsWorld.BlockPrefabName,
            BlumgiPhysicsWorld.BallPrefabName,
            BlumgiPhysicsWorld.BlobPrefabName,
            BlumgiPhysicsWorld.HoopPrefabName,
            // ★ 머리 위 공 — «물리가 없는 그림»이라 BlumgiPhysicsWorld 가 세우지 않는다 (패스 ②-c).
            BlumgiRestBallView.PrefabName,
            BackgroundPrefabName,
            TrailPrefabName,
            ConfettiPrefabName,
            // ★ 골인 빛줄기·텔레포트 — 컨페티와 달리 «골대»에 붙는다 (패스 ②-h).
            GoalBurstPrefabName,
        };

        public const string BackgroundPrefabName = "BlumgiLevelBackground";
        public const string TrailPrefabName = "BlumgiBallTrail";
        public const string ConfettiPrefabName = "BlumgiConfetti";
        public const string GoalBurstPrefabName = "BlumgiGoalBurst";

        [Header("Camera")]
        [SerializeField] private Camera _gameCamera;
        [SerializeField] private Camera _uiCamera;

        [Header("Root")]
        [SerializeField] private Transform _uiRoot;
        [SerializeField] private Transform _managerRoot;
        [SerializeField] private Transform _managementRoot;
        [SerializeField] private Transform _worldRoot;

        [Header("World")]
        [SerializeField] private BlumgiPhysicsWorld _physicsWorld;
        [SerializeField] private BlumgiCameraDirector _cameraDirector;

        [Header("Input")]
        [SerializeField] private BlumgiHoldInput _holdInput;

        /// <summary>초기화가 끝났나. 재생 검사가 «기다릴» 지점이다 (게임 코드가 쓰는 상태 그대로다).</summary>
        public bool IsReady { get; private set; }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private async void Awake()
        {
            await InitAsync(_cts.Token);
        }

        private void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private async Task InitAsync(CancellationToken cancellationToken)
        {
            // ① UI 루트·카메라. 이게 안 되면 창을 만들 자리가 없다.
            WindowManagement.Instance.BindEnvironment(_uiRoot, _uiCamera);

            // ② 데이터. 등록 목록은 사람이 들고 있는 BlumgiContainerRegister 하나다.
            BlumgiContainerRegister.RegisterAll();
            await DataManager.Instance.InitializeAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            // ③ 월드 프리팹. ⚠ 레벨을 세우는 쪽이 «동기»라 미리 다 읽어 둔다.
            for (int i = 0; i < WorldPrefabAddresses.Length; i++)
                await ArtLoader.LoadPrefabAsync(WorldPrefabAddresses[i]);

            if (cancellationToken.IsCancellationRequested)
                return;

            // ④ 로직. 데이터가 선 뒤여야 한다.
            BlumgiGameRoot root = BlumgiGameRoot.Instance;

            if (_managerRoot != null)
                root.transform.SetParent(_managerRoot, false);

            root.Bootstrap();

            // ⚠ 주입은 Bootstrap «뒤»다 — 매니저 실체가 그때 생긴다.
            root.Game.PhysicsWorld = _physicsWorld;
            root.Game.PrefabLoader = ArtLoader.FindLoadedPrefab;

            // ⑤ 월드 — 배경(원근 1/3) · 트레일 · 컨페티.
            BlumgiLevelBackground background = BuildBackground();
            BlumgiBallTrailView trail = SpawnWorld<BlumgiBallTrailView>(TrailPrefabName, _worldRoot);
            BlumgiConfettiBurst confetti = SpawnWorld<BlumgiConfettiBurst>(ConfettiPrefabName, _worldRoot);
            BlumgiGoalBurst goalBurst = SpawnWorld<BlumgiGoalBurst>(GoalBurstPrefabName, _worldRoot);

            // ★ 머리 위 공 — 원본은 «별개 오브젝트»다. 물리 공은 경기장 밖에서 낙하하므로
            //   여기서 세우지 않으면 블롭 머리 위가 빈다 (패스 ①-d 가 넘긴 것).
            BlumgiRestBallView restBall = SpawnWorld<BlumgiRestBallView>(
                BlumgiRestBallView.PrefabName, _worldRoot);

            // ⑥ 뷰 배선. 매니저가 선 뒤에 물린다.
            var presenterGo = new GameObject(nameof(BlumgiLevelPresenter));
            presenterGo.transform.SetParent(_worldRoot, false);

            var presenter = presenterGo.AddComponent<BlumgiLevelPresenter>();
            presenter.Bind(root.Game, _physicsWorld, _cameraDirector, _gameCamera, background, trail, restBall);

            // ⑦ 창 조종자. 매니저를 곧바로 읽으므로 ④ 뒤여야 한다.
            var management = AddManagement<BlumgiManagement>();
            management.Bind(root, presenter, _holdInput, confetti, goalBurst);
            management.Initialize();

            await management.LoadUiArtAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            // ⑧ 첫 화면 — 원본은 로딩이 끝나면 WELCOME 이다 [실측 전이 그래프].
            root.GameFlow.ChangeScreen(EBlumgiScreenType.Welcome);

            IsReady = true;
            Log.Success("Blumgi Bounce 초기화 완료");
        }

        /// <summary>
        /// 배경 — <b>소실 중심에 피벗을 둔 부모에 <c>localScale = 1/3</c></b> (5회차 원근 규칙).
        /// 원근 카메라를 세우지 않는 이유는 BG 콘텐츠가 <b>한 z 평면</b>에 있어 상수 배율로 축약되기 때문이다.
        /// </summary>
        private BlumgiLevelBackground BuildBackground()
        {
            Transform backgroundRoot = BlumgiBackgroundLayout.CreateRoot(_worldRoot);
            BlumgiLevelBackground background = SpawnWorld<BlumgiLevelBackground>(BackgroundPrefabName, backgroundRoot);

            BlumgiBackgroundLayout.Apply(background);
            return background;
        }

        private static T SpawnWorld<T>(string address, Transform parent)
            where T : Component
        {
            GameObject prefab = ArtLoader.FindLoadedPrefab(address);

            if (prefab == null)
            {
                Log.Error($"월드 프리팹이 없다: {address}");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.name = address;
            instance.transform.localPosition = Vector3.zero;

            var component = instance.GetComponent<T>();

            if (component == null)
                Log.Error($"{address} 에 {typeof(T).Name} 이 없다 — 프리팹을 다시 굽는다");

            return component;
        }

        private T AddManagement<T>() where T : BaseManagement
        {
            var go = new GameObject(typeof(T).Name);

            if (_managementRoot != null)
                go.transform.SetParent(_managementRoot, false);

            return go.AddComponent<T>();
        }
    }
}
