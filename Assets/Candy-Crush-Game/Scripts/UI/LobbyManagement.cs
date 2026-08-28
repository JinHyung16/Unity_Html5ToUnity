using JinHyung.Core;
using JinHyung.UI;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 모드 선택 화면을 조종한다.
    ///
    /// <para>
    /// 창은 <c>event</c> 로 위에 알리고, 여기가 받아 Manager 를 부른다 —
    /// <b>창이 Manager 를 직접 부르지 않는다.</b>
    /// </para>
    /// </summary>
    public class LobbyManagement : BaseManagement
    {
        protected override void AddWindows()
        {
            RegisterWindow(ModeSelectWindow.Key);
        }

        protected override void OnInitialize()
        {
            ModeSelectWindow window = GetWindow(ModeSelectWindow.Key);

            if (window != null)
                window.OnModeSelected += HandleModeSelected;

            CandyGameManager.Instance.GameFlow.OnScreenChanged += HandleScreenChanged;
        }

        protected override void OnDispose()
        {
            ModeSelectWindow window = GetWindow(ModeSelectWindow.Key);

            if (window != null)
                window.OnModeSelected -= HandleModeSelected;

            if (CandyGameManager.HasInstance)
                CandyGameManager.Instance.GameFlow.OnScreenChanged -= HandleScreenChanged;
        }

        private void HandleScreenChanged(EGameScreenType previous, EGameScreenType next)
        {
            if (next == EGameScreenType.ModeSelect)
            {
                ModeSelectWindow window = GetWindow(ModeSelectWindow.Key);

                if (window != null)
                    window.Open();

                return;
            }

            CloseAllWindows();
        }

        private void HandleModeSelected(EGameModeType mode)
        {
            CandyGameManager.Instance.ModeMgr.StartGame(mode);
        }
    }
}
