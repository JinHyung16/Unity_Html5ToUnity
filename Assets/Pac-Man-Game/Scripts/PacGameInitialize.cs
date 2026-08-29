using System.Threading;
using System.Threading.Tasks;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using UnityEngine;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 씬에서 <b>유일하게 <c>Awake</c> 를 가진 것</b>. 초기화 순서를 한 곳에서 못 박는다.
    ///
    /// <para>
    /// ⚠ 순서가 사양이다 — UI 루트 → 데이터 → 매니저 → 창 조종자 → 뷰 → 첫 화면.
    /// 데이터보다 매니저가 먼저 서면 <b>폴백값으로 초기화</b>되고, 그건 에러도 안 난다.
    /// </para>
    /// </summary>
    public class PacGameInitialize : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera _uiCamera;

        [Header("Root")]
        [SerializeField] private Transform _uiRoot;
        [SerializeField] private Transform _managerRoot;
        [SerializeField] private Transform _managementRoot;

        [Header("View")]
        [SerializeField] private MazeView _mazeView;
        [SerializeField] private PacAudio _audio;

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

            // ② 데이터. 등록 목록은 사람이 들고 있는 PacContainerRegister 하나다.
            PacContainerRegister.RegisterAll();
            await DataManager.Instance.InitializeAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            // ③ 로직. 데이터가 선 뒤여야 한다.
            PacGameManager game = PacGameManager.Instance;

            if (_managerRoot != null)
                game.transform.SetParent(_managerRoot, false);

            game.Bootstrap();
            game.Restart();

            // ④ 창 조종자. 매니저를 곧바로 읽으므로 ③ 뒤여야 한다.
            var management = AddManagement<PacManagement>();
            management.BindAudio(_audio);
            management.Initialize();

            // ⑤ 뷰. 매니저가 선 뒤에 물린다.
            if (_mazeView != null)
                _mazeView.Bind(game.MazeMgr, game.PacMgr, game.GhostMgr);

            // ⑥ 원본에는 화면 전이가 «없다» — 로드되면 바로 게임이다.
            game.GameFlow.ChangeScreen(EPacScreenType.Game);

            Log.Success("Pac-Man 초기화 완료");
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
