using JinHyung.Core;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 이 게임의 매니저 소유자. <b>등록 순서 = 초기화 순서 = 매 프레임 호출 순서</b>다.
    /// <para>
    /// 판 시뮬은 원본처럼 100ms 고정 틱으로 돈다(단위 3). 화면 프레임과 틱은 따로다 —
    /// 틱 누적은 <see cref="FrontWarsGameManager"/> 한 곳에만 둔다.
    /// </para>
    /// </summary>
    public class FrontWarsGameRoot : BaseGameManager<FrontWarsGameRoot>
    {
        /// <summary>화면 전이는 <b>여기 하나</b>를 지난다.</summary>
        public GameFlow<EFrontWarsScreenType> GameFlow { get; private set; }

        public FrontWarsGameManager Game { get; private set; }

        protected override void RegisterManagers()
        {
            GameFlow = new GameFlow<EFrontWarsScreenType>();
            Game = Register(new FrontWarsGameManager());
        }

        protected override void OnDestroy()
        {
            GameFlow?.Clear();
            base.OnDestroy();
        }
    }
}
