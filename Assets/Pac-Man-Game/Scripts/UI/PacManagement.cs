using JinHyung.UI;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 이 게임의 창 조종자. <b>창이 하나뿐</b>이라 조종할 것도 하나다 —
    /// 원본에 화면 전이가 없기 때문이다 (<c>01_게임플로우.md</c>).
    ///
    /// <para>
    /// ⚠ <b>「나중에 쓸 것 같아서」 창을 늘리지 않는다.</b> 늘리는 순간
    /// 「원본에 없는 창」이 되고 플로우 diff 가 깨진다.
    /// </para>
    /// </summary>
    public class PacManagement : BaseManagement
    {
        /// <summary>
        /// 안내 문구. ⚠ 원본은 <c>Use arrow keys or WASD to move…</c> 로 <b>키보드를 전제</b>한다 —
        /// 입력을 스와이프로 바꿨으므로 문구도 바뀐다 (「의도된 차이」 등재).
        /// ⚠ <b>언어는 원본대로 영어다.</b> 바꾼 것은 «조작 방법»이지 언어가 아니다 —
        /// 게다가 폰트에 한글 글리프가 없어 한글로 쓰면 두부로 뜬다 (실측).
        /// </summary>
        private const string Hint = "Swipe to move. Collect all pellets, avoid ghosts!";

        private PacAudio _audio;

        /// <summary>원본 alert 문구 (script.js:272 · 331). ⚠ 🎉 는 폰트에 없어 뗐다 — 「의도된 차이」.</summary>
        private const string ClearMessage = "You cleared the maze!";
        private const string GameOverMessage = "Game Over!";

        protected override void AddWindows()
        {
            RegisterWindow(PacGameWindow.Key);
            RegisterWindow(PacAlertPopup.Key);
        }

        public void BindAudio(PacAudio audio)
        {
            _audio = audio;
        }

        protected override void OnInitialize()
        {
            PacGameManager game = PacGameManager.Instance;
            PacGameWindow window = GetWindow(PacGameWindow.Key);

            if (window == null)
                return;

            window.SetHint(Hint);
            window.OnRestartClicked += HandleRestart;
            window.OnDirection += HandleDirection;
            window.OnFirstInput += HandleFirstInput;

            game.MazeMgr.OnStateChanged += HandleStateChanged;
            game.MazeMgr.OnTileChanged += HandlePelletEaten;
            game.MazeMgr.OnGameEnded += HandleGameEnded;
            game.GhostMgr.OnCaught += HandleCaught;

            game.GameFlow.OnScreenChanged += HandleScreenChanged;
        }

        protected override void OnDispose()
        {
            PacGameWindow window = GetWindow(PacGameWindow.Key);

            if (window != null)
            {
                window.OnRestartClicked -= HandleRestart;
                window.OnDirection -= HandleDirection;
                window.OnFirstInput -= HandleFirstInput;
            }

            if (PacGameManager.HasInstance == false)
                return;

            PacGameManager game = PacGameManager.Instance;
            game.MazeMgr.OnStateChanged -= HandleStateChanged;
            game.MazeMgr.OnTileChanged -= HandlePelletEaten;
            game.MazeMgr.OnGameEnded -= HandleGameEnded;
            game.GhostMgr.OnCaught -= HandleCaught;
            game.GameFlow.OnScreenChanged -= HandleScreenChanged;
        }

        private void HandleScreenChanged(EPacScreenType previous, EPacScreenType next)
        {
            PacGameWindow window = GetWindow(PacGameWindow.Key);

            if (window == null)
                return;

            if (next == EPacScreenType.Game)
            {
                window.Open();
                Refresh();
                return;
            }

            CloseAllWindows();
        }

        /// <summary>
        /// 원본 <c>alert</c> — 게임은 끝난 채 남고 알림만 뜬다.
        /// 닫아도 아무 일도 안 한다 (원본과 같다) — 빠져나가는 경로는 Restart 하나뿐이다.
        /// </summary>
        private void HandleGameEnded(bool cleared)
        {
            PacAlertPopup popup = GetWindow(PacAlertPopup.Key);

            if (popup == null)
                return;

            popup.SetMessage(cleared ? ClearMessage : GameOverMessage);
            popup.Open();
        }

        private void HandleRestart()
        {
            // 알림이 떠 있으면 닫는다 — 원본도 restart 는 새 판이다.
            PacAlertPopup popup = GetWindow(PacAlertPopup.Key);

            if (popup != null && popup.IsOpen())
                popup.Close();

            PacGameManager.Instance.Restart();
            Refresh();
        }

        private void HandleDirection(int dx, int dy)
        {
            PacGameManager.Instance.PacMgr.SetNextDirection(dx, dy);
        }

        /// <summary>⚠ 원본은 «첫 입력»에 배경음을 켠다 (브라우저 제약). 시점을 맞춘다.</summary>
        private void HandleFirstInput()
        {
            if (_audio != null)
                _audio.StartBackground();
        }

        private void HandlePelletEaten(int col, int row)
        {
            if (_audio != null)
                _audio.PlayPellet();
        }

        private void HandleCaught()
        {
            if (_audio != null)
                _audio.PlayDeath();
        }

        private void HandleStateChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            PacGameWindow window = GetWindow(PacGameWindow.Key);

            if (window == null)
                return;

            MazeManager maze = PacGameManager.Instance.MazeMgr;
            window.SetState(maze.Score, maze.Lives);
        }
    }
}
