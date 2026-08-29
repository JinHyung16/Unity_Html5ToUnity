using JinHyung.Core;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 이 게임의 매니저 소유자. <b>등록 순서 = 초기화 순서 = 매 틱 호출 순서</b>다.
    ///
    /// <para>
    /// ★★ 원본 <c>loop</c> (<c>script.js:455~469</c>) 의 순서가 사양이다 —
    /// <c>movePacman</c> → <c>moveGhost</c>(전부) → <c>checkCollisions</c>.
    /// 그래서 <b>팩맨을 먼저</b> 등록하고, 충돌 검사는 유령 매니저가 <b>이동 뒤에</b> 한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>무작위는 <see cref="RandomUtil"/> 하나에서만 나온다.</b>
    /// 매니저가 난수를 직접 만들면 하네스가 갈아 끼울 수 없어
    /// 골든 벡터도 결정성 검사도 성립하지 않는다.
    /// </para>
    /// </summary>
    public class PacGameManager : BaseGameManager<PacGameManager>
    {
        public GameFlow<EPacScreenType> GameFlow { get; private set; }
        public MazeManager MazeMgr { get; private set; }
        public PacmanManager PacMgr { get; private set; }
        public GhostManager GhostMgr { get; private set; }

        protected override void RegisterManagers()
        {
            GameFlow = new GameFlow<EPacScreenType>();

            // 미로가 먼저다 — 나머지 둘이 미로를 들고 벽을 묻는다.
            MazeMgr = Register(new MazeManager());
            PacMgr = Register(new PacmanManager(MazeMgr));
            GhostMgr = Register(new GhostManager(MazeMgr, PacMgr));

            // 충돌하면 «개체만» 되돌린다. 맵은 그대로다 (원본 resetEntities).
            GhostMgr.OnCaught += HandleCaught;
        }

        /// <summary>원본 <c>restartGame</c> — 점수·목숨·맵·개체 전부 초기화.</summary>
        public void Restart()
        {
            MazeMgr.Restart();
            ResetEntities();
        }

        /// <summary>원본 <c>resetEntities</c> — <b>맵은 건드리지 않는다.</b></summary>
        public void ResetEntities()
        {
            PacMgr.Reset();
            GhostMgr.Reset();
        }

        private void HandleCaught()
        {
            ResetEntities();
        }

        protected override void OnDestroy()
        {
            if (GhostMgr != null)
                GhostMgr.OnCaught -= HandleCaught;

            GameFlow?.Clear();
            GameFlow = null;

            base.OnDestroy();
        }
    }
}
