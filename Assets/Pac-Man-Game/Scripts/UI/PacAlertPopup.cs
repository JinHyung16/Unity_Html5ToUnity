using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 원본 <c>alert()</c> 대응 (<c>script.js:272</c> · <c>331</c>) — 첫 게임과 같은 방식이다.
    ///
    /// <para>
    /// ⚠ 원본은 OS 모달이라 <b>보드를 숨기지 않는다.</b> 우리도 게임 화면 위에 얹기만 한다.
    /// 닫아도 게임은 끝난 상태 그대로다 — <b>빠져나가는 경로는 Restart 하나뿐</b>이다 (원본과 같다).
    /// </para>
    ///
    /// <para>
    /// ⚠ 원본 클리어 문구에는 <c>🎉</c> 가 있다. 폰트에 그 글리프가 없어 두부로 뜨므로
    /// <b>이모지만 뗀다</b> — 「의도된 차이」로 등재돼 있다.
    /// </para>
    /// </summary>
    public class PacAlertPopup : BaseWindow
    {
        public static readonly WindowKey<PacAlertPopup> Key =
            new WindowKey<PacAlertPopup>("UI/Window/PacAlertPopup");

        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _confirmButton;

        public event Action OnConfirmed;

        protected override void OnOpened()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(HandleConfirm);
        }

        protected override void OnClosing()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(HandleConfirm);

            OnConfirmed = null;
        }

        public void SetMessage(string message)
        {
            if (_messageText != null)
                _messageText.text = message;
        }

        private void HandleConfirm()
        {
            OnConfirmed?.Invoke();
            Close();
        }
    }
}
