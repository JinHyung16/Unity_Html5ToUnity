using System;
using System.Collections.Generic;
using JinHyung.Core;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// <b>퀘스트 체인</b> — 전사 → 마법사 → 첫 보스 → 농부/양 [소스 직독 · <c>questManager</c>].
    ///
    /// <para>
    /// ★ 원본은 퀘스트를 <b>하나씩 순차로</b> 돌린다. 앞이 끝나야 다음 NPC 가 나오고,
    /// HUD 의 포인터도 <b>지금 퀘스트의 대상</b>을 가리킨다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>보스전 동안 «적 생성이 멈춘다»</b> [소스 — <c>enemyGenerator.setPaused(true)</c>].
    /// 이 한 줄이 없으면 보스전이 전혀 다른 난이도가 된다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 전투 규칙과 달리 <b>NPC 자리는 전부 «히어로 시작 기준 상대 좌표»</b>다 — 표에 절대값을 적지 않는다.
    /// </para>
    /// </summary>
    public sealed partial class UndeadSimulation
    {
        private const int MaxFireballs = 64;

        /// <summary>첫 보스 퀘스트의 단계 [소스 — <c>firstBossPhase</c>].</summary>
        public enum EBossPhase
        {
            WaitForHero,
            Summoning,
            Fight,
            Rewarding,
            Leaving,
            Done,
        }

        /// <summary>농부 퀘스트의 단계 [소스 — <c>farmerSheepPhase</c>].</summary>
        public enum EFarmerPhase
        {
            WaitForHero,
            Collecting,
            Thanks,
            Leaving,
            Done,
        }

        public struct Fireball
        {
            public bool Active;
            public UndeadVec2 Position;
            public UndeadVec2 Velocity;
            public UndeadVec2 Origin;
            public double AnimFrame;
        }

        /// <summary>양 한 마리 — 0 놓여 있음 · 1 따라옴 · 2 농부에게 전달.</summary>
        public struct Sheep
        {
            public int State;
            public UndeadVec2 Position;
        }

        private readonly Fireball[] _fireballs = new Fireball[MaxFireballs];
        private Sheep[] _sheep = Array.Empty<Sheep>();

        private double _bossAttackCooldown;
        private double _bossAnimRemaining;
        private int _bossBurstLeft;
        private double _bossFireballTimer;
        private double _npcThanksRemaining;

        // ── 클리어 뒤 «양 조우» [소스 sheepGenerator]
        private double _encounterTimer;
        private double _encounterThanks;

        private int _fountainLeft;
        private double _fountainTimer;
        private int _fountainXp;
        private UndeadVec2 _fountainOrigin;

        // ══════════════════════════════ 상태 (공개)

        /// <summary>구조가 얼마나 진행됐나 (초).</summary>
        public double RescueSeconds { get; private set; }

        public bool QuestRescued { get; private set; }

        public EUndeadQuest CurrentQuest { get; private set; }

        /// <summary>첫 퀘스트가 «시계»로 시작되기까지 남은 시간 [소스 — 전투 시작 2초 뒤 · 그때 전사가 놓인다].</summary>
        public double QuestIntroRemaining { get; private set; }

        /// <summary>퀘스트가 하나라도 «켜졌나» — 포인터·전사는 그 뒤에만 보인다 [소스 — <c>currentQuest === undefined</c> 면 숨김].</summary>
        public bool QuestActive { get { return CurrentQuest != EUndeadQuest.None; } }

        public bool MageMet { get; private set; }

        public bool MageCompleted { get; private set; }

        /// <summary>조각 상태 — 0 놓여 있음 · 1 히어로가 듦 · 2 마법사에게 전달.</summary>
        public int Fragment0State { get; private set; }

        public int Fragment1State { get; private set; }

        public UndeadVec2 WarriorPosition { get; private set; }

        public UndeadVec2 MagePosition { get; private set; }

        public UndeadVec2 Fragment0Position { get; private set; }

        public UndeadVec2 Fragment1Position { get; private set; }

        public UndeadVec2 FamilyPosition { get; private set; }

        public EBossPhase BossPhase { get; private set; }

        public double SummonRemaining { get; private set; }

        public bool BossActive { get; private set; }

        public UndeadVec2 BossPosition { get; private set; }

        public int BossHp { get; private set; }

        public int BossMaxHp { get; private set; }

        public double BossAnimFrame { get; private set; }

        public UndeadVec2 FarmerPosition { get; private set; }

        public EFarmerPhase FarmerPhase { get; private set; }

        public IReadOnlyList<Sheep> Sheep_ { get { return _sheep; } }

        public IReadOnlyList<Fireball> Fireballs { get { return _fireballs; } }

        public int ActiveFireballCount { get; private set; }

        /// <summary>모닥불 — 남은 회복 횟수와 자리 [소스 — <c>livesInStock</c>].</summary>


        /// <summary>보스전 동안 <b>적 생성이 멈춘다</b> [소스].</summary>
        public bool SpawnPaused { get; private set; }

        /// <summary>퀘스트를 전부 마쳤나 — <b>「한 판」의 클리어 조건</b>이다.</summary>
        public bool RunCleared { get; private set; }

        /// <summary>클리어 뒤 양 조우 — 0 없음 · 1 진행 · 2 감사 [소스 sheepGenerator].</summary>
        public int EncounterPhase { get; private set; }

        public UndeadVec2 EncounterSheepPosition { get; private set; }

        public UndeadVec2 EncounterFoldPosition { get; private set; }

        public bool EncounterSheepFollowing { get; private set; }

        /// <summary>조우 보상 XP 의 뿌리 — «다음 레벨 목표». 배선이 레벨업마다 넣는다 [소스 hl(level+1)].</summary>
        public int NextGoalForReward { get; set; }

        public event Action OnQuestRescued;

        public event Action OnMageCompleted;

        public event Action OnBossDefeated;

        public event Action OnRunCleared;

        /// <summary>회복했다 — 배선이 <c>+N</c> 팝업을 띄운다 [소스].</summary>
        public event Action<UndeadVec2, int> OnHeroHealed;

        /// <summary>모닥불에 닿았는데 <b>체력이 만땅</b>이다 — 「최대 체력」 [소스 <c>showFullHealthEffect</c>].</summary>
        public event Action<UndeadVec2> OnFireplaceFullHealth;

        /// <summary>모닥불에 닿았는데 <b>재고가 0</b>이다 — 「화염 소진!」 [소스 <c>showFireOutEffect</c>].</summary>
        public event Action<UndeadVec2> OnFireplaceOut;

        // ══════════════════════════════ 포인터

        /// <summary>
        /// 포인터가 가리키는 곳 — <b>지금 퀘스트의 대상</b>이다 [소스 <c>getCurrentQuestTarget</c>].
        /// </summary>
        public UndeadVec2 QuestTarget
        {
            get
            {
                switch (CurrentQuest)
                {
                    case EUndeadQuest.Warrior:
                        return WarriorPosition;

                    case EUndeadQuest.Mage:
                        if (MageMet == false)
                            return MagePosition;

                        if (Fragment0State == 0)
                            return Fragment0Position;

                        if (Fragment1State == 0)
                            return Fragment1Position;

                        return MagePosition;

                    case EUndeadQuest.FirstBoss:
                        return BossActive ? BossPosition : FamilyPosition;

                    case EUndeadQuest.FarmerSheep:
                        // ⚠ 농부를 «만나기 전»에는 양 자리가 아직 없다 — 그때 (0,0) 을 가리키면
                        //   봇도 사람도 «원점»으로 걸어간다. [사고] 실제로 완주가 안 됐다.
                        if (FarmerPhase == EFarmerPhase.WaitForHero)
                            return FarmerPosition;

                        for (int i = 0; i < _sheep.Length; i++)
                        {
                            if (_sheep[i].State == 0)
                                return _sheep[i].Position;
                        }

                        return FarmerPosition;

                    default:
                        return HeroPosition;
                }
            }
        }

        // ══════════════════════════════ 준비

        private void ResetQuest(UndeadVec2 heroPosition)
        {
            Array.Clear(_fireballs, 0, _fireballs.Length);

            RescueSeconds = 0.0;
            QuestRescued = false;
            CurrentQuest = EUndeadQuest.None;
            QuestIntroRemaining = _config.QuestIntroDelaySeconds;
            MageMet = false;
            MageCompleted = false;
            Fragment0State = 0;
            Fragment1State = 0;

            BossPhase = EBossPhase.WaitForHero;
            SummonRemaining = 0.0;
            BossActive = false;
            BossHp = 0;
            BossMaxHp = 0;
            BossAnimFrame = 0.0;
            _bossAttackCooldown = 0.0;
            _bossAnimRemaining = 0.0;
            _bossBurstLeft = 0;
            _bossFireballTimer = 0.0;

            FarmerPhase = EFarmerPhase.WaitForHero;
            _npcThanksRemaining = 0.0;
            _fountainLeft = 0;
            _fountainTimer = 0.0;
            SpawnPaused = false;
            RunCleared = false;
            EncounterPhase = 0;
            _encounterTimer = 0.0;
            ActiveFireballCount = 0;

            // ★ NPC 자리는 «히어로 시작 기준 상대»다 [소스].
            WarriorPosition = heroPosition + _config.WarriorOffset;
            MagePosition = heroPosition + _config.MageOffset;
            Fragment0Position = MagePosition + _config.Fragment1Offset;
            Fragment1Position = MagePosition + _config.Fragment2Offset;
            FamilyPosition = heroPosition + _config.FamilyOffset;
            FarmerPosition = heroPosition + _config.FarmerOffset;

            // 모닥불·나무는 «지형이 놓는다» — UndeadSimulation.World (청크 노이즈)

            // 양은 농부를 만나면 «그때» 자리가 정해진다 [소스 — 조각과 같은 방식].
            _sheep = new Sheep[Math.Max(0, _config.SheepCount)];
        }

        // ══════════════════════════════ 한 프레임

        private void StepQuest(double dt)
        {
            StepFireballs(dt);
            StepFountain(dt);
            StepEncounter(dt);

            switch (CurrentQuest)
            {
                case EUndeadQuest.None:
                    // 시계 퀘스트 — 전투 시작 2초 뒤 전사가 «그때 히어로 기준»으로 놓인다 [소스 spawnWarrior — hero.x−1500, hero.y+1500]
                    QuestIntroRemaining -= dt;

                    if (QuestIntroRemaining <= 0.0)
                    {
                        QuestIntroRemaining = 0.0;
                        WarriorPosition = HeroPosition + _config.WarriorOffset;
                        CurrentQuest = EUndeadQuest.Warrior;
                    }

                    break;

                case EUndeadQuest.Warrior:
                    StepWarrior(dt);
                    break;

                case EUndeadQuest.Mage:
                    FollowWarrior();
                    StepMage();
                    break;

                case EUndeadQuest.FirstBoss:
                    FollowWarrior();
                    StepFirstBoss(dt);
                    break;

                case EUndeadQuest.FarmerSheep:
                    FollowWarrior();
                    StepFarmer(dt);
                    break;
            }
        }

        /// <summary>구조된 전사는 히어로를 따라다닌다 [소스 — <c>followOffsetX −110</c>].</summary>
        private void FollowWarrior()
        {
            WarriorPosition = new UndeadVec2(HeroPosition.X + _config.WarriorFollowOffsetX, HeroPosition.Y);
        }

        // ══════════════════════════════ 퀘스트 1 — 전사

        private void StepWarrior(double dt)
        {
            bool inside = (WarriorPosition - HeroPosition).Magnitude <= _config.QuestRescueRadius;
            RescueSeconds = inside ? Math.Min(_config.QuestRescueSeconds, RescueSeconds + dt) : 0.0;

            if (inside == false || RescueSeconds < _config.QuestRescueSeconds)
                return;

            QuestRescued = true;
            CurrentQuest = EUndeadQuest.Mage;
            OnQuestRescued?.Invoke();
        }

        // ══════════════════════════════ 퀘스트 2 — 마법사

        private void StepMage()
        {
            double toMage = (MagePosition - HeroPosition).Magnitude;

            if (MageMet == false)
            {
                if (toMage > _config.MageMeetRadius)
                    return;

                MageMet = true;
                return;
            }

            if (Fragment0State == 0 && (Fragment0Position - HeroPosition).Magnitude <= _config.FragmentPickupRadius)
                Fragment0State = 1;

            if (Fragment1State == 0 && (Fragment1Position - HeroPosition).Magnitude <= _config.FragmentPickupRadius)
                Fragment1State = 1;

            if (Fragment0State == 1)
                Fragment0Position = new UndeadVec2(HeroPosition.X - 18.0, HeroPosition.Y - 112.0);

            if (Fragment1State == 1)
                Fragment1Position = new UndeadVec2(HeroPosition.X + 18.0, HeroPosition.Y - 112.0);

            if (toMage <= _config.FragmentDeliverRadius)
            {
                if (Fragment0State == 1)
                {
                    Fragment0State = 2;
                    Fragment0Position = new UndeadVec2(MagePosition.X - 18.0, MagePosition.Y - 112.0);
                }

                if (Fragment1State == 1)
                {
                    Fragment1State = 2;
                    Fragment1Position = new UndeadVec2(MagePosition.X + 18.0, MagePosition.Y - 112.0);
                }
            }

            if (Fragment0State != 2 || Fragment1State != 2)
                return;

            MageCompleted = true;
            CurrentQuest = EUndeadQuest.FirstBoss;
            LightningCount += _config.LightningRewardCount;
            OnMageCompleted?.Invoke();
        }

        // ══════════════════════════════ 퀘스트 3 — 첫 보스

        private void StepFirstBoss(double dt)
        {
            switch (BossPhase)
            {
                case EBossPhase.WaitForHero:
                    if ((FamilyPosition - HeroPosition).Magnitude > _config.FamilyTriggerRadius)
                        return;

                    // ★ 가족에게 닿으면 소환이 시작되고 «적 생성이 멈춘다» [소스]
                    BossPhase = EBossPhase.Summoning;
                    SummonRemaining = _config.DarkSoulSummonSeconds;
                    SpawnPaused = true;
                    return;

                case EBossPhase.Summoning:
                    SummonRemaining -= dt;

                    if (SummonRemaining > 0.0)
                        return;

                    SpawnBoss();
                    BossPhase = EBossPhase.Fight;
                    return;

                case EBossPhase.Fight:
                    StepBoss(dt);
                    return;

                case EBossPhase.Rewarding:
                    if (_fountainLeft > 0)
                        return;

                    // 분수가 끝나면 적이 다시 나오고 가족이 인사한다 [소스]
                    SpawnPaused = false;
                    _npcThanksRemaining = _config.NpcThanksSeconds;
                    BossPhase = EBossPhase.Leaving;
                    return;

                case EBossPhase.Leaving:
                    _npcThanksRemaining -= dt;

                    if (_npcThanksRemaining > 0.0)
                        return;

                    BossPhase = EBossPhase.Done;
                    CurrentQuest = EUndeadQuest.FarmerSheep;
                    return;
            }
        }

        /// <summary>
        /// 보스를 세운다. ★ <b>체력이 «지금 총알 피해»에 비례</b>한다 [소스 — <c>300 × heroBulletDamage</c>] —
        /// 그래서 업그레이드를 많이 먹어도 보스전 길이가 비슷하게 유지된다.
        /// </summary>
        private void SpawnBoss()
        {
            BossActive = true;
            BossMaxHp = Math.Max(1, _config.BossHpPerDamage * DamageStat);
            BossHp = BossMaxHp;
            BossPosition = FamilyPosition;
            BossAnimFrame = 0.0;
            _bossAttackCooldown = _config.BossAttackInterval;
            _bossAnimRemaining = 0.0;
            _bossBurstLeft = 0;
        }

        private void StepBoss(double dt)
        {
            if (BossActive == false)
                return;

            BossAnimFrame += _config.AnimationFps * dt;

            UndeadVec2 toHero = HeroPosition - BossPosition;
            double distance = toHero.Magnitude;

            if (distance > _config.BossChaseDeadZone)
            {
                double step = _config.BossMoveSpeed * dt;
                BossPosition = new UndeadVec2(BossPosition.X + toHero.X / distance * step,
                                              BossPosition.Y + toHero.Y / distance * step);
            }

            // ── 공격 : 주기마다 «연사»로 불덩이를 뿌린다 [소스]
            if (_bossBurstLeft > 0)
            {
                _bossFireballTimer -= dt;

                if (_bossFireballTimer <= 0.0)
                {
                    FireFireball();
                    _bossBurstLeft--;
                    _bossFireballTimer = _config.BossFireballInterval;
                }
            }
            else
            {
                _bossAttackCooldown -= dt;

                if (_bossAttackCooldown <= 0.0)
                {
                    _bossAttackCooldown = _config.BossAttackInterval;
                    _bossBurstLeft = Math.Max(1, _config.BossFireballBurst);
                    _bossFireballTimer = 0.0;
                    _bossAnimRemaining = _config.BossAttackAnimSeconds;
                }
            }

            if (_bossAnimRemaining > 0.0)
                _bossAnimRemaining -= dt;
        }

        private void FireFireball()
        {
            UndeadVec2 delta = HeroPosition - BossPosition;
            double length = delta.Magnitude;

            if (length <= 0.0)
                return;

            for (int i = 0; i < _fireballs.Length; i++)
            {
                if (_fireballs[i].Active)
                    continue;

                _fireballs[i] = new Fireball
                {
                    Active = true,
                    Position = BossPosition,
                    Origin = BossPosition,
                    Velocity = new UndeadVec2(delta.X / length * _config.FireballSpeed,
                                              delta.Y / length * _config.FireballSpeed),
                };

                return;
            }
        }

        private void StepFireballs(double dt)
        {
            int active = 0;

            for (int i = 0; i < _fireballs.Length; i++)
            {
                if (_fireballs[i].Active == false)
                    continue;

                _fireballs[i].Position = new UndeadVec2(
                    _fireballs[i].Position.X + _fireballs[i].Velocity.X * dt,
                    _fireballs[i].Position.Y + _fireballs[i].Velocity.Y * dt);

                _fireballs[i].AnimFrame += _config.AnimationFps * dt;

                // 히어로에게 맞으면 사라진다 — 원 대 사각형이다.
                if (CircleHitsHero(_fireballs[i].Position, _config.FireballRadius))
                {
                    HitHero(_config.FireballDamage);
                    _fireballs[i].Active = false;
                    continue;
                }

                if ((_fireballs[i].Position - _fireballs[i].Origin).Magnitude > _config.FireballMaxDistance)
                {
                    _fireballs[i].Active = false;
                    continue;
                }

                active++;
            }

            ActiveFireballCount = active;
        }

        /// <summary>
        /// 보스를 때린다 — 총알·번개가 부른다.
        /// <para>★ <b>맞은 만큼 보석이 떨어진다</b> [소스 — <c>dropDamageGem</c>].</para>
        /// </summary>
        private void DamageBoss(int damage)
        {
            if (BossActive == false)
                return;

            int applied = Math.Min(BossHp, damage);
            BossHp -= applied;
            SpawnGem(BossPosition, applied);

            if (BossHp > 0)
                return;

            BossActive = false;
            BossPhase = EBossPhase.Rewarding;
            StartFountain(BossPosition, _config.BossGemFountainCount, _config.BossGemXp);
            OnBossDefeated?.Invoke();
        }

        // ══════════════════════════════ 퀘스트 4 — 농부 · 양

        private void StepFarmer(double dt)
        {
            switch (FarmerPhase)
            {
                case EFarmerPhase.WaitForHero:
                    if ((FarmerPosition - HeroPosition).Magnitude > _config.FarmerMeetRadius)
                        return;

                    // 만나면 양 자리가 정해진다 [소스 — 조각과 같은 방식]
                    PlaceSheep();
                    FarmerPhase = EFarmerPhase.Collecting;
                    return;

                case EFarmerPhase.Collecting:
                    StepSheep();
                    return;

                case EFarmerPhase.Thanks:
                    _npcThanksRemaining -= dt;

                    if (_npcThanksRemaining > 0.0)
                        return;

                    StartFountain(FarmerPosition, _config.FarmerGemFountainCount, _config.FarmerGemXp);
                    FarmerPhase = EFarmerPhase.Leaving;
                    return;

                case EFarmerPhase.Leaving:
                    if (_fountainLeft > 0)
                        return;

                    FarmerPhase = EFarmerPhase.Done;
                    CurrentQuest = EUndeadQuest.Done;
                    RunCleared = true;
                    OnRunCleared?.Invoke();
                    return;
            }
        }

        /// <summary>양 다섯 마리를 «농부 기준 저작 상수» 자리에 놓는다 [소스 onFarmerMet].</summary>
        private void PlaceSheep()
        {
            for (int i = 0; i < _sheep.Length; i++)
            {
                UndeadVec2 offset = _config.SheepOffsets != null && i < _config.SheepOffsets.Length
                    ? _config.SheepOffsets[i]
                    : UndeadVec2.Zero;

                _sheep[i] = new Sheep { State = 0, Position = FarmerPosition + offset };
            }
        }

        private void StepSheep()
        {
            int delivered = 0;
            double toFarmer = (FarmerPosition - HeroPosition).Magnitude;

            for (int i = 0; i < _sheep.Length; i++)
            {
                if (_sheep[i].State == 0
                    && (_sheep[i].Position - HeroPosition).Magnitude <= _config.SheepPickupRadius)
                {
                    _sheep[i].State = 1;
                }

                if (_sheep[i].State == 1)
                {
                    // 따라오는 양은 히어로 뒤에 늘어선다
                    double offset = 40.0 + i * 26.0;
                    _sheep[i].Position = new UndeadVec2(HeroPosition.X - offset, HeroPosition.Y + 12.0);

                    if (toFarmer <= _config.FarmerMeetRadius)
                    {
                        _sheep[i].State = 2;
                        _sheep[i].Position = new UndeadVec2(FarmerPosition.X + (i - 2) * 40.0,
                                                            FarmerPosition.Y + 60.0);
                    }
                }

                if (_sheep[i].State == 2)
                    delivered++;
            }

            if (delivered < _sheep.Length)
                return;

            _npcThanksRemaining = _config.NpcThanksSeconds;
            FarmerPhase = EFarmerPhase.Thanks;
        }

        // ══════════════════════════════ 보석 분수 · 모닥불

        /// <summary>보상 보석을 <b>한 개씩 35ms 간격으로</b> 쏟는다 [소스 — <c>gemFountainIntervalMs</c>].</summary>
        private void StartFountain(UndeadVec2 origin, int count, int xp)
        {
            _fountainOrigin = origin;
            _fountainLeft = count;
            _fountainXp = xp;
            _fountainTimer = 0.0;
        }

        private void StepFountain(double dt)
        {
            if (_fountainLeft <= 0)
                return;

            _fountainTimer -= dt;

            if (_fountainTimer > 0.0)
                return;

            _fountainTimer = _config.GemFountainInterval;
            _fountainLeft--;

            double angle = RandomUtil.Value() * Math.PI * 2.0;
            double radius = RandomUtil.Value() * 120.0;
            SpawnGem(new UndeadVec2(_fountainOrigin.X + Math.Cos(angle) * radius,
                                    _fountainOrigin.Y + Math.Sin(angle) * radius), _fountainXp);
        }

        /// <summary>
        /// 클리어 뒤 «양 조우» [소스 sheepGenerator] — 45초마다 화면 밖에 양 하나와 우리가 생긴다.
        /// 양을 우리에 데려가면 1초 뒤 빨간 보석 3개(개당 ceil(다음 목표/3)).
        /// <para>진행 중인 조우가 «화면에 보이는» 채로 다음 타이머가 오면 새로 만들지 않는다 [소스].</para>
        /// </summary>
        private void StepEncounter(double dt)
        {
            if (RunCleared == false)
                return;

            _encounterTimer -= dt;

            if (EncounterPhase == 2)
            {
                _encounterThanks -= dt;

                if (_encounterThanks > 0.0)
                    return;

                int xp = (int)Math.Ceiling(NextGoalForReward / 3.0);

                for (int i = 0; i < _config.EncounterRewardGems; i++)
                {
                    double angle = i / (double)_config.EncounterRewardGems * Math.PI * 2.0 - Math.PI / 2.0;
                    SpawnGem(new UndeadVec2(EncounterFoldPosition.X + 45.0 * Math.Cos(angle),
                                            EncounterFoldPosition.Y - 70.0 + 20.0 * Math.Sin(angle)), xp);
                }

                EncounterPhase = 0;
                return;
            }

            if (EncounterPhase == 1)
            {
                if (EncounterSheepFollowing == false
                    && (EncounterSheepPosition - HeroPosition).Magnitude <= _config.SheepPickupRadius)
                {
                    EncounterSheepFollowing = true;
                }

                if (EncounterSheepFollowing)
                {
                    EncounterSheepPosition = new UndeadVec2(HeroPosition.X - 40.0, HeroPosition.Y + 12.0);

                    if ((EncounterFoldPosition - HeroPosition).Magnitude <= _config.FarmerMeetRadius)
                    {
                        EncounterPhase = 2;
                        _encounterThanks = _config.EncounterThanksSeconds;
                        _encounterTimer = _config.EncounterInterval;
                        return;
                    }
                }
            }

            if (_encounterTimer > 0.0)
                return;

            _encounterTimer = _config.EncounterInterval;

            if (EncounterPhase == 1 && (IsInView(EncounterSheepPosition) || IsInView(EncounterFoldPosition)))
                return;

            double dir = RandomUtil.Value() * Math.PI * 2.0;
            double halfW = _config.ViewportWidth * 0.5 + 100.0;
            double halfH = _config.ViewportHeight * 0.5 + 100.0;
            double edge = Math.Min(halfW / Math.Max(Math.Abs(Math.Cos(dir)), 0.001),
                                   halfH / Math.Max(Math.Abs(Math.Sin(dir)), 0.001));
            double sheepDistance = edge + _config.EncounterSheepMinOffscreen
                                   + RandomUtil.Value() * (_config.EncounterSheepMaxOffscreen - _config.EncounterSheepMinOffscreen);
            double foldDistance = sheepDistance + _config.EncounterFoldMinDistance
                                  + RandomUtil.Value() * (_config.EncounterFoldMaxDistance - _config.EncounterFoldMinDistance);

            EncounterSheepPosition = new UndeadVec2(CameraPivot.X + Math.Cos(dir) * sheepDistance,
                                                    CameraPivot.Y + Math.Sin(dir) * sheepDistance);
            EncounterFoldPosition = new UndeadVec2(CameraPivot.X + Math.Cos(dir) * foldDistance,
                                                   CameraPivot.Y + Math.Sin(dir) * foldDistance);
            EncounterSheepFollowing = false;
            EncounterPhase = 1;
        }

        /// <summary>불덩이는 <b>원</b>이고 히어로는 <b>사각형</b>이다.</summary>
        private bool CircleHitsHero(UndeadVec2 center, double radius)
        {
            double hx = HeroPosition.X + _config.HeroColliderX;
            double hy = HeroPosition.Y + _config.HeroColliderY;
            double cx = Clamp(center.X, hx, hx + _config.HeroColliderW);
            double cy = Clamp(center.Y, hy, hy + _config.HeroColliderH);
            double dx = center.X - cx;
            double dy = center.Y - cy;

            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
