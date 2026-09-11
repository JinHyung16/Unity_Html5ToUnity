namespace JinHyung.Data
{
    /// <summary>
    /// 이 게임의 <b>거동 상수 한 벌</b>. 행 하나짜리 표다.
    ///
    /// <para>
    /// ★★ <b>근거 등급</b> — <c>~Samples</c> 컬럼의 뜻이 셋이다.
    /// <b><c>-1</c> = 원본 «소스 직독»</b>(가장 강한 근거 · 저작 상수 그대로) ·
    /// <b>양수 = 실측</b>(관측 표본 수) · <b><c>0</c> = 미측정</b>(그 값은 0 이어야 한다).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>소스 직독 값을 관측치로 «되돌리지» 않는다.</b> 회차 9 에서 실측 169.3 이 저작값 170 으로,
    /// 0.3576 이 350ms 로 확인됐다 — 실측이 2% 안에서 맞았지만 <b>정본은 저작값</b>이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 여기 없는 것(적 종류별 스탯 · 업그레이드 · 레벨 목표)은 <b>각자의 표</b>가 든다.
    /// </para>
    /// </summary>
    public class UndeadConfigData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        // ══════════════════════════════ 히어로

        /// <summary>이동 속도 (world px/s). [소스] <c>0.17 px/ms × heroMoveSpeed(1)</c>.</summary>
        public double HeroMoveSpeedWorld { get; set; }

        public int HeroMoveSpeedSamples { get; set; }

        /// <summary>바이옴 1 의 시작 좌표 [소스].</summary>


        /// <summary>
        /// 최대 체력 [소스 — <c>maxHP = 7</c>].
        /// <para>⚠ 회차 6 실측은 「6대」였다 — <b>마지막 타격이 사망 화면에 가려 한 대를 놓친 것</b>이다.</para>
        /// </summary>
        public int HeroMaxHp { get; set; }

        /// <summary>피격 무적 (초) [소스 — <c>1e3 ms</c>]. 실측 0.95 는 표본 하한이었다.</summary>
        public double HeroInvincibleSeconds { get; set; }

        /// <summary>피격 시 <b>빨강(0xFF0000)</b> 물듦이 지속되는 시간 (초) [소스].</summary>
        public double HeroHitTintSeconds { get; set; }

        /// <summary>적이 맞았을 때 «빨강 물듦» 길이 (초) [소스 <c>hitTintDuration = 200</c>].</summary>
        public double EnemyHitTintSeconds { get; set; }

        // ══════════════════════════════ 액티브 스킬 [소스 — 동작 클래스의 상수]

        /// <summary>돌진이 지속시간 동안 전부 나아가는 거리 [소스 300].</summary>
        public double DashDistance { get; set; }

        /// <summary>돌진을 몇 «칸»으로 쪼개 옮기나 [소스 4].</summary>
        public double DashStepUnits { get; set; }

        /// <summary>돌진 피해 배수 [소스 2].</summary>
        public double DashDamageMultiplier { get; set; }

        /// <summary>돌진이 끝나는 자리에서 미는 반경 [소스 180].</summary>
        public double DashEndKnockbackRadius { get; set; }

        /// <summary>그때 미는 세기 [소스 12].</summary>
        public double DashEndKnockbackForce { get; set; }

        /// <summary>화염 자취 조각 간격 [소스 24].</summary>
        public double TrailSegmentSpacing { get; set; }

        /// <summary>조각이 남아 있는 시간 (초).</summary>
        public double TrailSegmentLifeSeconds { get; set; }

        /// <summary>조각의 판정 반경.</summary>
        public double TrailSegmentRadius { get; set; }

        /// <summary>자취 피해 배수 [소스 0.5].</summary>
        public double TrailDamageMultiplier { get; set; }

        /// <summary>같은 적을 다시 태우기까지 (초) [소스 0.5].</summary>
        public double TrailTargetThrottleSeconds { get; set; }

        /// <summary>섬광 이동이 올리는 시뮬 배율 [소스 2].</summary>
        public double FlashMoveSimulationScale { get; set; }

        /// <summary>마법사 보상 말풍선 (초) [소스 2].</summary>
        public double MageRewardBubbleSeconds { get; set; }

        /// <summary>가족 경고 말풍선 (초) [소스 5].</summary>
        public double FamilyWarningBubbleSeconds { get; set; }

        /// <summary>무적 동안 깜빡이는 주기 (초) [소스].</summary>
        public double HeroBlinkIntervalSeconds { get; set; }

        /// <summary>깜빡일 때의 낮은 쪽 알파 [소스].</summary>
        public double HeroBlinkAlpha { get; set; }

        /// <summary>히어로 충돌 상자 (원본 좌표계 · 발밑 기준) [소스 — <c>(-5,-30,10,30)</c>].</summary>
        public double HeroColliderX { get; set; }

        public double HeroColliderY { get; set; }

        public double HeroColliderW { get; set; }

        public double HeroColliderH { get; set; }

        /// <summary>보석 수집 반경 [소스 — <c>heroCollectRadius = 180</c>].</summary>
        public double CollectRadiusWorld { get; set; }

        // ══════════════════════════════ 사격

        /// <summary>발사 간격 (초) [소스 — <c>350ms / heroBulletFireRate</c>].</summary>
        public double FireIntervalSeconds { get; set; }

        public int FireIntervalSamples { get; set; }

        /// <summary>투사체 피해 [소스 — <c>heroBulletDamage = 2</c>].</summary>
        public int ProjectileDamage { get; set; }

        public int ProjectileDamageSamples { get; set; }

        /// <summary>투사체 속도 (world px/s) [실측].</summary>
        public double ProjectileSpeedWorld { get; set; }

        public int ProjectileSpeedSamples { get; set; }

        /// <summary>총구 오프셋 y [소스 — <c>hero.y - 20</c>].</summary>
        public double MuzzleOffsetY { get; set; }

        // ══════════════════════════════ 적

        /// <summary>적 충돌 상자 [소스 — <c>(-15,-45,30,45)</c>].</summary>
        public double EnemyColliderX { get; set; }

        public double EnemyColliderY { get; set; }

        public double EnemyColliderW { get; set; }

        public double EnemyColliderH { get; set; }

        /// <summary>접촉 공격 간격 (초) [소스].</summary>
        public double EnemyAttackIntervalSeconds { get; set; }

        /// <summary>겹친 «첫» 순간부터 첫 타격까지 (초) [소스 — <c>firstAttackWait = 200ms</c>].</summary>
        public double EnemyFirstAttackWaitSeconds { get; set; }

        /// <summary>접촉 피해 [소스 — <c>hero.hit(1)</c>].</summary>
        public int EnemyContactDamage { get; set; }

        /// <summary>개체마다 곱해지는 속도 흔들림 [소스 — <c>base × (1 + 0.2·rand − 0.1)</c>].</summary>
        public double EnemySpeedJitter { get; set; }

        /// <summary>총알에 맞았을 때 밀리는 세기 [소스].</summary>
        public double EnemyKnockbackStrength { get; set; }

        /// <summary>넉백 감쇠 (프레임당) [소스].</summary>
        public double EnemyKnockbackDecay { get; set; }

        /// <summary>화면 밖에 이만큼 있으면 <b>다시 배치된다</b> (초) [소스].</summary>
        public double EnemyRespawnOutOfViewSeconds { get; set; }

        /// <summary>이 거리 안에서는 더 다가가지 않는다 [소스 — <c>r &gt; 5</c>].</summary>
        public double EnemyChaseDeadZone { get; set; }

        // ══════════════════════════════ 스폰 — «위협 예산» [소스]

        /// <summary>웨이브 하나의 길이 (ms) [소스].</summary>
        public int MsPerWave { get; set; }

        /// <summary>
        /// 첫 판 완화 — 이 웨이브 수까지는 그대로 세고, 넘으면 <b>그만큼 뺀 값</b>을 난이도 웨이브로 쓴다 [소스].
        /// </summary>
        public int OnboardingWaveCount { get; set; }

        public double BaseWaveBoost { get; set; }

        public int SteepWaveInterval { get; set; }

        public double SteepWaveBoost { get; set; }

        /// <summary>스폰 판정 간격의 증가폭 (ms) [소스].</summary>
        public double BaseSpawnIntervalMs { get; set; }

        public double MaxSpawnIntervalMultiplier { get; set; }

        /// <summary>화면 밖 어디에 놓을지 — 화면 사각형에서 이만큼 바깥 [소스].</summary>
        public double SpawnMargin { get; set; }

        public double ThreatBudgetPerFrame { get; set; }

        // ══════════════════════════════ 카메라 [소스]

        /// <summary>
        /// 지수 추적 계수 — <c>n = 1 − (1 − smoothing)^(dt/16.667)</c> 로 히어로를 «뒤따른다».
        /// <para>⚠ 회차 1~8 에는 「리드 25.5」로 적혀 있었다 — <b>앞서 가는 게 아니라 «뒤처지는» 것</b>이다.</para>
        /// </summary>
        public double CameraSmoothing { get; set; }

        /// <summary>화면 중앙에서 아래로 내린 양 [소스 — <c>dHeight/2 + 20</c>].</summary>
        public double CameraOffsetY { get; set; }

        // ══════════════════════════════ 표시 · 단위

        /// <summary>스프라이트 애니 fps [소스 — <c>animationSpeed 0.2 × 60</c>].</summary>
        public double AnimationFps { get; set; }

        /// <summary>지형 타일 한 칸 (world px) [소스 — <c>vh = 24</c>].</summary>
        public int TerrainTileWorld { get; set; }

        /// <summary>설계 뷰포트 세로 (world px) — <b>고정되는 축</b> [실측 · 확정표 3-d].</summary>
        public int DesignViewportHeight { get; set; }

        /// <summary>레벨업에 뜨는 카드 장수 [소스 — <c>Math.min(3, 후보 수)</c>].</summary>
        public int LevelUpCardCount { get; set; }

        /// <summary>사망 시 부활 카운트다운 (초) [소스 — <c>durationMs = 8e3</c>].</summary>
        public int ReviveCountdownSeconds { get; set; }

        /// <summary>죽고 나서 「부활?」이 뜨기까지 — 그동안 적은 계속 움직인다 [소스 — <c>deathDelayMs 1e3</c>].</summary>
        public double DeathDelaySeconds { get; set; }

        /// <summary>부활 체력 [소스 — <c>reviveHealth 4</c>]. 최대 체력이 아니다.</summary>
        public int ReviveHp { get; set; }

        /// <summary>부활 자리 반경 안의 적은 전부 죽는다 [소스 — <c>reviveKillRadius 200</c>].</summary>
        public double ReviveKillRadius { get; set; }

        /// <summary>반경 밖의 적도 이 확률로 죽는다 [소스 — <c>Math.random() &lt; .8</c>].</summary>
        public double ReviveKillOthersChance { get; set; }

        /// <summary>부활 직후 스폰 예산 배율 [소스 — <c>.5</c>] — 회복 시간 동안 1 로 돌아온다.</summary>
        public double ReviveDifficultyMultiplier { get; set; }

        /// <summary>배율이 1 로 돌아오는 시간 (초) [소스 — <c>3e4 ms</c>].</summary>
        public double ReviveDifficultyRecoverySeconds { get; set; }

        public int FrameTicksPerSecond { get; set; }

        // ══════════════════════════════ 과제 — 쓰러진 전사

        /// <summary>전투 시작 뒤 첫 퀘스트(전사)가 «시계»로 시작되기까지 (초) [소스 — <c>introduction {type:"clock", delayMs:2e3}</c>].</summary>
        public double QuestIntroDelaySeconds { get; set; }

        /// <summary>
        /// 퀘스트 NPC 배치 — <b>전부 «히어로 시작 자리» 기준 상대 좌표</b>다 [소스].
        /// <para>⚠ 회차 8 의 실측 절대좌표 (169.3, 2559.3) 는 여기서 유도된다 — 표에 두 벌로 적지 않는다.</para>
        /// </summary>
        public double WarriorOffsetX { get; set; }

        public double WarriorOffsetY { get; set; }

        /// <summary>구조된 뒤 전사가 따라오는 자리 [소스 — <c>hero.x − 110</c>].</summary>
        public double WarriorFollowOffsetX { get; set; }

        /// <summary>
        /// 전사의 추종 «속도» — <b>히어로 이동 속도 스탯에 곱한다</b> [소스 — <c>followSpeedMultiplier .95</c>].
        /// <para>⚠ 원본은 순간이동이 아니다. 0.95 배라 <b>히어로가 달리면 조금씩 벌어지고</b>, 멈추면 따라붙는다.</para>
        /// <para>⚠ 히어로가 이동 속도를 강화하면 <b>전사도 같이 빨라진다</b> [소스 — 스탯을 읽는다].</para>
        /// </summary>
        public double WarriorFollowSpeedMultiplier { get; set; }

        /// <summary>따라오는 자리에 이만큼 가까우면 멈춘다 [소스 — <c>followStopDistance 12</c>].</summary>
        public double WarriorFollowStopDistance { get; set; }

        /// <summary>
        /// 구조된 전사가 <b>쿠나이를 던지는 간격</b> (초) [소스 — <c>attackIntervalMs 1e3</c>].
        /// <para>★ 구조되는 «그 순간» 쿨다운이 이 값으로 채워진다 — 첫 발은 구조 1초 뒤다 [소스].</para>
        /// </summary>
        public double WarriorAttackIntervalSeconds { get; set; }

        /// <summary>쿠나이가 나가는 높이 — 전사 «발» 기준 [소스 — <c>new Pd(this.x, this.y − 28, dir)</c>].</summary>
        public double KunaiSpawnOffsetY { get; set; }

        /// <summary>쿠나이 속도 (월드 px/s) [소스 — <c>moveSpeed .5</c> px/ms].</summary>
        public double KunaiSpeedWorld { get; set; }

        /// <summary>쿠나이 충돌 반경 [소스 — <c>collider = new pn(0,0,9)</c>].</summary>
        public double KunaiRadius { get; set; }

        /// <summary>쿠나이 넉백 세기 [소스 — <c>d.hit(damage, 2, 0xD7D7EA)</c>].</summary>
        public double KunaiKnockback { get; set; }

        // ══════════════════════════════ 전사가 «말한다» [소스 updateThanksBubble · updateQuestIntro]
        //
        // ★★ 원본에서 <b>다음 과제를 여는 것은 전사</b>다 — 앞 과제가 끝나면 곧장 다음이 켜지는 것이 아니라,
        //   전사가 «delay» 만큼 기다렸다가 말풍선을 «bubbleDuration» 동안 띄우고 «그 뒤에» 과제가 시작된다
        //   [소스 el — quest_mage/firstBoss/farmerSheep 의 introduction.type 이 "warrior"].
        //   ⚠ 그동안 currentQuest 는 «비어» 있어 HUD 포인터도 숨는다.

        /// <summary>구조 직후 「고마워, 친구!」 말풍선이 떠 있는 시간 (초) [소스 — <c>thanksBubbleDurationMs 2e3</c>].</summary>
        public double WarriorThanksBubbleSeconds { get; set; }

        /// <summary>마법사 과제 예고 — 기다림 (초) [소스 — <c>delayMs 5e3</c>].</summary>
        public double QuestIntroMageDelaySeconds { get; set; }

        /// <summary>마법사 과제 예고 — 말풍선 (초) [소스 — <c>bubbleDurationMs 4e3</c> · <c>iHearSomebodyScreaming</c>].</summary>
        public double QuestIntroMageBubbleSeconds { get; set; }

        /// <summary>첫 보스 과제 예고 — 기다림 (초) [소스 — <c>delayMs 1e4</c>].</summary>
        public double QuestIntroFirstBossDelaySeconds { get; set; }

        /// <summary>첫 보스 과제 예고 — 말풍선 (초) [소스 — <c>3e3</c> · <c>iFeelSomeDarkEnergy</c>].</summary>
        public double QuestIntroFirstBossBubbleSeconds { get; set; }

        /// <summary>농부/양 과제 예고 — 기다림 (초) [소스 — <c>delayMs 1e4</c>].</summary>
        public double QuestIntroFarmerDelaySeconds { get; set; }

        /// <summary>농부/양 과제 예고 — 말풍선 (초) [소스 — <c>3e3</c> · <c>whatTheAnimals</c>].</summary>
        public double QuestIntroFarmerBubbleSeconds { get; set; }

        public double MageOffsetX { get; set; }

        public double MageOffsetY { get; set; }

        /// <summary>이 반경 안에 들어가면 마법사가 말을 건다 [소스 — 250].</summary>
        public double MageMeetRadius { get; set; }

        /// <summary>아케인 조각 두 개의 자리 — <b>마법사 기준</b> [소스].</summary>
        public double Fragment1OffsetX { get; set; }

        public double Fragment1OffsetY { get; set; }

        public double Fragment2OffsetX { get; set; }

        public double Fragment2OffsetY { get; set; }

        /// <summary>조각을 줍는 반경 [소스 — <c>heroBindRadius 75</c>].</summary>
        public double FragmentPickupRadius { get; set; }

        /// <summary>마법사에게 건네지는 반경 [소스 — <c>mageBindRadius 150</c>].</summary>
        public double FragmentDeliverRadius { get; set; }

        /// <summary>마법사 퀘스트를 끝내면 받는 번개 수 [소스 — <c>increaseLightnings(2)</c>].</summary>
        public int LightningRewardCount { get; set; }

        // ══════════════════════════════ 퀘스트 3 — 첫 보스 [소스 · questManager · darkSoul]

        public double FamilyOffsetX { get; set; }

        public double FamilyOffsetY { get; set; }

        /// <summary>가족에게 이만큼 다가가면 보스가 소환된다 [소스 — 75].</summary>
        public double FamilyTriggerRadius { get; set; }

        /// <summary>소환 대기 (초) [소스 — 3000ms].</summary>
        public double DarkSoulSummonSeconds { get; set; }

        /// <summary>보스 체력 = 이 값 × <b>지금 총알 피해</b> [소스 — <c>300 × heroBulletDamage</c>].</summary>
        public int BossHpPerDamage { get; set; }

        public double BossMoveSpeedWorld { get; set; }

        public double BossColliderX { get; set; }

        public double BossColliderY { get; set; }

        public double BossColliderW { get; set; }

        public double BossColliderH { get; set; }

        public double BossAttackIntervalSeconds { get; set; }

        public double BossFireballIntervalSeconds { get; set; }

        public double BossAttackAnimSeconds { get; set; }

        /// <summary>한 번 공격에 쏘는 불덩이 수 [소스 — <c>attackBurstRemaining = 3</c>].</summary>
        public int BossFireballBurst { get; set; }

        public double BossChaseDeadZone { get; set; }

        public double BossKnockbackDecay { get; set; }

        /// <summary>보스를 잡으면 쏟아지는 보석 수와 개당 XP [소스].</summary>
        public int BossGemFountainCount { get; set; }

        public int BossGemXp { get; set; }

        // ══════════════════════════════ 불덩이 [소스 — wd]

        public double FireballSpeedWorld { get; set; }

        public double FireballRadius { get; set; }

        public double FireballMaxDistance { get; set; }

        public int FireballDamage { get; set; }

        // ══════════════════════════════ 퀘스트 4 — 농부 · 양 [소스]

        public double FarmerOffsetX { get; set; }

        public double FarmerOffsetY { get; set; }

        public int SheepCount { get; set; }

        /// <summary>농부가 말을 거는 반경 [소스 — <c>meetHeroRadius 100</c>].</summary>
        public double FarmerMeetRadius { get; set; }

        /// <summary>양을 데려오는 반경 — 조각과 같은 값으로 둔다(양 클래스의 반경은 못 읽었다 · 등재).</summary>
        public double SheepPickupRadius { get; set; }

        /// <summary>양 다섯 마리의 자리 — <b>농부 기준</b> [소스 <c>onFarmerMet</c>].</summary>
        public double Sheep0OffsetX { get; set; }
        public double Sheep0OffsetY { get; set; }
        public double Sheep1OffsetX { get; set; }
        public double Sheep1OffsetY { get; set; }
        public double Sheep2OffsetX { get; set; }
        public double Sheep2OffsetY { get; set; }
        public double Sheep3OffsetX { get; set; }
        public double Sheep3OffsetY { get; set; }
        public double Sheep4OffsetX { get; set; }
        public double Sheep4OffsetY { get; set; }

        // ══════════════════════════════ 클리어 뒤 «양 조우» [소스 — sheepGenerator]

        /// <summary>45초마다 화면 밖에 양 한 마리와 우리가 생긴다 [소스].</summary>
        public double EncounterIntervalSeconds { get; set; }

        public double EncounterSheepMinOffscreen { get; set; }

        public double EncounterSheepMaxOffscreen { get; set; }

        public double EncounterFoldMinDistance { get; set; }

        public double EncounterFoldMaxDistance { get; set; }

        public double EncounterThanksSeconds { get; set; }

        /// <summary>보상 보석 수 — 개당 XP 는 <c>ceil(다음 레벨 목표 / 3)</c> [소스].</summary>
        public int EncounterRewardGems { get; set; }

        public int FarmerGemFountainCount { get; set; }

        public int FarmerGemXp { get; set; }

        /// <summary>보석 분수가 한 개씩 나오는 간격 (초) [소스 — 35ms].</summary>
        public double GemFountainIntervalSeconds { get; set; }

        /// <summary>NPC 가 「고맙다」를 띄우는 시간 (초) [소스 — 3000ms].</summary>
        public double NpcThanksSeconds { get; set; }

        // ══════════════════════════════ 모닥불 [소스 — livesInStock 3]

        public int FireplaceLives { get; set; }

        public int FireplaceHealAmount { get; set; }

        /// <summary>회복 반경 [소스 — 충돌 원 <c>r = 55</c>].</summary>
        public double FireplaceRadius { get; set; }

        /// <summary>한 번 회복하고 다음까지 (초) [소스 — 700ms].</summary>
        public double FireplaceHealCooldownSeconds { get; set; }

        /// <summary>「화염 소진!」 재표시 간격 (초) [소스 <c>fireOutCooldownDurationMs 900</c>].</summary>
        public double FireOutCooldownSeconds { get; set; }

        /// <summary>모닥불 판정 원의 세로 중심 어긋남 (원본 px) [소스 <c>collider(0, −8, 55)</c>].</summary>
        public double FireplaceColliderOffsetY { get; set; }

        public int QuestMeterPixels { get; set; }

        // ══════════════════════════════ 월드 오브젝트 — 지형이 놓는다 [소스 class ld · Wl · updateMediumObjects]

        /// <summary>오브젝트 청크 한 변 (칸) [소스 — <c>mediumObjectChunkSize = healingObjectChunkSize = 24</c>].</summary>
        public int ObjectChunkTiles { get; set; }

        /// <summary>청크 원점의 노이즈가 이 값을 넘으면 나무 하나 [소스 — <c>&gt; .4</c>].</summary>
        public double TreeThreshold { get; set; }

        /// <summary>나무 발자국 (칸) [소스 — 4×2 · 통행 불가].</summary>
        public int TreeWidthTiles { get; set; }

        public int TreeHeightTiles { get; set; }

        /// <summary>나무 자리 = 칸 x×24 + 이 값, y×24 [소스 — <c>x = tileX*24 + 48</c>].</summary>
        public double TreeAnchorOffsetX { get; set; }

        /// <summary>히어로가 이 안이면 충전 [소스 — <c>triggerRadius = 150</c>].</summary>
        public double TreeTriggerRadius { get; set; }

        /// <summary>다 차는 데 걸리는 시간 (초) [소스 — <c>chargeDurationMs = 1250</c>].</summary>
        public double TreeChargeSeconds { get; set; }

        /// <summary>밖에 있으면 이 배로 식는다 [소스 — <c>−1.5·dt</c>].</summary>
        public double TreeChargeDecayMultiplier { get; set; }

        /// <summary>대기 흔들림 각속도 (rad/ms) [소스 — <c>scaleSpeed = .004</c>].</summary>
        public double TreeIdleSpeed { get; set; }

        /// <summary>터질 때 뿜는 유성 수 [소스 — 6 · 원형 등간격].</summary>
        public int TreeBurstMeteorCount { get; set; }

        /// <summary>유성 속도 (world px/s) [소스 — <c>moveSpeed = .5 px/ms</c>].</summary>
        public double MeteorSpeedWorld { get; set; }

        /// <summary>유성 판정 원 반지름 [소스 — <c>collider r = 40</c>].</summary>
        public double MeteorRadius { get; set; }

        /// <summary>유성 피해 = 총알 피해 × 이 값 [소스 — <c>2 * heroBulletDamage</c>].</summary>
        public double MeteorDamageMultiplier { get; set; }

        public double MeteorKnockback { get; set; }

        /// <summary>청크 원점의 노이즈가 이 값을 넘으면 모닥불 하나 [소스 — <c>&gt; .3</c>].</summary>
        public double FireplaceThreshold { get; set; }

        /// <summary>모닥불 자리 = 칸×24 + 이 값 [소스 — <c>+12</c>].</summary>
        public double FireplaceAnchorOffset { get; set; }

        /// <summary>나무가 터질 때 카메라 흔들림 세기 (px) · 시간 (초) [소스 — <c>triggerShake(10, 300)</c>].</summary>
        public double CameraShakeAmplitude { get; set; }

        public double CameraShakeSeconds { get; set; }

        /// <summary>이 반경 안에 머물면 구조가 진행된다 [소스 — <c>rescueRadius = 150</c>].</summary>
        public double QuestRescueRadius { get; set; }

        /// <summary>구조에 걸리는 시간 (초) [소스 — <c>rescueDurationMs = 4e3</c>].</summary>
        public double QuestRescueSeconds { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
