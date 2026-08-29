using System;
using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 유령 이동과 충돌. <b>「AI」가 아니다</b> — 팩맨 좌표를 한 번도 읽지 않는다.
    /// 벽에 부딪힐 때만 <b>무작위</b>로 방향을 고른다 (180도 회전만 제외).
    ///
    /// <para>
    /// ⚠ <b>등록 순서가 틱 순서다.</b> 원본 <c>loop</c> 는
    /// <c>movePacman</c> → <c>moveGhost</c> → <c>checkCollisions</c> 순서다 —
    /// 그래서 이 매니저는 <b>팩맨 다음에</b> 등록되고, 충돌 검사를 <b>이동 뒤에</b> 한다.
    /// </para>
    /// </summary>
    public class GhostManager : BaseManager, IGameUpdate
    {
        /// <summary>유령 하나의 상태. 원본 <c>ghosts[i]</c> 에 대응한다.</summary>
        public class Ghost
        {
            public float X;
            public float Y;
            public int DirX;
            public int DirY;
            public Color Color;
        }

        private struct Direction
        {
            public int X;
            public int Y;

            public Direction(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        /// <summary>원본 <c>dirs</c> 배열과 <b>같은 순서</b>다 — 무작위 인덱스가 순서에 걸린다.</summary>
        private static readonly Direction[] Directions =
        {
            new Direction(1, 0),
            new Direction(-1, 0),
            new Direction(0, 1),
            new Direction(0, -1),
        };

        public IReadOnlyList<Ghost> Ghosts
        {
            get { return _ghosts; }
        }

        public event Action OnMoved;

        /// <summary>충돌해서 개체를 되돌려야 할 때. 원본 <c>resetEntities</c> 에 대응한다.</summary>
        public event Action OnCaught;

        private readonly MazeManager _maze;
        private readonly PacmanManager _pacman;
        private readonly List<Ghost> _ghosts = new List<Ghost>(2);
        private readonly List<Direction> _candidates = new List<Direction>(3);
        private PacConfigData _config;

        public GhostManager(MazeManager maze, PacmanManager pacman)
        {
            _maze = maze;
            _pacman = pacman;
        }

        protected override void OnInitialize()
        {
            _config = GameRoot.Instance.PacConfigDataContainer?.Data;

            if (_config == null)
                throw new InvalidOperationException("PacConfigTable 을 못 읽었다");

            GhostDataContainer table = GameRoot.Instance.GhostDataContainer;
            _ghosts.Clear();

            for (int i = 0; i < table.AllValues.Count; i++)
            {
                GhostData row = table.GetByOriginIndex(i);

                // ⚠ 색은 «데이터»가 든다. 유령 둘은 모양이 같고 색만 다르다.
                if (ColorUtility.TryParseHtmlString("#" + row.ColorHex, out Color color) == false)
                    color = Color.white;

                _ghosts.Add(new Ghost { Color = color });
            }
        }

        protected override void OnDispose()
        {
            OnMoved = null;
            OnCaught = null;
        }

        /// <summary>원본 <c>resetEntities</c> 의 유령 부분 (<c>script.js:179~186</c>).</summary>
        public void Reset()
        {
            GhostDataContainer table = GameRoot.Instance.GhostDataContainer;

            for (int i = 0; i < _ghosts.Count; i++)
            {
                GhostData row = table.GetByOriginIndex(i);

                _ghosts[i].X = row.StartCol;
                _ghosts[i].Y = row.StartRow;
                _ghosts[i].DirX = row.DirX;
                _ghosts[i].DirY = row.DirY;
            }

            OnMoved?.Invoke();
        }

        public void OnUpdate(float deltaTime)
        {
            if (_maze.IsGameOver)
                return;

            for (int i = 0; i < _ghosts.Count; i++)
                Move(_ghosts[i], deltaTime);

            OnMoved?.Invoke();
            CheckCollisions();
        }

        /// <summary>원본 <c>moveGhost</c> (<c>script.js:280~313</c>).</summary>
        private void Move(Ghost ghost, float deltaTime)
        {
            float speed = _config.GhostSpeed * deltaTime;
            float newX = ghost.X + ghost.DirX * speed;
            float newY = ghost.Y + ghost.DirY * speed;

            int nextCol = Mathf.RoundToInt(newX);
            int nextRow = Mathf.RoundToInt(newY);

            if (_maze.IsWall(nextCol, nextRow))
            {
                // ★ 원본 이상 A3 — 고른 방향이 «벽인지 확인하지 않는다».
                //   벽을 고르면 다음 프레임에 또 부딪혀 또 고른다 (실측: 연속 제자리).
                int oppositeX = -ghost.DirX;
                int oppositeY = -ghost.DirY;

                _candidates.Clear();

                for (int i = 0; i < Directions.Length; i++)
                {
                    if (Directions[i].X == oppositeX && Directions[i].Y == oppositeY)
                        continue;

                    _candidates.Add(Directions[i]);
                }

                // ⚠ 무작위는 «한 곳에서만» 꺼낸다 — 하네스가 갈아 끼울 수 있어야 한다.
                Direction choice = RandomUtil.Pick(_candidates);
                ghost.DirX = choice.X;
                ghost.DirY = choice.Y;

                // ★ 원본 이상 A2 — 방향을 바꾼 프레임에는 «움직이지 않는다» (실측: 400프레임에 14회).
                return;
            }

            ghost.X = newX;
            ghost.Y = newY;

            // ★ 원본 이상 A4 — 유령은 «이동 뒤»에 랩한다. 팩맨은 벽 판정 «앞»이다.
            if (ghost.X < 0f)
                ghost.X = _maze.Cols - 1;

            if (ghost.X > _maze.Cols - 1)
                ghost.X = 0f;
        }

        /// <summary>
        /// 원본 <c>checkCollisions</c> (<c>script.js:322~336</c>).
        /// ⚠ <b>유클리드 거리</b> 0.7 이고, <b>한 프레임에 한 번만</b> 처리한다 (원본 <c>break</c>).
        /// </summary>
        private void CheckCollisions()
        {
            for (int i = 0; i < _ghosts.Count; i++)
            {
                float dx = _ghosts[i].X - _pacman.X;
                float dy = _ghosts[i].Y - _pacman.Y;

                if (Mathf.Sqrt(dx * dx + dy * dy) >= _config.CollideDistance)
                    continue;

                _maze.LoseLife();
                OnCaught?.Invoke();
                return;
            }
        }
    }
}
