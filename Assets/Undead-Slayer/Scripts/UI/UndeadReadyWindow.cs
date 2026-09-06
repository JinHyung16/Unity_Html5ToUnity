using System;
using JinHyung.UI;
using JinHyung.UI.Fx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 바이옴 진입 화면 — <b>「시작」 버튼 하나</b> [소스 <c>startGame</c> 게이트].
    ///
    /// <para>
    /// ★ 원본은 바이옴에 들어오면 <b>화면 전체를 검게 반투명(α 0.5)으로 덮고</b> 가운데 아래(높이의 75%)에
    /// 초록 「시작」 버튼이 뜬다. 누르기 «전»에는 타이머가 <c>00:00</c> 이고 적도 안 나온다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>버튼은 «항상» 숨쉰다</b> [소스] — <c>scale = hover × (1 + 0.5·(sin(t·0.005)+1)·0.13)</c>.
    /// 식과 값은 프리팹의 <see cref="UiScalePulse"/>(SinePulse) · <see cref="UiHoverScale"/> 이 들고
    /// <see cref="UiScaleStack"/> 이 곱한다. 이 창은 «열릴 때 처음부터 숨쉬게» 하고 누름만 받는다.
    /// </para>
    ///
    /// <para>⚠ <c>pointerdown</c> 에 바로 시작한다 — 떼는 것을 기다리지 않는다.</para>
    /// </summary>
    public sealed class UndeadReadyWindow : BaseWindow
    {
        public static readonly WindowKey<UndeadReadyWindow> Key =
            new WindowKey<UndeadReadyWindow>("UI/Window/UndeadReadyWindow");

        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _startText;
        [SerializeField] private UiScalePulse _pulse;

        /// <summary>「시작」을 눌렀다.</summary>
        public event Action OnStart;

        private bool _started;

        public void SetLabel(string label)
        {
            if (_startText != null)
                _startText.text = label;
        }

        /// <summary>지금 버튼 배율 — 검사가 «숨쉬는지» 잰다.</summary>
        public float CurrentButtonScale
        {
            get { return _startButton != null ? _startButton.transform.localScale.x : 1f; }
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            _started = false;

            if (_pulse != null)
                _pulse.Restart();   // [소스] 열릴 때 pulseElapsedMs = 0

            if (_startButton == null)
                return;

            _startButton.onClick.RemoveAllListeners();
            _startButton.onClick.AddListener(Press);
        }

        /// <summary>⚠ 연 곳에서 건 것을 <b>여기서 반드시 끊는다</b>.</summary>
        protected override void OnClosing()
        {
            if (_startButton != null)
                _startButton.onClick.RemoveAllListeners();

            base.OnClosing();
        }

        private void Press()
        {
            if (_started)
                return;

            _started = true;
            OnStart?.Invoke();
        }
    }
}
