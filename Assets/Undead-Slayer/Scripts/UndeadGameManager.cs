using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 전투 한 판을 소유한다. <b>매 틱 시뮬을 한 스텝 돌린다.</b>
    ///
    /// <para>
    /// ★ <b>원본 메인 루프의 «정지 게이트»를 그대로 옮긴다</b> —
    /// 레벨업 카드가 떠 있는 동안 <b>원본은 게임이 멈춘다</b>(원본 <c>tickerId="pause"</c>).
    /// 그 <c>if</c> 한 줄이 여기 있어야 한다.
    /// </para>
    ///
    /// <para>
    /// ★ 이 클래스는 <b>표 → 시뮬 구조체</b>로 옮겨 담는 일만 한다. 규칙은 전부 시뮬에 있다 —
    /// 여기서 값을 «보정»하면 검사가 보는 것과 게임이 도는 것이 갈린다.
    /// </para>
    /// </summary>
    public class UndeadGameManager : BaseManager, IGameUpdate
    {
        private UndeadSimulation _simulation;
        private UndeadLevelDataContainer _levels;

        /// <summary>입력. 배선이 넣는다 — 매니저가 유니티 입력을 직접 읽지 않는다(테스트가 막힌다).</summary>
        public System.Func<UndeadVec2> MoveInputSource { get; set; }

        /// <summary>
        /// 지금 «정지» 상태인가. 레벨업 카드나 부활 창이 떠 있으면 참이다.
        /// <para>
        /// ★ <b>기본값이 «정지»다</b> — 초기화가 끝나고 첫 화면(대기)이 켜지기 «전»에 몇 프레임이
        /// 흘러 <b>대기 화면인데 적이 이미 7마리 있는</b> 일이 실제로 있었다(회차 9 캡처).
        /// 「아무도 시작을 안 시켰으면 안 흐른다」가 안전한 쪽이다.
        /// </para>
        /// </summary>
        public bool Paused { get; set; } = true;

        public UndeadSimulation Simulation
        {
            get { return _simulation; }
        }

        /// <summary>레벨이 올랐다 — 배선이 카드 화면을 연다.</summary>
        public event System.Action<int> OnLevelUp;

        /// <summary>히어로가 죽었다 — 배선이 「부활?」 창을 연다.</summary>
        public event System.Action OnHeroDied;

        /// <summary>쓰러진 전사를 구했다 — 배선이 「과제 완료」로 간다.</summary>
        public event System.Action OnQuestRescued;

        /// <summary>마법사 퀘스트를 마쳤다 — 배선이 <b>무기 해금 화면</b>을 연다.</summary>
        public event System.Action OnWeaponUnlocked;

        /// <summary>한 판을 클리어했다 — 퀘스트 넷을 다 마쳤다.</summary>
        public event System.Action OnRunCleared;

        protected override void OnInitialize()
        {
            _levels = GameRoot.Instance.UndeadLevelDataContainer;
            UndeadConfigData config = GameRoot.Instance.UndeadConfigDataContainer.Config;

            if (config == null || _levels == null)
            {
                Log.Error("설정·레벨 표가 없다 — 데이터 등록을 본다");
                return;
            }

            _simulation = new UndeadSimulation(BuildConfig(config));
            _simulation.OnLevelUp += HandleLevelUp;
            _simulation.OnHeroDied += HandleHeroDied;
            _simulation.OnQuestRescued += HandleQuestRescued;
            _simulation.OnMageCompleted += HandleWeaponUnlocked;
            _simulation.OnRunCleared += HandleRunCleared;
            _simulation.SetEnemyTypes(BuildEnemyTypes());
            StartRun(config);
        }

        protected override void OnDispose()
        {
            // ⚠ 구독은 «짝»으로 끊는다 — 안 끊으면 다음 판에서 두 번 불린다.
            if (_simulation != null)
            {
                _simulation.OnLevelUp -= HandleLevelUp;
                _simulation.OnHeroDied -= HandleHeroDied;
                _simulation.OnQuestRescued -= HandleQuestRescued;
                _simulation.OnMageCompleted -= HandleWeaponUnlocked;
                _simulation.OnRunCleared -= HandleRunCleared;
            }

            OnRunCleared = null;
            OnWeaponUnlocked = null;
            OnQuestRescued = null;
            OnHeroDied = null;
            OnFinalDeath = null;
            OnLevelUp = null;
        }

        public void OnUpdate(float deltaTime)
        {
            if (_simulation == null)
                return;

            // ★ 원본의 정지 게이트 — 카드가 떠 있으면 «시간이 안 흐른다»
            if (Paused)
                return;

            UndeadVec2 input = MoveInputSource != null ? MoveInputSource() : UndeadVec2.Zero;
            _simulation.Step(deltaTime, input);

            if (_deathDelayRemaining < 0.0)
                return;

            _deathDelayRemaining -= deltaTime;

            if (_deathDelayRemaining > 0.0)
                return;

            _deathDelayRemaining = -1.0;
            Paused = true;

            if (_simulation.HasRevivedThisRun)
                OnFinalDeath?.Invoke();
            else
                OnHeroDied?.Invoke();
        }

        /// <summary>되살린다. 배선이 「부활」을 눌렀을 때 부른다.</summary>
        public void Revive()
        {
            if (_simulation == null)
                return;

            _simulation.Revive();
            Paused = false;
        }

        /// <summary>한 판을 처음부터. 「아니요」를 골랐을 때 배선이 부른다.</summary>
        public void ResetRun()
        {
            if (_simulation == null)
                return;

            StartRun(GameRoot.Instance.UndeadConfigDataContainer.Config);
            Paused = true;
        }

        private void StartRun(UndeadConfigData config)
        {
            // ★ 시작 자리는 «원본 저작값»이다 [소스 — 바이옴 1 은 (1620, 1010)].
            _simulation.Reset(new UndeadVec2(config.HeroStartX, config.HeroStartY), _levels.GoalForLevel(1));
            _simulation.NextGoalForReward = _levels.GoalForLevel(2);
        }

        /// <summary>레벨이 오르면 <b>다음 목표를 «식»에서 받는다</b> — 표를 뒤지지 않는다.</summary>
        private void HandleLevelUp(int level)
        {
            _simulation.GaugeGoal = _levels.GoalForLevel(level);
            _simulation.NextGoalForReward = _levels.GoalForLevel(level + 1);

            Paused = true;
            OnLevelUp?.Invoke(level);
        }

        /// <summary>
        /// 죽으면 <b>1초 뒤에</b> 게임을 멈추고 「부활?」을 띄운다 [소스 — <c>deathDelayMs 1e3</c> 동안 적이 계속 움직인다].
        /// 이미 한 번 살아났으면 「부활?」 없이 <b>판이 끝난다</b> [소스 — <c>meta_hasRevivedThisRun → queueRestart</c>].
        /// </summary>
        private void HandleHeroDied()
        {
            _deathDelayRemaining = _simulation.Config.DeathDelaySeconds;
        }

        private double _deathDelayRemaining = -1.0;

        /// <summary>죽고 나서 «두 번째» 죽음이라 부활 제안 없이 끝난다 — 배선이 「아니요」 경로로 보낸다.</summary>
        public event System.Action OnFinalDeath;

        private void HandleQuestRescued()
        {
            OnQuestRescued?.Invoke();
        }

        /// <summary>클리어 — <b>게임을 멈추고</b> 과제 완료 화면을 띄운다.</summary>
        private void HandleRunCleared()
        {
            Paused = true;
            OnRunCleared?.Invoke();
        }

        /// <summary>번개를 받았다 — <b>게임을 멈추고</b> 해금 화면을 띄운다 [소스 — <c>tickerId="pause"</c>].</summary>
        private void HandleWeaponUnlocked()
        {
            Paused = true;
            OnWeaponUnlocked?.Invoke();
        }

        /// <summary>적 종류를 <b>표에서</b> 옮겨 담는다 — 스폰 우선순위 순이다.</summary>
        private static List<UndeadSimulation.EnemyType> BuildEnemyTypes()
        {
            var list = new List<UndeadSimulation.EnemyType>(8);
            UndeadEnemyDataContainer table = GameRoot.Instance.UndeadEnemyDataContainer;
            IReadOnlyList<UndeadEnemyData> ordered = table.ByPriority;

            for (int i = 0; i < ordered.Count; i++)
            {
                UndeadEnemyData e = ordered[i];

                list.Add(new UndeadSimulation.EnemyType
                {
                    Id = e.Id,
                    Code = e.Code,
                    Speed = e.BaseMoveSpeedWorld,
                    MaxHpBase = e.MaxHpBase,
                    MaxHpPerWave = e.MaxHpPerWave,
                    UnlockWave = e.UnlockWave,
                    CostBase = e.SpawnCostBase,
                    CostPerWave = e.SpawnCostPerWave,
                    BudgetEarly = e.BudgetMultiplierEarly,
                    BudgetLate = e.BudgetMultiplierLate,
                    BudgetSwitchWave = e.BudgetSwitchWave,
                    InitialBudget = e.InitialThreatBudget,
                    SpawnOnUnlock = e.SpawnOnUnlock,
                    ScaleBase = e.SpriteScaleBase,
                    ScalePerWave = e.SpriteScalePerWave,
                    ScaleMax = e.SpriteScaleMax,
                });
            }

            return list;
        }

        private static UndeadSimConfig BuildConfig(UndeadConfigData c)
        {
            return new UndeadSimConfig
            {
                HeroMoveSpeed = c.HeroMoveSpeedWorld,
                HeroStartX = c.HeroStartX,
                HeroStartY = c.HeroStartY,
                HeroMaxHp = c.HeroMaxHp,
                HeroInvincibleSeconds = c.HeroInvincibleSeconds,
                DeathDelaySeconds = c.DeathDelaySeconds,
                ReviveHp = c.ReviveHp,
                ReviveKillRadius = c.ReviveKillRadius,
                ReviveKillOthersChance = c.ReviveKillOthersChance,
                ReviveDifficultyMultiplier = c.ReviveDifficultyMultiplier,
                ReviveDifficultyRecoverySeconds = c.ReviveDifficultyRecoverySeconds,
                HeroHitTintSeconds = c.HeroHitTintSeconds,
                HeroBlinkIntervalSeconds = c.HeroBlinkIntervalSeconds,
                HeroBlinkAlpha = c.HeroBlinkAlpha,
                HeroColliderX = c.HeroColliderX,
                HeroColliderY = c.HeroColliderY,
                HeroColliderW = c.HeroColliderW,
                HeroColliderH = c.HeroColliderH,
                CollectRadius = c.CollectRadiusWorld,

                FireInterval = c.FireIntervalSeconds,
                ProjectileDamage = c.ProjectileDamage,
                ProjectileSpeed = c.ProjectileSpeedWorld,
                MuzzleOffsetY = c.MuzzleOffsetY,

                // ⚠ 아래 셋은 «원본 소스에 있는» 값이다 — 유도한 것이 아니다.
                ProjectileRadius = 12.0,
                ProjectileRangeScreens = 1.5,
                ProjectileKnockback = 2.0,

                // ★ 번개 [소스 — 구슬 반경 25 · 중심 (+3, −25) · 넉백 1]
                LightningOrbRadius = 25.0,
                LightningCenterX = 3.0,
                LightningCenterY = -25.0,
                LightningKnockback = 1.0,

                EnemyColliderX = c.EnemyColliderX,
                EnemyColliderY = c.EnemyColliderY,
                EnemyColliderW = c.EnemyColliderW,
                EnemyColliderH = c.EnemyColliderH,
                EnemyAttackInterval = c.EnemyAttackIntervalSeconds,
                EnemyFirstAttackWait = c.EnemyFirstAttackWaitSeconds,
                EnemyContactDamage = c.EnemyContactDamage,
                EnemySpeedJitter = c.EnemySpeedJitter,
                EnemyKnockbackDecay = c.EnemyKnockbackDecay,
                EnemyRespawnOutOfViewSeconds = c.EnemyRespawnOutOfViewSeconds,
                EnemyChaseDeadZone = c.EnemyChaseDeadZone,

                MsPerWave = c.MsPerWave,
                OnboardingWaveCount = c.OnboardingWaveCount,
                BaseWaveBoost = c.BaseWaveBoost,
                SteepWaveInterval = c.SteepWaveInterval,
                SteepWaveBoost = c.SteepWaveBoost,
                BaseSpawnIntervalMs = c.BaseSpawnIntervalMs,
                MaxSpawnIntervalMultiplier = c.MaxSpawnIntervalMultiplier,
                SpawnMargin = c.SpawnMargin,
                ThreatBudgetPerFrame = c.ThreatBudgetPerFrame,

                CameraSmoothing = c.CameraSmoothing,
                CameraOffsetY = c.CameraOffsetY,

                // ★ 화면 크기는 «설계 세로»에서 나온다 — 세로 고정 · 가로는 화면비만큼 [확정표 3-d].
                ViewportHeight = c.DesignViewportHeight,
                ViewportWidth = c.DesignViewportHeight * (16.0 / 9.0),

                GemFallOffsetY = 30.0,
                GemGravity = 800.0,
                GemBounceDamping = 0.4,
                GemMaxBounces = 1,
                GemFlySeconds = 0.4,

                AnimationFps = c.AnimationFps,
                FrameTicksPerSecond = c.FrameTicksPerSecond,

                QuestMeterPixels = c.QuestMeterPixels,
                QuestIntroDelaySeconds = c.QuestIntroDelaySeconds,
                WarriorOffset = new UndeadVec2(c.WarriorOffsetX, c.WarriorOffsetY),
                WarriorFollowOffsetX = c.WarriorFollowOffsetX,
                MageOffset = new UndeadVec2(c.MageOffsetX, c.MageOffsetY),
                MageMeetRadius = c.MageMeetRadius,
                Fragment1Offset = new UndeadVec2(c.Fragment1OffsetX, c.Fragment1OffsetY),
                Fragment2Offset = new UndeadVec2(c.Fragment2OffsetX, c.Fragment2OffsetY),
                FragmentPickupRadius = c.FragmentPickupRadius,
                FragmentDeliverRadius = c.FragmentDeliverRadius,
                LightningRewardCount = c.LightningRewardCount,

                FamilyOffset = new UndeadVec2(c.FamilyOffsetX, c.FamilyOffsetY),
                FamilyTriggerRadius = c.FamilyTriggerRadius,
                DarkSoulSummonSeconds = c.DarkSoulSummonSeconds,
                BossHpPerDamage = c.BossHpPerDamage,
                BossMoveSpeed = c.BossMoveSpeedWorld,
                BossColliderX = c.BossColliderX,
                BossColliderY = c.BossColliderY,
                BossColliderW = c.BossColliderW,
                BossColliderH = c.BossColliderH,
                BossAttackInterval = c.BossAttackIntervalSeconds,
                BossFireballInterval = c.BossFireballIntervalSeconds,
                BossAttackAnimSeconds = c.BossAttackAnimSeconds,
                BossFireballBurst = c.BossFireballBurst,
                BossChaseDeadZone = c.BossChaseDeadZone,
                BossKnockbackDecay = c.BossKnockbackDecay,
                BossGemFountainCount = c.BossGemFountainCount,
                BossGemXp = c.BossGemXp,

                FireballSpeed = c.FireballSpeedWorld,
                FireballRadius = c.FireballRadius,
                FireballMaxDistance = c.FireballMaxDistance,
                FireballDamage = c.FireballDamage,

                FarmerOffset = new UndeadVec2(c.FarmerOffsetX, c.FarmerOffsetY),
                SheepCount = c.SheepCount,
                FarmerMeetRadius = c.FarmerMeetRadius,
                SheepPickupRadius = c.SheepPickupRadius,
                SheepOffsets = new[]
                {
                    new UndeadVec2(c.Sheep0OffsetX, c.Sheep0OffsetY),
                    new UndeadVec2(c.Sheep1OffsetX, c.Sheep1OffsetY),
                    new UndeadVec2(c.Sheep2OffsetX, c.Sheep2OffsetY),
                    new UndeadVec2(c.Sheep3OffsetX, c.Sheep3OffsetY),
                    new UndeadVec2(c.Sheep4OffsetX, c.Sheep4OffsetY),
                },
                EncounterInterval = c.EncounterIntervalSeconds,
                EncounterSheepMinOffscreen = c.EncounterSheepMinOffscreen,
                EncounterSheepMaxOffscreen = c.EncounterSheepMaxOffscreen,
                EncounterFoldMinDistance = c.EncounterFoldMinDistance,
                EncounterFoldMaxDistance = c.EncounterFoldMaxDistance,
                EncounterThanksSeconds = c.EncounterThanksSeconds,
                EncounterRewardGems = c.EncounterRewardGems,
                FarmerGemFountainCount = c.FarmerGemFountainCount,
                FarmerGemXp = c.FarmerGemXp,
                GemFountainInterval = c.GemFountainIntervalSeconds,
                NpcThanksSeconds = c.NpcThanksSeconds,

                FireplaceLives = c.FireplaceLives,
                FireplaceHealAmount = c.FireplaceHealAmount,
                FireplaceRadius = c.FireplaceRadius,
                FireplaceHealCooldown = c.FireplaceHealCooldownSeconds,
                FireOutCooldownSeconds = c.FireOutCooldownSeconds,
                FireplaceColliderOffsetY = c.FireplaceColliderOffsetY,
                ObjectChunkTiles = c.ObjectChunkTiles,
                TreeThreshold = c.TreeThreshold,
                TreeWidthTiles = c.TreeWidthTiles,
                TreeHeightTiles = c.TreeHeightTiles,
                TreeAnchorOffsetX = c.TreeAnchorOffsetX,
                TreeTriggerRadius = c.TreeTriggerRadius,
                TreeChargeSeconds = c.TreeChargeSeconds,
                TreeChargeDecayMultiplier = c.TreeChargeDecayMultiplier,
                TreeIdleSpeed = c.TreeIdleSpeed,
                TreeBurstMeteorCount = c.TreeBurstMeteorCount,
                MeteorSpeed = c.MeteorSpeedWorld,
                MeteorRadius = c.MeteorRadius,
                MeteorDamageMultiplier = c.MeteorDamageMultiplier,
                MeteorKnockback = c.MeteorKnockback,
                FireplaceThreshold = c.FireplaceThreshold,
                FireplaceAnchorOffset = c.FireplaceAnchorOffset,
                QuestRescueRadius = c.QuestRescueRadius,
                QuestRescueSeconds = c.QuestRescueSeconds,
            };
        }
    }
}
