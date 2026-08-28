using System.Collections.Generic;
using System.Threading.Tasks;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using UnityEngine;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 게임 화면과 종료 팝업을 조종한다. <b>보드 ↔ 화면의 배선이 전부 여기 있다.</b>
    ///
    /// <para>
    /// 원본은 로직과 DOM 이 한 함수 안에 섞여 있었다. 우리는
    /// <b>Manager(로직) ↔ Management(창 조종) ↔ Window(표기)</b> 로 갈랐다 —
    /// 값·분기·순서는 그대로 두고 구조만 다시 세운 것이다.
    /// </para>
    /// </summary>
    public class GameManagement : BaseManagement
    {
        private Sprite[] _candySprites;

        protected override void AddWindows()
        {
            RegisterWindow(GameWindow.Key);
            RegisterWindow(TimeUpPopup.Key);
        }

        protected override void OnInitialize()
        {
            CandyGameManager game = CandyGameManager.Instance;

            game.GameFlow.OnScreenChanged += HandleScreenChanged;

            game.ModeMgr.OnGameStarted += HandleGameStarted;
            game.ModeMgr.OnModeCleared += HandleModeCleared;
            game.ModeMgr.OnTimerTextChanged += HandleTimerTextChanged;
            game.ModeMgr.OnGameEnded += HandleGameEnded;

            game.BoardMgr.OnBoardChanged += HandleBoardChanged;
            game.BoardMgr.OnScoreChanged += HandleScoreChanged;

            GameWindow window = GetWindow(GameWindow.Key);

            if (window != null)
            {
                window.BuildCells(game.BoardMgr.CellCount);
                window.OnChangeModeClicked += HandleChangeModeClicked;
                window.OnCellDragBegan += HandleCellDragBegan;
                window.OnCellDropped += HandleCellDropped;
                window.OnCellDragEnded += HandleCellDragEnded;
            }

            TimeUpPopup popup = GetWindow(TimeUpPopup.Key);

            if (popup != null)
                popup.OnConfirmed += HandlePopupConfirmed;
        }

        protected override void OnDispose()
        {
            if (CandyGameManager.HasInstance)
            {
                CandyGameManager game = CandyGameManager.Instance;

                if (game.GameFlow != null)
                    game.GameFlow.OnScreenChanged -= HandleScreenChanged;

                if (game.ModeMgr != null)
                {
                    game.ModeMgr.OnGameStarted -= HandleGameStarted;
                    game.ModeMgr.OnModeCleared -= HandleModeCleared;
                    game.ModeMgr.OnTimerTextChanged -= HandleTimerTextChanged;
                    game.ModeMgr.OnGameEnded -= HandleGameEnded;
                }

                if (game.BoardMgr != null)
                {
                    game.BoardMgr.OnBoardChanged -= HandleBoardChanged;
                    game.BoardMgr.OnScoreChanged -= HandleScoreChanged;
                }
            }

            GameWindow window = GetWindow(GameWindow.Key);

            if (window != null)
            {
                window.OnChangeModeClicked -= HandleChangeModeClicked;
                window.OnCellDragBegan -= HandleCellDragBegan;
                window.OnCellDropped -= HandleCellDropped;
                window.OnCellDragEnded -= HandleCellDragEnded;
            }

            TimeUpPopup popup = GetWindow(TimeUpPopup.Key);

            if (popup != null)
                popup.OnConfirmed -= HandlePopupConfirmed;
        }

        /// <summary>사탕 그림을 읽어 창에 주입한다. <b>UI 는 주소를 모른다.</b></summary>
        public async Task LoadArtAsync()
        {
            CandyDataContainer container = GameRoot.Instance.CandyDataContainer;

            if (container == null || container.Count == 0)
            {
                Log.Error("CandyTable 이 없다. 아트를 읽을 수 없다.");
                return;
            }

            var addresses = new List<string>(container.Count);

            // ★ 순서가 원본 배열 순서다 — AllValues[i] 가 원본 candyColors[i] 에 대응한다.
            for (int i = 0; i < container.AllValues.Count; i++)
                addresses.Add(container.AllValues[i].SpriteAddress);

            _candySprites = await ArtLoader.LoadSpritesAsync(addresses);

            GameWindow window = GetWindow(GameWindow.Key);

            if (window != null)
                window.SetCandySprites(_candySprites);
        }

        // ────────────────────────────── 화면 전이

        private void HandleScreenChanged(EGameScreenType previous, EGameScreenType next)
        {
            if (next == EGameScreenType.Game)
                return; // 여는 것은 HandleGameStarted 가 한다 (원본 startGame 순서 그대로)

            CloseAllWindows();
        }

        private void HandleGameStarted(EGameModeType mode)
        {
            GameWindow window = GetWindow(GameWindow.Key);

            if (window == null)
                return;

            window.Open();
            window.SetInputLocked(false);

            CandyGameManager.Instance.GameFlow.ChangeScreen(EGameScreenType.Game);
        }

        private void HandleModeCleared()
        {
            CloseAllWindows();
            CandyGameManager.Instance.GameFlow.ChangeScreen(EGameScreenType.ModeSelect);
        }

        // ────────────────────────────── 보드 → 화면

        private void HandleBoardChanged()
        {
            GameWindow window = GetWindow(GameWindow.Key);

            if (window != null)
                window.SetBoard(CandyGameManager.Instance.BoardMgr.Cells);
        }

        private void HandleScoreChanged(int score)
        {
            GameWindow window = GetWindow(GameWindow.Key);

            if (window != null)
                window.SetScore(score);
        }

        private void HandleTimerTextChanged(string text)
        {
            GameWindow window = GetWindow(GameWindow.Key);

            if (window != null)
                window.SetTimerText(text);
        }

        /// <summary>
        /// 원본 <c>endGame</c> — 잠그고 알린다.
        /// <b>보드를 숨기지 않고 점수도 지우지 않는다.</b>
        /// </summary>
        private void HandleGameEnded(int score)
        {
            GameWindow window = GetWindow(GameWindow.Key);

            if (window != null)
                window.SetInputLocked(true);

            TimeUpPopup popup = GetWindow(TimeUpPopup.Key);

            if (popup == null)
                return;

            popup.SetData(GameModeManager.BuildGameOverText(score));
            popup.Open();
        }

        private void HandlePopupConfirmed()
        {
            CloseWindow(TimeUpPopup.Key);
        }

        // ────────────────────────────── 화면 → 보드

        private void HandleChangeModeClicked()
        {
            CloseWindow(TimeUpPopup.Key);
            CandyGameManager.Instance.ModeMgr.ChangeMode();
        }

        private void HandleCellDragBegan(int index)
        {
            CandyGameManager.Instance.BoardMgr.BeginDrag(index);
        }

        private void HandleCellDropped(int index)
        {
            CandyGameManager.Instance.BoardMgr.Drop(index);
        }

        private void HandleCellDragEnded(int index)
        {
            CandyGameManager.Instance.BoardMgr.EndDrag();
        }
    }
}
