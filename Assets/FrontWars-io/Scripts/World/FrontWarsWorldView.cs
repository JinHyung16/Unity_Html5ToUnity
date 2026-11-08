using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 월드 쪽 묶음 — 지도 그리기 · 유닛 · 이름 · 카메라 · 입력을 판에 붙이고 틱을 나눠 준다.
    /// <para>판이 시작되면 붙고, 끝나면 뗀다. 그리기는 시뮬 틱마다 바뀐 칸만 올린다 (<see cref="FrontWarsMapRenderer.OnTick"/>).</para>
    /// </summary>
    public sealed class FrontWarsWorldView : MonoBehaviour
    {
        [SerializeField] private FrontWarsMapRenderer _mapRenderer;
        [SerializeField] private FrontWarsUnitView _unitView;
        [SerializeField] private FrontWarsNameView _nameView;
        [SerializeField] private FrontWarsFxView _fxView;
        [SerializeField] private FrontWarsCameraController _cameraController;
        [SerializeField] private FrontWarsMapInput _input;
        [SerializeField] private FrontWarsPerfOverlay _perfOverlay;
        [SerializeField] private FrontWarsRailView _railView;
        [SerializeField] private Camera _gameCamera;

        private FrontWarsGameManager _manager;

        public FrontWarsMapInput Input => _input;
        public FrontWarsCameraController CameraController => _cameraController;
        public FrontWarsMapRenderer MapRenderer => _mapRenderer;
        public FrontWarsUnitView UnitView => _unitView;
        public FrontWarsNameView NameView => _nameView;
        public FrontWarsFxView FxView => _fxView;

        /// <summary>성능 표시 — 씬에 안 붙였으면 null 이다 (설정 항목은 그냥 안 먹는다).</summary>
        public FrontWarsPerfOverlay PerfOverlay => _perfOverlay;
        public FrontWarsRailView RailView => _railView;

        public void Bind(FrontWarsGameManager manager)
        {
            _manager = manager;
            _manager.OnGameStarted += HandleGameStarted;
            _manager.OnGameStopped += HandleGameStopped;
            _manager.OnTick += HandleTick;

            if (_input != null)
                _input.OnAlternateView += HandleAlternateView;
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.OnGameStarted -= HandleGameStarted;
                _manager.OnGameStopped -= HandleGameStopped;
                _manager.OnTick -= HandleTick;
            }

            if (_input != null)
                _input.OnAlternateView -= HandleAlternateView;
        }

        private void HandleGameStarted(FrontWarsGame game)
        {
            FrontWarsPlayer me = _manager.LocalPlayer;

            _mapRenderer.SetCamera(_gameCamera);
            _mapRenderer.Bind(game, me);
            _cameraController.Bind(_gameCamera, game.Map);
            _unitView.Bind(game, _mapRenderer);
            _nameView.LocalPlayer = () => _manager.LocalPlayer;
            _nameView.Bind(game, _cameraController);
            _input.Bind(_manager, _cameraController);

            if (_fxView != null)
                _fxView.Bind(game, _mapRenderer, () => _manager.LocalPlayer);

            if (_railView != null)
                _railView.Bind(game, _mapRenderer.Colors, _cameraController, () => _manager.LocalPlayer);

            if (_perfOverlay != null)
                _perfOverlay.Bind(game, _manager, _cameraController, _railView);

            gameObject.SetActive(true);
        }

        private void HandleGameStopped()
        {
            _mapRenderer.Unbind();
            _unitView.Unbind();
            _nameView.Unbind();

            if (_fxView != null)
                _fxView.Unbind();

            if (_railView != null)
                _railView.Unbind();

            if (_perfOverlay != null)
                _perfOverlay.Unbind();
        }

        private void HandleTick(int tick)
        {
            _mapRenderer.OnTick();
            _nameView.OnTick(tick);
        }

        private void HandleAlternateView(bool on)
        {
            _mapRenderer.SetTacticalView(on);
        }
    }
}
