using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>판 끝 — 「○○가 승리했습니다!」 · 승리/패배 · 게임 종료 · 관전 (`04_UIUX규칙.md` 「판 끝 창」).</summary>
    public sealed class FrontWarsGameOverWindow : BaseWindow
    {
        public static readonly WindowKey<FrontWarsGameOverWindow> Key = new WindowKey<FrontWarsGameOverWindow>("UI/Window/FrontWarsGameOverWindow");

        /// <summary>아이콘 칸 그림 — 코드로 그린 도형이라 프리팹에 굽지 못해 열 때 넣는다.</summary>
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Button _exitButton;
        [SerializeField] private TMP_Text _exitLabel;
        [SerializeField] private Button _watchButton;
        [SerializeField] private TMP_Text _watchLabel;

        public event Action OnExit;
        public event Action OnKeepWatching;

        public void Show(string title, string message, string exitLabel, string watchLabel)
        {
            FrontWarsFonts.ApplyTo(gameObject);

            if (_icon != null)
                _icon.sprite = FrontWarsIconFactory.Get(FrontWarsIconFactory.EShape.Star);

            _title.text = title;
            _message.text = message;
            _exitLabel.text = exitLabel;
            _watchLabel.text = watchLabel;
        }

        protected override void OnOpened()
        {
            _exitButton.onClick.AddListener(HandleExit);
            _watchButton.onClick.AddListener(HandleWatch);
        }

        protected override void OnClosing()
        {
            _exitButton.onClick.RemoveListener(HandleExit);
            _watchButton.onClick.RemoveListener(HandleWatch);
        }

        private void HandleExit() => OnExit?.Invoke();
        private void HandleWatch() => OnKeepWatching?.Invoke();
    }
}
