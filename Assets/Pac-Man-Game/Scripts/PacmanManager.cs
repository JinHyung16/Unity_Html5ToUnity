using System;
using JinHyung.Core;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 팩맨의 이동과 «방향 예약». 원본 <c>handleInput</c> + <c>movePacman</c> 이다.
    ///
    /// <para>
    /// ★★ <b>순서가 사양이다</b> — 입력 반영 → 터널 랩 → 벽 판정 → 이동 → 펠릿.
    /// 순서를 바꾸면 경계 거동이 달라진다 (골든 A1 · A4).
    /// </para>
    /// </summary>
    public class PacmanManager : BaseManager, IGameUpdate
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public int DirX { get; private set; }
        public int DirY { get; private set; }
        public int NextDirX { get; private set; }
        public int NextDirY { get; private set; }

        /// <summary>입 애니메이션용 누적 시각. 원본 <c>gameTime</c> 에 대응한다.</summary>
        public float GameTime { get; private set; }

        public event Action OnMoved;

        private readonly MazeManager _maze;
        private PacConfigData _config;

        public PacmanManager(MazeManager maze)
        {
            _maze = maze;
        }

        protected override void OnInitialize()
        {
            _config = GameRoot.Instance.PacConfigDataContainer?.Data;

            if (_config == null)
                throw new InvalidOperationException("PacConfigTable 을 못 읽었다");
        }

        protected override void OnDispose()
        {
            OnMoved = null;
        }

        /// <summary>원본 <c>resetEntities</c> 의 팩맨 부분 (<c>script.js:171~177</c>).</summary>
        public void Reset()
        {
            X = _config.PacStartCol;
            Y = _config.PacStartRow;
            DirX = 0;
            DirY = 0;
            NextDirX = 0;
            NextDirY = 0;
            OnMoved?.Invoke();
        }

        /// <summary>
        /// 원본 <c>trySetDirection</c> (<c>script.js:208~211</c>) — <b>예약만 한다.</b>
        /// 실제 전환은 <c>HandleInput</c> 이 정렬을 보고 결정한다.
        /// </summary>
        public void SetNextDirection(int dx, int dy)
        {
            NextDirX = dx;
            NextDirY = dy;
        }

        public void OnUpdate(float deltaTime)
        {
            if (_maze.IsGameOver)
                return;

            GameTime += deltaTime;
            Move(deltaTime);
        }

        /// <summary>
        /// 원본 <c>handleInput</c> (<c>script.js:213~232</c>).
        ///
        /// <para>
        /// ⚠ <b>정렬 창이 0.35 다.</b> 칸 중심에서 그 안이면 전환을 허용한다 —
        /// 엄밀한 격자 정렬을 요구하지 않는 것이 <b>이 게임의 조작감</b>이다.
        /// </para>
        ///
        /// <para>⚠ <b>멈춰 있으면 정렬과 무관하게 허용</b>한다 (첫 입력이 먹히게).</para>
        /// </summary>
        private void HandleInput()
        {
            int centerCol = Mathf.RoundToInt(X);
            int centerRow = Mathf.RoundToInt(Y);

            float offsetX = Mathf.Abs(X - centerCol);
            float offsetY = Mathf.Abs(Y - centerRow);

            bool aligned = offsetX < _config.AlignWindow && offsetY < _config.AlignWindow;
            bool stopped = DirX == 0 && DirY == 0;

            if (aligned == false && stopped == false)
                return;

            int targetCol = centerCol + NextDirX;
            int targetRow = centerRow + NextDirY;

            if (_maze.IsWall(targetCol, targetRow))
                return;

            DirX = NextDirX;
            DirY = NextDirY;
        }

        /// <summary>원본 <c>movePacman</c> (<c>script.js:234~275</c>).</summary>
        private void Move(float deltaTime)
        {
            HandleInput();

            float speedPerFrame = _config.PacSpeed * deltaTime;
            float newX = X + DirX * speedPerFrame;
            float newY = Y + DirY * speedPerFrame;

            // ★ 터널 랩 — 원본은 «벽 판정 앞»에서 한다. 가로만 랩한다 (골든 A4).
            //   ⚠ 유령은 «이동 뒤»에 랩한다 — 순서가 다르다. 합치면 한쪽이 어긋난다.
            if (newX < 0f)
                newX = _maze.Cols - 1;

            if (newX > _maze.Cols - 1)
                newX = 0f;

            int nextCol = Mathf.RoundToInt(newX);
            int nextRow = Mathf.RoundToInt(newY);

            if (_maze.IsWall(nextCol, nextRow))
            {
                // ★ 원본 이상 A1 — 되돌리는 것이 아니라 «벽 칸에서 진행 방향만큼 뺀 자리»로 옮긴다.
                //   실측: x 26.4000 → 26.0000 · dir (1,0) → (0,0)
                int dx = DirX;
                int dy = DirY;

                DirX = 0;
                DirY = 0;
                X = nextCol - dx;
                Y = nextRow - dy;

                OnMoved?.Invoke();
                return;
            }

            X = newX;
            Y = newY;

            // ⚠ 이동 여부와 무관하게 «현재 칸»을 먹는다 (골든 V1).
            _maze.TryEat(Mathf.RoundToInt(X), Mathf.RoundToInt(Y));

            OnMoved?.Invoke();
        }
    }
}
