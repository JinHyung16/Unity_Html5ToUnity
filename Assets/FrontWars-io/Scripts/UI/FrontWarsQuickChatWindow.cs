using System;
using System.Collections.Generic;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>퀵 채팅 한 묶음 — 카테고리 하나와 그 안의 문구 키들 `[소스] ko.json:quick_chat.phrases.<카테고리>.<키>`.</summary>
    public sealed class FrontWarsQuickChatCategory
    {
        public string Id;
        public string Label;
        public List<string> Keys = new List<string>();
        public List<string> Phrases = new List<string>();
    }

    /// <summary>
    /// 퀵 채팅 창 — 정보 판 「퀵 채팅」 버튼이 연다 `[소스] QPQdQ6Sy.js:(0,Z.jsx)(ka,{open:f,onClose:()=>m(!1),onSend:me,recipientId:i?.id()})`.
    /// <list type="bullet">
    /// <item>폭 900. 카테고리(6) → 문구 → 문구에 「[P1]」 이 있으면 플레이어 고르기 → 미리 보기 → 전송.</item>
    /// <item>보내는 값은 「카테고리.키」 와 (고른) 플레이어 id 다. 보내면 창이 닫힌다.</item>
    /// <item>처음 카테고리는 「도움」 · 플레이어 목록은 살아 있는 사람을 이름순으로 보여 준다.</item>
    /// </list>
    /// <para>의도된 차이: 원본은 플레이어 목록을 스크롤로 다 보여 준다. 우리는 검색으로 좁혀 <see cref="PlayerCapacity"/> 명까지 보여 준다.</para>
    /// </summary>
    public sealed class FrontWarsQuickChatWindow : BaseWindow
    {
        public static readonly WindowKey<FrontWarsQuickChatWindow> Key = new WindowKey<FrontWarsQuickChatWindow>("UI/Window/FrontWarsQuickChatWindow");

        public const float WindowWidth = 900f;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;
        [SerializeField] private TMP_Text _categoryTitle;
        [SerializeField] private TMP_Text _phraseTitle;
        [SerializeField] private TMP_Text _playerTitle;
        [SerializeField] private Button[] _categoryButtons;
        [SerializeField] private TMP_Text[] _categoryLabels;
        [SerializeField] private Button[] _phraseButtons;
        [SerializeField] private TMP_Text[] _phraseLabels;
        [SerializeField] private TMP_InputField _playerSearch;
        [SerializeField] private TMP_Text _playerPlaceholder;
        [SerializeField] private Button[] _playerButtons;
        [SerializeField] private TMP_Text[] _playerLabels;
        [SerializeField] private TMP_Text _preview;
        [SerializeField] private Button _sendButton;
        [SerializeField] private TMP_Text _sendLabel;
        [SerializeField] private Button _closeButton;

        private static readonly Color SelectedColor = new Color32(59, 130, 246, 255);
        private static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.08f);

        private readonly List<(string Id, string Name)> _players = new List<(string, string)>();
        private readonly List<(string Id, string Name)> _shown = new List<(string, string)>();
        private IReadOnlyList<FrontWarsQuickChatCategory> _categories;
        private Func<string, string> _text;
        private int _category;
        private int _phrase = -1;
        private string _playerId;

        public int CategoryCapacity => _categoryButtons.Length;
        public int PhraseCapacity => _phraseButtons.Length;
        public int PlayerCapacity => _playerButtons.Length;

        /// <summary>고른 문구 키(「카테고리.키」)와 대상 플레이어 id (없으면 null).</summary>
        public event Action<string, string> OnSend;

        public void Show(Func<string, string> text, IReadOnlyList<FrontWarsQuickChatCategory> categories, IReadOnlyList<(string Id, string Name)> players)
        {
            FrontWarsFonts.ApplyTo(gameObject);
            _text = text;
            _categories = categories;
            _players.Clear();
            _players.AddRange(players);
            _category = 0;
            _phrase = -1;
            _playerId = null;

            _title.text = text("quick_chat.title");
            _subtitle.text = text("quick_chat.build_your_message");
            _categoryTitle.text = text("quick_chat.category");
            _phraseTitle.text = text("quick_chat.phrase");
            _playerTitle.text = text("quick_chat.player");
            _playerPlaceholder.text = text("quick_chat.search_player");
            _sendLabel.text = text("quick_chat.send");
            _playerSearch.SetTextWithoutNotify(string.Empty);

            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                bool used = i < categories.Count;
                _categoryButtons[i].gameObject.SetActive(used);

                if (used)
                    _categoryLabels[i].text = categories[i].Label;
            }

            Refresh();
        }

        /// <summary>고른 문구 — 아직 없으면 null.</summary>
        public string SelectedKey => _phrase < 0 || _categories == null ? null : $"{_categories[_category].Id}.{_categories[_category].Keys[_phrase]}";

        /// <summary>문구에 플레이어 자리가 있나 (「[P1]」 → 문구 표에서는 「{0}」).</summary>
        public bool NeedsPlayer => _phrase >= 0 && _categories[_category].Phrases[_phrase].Contains("{0}");

        private void Refresh()
        {
            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                if (_categoryButtons[i].gameObject.activeSelf)
                    _categoryButtons[i].targetGraphic.color = i == _category ? SelectedColor : IdleColor;
            }

            FrontWarsQuickChatCategory category = _categories[_category];

            for (int i = 0; i < _phraseButtons.Length; i++)
            {
                bool used = i < category.Keys.Count;
                _phraseButtons[i].gameObject.SetActive(used);

                if (used == false)
                    continue;

                _phraseLabels[i].text = category.Phrases[i];
                _phraseButtons[i].targetGraphic.color = i == _phrase ? SelectedColor : IdleColor;
            }

            RefreshPlayers();

            _preview.text = PreviewText();
            _sendButton.interactable = _phrase >= 0 && (NeedsPlayer == false || _playerId != null);
        }

        private void RefreshPlayers()
        {
            bool needs = NeedsPlayer;
            _playerTitle.transform.parent.gameObject.SetActive(needs);
            _shown.Clear();

            if (needs == false)
                return;

            string filter = _playerSearch.text;

            foreach (var player in _players)
            {
                if (string.IsNullOrEmpty(filter) == false && player.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                _shown.Add(player);

                if (_shown.Count >= _playerButtons.Length)
                    break;
            }

            for (int i = 0; i < _playerButtons.Length; i++)
            {
                bool used = i < _shown.Count;
                _playerButtons[i].gameObject.SetActive(used);

                if (used == false)
                    continue;

                _playerLabels[i].text = _shown[i].Name;
                _playerButtons[i].targetGraphic.color = _shown[i].Id == _playerId ? SelectedColor : IdleColor;
            }
        }

        private string PreviewText()
        {
            if (_phrase < 0)
                return _text("quick_chat.build_your_message");

            string phrase = _categories[_category].Phrases[_phrase];

            if (NeedsPlayer == false)
                return phrase;

            string name = NameOf(_playerId) ?? _text("quick_chat.player");
            return phrase.Replace("{0}", name);
        }

        private string NameOf(string id)
        {
            foreach (var player in _players)
            {
                if (player.Id == id)
                    return player.Name;
            }

            return null;
        }

        protected override void OnOpened()
        {
            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                int index = i;
                _categoryButtons[i].onClick.AddListener(() => HandleCategory(index));
            }

            for (int i = 0; i < _phraseButtons.Length; i++)
            {
                int index = i;
                _phraseButtons[i].onClick.AddListener(() => HandlePhrase(index));
            }

            for (int i = 0; i < _playerButtons.Length; i++)
            {
                int index = i;
                _playerButtons[i].onClick.AddListener(() => HandlePlayer(index));
            }

            _playerSearch.onValueChanged.AddListener(HandleSearch);
            _sendButton.onClick.AddListener(HandleSend);
            _closeButton.onClick.AddListener(Close);
        }

        protected override void OnClosing()
        {
            foreach (Button button in _categoryButtons)
                button.onClick.RemoveAllListeners();

            foreach (Button button in _phraseButtons)
                button.onClick.RemoveAllListeners();

            foreach (Button button in _playerButtons)
                button.onClick.RemoveAllListeners();

            _playerSearch.onValueChanged.RemoveListener(HandleSearch);
            _sendButton.onClick.RemoveListener(HandleSend);
            _closeButton.onClick.RemoveListener(Close);
        }

        private void HandleCategory(int index)
        {
            if (_categories == null || index >= _categories.Count)
                return;

            _category = index;
            _phrase = -1;
            _playerId = null;
            Refresh();
        }

        private void HandlePhrase(int index)
        {
            _phrase = index;
            _playerId = null;
            Refresh();
        }

        private void HandlePlayer(int index)
        {
            if (index >= _shown.Count)
                return;

            _playerId = _shown[index].Id;
            Refresh();
        }

        private void HandleSearch(string _) => Refresh();

        private void HandleSend()
        {
            string key = SelectedKey;

            if (key == null || (NeedsPlayer && _playerId == null))
                return;

            OnSend?.Invoke(key, NeedsPlayer ? _playerId : null);
            Close();
        }
    }
}
