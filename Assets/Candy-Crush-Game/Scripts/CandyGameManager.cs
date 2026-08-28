using System;
using JinHyung.Core;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 이 게임의 매니저 소유자. <b>등록 순서 = 초기화 순서 = 매 틱 호출 순서</b>다.
    ///
    /// <para>
    /// 원본은 <c>gameLoop</c> 하나와 타이머 하나가 각각 <c>setInterval</c> 로 돌았다.
    /// 우리는 <see cref="BaseGameManager{T}"/> 하나가 둘을 <b>등록 순서대로</b> 돌린다 —
    /// 매니저마다 <c>Update</c> 를 두면 순서가 유니티 손에 넘어간다.
    /// </para>
    /// </summary>
    public class CandyGameManager : BaseGameManager<CandyGameManager>
    {
        public GameFlow<EGameScreenType> GameFlow { get; private set; }
        public BoardManager BoardMgr { get; private set; }
        public GameModeManager ModeMgr { get; private set; }

        /// <summary>
        /// ⚠ <b>주입식 RNG.</b> 채점·재현이 필요하면 여기 시드를 넣는다.
        /// 매니저 안에서 <c>UnityEngine.Random</c> 을 직접 부르지 않는다.
        /// </summary>
        public Random RandomSource { get; set; }

        protected override void RegisterManagers()
        {
            GameFlow = new GameFlow<EGameScreenType>();

            // 보드가 먼저다 — 모드 매니저가 보드를 들고 시작·정지를 시킨다.
            BoardMgr = Register(new BoardManager(RandomSource ?? new Random()));
            ModeMgr = Register(new GameModeManager(BoardMgr));
        }

        protected override void OnDestroy()
        {
            GameFlow?.Clear();
            GameFlow = null;

            base.OnDestroy();
        }
    }
}
