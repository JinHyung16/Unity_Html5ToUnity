using System.Threading;
using System.Threading.Tasks;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using UnityEngine;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// ★ <b>이 프로젝트에서 <c>Awake</c> 를 쓰는 유일한 파일.</b>
    ///
    /// <para>
    /// Manager·Management 의 생성·초기화 순서를 여기 한 곳이 정한다.
    /// <c>Awake</c> 에 맡기면 순서가 유니티 손에 넘어가 <b>회차마다 다른 버그</b>가 난다.
    /// </para>
    ///
    /// <para>
    /// <b>순서가 규약이다</b> — 데이터 → Manager → Management → 아트 → 첫 화면.
    /// 데이터보다 Manager 가 먼저 서면 매니저가 폴백값으로 초기화되고,
    /// Manager 보다 Management 가 먼저 서면 <c>Initialize</c> 에서 매니저를 읽다 터진다.
    /// </para>
    /// </summary>
    public class GameInitialize : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera _uiCamera;

        [Header("Root")]
        [SerializeField] private Transform _uiRoot;
        [SerializeField] private Transform _managerRoot;
        [SerializeField] private Transform _managementRoot;

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
            // ① UI 루트·카메라를 물린다. 이게 안 되면 창을 만들 자리가 없다.
            WindowManagement.Instance.BindEnvironment(_uiRoot, _uiCamera);

            // ② 데이터. 등록 목록은 사람이 들고 있는 ContainerRegister 하나다.
            ContainerRegister.RegisterAll();
            await DataManager.Instance.InitializeAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            // ③ 로직 매니저. 데이터가 선 뒤여야 폴백값으로 초기화되지 않는다.
            CandyGameManager game = CandyGameManager.Instance;

            if (_managerRoot != null)
                game.transform.SetParent(_managerRoot, false);

            game.Bootstrap();

            // ④ 창 조종자. 매니저를 곧바로 읽으므로 ③ 뒤여야 한다.
            AddManagement<LobbyManagement>();
            GameManagement gameManagement = AddManagement<GameManagement>();

            // ⑤ 아트. 창이 선 뒤에 주입한다.
            await gameManagement.LoadArtAsync();

            if (cancellationToken.IsCancellationRequested)
                return;

            // ⑥ 첫 화면. 원본도 모드 선택이 먼저다 (index.html 에서 유일하게 켜져 있는 화면).
            game.GameFlow.ChangeScreen(EGameScreenType.ModeSelect);

            Log.Success("게임 초기화 완료");
        }

        /// <summary>⚠ <c>Awake</c> 가 아니라 <b>여기서 명시적으로</b> 초기화한다.</summary>
        private T AddManagement<T>() where T : BaseManagement
        {
            var go = new GameObject(typeof(T).Name);

            if (_managementRoot != null)
                go.transform.SetParent(_managementRoot, false);

            T management = go.AddComponent<T>();
            management.Initialize();
            return management;
        }
    }
}
