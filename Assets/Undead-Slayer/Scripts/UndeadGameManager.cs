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

        /// <summary>스킬 키를 읽는 곳 — 배선이 꽂는다. 없으면 스킬이 <b>영원히 안 나간다</b>.</summary>
        public System.Func<EUndeadSkill> SkillInputSource { get; set; }

        /// <summary>
        /// 지금 «정지» 상태인가. 레벨업 카드나 부활 창이 떠 있으면 참이다.
        /// <para>
        /// ★ <b>기본값이 «정지»다</b> — 초기화가 끝나고 첫 화면(대기)이 켜지기 «전»에 몇 프레임이
        /// 흘러 <b>대기 화면인데 적이 이미 7마리 있는</b> 일이 실제로 있었다(회차 9 캡처).
        /// 「아무도 시작을 안 시켰으면 안 흐른다」가 안전한 쪽이다.
        /// </para>
        /// </summary>
        public bool Paused { get; set; } = true;

        /// <summary>
        /// 지금 판이 도는 <b>바이옴</b> — <b>1 묘지 · 2 겨울 황무지</b> [소스 <c>vu({biome})</c>].
        ///
        /// <para>
        /// ★ 로비 포털이 정한다. <b>판을 새로 세우기 «전»에</b> 바꿔야 한다 —
        /// <see cref="ResetRun"/> 이 이 값으로 시작 자리를 고른다.
        /// </para>
        /// </summary>
        public int Biome { get; private set; } = 1;

        /// <summary>바이옴을 바꾼다 [소스 <c>op(biome)</c>] — 다음 <see cref="ResetRun"/> 부터 먹는다.</summary>
        public void SetBiome(int biome)
        {
            Biome = biome;
        }

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

        /// <summary>
        /// 과제 하나가 «완료»됐다 [소스 <c>taskCompletionPrompt.queueCompletion</c>].
        /// <para>⚠ 이건 «퀘스트 체인 클리어»와 다른 사건이다 — 원본의 프롬프트는 이쪽에 물린다.</para>
        /// </summary>
        public event System.Action<int> OnTaskCompleted;

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
            _simulation.OnTaskCompleted += HandleTaskCompleted;
            _simulation.SetEnemyTypes(BuildEnemyTypes());

            // ★★ 과제를 세운다 — <b>안 부르면 과제가 0개인 채로 돈다</b>(그런데 오류는 안 난다).
            _simulation.SetupTasks(BuildTaskDefinitions());

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
                _simulation.OnTaskCompleted -= HandleTaskCompleted;
            }

            OnRunCleared = null;
            OnTaskCompleted = null;
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

            // ★ 스킬은 «시뮬을 돌리기 전»에 받는다 — 그래야 누른 프레임에 곧바로 나간다.
            //   ⚠ 정지 중에는 안 받는다 [소스 isGameplayPaused] — 카드가 떠 있을 때 눌리면 안 된다.
            if (SkillInputSource != null)
            {
                EUndeadSkill pressed = SkillInputSource();

                if (pressed != EUndeadSkill.None)
                    _simulation.ActivateSkill(pressed);
            }

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

        /// <summary>
        /// 시작 자리 — <b>바이옴 표가 정본이다</b> [소스 <c>i.x = 1===t ? 1620 : 640</c>].
        /// <para>⚠ 표에 그 바이옴이 없으면 <b>조용히 (0,0) 에서 시작하지 않는다</b> — 시끄럽게 알리고 1 로 선다.</para>
        /// </summary>
        private UndeadVec2 HeroStart()
        {
            UndeadBiomeDataContainer biomes = GameRoot.Instance.UndeadBiomeDataContainer;
            UndeadBiomeData row = biomes == null ? null : biomes.Get(Biome);

            if (row == null)
            {
                Log.Error($"바이옴 {Biome} 이 표에 없다 — 바이옴 1 자리에서 시작한다");
                row = biomes == null ? null : biomes.Get(1);
            }

            return row == null ? UndeadVec2.Zero : new UndeadVec2(row.HeroStartX, row.HeroStartY);
        }

        private void StartRun(UndeadConfigData config)
        {
            // ★ 시작 자리는 «원본 저작값»이다 [소스 — 바이옴 1 은 (1620, 1010)].
            // ⚠ 바이옴을 «먼저» 심는다 — Reset 이 도는 동안 과제 지표 판정이 이 값을 본다
            _simulation.SetBiome(Biome);
            _simulation.Reset(HeroStart(), _levels.GoalForLevel(1));
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

        private void HandleTaskCompleted(int taskIndex)
        {
            OnTaskCompleted?.Invoke(taskIndex);
        }

        /// <summary>번개를 받았다 — <b>게임을 멈추고</b> 해금 화면을 띄운다 [소스 — <c>tickerId="pause"</c>].</summary>
        private void HandleWeaponUnlocked()
        {
            Paused = true;
            OnWeaponUnlocked?.Invoke();
        }

        /// <summary>적 종류를 <b>표에서</b> 옮겨 담는다 — 스폰 우선순위 순이다.</summary>
        /// <summary>
        /// 스킬 표를 <see cref="EUndeadSkill"/> <b>순서대로</b> 배열에 담는다.
        ///
        /// <para>
        /// ⚠ <b>표의 줄 순서가 아니라 <c>Order</c> 가 슬롯 번호다.</b> 줄 순서를 믿으면
        /// 표를 한 줄 옮겼을 때 쿨다운이 서로 바뀐다 — 그런데 <b>오류는 안 난다</b>.
        /// </para>
        /// </summary>
        private static double[] BuildSkillSeconds(System.Func<UndeadSkillData, double> pick)
        {
            var values = new double[UndeadSimulation.SkillCount];
            UndeadSkillDataContainer table = GameRoot.Instance.UndeadSkillDataContainer;

            if (table == null)
            {
                Log.Error("스킬 표가 없다 — 컨테이너 등록을 본다");
                return values;
            }

            IReadOnlyList<UndeadSkillData> all = table.AllValues;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Order >= 0 && all[i].Order < values.Length)
                    values[all[i].Order] = pick(all[i]);
                else
                    Log.Error($"스킬 {all[i].Code} 의 Order {all[i].Order} 가 슬롯 범위 밖이다");
            }

            return values;
        }

        /// <summary>과제 표를 시뮬이 쓰는 정의로 옮긴다 — 시뮬은 <c>JinHyung.Data</c> 를 안 본다.</summary>
        private static UndeadSimulation.UndeadTaskDefinition[] BuildTaskDefinitions()
        {
            UndeadTaskDataContainer table = GameRoot.Instance.UndeadTaskDataContainer;

            if (table == null)
            {
                Log.Error("과제 표가 없다 — 컨테이너 등록을 본다");
                return System.Array.Empty<UndeadSimulation.UndeadTaskDefinition>();
            }

            IReadOnlyList<UndeadTaskData> all = table.AllValues;
            var list = new UndeadSimulation.UndeadTaskDefinition[all.Count];

            for (int i = 0; i < all.Count; i++)
            {
                list[i] = new UndeadSimulation.UndeadTaskDefinition
                {
                    Code = all[i].Code,
                    Order = all[i].Order,
                    MetricId = all[i].MetricId,
                    GoalOffset = all[i].GoalOffset,
                    RewardSkill = SkillOf(all[i].RewardSkill),
                    PrerequisiteSkills = SkillsOf(all[i].PrerequisiteSkills),
                };
            }

            return list;
        }

        /// <summary>스킬 코드를 슬롯 번호로 — <b>표의 <c>Order</c> 를 지난다</b>(코드 문자열을 코드에 박지 않는다).</summary>
        private static EUndeadSkill SkillOf(string code)
        {
            if (string.IsNullOrEmpty(code))
                return EUndeadSkill.None;

            UndeadSkillData data = GameRoot.Instance.UndeadSkillDataContainer?.Get(code);

            if (data == null)
            {
                Log.Error($"스킬 코드가 표에 없다 — {code}");
                return EUndeadSkill.None;
            }

            return (EUndeadSkill)data.Order;
        }

        private static EUndeadSkill[] SkillsOf(string codes)
        {
            if (string.IsNullOrEmpty(codes))
                return System.Array.Empty<EUndeadSkill>();

            string[] parts = codes.Split(';');
            var list = new List<EUndeadSkill>(parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                string code = parts[i].Trim();

                if (code.Length == 0)
                    continue;

                EUndeadSkill skill = SkillOf(code);

                if (skill != EUndeadSkill.None)
                    list.Add(skill);
            }

            return list.ToArray();
        }

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
                HeroMaxHp = c.HeroMaxHp,
                HeroInvincibleSeconds = c.HeroInvincibleSeconds,
                DeathDelaySeconds = c.DeathDelaySeconds,
                ReviveHp = c.ReviveHp,
                ReviveKillRadius = c.ReviveKillRadius,
                ReviveKillOthersChance = c.ReviveKillOthersChance,
                ReviveDifficultyMultiplier = c.ReviveDifficultyMultiplier,
                ReviveDifficultyRecoverySeconds = c.ReviveDifficultyRecoverySeconds,
                HeroHitTintSeconds = c.HeroHitTintSeconds,
                EnemyHitTintSeconds = c.EnemyHitTintSeconds,

                // ── 액티브 스킬 [소스] — 쿨다운·지속은 «스킬 표»가, 나머지 상수는 설정 표가 든다
                SkillCooldownSeconds = BuildSkillSeconds(skill => skill.CooldownSeconds),
                SkillActiveSeconds = BuildSkillSeconds(skill => skill.ActiveSeconds),
                DashDistance = c.DashDistance,
                DashStepUnits = c.DashStepUnits,
                DashDamageMultiplier = c.DashDamageMultiplier,
                DashEndKnockbackRadius = c.DashEndKnockbackRadius,
                DashEndKnockbackForce = c.DashEndKnockbackForce,
                TrailSegmentSpacing = c.TrailSegmentSpacing,
                TrailSegmentLifeSeconds = c.TrailSegmentLifeSeconds,
                TrailSegmentRadius = c.TrailSegmentRadius,
                TrailDamageMultiplier = c.TrailDamageMultiplier,
                TrailTargetThrottleSeconds = c.TrailTargetThrottleSeconds,
                FlashMoveSimulationScale = c.FlashMoveSimulationScale,
                MageRewardBubbleSeconds = c.MageRewardBubbleSeconds,
                FamilyWarningBubbleSeconds = c.FamilyWarningBubbleSeconds,
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
                WarriorFollowSpeedMultiplier = c.WarriorFollowSpeedMultiplier,
                WarriorFollowStopDistance = c.WarriorFollowStopDistance,
                WarriorAttackIntervalSeconds = c.WarriorAttackIntervalSeconds,
                KunaiSpawnOffsetY = c.KunaiSpawnOffsetY,
                KunaiSpeedWorld = c.KunaiSpeedWorld,
                KunaiRadius = c.KunaiRadius,
                KunaiKnockback = c.KunaiKnockback,
                WarriorThanksBubbleSeconds = c.WarriorThanksBubbleSeconds,
                QuestIntroMageDelaySeconds = c.QuestIntroMageDelaySeconds,
                QuestIntroMageBubbleSeconds = c.QuestIntroMageBubbleSeconds,
                QuestIntroFirstBossDelaySeconds = c.QuestIntroFirstBossDelaySeconds,
                QuestIntroFirstBossBubbleSeconds = c.QuestIntroFirstBossBubbleSeconds,
                QuestIntroFarmerDelaySeconds = c.QuestIntroFarmerDelaySeconds,
                QuestIntroFarmerBubbleSeconds = c.QuestIntroFarmerBubbleSeconds,
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
