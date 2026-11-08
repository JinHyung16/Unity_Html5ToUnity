using System;
using System.Collections.Generic;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>순위표 한 줄.</summary>
    public readonly struct FrontWarsLeaderboardRow
    {
        public readonly int Rank;
        public readonly string Name;
        public readonly string Owned;
        public readonly string Gold;
        public readonly string Troops;
        public readonly string PlayerId;
        public readonly bool IsMe;

        public FrontWarsLeaderboardRow(int rank, string name, string owned, string gold, string troops, string playerId, bool isMe)
        {
            Rank = rank;
            Name = name;
            Owned = owned;
            Gold = gold;
            Troops = troops;
            PlayerId = playerId;
            IsMe = isMe;
        }
    }

    /// <summary>
    /// 인게임 HUD — 원본 실측 배치 (`04_UIUX규칙.md` 「인게임」 · `probe/ingame_ko3.json` · 1920×1080).
    /// <list type="bullet">
    /// <item>왼쪽 위: 순위표 열기 · 경제 방식 · 순위표 (머리줄 + 1~4위 + 내 줄 + 「+」).</item>
    /// <item>오른쪽 위: 다시 보기 · 일시정지 · 설정 · 나가기 · 타이머.</item>
    /// <item>왼쪽 아래 제어판: 병력 · 골드 · 공격 비율 · 병력 기부 · 골드 기부 슬라이더.</item>
    /// <item>가운데 아래 건설 막대 10칸 · 오른쪽 아래 이벤트 패널.</item>
    /// </list>
    /// <para>⚠ 창에는 게임 로직이 없다 — 값을 받아 표기만 하고, 조작은 event 로 올린다.</para>
    /// </summary>
    public sealed class FrontWarsHudWindow : BaseWindow
    {
        public static readonly WindowKey<FrontWarsHudWindow> Key = new WindowKey<FrontWarsHudWindow>("UI/Window/FrontWarsHudWindow");

        /// <summary>건설 막대 칸 순서 — 단축키 1~6 · 7~0 `[소스] BMmXchgl.js:handleBuildKeyUp(e){`.</summary>
        public static readonly EFrontWarsUnit[] BuildSlots =
        {
            EFrontWarsUnit.City, EFrontWarsUnit.Factory, EFrontWarsUnit.Port, EFrontWarsUnit.DefensePost, EFrontWarsUnit.MissileSilo,
            EFrontWarsUnit.SAMLauncher, EFrontWarsUnit.Warship, EFrontWarsUnit.AtomBomb, EFrontWarsUnit.HydrogenBomb, EFrontWarsUnit.MIRV
        };

        public static readonly string[] BuildHotkeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };

        /// <summary>이벤트 거르기 순서 — 공격 · 핵무기 · 무역 · 동맹 · 채팅 `[소스] QPQdQ6Sy.js:new Set([H.ATTACK,H.NUKE,H.ALLIANCE,H.TRADE,H.CHAT])` 버튼 실측 순서.</summary>
        public static readonly string[] FilterKeys = { "filter.attack", "filter.nuke", "filter.trade", "filter.alliance", "filter.chat" };

        private static readonly Color IncreaseColor = new Color32(34, 197, 94, 255);
        private static readonly Color DecreaseColor = new Color32(239, 68, 68, 255);
        private static readonly Color FilterOn = new Color(1f, 1f, 1f, 1f);
        private static readonly Color FilterOff = new Color(1f, 1f, 1f, 0.35f);

        /// <summary>순위표 내 줄 바탕 검정 40% · 나머지 줄 바탕 없음 `[실측] probe/ingame_menus_ffa.json div 0,263,400,43 oklab(0 0 0 / 0.4)`.</summary>
        private static readonly Color MeRowColor = new Color(0f, 0f, 0f, 0.4f);

        [Header("Top")]
        [SerializeField] private TMP_Text _economyText;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private Button _leaderboardToggle;
        [SerializeField] private Button _replayButton;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private TMP_Text _spawnHint;
        [SerializeField] private Image _leaderboardIcon;
        [SerializeField] private Image _replayIcon;
        [SerializeField] private Image _pauseIcon;
        [SerializeField] private Image _settingsIcon;
        [SerializeField] private Image _exitIcon;

        [Header("Leaderboard")]
        [SerializeField] private GameObject _leaderboardRoot;
        [SerializeField] private TMP_Text[] _headerTexts;
        [SerializeField] private Button[] _rowButtons;
        [SerializeField] private TMP_Text[] _rowRank;
        [SerializeField] private TMP_Text[] _rowName;
        [SerializeField] private TMP_Text[] _rowOwned;
        [SerializeField] private TMP_Text[] _rowGold;
        [SerializeField] private TMP_Text[] _rowTroops;
        [SerializeField] private LayoutElement _rowsViewport;
        [SerializeField] private RectTransform _rowsContent;
        [SerializeField] private ScrollRect _leaderboardScroll;
        [SerializeField] private Button _expandButton;
        [SerializeField] private TMP_Text _expandLabel;

        [Header("Control panel")]
        [SerializeField] private GameObject _controlRoot;
        [SerializeField] private TMP_Text _troopsLabel;
        [SerializeField] private TMP_Text _troopsValue;
        [SerializeField] private TMP_Text _troopsIncrease;
        [SerializeField] private TMP_Text _goldLabel;
        [SerializeField] private TMP_Text _goldValue;
        [SerializeField] private TMP_Text _attackLabel;
        [SerializeField] private TMP_Text _attackValue;
        [SerializeField] private Slider _attackSlider;
        [SerializeField] private TMP_Text _sendTroopsLabel;
        [SerializeField] private TMP_Text _sendTroopsValue;
        [SerializeField] private Slider _sendTroopsSlider;
        [SerializeField] private TMP_Text _sendGoldLabel;
        [SerializeField] private TMP_Text _sendGoldValue;
        [SerializeField] private Slider _sendGoldSlider;

        [Header("Build bar")]
        [SerializeField] private GameObject _buildRoot;
        [SerializeField] private Button[] _buildButtons;
        [SerializeField] private TMP_Text[] _buildHotkeys;
        [SerializeField] private TMP_Text[] _buildCounts;
        [SerializeField] private Image[] _buildIcons;

        [Header("Player info")]
        [SerializeField] private GameObject _infoRoot;
        [SerializeField] private TMP_Text _infoName;
        [SerializeField] private TMP_Text _infoType;
        [SerializeField] private TMP_Text _infoRelation;
        [SerializeField] private TMP_Text _infoTroopsLabel;
        [SerializeField] private TMP_Text _infoTroops;
        [SerializeField] private GameObject _infoAttackingRow;
        [SerializeField] private TMP_Text _infoAttackingLabel;
        [SerializeField] private TMP_Text _infoAttacking;
        [SerializeField] private TMP_Text _infoGold;
        [SerializeField] private TMP_Text _infoTradeLabel;
        [SerializeField] private TMP_Text _infoTrade;
        [SerializeField] private TMP_Text _infoNukesLabel;
        [SerializeField] private TMP_Text _infoNukes;
        [SerializeField] private TMP_Text _infoTraitorLabel;
        [SerializeField] private TMP_Text _infoTraitor;
        [SerializeField] private TMP_Text[] _infoCountLabels;
        [SerializeField] private TMP_Text[] _infoCounts;
        [SerializeField] private GameObject _infoActionsRoot;
        [SerializeField] private Button[] _infoActionButtons;
        [SerializeField] private TMP_Text[] _infoActionLabels;

        /// <summary>정보 판 건물 수 칸 순서 — 도시 · 공장 · 항구 · 격납고 · SAM · 군함 `[실측] probe/ingame_menus_ffa.json`.</summary>
        public static readonly EFrontWarsUnit[] InfoCountTypes =
        {
            EFrontWarsUnit.City, EFrontWarsUnit.Factory, EFrontWarsUnit.Port, EFrontWarsUnit.MissileSilo, EFrontWarsUnit.SAMLauncher, EFrontWarsUnit.Warship
        };

        [Header("Team")]
        [SerializeField] private Button _teamButton;
        [SerializeField] private TMP_Text _teamLabel;

        [Header("Game speed · top bar")]
        [SerializeField] private RectTransform _topRightPanel;
        [SerializeField] private GameObject _speedPanel;
        [SerializeField] private TMP_Text _speedTitle;
        [SerializeField] private Button[] _speedButtons;
        [SerializeField] private Image[] _topBarSegments;

        /// <summary>오른쪽 위 판 높이 — 속도 판이 닫혔을 때 83 · 열렸을 때 124 [실측].</summary>
        private const float TopRightClosedHeight = 83f;
        private const float TopRightOpenHeight = 124f;

        /// <summary>속도 버튼 색 — 고른 것 검정 · 나머지 gray-800 40%(×0.5) / 50% [실측 bg-gray-800/40 · /50].</summary>
        private static readonly Color SpeedSelected = new Color(0f, 0f, 0f, 1f);
        private static readonly Color SpeedIdleSlow = new Color32(31, 41, 55, 102);
        private static readonly Color SpeedIdle = new Color32(31, 41, 55, 128);

        [Header("Alert frame")]
        [SerializeField] private FrontWarsAlertFrame _alertFrame;

        [Header("Events")]
        [SerializeField] private GameObject _eventsRoot;
        [SerializeField] private Button[] _filterButtons;
        [SerializeField] private TMP_Text[] _filterLabels;
        [SerializeField] private Button _hideButton;
        [SerializeField] private TMP_Text _hideLabel;
        [SerializeField] private TMP_Text _emptyText;
        [SerializeField] private Button[] _eventButtons;
        [SerializeField] private TMP_Text[] _eventTexts;
        [SerializeField] private Button[] _eventAcceptButtons;
        [SerializeField] private TMP_Text[] _eventAcceptLabels;
        [SerializeField] private Button[] _eventRejectButtons;
        [SerializeField] private TMP_Text[] _eventRejectLabels;

        private readonly List<string> _rowPlayerIds = new List<string>();
        private string _acceptLabel = "수락";
        private string _extendLabel = "연장";
        private bool _leaderboardShown = true;

        public event Action<float> OnAttackRatioChanged;
        public event Action<float> OnSendTroopsRatioChanged;
        public event Action<float> OnSendGoldRatioChanged;
        public event Action<EFrontWarsUnit> OnBuildSelected;
        public event Action OnPause;
        public event Action OnExit;
        public event Action OnSettings;
        public event Action OnReplay;
        public event Action OnLeaderboardToggle;
        public event Action OnTeamButton;
        public event Action<int> OnSpeedSelected;
        public event Action OnLeaderboardExpand;
        public event Action<string> OnFocusPlayer;
        public event Action<int> OnFilterToggle;
        public event Action OnEventsHide;
        public event Action<int> OnEventClicked;

        /// <summary>동맹 요청 줄의 수락 · 거절 (줄 번호 · 수락 여부).</summary>
        public event Action<int, bool> OnAllianceReply;
        public event Action<int> OnInfoAction;

        public int LeaderboardCapacity => _rowName != null ? _rowName.Length : 0;
        public int EventCapacity => _eventTexts != null ? _eventTexts.Length : 0;

        public void ApplyStaticTexts(Func<string, string> text)
        {
            FrontWarsFonts.ApplyTo(gameObject);

            string[] headers = { "leaderboard.rank", "leaderboard.player", "leaderboard.owned", "leaderboard.gold", "leaderboard.troops" };
            for (int i = 0; i < _headerTexts.Length && i < headers.Length; i++)
                _headerTexts[i].text = text(headers[i]);

            _troopsLabel.text = text("control.troops") + ":";
            _goldLabel.text = text("control.gold") + ":";
            _attackLabel.text = text("control.attack_ratio") + ":";
            _sendTroopsLabel.text = text("control.send_troops") + ":";
            _sendGoldLabel.text = text("control.send_gold") + ":";
            _hideLabel.text = text("events.hide");

            if (_speedTitle != null)
                _speedTitle.text = text("game_control.game_speed");

            _acceptLabel = text("alliance.accept");
            _extendLabel = text("alliance.extend");

            for (int i = 0; _eventAcceptLabels != null && i < _eventAcceptLabels.Length; i++)
            {
                _eventAcceptLabels[i].text = _acceptLabel;
                _eventRejectLabels[i].text = text("alliance.reject");
            }
            _emptyText.text = text("events.none");
            _spawnHint.text = text("hud.spawn_hint");

            // 버튼 아이콘 — 원본 SVG 대신 코드로 그린 도형
            _leaderboardIcon.sprite = FrontWarsIconFactory.GetUi(FrontWarsIconFactory.EUiIcon.Menu);
            _replayIcon.sprite = FrontWarsIconFactory.GetUi(FrontWarsIconFactory.EUiIcon.Replay);
            _pauseIcon.sprite = FrontWarsIconFactory.GetUi(FrontWarsIconFactory.EUiIcon.Pause);
            _settingsIcon.sprite = FrontWarsIconFactory.GetUi(FrontWarsIconFactory.EUiIcon.Gear);
            _exitIcon.sprite = FrontWarsIconFactory.GetUi(FrontWarsIconFactory.EUiIcon.Close);

            for (int i = 0; i < _filterLabels.Length && i < FilterKeys.Length; i++)
                _filterLabels[i].text = text(FilterKeys[i]);

            for (int i = 0; i < _buildHotkeys.Length && i < BuildHotkeys.Length; i++)
            {
                _buildHotkeys[i].text = BuildHotkeys[i];
                _buildIcons[i].sprite = FrontWarsIconFactory.Get(FrontWarsIconFactory.ShapeOf(BuildSlots[i]));
            }
        }

        /// <summary>스폰 단계엔 순위표 · 제어판 · 건설 · 이벤트를 숨기고 안내를 띄운다 (`01_게임플로우.md` 「인게임 HUD」 「언제 보이나」).</summary>
        public void SetSpawnPhase(bool spawnPhase, bool showHint)
        {
            // ⚠ 켜짐 여부는 따로 든다 — activeSelf 를 다시 읽으면 스폰 단계에 한 번 꺼진 순위표가 영영 안 켜진다
            _leaderboardRoot.SetActive(spawnPhase == false && _leaderboardShown);
            _controlRoot.SetActive(spawnPhase == false);
            _buildRoot.SetActive(spawnPhase == false);
            _eventsRoot.SetActive(spawnPhase == false);
            _spawnHint.gameObject.SetActive(spawnPhase && showHint);
        }

        public void SetLeaderboardVisible(bool visible)
        {
            _leaderboardShown = visible;
            _leaderboardRoot.SetActive(visible);
        }

        public bool LeaderboardVisible => _leaderboardShown;

        public void SetEconomy(string text) => _economyText.text = text;

        /// <summary>순위표가 열리면 경제 방식 글자가 순위표 폭만큼 오른쪽으로 간다 [실측 44 → 338].</summary>
        public void SetEconomyOffset(float x)
        {
            RectTransform rect = _economyText.rectTransform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        }

        public void SetTimer(string text) => _timerText.text = text;

        public void SetPaused(bool paused)
        {
            _pauseIcon.sprite = FrontWarsIconFactory.GetUi(paused ? FrontWarsIconFactory.EUiIcon.Play : FrontWarsIconFactory.EUiIcon.Pause);
        }

        /// <summary>줄 높이 · 편 상태 최대 높이 `[소스] QPQdQ6Sy.js:var Mr=40,Nr=400` · `min(400px, calc(100vh - 360px))`.</summary>
        public const float LeaderboardRowHeight = 40f;
        public const float LeaderboardMaxHeight = 400f;
        private const float LeaderboardHeightMargin = 360f;

        private readonly List<FrontWarsLeaderboardRow> _leaderboardRows = new List<FrontWarsLeaderboardRow>();

        /// <summary>
        /// 순위표 줄 — 접으면 준 줄만, 펴면 모든 플레이어를 스크롤로 본다. 화면에 보이는 만큼만 실제 줄에 채운다 `[소스] 보이는 줄 ±5 만 그림`.
        /// </summary>
        public void SetLeaderboard(IReadOnlyList<FrontWarsLeaderboardRow> rows, string expandLabel, bool expanded)
        {
            _leaderboardRows.Clear();
            _leaderboardRows.AddRange(rows);

            float canvasHeight = ((RectTransform)transform).rect.height;
            float full = rows.Count * LeaderboardRowHeight;
            float limit = expanded
                ? Mathf.Max(LeaderboardRowHeight, Mathf.Min(LeaderboardMaxHeight, canvasHeight - LeaderboardHeightMargin))
                : full;

            _rowsViewport.preferredHeight = Mathf.Min(full, limit);
            _rowsContent.sizeDelta = new Vector2(0f, full);
            _leaderboardScroll.enabled = full > limit;

            if (_leaderboardScroll.enabled == false)
                _rowsContent.anchoredPosition = Vector2.zero;

            RefreshLeaderboardRows();
            _expandLabel.text = expandLabel;
        }

        /// <summary>스크롤 자리에 맞춰 실제 줄에 값을 다시 채운다.</summary>
        private void RefreshLeaderboardRows()
        {
            _rowPlayerIds.Clear();
            int first = Mathf.Clamp(Mathf.FloorToInt(_rowsContent.anchoredPosition.y / LeaderboardRowHeight), 0,
                                    Mathf.Max(0, _leaderboardRows.Count - 1));

            if (_leaderboardRows.Count <= _rowName.Length)
                first = 0;

            for (int i = 0; i < _rowName.Length; i++)
            {
                int index = first + i;
                bool used = index < _leaderboardRows.Count;
                _rowButtons[i].gameObject.SetActive(used);

                if (used == false)
                {
                    _rowPlayerIds.Add(null);
                    continue;
                }

                FrontWarsLeaderboardRow r = _leaderboardRows[index];
                FontStyles style = r.IsMe ? FontStyles.Bold : FontStyles.Normal;
                _rowButtons[i].targetGraphic.color = r.IsMe ? MeRowColor : Color.clear;
                ((RectTransform)_rowButtons[i].transform).anchoredPosition = new Vector2(0f, -index * LeaderboardRowHeight);
                SetRowText(_rowRank[i], r.Rank.ToString(), style);
                SetRowText(_rowName[i], r.Name, style);
                SetRowText(_rowOwned[i], r.Owned, style);
                SetRowText(_rowGold[i], r.Gold, style);
                SetRowText(_rowTroops[i], r.Troops, style);
                _rowPlayerIds.Add(r.PlayerId);
            }
        }

        private void HandleLeaderboardScroll(Vector2 _) => RefreshLeaderboardRows();

        private static void SetRowText(TMP_Text text, string value, FontStyles style)
        {
            text.text = value;
            text.fontStyle = style;
        }

        public void SetControl(string troops, string maxTroops, string increase, bool increasing, string gold,
                               float attackRatio, string attackTroops, float sendTroopsRatio, string sendTroops, float sendGoldRatio, string sendGold)
        {
            _troopsValue.text = $"{troops} / {maxTroops}";
            _troopsIncrease.text = $"({increase})";
            _troopsIncrease.color = increasing ? IncreaseColor : DecreaseColor;
            _goldValue.text = gold;

            _attackSlider.SetValueWithoutNotify(attackRatio * 100f);
            _attackValue.text = $"{Mathf.Round(attackRatio * 100f):0}% ({attackTroops})";
            _sendTroopsSlider.SetValueWithoutNotify(sendTroopsRatio * 100f);
            _sendTroopsValue.text = $"{Mathf.Round(sendTroopsRatio * 100f):0}% ({sendTroops})";
            _sendGoldSlider.SetValueWithoutNotify(sendGoldRatio * 100f);
            _sendGoldValue.text = $"{Mathf.Round(sendGoldRatio * 100f):0}% ({sendGold})";
        }

        /// <summary>건설 칸 — 보유 수 · 지을 수 있는가 (못 지으면 흐리게).</summary>
        public void SetBuildSlot(int index, int count, bool affordable, bool selected, Color ownerColor)
        {
            if (index < 0 || index >= _buildButtons.Length)
                return;

            _buildCounts[index].text = count.ToString();
            _buildButtons[index].interactable = affordable;
            _buildIcons[index].color = affordable ? ownerColor : new Color(ownerColor.r, ownerColor.g, ownerColor.b, 0.35f);
            _buildButtons[index].targetGraphic.color = selected ? new Color(1f, 1f, 1f, 0.35f) : new Color(0f, 0f, 0f, 0.35f);
        }

        /// <summary>플레이어 정보 판 — 공격 중 병력이 null 이면 그 줄을 숨긴다. 무역 상태 · 보낸 핵 · 배신자는 늘 보인다.</summary>
        public void SetPlayerInfo(string name, string type, string relation, string troopsLabel, string troops,
                                  string attackingLabel, string attacking, string gold, IReadOnlyList<string> countLabels, IReadOnlyList<int> counts,
                                  string tradeLabel, string trade, string nukesLabel, string nukes, string traitorLabel, string traitor)
        {
            _infoTradeLabel.text = tradeLabel;
            _infoTrade.text = trade;
            _infoNukesLabel.text = nukesLabel;
            _infoNukes.text = nukes;
            _infoTraitorLabel.text = traitorLabel;
            _infoTraitor.text = traitor;

            _infoRoot.SetActive(true);
            _infoName.text = name;
            _infoType.text = type;
            _infoRelation.text = relation;
            _infoTroopsLabel.text = troopsLabel;
            _infoTroops.text = troops;
            _infoAttackingRow.SetActive(attacking != null);
            _infoAttackingLabel.text = attackingLabel;
            _infoAttacking.text = attacking ?? string.Empty;
            _infoGold.text = gold;

            for (int i = 0; i < _infoCounts.Length && i < counts.Count; i++)
            {
                _infoCountLabels[i].text = countLabels[i];
                _infoCounts[i].text = counts[i].ToString();
            }
        }

        /// <summary>
        /// 팀 글자 — 팀전에서만 왼쪽 위에 팀 색으로 뜬다. 누르면 순위표를 접고 편다 [실측 44,32 · 14px 500 · 순위표가 열리면 폭 286, 접으면 글자 폭].
        /// </summary>
        public void SetTeamButton(bool show, string label, Color color)
        {
            if (_teamButton == null)
                return;

            if (_teamButton.gameObject.activeSelf != show)
                _teamButton.gameObject.SetActive(show);

            if (show == false)
                return;

            _teamLabel.text = label ?? string.Empty;
            _teamLabel.color = color;

            var rect = (RectTransform)_teamButton.transform;
            float width = _leaderboardShown ? TeamLabelOpenWidth : TeamLabelWidth;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        public bool TeamButtonVisible => _teamButton != null && _teamButton.gameObject.activeSelf;

        /// <summary>팀 글자가 제 폭만큼만 차지할 때의 폭 (접힌 순위표 · 경제 글자 자리 계산에 쓴다).</summary>
        public float TeamLabelWidth => _teamLabel == null ? 0f : Mathf.Ceil(_teamLabel.GetPreferredValues(_teamLabel.text).x);

        /// <summary>순위표가 열렸을 때 팀 글자 칸 폭 [실측 286].</summary>
        private const float TeamLabelOpenWidth = 286f;

        /// <summary>
        /// 「게임 속도」 판 — 다시 보기 버튼으로 연다 [실측 1649,16,127×124 · 버튼 53×33 두 줄]. 판이 열리면 오른쪽 위 판도 124 로 길어진다.
        /// </summary>
        public void SetSpeedPanel(bool open, int selected)
        {
            if (_speedPanel == null)
                return;

            if (_speedPanel.activeSelf != open)
                _speedPanel.SetActive(open);

            _topRightPanel.sizeDelta = new Vector2(_topRightPanel.sizeDelta.x, open ? TopRightOpenHeight : TopRightClosedHeight);

            for (int i = 0; i < _speedButtons.Length; i++)
                _speedButtons[i].targetGraphic.color = i == selected ? SpeedSelected : (i == 0 ? SpeedIdleSlow : SpeedIdle);
        }

        public bool SpeedPanelVisible => _speedPanel != null && _speedPanel.activeSelf;

        /// <summary>
        /// 화면 맨 위 10px 막대의 조각들 — 비율(0~1) · 색 `[소스] QPQdQ6Sy.js function ea(){`. 조각이 없으면 바탕만 보인다.
        /// </summary>
        public void SetTopBar(IReadOnlyList<(float Ratio, Color Color)> segments)
        {
            if (_topBarSegments == null)
                return;

            float x = 0f;

            for (int i = 0; i < _topBarSegments.Length; i++)
            {
                bool used = i < segments.Count;
                GameObject go = _topBarSegments[i].gameObject;

                if (go.activeSelf != used)
                    go.SetActive(used);

                if (used == false)
                    continue;

                float end = Mathf.Clamp01(x + segments[i].Ratio);
                RectTransform rect = _topBarSegments[i].rectTransform;
                rect.anchorMin = new Vector2(x, 0f);
                rect.anchorMax = new Vector2(end, 1f);
                _topBarSegments[i].color = segments[i].Color;
                x = end;
            }
        }

        /// <summary>경고 테두리 — 공격받음 · 배신당함 (<see cref="FrontWarsAlertFrame"/>).</summary>
        public FrontWarsAlertFrame AlertFrame => _alertFrame;

        // ── 튜토리얼 — 비출 자리 · 단계별로 드러내기

        private static readonly Vector3[] CornerBuffer = new Vector3[4];

        /// <summary>
        /// 튜토리얼이 비출 HUD 자리 네 귀퉁이(월드) `[소스] id: tutorial-control-panel · tutorial-attack-slider · tutorial-leaderboard · tutorial-game-control`.
        /// <para>공격 비율은 이름 줄과 막대를 합친 상자다 (원본 div 가 둘을 감싼다). 꺼져 있으면 false.</para>
        /// </summary>
        public bool TryGetTutorialTarget(string id, Vector3[] corners)
        {
            switch (id)
            {
                case FrontWarsTutorialObjectives.TargetControlPanel:
                    return CornersOf(_controlRoot.transform as RectTransform, corners);
                case FrontWarsTutorialObjectives.TargetLeaderboard:
                    return CornersOf(_leaderboardRoot.transform as RectTransform, corners);
                case FrontWarsTutorialObjectives.TargetGameControl:
                    return CornersOf(_topRightPanel, corners);
                case FrontWarsTutorialObjectives.TargetAttackSlider:
                    if (CornersOf(_attackLabel.rectTransform, corners) == false || CornersOf((RectTransform)_attackSlider.transform, CornerBuffer) == false)
                        return false;

                    Vector3 min = Vector3.Min(Vector3.Min(corners[0], corners[2]), Vector3.Min(CornerBuffer[0], CornerBuffer[2]));
                    Vector3 max = Vector3.Max(Vector3.Max(corners[0], corners[2]), Vector3.Max(CornerBuffer[0], CornerBuffer[2]));
                    corners[0] = new Vector3(min.x, min.y, min.z);
                    corners[1] = new Vector3(min.x, max.y, min.z);
                    corners[2] = new Vector3(max.x, max.y, min.z);
                    corners[3] = new Vector3(max.x, min.y, min.z);
                    return true;
                default:
                    return false;
            }
        }

        private static bool CornersOf(RectTransform rect, Vector3[] corners)
        {
            if (rect == null || rect.gameObject.activeInHierarchy == false)
                return false;

            rect.GetWorldCorners(corners);
            return true;
        }

        /// <summary>단계별로 드러내기 — 0.6초에 걸쳐 나타나고, 숨은 동안은 눌리지 않는다 `[소스] jr · opacity 0.6s ease`.</summary>
        private const float GateFadeSeconds = 0.6f;

        private readonly bool[] _gateShown = { true, true, true };
        private CanvasGroup[] _gateGroups;

        /// <summary>튜토리얼 동안 순위표(톱 3 부터) · 사건 목록(사수부터) · 건설 막대(도시부터)를 가린다. 튜토리얼이 아니면 셋 다 true.</summary>
        public void SetTutorialGate(bool leaderboard, bool events, bool build)
        {
            _gateShown[0] = leaderboard;
            _gateShown[1] = events;
            _gateShown[2] = build;
        }

        public bool IsGateShown(int part) => _gateShown[part];

        private void Update()
        {
            if (_gateGroups == null)
            {
                _gateGroups = new[] { GateGroup(_leaderboardRoot), GateGroup(_eventsRoot), GateGroup(_buildRoot) };
            }

            for (int i = 0; i < _gateGroups.Length; i++)
            {
                CanvasGroup group = _gateGroups[i];
                float target = _gateShown[i] ? 1f : 0f;
                group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / GateFadeSeconds);
                group.blocksRaycasts = _gateShown[i];
                group.interactable = _gateShown[i];
            }
        }

        private static CanvasGroup GateGroup(GameObject root)
        {
            var group = root.GetComponent<CanvasGroup>();
            return group != null ? group : root.AddComponent<CanvasGroup>();
        }

        public void HidePlayerInfo()
        {
            if (_infoRoot.activeSelf)
                _infoRoot.SetActive(false);
        }

        public bool PlayerInfoVisible => _infoRoot.activeSelf;

        /// <summary>정보 판 아래 외교 버튼 — 목록이 비면 묶음을 숨긴다.</summary>
        public void SetPlayerActions(IReadOnlyList<(EFrontWarsRadialAction Action, string Label, bool Enabled)> actions)
        {
            bool any = actions.Count > 0;

            if (_infoActionsRoot.activeSelf != any)
                _infoActionsRoot.SetActive(any);

            for (int i = 0; i < _infoActionButtons.Length; i++)
            {
                bool used = i < actions.Count;
                _infoActionButtons[i].gameObject.SetActive(used);

                if (used == false)
                    continue;

                _infoActionLabels[i].text = actions[i].Label;
                _infoActionButtons[i].interactable = actions[i].Enabled;
            }
        }

        public void SetFilter(int index, bool on)
        {
            if (index >= 0 && index < _filterLabels.Length)
                _filterLabels[index].color = on ? FilterOn : FilterOff;
        }

        public void SetEventsCollapsed(bool collapsed, string hideLabel)
        {
            foreach (Button b in _eventButtons)
                b.gameObject.SetActive(collapsed == false && b.gameObject.activeSelf);

            _hideLabel.text = hideLabel;
        }

        /// <summary>이벤트 줄 — Reply 는 동맹 요청(수락 · 거절), Renew 는 동맹 연장(연장 · 무시) 줄이다.</summary>
        public void SetEvents(IReadOnlyList<(string Text, Color Color, bool Reply, bool Renew)> events, bool collapsed)
        {
            int shown = collapsed ? 0 : Mathf.Min(events.Count, _eventTexts.Length);

            for (int i = 0; i < _eventTexts.Length; i++)
            {
                bool used = i < shown;
                _eventButtons[i].gameObject.SetActive(used);

                // 동맹 요청 줄만 「수락 · 거절」 버튼이 붙는다 [실측 원본 동맹 요청 줄]
                if (_eventAcceptButtons != null && i < _eventAcceptButtons.Length)
                {
                    bool reply = used && events[i].Reply;
                    bool renew = used && events[i].Renew;
                    _eventAcceptButtons[i].gameObject.SetActive(reply || renew);
                    _eventRejectButtons[i].gameObject.SetActive(reply);
                    _eventAcceptLabels[i].text = renew ? _extendLabel : _acceptLabel;
                }

                if (used == false)
                    continue;

                _eventTexts[i].text = events[i].Text;
                _eventTexts[i].color = events[i].Color;
            }

            _emptyText.gameObject.SetActive(collapsed == false && events.Count == 0);
        }

        protected override void OnOpened()
        {
            _leaderboardScroll.onValueChanged.AddListener(HandleLeaderboardScroll);
            _leaderboardToggle.onClick.AddListener(HandleLeaderboardToggle);

            if (_teamButton != null)
                _teamButton.onClick.AddListener(HandleTeamButton);

            for (int i = 0; _speedButtons != null && i < _speedButtons.Length; i++)
            {
                int index = i;
                _speedButtons[i].onClick.AddListener(() => OnSpeedSelected?.Invoke(index));
            }
            _replayButton.onClick.AddListener(HandleReplay);
            _pauseButton.onClick.AddListener(HandlePause);
            _settingsButton.onClick.AddListener(HandleSettings);
            _exitButton.onClick.AddListener(HandleExit);
            _expandButton.onClick.AddListener(HandleExpand);
            _attackSlider.onValueChanged.AddListener(HandleAttackSlider);
            _sendTroopsSlider.onValueChanged.AddListener(HandleSendTroopsSlider);
            _sendGoldSlider.onValueChanged.AddListener(HandleSendGoldSlider);
            _hideButton.onClick.AddListener(HandleHide);

            for (int i = 0; i < _buildButtons.Length; i++)
            {
                int index = i;
                _buildButtons[i].onClick.AddListener(() => OnBuildSelected?.Invoke(BuildSlots[index]));
            }

            for (int i = 0; i < _rowButtons.Length; i++)
            {
                int index = i;
                _rowButtons[i].onClick.AddListener(() => HandleRow(index));
            }

            for (int i = 0; i < _filterButtons.Length; i++)
            {
                int index = i;
                _filterButtons[i].onClick.AddListener(() => OnFilterToggle?.Invoke(index));
            }

            for (int i = 0; i < _eventButtons.Length; i++)
            {
                int index = i;
                _eventButtons[i].onClick.AddListener(() => OnEventClicked?.Invoke(index));

                if (_eventAcceptButtons == null || i >= _eventAcceptButtons.Length)
                    continue;

                _eventAcceptButtons[i].onClick.AddListener(() => OnAllianceReply?.Invoke(index, true));
                _eventRejectButtons[i].onClick.AddListener(() => OnAllianceReply?.Invoke(index, false));
            }

            for (int i = 0; i < _infoActionButtons.Length; i++)
            {
                int index = i;
                _infoActionButtons[i].onClick.AddListener(() => OnInfoAction?.Invoke(index));
            }
        }

        protected override void OnClosing()
        {
            _leaderboardScroll.onValueChanged.RemoveListener(HandleLeaderboardScroll);
            _leaderboardToggle.onClick.RemoveListener(HandleLeaderboardToggle);

            if (_teamButton != null)
                _teamButton.onClick.RemoveListener(HandleTeamButton);

            for (int i = 0; _speedButtons != null && i < _speedButtons.Length; i++)
                _speedButtons[i].onClick.RemoveAllListeners();
            _replayButton.onClick.RemoveListener(HandleReplay);
            _pauseButton.onClick.RemoveListener(HandlePause);
            _settingsButton.onClick.RemoveListener(HandleSettings);
            _exitButton.onClick.RemoveListener(HandleExit);
            _expandButton.onClick.RemoveListener(HandleExpand);
            _attackSlider.onValueChanged.RemoveListener(HandleAttackSlider);
            _sendTroopsSlider.onValueChanged.RemoveListener(HandleSendTroopsSlider);
            _sendGoldSlider.onValueChanged.RemoveListener(HandleSendGoldSlider);
            _hideButton.onClick.RemoveListener(HandleHide);

            foreach (Button b in _buildButtons) b.onClick.RemoveAllListeners();
            foreach (Button b in _rowButtons) b.onClick.RemoveAllListeners();
            foreach (Button b in _filterButtons) b.onClick.RemoveAllListeners();
            foreach (Button b in _eventButtons) b.onClick.RemoveAllListeners();
            foreach (Button b in _infoActionButtons) b.onClick.RemoveAllListeners();
        }

        private void HandleRow(int index)
        {
            if (index < _rowPlayerIds.Count && _rowPlayerIds[index] != null)
                OnFocusPlayer?.Invoke(_rowPlayerIds[index]);
        }

        private void HandleTeamButton() => OnTeamButton?.Invoke();

        private void HandleLeaderboardToggle() => OnLeaderboardToggle?.Invoke();
        private void HandleReplay() => OnReplay?.Invoke();
        private void HandlePause() => OnPause?.Invoke();
        private void HandleSettings() => OnSettings?.Invoke();
        private void HandleExit() => OnExit?.Invoke();
        private void HandleExpand() => OnLeaderboardExpand?.Invoke();
        private void HandleHide() => OnEventsHide?.Invoke();
        private void HandleAttackSlider(float v) => OnAttackRatioChanged?.Invoke(v / 100f);
        private void HandleSendTroopsSlider(float v) => OnSendTroopsRatioChanged?.Invoke(v / 100f);
        private void HandleSendGoldSlider(float v) => OnSendGoldRatioChanged?.Invoke(v / 100f);
    }
}
