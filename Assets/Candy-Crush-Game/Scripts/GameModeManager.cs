using System;
using JinHyung.Core;
using JinHyung.Data;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 모드·타이머·종료. 원본 <c>startGame</c> · <c>updateTimerDisplay</c> ·
    /// <c>endGame</c> · <c>changeMode</c> (<c>script.js:187~245</c>).
    ///
    /// <para>
    /// 두 모드의 차이는 <b>타이머 하나뿐</b>이다 — 보드·매치·점수 규칙은 완전히 같다
    /// (<c>HtmlToUnityLogic/02_시스템_게임모드.md</c>).
    /// </para>
    /// </summary>
    public class GameModeManager : BaseManager, IGameUpdate
    {
        /// <summary>원본 타이머 간격. <b>「1초」의 정의라 튜너블이 아니다</b> (<c>script.js:207</c>).</summary>
        private const float TimerIntervalSeconds = 1f;

        public EGameModeType CurrentMode { get; private set; }
        public int TimeLeft { get; private set; }

        /// <summary>원본 <c>draggable=false</c> (<c>script.js:227</c>) 에 대응한다.</summary>
        public bool IsInputLocked { get; private set; }

        /// <summary>타이머 표기 문자열. Endless 에서는 <b>빈 문자열</b>이다 (원본 <c>script.js:209</c> · <c>220</c>).</summary>
        public event Action<string> OnTimerTextChanged;

        /// <summary>Timed 종료. 인자는 최종 점수 — 원본 <c>alert</c> 문구에 들어가는 값이다.</summary>
        public event Action<int> OnGameEnded;

        /// <summary>모드 선택 화면으로 돌아간다. 원본 <c>changeMode</c>.</summary>
        public event Action OnModeCleared;

        /// <summary>게임 화면으로 들어간다. 원본 <c>startGame</c> 의 화면 부분.</summary>
        public event Action<EGameModeType> OnGameStarted;

        private readonly BoardManager _board;
        private bool _timerRunning;
        private float _timerAccum;

        public GameModeManager(BoardManager board)
        {
            _board = board;
        }

        protected override void OnDispose()
        {
            OnTimerTextChanged = null;
            OnGameEnded = null;
            OnModeCleared = null;
            OnGameStarted = null;
            _timerRunning = false;
        }

        /// <summary>
        /// 원본 <c>startGame(mode)</c> (<c>script.js:187~211</c>).
        /// <b>순서가 사양이다</b> — 화면 전환 → 보드 생성 → 점수 0 → 루프 시작 → (Timed면) 타이머.
        /// </summary>
        public void StartGame(EGameModeType mode)
        {
            CurrentMode = mode;
            IsInputLocked = false;

            OnGameStarted?.Invoke(mode);

            _board.CreateBoard();
            _board.StartTick();

            if (mode == EGameModeType.Timed)
            {
                GameConfigData config = GameRoot.Instance.GameConfigDataContainer?.Data;
                TimeLeft = config == null ? 0 : config.TimedSeconds;

                _timerRunning = true;
                _timerAccum = 0f;
                RaiseTimerText();
            }
            else
            {
                // 원본 script.js:209 — Endless 에서는 타이머를 «빈 문자열»로 둔다.
                // ⚠ 요소를 끄는 것이 아니다. 크기 0으로 남아 레이아웃 배분에는 참여한다.
                _timerRunning = false;
                TimeLeft = 0;
                OnTimerTextChanged?.Invoke(string.Empty);
            }
        }

        public void OnUpdate(float deltaTime)
        {
            if (_timerRunning == false)
                return;

            _timerAccum += deltaTime;

            if (_timerAccum < TimerIntervalSeconds)
                return;

            _timerAccum -= TimerIntervalSeconds;

            // 원본 script.js:200~207 — 먼저 줄이고, 표기를 갱신하고, 그다음 0 판정을 한다.
            TimeLeft--;
            RaiseTimerText();

            if (TimeLeft <= 0)
            {
                _timerRunning = false;
                EndGame();
            }
        }

        /// <summary>
        /// 원본 <c>endGame</c> (<c>script.js:225~229</c>).
        ///
        /// <para>
        /// ⚠ <b>보드를 숨기지 않고 점수도 지우지 않는다.</b> 잠그고 알림만 띄운다 —
        /// 빠져나가는 경로는 <c>[Change Mode]</c> 하나뿐이다.
        /// </para>
        /// </summary>
        private void EndGame()
        {
            _board.StopTick();
            IsInputLocked = true;

            OnGameEnded?.Invoke(_board.Score);
        }

        /// <summary>
        /// 원본 <c>changeMode</c> (<c>script.js:232~240</c>).
        ///
        /// <para>
        /// ⚠ 원본은 <c>currentMode</c> 와 <c>timeLeft</c> 를 <b>지우지 않는다.</b>
        /// 다음 <c>startGame</c> 이 덮으므로 결과는 같다 — 그대로 옮긴다.
        /// </para>
        /// </summary>
        public void ChangeMode()
        {
            _board.StopTick();
            _timerRunning = false;
            IsInputLocked = false;

            OnModeCleared?.Invoke();
        }

        /// <summary>
        /// 원본 <c>updateTimerDisplay</c> (<c>script.js:214~222</c>).
        /// 포맷은 <c>Time Left: M:SS</c> — <b>분은 패딩이 없고 초만 두 자리</b>다.
        /// </summary>
        private void RaiseTimerText()
        {
            if (CurrentMode != EGameModeType.Timed)
            {
                OnTimerTextChanged?.Invoke(string.Empty);
                return;
            }

            int minutes = TimeLeft / 60;
            int seconds = TimeLeft % 60;

            OnTimerTextChanged?.Invoke($"Time Left: {minutes}:{seconds:D2}");
        }

        /// <summary>원본 <c>alert</c> 문구 (<c>script.js:228</c>). <b>바이트 단위로 같아야 한다.</b></summary>
        public static string BuildGameOverText(int score)
        {
            return $"Time's Up! Your score is {score}";
        }
    }
}
