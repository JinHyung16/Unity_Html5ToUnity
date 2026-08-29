using System;
using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 미로 격자와 «판의 상태»(점수·목숨·남은 펠릿)를 든다.
    ///
    /// <para>
    /// <b>이것이 진실 소스다.</b> 화면은 여기를 읽어 표기만 한다 —
    /// 화면이 상태를 들기 시작하면 원본 배열과 그림이 갈린다.
    /// </para>
    /// </summary>
    public class MazeManager : BaseManager
    {
        public const int Empty = 0;
        public const int Wall = 1;
        public const int Pellet = 2;
        public const int PowerPellet = 3;

        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int PelletsRemaining { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>클리어했나. 원본은 <c>gameOver</c> 하나로 뭉쳤지만 문구가 갈리므로 나눈다.</summary>
        public bool IsCleared { get; private set; }

        public event Action OnStateChanged;
        public event Action<int, int> OnTileChanged;
        public event Action<bool> OnGameEnded;

        private int[] _tiles;
        private PacConfigData _config;

        protected override void OnInitialize()
        {
            _config = GameRoot.Instance.PacConfigDataContainer?.Data;

            if (_config == null)
                throw new InvalidOperationException("PacConfigTable 을 못 읽었다");

            Cols = _config.Cols;
            Rows = _config.Rows;
            _tiles = new int[Cols * Rows];
        }

        protected override void OnDispose()
        {
            OnStateChanged = null;
            OnTileChanged = null;
            OnGameEnded = null;
        }

        /// <summary>원본 <c>initMap</c> (<c>script.js:156~168</c>) — 문자열을 숫자 격자로.</summary>
        public void BuildMap()
        {
            MazeDataContainer maze = GameRoot.Instance.MazeDataContainer;
            PelletsRemaining = 0;

            for (int r = 0; r < Rows; r++)
            {
                string row = maze.GetRow(r);

                for (int c = 0; c < Cols; c++)
                {
                    int value = row[c] - '0';
                    _tiles[r * Cols + c] = value;

                    if (value == Pellet || value == PowerPellet)
                        PelletsRemaining++;
                }
            }
        }

        /// <summary>원본 <c>restartGame</c> (<c>script.js:189~196</c>) — 점수·목숨·맵 전부.</summary>
        public void Restart()
        {
            Score = 0;
            Lives = _config.StartLives;
            IsGameOver = false;
            IsCleared = false;
            BuildMap();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 원본 <c>isWall</c> (<c>script.js:203~206</c>).
        /// ⚠ <b>격자 밖은 «벽으로 친다».</b> 터널 랩이 가로에만 있는 이유가 이것이다.
        /// </summary>
        public bool IsWall(int col, int row)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                return true;

            return _tiles[row * Cols + col] == Wall;
        }

        public int GetTile(int col, int row)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                return Wall;

            return _tiles[row * Cols + col];
        }

        public IReadOnlyList<int> Tiles
        {
            get { return _tiles; }
        }

        /// <summary>
        /// 원본 <c>movePacman</c> 의 펠릿 부분 (<c>script.js:261~274</c>).
        ///
        /// <para>
        /// ⚠ <b>이동 여부와 무관하게 «현재 칸»을 먹는다.</b> 골든 V1 —
        /// 시작 칸에 펠릿이 있어 <b>움직이지 않아도 첫 프레임에 +10</b> 이 된다.
        /// 「이동해야 먹는다」로 바꾸면 시작 점수가 어긋난다.
        /// </para>
        /// </summary>
        public bool TryEat(int col, int row)
        {
            int tile = GetTile(col, row);

            if (tile != Pellet && tile != PowerPellet)
                return false;

            // ⚠ 파워펠릿에 «효과가 없다». 점수만 다르다 — 원본이 유령 상태를 안 건드린다.
            Score += tile == Pellet ? _config.PelletScore : _config.PowerPelletScore;

            _tiles[row * Cols + col] = Empty;
            PelletsRemaining--;

            OnTileChanged?.Invoke(col, row);
            OnStateChanged?.Invoke();

            if (PelletsRemaining <= 0)
            {
                IsGameOver = true;
                IsCleared = true;
                OnGameEnded?.Invoke(true);
            }

            return true;
        }

        /// <summary>원본 <c>checkCollisions</c> 의 목숨 부분 (<c>script.js:325~334</c>).</summary>
        public void LoseLife()
        {
            Lives--;
            OnStateChanged?.Invoke();

            if (Lives <= 0)
            {
                IsGameOver = true;
                IsCleared = false;
                OnGameEnded?.Invoke(false);
            }
        }
    }
}
