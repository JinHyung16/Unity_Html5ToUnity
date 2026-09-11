using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 창을 조종한다. <b>게임 로직은 매니저가 들고</b>, 창을 열고 닫고 값을 넣는 일만 여기가 한다.
    ///
    /// <para>
    /// ★ <b>화면 전이는 <c>GameFlow</c> 하나를 지난다</b> — 흩어지면 플로우 diff 를 못 돌린다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>「게임을 멈춘다」는 여기가 아니라 매니저의 몫이다</b> (원본 메인 루프의 정지 게이트).
    /// 창이 «떠 있다는 사실»과 «시간이 멈췄다는 사실»을 각각 다른 곳에 두면 한쪽이 반드시 어긋난다 —
    /// 그래서 이 클래스는 <c>Paused</c> 를 <b>카드가 확정될 때만</b> 되돌린다.
    /// </para>
    /// </summary>
    public sealed class UndeadManagement : BaseManagement
    {
        private UndeadGameRoot _root;

        /// <summary>이번 레벨업에 내놓은 카드. <b>무작위 뽑기</b>는 원본 그대로 «관측된 사실»이다.</summary>
        private readonly List<UndeadUpgradeData> _offered = new List<UndeadUpgradeData>(3);

        /// <summary>뽑기용 임시 버퍼 — <b>매 레벨업마다 새로 만들지 않는다</b>(정지 중 GC 를 만들 이유가 없다).</summary>
        private readonly List<UndeadUpgradeData> _pool = new List<UndeadUpgradeData>(8);

        private readonly List<double> _weights = new List<double>(8);

        public void Bind(UndeadGameRoot root)
        {
            _root = root;
        }

        /// <summary>축별 업그레이드 아이콘 — <b>미리 받아 둔 것</b>을 카드 창에 그대로 넘긴다.</summary>
        public void BindUpgradeIcons(IReadOnlyDictionary<string, UnityEngine.Sprite> icons)
        {
            _upgradeIcons = icons;
        }

        private IReadOnlyDictionary<string, UnityEngine.Sprite> _upgradeIcons;

        protected override void AddWindows()
        {
            RegisterWindow(UndeadReadyWindow.Key);
            RegisterWindow(UndeadBattleHudWindow.Key);
            RegisterWindow(UndeadLevelUpWindow.Key);
            RegisterWindow(UndeadReviveWindow.Key);
            RegisterWindow(UndeadWeaponUnlockWindow.Key);
            RegisterWindow(UndeadTaskCompleteWindow.Key);
            RegisterWindow(UndeadLobbyHudWindow.Key);
        }

        protected override void OnInitialize()
        {
            if (_root == null)
            {
                Log.Error("UndeadManagement 에 루트가 안 물렸다 — 배선을 본다");
                return;
            }

            _root.GameFlow.OnScreenChanged += HandleScreenChanged;
            _root.Game.OnLevelUp += HandleLevelUp;
            _root.Game.OnHeroDied += HandleHeroDied;
            _root.Game.OnFinalDeath += HandleDecline;
            _root.Game.OnWeaponUnlocked += HandleWeaponUnlocked;
            _root.Game.OnRunCleared += HandleRunCleared;
            _root.Game.OnTaskCompleted += HandleTaskCompleted;

            if (_root.Lobby != null)
            {
                _root.Lobby.OnEnterBiome += HandleEnterBiome;
                _root.Lobby.OnRewardClaimed += HandleRewardClaimed;
                _root.Lobby.IsModalOpen = IsAnyModalOpen;
            }
        }

        /// <summary>⚠ 구독은 <b>짝으로</b> 끊는다.</summary>
        protected override void OnDispose()
        {
            if (_root != null)
            {
                _root.GameFlow.OnScreenChanged -= HandleScreenChanged;

                if (_root.Game != null)
                {
                    _root.Game.OnLevelUp -= HandleLevelUp;
                    _root.Game.OnHeroDied -= HandleHeroDied;
                    _root.Game.OnFinalDeath -= HandleDecline;
                    _root.Game.OnWeaponUnlocked -= HandleWeaponUnlocked;
                    _root.Game.OnRunCleared -= HandleRunCleared;
                    _root.Game.OnTaskCompleted -= HandleTaskCompleted;

                if (_root.Lobby != null)
                {
                    _root.Lobby.OnEnterBiome -= HandleEnterBiome;
                    _root.Lobby.OnRewardClaimed -= HandleRewardClaimed;
                }
                }
            }
        }

        private void LateUpdate()
        {
            if (IsInitialized == false || _root == null || _root.Game == null)
                return;

            StepLobbyHud();

            UndeadSimulation sim = _root.Game.Simulation;

            if (sim == null)
                return;

            UndeadBattleHudWindow hud = GetWindow(UndeadBattleHudWindow.Key);

            if (hud == null || hud.IsOpen() == false)
                return;

            hud.SetGauge(sim.Gauge, sim.GaugeGoal);
            hud.SetElapsed(sim.ElapsedSeconds);
            hud.SetLevel(sim.Level);

            // ★ 스킬 슬롯 [소스 skillHud.update] — 가진 것만·쿨다운·키 뱃지가 매 프레임 갈린다
            hud.SetSkills(sim);
            StepTaskPrompt(UnityEngine.Time.deltaTime);

            // ★ 최고 기록은 «항상» 보인다 [소스 guiBestScore] — 대기 화면 전용이 아니다.
            //   시간은 «지금 시간과 최고 중 큰 값»이라 신기록을 세우는 동안 실시간으로 올라간다.
            UndeadTextDataContainer texts = GameRoot.Instance.UndeadTextDataContainer;
            hud.SetBestRecord(texts.Ko("bestLevel"), UndeadRecord.BestLevel,
                              texts.Ko("bestTime"), UndeadRecord.DisplayTimeSeconds(sim.ElapsedSeconds));

            // [소스 clock.update] 1초마다 넘겼으면 저장한다 — 판이 끝날 때가 아니다
            if (sim.ElapsedSeconds - _lastRecordSaveSeconds >= 1.0)
            {
                _lastRecordSaveSeconds = sim.ElapsedSeconds;
                UndeadRecord.ReportTime(sim.ElapsedSeconds);
            }
            // 포인터 [소스 bd.update] — 퀘스트가 켜져 있고 · 레벨업 카드가 안 떠 있고 · 대상이 화면 «밖»일 때만
            UndeadLevelUpWindow levelUp = GetWindow(UndeadLevelUpWindow.Key);
            bool pointerAllowed = sim.QuestActive && (levelUp == null || levelUp.IsOpen() == false);
            // 화면 중앙이 보는 월드 점은 피벗에서 offsetY 만큼 «위»다 [소스 — 히어로가 화면 중앙보다 20 아래 선다]
            UndeadVec2 fromCamera = sim.QuestTarget - new UndeadVec2(sim.CameraPivot.X, sim.CameraPivot.Y - sim.Config.CameraOffsetY);
            hud.SetQuest(pointerAllowed, (float)fromCamera.X, (float)fromCamera.Y, sim.QuestDistanceMeters, Time.deltaTime);
        }

        /// <summary>
        /// 로비 UI 를 매 프레임 갱신한다.
        /// <para>⚠ 로비 안에서만 돈다 — 전투 중에 돌면 «없는 NPC» 를 읽는다.</para>
        /// </summary>
        private void StepLobbyHud()
        {
            if (_root.Lobby == null || _root.Lobby.IsInside == false)
                return;

            UndeadLobbyHudWindow hud = GetWindow(UndeadLobbyHudWindow.Key);

            if (hud == null || hud.IsOpen() == false)
                return;

            hud.Apply(_root.Lobby.Simulation, _root.Game.Simulation, _lobbyTexts);
            hud.StepFade(Time.deltaTime);
        }

        private void HandleScreenChanged(EUndeadScreenType previous, EUndeadScreenType next)
        {
            switch (next)
            {
                case EUndeadScreenType.Lobby:
                    OpenLobby();
                    break;

                case EUndeadScreenType.Ready:
                    CloseLobby();
                    OpenReady();
                    break;

                case EUndeadScreenType.Battle:
                    CloseLobby();
                    GetWindow(UndeadBattleHudWindow.Key).Open();
                    CloseWindow(UndeadReadyWindow.Key);
                    CloseWindow(UndeadLevelUpWindow.Key);
                    CloseWindow(UndeadReviveWindow.Key);
                    CloseWindow(UndeadWeaponUnlockWindow.Key);
                    CloseWindow(UndeadTaskCompleteWindow.Key);
                    break;

                case EUndeadScreenType.LevelUp:
                    OpenLevelUp();
                    break;

                case EUndeadScreenType.Revive:
                    OpenRevive();
                    break;

                case EUndeadScreenType.WeaponUnlock:
                    OpenWeaponUnlock();
                    break;

                case EUndeadScreenType.TaskComplete:
                    OpenTaskComplete();
                    break;
            }
        }

        /// <summary>최고 시간을 마지막으로 저장한 시각 — [소스] 1초 간격이다.</summary>
        private double _lastRecordSaveSeconds;

        private void HandleLevelUp(int level)
        {
            // [소스 incrementLevel] bestLevel = max(level, bestLevel)
            UndeadRecord.ReportLevel(level);
            _root.GameFlow.ChangeScreen(EUndeadScreenType.LevelUp);
        }

        /// <summary>
        /// 바이옴 진입 화면. ★ <b>원본은 「시작」을 눌러야 전투가 시작된다</b> [실측] —
        /// 그동안 타이머는 <c>00:00</c> 이고 적도 안 나온다. 그래서 <b>멈춰 둔다</b>.
        /// </summary>
        /// <summary>
        /// 로비를 연다 [소스 <c>lobby</c> 씬].
        ///
        /// <para>
        /// ⚠ <b>전투를 멈춘다</b> — 로비와 전투는 같은 씬에 있고, 안 멈추면 <b>보이지 않는 전투가 계속 돈다</b>.
        /// </para>
        /// </summary>
        private void OpenLobby()
        {
            _root.Game.Paused = true;

            CloseWindow(UndeadReadyWindow.Key);
            CloseWindow(UndeadBattleHudWindow.Key);
            CloseWindow(UndeadLevelUpWindow.Key);
            CloseWindow(UndeadReviveWindow.Key);
            CloseWindow(UndeadWeaponUnlockWindow.Key);
            CloseWindow(UndeadTaskCompleteWindow.Key);

            if (_root.Lobby == null)
            {
                Log.Error("로비 매니저가 없다 — 로비 화면이 빈 채로 뜬다");
                return;
            }

            _root.Lobby.Enter();

            UndeadLobbyHudWindow hud = GetWindow(UndeadLobbyHudWindow.Key);
            hud.BindCamera(_lobbyCamera);
            hud.OnRewardConfirmed -= HandleRewardConfirmed;
            hud.OnRewardConfirmed += HandleRewardConfirmed;
            hud.OnFadeCompleted -= HandleFadeCompleted;
            hud.OnFadeCompleted += HandleFadeCompleted;
            hud.Open();
        }

        /// <summary>로비 UI 가 월드 좌표를 화면으로 옮길 때 쓴다 — 배선이 꽂아 준다.</summary>
        private Camera _lobbyCamera;

        public void BindLobbyCamera(Camera camera)
        {
            _lobbyCamera = camera;
        }

        private void HandleRewardConfirmed()
        {
            GetWindow(UndeadLobbyHudWindow.Key)?.HideReward();
        }

        /// <summary>페이드가 다 찼다 — 그때 바이옴으로 넘어간다 [소스 <c>lobbyExitFade</c>].</summary>
        private void HandleFadeCompleted()
        {
            _root.Game.ResetRun();
            _root.GameFlow.ChangeScreen(EUndeadScreenType.Ready);
        }

        private void CloseLobby()
        {
            _root.Lobby?.Leave();
            CloseWindow(UndeadLobbyHudWindow.Key);
        }

        /// <summary>포털에 들어갔다 — <b>바이옴 진입 화면</b>으로 간다 [소스 <c>lobbyExitFade.start(biome)</c>].</summary>
        private void HandleEnterBiome(int biome)
        {
            // ★ 어느 바이옴으로 들어가는지는 «판을 세우기 전»에 정해 둔다 [소스 op(biome) → vu({biome})].
            _root.Game.SetBiome(biome);

            // ★ 곧바로 안 넘어간다 — <b>1초 페이드가 다 찬 뒤</b>다 [소스 lobbyExitFade].
            GetWindow(UndeadLobbyHudWindow.Key)?.BeginFade();
        }

        /// <summary>NPC 앞에서 링이 다 차 보상을 받았다 — 스킬 보상 팝업을 띄운다 [소스 <c>skillRewardPopup</c>].</summary>
        private void HandleRewardClaimed(int taskIndex, EUndeadSkill skill)
        {
            UndeadLobbyHudWindow hud = GetWindow(UndeadLobbyHudWindow.Key);

            if (hud == null || _lobbyTexts == null)
                return;

            hud.ShowReward(_lobbyTexts.SkillName(skill), _lobbyTexts.SkillIcon(skill));
        }

        /// <summary>로비 UI 가 쓰는 문구·아이콘 창구 — 배선이 꽂아 준다.</summary>
        private ILobbyTexts _lobbyTexts;

        public void BindLobbyTexts(ILobbyTexts texts)
        {
            _lobbyTexts = texts;
        }

        /// <summary>모달이 떠 있나 — 로비의 수령이 이 값을 본다 [소스 <c>isModalOpen</c>].</summary>
        private bool IsAnyModalOpen()
        {
            UndeadLevelUpWindow levelUp = GetWindow(UndeadLevelUpWindow.Key);
            UndeadTaskCompleteWindow task = GetWindow(UndeadTaskCompleteWindow.Key);
            UndeadWeaponUnlockWindow weapon = GetWindow(UndeadWeaponUnlockWindow.Key);

            UndeadLobbyHudWindow lobbyHud = GetWindow(UndeadLobbyHudWindow.Key);

            return (levelUp != null && levelUp.IsOpen())
                   || (task != null && task.IsOpen())
                   || (weapon != null && weapon.IsOpen())
                   || (lobbyHud != null && lobbyHud.IsRewardOpen);
        }

        private void OpenReady()
        {
            _root.Game.Paused = true;

            UndeadReadyWindow window = GetWindow(UndeadReadyWindow.Key);
            window.SetLabel(GameRoot.Instance.UndeadTextDataContainer.Ko("start"));
            window.OnStart -= HandleStart;
            window.OnStart += HandleStart;
            window.Open();

            // ★ 원본은 시작 게이트 «위»에 HUD(0/15 · 00:00 · 레벨)가 보인다 [원본 캡처] — 대기 창 뒤에 열어 앞에 온다.
            GetWindow(UndeadBattleHudWindow.Key).Open();
        }

        private void HandleStart()
        {
            _root.Game.Paused = false;
            _root.GameFlow.ChangeScreen(EUndeadScreenType.Battle);
        }

        private void HandleWeaponUnlocked()
        {
            _root.GameFlow.ChangeScreen(EUndeadScreenType.WeaponUnlock);
        }

        /// <summary>무기 해금 화면 [소스 <c>rewardLightningWeapon</c>] — 「확인」이 게임을 다시 흐르게 한다.</summary>
        private void OpenWeaponUnlock()
        {
            UndeadTextDataContainer texts = GameRoot.Instance.UndeadTextDataContainer;

            UndeadWeaponUnlockWindow window = GetWindow(UndeadWeaponUnlockWindow.Key);
            window.Show(texts.Ko("taskReward"), texts.Ko("newWeaponLightning"), texts.Ko("ok"));
            window.OnConfirmed -= HandleWeaponUnlockConfirmed;
            window.OnConfirmed += HandleWeaponUnlockConfirmed;
            window.Open();
        }

        private void HandleWeaponUnlockConfirmed()
        {
            _root.Game.Paused = false;
            _root.GameFlow.ChangeScreen(EUndeadScreenType.Battle);
        }

        private void HandleRunCleared()
        {
            _root.GameFlow.ChangeScreen(EUndeadScreenType.TaskComplete);
        }

        // ══════════════════════════════ 과제 완료 프롬프트 [소스 taskCompletionPrompt · Vd]

        /// <summary>완료가 «쌓인» 뒤 프롬프트까지의 지연 (초) [소스 <c>delayElapsedMs &lt; 2000</c>].</summary>
        private const double TaskPromptDelaySeconds = 2.0;

        private double _taskPromptElapsed = -1.0;

        /// <summary>
        /// 한 판에 <b>한 번만</b> 뜬다 [소스 — 닫으면 <c>isSuppressed = true</c>].
        /// <para>⚠ 없으면 과제를 끝낼 때마다 판이 멈춘다.</para>
        /// </summary>
        private bool _taskPromptSuppressed;

        /// <summary>
        /// 과제가 완료됐다 [소스 <c>queueCompletion</c>] — <b>바로 안 띄운다.</b>
        /// <para>2초를 세고 나서 게임을 멈추고 띄운다. 그동안 죽으면 안 띄운다.</para>
        /// </summary>
        private void HandleTaskCompleted(int taskIndex)
        {
            if (_taskPromptSuppressed || _taskPromptElapsed >= 0.0)
                return;

            _taskPromptElapsed = 0.0;
        }

        /// <summary>매 프레임 — 지연을 세고 때가 되면 띄운다.</summary>
        private void StepTaskPrompt(double dt)
        {
            if (_taskPromptElapsed < 0.0 || _taskPromptSuppressed)
                return;

            // 죽어 있는 동안에는 세지 않는다 [소스 handleHeroDeath — isWaitingForRevive]
            if (_root.Game.Simulation == null || _root.Game.Simulation.HeroDead)
                return;

            _taskPromptElapsed += dt;

            if (_taskPromptElapsed < TaskPromptDelaySeconds)
                return;

            _taskPromptElapsed = -1.0;
            _taskPromptSuppressed = true;

            // ★ 프롬프트가 뜨면 «게임이 선다» [소스 — tickerId "pause" + stopGameplay()]
            _root.Game.Paused = true;
            _root.GameFlow.ChangeScreen(EUndeadScreenType.TaskComplete);
        }

        /// <summary>과제 완료 — 「한 판의 끝」. 「계속」은 이어서, 「로비로」는 한 판을 끝낸다.</summary>
        private void OpenTaskComplete()
        {
            UndeadTextDataContainer texts = GameRoot.Instance.UndeadTextDataContainer;

            UndeadTaskCompleteWindow window = GetWindow(UndeadTaskCompleteWindow.Key);
            window.Show(texts.Ko("taskCompletionTitle"), texts.Ko("taskCompletionMessage"),
                        texts.Ko("continueRun"), texts.Ko("returnToLobby"));
            window.OnContinue -= HandleTaskContinue;
            window.OnContinue += HandleTaskContinue;
            window.OnTemple -= HandleTaskTemple;
            window.OnTemple += HandleTaskTemple;
            window.Open();
        }

        private void HandleTaskContinue()
        {
            _root.Game.Paused = false;
            _root.GameFlow.ChangeScreen(EUndeadScreenType.Battle);
        }

        /// <summary>
        /// 「사원으로」 — <b>로비로 돌아간다</b> [소스 <c>isLobbyUnlocked ? "lobby" : "game"</c>].
        /// <para>⚠ 로비가 아직 «안 열렸으면» 바이옴 진입 화면이다 — <see cref="NextHomeScreen"/> 이 가른다.</para>
        /// </summary>
        private void HandleTaskTemple()
        {
            // ★ 판이 새로 시작하면 «한 번만» 제약도 풀린다 [소스 — 새 판이면 coordinator 가 새로 선다]
            _taskPromptSuppressed = false;
            _taskPromptElapsed = -1.0;

            _root.Game.ResetRun();
            _root.GameFlow.ChangeScreen(NextHomeScreen());
        }

        private void HandleHeroDied()
        {
            _root.GameFlow.ChangeScreen(EUndeadScreenType.Revive);
        }

        /// <summary>
        /// 사망 화면. 카운트다운은 <b>8</b> 이다 [실측].
        /// <para>⚠ 「부활」의 «대가»와 카운트다운 종료 시 원본 거동은 <b>미측정</b>이다.</para>
        /// </summary>
        private void OpenRevive()
        {
            UndeadTextDataContainer texts = GameRoot.Instance.UndeadTextDataContainer;
            UndeadConfigData config = GameRoot.Instance.UndeadConfigDataContainer.Config;

            UndeadReviveWindow window = GetWindow(UndeadReviveWindow.Key);
            window.Show(texts.Ko("revive") + "?", texts.Ko("revive"), texts.Ko("no"),
                        config.ReviveCountdownSeconds);
            window.OnRevive -= HandleRevive;
            window.OnRevive += HandleRevive;
            window.OnDecline -= HandleDecline;
            window.OnDecline += HandleDecline;
            window.Open();
        }

        private void HandleRevive()
        {
            _root.Game.Revive();
            _root.GameFlow.ChangeScreen(EUndeadScreenType.Battle);
        }

        /// <summary>
        /// 부활하지 않는다 — 한 판이 끝난다.
        /// <para>
        /// ⚠ 그다음은 <b>로비(사원)</b> 다 — 로비가 아직 안 열렸으면 바이옴 진입 화면이다
        /// [소스 <c>isLobbyUnlocked ? "lobby" : "game"</c>].
        /// </para>
        /// </summary>
        private void HandleDecline()
        {
            // ★ 판이 새로 시작하면 «한 번만» 제약도 풀린다 [소스 — 새 판이면 coordinator 가 새로 선다]
            _taskPromptSuppressed = false;
            _taskPromptElapsed = -1.0;

            // ★★ <b>최종 사망이 로비를 연다</b> [소스 — <c>finalDeath</c> 에서 <c>isLobbyUnlocked = true</c>].
            //   ⚠ 부활하고 «다시» 죽은 두 번째 죽음이다 — 첫 죽음에는 안 열린다.
            UndeadRecord.UnlockLobby();

            _root.Game.ResetRun();
            _root.GameFlow.ChangeScreen(NextHomeScreen());
        }

        /// <summary>
        /// 판을 마치고 «돌아갈 곳» [소스 <c>isLobbyUnlocked ? "lobby" : "game"</c>].
        /// <para>⚠ 로비가 열리기 «전»에는 로비를 거치지 않는다 — 바로 바이옴 진입 화면이다.</para>
        /// </summary>
        private static EUndeadScreenType NextHomeScreen()
        {
            return UndeadRecord.LobbyUnlocked ? EUndeadScreenType.Lobby : EUndeadScreenType.Ready;
        }

        /// <summary>
        /// 카드를 뽑는다 [소스 <c>generateOptions</c>] — <b>가중치 &gt; 0 인 후보</b> 중에서
        /// <b>가중치 비례 «비복원» 추출</b>로 최대 3장.
        ///
        /// <para>
        /// ⚠ 균등 뽑기가 아니다. 그리고 <b>후보 자체가 상태에 따라 변한다</b> —
        /// 번개를 얻기 전에는 번개 4종의 가중치가 0 이라 <b>아예 안 뜬다</b>.
        /// </para>
        /// </summary>
        private void OpenLevelUp()
        {
            UndeadUpgradeDataContainer upgrades = GameRoot.Instance.UndeadUpgradeDataContainer;
            UndeadTextDataContainer texts = GameRoot.Instance.UndeadTextDataContainer;
            UndeadConfigData config = GameRoot.Instance.UndeadConfigDataContainer.Config;
            UndeadSimulation sim = _root.Game.Simulation;

            _offered.Clear();
            _pool.Clear();
            _weights.Clear();

            for (int i = 0; i < upgrades.AllValues.Count; i++)
            {
                double weight = UndeadUpgradeDataContainer.Weight(upgrades.AllValues[i],
                                                                  sim.LightningCount,
                                                                  sim.LightningDamageStat,
                                                                  sim.Level);

                if (weight <= 0.0)
                    continue;

                _pool.Add(upgrades.AllValues[i]);
                _weights.Add(weight);
            }

            int cardCount = Mathf.Min(config.LevelUpCardCount, _pool.Count);

            for (int card = 0; card < cardCount && _pool.Count > 0; card++)
            {
                double total = 0.0;

                for (int i = 0; i < _weights.Count; i++)
                    total += _weights[i];

                double roll = RandomUtil.Value() * total;
                int picked = 0;

                for (int i = 0; i < _weights.Count; i++)
                {
                    roll -= _weights[i];

                    if (roll > 0.0)
                        continue;

                    picked = i;
                    break;
                }

                _offered.Add(_pool[picked]);
                _pool.RemoveAt(picked);
                _weights.RemoveAt(picked);
            }

            UndeadLevelUpWindow window = GetWindow(UndeadLevelUpWindow.Key);
            window.BindIcons(_upgradeIcons);
            window.Show(_offered, texts.Ko("newLevel"), texts.Ko("choose"));
            window.OnConfirmed -= HandleUpgradeConfirmed;
            window.OnConfirmed += HandleUpgradeConfirmed;
            window.Open();
        }

        private void HandleUpgradeConfirmed(int upgradeId)
        {
            UndeadUpgradeData picked = GameRoot.Instance.UndeadUpgradeDataContainer.Get(upgradeId);

            if (picked != null)
                _root.Game.Simulation.ApplyUpgrade(picked.Code, picked.ApplyKind, picked.Amount);

            _root.Game.Paused = false;
            _root.GameFlow.ChangeScreen(EUndeadScreenType.Battle);
        }
    }
}
