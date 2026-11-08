using System;
using System.Collections.Generic;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 싱글 설정 창 — 지도 탭 · 규칙 및 옵션 탭 (`01_게임플로우.md` 「싱글 설정 창」). 지도는 결정 목록 C 에 따라 호주 하나다.
    /// <para>팀전을 고르면 팀 수 버튼이 나온다 `[소스] BVamDUD6.js:setNumberOfTeams(\`HumansVsNations\`)` — 사람 대 국가 · 2~7팀 · 4인/3인/2인 팀. 커뮤니티/커스텀 맵은 범위 밖이라 버튼이 없다.</para>
    /// </summary>
    public sealed class FrontWarsSingleplayerWindow : BaseWindow
    {
        public static readonly WindowKey<FrontWarsSingleplayerWindow> Key = new WindowKey<FrontWarsSingleplayerWindow>("UI/Window/FrontWarsSingleplayerWindow");

        /// <summary>규칙 토글 순서 — 즉시 건설 · 무한 골드 · 무한 병력 · 미니맵 · 수몰 핵무기 · 랜덤 스폰 · 동맹 비활성화.</summary>
        public static readonly string[] RuleKeys = { "sp.instant_build", "sp.infinite_gold", "sp.infinite_troops", "sp.compact_map", "sp.water_nukes", "sp.random_spawn", "sp.disable_alliances" };

        /// <summary>유닛 켜고 끄기 순서 `[실측] menu_flow_ko2.json`.</summary>
        public static readonly EFrontWarsUnit[] UnitToggles =
        {
            EFrontWarsUnit.City, EFrontWarsUnit.DefensePost, EFrontWarsUnit.Port, EFrontWarsUnit.Warship, EFrontWarsUnit.TransportShip,
            EFrontWarsUnit.MissileSilo, EFrontWarsUnit.SAMLauncher, EFrontWarsUnit.AtomBomb, EFrontWarsUnit.HydrogenBomb, EFrontWarsUnit.MIRV, EFrontWarsUnit.Factory
        };

        /// <summary>팀 수 버튼 순서 — 첫 줄 2인 · 3인 · 4인 팀 · 인류 vs 국가, 둘째 줄 2~7팀 `[실측] probe/team_options_team.json`.</summary>
        public static readonly string[] TeamChoices = { "Duos", "Trios", "Quads", "HumansVsNations", "2", "3", "4", "5", "6", "7" };

        /// <summary>팀전을 고르면 팀 수 묶음만큼 아래 줄이 내려간다 [실측 국가 글자 497 → 660].</summary>
        public const float TeamsBlockHeight = 163f;

        /// <summary>규칙 스크롤 내용 높이 — 개인전 [실측 첫 줄 245 ~ 유닛 끝 1062].</summary>
        public const float RulesContentHeight = 827f;

        [SerializeField] private RectTransform _rulesContent;
        [SerializeField] private RectTransform _belowTeams;

        private static readonly EFrontWarsDifficulty[] Difficulties = { EFrontWarsDifficulty.Easy, EFrontWarsDifficulty.Medium, EFrontWarsDifficulty.Hard, EFrontWarsDifficulty.Impossible };
        private static readonly EFrontWarsGameMode[] Modes = { EFrontWarsGameMode.FFA, EFrontWarsGameMode.Team };
        private static readonly EFrontWarsEconomyMode[] Economies = { EFrontWarsEconomyMode.Classic, EFrontWarsEconomyMode.Fast };

        private const int MaxBots = 400;
        private const int MaxNations = 400;

        private static readonly Color Selected = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color Unselected = new Color(1f, 1f, 1f, 0.06f);

        [SerializeField] private TMP_Text _title;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _backLabel;
        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _startLabel;
        [SerializeField] private Button _mapTab;
        [SerializeField] private TMP_Text _mapTabLabel;
        [SerializeField] private Button _rulesTab;
        [SerializeField] private TMP_Text _rulesTabLabel;
        [SerializeField] private GameObject _mapPanel;
        [SerializeField] private GameObject _rulesPanel;
        [SerializeField] private Button _mapTile;
        [SerializeField] private TMP_Text _mapTileLabel;
        [SerializeField] private Image _mapThumbnail;

        [SerializeField] private TMP_Text _difficultyTitle;
        [SerializeField] private Button[] _difficultyButtons;
        [SerializeField] private TMP_Text[] _difficultyLabels;
        [SerializeField] private TMP_Text _modeTitle;
        [SerializeField] private Button[] _modeButtons;
        [SerializeField] private TMP_Text[] _modeLabels;
        [SerializeField] private GameObject _teamsRoot;
        [SerializeField] private TMP_Text _teamsTitle;
        [SerializeField] private Button[] _teamButtons;
        [SerializeField] private TMP_Text[] _teamLabels;
        [SerializeField] private TMP_Text _economyTitle;
        [SerializeField] private Button[] _economyButtons;
        [SerializeField] private TMP_Text[] _economyLabels;
        [SerializeField] private TMP_Text _botsLabel;
        [SerializeField] private Slider _botsSlider;
        [SerializeField] private TMP_Text _nationsLabel;
        [SerializeField] private Slider _nationsSlider;
        [SerializeField] private Toggle[] _ruleToggles;
        [SerializeField] private TMP_Text[] _ruleLabels;
        [SerializeField] private Toggle[] _unitToggles;
        [SerializeField] private TMP_Text[] _unitLabels;

        /// <summary>시작 골드(백만) · 골드 배율 · 게임 시간(분) — 켜면 5 · 2 · 30 `[소스] BVamDUD6.js:goldMultiplier:l.goldMultiplierEnabled?l.goldMultiplier:void 0`. 값 바꾸기 칸은 아직 없다 (배치 미측정).</summary>
        [SerializeField] private Button[] _extraCards;
        [SerializeField] private Outline[] _extraBorders;
        [SerializeField] private Image[] _extraDots;
        [SerializeField] private TMP_Text[] _extraLabels;
        [SerializeField] private TMP_Text[] _extraOffValues;
        [SerializeField] private TMP_InputField[] _extraInputs;
        [SerializeField] private Button[] _extraClears;

        /// <summary>추가 줄 카드 테두리 — 켜면 주황 · 꺼지면 흰색 20% `[실측] probe/gameover.json rulesAfter`.</summary>
        private static readonly Color ExtraOnBorder = new Color(242f / 255f, 149f / 255f, 32f / 255f, 1f);
        private static readonly Color ExtraOffBorder = new Color(1f, 1f, 1f, 0.2f);

        /// <summary>게임 시간 입력 최소 [실측 input min=1].</summary>
        private const int MinGameLengthMinutes = 1;

        /// <summary>오른쪽 요약 — 지도 · 모드 · 난이도 · 경제 모드 · 봇 · 모디파이어 `[실측] menu_flow_ko2.json single_rules_ko_scroll0 x 1230~1534`.</summary>
        [SerializeField] private TMP_Text[] _summaryLabels;
        [SerializeField] private TMP_Text[] _summaryValues;

        public static readonly string[] SummaryKeys = { "sp.summary.map", "sp.summary.mode", "sp.summary.difficulty", "sp.summary.economy", "sp.summary.bots", "sp.summary.modifiers" };

        public static readonly string[] ExtraKeys = { "sp.starting_gold", "sp.gold_multiplier", "sp.game_length" };

        private const long DefaultStartingGoldMillions = 5;
        private const double DefaultGoldMultiplier = 2;
        private const int DefaultGameLengthMinutes = 30;

        private Func<string, string> _text;
        private FrontWarsSingleplayerOptions _options;
        private int _mapNations;

        public event Action<FrontWarsSingleplayerOptions> OnStart;
        public event Action OnBack;

        public void Show(Func<string, string> text, FrontWarsSingleplayerOptions options, int mapNations)
        {
            FrontWarsFonts.ApplyTo(gameObject);
            _text = text;
            _options = options;
            _mapNations = mapNations;

            _title.text = text("sp.title");
            _backLabel.text = text("sp.back");
            _startLabel.text = text("sp.start");
            _mapTabLabel.text = text("sp.tab.map");
            _rulesTabLabel.text = text("sp.tab.rules");
            _mapTileLabel.text = text($"sp.map.{options.MapName}");
            _difficultyTitle.text = text("sp.difficulty");
            _modeTitle.text = text("sp.mode");
            _teamsTitle.text = text("sp.teams_title");
            _economyTitle.text = text("sp.economy");

            for (int i = 0; i < _difficultyLabels.Length; i++)
                _difficultyLabels[i].text = text($"sp.difficulty.{Difficulties[i]}");

            for (int i = 0; i < _modeLabels.Length; i++)
                _modeLabels[i].text = text($"sp.mode.{Modes[i].ToString().ToLowerInvariant()}");

            for (int i = 0; i < _teamLabels.Length; i++)
                _teamLabels[i].text = text($"sp.teams.{TeamChoices[i]}");

            for (int i = 0; i < _economyLabels.Length; i++)
                _economyLabels[i].text = text($"sp.economy.{Economies[i]}");

            for (int i = 0; i < _ruleLabels.Length; i++)
                _ruleLabels[i].text = text(RuleKeys[i]);

            for (int i = 0; i < _unitLabels.Length; i++)
                _unitLabels[i].text = text($"unit.{UnitToggles[i]}");

            for (int i = 0; i < _summaryLabels.Length && i < SummaryKeys.Length; i++)
                _summaryLabels[i].text = text(SummaryKeys[i]);

            _botsSlider.minValue = 0;
            _botsSlider.maxValue = MaxBots;
            _nationsSlider.minValue = 0;
            _nationsSlider.maxValue = MaxNations;
            ShowTab(true);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < _difficultyButtons.Length; i++)
                _difficultyButtons[i].targetGraphic.color = Difficulties[i] == _options.Difficulty ? Selected : Unselected;

            for (int i = 0; i < _modeButtons.Length; i++)
                _modeButtons[i].targetGraphic.color = Modes[i] == _options.GameMode ? Selected : Unselected;

            bool team = _options.GameMode == EFrontWarsGameMode.Team;
            _teamsRoot.SetActive(team);
            _belowTeams.anchoredPosition = new Vector2(_belowTeams.anchoredPosition.x, team ? -TeamsBlockHeight : 0f);
            _rulesContent.sizeDelta = new Vector2(_rulesContent.sizeDelta.x, RulesContentHeight + (team ? TeamsBlockHeight : 0f));

            for (int i = 0; i < _teamButtons.Length; i++)
                _teamButtons[i].targetGraphic.color = TeamChoices[i] == _options.PlayerTeams ? Selected : Unselected;

            for (int i = 0; i < _economyButtons.Length; i++)
                _economyButtons[i].targetGraphic.color = Economies[i] == _options.EconomyMode ? Selected : Unselected;

            _botsSlider.SetValueWithoutNotify(_options.Bots);
            _botsLabel.text = string.Format(_text("sp.bots"), _options.Bots);

            int nations = _options.Nations ?? _mapNations;
            _nationsSlider.SetValueWithoutNotify(nations);
            _nationsLabel.text = _options.Nations == null ? string.Format(_text("sp.nations_default"), nations) : string.Format(_text("sp.nations"), nations);

            bool[] rules = { _options.InstantBuild, _options.InfiniteGold, _options.InfiniteTroops, _options.CompactMap, _options.WaterNukes, _options.RandomSpawn, _options.DisableAlliances };
            for (int i = 0; i < _ruleToggles.Length && i < rules.Length; i++)
                _ruleToggles[i].SetIsOnWithoutNotify(rules[i]);

            for (int i = 0; i < _unitToggles.Length; i++)
                _unitToggles[i].SetIsOnWithoutNotify(_options.DisabledUnits.Contains(UnitToggles[i]) == false);

            string[] values =
            {
                _options.StartingGoldMillions?.ToString(),
                _options.GoldMultiplier?.ToString("0.##"),
                _options.GameLengthMinutes?.ToString()
            };

            // 추가 줄 카드 — 꺼지면 「—」, 켜면 주황 테두리 · 점 · 숫자 입력 · 지우기 [실측]
            for (int i = 0; i < _extraCards.Length && i < values.Length; i++)
            {
                bool on = values[i] != null;
                _extraLabels[i].text = _text(ExtraKeys[i]);
                _extraOffValues[i].text = _text("sp.off");
                _extraDots[i].gameObject.SetActive(on);
                _extraBorders[i].effectColor = on ? ExtraOnBorder : ExtraOffBorder;
                _extraOffValues[i].gameObject.SetActive(on == false);
                _extraInputs[i].gameObject.SetActive(on);
                _extraClears[i].gameObject.SetActive(on);

                if (on && _extraInputs[i].isFocused == false)
                    _extraInputs[i].SetTextWithoutNotify(values[i]);
            }

            RefreshSummary(rules);
        }

        /// <summary>요약 판 값 — 켠 규칙은 규칙 이름을 이어 붙이고, 없으면 「—」 [실측 fs 20].</summary>
        private void RefreshSummary(bool[] rules)
        {
            if (_summaryValues == null || _summaryValues.Length < SummaryKeys.Length)
                return;

            var modifiers = new List<string>();
            for (int i = 0; i < rules.Length && i < RuleKeys.Length; i++)
            {
                if (rules[i])
                    modifiers.Add(_text(RuleKeys[i]));
            }

            // 켠 추가 줄도 모디파이어로 보인다 [실측 「제한 시간 30분」 칩]
            if (_options.StartingGoldMillions.HasValue)
                modifiers.Add(string.Format(_text("sp.summary.starting_gold"), _options.StartingGoldMillions.Value));
            if (_options.GoldMultiplier.HasValue)
                modifiers.Add(string.Format(_text("sp.summary.gold_multiplier"), _options.GoldMultiplier.Value.ToString("0.##")));
            if (_options.GameLengthMinutes.HasValue)
                modifiers.Add(string.Format(_text("sp.summary.game_length"), _options.GameLengthMinutes.Value));

            string mode = _text($"sp.mode.{_options.GameMode.ToString().ToLowerInvariant()}");
            if (_options.GameMode == EFrontWarsGameMode.Team)
                mode = $"{mode} · {_text($"sp.teams.{_options.PlayerTeams}")}";

            _summaryValues[0].text = _text($"sp.map.{_options.MapName}");
            _summaryValues[1].text = mode;
            _summaryValues[2].text = _text($"sp.difficulty.{_options.Difficulty}");
            _summaryValues[3].text = _text($"sp.economy.{_options.EconomyMode}");
            _summaryValues[4].text = _options.Bots.ToString();
            _summaryValues[5].text = modifiers.Count == 0 ? "—" : string.Join(", ", modifiers);
        }

        /// <summary>추가 줄 숫자 입력 — 못 읽거나 범위 밖이면 이전 값으로 되돌린다.</summary>
        private void SetExtraValue(int index, string text)
        {
            switch (index)
            {
                case 0:
                    if (long.TryParse(text, out long gold) && gold >= 0)
                        _options.StartingGoldMillions = gold;
                    break;
                case 1:
                    if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double multiplier) && multiplier > 0)
                        _options.GoldMultiplier = multiplier;
                    break;
                case 2:
                    if (int.TryParse(text, out int minutes))
                        _options.GameLengthMinutes = Mathf.Max(MinGameLengthMinutes, minutes);
                    break;
            }

            Refresh();
        }

        private void SetExtra(int index, bool on)
        {
            switch (index)
            {
                case 0: _options.StartingGoldMillions = on ? DefaultStartingGoldMillions : (long?)null; break;
                case 1: _options.GoldMultiplier = on ? DefaultGoldMultiplier : (double?)null; break;
                case 2: _options.GameLengthMinutes = on ? DefaultGameLengthMinutes : (int?)null; break;
            }

            Refresh();
        }

        private void ShowTab(bool map)
        {
            _mapPanel.SetActive(map);
            _rulesPanel.SetActive(map == false);
            _mapTab.targetGraphic.color = map ? Selected : Unselected;
            _rulesTab.targetGraphic.color = map ? Unselected : Selected;
        }

        /// <summary>맵 썸네일 그림 — 우리 지형 데이터를 줄여 구운 158×158 [실측 크기].</summary>
        public void SetMapThumbnail(Sprite sprite)
        {
            if (_mapThumbnail == null)
                return;

            _mapThumbnail.sprite = sprite;
            _mapThumbnail.enabled = sprite != null;
        }

        protected override void OnOpened()
        {
            _backButton.onClick.AddListener(HandleBack);
            _startButton.onClick.AddListener(HandleStart);
            _mapTab.onClick.AddListener(() => ShowTab(true));
            _rulesTab.onClick.AddListener(() => ShowTab(false));
            _botsSlider.onValueChanged.AddListener(v => { _options.Bots = Mathf.RoundToInt(v); Refresh(); });
            _nationsSlider.onValueChanged.AddListener(v => { _options.Nations = Mathf.RoundToInt(v); Refresh(); });

            for (int i = 0; i < _difficultyButtons.Length; i++)
            {
                EFrontWarsDifficulty d = Difficulties[i];
                _difficultyButtons[i].onClick.AddListener(() => { _options.Difficulty = d; Refresh(); });
            }

            for (int i = 0; i < _modeButtons.Length; i++)
            {
                EFrontWarsGameMode m = Modes[i];
                _modeButtons[i].onClick.AddListener(() => { _options.GameMode = m; Refresh(); });
            }

            for (int i = 0; i < _teamButtons.Length; i++)
            {
                string teams = TeamChoices[i];
                _teamButtons[i].onClick.AddListener(() => { _options.PlayerTeams = teams; Refresh(); });
            }

            for (int i = 0; i < _economyButtons.Length; i++)
            {
                EFrontWarsEconomyMode e = Economies[i];
                _economyButtons[i].onClick.AddListener(() => { _options.EconomyMode = e; Refresh(); });
            }

            for (int i = 0; i < _ruleToggles.Length; i++)
            {
                int index = i;
                _ruleToggles[i].onValueChanged.AddListener(on => SetRule(index, on));
            }

            for (int i = 0; i < _unitToggles.Length; i++)
            {
                EFrontWarsUnit unit = UnitToggles[i];
                _unitToggles[i].onValueChanged.AddListener(on =>
                {
                    if (on)
                        _options.DisabledUnits.Remove(unit);
                    else
                        _options.DisabledUnits.Add(unit);
                });
            }

            for (int i = 0; i < _extraCards.Length; i++)
            {
                int index = i;

                // 꺼진 카드를 누르면 기본값으로 켠다 · 지우기 = 끔 · 입력을 마치면 값
                _extraCards[i].onClick.AddListener(() =>
                {
                    if (_extraInputs[index].gameObject.activeSelf == false)
                        SetExtra(index, true);
                });
                _extraClears[i].onClick.AddListener(() => SetExtra(index, false));
                _extraInputs[i].onEndEdit.AddListener(value => SetExtraValue(index, value));
            }
        }

        private void SetRule(int index, bool on)
        {
            switch (index)
            {
                case 0: _options.InstantBuild = on; break;
                case 1: _options.InfiniteGold = on; break;
                case 2: _options.InfiniteTroops = on; break;
                case 3: _options.CompactMap = on; break;
                case 4: _options.WaterNukes = on; break;
                case 5: _options.RandomSpawn = on; break;
                case 6: _options.DisableAlliances = on; break;
            }
        }

        protected override void OnClosing()
        {
            _backButton.onClick.RemoveListener(HandleBack);
            _startButton.onClick.RemoveListener(HandleStart);
            _mapTab.onClick.RemoveAllListeners();
            _rulesTab.onClick.RemoveAllListeners();
            _botsSlider.onValueChanged.RemoveAllListeners();
            _nationsSlider.onValueChanged.RemoveAllListeners();
            foreach (Button b in _difficultyButtons) b.onClick.RemoveAllListeners();
            foreach (Button b in _modeButtons) b.onClick.RemoveAllListeners();
            foreach (Button b in _teamButtons) b.onClick.RemoveAllListeners();
            foreach (Button b in _economyButtons) b.onClick.RemoveAllListeners();
            foreach (Toggle t in _ruleToggles) t.onValueChanged.RemoveAllListeners();
            foreach (Toggle t in _unitToggles) t.onValueChanged.RemoveAllListeners();
            foreach (Button b in _extraCards) b.onClick.RemoveAllListeners();
            foreach (Button b in _extraClears) b.onClick.RemoveAllListeners();
            foreach (TMP_InputField f in _extraInputs) f.onEndEdit.RemoveAllListeners();
        }

        private void HandleBack() => OnBack?.Invoke();
        private void HandleStart() => OnStart?.Invoke(_options);
    }
}
