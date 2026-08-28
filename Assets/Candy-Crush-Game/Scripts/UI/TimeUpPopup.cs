using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// Timed 종료 알림. 원본 <c>alert()</c> (<c>script.js:228</c>) 의 대체다.
    ///
    /// <para>
    /// ⚠ <b>이건 「원본에 없는 창」이다.</b> 브라우저 모달에 대응물이 없어서 만든 유일한 예외이고,
    /// 그래서 <b>플로우 diff 에서 예외로 등재</b>돼 있다 (<c>HtmlToUnityLogic/01_게임플로우.md</c>).
    /// 최소 팝업이다 — 문구는 원본 그대로, 버튼은 확인 하나, <b>재시작 버튼을 만들지 않는다.</b>
    /// 닫으면 보드는 잠긴 채 남고 빠져나가는 경로는 <c>[Change Mode]</c> 하나뿐이다.
    /// </para>
    /// </summary>
    public class TimeUpPopup : BaseWindow
    {
        public static readonly WindowKey<TimeUpPopup> Key =
            new WindowKey<TimeUpPopup>("UI/Window/TimeUpPopup");

        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _confirmButton;

        public event Action OnConfirmed;

        /// <summary>문구는 <c>GameModeManager.BuildGameOverText</c> 가 만든다 — <b>바이트 단위로 원본과 같다.</b></summary>
        public void SetData(string message)
        {
            if (_messageText != null)
                _messageText.text = message;
        }

        protected override void OnOpened()
        {
            if (_confirmButton == null)
                return;

            _confirmButton.onClick.RemoveListener(HandleConfirm);
            _confirmButton.onClick.AddListener(HandleConfirm);
        }

        protected override void OnClosing()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(HandleConfirm);
        }

        private void HandleConfirm()
        {
            OnConfirmed?.Invoke();
        }
    }
}
