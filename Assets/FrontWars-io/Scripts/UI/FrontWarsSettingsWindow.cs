using System;
using System.Collections.Generic;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>설정 항목 하나 — 저장 키 · 창 카드 · 적용 대상이 이 이름으로 묶인다.</summary>
    public enum EFrontWarsSetting
    {
        LeftClickMenu,
        AttackRatio,
        Emoji,
        AnonymousNames,
        HideLobbyId,
        UiScale,
        AlertFrame,
        SpecialEffects,
        StructureSprites,
        TerritoryPattern,
        PerformanceOverlay,
        DarkMap
    }

    /// <summary>카드 모양 — 스위치(켜고 끄기) 또는 슬라이더(값).</summary>
    public enum EFrontWarsSettingKind
    {
        Switch,
        Slider
    }

    /// <summary>설정 창 왼쪽 탭.</summary>
    public enum EFrontWarsSettingsTab
    {
        General,
        Keys,
        ThemeMain,
        ThemeBackground,
        ThemeInGame
    }

    /// <summary>카드 한 장 규격 — 자리는 원본 실측, 값 범위는 표에 적는다.</summary>
    public sealed class FrontWarsSettingCard
    {
        public EFrontWarsSetting Setting;
        public EFrontWarsSettingKind Kind;
        public EFrontWarsSettingsTab Tab;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public string TextKey;
        public float Min;
        public float Max;
        public float Default;

        /// <summary>카드 왼쪽에서 제목까지 [실측 — 앞에 그림글자가 붙는 카드는 47, 아니면 17].</summary>
        public float TitleIndent = 17f;

        /// <summary>카드 위에서 제목까지 [실측 — 설명이 한 줄인 높은 카드는 24, 아니면 13].</summary>
        public float TitleTop = 13f;
    }

    /// <summary>
    /// 설정 창 — 원본 「설정」 창 `[실측] probe/menu_modals.json settings` (`04_UIUX규칙.md` 「설정 창 · 설명 창」).
    /// <para>왼쪽 칸에 탭 다섯(일반 · 키 설정 · 테마 메인 · 배경 · 게임 내), 오른쪽 칸에 묶음 제목과 카드가 두 열로 선다.</para>
    /// <para>「로비 ID 숨기기」 는 혼자 하는 판에 로비가 없어 값만 저장한다 (의도된 차이). 테마 메인 · 배경은 우리 테마가 하나뿐이라 고를 것이 없다.</para>
    /// </summary>
    public sealed class FrontWarsSettingsWindow : BaseWindow
    {
        public static readonly WindowKey<FrontWarsSettingsWindow> Key = new WindowKey<FrontWarsSettingsWindow>("UI/Window/FrontWarsSettingsWindow");

        /// <summary>스위치 켜짐 주황 · 꺼짐 흰색 20% · 손잡이 20px 이 좌우로 [실측 52×28 · 손잡이 x +4 / +28].</summary>
        private static readonly Color SwitchOn = new Color(242f / 255f, 149f / 255f, 32f / 255f, 1f);
        private static readonly Color SwitchOff = new Color(1f, 1f, 1f, 0.2f);
        public const float SwitchWidth = 52f;
        public const float SwitchHeight = 28f;
        public const float KnobOffX = 4f;
        public const float KnobOnX = 28f;

        /// <summary>오른쪽 칸 묶음 제목 — 게임플레이 · 인터페이스 · 그래픽 및 효과 [실측 y 210 · 356 · 582].</summary>
        public static readonly (string TextKey, float Y, EFrontWarsSettingsTab Tab)[] Sections =
        {
            ("settings.section.gameplay", 210f, EFrontWarsSettingsTab.General),
            ("settings.section.interface", 356f, EFrontWarsSettingsTab.General),
            ("settings.section.graphics", 582f, EFrontWarsSettingsTab.General)
        };

        /// <summary>왼쪽 탭 [실측 — 묶음 「설정」 200 · 일반 235 · 키 설정 288 · 묶음 「테마」 342 · 메인 387 · 배경 440 · 게임 내 494].</summary>
        public static readonly (EFrontWarsSettingsTab Tab, string TextKey, float Y)[] Tabs =
        {
            (EFrontWarsSettingsTab.General, "settings.tab.general", 235f),
            (EFrontWarsSettingsTab.Keys, "settings.tab.keys", 288f),
            (EFrontWarsSettingsTab.ThemeMain, "settings.tab.theme_main", 387f),
            (EFrontWarsSettingsTab.ThemeBackground, "settings.tab.theme_background", 440f),
            (EFrontWarsSettingsTab.ThemeInGame, "settings.tab.theme_ingame", 494f)
        };

        /// <summary>
        /// 카드 자리 [실측 — 왼쪽 열 x 764 · 오른쪽 열 x 1150 · 너비 376 · 높이 91 또는 71].
        /// <para>값 범위 — 공격 비율 1~100% (기본 20) · 인터페이스 크기 50~150% (기본 100) `[소스] BVamDUD6.js:labelKey:\`attack_ratio\`…min:1,max:100` · `INGAME_UI_SCALE_MIN=.5 … MAX=1.5`.</para>
        /// </summary>
        public static readonly FrontWarsSettingCard[] Cards =
        {
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.LeftClickMenu, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.General, X = 764f, Y = 241f, Width = 376f, Height = 91f, TextKey = "left_click_menu", Default = 0f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.AttackRatio, Kind = EFrontWarsSettingKind.Slider, Tab = EFrontWarsSettingsTab.General, X = 1150f, Y = 241f, Width = 376f, Height = 91f, TextKey = "attack_ratio", Min = 1f, Max = 100f, Default = 20f, TitleIndent = 47f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.Emoji, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.General, X = 764f, Y = 387f, Width = 376f, Height = 71f, TextKey = "emoji", Default = 1f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.AnonymousNames, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.General, X = 1150f, Y = 387f, Width = 376f, Height = 71f, TextKey = "anonymous_names", Default = 0f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.HideLobbyId, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.General, X = 764f, Y = 468f, Width = 376f, Height = 91f, TextKey = "hide_lobby_id", Default = 0f, TitleTop = 23f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.UiScale, Kind = EFrontWarsSettingKind.Slider, Tab = EFrontWarsSettingsTab.General, X = 1150f, Y = 468f, Width = 376f, Height = 91f, TextKey = "ui_scale", Min = 50f, Max = 150f, Default = 100f, TitleIndent = 47f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.AlertFrame, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.General, X = 764f, Y = 613f, Width = 376f, Height = 91f, TextKey = "alert_frame", Default = 1f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.SpecialEffects, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.General, X = 1150f, Y = 613f, Width = 376f, Height = 91f, TextKey = "effects", Default = 1f, TitleTop = 24f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.StructureSprites, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.General, X = 764f, Y = 715f, Width = 376f, Height = 71f, TextKey = "structures", Default = 1f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.TerritoryPattern, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.General, X = 1150f, Y = 715f, Width = 376f, Height = 71f, TextKey = "territory_pattern", Default = 1f, TitleIndent = 47f },
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.PerformanceOverlay, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.General, X = 764f, Y = 795f, Width = 376f, Height = 71f, TextKey = "performance_overlay", Default = 0f },
            // 테마 「게임 내」 탭 — 우리가 가진 유일한 테마 선택 (지도 어둡게). 자리는 일반 탭 첫 카드 자리를 그대로 쓴다 (미측정)
            new FrontWarsSettingCard { Setting = EFrontWarsSetting.DarkMap, Kind = EFrontWarsSettingKind.Switch, Tab = EFrontWarsSettingsTab.ThemeInGame, X = 764f, Y = 241f, Width = 376f, Height = 71f, TextKey = "dark_map", Default = 0f }
        };

        public static int IndexOf(EFrontWarsSetting setting)
        {
            for (int i = 0; i < Cards.Length; i++)
            {
                if (Cards[i].Setting == setting)
                    return i;
            }

            return -1;
        }

        [SerializeField] private TMP_Text _title;
        [SerializeField] private Button _backButton;
        [SerializeField] private Image _backIcon;
        [SerializeField] private TMP_Text _navGroupSettings;
        [SerializeField] private TMP_Text _navGroupTheme;
        [SerializeField] private Button[] _tabButtons;
        [SerializeField] private Image[] _tabBackgrounds;
        [SerializeField] private TMP_Text[] _tabLabels;

        [SerializeField] private TMP_Text[] _sectionTitles;
        [SerializeField] private GameObject[] _cardRoots;
        [SerializeField] private TMP_Text[] _cardTitles;
        [SerializeField] private TMP_Text[] _cardDescs;
        [SerializeField] private TMP_Text[] _cardValues;
        [SerializeField] private Button[] _cardSwitches;
        [SerializeField] private Image[] _cardTracks;
        [SerializeField] private RectTransform[] _cardKnobs;
        [SerializeField] private Slider[] _cardSliders;

        [SerializeField] private GameObject _keysRoot;
        [SerializeField] private TMP_Text[] _keyRowKeys;
        [SerializeField] private TMP_Text[] _keyRowTexts;

        [SerializeField] private GameObject _themeNoticeRoot;
        [SerializeField] private TMP_Text _themeNotice;

        [SerializeField] private GameObject _resetRoot;
        [SerializeField] private TMP_Text _resetDesc;
        [SerializeField] private Button _resetButton;
        [SerializeField] private TMP_Text _resetLabel;

        private readonly float[] _values = new float[Cards.Length];
        private Func<string, string> _text;
        private EFrontWarsSettingsTab _tab = EFrontWarsSettingsTab.General;

        public event Action OnBack;
        public event Action<EFrontWarsSetting, float> OnSettingChanged;
        public event Action OnResetRequested;

        public EFrontWarsSettingsTab CurrentTab => _tab;

        public float ValueOf(EFrontWarsSetting setting)
        {
            int index = IndexOf(setting);
            return index < 0 ? 0f : _values[index];
        }

        public void Show(Func<string, string> text, IReadOnlyDictionary<EFrontWarsSetting, float> values)
        {
            _text = text;
            FrontWarsFonts.ApplyTo(gameObject);
            _backIcon.sprite = FrontWarsIconFactory.GetUi(FrontWarsIconFactory.EUiIcon.Back);

            _title.text = text("settings.title");
            _navGroupSettings.text = text("settings.group.settings");
            _navGroupTheme.text = text("settings.group.theme");

            for (int i = 0; i < _tabLabels.Length && i < Tabs.Length; i++)
                _tabLabels[i].text = text(Tabs[i].TextKey);

            for (int i = 0; i < _sectionTitles.Length && i < Sections.Length; i++)
                _sectionTitles[i].text = text(Sections[i].TextKey);

            for (int i = 0; i < Cards.Length; i++)
            {
                FrontWarsSettingCard card = Cards[i];
                _cardTitles[i].text = text($"settings.{card.TextKey}");
                _cardDescs[i].text = text($"settings.{card.TextKey}_desc");

                float value = values != null && values.TryGetValue(card.Setting, out float saved) ? saved : card.Default;
                _values[i] = value;

                if (card.Kind == EFrontWarsSettingKind.Slider)
                {
                    _cardSliders[i].minValue = card.Min;
                    _cardSliders[i].maxValue = card.Max;
                    _cardSliders[i].SetValueWithoutNotify(value);
                    _cardValues[i].text = $"{value:0} %";
                }
                else
                {
                    PaintSwitch(_cardTracks[i], _cardKnobs[i], value > 0.5f);
                }
            }

            for (int i = 0; i < _keyRowKeys.Length && i < FrontWarsInstructionsWindow.Rows.Length; i++)
            {
                _keyRowKeys[i].text = FrontWarsInstructionsWindow.Rows[i].Keys;
                _keyRowTexts[i].text = text(FrontWarsInstructionsWindow.Rows[i].TextKey);
            }

            _themeNotice.text = text("settings.theme_notice");
            _resetDesc.text = text("settings.reset_desc");
            _resetLabel.text = text("settings.reset");

            SetTab(EFrontWarsSettingsTab.General);
        }

        /// <summary>바깥(제어판 슬라이더 등)에서 값이 바뀌면 창도 따라 바꾼다.</summary>
        public void SetValue(EFrontWarsSetting setting, float value)
        {
            int index = IndexOf(setting);

            if (index < 0)
                return;

            _values[index] = value;

            if (Cards[index].Kind == EFrontWarsSettingKind.Slider)
            {
                _cardSliders[index].SetValueWithoutNotify(value);
                _cardValues[index].text = $"{value:0} %";
            }
            else
            {
                PaintSwitch(_cardTracks[index], _cardKnobs[index], value > 0.5f);
            }
        }

        public void SetTab(EFrontWarsSettingsTab tab)
        {
            _tab = tab;

            for (int i = 0; i < _tabBackgrounds.Length && i < Tabs.Length; i++)
                _tabBackgrounds[i].enabled = Tabs[i].Tab == tab;

            for (int i = 0; i < _sectionTitles.Length && i < Sections.Length; i++)
                _sectionTitles[i].gameObject.SetActive(Sections[i].Tab == tab);

            for (int i = 0; i < _cardRoots.Length && i < Cards.Length; i++)
                _cardRoots[i].SetActive(Cards[i].Tab == tab);

            _keysRoot.SetActive(tab == EFrontWarsSettingsTab.Keys);
            // 테마 메인 · 배경은 고를 것이 없다 — 알림만 보인다
            _themeNoticeRoot.SetActive(tab == EFrontWarsSettingsTab.ThemeMain || tab == EFrontWarsSettingsTab.ThemeBackground);
            _resetRoot.SetActive(tab == EFrontWarsSettingsTab.General);
        }

        private static void PaintSwitch(Image track, RectTransform knob, bool on)
        {
            track.color = on ? SwitchOn : SwitchOff;
            knob.anchoredPosition = new Vector2(on ? KnobOnX : KnobOffX, knob.anchoredPosition.y);
        }

        protected override void OnOpened()
        {
            _backButton.onClick.AddListener(HandleBack);
            _resetButton.onClick.AddListener(HandleReset);

            for (int i = 0; i < _tabButtons.Length && i < Tabs.Length; i++)
            {
                int index = i;
                _tabButtons[i].onClick.AddListener(() => SetTab(Tabs[index].Tab));
            }

            for (int i = 0; i < Cards.Length; i++)
            {
                int index = i;

                if (Cards[i].Kind == EFrontWarsSettingKind.Slider)
                    _cardSliders[i].onValueChanged.AddListener(value => HandleSlider(index, value));
                else
                    _cardSwitches[i].onClick.AddListener(() => HandleSwitch(index));
            }
        }

        protected override void OnClosing()
        {
            _backButton.onClick.RemoveAllListeners();
            _resetButton.onClick.RemoveAllListeners();

            for (int i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].onClick.RemoveAllListeners();

            for (int i = 0; i < Cards.Length; i++)
            {
                if (Cards[i].Kind == EFrontWarsSettingKind.Slider)
                    _cardSliders[i].onValueChanged.RemoveAllListeners();
                else
                    _cardSwitches[i].onClick.RemoveAllListeners();
            }
        }

        private void HandleBack() => OnBack?.Invoke();

        private void HandleReset() => OnResetRequested?.Invoke();

        private void HandleSlider(int index, float value)
        {
            _values[index] = value;
            _cardValues[index].text = $"{value:0} %";
            OnSettingChanged?.Invoke(Cards[index].Setting, value);
        }

        private void HandleSwitch(int index)
        {
            bool on = _values[index] < 0.5f;
            _values[index] = on ? 1f : 0f;
            PaintSwitch(_cardTracks[index], _cardKnobs[index], on);
            OnSettingChanged?.Invoke(Cards[index].Setting, _values[index]);
        }
    }
}
