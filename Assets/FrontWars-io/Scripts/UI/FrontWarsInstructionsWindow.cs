using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 게임 방법 창 — 원본 「설명」 창의 틀(뒤로 · 제목 · 왼쪽 탭 · 오른쪽 카드 2열)과 「단축키」 탭 `[실측] probe/menu_modals.json` (`04_UIUX규칙.md` 「설정 창 · 설명 창」).
    /// <para>탭 여덟 개 — 단축키는 키 카드 목록, 나머지 일곱은 「이름 + 설명」 목록이다. 원본 그림은 안 쓰고 (R11) 글로 대신한다.</para>
    /// </summary>
    public sealed class FrontWarsInstructionsWindow : BaseWindow
    {
        public static readonly WindowKey<FrontWarsInstructionsWindow> Key = new WindowKey<FrontWarsInstructionsWindow>("UI/Window/FrontWarsInstructionsWindow");

        /// <summary>
        /// 카드 순서 — 원본 단축키 탭 순서를 따른다 [실측 probe/menu_modals.json]. 키 표시는 우리 입력(`FrontWarsMapInput`)의 실제 조작이다.
        /// <para>원본 입력 소스: 휠 클릭 = 가까운 건물 업그레이드(누르고 있으면 반복) · Ctrl+클릭 = 보조 동작 · Alt+클릭 = 이모지 · Shift+휠 = 공격 비율 `[소스] BMmXchgl.js:handlePointerUp(e){`.</para>
        /// <para>⚠ 원본의 Ctrl+클릭은 건설 메뉴를 연다 — 우리는 건설 메뉴가 원형 메뉴 「건설」 조각이라, 그 자리에 원형 메뉴를 연다.</para>
        /// </summary>
        public static readonly (string Keys, string TextKey)[] Rows =
        {
            ("Space", "instructions.view"),
            ("H", "instructions.hide_ui"),
            ("W A S D", "instructions.pan"),
            ("C", "instructions.center"),
            ("Q / E", "instructions.zoom"),
            ("T / Y", "instructions.attack_ratio"),
            ("Shift + 휠", "instructions.attack_ratio_wheel"),
            ("Shift + 클릭", "instructions.attack_altclick"),
            ("B", "instructions.boat"),
            ("G", "instructions.ground"),
            ("Ctrl + 클릭", "instructions.ctrl_menu"),
            ("Alt + 클릭", "instructions.emoji"),
            ("휠 클릭", "instructions.upgrade"),
            ("1 ~ 0", "instructions.build"),
            ("우클릭", "instructions.radial"),
            ("왼쪽 클릭", "instructions.click")
        };

        /// <summary>왼쪽 탭 [실측 488,y,228×51 · 간격 55 · 글자 538,y+12 18px 600].</summary>
        public static readonly (string Id, float Y)[] Tabs =
        {
            ("keys", 200f), ("ui", 255f), ("radial", 310f), ("info", 365f),
            ("alliance", 420f), ("build", 475f), ("units", 530f), ("icons", 585f)
        };

        /// <summary>한 탭에 들어가는 항목 수 [미측정 — 원본은 탭마다 다르다. 우리는 열 자리를 두고 빈 자리는 숨긴다].</summary>
        public const int ItemsPerTab = 10;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _heading;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Image _closeIcon;
        [SerializeField] private TMP_Text[] _rowKeys;
        [SerializeField] private TMP_Text[] _rowTexts;
        [SerializeField] private GameObject _keysRoot;
        [SerializeField] private Button[] _tabButtons;
        [SerializeField] private Image[] _tabHighlights;
        [SerializeField] private TMP_Text[] _tabLabels;
        [SerializeField] private GameObject _itemsRoot;
        [SerializeField] private GameObject[] _itemRoots;
        [SerializeField] private TMP_Text[] _itemNames;
        [SerializeField] private TMP_Text[] _itemDescs;

        private Func<string, string> _text;
        private int _tab;

        public event Action OnClose;

        public int CurrentTab => _tab;

        public void Show(Func<string, string> text)
        {
            _text = text;
            FrontWarsFonts.ApplyTo(gameObject);
            _title.text = text("instructions.title");
            _closeIcon.sprite = FrontWarsIconFactory.GetUi(FrontWarsIconFactory.EUiIcon.Back);

            for (int i = 0; i < _tabLabels.Length && i < Tabs.Length; i++)
                _tabLabels[i].text = text($"instructions.tab.{Tabs[i].Id}");

            for (int i = 0; i < _rowKeys.Length && i < Rows.Length; i++)
            {
                _rowKeys[i].text = Rows[i].Keys;
                _rowTexts[i].text = text(Rows[i].TextKey);
            }

            SetTab(0);
        }

        /// <summary>탭을 바꾼다 — 단축키 탭만 키 카드, 나머지는 「이름 + 설명」 목록.</summary>
        public void SetTab(int index)
        {
            if (index < 0 || index >= Tabs.Length)
                return;

            _tab = index;
            string id = Tabs[index].Id;
            bool keys = id == "keys";

            for (int i = 0; i < _tabHighlights.Length; i++)
                _tabHighlights[i].enabled = i == index;

            _heading.text = _text($"instructions.{id}.title");
            _keysRoot.SetActive(keys);
            _itemsRoot.SetActive(keys == false);

            if (keys)
                return;

            for (int i = 0; i < _itemRoots.Length; i++)
            {
                string nameKey = $"instructions.{id}.item{i + 1}.name";
                string descKey = $"instructions.{id}.item{i + 1}.desc";
                string desc = _text(descKey);
                bool used = string.IsNullOrEmpty(desc) == false && desc != descKey;

                _itemRoots[i].SetActive(used);

                if (used == false)
                    continue;

                string itemName = _text(nameKey);
                bool hasName = string.IsNullOrEmpty(itemName) == false && itemName != nameKey;
                _itemNames[i].gameObject.SetActive(hasName);
                _itemNames[i].text = hasName ? itemName : string.Empty;
                _itemDescs[i].text = desc;
            }
        }

        protected override void OnOpened()
        {
            _closeButton.onClick.AddListener(HandleClose);

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                _tabButtons[i].onClick.AddListener(() => SetTab(index));
            }
        }

        protected override void OnClosing()
        {
            _closeButton.onClick.RemoveListener(HandleClose);

            for (int i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].onClick.RemoveAllListeners();
        }

        private void HandleClose() => OnClose?.Invoke();
    }
}
