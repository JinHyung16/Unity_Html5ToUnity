using JinHyung.Core;
using JinHyung.UI;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 씬에서 <b>유일하게 <c>Awake</c> 를 가진 것</b>. 초기화 순서를 한 곳에 못 박는다.
    ///
    /// <para>
    /// 순서: UI 루트 → 데이터(단위 1) → 맵(단위 2) → 매니저 → 월드 뷰(단위 6) → 창(단위 7·15) → 첫 화면.
    /// 지금은 단위 0 — 토대만 선다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsGameInitialize : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera _gameCamera;
        [SerializeField] private Camera _uiCamera;

        [Header("Root")]
        [SerializeField] private Transform _uiRoot;
        [SerializeField] private Transform _managerRoot;
        [SerializeField] private Transform _managementRoot;
        [SerializeField] private Transform _worldRoot;

        [Header("View")]
        [SerializeField] private FrontWarsWorldView _worldView;
        [SerializeField] private FrontWarsManagement _management;

        /// <summary>초기화가 끝났나. 재생 검사가 기다릴 지점이다.</summary>
        public bool IsReady { get; private set; }

        public Camera GameCamera
        {
            get { return _gameCamera; }
        }

        public Transform WorldRoot
        {
            get { return _worldRoot; }
        }

        private readonly System.Threading.CancellationTokenSource _cts = new System.Threading.CancellationTokenSource();

        private async void Awake()
        {
            // ① UI 루트 · 카메라
            WindowManagement.Instance.BindEnvironment(_uiRoot, _uiCamera);

            // ② 데이터 — 등록 목록은 FrontWarsContainerRegister 하나다.
            //    ⚠ 매니저보다 먼저다 — 데이터 없이 매니저가 서면 폴백값으로 시작하고 에러도 안 난다.
            JinHyung.Data.FrontWarsContainerRegister.RegisterAll();
            await JinHyung.Data.DataManager.Instance.InitializeAsync(_cts.Token);

            if (_cts.IsCancellationRequested)
                return;

            // ③ 매니저
            FrontWarsGameRoot root = FrontWarsGameRoot.Instance;

            if (_managerRoot != null)
                root.transform.SetParent(_managerRoot, false);

            root.Bootstrap();

            // ④ 월드 뷰 — 판이 시작되면 지도 · 유닛 · 이름 · 카메라 · 입력이 붙는다
            if (_worldView == null || _management == null)
            {
                Log.Error("[FrontWarsGameInitialize] 월드 뷰 · 창 조종이 안 물렸다 — FrontWarsSceneBuilder.AddWorldAndManagement 를 돌린다");
                return;
            }

            _worldView.Bind(root.Game);

            // ⑤ 창 — 화면 전이를 구독한다
            _management.Bind(root, _worldView);
            _management.Initialize();

            // ⑥ 첫 화면 — 메뉴. 튜토리얼은 메뉴의 버튼으로 연다.
            root.GameFlow.ChangeScreen(EFrontWarsScreenType.Menu);

            IsReady = true;
            Log.Success("FrontWars-io 초기화 완료");
        }

        private void OnDestroy()
        {
            if (_management != null)
                _management.Dispose();

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
