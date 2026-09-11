namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 시뮬이 도는 데 필요한 값 한 벌. <b>표에서 옮겨 담는 «순수 구조체»</b>다 —
    /// 시뮬은 유니티도 <c>DataContainer</c> 도 모른다.
    ///
    /// <para>
    /// ⚠ <b>여기 기본값을 넣지 않는다.</b> 안 담긴 값은 0 이고, 0 이면 계산이 눈에 띄게 죽는다 —
    /// 「그럴듯한 기본값」이 있으면 <b>표를 안 옮긴 것이 조용히 굴러간다</b>.
    /// </para>
    ///
    /// <para>단위는 전부 <b>원본 좌표계 · 초</b>다 (원본 소스의 <c>px/ms</c> 는 ×1000 해서 담는다).</para>
    /// </summary>
    public struct UndeadSimConfig
    {
        // ── 히어로
        public double HeroMoveSpeed;
        public int HeroMaxHp;
        public double HeroInvincibleSeconds;

        // ── 부활 [소스 — applyRevive · killEnemiesForRevive · applyReviveDifficultyRelief]
        public double DeathDelaySeconds;
        public int ReviveHp;
        public double ReviveKillRadius;
        public double ReviveKillOthersChance;
        public double ReviveDifficultyMultiplier;
        public double ReviveDifficultyRecoverySeconds;
        public double HeroHitTintSeconds;
        public double EnemyHitTintSeconds;

        // ══════════════════════════════ 액티브 스킬 [소스 — 스킬 정의 배열 · 동작 클래스]

        /// <summary>스킬별 재사용 대기 (초) — <see cref="EUndeadSkill"/> 순서다.</summary>
        public double[] SkillCooldownSeconds;

        /// <summary>스킬별 효과 지속 (초) — <see cref="EUndeadSkill"/> 순서다.</summary>
        public double[] SkillActiveSeconds;

        /// <summary>돌진이 지속시간 동안 «전부» 나아가는 거리 [소스 300].</summary>
        public double DashDistance;

        /// <summary>돌진을 몇 «칸»으로 쪼개 옮기나 [소스 4] — 잘게 쪼개야 통과하며 때린다.</summary>
        public double DashStepUnits;

        /// <summary>돌진 피해 = 총알 피해 × 이 값 [소스 2].</summary>
        public double DashDamageMultiplier;

        /// <summary>돌진이 끝나는 자리에서 미는 반경 [소스 √32400 = 180].</summary>
        public double DashEndKnockbackRadius;

        /// <summary>그때 미는 세기 [소스 12].</summary>
        public double DashEndKnockbackForce;

        /// <summary>화염 자취 조각 간격 [소스 24].</summary>
        public double TrailSegmentSpacing;

        /// <summary>조각 하나가 남아 있는 시간 (초).</summary>
        public double TrailSegmentLifeSeconds;

        /// <summary>조각의 판정 반경.</summary>
        public double TrailSegmentRadius;

        /// <summary>자취 피해 = 총알 피해 × 이 값 [소스 0.5].</summary>
        public double TrailDamageMultiplier;

        /// <summary>같은 적을 다시 태우기까지 (초) [소스 500ms].</summary>
        public double TrailTargetThrottleSeconds;

        /// <summary>섬광 이동이 올리는 시뮬 배율 [소스 2].</summary>
        public double FlashMoveSimulationScale;

        /// <summary>마법사 보상 말풍선 (초) [소스 <c>rewardBubbleDurationMs = 2000</c>].</summary>
        public double MageRewardBubbleSeconds;

        /// <summary>가족 경고 말풍선 (초) [소스 <c>familyWarningBubbleDurationMs = 5000</c>].</summary>
        public double FamilyWarningBubbleSeconds;
        public double HeroBlinkIntervalSeconds;
        public double HeroBlinkAlpha;
        public double HeroColliderX;
        public double HeroColliderY;
        public double HeroColliderW;
        public double HeroColliderH;
        public double CollectRadius;

        // ── 사격
        public double FireInterval;
        public int ProjectileDamage;
        public double ProjectileSpeed;
        public double MuzzleOffsetY;

        /// <summary>총알 충돌 반경 [소스 — 12].</summary>
        public double ProjectileRadius;

        /// <summary>히어로에서 이 배수 × 화면 긴 변을 넘어가면 사라진다 [소스 — 1.5].</summary>
        public double ProjectileRangeScreens;

        /// <summary>총알이 맞혔을 때 미는 세기 [소스 — 2].</summary>
        public double ProjectileKnockback;

        // ── 번개 (궤도 무기) [소스 — ql · heroLightning]
        /// <summary>구슬의 충돌 반경 [소스 — 25].</summary>
        public double LightningOrbRadius;

        /// <summary>구슬이 도는 중심의 히어로 기준 오프셋 [소스 — (+3, −25)].</summary>
        public double LightningCenterX;

        public double LightningCenterY;

        /// <summary>번개가 맞혔을 때 미는 세기 [소스 — 1].</summary>
        public double LightningKnockback;

        // ── 적
        public double EnemyColliderX;
        public double EnemyColliderY;
        public double EnemyColliderW;
        public double EnemyColliderH;
        public double EnemyAttackInterval;
        public double EnemyFirstAttackWait;
        public int EnemyContactDamage;
        public double EnemySpeedJitter;
        public double EnemyKnockbackDecay;
        public double EnemyRespawnOutOfViewSeconds;
        public double EnemyChaseDeadZone;

        // ── 스폰 (위협 예산)
        public double MsPerWave;
        public int OnboardingWaveCount;
        public double BaseWaveBoost;
        public int SteepWaveInterval;
        public double SteepWaveBoost;
        public double BaseSpawnIntervalMs;
        public double MaxSpawnIntervalMultiplier;
        public double SpawnMargin;
        public double ThreatBudgetPerFrame;

        // ── 카메라 · 화면 (스폰 자리 · 조준 후보 · 화면 밖 판정에 쓰인다)
        public double CameraSmoothing;
        public double CameraOffsetY;
        public double ViewportWidth;
        public double ViewportHeight;

        // ── 보석
        public double GemFallOffsetY;
        public double GemGravity;
        public double GemBounceDamping;
        public int GemMaxBounces;
        public double GemFlySeconds;

        // ── 표시 · 틱
        public double AnimationFps;
        public int FrameTicksPerSecond;

        // ── 퀘스트 체인 [소스 — questManager · warrior · mage]
        public double QuestMeterPixels;
        public double QuestIntroDelaySeconds;
        public double QuestRescueRadius;
        public double QuestRescueSeconds;
        public UndeadVec2 WarriorOffset;
        public double WarriorFollowOffsetX;
        public double WarriorFollowSpeedMultiplier;
        public double WarriorFollowStopDistance;
        public double WarriorAttackIntervalSeconds;

        // ── 전사의 쿠나이 [소스 class Pd] — 동료가 «스스로» 싸운다
        public double KunaiSpawnOffsetY;
        public double KunaiSpeedWorld;
        public double KunaiRadius;
        public double KunaiKnockback;

        // ── 전사가 다음 과제를 «예고»한다 [소스 el.introduction — type "warrior"]
        public double WarriorThanksBubbleSeconds;
        public double QuestIntroMageDelaySeconds;
        public double QuestIntroMageBubbleSeconds;
        public double QuestIntroFirstBossDelaySeconds;
        public double QuestIntroFirstBossBubbleSeconds;
        public double QuestIntroFarmerDelaySeconds;
        public double QuestIntroFarmerBubbleSeconds;

        public UndeadVec2 MageOffset;
        public double MageMeetRadius;
        public UndeadVec2 Fragment1Offset;
        public UndeadVec2 Fragment2Offset;
        public double FragmentPickupRadius;
        public double FragmentDeliverRadius;
        public int LightningRewardCount;

        // ── 퀘스트 3 : 첫 보스 [소스]
        public UndeadVec2 FamilyOffset;
        public double FamilyTriggerRadius;
        public double DarkSoulSummonSeconds;
        public int BossHpPerDamage;
        public double BossMoveSpeed;
        public double BossColliderX;
        public double BossColliderY;
        public double BossColliderW;
        public double BossColliderH;
        public double BossAttackInterval;
        public double BossFireballInterval;
        public double BossAttackAnimSeconds;
        public int BossFireballBurst;
        public double BossChaseDeadZone;
        public double BossKnockbackDecay;
        public int BossGemFountainCount;
        public int BossGemXp;

        // ── 불덩이 [소스]
        public double FireballSpeed;
        public double FireballRadius;
        public double FireballMaxDistance;
        public int FireballDamage;

        // ── 퀘스트 4 : 농부 · 양 [소스]
        public UndeadVec2 FarmerOffset;
        public int SheepCount;
        public double FarmerMeetRadius;
        public double SheepPickupRadius;
        public UndeadVec2[] SheepOffsets;
        public double EncounterInterval;
        public double EncounterSheepMinOffscreen;
        public double EncounterSheepMaxOffscreen;
        public double EncounterFoldMinDistance;
        public double EncounterFoldMaxDistance;
        public double EncounterThanksSeconds;
        public int EncounterRewardGems;
        public int FarmerGemFountainCount;
        public int FarmerGemXp;
        public double GemFountainInterval;
        public double NpcThanksSeconds;

        // ── 모닥불 [소스]
        public int FireplaceLives;
        public int FireplaceHealAmount;
        public double FireplaceRadius;
        public double FireplaceHealCooldown;
        public double FireOutCooldownSeconds;
        public double FireplaceColliderOffsetY;

        // ── 월드 오브젝트 [소스] — 청크 노이즈가 놓는 나무·모닥불
        public int ObjectChunkTiles;
        public double TreeThreshold;
        public int TreeWidthTiles;
        public int TreeHeightTiles;
        public double TreeAnchorOffsetX;
        public double TreeTriggerRadius;
        public double TreeChargeSeconds;
        public double TreeChargeDecayMultiplier;
        public double TreeIdleSpeed;
        public int TreeBurstMeteorCount;
        public double MeteorSpeed;
        public double MeteorRadius;
        public double MeteorDamageMultiplier;
        public double MeteorKnockback;
        public double FireplaceThreshold;
        public double FireplaceAnchorOffset;
    }
}
