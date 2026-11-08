using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 메뉴 — 이름 입력 · 싱글 플레이 · 튜토리얼 · 게임 방법 (`01_게임플로우.md` 「메뉴」). 온라인 버튼(로비 · 순위표 · 로그인)은 범위 밖이라 없다.
    /// </summary>
    public sealed class FrontWarsMenuWindow : BaseWindow
    {
        public static readonly WindowKey<FrontWarsMenuWindow> Key = new WindowKey<FrontWarsMenuWindow>("UI/Window/FrontWarsMenuWindow");

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private Button _singleplayerButton;
        [SerializeField] private TMP_Text _singleplayerLabel;
        [SerializeField] private Button _tutorialButton;
        [SerializeField] private TMP_Text _tutorialLabel;
        [SerializeField] private Button _instructionsButton;
        [SerializeField] private TMP_Text _instructionsLabel;
        [SerializeField] private TMP_Text _namePlaceholder;

        /// <summary>지도 데이터 저작자 표시 — CC BY-SA 4.0 의무 (`Data/Maps/ATTRIBUTION.md`). 원본 화면에 없는 줄이다 (의도된 차이).</summary>
        [SerializeField] private TMP_Text _mapCredit;

        public event Action OnSingleplayer;
        public event Action OnTutorial;
        public event Action OnInstructions;
        public event Action<string> OnNameChanged;

        public string PlayerName => _nameInput != null ? _nameInput.text : string.Empty;

        public void Show(Func<string, string> text, string playerName)
        {
            FrontWarsFonts.ApplyTo(gameObject);
            _title.text = text("menu.title");
            _singleplayerLabel.text = text("menu.singleplayer");
            _tutorialLabel.text = text("menu.tutorial");
            _instructionsLabel.text = text("menu.instructions");
            _namePlaceholder.text = text("menu.name_placeholder");
            _mapCredit.text = text("menu.map_credit");
            _nameInput.SetTextWithoutNotify(playerName ?? string.Empty);
        }

        protected override void OnOpened()
        {
            _singleplayerButton.onClick.AddListener(HandleSingleplayer);
            _tutorialButton.onClick.AddListener(HandleTutorial);
            _instructionsButton.onClick.AddListener(HandleInstructions);
            _nameInput.onEndEdit.AddListener(HandleName);
        }

        protected override void OnClosing()
        {
            _singleplayerButton.onClick.RemoveListener(HandleSingleplayer);
            _tutorialButton.onClick.RemoveListener(HandleTutorial);
            _instructionsButton.onClick.RemoveListener(HandleInstructions);
            _nameInput.onEndEdit.RemoveListener(HandleName);
        }

        private void HandleSingleplayer() => OnSingleplayer?.Invoke();
        private void HandleTutorial() => OnTutorial?.Invoke();
        private void HandleInstructions() => OnInstructions?.Invoke();
        private void HandleName(string value) => OnNameChanged?.Invoke(value);
    }
}
