using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 과제 완료 — <b>퀘스트 넷을 다 마친 「한 판의 끝」</b>.
    ///
    /// <para>
    /// 문구 [실측 문구표] — 제목 <c>taskCompletionTitle</c>(「과제 완료!」) ·
    /// 본문 <c>taskCompletionMessage</c>(「사원으로 돌아가거나 계속 진행하세요」) ·
    /// 버튼 <c>toTemple</c> / <c>continueRun</c>.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>「사원으로」의 목적지(로비)는 이관 범위 밖</b>이다 — 누르면 <b>대기 화면</b>으로 되돌린다.
    /// 의도된 차이로 등재했다. 「계속」은 원본대로 그 판을 이어서 한다.
    /// </para>
    /// </summary>
    public sealed class UndeadTaskCompleteWindow : BaseWindow
    {
        public static readonly WindowKey<UndeadTaskCompleteWindow> Key =
            new WindowKey<UndeadTaskCompleteWindow>("UI/Window/UndeadTaskCompleteWindow");

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _continueButton;
        [SerializeField] private TMP_Text _continueText;
        [SerializeField] private Button _templeButton;
        [SerializeField] private TMP_Text _templeText;

        /// <summary>「계속」 — 그 판을 이어서 한다.</summary>
        public event Action OnContinue;

        /// <summary>「사원으로」 — 한 판을 끝낸다.</summary>
        public event Action OnTemple;

        public void Show(string title, string message, string continueLabel, string templeLabel)
        {
            if (_titleText != null)
                _titleText.text = title;

            if (_messageText != null)
                _messageText.text = message;

            if (_continueText != null)
                _continueText.text = continueLabel;

            if (_templeText != null)
                _templeText.text = templeLabel;
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.onClick.AddListener(() => OnContinue?.Invoke());
            }

            if (_templeButton != null)
            {
                _templeButton.onClick.RemoveAllListeners();
                _templeButton.onClick.AddListener(() => OnTemple?.Invoke());
            }
        }

        /// <summary>⚠ 연 곳에서 건 것을 <b>여기서 반드시 끊는다</b>.</summary>
        protected override void OnClosing()
        {
            if (_continueButton != null)
                _continueButton.onClick.RemoveAllListeners();

            if (_templeButton != null)
                _templeButton.onClick.RemoveAllListeners();

            base.OnClosing();
        }
    }
}
