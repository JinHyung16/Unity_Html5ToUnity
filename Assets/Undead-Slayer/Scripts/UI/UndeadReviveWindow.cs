using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 사망 — <b>「부활?」 + 카운트다운 + 「부활」 / 「아니요」</b> [실측].
    ///
    /// <para>
    /// 실측 배치 (세로 580 기준) — 제목 「부활?」 중심 y 167 · 큰 숫자 y 222(높이 69) ·
    /// 「부활」 y 333 · 「아니요」 y 401. 카운트다운은 <b>8</b> 부터 [실측].
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>「부활」의 «대가»는 미측정</b>이다 — 원본이 광고를 요구하는지 재화를 쓰는지 안 봤다.
    /// 지금은 <b>누르면 그 자리에서 되살아난다</b>로 두고 그 사실을 등재한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 카운트다운이 0 이 되면 원본이 무엇을 하는지도 <b>미측정</b>이다 —
    /// 우리는 「아니요」와 같게(한 판 종료) 둔다.
    /// </para>
    /// </summary>
    public sealed class UndeadReviveWindow : BaseWindow
    {
        public static readonly WindowKey<UndeadReviveWindow> Key =
            new WindowKey<UndeadReviveWindow>("UI/Window/UndeadReviveWindow");

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private Button _reviveButton;
        [SerializeField] private TMP_Text _reviveText;
        [SerializeField] private Button _declineButton;
        [SerializeField] private TMP_Text _declineText;

        /// <summary>부활을 골랐다.</summary>
        public event Action OnRevive;

        /// <summary>부활하지 않기로 했다 — 「아니요」 또는 <b>카운트다운 종료</b>.</summary>
        public event Action OnDecline;

        private double _remaining;
        private bool _counting;

        /// <summary>창을 세운다. <paramref name="seconds"/> 는 실측 <b>8</b> 이다.</summary>
        public void Show(string title, string reviveLabel, string declineLabel, int seconds)
        {
            if (_titleText != null)
                _titleText.text = title;

            if (_reviveText != null)
                _reviveText.text = reviveLabel;

            if (_declineText != null)
                _declineText.text = declineLabel;

            _remaining = seconds;
            _counting = true;
            UpdateCountdown();
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            if (_reviveButton != null)
            {
                _reviveButton.onClick.RemoveAllListeners();
                _reviveButton.onClick.AddListener(() => { _counting = false; OnRevive?.Invoke(); });
            }

            if (_declineButton != null)
            {
                _declineButton.onClick.RemoveAllListeners();
                _declineButton.onClick.AddListener(() => { _counting = false; OnDecline?.Invoke(); });
            }
        }

        protected override void OnClosing()
        {
            _counting = false;

            if (_reviveButton != null)
                _reviveButton.onClick.RemoveAllListeners();

            if (_declineButton != null)
                _declineButton.onClick.RemoveAllListeners();

            base.OnClosing();
        }

        // [소스] onButtonEnter → scale 1.02 · onButtonLeave → 1 → 버튼의 UiHoverScale 이 든다 (프리팹)

        private void Update()
        {
            if (_counting == false)
                return;

            _remaining -= Time.deltaTime;
            UpdateCountdown();

            if (_remaining > 0.0)
                return;

            _counting = false;
            OnDecline?.Invoke();
        }

        private void UpdateCountdown()
        {
            if (_countdownText == null)
                return;

            _countdownText.text = Mathf.Max(0, Mathf.CeilToInt((float)_remaining)).ToString();
        }
    }
}
