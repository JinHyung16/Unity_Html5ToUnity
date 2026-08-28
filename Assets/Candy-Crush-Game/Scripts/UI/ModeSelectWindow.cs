using System;
using JinHyung.UI;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 모드 선택 화면. 원본 <c>#modeSelection</c> (<c>index.html:23~27</c> · <c>style.css:71~101</c>).
    ///
    /// <para>
    /// 원본은 <c>position:absolute</c> + <c>z-index:10</c> 으로 <b>전체를 덮는다.</b>
    /// 우리는 밴드로 옮겼다 — 수치를 상수로 옮기지 않고 「A 가 B 위에 뜬다」는 사실만 가져왔다.
    /// </para>
    /// </summary>
    public class ModeSelectWindow : BaseWindow
    {
        public static readonly WindowKey<ModeSelectWindow> Key =
            new WindowKey<ModeSelectWindow>("UI/Window/ModeSelectWindow");

        [SerializeField] private Button _endlessButton;
        [SerializeField] private Button _timedButton;

        /// <summary>위로 올리는 것은 <c>event</c> 로만. 창이 Manager 를 직접 부르지 않는다.</summary>
        public event Action<EGameModeType> OnModeSelected;

        protected override void OnOpened()
        {
            // ⚠ 재사용되는 창이다. 더하기 전에 빼서 «두 번 구독»을 막는다.
            if (_endlessButton != null)
            {
                _endlessButton.onClick.RemoveListener(HandleEndless);
                _endlessButton.onClick.AddListener(HandleEndless);
            }

            if (_timedButton != null)
            {
                _timedButton.onClick.RemoveListener(HandleTimed);
                _timedButton.onClick.AddListener(HandleTimed);
            }
        }

        protected override void OnClosing()
        {
            if (_endlessButton != null)
                _endlessButton.onClick.RemoveListener(HandleEndless);

            if (_timedButton != null)
                _timedButton.onClick.RemoveListener(HandleTimed);
        }

        private void HandleEndless()
        {
            OnModeSelected?.Invoke(EGameModeType.Endless);
        }

        private void HandleTimed()
        {
            OnModeSelected?.Invoke(EGameModeType.Timed);
        }
    }
}
