using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 새 무기 해금 — <b>마법사 퀘스트 보상</b> [소스 직독 · <c>rewardLightningWeapon</c>].
    ///
    /// <para>
    /// 구성 [소스] — 제목 <c>taskReward</c>(18) · 부제 <c>newWeaponLightning</c>(24 · 흰색 · bold) ·
    /// <c>icon_lightning</c> 아이콘(배율 4) · 버튼 <c>ok</c>(160×48).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>이 창이 떠 있는 동안 게임이 멈춘다</b> [소스 — <c>tickerId="pause"</c>].
    /// 「확인」을 누르면 다시 흐른다.
    /// </para>
    /// </summary>
    public sealed class UndeadWeaponUnlockWindow : BaseWindow
    {
        public static readonly WindowKey<UndeadWeaponUnlockWindow> Key =
            new WindowKey<UndeadWeaponUnlockWindow>("UI/Window/UndeadWeaponUnlockWindow");

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _subtitleText;
        [SerializeField] private Button _okButton;
        [SerializeField] private TMP_Text _okText;

        /// <summary>「확인」을 눌렀다.</summary>
        public event Action OnConfirmed;

        public void Show(string title, string subtitle, string okLabel)
        {
            if (_titleText != null)
                _titleText.text = title;

            if (_subtitleText != null)
                _subtitleText.text = subtitle;

            if (_okText != null)
                _okText.text = okLabel;
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            if (_okButton == null)
                return;

            _okButton.onClick.RemoveAllListeners();
            _okButton.onClick.AddListener(() => OnConfirmed?.Invoke());
        }

        /// <summary>⚠ 연 곳에서 건 것을 <b>여기서 반드시 끊는다</b>.</summary>
        protected override void OnClosing()
        {
            if (_okButton != null)
                _okButton.onClick.RemoveAllListeners();

            base.OnClosing();
        }
    }
}
