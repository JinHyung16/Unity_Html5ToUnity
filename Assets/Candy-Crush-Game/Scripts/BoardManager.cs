using System;
using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 보드 시뮬레이션. <b>원본 <c>script.js</c> 의 절차를 그대로 옮긴 것</b>이다.
    ///
    /// <para>
    /// ⚠ <b>「더 나은 공식」을 넣지 않는다.</b> 이 클래스의 거동은
    /// <c>HtmlToUnityLogic/07_기대값.md</c> 의 골든 27벡터로 못 박혀 있다.
    /// 자연스럽게 다시 짜면 <b>V13 · V16 · V18 · D6 · D7 이 전부 틀린다.</b>
    /// </para>
    ///
    /// <para>
    /// 칸의 값은 <b>사탕 원본 인덱스(0~5)</b>이고 <see cref="Blank"/>(-1) 이 빈 칸이다.
    /// 원본은 <c>backgroundImage</c> 문자열을 비교하고 빈 칸을 <c>""</c> 로 뒀다 —
    /// 값만 옮기고 <b>표현은 엔진에 맞게 바꿨다.</b>
    /// </para>
    /// </summary>
    public class BoardManager : BaseManager, IGameUpdate
    {
        /// <summary>빈 칸. 원본의 <c>backgroundImage === ""</c> 에 대응한다.</summary>
        public const int Blank = -1;

        /// <summary>드래그 상태 없음. 원본의 <c>undefined</c> / <c>null</c> 에 대응한다.</summary>
        private const int NoId = int.MinValue;

        public int Width { get; private set; }
        public int CellCount { get; private set; }
        public int Score { get; private set; }
        public bool IsRunning { get; private set; }

        /// <summary>칸 상태. 읽기 전용으로 넘긴다 — <b>보드가 진실 소스</b>이고 UI 는 표기만 한다.</summary>
        public IReadOnlyList<int> Cells
        {
            get { return _cells; }
        }

        public event Action<int> OnScoreChanged;
        public event Action OnBoardChanged;

        private int[] _cells;
        private int _candyKindCount;

        // 원본 script.js:124 · 138 · 151 · 165 의 루프 상한. width 에서 파생된다.
        private int _rowFourLimit;
        private int _columnFourLimit;
        private int _rowThreeLimit;
        private int _columnThreeLimit;

        // 원본 script.js:56 의 드래그 상태 4개.
        private int _draggedId = NoId;
        private int _replacedId = NoId;
        private int _colorBeingDragged = Blank;
        private int _colorBeingReplaced = Blank;

        private float _tickInterval;
        private float _tickAccum;

        /// <summary>
        /// ⚠ <b>주입식 RNG.</b> <c>UnityEngine.Random</c> 을 직접 부르지 않는다 —
        /// 재현이 안 되면 채점도 안 된다.
        /// </summary>
        private Random _random;

        public BoardManager(Random random)
        {
            _random = random ?? new Random();
        }

        protected override void OnInitialize()
        {
            GameConfigData config = GameRoot.Instance.GameConfigDataContainer?.Data;

            if (config == null)
            {
                // 폴백을 깔지 않는다. 설정이 없으면 그건 오류다.
                Log.Error("GameConfigTable 이 없다. 보드를 세울 수 없다.");
                return;
            }

            Width = config.BoardWidth;
            CellCount = Width * Width;
            _cells = new int[CellCount];

            // ★ 상한은 값이 아니라 파생식이다. 폭이 바뀌면 같이 바뀌어야 한다.
            //   원본 값(width=8): 60 · 40 · 62 · 48
            _rowFourLimit = CellCount - 4;              // 60
            _columnFourLimit = Width * (Width - 3);     // 40
            _rowThreeLimit = CellCount - 2;             // 62
            _columnThreeLimit = Width * (Width - 2);    // 48

            _candyKindCount = GameRoot.Instance.CandyDataContainer?.Count ?? 0;

            if (_candyKindCount <= 0)
                Log.Error("CandyTable 이 비었다. 사탕을 뽑을 수 없다.");

            _tickInterval = config.TickIntervalMs / 1000f;
        }

        protected override void OnDispose()
        {
            OnScoreChanged = null;
            OnBoardChanged = null;
            IsRunning = false;
        }

        // ────────────────────────────── 시작 · 정지

        /// <summary>
        /// 원본 <c>createBoard()</c> (<c>script.js:34~53</c>) + <c>startGame</c> 의 보드 부분.
        ///
        /// <para>
        /// ⚠ <b>초기 보드의 매치를 막지 않는다.</b> 원본에 재추첨이 없어서
        /// <b>판을 켜자마자 점수가 오르는 것이 사양</b>이다.
        /// </para>
        /// </summary>
        public void CreateBoard()
        {
            if (_cells == null)
                return;

            for (int i = 0; i < CellCount; i++)
                _cells[i] = NextCandy();

            Score = 0;
            _tickAccum = 0f;
            ClearDragState();

            OnScoreChanged?.Invoke(Score);
            OnBoardChanged?.Invoke();
        }

        public void StartTick()
        {
            IsRunning = true;
            _tickAccum = 0f;
        }

        /// <summary>원본 <c>clearInterval(gameInterval)</c> 에 대응한다.</summary>
        public void StopTick()
        {
            IsRunning = false;
        }

        // ────────────────────────────── 메인 루프

        public void OnUpdate(float deltaTime)
        {
            if (IsRunning == false || _cells == null)
                return;

            _tickAccum += deltaTime;

            // 한 프레임에 한 틱만 돈다. 원본 setInterval 은 밀린 틱을 쌓아 두지 않는다 —
            // while 로 몰아 돌리면 끊긴 프레임 뒤에 연쇄가 한꺼번에 터진다.
            if (_tickAccum < _tickInterval)
                return;

            _tickAccum -= _tickInterval;
            GameLoop();
        }

        /// <summary>
        /// 원본 <c>gameLoop</c> (<c>script.js:178~184</c>).
        ///
        /// <para>
        /// ★★ <b>이 다섯 줄의 순서가 사양이다.</b> 먼저 먹은 판정이 뒤 판정을 막고(<c>V16</c> · <c>V18</c>),
        /// 낙하가 판정 뒤에 붙어 한 틱 안에서 제거와 메움이 같이 일어난다.
        /// 묶어서 판정하거나 순서를 바꾸면 점수가 달라진다.
        /// </para>
        /// </summary>
        private void GameLoop()
        {
            int before = Score;

            CheckRowForFour();
            CheckColumnForFour();
            CheckRowForThree();
            CheckColumnForThree();
            MoveIntoSquareBelow();

            if (Score != before)
                OnScoreChanged?.Invoke(Score);

            OnBoardChanged?.Invoke();
        }

        // ────────────────────────────── 판정 4종

        /// <summary>
        /// 원본 <c>checkRowForFour</c> (<c>script.js:123~135</c>).
        ///
        /// <para>
        /// ⚠ <b>[원본 이상 #1] 상한이 <c>CellCount - 4</c>(=60) 라 <c>i = 60</c> 이 빠진다.</b>
        /// 그래서 <b>마지막 행 col4~7 의 4연속이 4매치로 안 잡히고</b>
        /// <see cref="CheckRowForThree"/> 가 <c>[60,61,62]</c> 를 3매치로 대신 먹는다 (골든 <c>V6</c>: +3).
        /// <b>고치지 않는다</b> — 고치면 원본과 달라지고 V6 이 깨진다.
        /// </para>
        /// </summary>
        private void CheckRowForFour()
        {
            int score = GetScoreMatchFour();

            for (int i = 0; i < _rowFourLimit; i++)
            {
                if (i % Width >= Width - 3)
                    continue;

                int decided = _cells[i];

                if (decided == Blank)
                    continue;

                if (_cells[i + 1] != decided || _cells[i + 2] != decided || _cells[i + 3] != decided)
                    continue;

                Score += score;

                _cells[i] = Blank;
                _cells[i + 1] = Blank;
                _cells[i + 2] = Blank;
                _cells[i + 3] = Blank;
            }
        }

        /// <summary>원본 <c>checkColumnForFour</c> (<c>script.js:137~148</c>).</summary>
        private void CheckColumnForFour()
        {
            int score = GetScoreMatchFour();

            for (int i = 0; i < _columnFourLimit; i++)
            {
                int decided = _cells[i];

                if (decided == Blank)
                    continue;

                if (_cells[i + Width] != decided || _cells[i + 2 * Width] != decided || _cells[i + 3 * Width] != decided)
                    continue;

                Score += score;

                _cells[i] = Blank;
                _cells[i + Width] = Blank;
                _cells[i + 2 * Width] = Blank;
                _cells[i + 3 * Width] = Blank;
            }
        }

        /// <summary>원본 <c>checkRowForThree</c> (<c>script.js:150~162</c>).</summary>
        private void CheckRowForThree()
        {
            int score = GetScoreMatchThree();

            for (int i = 0; i < _rowThreeLimit; i++)
            {
                if (i % Width >= Width - 2)
                    continue;

                int decided = _cells[i];

                if (decided == Blank)
                    continue;

                if (_cells[i + 1] != decided || _cells[i + 2] != decided)
                    continue;

                Score += score;

                _cells[i] = Blank;
                _cells[i + 1] = Blank;
                _cells[i + 2] = Blank;
            }
        }

        /// <summary>원본 <c>checkColumnForThree</c> (<c>script.js:164~175</c>).</summary>
        private void CheckColumnForThree()
        {
            int score = GetScoreMatchThree();

            for (int i = 0; i < _columnThreeLimit; i++)
            {
                int decided = _cells[i];

                if (decided == Blank)
                    continue;

                if (_cells[i + Width] != decided || _cells[i + 2 * Width] != decided)
                    continue;

                Score += score;

                _cells[i] = Blank;
                _cells[i + Width] = Blank;
                _cells[i + 2 * Width] = Blank;
            }
        }

        // ────────────────────────────── 낙하 · 재생성

        /// <summary>
        /// 원본 <c>moveIntoSquareBelow</c> (<c>script.js:105~120</c>).
        ///
        /// <para>
        /// ★★ <b>순회 방향이 사양이다.</b> <c>i</c> 를 <b>올려 가며</b> 훑으므로
        /// 방금 내린 칸을 같은 호출에서 다시 본다 — 그래서 사탕 하나가
        /// <b>빈 칸이 이어지는 만큼 한 번에 미끄러지고 중간 빈 칸은 남는다</b> (골든 <c>V13</c>).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>「중력」으로 다시 짜지 않는다.</b> 아래에서 위로 훑거나 열 단위로 압축하면
        /// 한 틱에 열 전체가 정리되어 <b>V13 · V15 가 전부 깨진다.</b>
        /// </para>
        ///
        /// <para>
        /// 순서도 사양이다 — <b>0행 채우기가 먼저, 내리기가 나중</b>이다.
        /// </para>
        /// </summary>
        private void MoveIntoSquareBelow()
        {
            // ① 맨 윗줄의 빈 칸을 새 사탕으로 (원본 script.js:107~112)
            for (int i = 0; i < Width; i++)
            {
                if (_cells[i] == Blank)
                    _cells[i] = NextCandy();
            }

            // ② 위 칸을 아래로 (원본 script.js:114~119) — i 오름차순, 제자리 수정
            int limit = Width * (Width - 1);

            for (int i = 0; i < limit; i++)
            {
                if (_cells[i + Width] != Blank)
                    continue;

                _cells[i + Width] = _cells[i];
                _cells[i] = Blank;
            }
        }

        // ────────────────────────────── 드래그

        /// <summary>원본 <c>dragStart</c> (<c>script.js:58~61</c>).</summary>
        public void BeginDrag(int index)
        {
            if (IsValidIndex(index) == false)
                return;

            _colorBeingDragged = _cells[index];
            _draggedId = index;
        }

        /// <summary>
        /// 원본 <c>dragDrop</c> (<c>script.js:75~80</c>).
        ///
        /// <para>
        /// ★ <b>여기서 「먼저」 교환한다.</b> 유효성 판정은 <see cref="EndDrag"/> 가 뒤에 한다.
        /// 「검사하고 나서 교환」으로 뒤집으면 화면 결과가 같아 보여도
        /// <b><c>D6</c>(0번 칸 복제)이 재현되지 않는다.</b>
        /// </para>
        /// </summary>
        public void Drop(int index)
        {
            if (IsValidIndex(index) == false || _draggedId == NoId)
                return;

            _colorBeingReplaced = _cells[index];
            _replacedId = index;

            _cells[index] = _colorBeingDragged;
            _cells[_draggedId] = _colorBeingReplaced;

            OnBoardChanged?.Invoke();
        }

        /// <summary>
        /// 원본 <c>dragEnd</c> (<c>script.js:82~102</c>).
        ///
        /// <para>
        /// ⚠ <b>[원본 이상 #2] 원본은 <c>if (squareIdBeingReplaced &amp;&amp; …)</c> 로 판정한다.</b>
        /// JS 에서 <c>0</c> 은 falsy 라 <b>0번 칸에 떨어뜨리면 두 조건을 다 건너뛰고</b>
        /// 마지막 <c>else</c> 로 빠져 <b>드래그한 칸만 되돌린다</b> —
        /// <c>Drop</c> 이 이미 교환한 뒤라 두 칸이 같은 색이 되고 한 색이 사라진다 (골든 <c>D6</c>).
        /// 아래 <c>replacedTruthy</c> 가 그 falsy 판정을 그대로 옮긴 것이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>[원본 이상 #3] <c>validMoves</c> 에 열 경계 가드가 없다.</b>
        /// <c>±1</c> 이 행을 넘어가므로 <b>행 경계 좌우 스왑이 성립</b>한다 (골든 <c>D7</c>).
        /// </para>
        ///
        /// <para>
        /// ⚠ <c>_replacedId</c> 는 <b>유효 이동일 때만</b> 지워진다. 원본이 그렇다 —
        /// 무효 이동 뒤에는 값이 남고, 그것이 다음 판정에 쓰인다.
        /// </para>
        /// </summary>
        public void EndDrag()
        {
            if (_draggedId == NoId)
                return;

            bool hasReplaced = _replacedId != NoId;

            // 원본: validMoves.includes(squareIdBeingReplaced)
            bool validMove = hasReplaced
                             && (_replacedId == _draggedId - 1
                                 || _replacedId == _draggedId - Width
                                 || _replacedId == _draggedId + 1
                                 || _replacedId == _draggedId + Width);

            // 원본의 `if (squareIdBeingReplaced && …)` — JS 에서 0 은 falsy 다.
            bool replacedTruthy = hasReplaced && _replacedId != 0;

            if (replacedTruthy && validMove)
            {
                _replacedId = NoId;
            }
            else if (replacedTruthy && validMove == false)
            {
                _cells[_replacedId] = _colorBeingReplaced;
                _cells[_draggedId] = _colorBeingDragged;
            }
            else
            {
                // ★ 여기서 «드래그한 칸만» 되돌린다. 0번 칸은 Drop 이 넣은 값을 그대로 든다.
                _cells[_draggedId] = _colorBeingDragged;
            }

            OnBoardChanged?.Invoke();
        }

        // ────────────────────────────── 내부

        private bool IsValidIndex(int index)
        {
            return _cells != null && index >= 0 && index < CellCount;
        }

        private void ClearDragState()
        {
            _draggedId = NoId;
            _replacedId = NoId;
            _colorBeingDragged = Blank;
            _colorBeingReplaced = Blank;
        }

        /// <summary>원본 <c>Math.floor(Math.random() * candyColors.length)</c> (<c>script.js:41</c> · <c>109</c>).</summary>
        private int NextCandy()
        {
            if (_candyKindCount <= 0)
                return Blank;

            return _random.Next(0, _candyKindCount);
        }

        private int GetScoreMatchThree()
        {
            GameConfigData config = GameRoot.Instance.GameConfigDataContainer?.Data;
            return config == null ? 0 : config.ScoreMatchThree;
        }

        private int GetScoreMatchFour()
        {
            GameConfigData config = GameRoot.Instance.GameConfigDataContainer?.Data;
            return config == null ? 0 : config.ScoreMatchFour;
        }
    }
}
