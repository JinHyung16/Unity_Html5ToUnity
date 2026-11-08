using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 창을 조종한다 — 화면 전이(메뉴 → 싱글 설정 → 게임) · HUD 값 넣기 · 이벤트 목록 · 오른쪽 클릭 메뉴 · 판 끝.
    /// <para>게임 로직은 시뮬 · 매니저가 들고, 여기는 창을 열고 닫고 값을 넣고 명령을 올린다.</para>
    /// </summary>
    public sealed class FrontWarsManagement : BaseManagement
    {
        /// <summary>순위표 접었을 때 위 줄 수 [실측 1~4 + 내 줄].</summary>
        private const int LeaderboardTopRows = 4;

        /// <summary>순위표 폭 — 열리면 경제 글자가 이만큼 오른쪽으로 [실측 44 → 338].</summary>
        private const float EconomyClosedX = 44f;
        private const float EconomyOpenX = 338f;

        /// <summary>팀 글자와 경제 글자 사이 [실측 gap 8px].</summary>
        private const float TeamLabelGap = 8f;

        /// <summary>속도 배수 — ×0.5 · ×1 · ×2 · Max `[소스] Slow=2 · Normal=1 · Fast=.5 · Fastest=0`.</summary>
        private static readonly double[] SpeedMultipliers = { 2.0, 1.0, 0.5, 0.0 };
        private const int NormalSpeedIndex = 1;

        /// <summary>테마 강조색 — 스폰 진행 막대 [실측 주황 rgb(242,149,32)].</summary>
        private static readonly Color AccentColor = new Color32(242, 149, 32, 255);

        private readonly List<(float Ratio, Color Color)> _topBar = new List<(float, Color)>();
        private readonly Dictionary<string, long> _teamTiles = new Dictionary<string, long>();
        private readonly List<string> _teamOrder = new List<string>();

        /// <summary>병력 증가 색을 바꾸는 주기 `[소스] t.ticks()%5==0&&(h(r>=f),p(r))`.</summary>
        private const int IncreaseCompareInterval = 5;

        private const string PlayerNamePrefsKey = "frontwars:username";
        private const string TutorialSeenPrefsKey = "frontwars:tutorial_seen";

        private FrontWarsGameRoot _root;
        private FrontWarsWorldView _world;
        private FrontWarsEventLog _events;
        private FrontWarsTutorialObjectives _tutorial;
        private FrontWarsPlayer _focusedPlayer;
        private FrontWarsPlayer _pinnedPlayer;
        private readonly List<(EFrontWarsRadialAction Action, string Label, bool Enabled)> _infoActions = new List<(EFrontWarsRadialAction, string, bool)>();
        private readonly int[] _infoCounts = new int[FrontWarsHudWindow.InfoCountTypes.Length];
        private readonly string[] _infoCountLabels = new string[FrontWarsHudWindow.InfoCountTypes.Length];

        /// <summary>오른쪽 클릭 「삭제」 가 찾는 내 유닛 거리 — 맨해튼 5칸 `[소스] na=5 · ra=e=>…manhattanDist(…)<=na`.</summary>
        private const int DeleteSearchRange = 5;

        /// <summary>정보 판 「보낸 핵」 — 아직 날아가는 중인 핵 [실측 설명 창 「정보 메뉴」].</summary>
        private static readonly EFrontWarsUnit[] NukeTypes =
        {
            EFrontWarsUnit.AtomBomb, EFrontWarsUnit.HydrogenBomb, EFrontWarsUnit.MIRV, EFrontWarsUnit.MIRVWarhead
        };

        /// <summary>퀵 채팅 문구 키 앞머리 — 문구 표에서 카테고리 · 문구 목록을 만든다 `[소스] ko.json:quick_chat.phrases`.</summary>
        private const string QuickChatPrefix = "quick_chat.";

        /// <summary>카테고리 차례 `[소스] ko.json:quick_chat.categories` — 처음 고른 칸은 「도움」.</summary>
        private static readonly string[] QuickChatCategoryOrder = { "help", "attack", "defend", "greet", "misc", "warnings" };
        private readonly FrontWarsSingleplayerOptions _options = new FrontWarsSingleplayerOptions();
        private readonly bool[] _filters = { true, true, true, true, true };
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _eventsCollapsed;
        private bool _leaderboardExpanded;

        /// <summary>「게임 속도」 판이 열렸나 · 고른 속도 칸 (0 ×0.5 · 1 ×1 · 2 ×2 · 3 Max).</summary>
        private bool _speedPanelOpen;
        private int _speedIndex = NormalSpeedIndex;
        private bool _gameOverShown;
        private double _lastIncrease;
        private bool _increasing = true;
        private int _radialTile = -1;
        private FrontWarsPlayer _radialTarget;

        private static FrontWarsTextDataContainer Texts => GameRoot.Instance.FrontWarsTextDataContainer;

        public void Bind(FrontWarsGameRoot root, FrontWarsWorldView world)
        {
            _root = root;
            _world = world;
        }

        protected override void AddWindows()
        {
            RegisterWindow(FrontWarsMenuWindow.Key);
            RegisterWindow(FrontWarsSingleplayerWindow.Key);
            RegisterWindow(FrontWarsHudWindow.Key);
            RegisterWindow(FrontWarsGameOverWindow.Key);
            RegisterWindow(FrontWarsRadialMenuWindow.Key);
            RegisterWindow(FrontWarsTutorialWindow.Key);
            RegisterWindow(FrontWarsInstructionsWindow.Key);
            RegisterWindow(FrontWarsSettingsWindow.Key);
            RegisterWindow(FrontWarsQuickChatWindow.Key);
        }

        protected override void OnInitialize()
        {
            _root.GameFlow.OnScreenChanged += HandleScreenChanged;
            _root.Game.OnGameStarted += HandleGameStarted;
            _root.Game.OnTick += HandleTick;
            _world.Input.OnContextMenu += HandleContextMenu;
            _world.Input.OnCancel += HandleCancel;
            _world.Input.OnUiHiddenChanged += HandleUiHidden;
            _world.Input.OnAttackRatioChanged += HandleInputAttackRatio;
            _world.Input.OnFocusedPlayer += HandleFocusedPlayer;
        }

        protected override void OnDispose()
        {
            _cts.Cancel();
            _root.GameFlow.OnScreenChanged -= HandleScreenChanged;
            _root.Game.OnGameStarted -= HandleGameStarted;
            _root.Game.OnTick -= HandleTick;
            _world.Input.OnContextMenu -= HandleContextMenu;
            _world.Input.OnCancel -= HandleCancel;
            _world.Input.OnUiHiddenChanged -= HandleUiHidden;
            _world.Input.OnAttackRatioChanged -= HandleInputAttackRatio;
            _world.Input.OnFocusedPlayer -= HandleFocusedPlayer;
            _events?.Dispose();
            _tutorial?.Dispose();
        }

        // ── 화면 전이

        private void HandleScreenChanged(EFrontWarsScreenType previous, EFrontWarsScreenType next)
        {
            switch (next)
            {
                case EFrontWarsScreenType.Menu:
                    CloseAllWindows();
                    OpenMenu();
                    break;
                case EFrontWarsScreenType.SingleplayerSetup:
                    CloseWindow(FrontWarsMenuWindow.Key);
                    OpenSingleplayer();
                    break;
                case EFrontWarsScreenType.Game:
                    CloseWindow(FrontWarsMenuWindow.Key);
                    CloseWindow(FrontWarsSingleplayerWindow.Key);
                    OpenHud();
                    break;
            }
        }

        private void OpenMenu()
        {
            FrontWarsMenuWindow menu = GetWindow(FrontWarsMenuWindow.Key);
            menu.Show(Texts.Ko, PlayerPrefs.GetString(PlayerNamePrefsKey, string.Empty));
            menu.OnSingleplayer -= HandleMenuSingleplayer;
            menu.OnSingleplayer += HandleMenuSingleplayer;
            menu.OnTutorial -= HandleMenuTutorial;
            menu.OnTutorial += HandleMenuTutorial;
            menu.OnNameChanged -= HandleNameChanged;
            menu.OnNameChanged += HandleNameChanged;
            menu.OnInstructions -= HandleMenuInstructions;
            menu.OnInstructions += HandleMenuInstructions;
            menu.Open();
        }

        private void OpenSingleplayer()
        {
            FrontWarsSingleplayerWindow window = GetWindow(FrontWarsSingleplayerWindow.Key);
            int nations = GameRoot.Instance.FrontWarsNationDataContainer.ForMap(_options.MapName).Count;
            window.Show(Texts.Ko, _options, nations);
            window.OnStart -= HandleSingleplayerStart;
            window.OnStart += HandleSingleplayerStart;
            window.OnBack -= HandleSingleplayerBack;
            window.OnBack += HandleSingleplayerBack;
            window.Open();
            LoadMapThumbnail(window, _options.MapName);
        }

        /// <summary>맵 썸네일은 읽는 데 시간이 걸린다 — 오면 넣는다 (창이 닫혔으면 버린다).</summary>
        private async void LoadMapThumbnail(FrontWarsSingleplayerWindow window, string mapName)
        {
            try
            {
                Sprite sprite = await FrontWarsSession.LoadMapThumbnailAsync(mapName, _cts.Token);

                if (window != null && sprite != null)
                    window.SetMapThumbnail(sprite);
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        private void HandleMenuSingleplayer() => _root.GameFlow.ChangeScreen(EFrontWarsScreenType.SingleplayerSetup);
        private void HandleSingleplayerBack() => _root.GameFlow.ChangeScreen(EFrontWarsScreenType.Menu);

        /// <summary>게임 방법 — 메뉴 위에 뜨는 창. 화면은 메뉴 그대로다.</summary>
        private void HandleMenuInstructions()
        {
            FrontWarsInstructionsWindow window = GetWindow(FrontWarsInstructionsWindow.Key);
            window.Show(Texts.Ko);
            window.OnClose -= HandleInstructionsClose;
            window.OnClose += HandleInstructionsClose;
            window.Open();
        }

        private void HandleInstructionsClose() => CloseWindow(FrontWarsInstructionsWindow.Key);

        private void HandleNameChanged(string name)
        {
            PlayerPrefs.SetString(PlayerNamePrefsKey, name ?? string.Empty);
        }

        private string PlayerName => PlayerPrefs.GetString(PlayerNamePrefsKey, string.Empty);

        private async void HandleSingleplayerStart(FrontWarsSingleplayerOptions options)
        {
            try
            {
                await FrontWarsSession.StartSingleplayerAsync(_root.Game, options, PlayerName, _cts.Token);
                _root.GameFlow.ChangeScreen(EFrontWarsScreenType.Game);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Log.Error($"[FrontWars] 판을 못 열었다 — {e.Message}");
            }
        }

        /// <summary>튜토리얼 판 — 표의 튜토리얼 설정으로 연다 (`00_게임개요.md` 「튜토리얼」).</summary>
        private void HandleMenuTutorial()
        {
            FrontWarsTutorialData t = GameRoot.Instance.FrontWarsTutorialDataContainer.AllValues[0];

            var options = new FrontWarsSingleplayerOptions
            {
                MapName = t.Map,
                CompactMap = t.CompactMap,
                Difficulty = (EFrontWarsDifficulty)Enum.Parse(typeof(EFrontWarsDifficulty), t.Difficulty),
                EconomyMode = (EFrontWarsEconomyMode)Enum.Parse(typeof(EFrontWarsEconomyMode), t.EconomyMode),
                Nations = t.DisableNations ? 0 : (int?)null,
                Bots = t.Bots,
                InfiniteGold = t.InfiniteGold,
                InfiniteTroops = t.InfiniteTroops,
                InstantBuild = t.InstantBuild,
                DonateGold = t.DonateGold,
                DonateTroops = t.DonateTroops,
                DisableAlliances = t.DisableAlliances,
                Tutorial = true,
                SpawnPhaseTurns = t.SpawnPhaseTurns,
                StartingTroops = t.StartingTroops
            };

            foreach (string code in (t.DisabledUnits ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                options.DisabledUnits.Add((EFrontWarsUnit)Enum.Parse(typeof(EFrontWarsUnit), code));

            PlayerPrefs.SetInt(TutorialSeenPrefsKey, 1);
            HandleSingleplayerStart(options);
        }

        // ── 판

        private void HandleGameStarted(FrontWarsGame game)
        {
            _events?.Dispose();
            _events = new FrontWarsEventLog(game, () => _root.Game.LocalPlayer, Texts);
            _gameOverShown = false;
            _speedPanelOpen = false;
            _speedIndex = NormalSpeedIndex;
            _root.Game.SpeedMultiplier = SpeedMultipliers[NormalSpeedIndex];
            game.OnWinner += HandleWinner;
            game.OnTeamWinner += HandleTeamWinner;
            game.OnEmoji += HandleEmoji;
            game.Diplomacy.OnBroken += HandleAllianceBroken;
            _betrayHoldUntil = -1f;

            ApplySavedSettings();

            _tutorial?.Dispose();
            _tutorial = game.Settings.Tutorial ? new FrontWarsTutorialObjectives(game, () => _root.Game.LocalPlayer) : null;
            CloseWindow(FrontWarsTutorialWindow.Key);
        }

        private void OpenHud()
        {
            FrontWarsHudWindow hud = GetWindow(FrontWarsHudWindow.Key);
            hud.ApplyStaticTexts(Texts.Ko);

            hud.OnAttackRatioChanged -= HandleHudAttackRatio;
            hud.OnAttackRatioChanged += HandleHudAttackRatio;
            hud.OnSendTroopsRatioChanged -= HandleSendTroopsRatio;
            hud.OnSendTroopsRatioChanged += HandleSendTroopsRatio;
            hud.OnSendGoldRatioChanged -= HandleSendGoldRatio;
            hud.OnSendGoldRatioChanged += HandleSendGoldRatio;
            hud.OnBuildSelected -= HandleBuildSelected;
            hud.OnBuildSelected += HandleBuildSelected;
            hud.OnPause -= HandlePause;
            hud.OnPause += HandlePause;
            hud.OnExit -= HandleExit;
            hud.OnExit += HandleExit;
            hud.OnSettings -= HandleSettings;
            hud.OnSettings += HandleSettings;
            hud.OnLeaderboardToggle -= HandleLeaderboardToggle;
            hud.OnLeaderboardToggle += HandleLeaderboardToggle;
            hud.OnTeamButton -= HandleTeamButton;
            hud.OnTeamButton += HandleTeamButton;
            hud.OnReplay -= HandleSpeedPanel;
            hud.OnReplay += HandleSpeedPanel;
            hud.OnSpeedSelected -= HandleSpeedSelected;
            hud.OnSpeedSelected += HandleSpeedSelected;
            hud.OnLeaderboardExpand -= HandleLeaderboardExpand;
            hud.OnLeaderboardExpand += HandleLeaderboardExpand;
            hud.OnFocusPlayer -= HandleFocusPlayer;
            hud.OnFocusPlayer += HandleFocusPlayer;
            hud.OnFilterToggle -= HandleFilterToggle;
            hud.OnFilterToggle += HandleFilterToggle;
            hud.OnEventsHide -= HandleEventsHide;
            hud.OnEventsHide += HandleEventsHide;
            hud.OnEventClicked -= HandleEventClicked;
            hud.OnEventClicked += HandleEventClicked;
            hud.OnAllianceReply -= HandleAllianceReply;
            hud.OnAllianceReply += HandleAllianceReply;
            hud.OnInfoAction -= HandleInfoAction;
            hud.OnInfoAction += HandleInfoAction;

            hud.SetLeaderboardVisible(true);
            hud.Open();
            hud.AlertFrame?.ResetState();
        }

        private void HandleTick(int tick)
        {
            _events?.Tick();
            _tutorial?.Tick();
        }

        private void LateUpdate()
        {
            if (IsInitialized == false || _root == null)
                return;

            FrontWarsGame game = _root.Game.Game;
            FrontWarsHudWindow hud = WindowManagement.Instance.FindCreated(FrontWarsHudWindow.Key) as FrontWarsHudWindow;

            if (game == null || hud == null || hud.IsOpen() == false)
                return;

            FrontWarsPlayer me = _root.Game.LocalPlayer;
            bool spawn = game.InSpawnPhase;

            hud.SetSpawnPhase(spawn, me == null || me.HasSpawned == false);
            hud.SetEconomy(Texts.Ko($"hud.economy.{game.Settings.EconomyMode}"));
            hud.SetEconomyOffset(EconomyX(hud, game, me, spawn));
            hud.SetTimer(TimerText(game));
            hud.SetSpeedPanel(_speedPanelOpen, _speedIndex);
            hud.SetTopBar(TopBarSegments(game));
            hud.SetPaused(_root.Game.Paused);
            UpdateTutorial(game, me, hud);
            UpdateAlertFrame(hud, me);

            // 성능 표시 켜고 끄기 [실측 설명 「성능 정보 표시 (게임 중 Shift-D)」]
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.dKey.wasPressedThisFrame && keyboard.shiftKey.isPressed)
                ToggleSetting(EFrontWarsSetting.PerformanceOverlay);

            if (spawn || me == null)
                return;

            UpdateLeaderboard(hud, game, me);
            UpdatePlayerInfo(hud, me);
            UpdateControl(hud, game, me);
            UpdateBuildBar(hud, game, me);
            UpdateEvents(hud);
        }

        /// <summary>
        /// 스폰 단계엔 「-」, 그 뒤엔 스폰 끝부터 센 시간 `[소스] QPQdQ6Sy.js:n>0&&(r=`${n}m `+r),t>0&&(r=`${t}h `+r)`.
        /// <para>「49s」 · 「1m 5s」 · 1시간이 넘으면 「1h 2m 3s」 (분이 0이면 분을 뺀다). 게임 시간을 켠 판은 남은 시간 「M:SS」.</para>
        /// </summary>
        private static string TimerText(FrontWarsGame game)
        {
            if (game.InSpawnPhase)
                return Texts.Ko("hud.timer_spawn");

            int seconds = (game.Ticks - game.Settings.SpawnPhaseTurns) / 10;

            if (game.Settings.GameLengthMinutes.HasValue)
            {
                int remaining = Math.Max(0, game.Settings.GameLengthMinutes.Value * 60 - seconds);
                return $"{remaining / 60}:{remaining % 60:00}";
            }

            if (seconds <= 0)
                return Texts.Ko("hud.timer_spawn");

            int hours = seconds / 3600;
            int minutes = seconds % 3600 / 60;
            string text = $"{seconds % 60}s";

            if (minutes > 0)
                text = $"{minutes}m " + text;

            if (hours > 0)
                text = $"{hours}h " + text;

            return text;
        }

        private void UpdateLeaderboard(FrontWarsHudWindow hud, FrontWarsGame game, FrontWarsPlayer me)
        {
            // 팀전이면 왼쪽 위에 팀 색 글자 「Team: 이름」 — 누르면 순위표를 접고 편다 [실측 probe/followup_team.json · aria 「Toggle Leaderboard」]
            bool teamMode = IsTeamLabelShown(game, me);
            hud.SetTeamButton(teamMode, teamMode ? Texts.Format("leaderboard.team_label", me.Team) : null,
                              teamMode ? (Color)_world.MapRenderer.Colors.TeamColor(me.Team) : Color.white);

            // 접으면 위 4줄(+ 내가 그 안에 없으면 내 줄), 펴면 모든 플레이어 `[소스] if(!s)return l.slice(0,Br)` (Br=4)
            List<FrontWarsPlayer> sorted = game.Players().OrderByDescending(p => p.NumTilesOwned).ToList();
            var rows = new List<FrontWarsLeaderboardRow>();
            double land = Math.Max(1, game.NumLandTiles);
            int myRank = sorted.IndexOf(me);
            int top = _leaderboardExpanded ? sorted.Count : LeaderboardTopRows;

            for (int i = 0; i < sorted.Count && rows.Count < top; i++)
                rows.Add(Row(sorted[i], i + 1, land, sorted[i] == me));

            if (_leaderboardExpanded == false && myRank >= top)
                rows.Add(Row(me, myRank + 1, land, true));

            hud.SetLeaderboard(rows, _leaderboardExpanded ? Texts.Ko("leaderboard.less") : Texts.Ko("leaderboard.more"), _leaderboardExpanded);
        }

        private static bool IsTeamLabelShown(FrontWarsGame game, FrontWarsPlayer me)
        {
            return game.Settings.IsTeamMode && me != null && string.IsNullOrEmpty(me.Team) == false;
        }

        /// <summary>
        /// 경제 방식 글자 자리 — 순위표가 열리면 338, 닫히면 44 [실측].
        /// <para>팀전은 팀 글자 바로 뒤 8px — 순위표를 접으면 팀 글자가 제 폭으로 줄어 경제 글자도 따라온다 [실측 접으면 44+65+8 = 117].</para>
        /// </summary>
        private float EconomyX(FrontWarsHudWindow hud, FrontWarsGame game, FrontWarsPlayer me, bool spawn)
        {
            bool open = hud.LeaderboardVisible && spawn == false;

            if (open)
                return EconomyOpenX;

            if (spawn == false && IsTeamLabelShown(game, me))
                return EconomyClosedX + hud.TeamLabelWidth + TeamLabelGap;

            return EconomyClosedX;
        }

        /// <summary>
        /// 화면 맨 위 10px 막대 `[소스] QPQdQ6Sy.js function ea(){` —
        /// 스폰 단계엔 스폰 진행(강조색 하나), 팀전엔 팀마다 땅 비율(팀 색). 개인전 스폰 뒤엔 바탕만 남는다.
        /// </summary>
        private List<(float Ratio, Color Color)> TopBarSegments(FrontWarsGame game)
        {
            _topBar.Clear();

            if (game.InSpawnPhase)
            {
                float ratio = Mathf.Clamp01(game.Ticks / (float)Math.Max(1, game.Settings.SpawnPhaseTurns));
                _topBar.Add((ratio, AccentColor));
                return _topBar;
            }

            if (game.Settings.IsTeamMode == false)
                return _topBar;

            _teamTiles.Clear();
            _teamOrder.Clear();
            long total = 0;

            foreach (FrontWarsPlayer p in game.Players())
            {
                if (string.IsNullOrEmpty(p.Team))
                    continue;

                if (_teamTiles.ContainsKey(p.Team) == false)
                {
                    _teamTiles[p.Team] = 0;
                    _teamOrder.Add(p.Team);
                }

                _teamTiles[p.Team] += p.NumTilesOwned;
                total += p.NumTilesOwned;
            }

            if (total == 0)
                return _topBar;

            foreach (string team in _teamOrder)
                _topBar.Add((_teamTiles[team] / (float)total, _world.MapRenderer.Colors.TeamColor(team)));

            return _topBar;
        }

        private static FrontWarsLeaderboardRow Row(FrontWarsPlayer p, int rank, double land, bool isMe)
        {
            string owned = (Math.Floor(p.NumTilesOwned / land * 1000) / 10).ToString("F1") + "%";
            return new FrontWarsLeaderboardRow(rank, p.Name, owned, FrontWarsNumberFormat.Gold(p.Gold), FrontWarsNumberFormat.Troops(p.Troops), p.Id, isMe);
        }

        private void UpdateControl(FrontWarsHudWindow hud, FrontWarsGame game, FrontWarsPlayer me)
        {
            double rate = game.Rules.TroopIncreaseRate(me);

            if (game.Ticks % IncreaseCompareInterval == 0)
            {
                _increasing = rate >= _lastIncrease;
                _lastIncrease = rate;
            }

            FrontWarsMapInput input = _world.Input;
            string increase = (rate > 0 ? "+" : string.Empty) + FrontWarsNumberFormat.Troops(Math.Abs(rate));

            hud.SetControl(
                FrontWarsNumberFormat.Troops(me.Troops),
                FrontWarsNumberFormat.Troops(game.Rules.MaxTroops(me)),
                increase,
                _increasing,
                FrontWarsNumberFormat.Gold(me.Gold),
                input.AttackRatio,
                FrontWarsNumberFormat.Number(Math.Floor(me.Troops / 10.0 * input.AttackRatio), 1),
                input.SendTroopsRatio,
                FrontWarsNumberFormat.Number(Math.Floor(me.Troops / 10.0 * input.SendTroopsRatio)),
                input.SendGoldRatio,
                FrontWarsNumberFormat.Number(Math.Floor(me.Gold * (double)input.SendGoldRatio)));
        }

        private void UpdateBuildBar(FrontWarsHudWindow hud, FrontWarsGame game, FrontWarsPlayer me)
        {
            // 팀전이면 팀 색 변형 — 플레이어를 넘기는 쪽이 팀을 본다
            Color owner = _world.MapRenderer.Colors != null ? (Color)_world.MapRenderer.Colors.TerritoryColor(me) : Color.white;

            for (int i = 0; i < FrontWarsHudWindow.BuildSlots.Length; i++)
            {
                EFrontWarsUnit type = FrontWarsHudWindow.BuildSlots[i];
                bool affordable = game.Rules.IsUnitDisabled(type) == false && me.Gold >= game.Rules.UnitCost(type, me);
                hud.SetBuildSlot(i, me.UnitsOwned(type), affordable, _world.Input.PlacementUnit == type, owner);
            }
        }

        private readonly List<FrontWarsEvent> _visibleEvents = new List<FrontWarsEvent>();

        private void UpdateEvents(FrontWarsHudWindow hud)
        {
            _visibleEvents.Clear();

            if (_events != null)
            {
                foreach (FrontWarsEvent e in _events.Events.OrderBy(e => e.Priority).ThenBy(e => e.Tick))
                {
                    if (_filters[(int)e.Category])
                        _visibleEvents.Add(e);
                }
            }

            int start = Math.Max(0, _visibleEvents.Count - hud.EventCapacity);
            var lines = new List<(string, Color, bool, bool)>();

            for (int i = start; i < _visibleEvents.Count; i++)
            {
                FrontWarsEvent e = _visibleEvents[i];
                bool reply = e.AllianceRequest != null && e.AllianceRequest.Status == EFrontWarsRequestStatus.Pending;
                bool renew = string.IsNullOrEmpty(e.RenewWithName) == false;
                lines.Add((e.Text, FrontWarsEventLog.ColorOf(e.Category), reply, renew));
            }

            if (start > 0)
                _visibleEvents.RemoveRange(0, start);

            hud.SetEvents(lines, _eventsCollapsed);

            for (int i = 0; i < _filters.Length; i++)
                hud.SetFilter(i, _filters[i]);
        }

        // ── HUD 조작

        private void HandleHudAttackRatio(float ratio) => _world.Input.SetAttackRatio(ratio);
        private void HandleInputAttackRatio(float ratio) { }
        private void HandleSendTroopsRatio(float ratio) => _world.Input.SendTroopsRatio = ratio;
        private void HandleSendGoldRatio(float ratio) => _world.Input.SendGoldRatio = ratio;
        private void HandleBuildSelected(EFrontWarsUnit type) => _world.Input.SetPlacement(_world.Input.PlacementUnit == type ? (EFrontWarsUnit?)null : type);
        private void HandlePause() => _root.Game.Paused = _root.Game.Paused == false;
        // ── 설정 창 — 값은 PlayerPrefs 에 둔다 (원본 키 이름을 따른다 `[소스] settings.attackRatio · settings.specialEffects · settings.structureSprites`)

        /// <summary>저장 키 — 원본이 쓰는 이름을 그대로 쓴다 (원본에서 못 읽은 항목은 같은 꼴로 지었다).</summary>
        private static readonly Dictionary<EFrontWarsSetting, string> SettingKeys = new Dictionary<EFrontWarsSetting, string>
        {
            { EFrontWarsSetting.LeftClickMenu, "frontwars:settings.leftClickOpensMenu" },
            { EFrontWarsSetting.AttackRatio, "frontwars:settings.attackRatio" },
            { EFrontWarsSetting.Emoji, "frontwars:settings.emojis" },
            { EFrontWarsSetting.AnonymousNames, "frontwars:settings.anonymousNames" },
            { EFrontWarsSetting.HideLobbyId, "frontwars:settings.lobbyIdVisibility" },
            { EFrontWarsSetting.UiScale, "frontwars:settings.uiScale" },
            { EFrontWarsSetting.AlertFrame, "frontwars:settings.alertFrame" },
            { EFrontWarsSetting.SpecialEffects, "frontwars:settings.specialEffects" },
            { EFrontWarsSetting.StructureSprites, "frontwars:settings.structureSprites" },
            { EFrontWarsSetting.TerritoryPattern, "frontwars:settings.territoryPatterns" },
            { EFrontWarsSetting.PerformanceOverlay, "frontwars:settings.performanceOverlay" },
            { EFrontWarsSetting.DarkMap, "frontwars:settings.darkMode" }
        };

        /// <summary>UI 크기 100% 일 때 기준 해상도 — 크게 하면 기준을 줄여 UI 가 커진다.</summary>
        private static readonly Vector2 UiReferenceResolution = new Vector2(1920f, 1080f);

        private readonly Dictionary<EFrontWarsSetting, float> _settings = new Dictionary<EFrontWarsSetting, float>();
        private CanvasScaler _uiScaler;
        private bool _alertFrameEnabled = true;

        /// <summary>창이 쓰는 단위로 읽는다 — 스위치는 0/1, 슬라이더는 퍼센트. 공격 비율만 저장은 0~1 이다 (원본과 같게).</summary>
        private static float ReadSetting(FrontWarsSettingCard card)
        {
            string key = SettingKeys[card.Setting];

            if (card.Setting == EFrontWarsSetting.AttackRatio)
                return Mathf.Round(PlayerPrefs.GetFloat(key, card.Default / 100f) * 100f);

            return card.Kind == EFrontWarsSettingKind.Slider
                ? PlayerPrefs.GetFloat(key, card.Default)
                : PlayerPrefs.GetInt(key, Mathf.RoundToInt(card.Default));
        }

        private static void WriteSetting(FrontWarsSettingCard card, float value)
        {
            string key = SettingKeys[card.Setting];

            if (card.Setting == EFrontWarsSetting.AttackRatio)
                PlayerPrefs.SetFloat(key, value / 100f);
            else if (card.Kind == EFrontWarsSettingKind.Slider)
                PlayerPrefs.SetFloat(key, value);
            else
                PlayerPrefs.SetInt(key, value > 0.5f ? 1 : 0);
        }

        /// <summary>저장된 값을 모두 읽어 월드 · UI 에 적용한다 (판이 붙을 때 · 초기화한 뒤).</summary>
        private void ApplySavedSettings()
        {
            _settings.Clear();

            foreach (FrontWarsSettingCard card in FrontWarsSettingsWindow.Cards)
            {
                float value = ReadSetting(card);
                _settings[card.Setting] = value;
                ApplySetting(card.Setting, value);
            }
        }

        private void ApplySetting(EFrontWarsSetting setting, float value)
        {
            bool on = value > 0.5f;

            switch (setting)
            {
                case EFrontWarsSetting.LeftClickMenu:
                    if (_world != null && _world.Input != null)
                        _world.Input.LeftClickOpensMenu = on;
                    break;
                case EFrontWarsSetting.AttackRatio:
                    if (_world != null && _world.Input != null)
                        _world.Input.SetAttackRatio(value / 100f);
                    break;
                case EFrontWarsSetting.Emoji:
                    if (_world != null && _world.FxView != null)
                        _world.FxView.EmojisEnabled = on;
                    break;
                case EFrontWarsSetting.AnonymousNames:
                    if (_world != null && _world.NameView != null)
                        _world.NameView.AnonymousNames = on;
                    break;
                case EFrontWarsSetting.HideLobbyId:
                    // 혼자 하는 판엔 로비가 없다 — 값만 저장한다 (의도된 차이)
                    break;
                case EFrontWarsSetting.UiScale:
                    ApplyUiScale(value);
                    break;
                case EFrontWarsSetting.AlertFrame:
                    _alertFrameEnabled = on;
                    break;
                case EFrontWarsSetting.SpecialEffects:
                    if (_world != null && _world.FxView != null)
                        _world.FxView.EffectsEnabled = on;
                    break;
                case EFrontWarsSetting.StructureSprites:
                    if (_world != null && _world.UnitView != null)
                        _world.UnitView.ShowStructures = on;
                    break;
                case EFrontWarsSetting.TerritoryPattern:
                    // 원본도 값만 저장한다 — 맵 그리기가 이 설정을 읽지 않고, 무늬는 상점 치장품을 산 플레이어만 갖는다 `[소스] territoryPatterns 는 설정 창에만 있음`
                    break;
                case EFrontWarsSetting.PerformanceOverlay:
                    if (_world != null && _world.PerfOverlay != null)
                    {
                        _world.PerfOverlay.OnCloseRequested -= HandlePerfOverlayClose;
                        _world.PerfOverlay.OnCloseRequested += HandlePerfOverlayClose;
                        _world.PerfOverlay.Visible = on;
                    }

                    break;
                case EFrontWarsSetting.DarkMap:
                    if (_world != null && _world.MapRenderer != null)
                        _world.MapRenderer.SetTheme(on);
                    break;
            }
        }

        private void ApplyUiScale(float percent)
        {
            if (_uiScaler == null)
                _uiScaler = FindFirstObjectByType<CanvasScaler>();

            if (_uiScaler == null)
                return;

            float scale = Mathf.Clamp(percent, 50f, 150f) / 100f;
            _uiScaler.referenceResolution = UiReferenceResolution / scale;
        }

        private void HandleSettings()
        {
            FrontWarsSettingsWindow window = GetWindow(FrontWarsSettingsWindow.Key);
            window.Show(Texts.Ko, _settings);
            window.OnBack -= HandleSettingsBack;
            window.OnBack += HandleSettingsBack;
            window.OnSettingChanged -= HandleSettingChanged;
            window.OnSettingChanged += HandleSettingChanged;
            window.OnResetRequested -= HandleSettingsReset;
            window.OnResetRequested += HandleSettingsReset;
            window.Open();
        }

        private void HandleSettingsBack() => CloseWindow(FrontWarsSettingsWindow.Key);

        private void HandleSettingChanged(EFrontWarsSetting setting, float value)
        {
            int index = FrontWarsSettingsWindow.IndexOf(setting);

            if (index < 0)
                return;

            _settings[setting] = value;
            WriteSetting(FrontWarsSettingsWindow.Cards[index], value);
            ApplySetting(setting, value);
        }

        /// <summary>모든 설정 초기화 — 저장을 지우고 기본값으로 되돌린다 [실측 버튼 「모든 설정 초기화」].</summary>
        private void HandleSettingsReset()
        {
            foreach (string key in SettingKeys.Values)
                PlayerPrefs.DeleteKey(key);

            ApplySavedSettings();

            FrontWarsSettingsWindow window = GetWindow(FrontWarsSettingsWindow.Key);

            foreach (var pair in _settings)
                window.SetValue(pair.Key, pair.Value);
        }

        /// <summary>설정 하나만 바깥에서 바꾼다 (성능 표시 Shift+D 처럼).</summary>
        private void ToggleSetting(EFrontWarsSetting setting)
        {
            float value = _settings.TryGetValue(setting, out float current) && current > 0.5f ? 0f : 1f;
            HandleSettingChanged(setting, value);

            FrontWarsSettingsWindow window = GetWindow(FrontWarsSettingsWindow.Key);

            if (window != null)
                window.SetValue(setting, value);
        }

        private void HandleLeaderboardToggle()
        {
            FrontWarsHudWindow hud = GetWindow(FrontWarsHudWindow.Key);
            hud.SetLeaderboardVisible(hud.LeaderboardVisible == false);
        }

        private void HandleLeaderboardExpand() => _leaderboardExpanded = _leaderboardExpanded == false;

        private void HandleTeamButton() => HandleLeaderboardToggle();

        /// <summary>다시 보기 버튼 — 「게임 속도」 판을 열고 닫는다 [실측 probe/followup_replay.json].</summary>
        private void HandleSpeedPanel() => _speedPanelOpen = _speedPanelOpen == false;

        /// <summary>속도 고르기 — 턴 간격 × 배수 `[소스] turnIntervalMs()*this.speedMultiplier` · Slow=2 · Normal=1 · Fast=.5 · Fastest=0.</summary>
        private void HandleSpeedSelected(int index)
        {
            if (index < 0 || index >= SpeedMultipliers.Length)
                return;

            _speedIndex = index;
            _root.Game.SpeedMultiplier = SpeedMultipliers[index];
        }

        private void HandleFocusPlayer(string playerId)
        {
            FrontWarsGame game = _root.Game.Game;
            if (game == null || game.HasPlayer(playerId) == false)
                return;

            FrontWarsPlayer p = game.Player(playerId);
            if (p.IsAlive == false)
                return;

            FrontWarsNameLocation location = FrontWarsNamePlacement.Compute(game, p);
            _world.CameraController.FocusOn(location.X, location.Y, false);
        }

        private void HandleFilterToggle(int index) => _filters[index] = _filters[index] == false;

        private void HandleEventsHide()
        {
            _eventsCollapsed = _eventsCollapsed == false;
            GetWindow(FrontWarsHudWindow.Key).SetEventsCollapsed(_eventsCollapsed, Texts.Ko(_eventsCollapsed ? "events.show" : "events.hide"));
        }

        /// <summary>동맹 요청 줄의 수락 · 거절 버튼 [실측 원본 동맹 요청 줄에 버튼 두 개].</summary>
        private void HandleAllianceReply(int index, bool accept)
        {
            if (index >= _visibleEvents.Count)
                return;

            FrontWarsEvent e = _visibleEvents[index];

            // 「연장」 줄 — 곧 끝나는 동맹을 이름으로 찾아 연장한다
            if (string.IsNullOrEmpty(e.RenewWithName) == false)
            {
                if (accept == false)
                {
                    _events.Remove(e);
                    return;
                }

                FrontWarsPlayer ally = _root.Game.Game?.Players().FirstOrDefault(p => p.Name == e.RenewWithName);

                if (ally != null)
                    _root.Game.Submit(new FrontWarsAllianceExtensionIntent { RecipientId = ally.Id });

                _events.Remove(e);
                return;
            }

            if (e.AllianceRequest == null || e.AllianceRequest.Status != EFrontWarsRequestStatus.Pending)
                return;

            _root.Game.Submit(new FrontWarsAllianceReplyIntent { RequestorId = e.AllianceRequest.Requestor.Id, Accept = accept });
            _events.Remove(e);
        }

        /// <summary>이벤트 누르기 — 동맹 요청이면 수락, 아니면 그 나라로 카메라.</summary>
        private void HandleEventClicked(int index)
        {
            if (index >= _visibleEvents.Count)
                return;

            FrontWarsEvent e = _visibleEvents[index];

            if (e.AllianceRequest != null && e.AllianceRequest.Status == EFrontWarsRequestStatus.Pending)
            {
                _root.Game.Submit(new FrontWarsAllianceReplyIntent { RequestorId = e.AllianceRequest.Requestor.Id, Accept = true });
                _events.Remove(e);
                return;
            }

            if (e.FocusPlayerId != null)
                HandleFocusPlayer(e.FocusPlayerId);
        }

        private void HandleExit()
        {
            _root.Game.StopGame();
            _tutorial?.Dispose();
            _tutorial = null;
            _world.Input.SetPlacement(null);
            _root.GameFlow.ChangeScreen(EFrontWarsScreenType.Menu);
        }

        private void HandleUiHidden(bool hidden)
        {
            FrontWarsHudWindow hud = WindowManagement.Instance.FindCreated(FrontWarsHudWindow.Key) as FrontWarsHudWindow;
            if (hud != null)
                hud.SetEnable(hidden == false);
        }

        private void HandleCancel()
        {
            _pinnedPlayer = null;
            CloseWindow(FrontWarsRadialMenuWindow.Key);
        }

        /// <summary>정보 판 외교 버튼 — 붙잡은 플레이어를 대상으로 원형 메뉴 동작을 그대로 쓴다.</summary>
        private void HandleInfoAction(int index)
        {
            if (_pinnedPlayer == null || index < 0 || index >= _infoActions.Count || _infoActions[index].Enabled == false)
                return;

            _radialTarget = _pinnedPlayer;
            HandleRadialAction(_infoActions[index].Action);
        }

        // ── 플레이어 정보 판

        private void HandleFocusedPlayer(FrontWarsPlayer player) => _focusedPlayer = player;

        /// <summary>커서 아래 플레이어를 오른쪽 위 판에 보인다 [실측 — 커서를 옮기면 내용이 바뀐다].</summary>
        private void UpdatePlayerInfo(FrontWarsHudWindow hud, FrontWarsPlayer me)
        {
            if (_pinnedPlayer != null && _pinnedPlayer.IsAlive == false)
                _pinnedPlayer = null;

            FrontWarsPlayer p = _pinnedPlayer ?? _focusedPlayer;

            if (p == null || p.IsAlive == false)
            {
                hud.HidePlayerInfo();
                return;
            }

            // 붙잡은 남의 플레이어면 버튼 세 개 — 퀵 채팅 · 타겟 표시 · 무역 중단 [실측 설명 창 「정보 메뉴」]
            _infoActions.Clear();

            bool embargo = me.HasEmbargoAgainst(p);

            if (p == _pinnedPlayer && p != me)
            {
                _infoActions.Add((EFrontWarsRadialAction.QuickChat, Texts.Ko("radial.quick_chat"), true));
                _infoActions.Add((EFrontWarsRadialAction.Target, Texts.Ko("radial.target"), me.CanTarget(p)));
                _infoActions.Add(embargo
                    ? (EFrontWarsRadialAction.StopEmbargo, Texts.Ko("radial.stop_embargo"), true)
                    : (EFrontWarsRadialAction.Embargo, Texts.Ko("radial.embargo"), true));
            }

            hud.SetPlayerActions(_infoActions);

            // 원본은 나 자신도 「중립」 으로 보인다 [실측]
            string relation;
            if (p == me)
                relation = Texts.Ko("relation.Neutral");
            else if (me.IsAlliedWith(p))
                relation = Texts.Ko("relation.ally");
            else
                relation = Texts.Ko($"relation.{me.Relation(p)}");

            double attacking = 0;
            foreach (FrontWarsAttack attack in p.OutgoingAttacks)
                attacking += attack.Troops;

            for (int i = 0; i < _infoCounts.Length; i++)
            {
                EFrontWarsUnit type = FrontWarsHudWindow.InfoCountTypes[i];
                _infoCounts[i] = p.UnitsOwned(type);
                _infoCountLabels[i] = Texts.Ko($"info.count.{type}");
            }

            // 무역 상태 · 보낸 핵 · 배신자 [실측 설명 창 「정보 메뉴」 — 이름 · 골드 · 병력과 함께 보인다]
            bool tradeStopped = p != me && (embargo || p.HasEmbargoAgainst(me));
            int nukesSent = 0;

            foreach (EFrontWarsUnit type in NukeTypes)
                nukesSent += p.UnitsOwned(type);

            hud.SetPlayerInfo(
                p.Name,
                Texts.Ko($"player.type.{p.Type}"),
                relation,
                Texts.Ko("info.troops"),
                FrontWarsNumberFormat.Troops(p.Troops),
                Texts.Ko("info.attacking"),
                attacking > 0 ? FrontWarsNumberFormat.Troops(attacking) : null,
                FrontWarsNumberFormat.Gold(p.Gold),
                _infoCountLabels,
                _infoCounts,
                Texts.Ko("info.trade"),
                Texts.Ko(tradeStopped ? "info.trade.stopped" : "info.trade.open"),
                Texts.Ko("info.nukes_sent"),
                nukesSent.ToString(),
                Texts.Ko("info.traitor"),
                Texts.Ko(p.IsTraitor ? "info.yes" : "info.no"));
        }

        // ── 경고 프레임 · 이모지

        private float _betrayHoldUntil = -1f;

        /// <summary>
        /// 경고 테두리 목표 — 봇이 아닌 상대(국가 포함)의 진행 중 공격이 오거나, 5초 안에 배신당했을 때 `[소스] BMmXchgl.js:tn … update(t){`.
        /// 내가 없거나 죽었으면 끈다. 설정 「경고 프레임」 이 꺼지면 안 그린다.
        /// </summary>
        private void UpdateAlertFrame(FrontWarsHudWindow hud, FrontWarsPlayer me)
        {
            FrontWarsAlertFrame frame = hud.AlertFrame;

            if (frame == null)
                return;

            if (_alertFrameEnabled == false || me == null || me.IsAlive == false)
            {
                _betrayHoldUntil = -1f;
                frame.SetTargets(false, false);
                return;
            }

            bool attacked = false;

            foreach (FrontWarsAttack attack in me.IncomingAttacks)
            {
                if (attack.Retreating || attack.Troops <= 0 || attack.Attacker == null || attack.Attacker.Type == EFrontWarsPlayerType.Bot)
                    continue;

                attacked = true;
                break;
            }

            frame.SetTargets(attacked, Time.unscaledTime < _betrayHoldUntil);
        }

        /// <summary>성능 창 × — 설정 값도 함께 끈다 `[소스] q.togglePerformanceOverlay()`.</summary>
        // ── 퀵 채팅

        private string _quickChatRecipientId;

        /// <summary>
        /// 퀵 채팅 창을 연다 `[소스] QPQdQ6Sy.js:(0,Z.jsx)(ka,{open:f,…,recipientId:i?.id()})`.
        /// 카테고리 · 문구는 문구 표에서 모으고, 플레이어 목록은 살아 있는 사람을 이름순으로 준다.
        /// </summary>
        private void OpenQuickChat(string recipientId)
        {
            FrontWarsGame game = _root.Game.Game;
            FrontWarsPlayer me = _root.Game.LocalPlayer;

            if (game == null || me == null || string.IsNullOrEmpty(recipientId))
                return;

            _quickChatRecipientId = recipientId;
            var players = new List<(string Id, string Name)>();

            foreach (FrontWarsPlayer p in game.Players())
            {
                if (p.IsAlive)
                    players.Add((p.Id, p.Name));
            }

            players.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            FrontWarsQuickChatWindow window = GetWindow(FrontWarsQuickChatWindow.Key);
            window.Show(Texts.Ko, QuickChatCategories(), players);
            window.OnSend -= HandleQuickChatSend;
            window.OnSend += HandleQuickChatSend;
            window.Open();
        }

        /// <summary>문구 표에서 카테고리별 문구 목록을 만든다 — 키는 「quick_chat.<카테고리>.<문구>」.</summary>
        private static List<FrontWarsQuickChatCategory> QuickChatCategories()
        {
            var list = new List<FrontWarsQuickChatCategory>();

            foreach (string id in QuickChatCategoryOrder)
            {
                var category = new FrontWarsQuickChatCategory { Id = id, Label = Texts.Ko($"{QuickChatPrefix}category.{id}") };
                string prefix = $"{QuickChatPrefix}{id}.";

                foreach (FrontWarsTextData row in Texts.AllValues)
                {
                    if (row.Key.StartsWith(prefix, StringComparison.Ordinal) == false)
                        continue;

                    category.Keys.Add(row.Key.Substring(prefix.Length));
                    category.Phrases.Add(row.Ko);
                }

                if (category.Keys.Count > 0)
                    list.Add(category);
            }

            return list;
        }

        private void HandleQuickChatSend(string key, string targetId)
        {
            if (string.IsNullOrEmpty(_quickChatRecipientId))
                return;

            _root.Game.Submit(new FrontWarsQuickChatIntent { RecipientId = _quickChatRecipientId, Key = key, TargetId = targetId });
            _pinnedPlayer = null;
        }

        private void HandlePerfOverlayClose() => ToggleSetting(EFrontWarsSetting.PerformanceOverlay);

        private void HandleAllianceBroken(FrontWarsPlayer breaker, FrontWarsPlayer betrayed)
        {
            if (betrayed == _root.Game.LocalPlayer)
                _betrayHoldUntil = Time.unscaledTime + FrontWarsAlertFrame.BetrayHoldSeconds;
        }

        /// <summary>
        /// 이모지는 보낸 사람 이름표 위에 뜬다 `[소스] BMmXchgl.js:getEmoji(e){…for(let n of e.outgoingEmojis())if(n.recipientID===`AllPlayers`||n.recipientID===t)`.
        /// 받는 사람이 「모든 플레이어」 이거나 나일 때만 보인다.
        /// </summary>
        private void HandleEmoji(FrontWarsEmojiMessage message)
        {
            if (_world == null || _world.NameView == null)
                return;

            FrontWarsGame game = _root.Game.Game;
            FrontWarsPlayer me = _root.Game.LocalPlayer;
            FrontWarsPlayer sender = game?.PlayerBySmallId(message.SenderSmallId);

            if (sender == null || sender.IsAlive == false)
                return;

            bool toEveryone = message.RecipientSmallId == FrontWarsPlayer.AllPlayersId;

            if (toEveryone == false && (me == null || message.RecipientSmallId != me.SmallId))
                return;

            _world.NameView.ShowEmoji(sender, message.Emoji);
        }

        /// <summary>그 칸에서 맨해튼 5칸 안의 내 유닛 중 가장 가까운 것 (종류 무관) `[소스] e.myPlayer.units().filter(…<=na).sort(…)[0]`.</summary>
        private static FrontWarsUnit NearestOwnUnit(FrontWarsGame game, FrontWarsPlayer me, int tile)
        {
            FrontWarsUnit best = null;
            int bestDist = int.MaxValue;

            foreach (FrontWarsUnit unit in me.AllUnits)
            {
                int d = game.Map.ManhattanDistance(unit.Tile, tile);

                if (d <= DeleteSearchRange && d < bestDist)
                {
                    bestDist = d;
                    best = unit;
                }
            }

            return best;
        }

        // ── 오른쪽 클릭 메뉴 `[소스] QPQdQ6Sy.js:function oa(e,t,n){ · function ua(e){`

        private const string ItemInfo = "info";
        private const string ItemBuild = "build";
        private const string ItemDelete = "delete";
        private const string ItemBoat = "boat";
        private const string ItemDonateTroops = "donateTroops";
        private const string ItemBreakAlly = "breakAlly";
        private const string ItemAlly = "ally";
        private const string ItemDonateGold = "donateGold";
        private const string ItemAttack = "attack";

        /// <summary>건설 하위 메뉴 `[소스] _=(0,Y.useCallback)(()=>[{type:W.City,id:`build_city`…`.</summary>
        private static readonly (EFrontWarsUnit Type, string Id, string Title, string Desc)[] BuildItems =
        {
            (EFrontWarsUnit.City, "build_city", "city", "increases_max_population"),
            (EFrontWarsUnit.Factory, "build_factory", "factory", "produces_units"),
            (EFrontWarsUnit.Port, "build_port", "port", "allows_naval_units"),
            (EFrontWarsUnit.DefensePost, "build_defense", "defense_post", "increases_defenses"),
            (EFrontWarsUnit.MissileSilo, "build_missile_silo", "missile_silo", "launches_missiles"),
            (EFrontWarsUnit.SAMLauncher, "build_sam", "sam_launcher", "anti_aircraft_defense")
        };

        /// <summary>공격 하위 메뉴 `[소스] v=(0,Y.useCallback)(()=>{let e=[{type:W.AtomBomb,id:`attack_atombomb`…`.</summary>
        private static readonly (EFrontWarsUnit Type, string Id, string Title, string Desc)[] AttackItems =
        {
            (EFrontWarsUnit.AtomBomb, "attack_atombomb", "atom_bomb", "nuclear_weapon"),
            (EFrontWarsUnit.HydrogenBomb, "attack_hydrogenbomb", "hydrogen_bomb", "thermonuclear_weapon"),
            (EFrontWarsUnit.MIRV, "attack_mirv", "mirv", "multiple_independently_targetable_reentry_vehicle"),
            (EFrontWarsUnit.Warship, "attack_warship", "warship", "battleship_unit")
        };

        /// <summary>열린 하위 메뉴 줄 — 비면 첫 겹.</summary>
        private readonly List<string> _radialPath = new List<string>();
        private readonly List<FrontWarsRadialItem> _radialItems = new List<FrontWarsRadialItem>();

        private void HandleContextMenu(Vector2 screen, int tile)
        {
            FrontWarsGame game = _root.Game.Game;
            FrontWarsPlayer me = _root.Game.LocalPlayer;

            if (game == null || me == null || tile < 0 || me.IsAlive == false)
                return;

            _radialTile = tile;
            _radialTarget = game.Owner(tile);
            _radialPath.Clear();

            FrontWarsRadialMenuWindow radial = GetWindow(FrontWarsRadialMenuWindow.Key);
            radial.Place(screen);
            RefreshRadial(radial);
            radial.OnItem -= HandleRadialItem;
            radial.OnItem += HandleRadialItem;
            radial.OnCenter -= HandleRadialCenter;
            radial.OnCenter += HandleRadialCenter;
            radial.OnDismiss -= HandleCancel;
            radial.OnDismiss += HandleCancel;
            radial.Open();
        }

        /// <summary>지금 겹의 조각을 다시 만든다 — 골드 · 재사용 대기가 바뀌면 조각 상태도 바뀐다.</summary>
        private void RefreshRadial(FrontWarsRadialMenuWindow radial)
        {
            FrontWarsGame game = _root.Game.Game;
            FrontWarsPlayer me = _root.Game.LocalPlayer;

            if (game == null || me == null)
                return;

            _radialItems.Clear();
            bool myTile = _radialTarget == me;

            if (_radialPath.Count == 0)
                BuildRootItems(game, me, myTile);
            else if (_radialPath[_radialPath.Count - 1] == ItemBuild)
                BuildUnitItems(game, me, BuildItems, FrontWarsRadialMenuWindow.BuildingColor, false);
            else
                BuildUnitItems(game, me, AttackItems, FrontWarsRadialMenuWindow.RedColor, true);

            // 가운데 아이콘 — 하위 메뉴면 뒤로, 내 땅이면 방패, 아니면 칼 `[소스] ee=l.length>0?$.back:v.isMyTile?$.shield:$.sword`
            Sprite center = _radialPath.Count > 0
                ? FrontWarsIconFactory.GetUi(FrontWarsIconFactory.EUiIcon.Back)
                : myTile ? FrontWarsIconFactory.Get(FrontWarsIconFactory.EShape.Shield) : FrontWarsIconFactory.GetEmoji("⚔️");

            radial.Show(_radialItems, _radialPath.Count == 0 ? 0 : 1, center, CircularText("unavailable"));
        }

        private static string CircularText(string key) => Texts.Ko($"circular_menu.{key}");

        private void BuildRootItems(FrontWarsGame game, FrontWarsPlayer me, bool myTile)
        {
            int tile = _radialTile;
            FrontWarsPlayer owner = _radialTarget;

            _radialItems.Add(new FrontWarsRadialItem
            {
                Id = ItemInfo, Color = FrontWarsRadialMenuWindow.InfoColor, Glyph = "i", Disabled = owner == null,
                Title = CircularText("player_info"), Description = CircularText("view_player_information")
            });

            if (myTile)
            {
                _radialItems.Add(new FrontWarsRadialItem
                {
                    Id = ItemBuild, Color = FrontWarsRadialMenuWindow.BuildColor, Icon = FrontWarsIconFactory.Get(FrontWarsIconFactory.EShape.Hammer),
                    Disabled = game.InSpawnPhase, HasSubMenu = true,
                    Title = CircularText("build"), Description = CircularText("build_structures")
                });

                // 삭제 — 땅이고 · 삭제 대기가 끝났고 · 5칸 안에 내 유닛이 있어야 누른다
                bool near = NearestOwnUnit(game, me, tile) != null;
                int wait = me.LastDeleteUnitTick < 0 ? 0 : Math.Max(0, game.Settings.Config.DeleteUnitCooldownTicks - (game.Ticks - me.LastDeleteUnitTick));
                FrontWarsRadialItem delete = new FrontWarsRadialItem
                {
                    Id = ItemDelete, Color = FrontWarsRadialMenuWindow.RedColor, Icon = FrontWarsIconFactory.GetUi(FrontWarsIconFactory.EUiIcon.Close),
                    Disabled = game.Map.IsLand(tile) == false || me.CanDeleteUnit() == false || near == false,
                    Title = CircularText("delete_unit"), Description = CircularText("delete_selected_unit")
                };
                ApplyCooldown(delete, near ? wait : 0, game.Settings.Config.DeleteUnitCooldownTicks);
                _radialItems.Add(delete);
                return;
            }

            bool allied = owner != null && owner.IsAlliedWith(me);
            bool sameTeam = owner != null && owner.IsOnSameTeam(me);
            bool disconnected = owner != null && owner.IsDisconnected;
            bool hostile = sameTeam == false || disconnected;
            List<FrontWarsBuildable> buildable = game.InSpawnPhase ? null : me.BuildableUnits(tile);

            if (hostile)
            {
                bool boatsOff = game.Rules.IsUnitDisabled(EFrontWarsUnit.TransportShip);
                int inUse = me.Units(EFrontWarsUnit.TransportShip).Count;
                int max = game.Settings.Config.BoatMaxNumber;
                _radialItems.Add(new FrontWarsRadialItem
                {
                    Id = ItemBoat, Color = FrontWarsRadialMenuWindow.BoatColor, Icon = FrontWarsIconFactory.Get(FrontWarsIconFactory.EShape.TransportShip),
                    Disabled = boatsOff || buildable == null || CanBuildHere(buildable, EFrontWarsUnit.TransportShip) == false,
                    Counter = boatsOff ? null : $"{Math.Max(0, max - inUse)}/{max}",
                    CounterLine = boatsOff ? null : "⛵ " + CircularText("boats_in_transit").Replace("{0}", inUse.ToString()).Replace("{1}", max.ToString()),
                    Title = CircularText("transport_ship"), Description = CircularText(boatsOff ? "boats_disabled" : "deploy_transport_ship")
                });
            }

            if (allied || sameTeam)
            {
                var donate = new FrontWarsRadialItem
                {
                    Id = ItemDonateTroops, Color = FrontWarsRadialMenuWindow.AllyColor, Icon = FrontWarsIconFactory.GetEmoji("🪖"),
                    Disabled = me.CanDonateTroops(owner) == false,
                    Title = CircularText("donate_troops"), Description = CircularText("send_troops_to_ally")
                };
                ApplyCooldown(donate, me.DonationCooldownTicks(owner), game.Settings.Config.DonateCooldownTicks);
                _radialItems.Add(donate);
            }

            if (owner != null && owner.IsAlive)
            {
                if (allied)
                {
                    _radialItems.Add(new FrontWarsRadialItem
                    {
                        Id = ItemBreakAlly, Color = FrontWarsRadialMenuWindow.RedColor, Icon = FrontWarsIconFactory.GetEmoji("💔"),
                        Disabled = me.IsAlliedWith(owner) == false,
                        Title = CircularText("break_alliance"), Description = CircularText("break_alliance_desc")
                    });
                }
                else if (sameTeam == false)
                {
                    bool alliancesOff = game.Settings.DisableAlliances;
                    var ally = new FrontWarsRadialItem
                    {
                        Id = ItemAlly, Color = FrontWarsRadialMenuWindow.AllyColor, Icon = FrontWarsIconFactory.GetEmoji("🤝"),
                        Disabled = me.CanSendAllianceRequest(owner) == false,
                        Title = CircularText("alliance"), Description = CircularText(alliancesOff ? "alliances_disabled" : "request_alliance")
                    };
                    ApplyCooldown(ally, me.AllianceRequestCooldownTicks(owner), game.Settings.Config.AllianceRequestCooldownTicks);
                    _radialItems.Add(ally);
                }
            }

            if (allied || sameTeam)
            {
                var gold = new FrontWarsRadialItem
                {
                    Id = ItemDonateGold, Color = FrontWarsRadialMenuWindow.BuildColor, Icon = FrontWarsIconFactory.GetEmoji("💰"),
                    Disabled = me.CanDonateGold(owner) == false,
                    Title = CircularText("donate_gold"), Description = CircularText("send_gold_to_ally")
                };
                ApplyCooldown(gold, me.DonationCooldownTicks(owner), game.Settings.Config.DonateCooldownTicks);
                _radialItems.Add(gold);
            }

            if (hostile)
            {
                // 공격 조각은 하위 메뉴(핵 · 군함) — 동맹이면 (끊긴 상대가 아니면) 못 누른다 `[소스] disabled:()=>n?!1:t?!r:!1`
                _radialItems.Add(new FrontWarsRadialItem
                {
                    Id = ItemAttack, Color = FrontWarsRadialMenuWindow.RedColor, Icon = FrontWarsIconFactory.GetEmoji("⚔️"),
                    Disabled = sameTeam == false && allied && disconnected == false, HasSubMenu = true,
                    Title = CircularText("attack"), Description = CircularText("attack_with_units")
                });
            }
        }

        /// <summary>건설 · 공격 하위 메뉴 조각 `[소스] disabled:t=>{…h(e.type)>=t.myPlayer.gold()?!0:…}`.</summary>
        private void BuildUnitItems(FrontWarsGame game, FrontWarsPlayer me, (EFrontWarsUnit Type, string Id, string Title, string Desc)[] defs, Color color, bool attack)
        {
            List<FrontWarsBuildable> buildable = game.InSpawnPhase ? null : me.BuildableUnits(_radialTile);
            int siloWait = MissileCooldown(game, me);

            foreach (var def in defs)
            {
                FrontWarsBuildable? entry = Find(buildable, def.Type);
                long cost = game.Rules.UnitCost(def.Type, me);
                bool upgrade = entry.HasValue && entry.Value.UpgradeUnitId >= 0;
                bool disabled = game.Rules.IsUnitDisabled(def.Type) || entry.HasValue == false || cost >= me.Gold;
                bool nuke = def.Type == EFrontWarsUnit.AtomBomb || def.Type == EFrontWarsUnit.HydrogenBomb || def.Type == EFrontWarsUnit.MIRV;

                if (disabled == false)
                {
                    if (nuke)
                        disabled = me.Units(EFrontWarsUnit.MissileSilo).Count == 0 || (attack && siloWait > 0);
                    else if (def.Type == EFrontWarsUnit.Warship)
                        disabled = me.Units(EFrontWarsUnit.Port).Count == 0;
                    else if (attack == false)
                        disabled = upgrade == false && entry.Value.BuildTile == FrontWarsPlayer.NoTile;
                }

                var item = new FrontWarsRadialItem
                {
                    Id = def.Id, Color = color, Icon = FrontWarsIconFactory.Get(FrontWarsIconFactory.ShapeOf(def.Type)), Disabled = disabled,
                    Title = (attack == false && upgrade ? "⬆ " : string.Empty) + CircularText(def.Title),
                    Description = CircularText(def.Desc),
                    Cost = $"{FrontWarsNumberFormat.Gold(cost)} Gold"
                };

                if (attack && nuke)
                    ApplyCooldown(item, siloWait, game.Settings.Config.SiloCooldownTicks);

                _radialItems.Add(item);
            }
        }

        private static FrontWarsBuildable? Find(List<FrontWarsBuildable> list, EFrontWarsUnit type)
        {
            if (list == null)
                return null;

            foreach (FrontWarsBuildable b in list)
            {
                if (b.Type == type)
                    return b;
            }

            return null;
        }

        private static bool CanBuildHere(List<FrontWarsBuildable> list, EFrontWarsUnit type)
        {
            FrontWarsBuildable? entry = Find(list, type);
            return entry.HasValue && entry.Value.BuildTile != FrontWarsPlayer.NoTile;
        }

        /// <summary>내 격납고 중 가장 빨리 쏠 수 있는 것까지 남은 틱 (하나라도 준비됐으면 0) `[소스] ia=e=>{…missileCooldownTicks()…}`.</summary>
        private static int MissileCooldown(FrontWarsGame game, FrontWarsPlayer me)
        {
            List<FrontWarsUnit> silos = me.Units(EFrontWarsUnit.MissileSilo);

            if (silos.Count == 0)
                return 0;

            int best = int.MaxValue;

            foreach (FrontWarsUnit silo in silos)
            {
                int wait = silo.MissileTimerQueue.Count < silo.Level || silo.MissileTimerQueue.Count == 0
                    ? 0
                    : Math.Max(0, game.Settings.Config.SiloCooldownTicks - (game.Ticks - silo.MissileTimerQueue[0]));

                if (wait <= 0)
                    return 0;

                best = Math.Min(best, wait);
            }

            return best;
        }

        /// <summary>재사용 대기 — 조각 어둡게 덮기 · 툴팁 「⏳ 재사용 대기시간: N초」.</summary>
        private static void ApplyCooldown(FrontWarsRadialItem item, int remainingTicks, int totalTicks)
        {
            if (remainingTicks <= 0 || totalTicks <= 0)
                return;

            item.Cooldown = Mathf.Clamp01(remainingTicks / (float)totalTicks);
            int seconds = (int)Math.Ceiling(remainingTicks / 10.0);
            item.CooldownLine = "⏳ " + CircularText("cooldown_remaining").Replace("{0}", seconds.ToString());
        }

        private void HandleRadialItem(string id)
        {
            FrontWarsGameManager manager = _root.Game;
            FrontWarsPlayer me = manager.LocalPlayer;
            FrontWarsGame game = manager.Game;
            FrontWarsRadialMenuWindow radial = GetWindow(FrontWarsRadialMenuWindow.Key);

            if (me == null || game == null)
                return;

            // 하위 메뉴가 있는 조각은 한 겹 더 연다
            if (id == ItemBuild || id == ItemAttack)
            {
                _radialPath.Add(id);
                RefreshRadial(radial);
                return;
            }

            CloseWindow(FrontWarsRadialMenuWindow.Key);
            string targetId = _radialTarget?.Id;
            int tile = _radialTile;
            FrontWarsMapInput input = _world.Input;

            switch (id)
            {
                case ItemInfo:
                    // 그 플레이어를 정보 판에 붙잡는다 — 외교 버튼이 같이 뜬다 (Esc 로 푼다)
                    _pinnedPlayer = _radialTarget;
                    return;
                case ItemDelete:
                {
                    FrontWarsUnit unit = NearestOwnUnit(game, me, tile);
                    if (unit != null)
                        manager.Submit(new FrontWarsDeleteUnitIntent { UnitId = unit.Id });
                    return;
                }
                case ItemBoat:
                    manager.Submit(new FrontWarsBoatIntent
                    {
                        TargetId = targetId,
                        Destination = tile,
                        Troops = Math.Floor(me.Troops * input.AttackRatio),
                        Source = FrontWarsTransportShipRules.BestTransportShipSpawn(game, me, tile)
                    });
                    return;
                case ItemDonateTroops:
                    manager.Submit(new FrontWarsDonateTroopsIntent { RecipientId = targetId, Troops = Math.Floor(me.Troops * input.SendTroopsRatio) });
                    return;
                case ItemDonateGold:
                    manager.Submit(new FrontWarsDonateGoldIntent { RecipientId = targetId, Gold = Math.Floor(me.Gold * (double)input.SendGoldRatio) });
                    return;
                case ItemAlly:
                    manager.Submit(new FrontWarsAllianceRequestIntent { RecipientId = targetId });
                    return;
                case ItemBreakAlly:
                    manager.Submit(new FrontWarsBreakAllianceIntent { RecipientId = targetId });
                    return;
            }

            // 하위 메뉴 조각 — 올릴 수 있으면 업그레이드, 아니면 그 칸에 짓기 (핵 · 군함은 그 칸을 목표로 짓기) `[소스] n.canUpgrade===!1?u.emit(new je(e.type,t.tile)):u.emit(new Mt(n.canUpgrade,e.type))`
            foreach (var def in BuildItems)
            {
                if (def.Id != id)
                    continue;

                FrontWarsBuildable? entry = Find(me.BuildableUnits(tile), def.Type);

                if (entry.HasValue == false)
                    return;

                if (entry.Value.UpgradeUnitId >= 0)
                    manager.Submit(new FrontWarsUpgradeStructureIntent { UnitId = entry.Value.UpgradeUnitId });
                else
                    manager.Submit(new FrontWarsBuildUnitIntent { Unit = def.Type, Tile = tile });

                return;
            }

            foreach (var def in AttackItems)
            {
                if (def.Id == id)
                {
                    manager.Submit(new FrontWarsBuildUnitIntent { Unit = def.Type, Tile = tile });
                    return;
                }
            }
        }

        /// <summary>
        /// 가운데 원 — 하위 메뉴면 한 겹 뒤로, 내 땅이면 그 칸에 방어 초소, 남의 땅이면 그 자리 공격 `[소스] O=e=>{…l.length>0?(d(e=>e.slice(0,-1))…):(v.isMyTile&&v.tile?u.emit(new je(W.DefensePost,v.tile)):u.emit(new Yt(n,i)),b())}`.
        /// </summary>
        private void HandleRadialCenter()
        {
            FrontWarsRadialMenuWindow radial = GetWindow(FrontWarsRadialMenuWindow.Key);

            if (_radialPath.Count > 0)
            {
                _radialPath.RemoveAt(_radialPath.Count - 1);
                RefreshRadial(radial);
                return;
            }

            FrontWarsPlayer me = _root.Game.LocalPlayer;
            CloseWindow(FrontWarsRadialMenuWindow.Key);

            if (me == null || _radialTile < 0)
                return;

            if (_radialTarget == me)
                _root.Game.Submit(new FrontWarsBuildUnitIntent { Unit = EFrontWarsUnit.DefensePost, Tile = _radialTile });
            else
                _world.Input.HandleClick(_radialTile);
        }

        /// <summary>정보 판 버튼 동작 — 타겟 · 퀵 채팅 · 무역 중단/재개.</summary>
        private void HandleRadialAction(EFrontWarsRadialAction action)
        {
            FrontWarsGameManager manager = _root.Game;
            string targetId = _radialTarget?.Id;

            switch (action)
            {
                case EFrontWarsRadialAction.Target:
                    manager.Submit(new FrontWarsTargetPlayerIntent { TargetId = targetId });
                    break;
                case EFrontWarsRadialAction.QuickChat:
                    OpenQuickChat(targetId);
                    break;
                case EFrontWarsRadialAction.Embargo:
                    manager.Submit(new FrontWarsEmbargoIntent { TargetId = targetId, Start = true });
                    break;
                case EFrontWarsRadialAction.StopEmbargo:
                    manager.Submit(new FrontWarsEmbargoIntent { TargetId = targetId, Start = false });
                    break;
            }
        }

        // ── 튜토리얼

        /// <summary>브리핑 · 비추기 시간 `[소스] _r=12e3 · yr timerMs:15e3`.</summary>
        private const float BriefingSeconds = 12f;
        private const float SpotlightSeconds = 15f;

        /// <summary>가리개 짙기 — 브리핑 0.55 · 슬라이더 비추기 0.5 · 건설 안내 0.6 `[소스] vr · yr · nr 기본값`.</summary>
        private const float BriefingMask = 0.55f;
        private const float SpotlightMask = 0.5f;
        private const float GuideMask = 0.6f;

        private const string BriefKeyPrefix = "brief-";
        private const string SpotKeyPrefix = "spot-";

        private readonly FrontWarsTutorialView _tutorialView = new FrontWarsTutorialView();
        private readonly Vector3[] _tutorialCorners = new Vector3[4];
        private readonly Vector3[] _tutorialBoundary = new Vector3[4];
        private int _tutorialNameTick = -1;
        private FrontWarsNameLocation _tutorialName;
        private int _tutorialFocusedPlayer = -1;
        private string _sliderSpotObjective;
        private float _sliderSpotRatio;
        private bool _sliderSpotHidden;

        /// <summary>튜토리얼 화면을 엔진 상태로 채운다 `[소스] QPQdQ6Sy.js:function Er(){`.</summary>
        private void UpdateTutorial(FrontWarsGame game, FrontWarsPlayer me, FrontWarsHudWindow hud)
        {
            if (_tutorial == null || _tutorial.Stage == EFrontWarsTutorialStage.Finished)
            {
                hud.SetTutorialGate(true, true, true);
                CloseWindow(FrontWarsTutorialWindow.Key);
                return;
            }

            FrontWarsTutorialWindow window = GetWindow(FrontWarsTutorialWindow.Key);

            if (window.IsOpen() == false)
            {
                window.Show(Texts.Ko);
                window.OnSpotlightDone -= HandleTutorialSpotDone;
                window.OnSpotlightDone += HandleTutorialSpotDone;
                window.OnKeepPlaying -= HandleTutorialContinue;
                window.OnKeepPlaying += HandleTutorialContinue;
                window.OnRealMatch -= HandleTutorialRealMatch;
                window.OnRealMatch += HandleTutorialRealMatch;
                window.OnRestart -= HandleTutorialRestart;
                window.OnRestart += HandleTutorialRestart;
                window.Open();
            }

            // 가리개 · 카드는 원형 메뉴보다 위다 `[소스] z-index 10001 · 10003`
            if (window.transform.GetSiblingIndex() != window.transform.parent.childCount - 1)
                WindowManagement.Instance.BringToFront(window);

            UpdateTutorialGate(hud);

            FrontWarsTutorialView view = _tutorialView;
            view.Stage = _tutorial.Stage;
            view.CelebrationSeq = _tutorial.CelebrateSeq;
            view.VictoryTiles = me != null ? me.NumTilesOwned : 0;
            view.TipKey = null;
            view.TipText = null;
            view.SpotKey = null;
            view.SpotTarget = null;
            view.SpotBoundary = null;
            view.SpotGotIt = null;
            view.SpawnMarker = false;
            view.Reticle = false;
            view.Alert = false;

            FrontWarsTutorialObjective objective = _tutorial.Current;
            bool playing = objective != null && (_tutorial.Stage == EFrontWarsTutorialStage.Objectives || _tutorial.Stage == EFrontWarsTutorialStage.Extra);

            if (playing)
                FillTutorialPlaying(view, window, game, me, hud, objective);

            window.Render(view);
        }

        private void FillTutorialPlaying(FrontWarsTutorialView view, FrontWarsTutorialWindow window, FrontWarsGame game, FrontWarsPlayer me,
                                         FrontWarsHudWindow hud, FrontWarsTutorialObjective objective)
        {
            (int Value, int Max)? progress = objective.Progress?.Invoke();
            view.ObjectiveCount = _tutorial.Objectives.Count;
            view.Index = _tutorial.Index;
            view.Icon = objective.Icon;
            view.ChipText = Texts.Ko($"tutorial2.obj.{objective.Id}.chip").Replace("{0}", progress.HasValue ? progress.Value.Max.ToString() : string.Empty);
            view.Progress = progress.HasValue ? $"{progress.Value.Value}/{progress.Value.Max}" : null;
            view.Transitioning = _tutorial.Celebrating;

            // 지도 표시 대상이 바뀌면 카메라가 그리로 간다 `[소스] Er useEffect markerPlayer → Gt(x,y)`
            FrontWarsPlayer marker = _tutorial.MarkerPlayer;
            int markerId = marker != null ? marker.SmallId : -1;
            if (markerId != _tutorialFocusedPlayer)
            {
                _tutorialFocusedPlayer = markerId;
                if (marker != null)
                {
                    FrontWarsNameLocation at = FrontWarsNamePlacement.Compute(game, marker);
                    if (at.X != 0 || at.Y != 0)
                        _world.CameraController.FocusOn(at.X, at.Y, false);
                }
            }

            // 완료 중엔 칩만 · 브리핑 중엔 칩 + 브리핑만
            if (view.Transitioning)
                return;

            if (_tutorial.BriefingPending && objective.BriefingTarget != null)
            {
                if (hud.TryGetTutorialTarget(objective.BriefingTarget, _tutorialCorners))
                {
                    view.SpotKey = BriefKeyPrefix + objective.Id;
                    view.SpotTarget = window.CanvasRectOf(_tutorialCorners);
                    view.SpotText = Texts.Ko($"tutorial2.obj.{objective.Id}.brief");
                    view.SpotMask = BriefingMask;
                    view.SpotGotIt = Texts.Ko("tutorial2.got_it");
                    view.SpotSeconds = BriefingSeconds;
                }

                return;
            }

            if (_tutorial.TipObjectiveId != null && objective.BuildGuide == null && objective.SpotlightTarget == null)
            {
                view.TipKey = _tutorial.TipObjectiveId;
                view.TipText = Texts.Ko($"tutorial2.obj.{_tutorial.TipObjectiveId}.tip");
                view.TipTimed = true;
            }

            if (objective.BuildGuide.HasValue)
                FillBuildGuide(view, window, game, me, objective);

            if (objective.SpotlightTarget != null)
                FillSliderSpotlight(view, window, hud, objective);

            if (objective.Marker == EFrontWarsTutorialMarker.Spawn)
            {
                view.SpawnMarker = true;
                view.SpawnLabel = Texts.Ko("tutorial2.obj.spawn.marker");
            }

            if (objective.Marker == EFrontWarsTutorialMarker.Expand && _tutorial.ExpandTargetTile >= 0)
            {
                view.Reticle = true;
                view.ReticleFriendly = true;
                view.ReticleScreen = _world.CameraController.TileToScreen(game.Map.X(_tutorial.ExpandTargetTile), game.Map.Y(_tutorial.ExpandTargetTile));
                view.ReticleLabel = Texts.Ko("tutorial2.obj.expand.marker");
            }

            if (marker != null)
            {
                FrontWarsNameLocation at = FrontWarsNamePlacement.Compute(game, marker);
                view.Reticle = at.X != 0 || at.Y != 0;
                view.ReticleFriendly = false;
                view.ReticleScreen = _world.CameraController.TileToScreen(at.X, at.Y);
                view.ReticleLabel = Texts.Ko(objective.Id == "defend" ? "tutorial2.defend_marker"
                                           : objective.Marker == EFrontWarsTutorialMarker.Attacker ? "tutorial2.attacker_marker"
                                           : "tutorial2.outpost_marker");
            }

            view.Alert = _tutorial.UnderAttack;
        }

        /// <summary>
        /// 건설 안내 세 단계 `[소스] gr` — 하위 메뉴에 그 건물 조각이 보이면 「고르세요」, 첫 겹에 건설 조각이 보이면 「건설 메뉴를 여세요」,
        /// 아니면 「내 땅을 우클릭」 카드 + 내 이름 자리에 초록 조준경.
        /// </summary>
        private void FillBuildGuide(FrontWarsTutorialView view, FrontWarsTutorialWindow window, FrontWarsGame game, FrontWarsPlayer me, FrontWarsTutorialObjective objective)
        {
            string sliceId = BuildSliceId(objective.BuildGuide.Value);
            var radial = WindowManagement.Instance.FindCreated(FrontWarsRadialMenuWindow.Key) as FrontWarsRadialMenuWindow;
            bool radialOpen = radial != null && radial.IsOpen();

            if (radialOpen && radial.TryGetSliceCorners(sliceId, _tutorialCorners))
            {
                FillGuideSpot(view, window, radial, $"pick-{objective.Id}", Texts.Ko($"tutorial2.obj.{objective.Id}.step_pick"));
                return;
            }

            if (radialOpen && radial.TryGetSliceCorners(ItemBuild, _tutorialCorners))
            {
                FillGuideSpot(view, window, radial, $"build-{objective.Id}", Texts.Ko($"tutorial2.obj.{objective.Id}.step_build"));
                return;
            }

            view.TipKey = $"open-{objective.Id}";
            view.TipText = Texts.Ko($"tutorial2.obj.{objective.Id}.step_open");
            view.TipTimed = false;

            if (me == null)
                return;

            FrontWarsNameLocation at = MyNameLocation(game, me);
            view.Reticle = at.X != 0 || at.Y != 0;
            view.ReticleFriendly = true;
            view.ReticleScreen = _world.CameraController.TileToScreen(at.X, at.Y);
            view.ReticleLabel = Texts.Ko($"tutorial2.obj.{objective.Id}.marker");
        }

        private void FillGuideSpot(FrontWarsTutorialView view, FrontWarsTutorialWindow window, FrontWarsRadialMenuWindow radial, string key, string text)
        {
            radial.GetMenuCorners(_tutorialBoundary);
            view.SpotKey = key;
            view.SpotTarget = window.CanvasRectOf(_tutorialCorners);
            view.SpotBoundary = window.CanvasRectOf(_tutorialBoundary);
            view.SpotText = text;
            view.SpotMask = GuideMask;
        }

        /// <summary>
        /// 공격 비율 비추기 `[소스] yr` — 비율을 바꾸거나 공격이 한 번이라도 들어오면 이 목표 동안 다시 안 뜬다.
        /// </summary>
        private void FillSliderSpotlight(FrontWarsTutorialView view, FrontWarsTutorialWindow window, FrontWarsHudWindow hud, FrontWarsTutorialObjective objective)
        {
            if (_sliderSpotObjective != objective.Id)
            {
                _sliderSpotObjective = objective.Id;
                _sliderSpotRatio = _world.Input.AttackRatio;
                _sliderSpotHidden = false;
            }

            if (_world.Input.AttackRatio != _sliderSpotRatio || _tutorial.UnderAttack)
                _sliderSpotHidden = true;

            if (_sliderSpotHidden || hud.TryGetTutorialTarget(objective.SpotlightTarget, _tutorialCorners) == false)
                return;

            view.SpotKey = SpotKeyPrefix + objective.Id;
            view.SpotTarget = window.CanvasRectOf(_tutorialCorners);
            view.SpotText = Texts.Ko($"tutorial2.obj.{objective.Id}.tip");
            view.SpotMask = SpotlightMask;
            view.SpotGotIt = Texts.Ko("tutorial2.got_it");
            view.SpotSeconds = SpotlightSeconds;
        }

        /// <summary>내 이름 자리 — 무거운 계산이라 틱마다 한 번만.</summary>
        private FrontWarsNameLocation MyNameLocation(FrontWarsGame game, FrontWarsPlayer me)
        {
            if (_tutorialNameTick != game.Ticks)
            {
                _tutorialNameTick = game.Ticks;
                _tutorialName = FrontWarsNamePlacement.Compute(game, me);
            }

            return _tutorialName;
        }

        private static string BuildSliceId(EFrontWarsUnit type)
        {
            foreach (var item in BuildItems)
                if (item.Type == type)
                    return item.Id;

            return null;
        }

        /// <summary>
        /// 튜토리얼 동안 HUD 를 단계별로 드러낸다 `[소스] Ar(e,t)` — 순위표는 「톱 3」, 사건 목록은 「사수」, 건설 막대는 「도시」 목표부터. 추가 목표면 다 보인다.
        /// </summary>
        private void UpdateTutorialGate(FrontWarsHudWindow hud)
        {
            if (_tutorial.Stage == EFrontWarsTutorialStage.Extra)
            {
                hud.SetTutorialGate(true, true, true);
                return;
            }

            int index = _tutorial.Index;
            hud.SetTutorialGate(index >= _tutorial.MainIndexOf("top3"),
                                index >= _tutorial.MainIndexOf("defend"),
                                index >= _tutorial.MainIndexOf("city"));
        }

        private void HandleTutorialSpotDone(string key)
        {
            if (_tutorial == null)
                return;

            if (key.StartsWith(BriefKeyPrefix, StringComparison.Ordinal))
                _tutorial.DismissBriefing();
            else if (key.StartsWith(SpotKeyPrefix, StringComparison.Ordinal))
                _sliderSpotHidden = true;
        }

        private void HandleTutorialContinue() => _tutorial?.ContinueConquest();

        /// <summary>「실전 매치 플레이」 — 판을 나가 메뉴로 `[소스] markLeftToMenu(), v.leaveGame()`.</summary>
        private void HandleTutorialRealMatch() => HandleExit();

        private void HandleTutorialRestart()
        {
            HandleExit();
            HandleMenuTutorial();
        }

        // ── 판 끝

        private void HandleWinner(FrontWarsPlayer winner)
        {
            if (_gameOverShown)
                return;

            _gameOverShown = true;
            FrontWarsPlayer me = _root.Game.LocalPlayer;
            bool victory = winner == me;

            FrontWarsGameOverWindow window = GetWindow(FrontWarsGameOverWindow.Key);
            // 제목 줄이 「○○가 승리했습니다!」 [실측], 아래 한 줄이 승리 · 패배
            window.Show(Texts.Format("end.winner", winner.Name), Texts.Ko(victory ? "end.victory" : "end.defeat"), Texts.Ko("end.exit"), Texts.Ko("end.keep_watching"));
            window.OnExit -= HandleGameOverExit;
            window.OnExit += HandleGameOverExit;
            window.OnKeepWatching -= HandleKeepWatching;
            window.OnKeepWatching += HandleKeepWatching;
            window.Open();
        }

        /// <summary>팀전 — 승자는 팀 이름이다 `[소스] Cq3gr8XO.js:checkWinnerTeam(){`. 내 팀이면 승리.</summary>
        private void HandleTeamWinner(string team)
        {
            if (_gameOverShown)
                return;

            _gameOverShown = true;
            FrontWarsPlayer me = _root.Game.LocalPlayer;
            bool victory = me != null && me.Team == team;

            FrontWarsGameOverWindow window = GetWindow(FrontWarsGameOverWindow.Key);
            window.Show(Texts.Format("end.team_winner", TeamName(team)), Texts.Ko(victory ? "end.victory" : "end.defeat"), Texts.Ko("end.exit"), Texts.Ko("end.keep_watching"));
            window.OnExit -= HandleGameOverExit;
            window.OnExit += HandleGameOverExit;
            window.OnKeepWatching -= HandleKeepWatching;
            window.OnKeepWatching += HandleKeepWatching;
            window.Open();
        }

        /// <summary>팀 이름 — 색 이름은 문구 표, 「Team 8」 처럼 표에 없는 것은 그대로.</summary>
        private static string TeamName(string team)
        {
            string key = $"team.{team}";
            string ko = Texts.Ko(key);
            return ko == key ? team : ko;
        }

        private void HandleGameOverExit()
        {
            CloseWindow(FrontWarsGameOverWindow.Key);
            HandleExit();
        }

        /// <summary>「관전」 — 창만 닫고 판을 계속 보여준다. 원본에서 눌러 본 적이 없어 그 뒤 화면은 만들지 않는다 (사람 확정 2026-09-21 · 의도된 차이 16).</summary>
        private void HandleKeepWatching()
        {
            CloseWindow(FrontWarsGameOverWindow.Key);
        }
    }
}
