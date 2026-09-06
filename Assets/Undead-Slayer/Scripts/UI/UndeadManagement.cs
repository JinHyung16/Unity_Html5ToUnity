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
                }
            }
        }

        private void LateUpdate()
        {
            if (IsInitialized == false || _root == null || _root.Game == null)
                return;

            UndeadSimulation sim = _root.Game.Simulation;

            if (sim == null)
                return;

            UndeadBattleHudWindow hud = GetWindow(UndeadBattleHudWindow.Key);

            if (hud == null || hud.IsOpen() == false)
                return;

            hud.SetGauge(sim.Gauge, sim.GaugeGoal);
            hud.SetElapsed(sim.ElapsedSeconds);
            hud.SetLevel(sim.Level);
            // 포인터 [소스 bd.update] — 퀘스트가 켜져 있고 · 레벨업 카드가 안 떠 있고 · 대상이 화면 «밖»일 때만
            UndeadLevelUpWindow levelUp = GetWindow(UndeadLevelUpWindow.Key);
            bool pointerAllowed = sim.QuestActive && (levelUp == null || levelUp.IsOpen() == false);
            // 화면 중앙이 보는 월드 점은 피벗에서 offsetY 만큼 «위»다 [소스 — 히어로가 화면 중앙보다 20 아래 선다]
            UndeadVec2 fromCamera = sim.QuestTarget - new UndeadVec2(sim.CameraPivot.X, sim.CameraPivot.Y - sim.Config.CameraOffsetY);
            hud.SetQuest(pointerAllowed, (float)fromCamera.X, (float)fromCamera.Y, sim.QuestDistanceMeters, Time.deltaTime);
        }

        private void HandleScreenChanged(EUndeadScreenType previous, EUndeadScreenType next)
        {
            switch (next)
            {
                case EUndeadScreenType.Ready:
                    OpenReady();
                    break;

                case EUndeadScreenType.Battle:
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

        private void HandleLevelUp(int level)
        {
            _root.GameFlow.ChangeScreen(EUndeadScreenType.LevelUp);
        }

        /// <summary>
        /// 바이옴 진입 화면. ★ <b>원본은 「시작」을 눌러야 전투가 시작된다</b> [실측] —
        /// 그동안 타이머는 <c>00:00</c> 이고 적도 안 나온다. 그래서 <b>멈춰 둔다</b>.
        /// </summary>
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

        /// <summary>⚠ 로비는 <b>이관 범위 밖</b>이라 대기 화면으로 되돌린다 (의도된 차이).</summary>
        private void HandleTaskTemple()
        {
            _root.Game.ResetRun();
            _root.GameFlow.ChangeScreen(EUndeadScreenType.Ready);
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
        /// ⚠ <b>원본이 그다음 어디로 가는지는 «이관 범위 밖»이다</b>(로비 「사원」).
        /// 우리는 <b>대기 화면으로 되돌린다</b> — 「멈춤」은 사람이 갇힌다. 의도된 차이로 등재.
        /// </para>
        /// </summary>
        private void HandleDecline()
        {
            _root.Game.ResetRun();
            _root.GameFlow.ChangeScreen(EUndeadScreenType.Ready);
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
