using JinHyung.Core;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 이 게임의 매니저 소유자. <b>등록 순서 = 초기화 순서 = 매 틱 호출 순서</b>다.
    ///
    /// <para>
    /// 지금은 전투 매니저 하나다 — CLAUDE.md 「그 게임이 실제로 부르는 것만 만든다」.
    /// 과제·스킬이 이관 범위에 들어오면 그때 늘린다.
    /// </para>
    /// </summary>
    public class UndeadGameRoot : BaseGameManager<UndeadGameRoot>
    {
        /// <summary>화면 전이는 <b>여기 하나</b>를 지난다. 흩어지면 플로우 diff 를 못 돌린다.</summary>
        public GameFlow<EUndeadScreenType> GameFlow { get; private set; }

        public UndeadGameManager Game { get; private set; }

        protected override void RegisterManagers()
        {
            GameFlow = new GameFlow<EUndeadScreenType>();
            Game = Register(new UndeadGameManager());
        }
    }
}
