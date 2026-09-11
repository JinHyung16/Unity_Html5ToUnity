using JinHyung.Core;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 이 게임의 매니저 소유자. <b>등록 순서 = 초기화 순서 = 매 틱 호출 순서</b>다.
    ///
    /// <para>
    /// 전투와 <b>로비</b> 둘이다 — 원본도 두 씬이다 (<c>isLobbyUnlocked</c> 가 어느 쪽으로 들어갈지 고른다).
    /// </para>
    /// </summary>
    public class UndeadGameRoot : BaseGameManager<UndeadGameRoot>
    {
        /// <summary>화면 전이는 <b>여기 하나</b>를 지난다. 흩어지면 플로우 diff 를 못 돌린다.</summary>
        public GameFlow<EUndeadScreenType> GameFlow { get; private set; }

        public UndeadGameManager Game { get; private set; }

        /// <summary>로비 — <b>전투와 같은 씬</b>이지만 도는 루프가 다르다.</summary>
        public UndeadLobbyManager Lobby { get; private set; }

        protected override void RegisterManagers()
        {
            GameFlow = new GameFlow<EUndeadScreenType>();
            Game = Register(new UndeadGameManager());
            Lobby = Register(new UndeadLobbyManager());
        }
    }
}
