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

        /// <summary>
        /// 살아 있을 수 있는 쿠나이 수. 전사는 <b>초당 4발</b>을 던지고 하나는 화면 밖으로 나가야 사라진다 —
        /// 화면 대각(≈1180) ÷ 속도 500 ≈ 2.4초 · 4발 = 10개면 충분하다. 여유를 둔다.
        /// </summary>
        private const int MaxKunais = 32;

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

        /// <summary>
        /// 구조된 전사가 던지는 쿠나이 [소스 class <c>Pd</c>].
        /// <para>★ <b>적 하나를 한 번만</b> 때리고 <b>관통</b>한다 — 총알과 다르다(총알은 맞으면 사라진다).</para>
        /// </summary>
        public struct Kunai
        {
            public bool Active;
            public UndeadVec2 Position;

            /// <summary>단위 방향 — 대각 넷 중 하나다 [소스 — <c>±√½</c>].</summary>
            public UndeadVec2 Direction;
        }

        private readonly Fireball[] _fireballs = new Fireball[MaxFireballs];
        private readonly Kunai[] _kunais = new Kunai[MaxKunais];

        /// <summary>
        /// 「이 쿠나이가 저 적을 이미 때렸나」 — 원본의 <c>hitEnemies</c> 집합이다 [소스].
        /// <para>⚠ 마지막 칸은 <b>보스</b> 몫이다 — 보스는 적 배열 밖에 있다(궤도 <c>_orbHits</c> 와 같은 규약).</para>
        /// </summary>
        private readonly bool[] _kunaiHits = new bool[MaxKunais * MaxEnemies];

        private double _warriorAttackCooldown;
        private double _warriorThanksRemaining;
        private double _questIntroDelay;
        private double _questIntroBubble;
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

        /// <summary>
        /// 구조 진행률 0~1 — <b>전사 위의 링</b>이 이 값으로 컷을 고른다 [소스 <c>setProgress</c>].
        /// <para>⚠ [사고] <see cref="RescueSeconds"/> 는 세고 있었는데 <b>읽는 곳이 없어</b> 링이 화면에 없었다.</para>
        /// </summary>
        public double RescueProgress
        {
            get
            {
                return _config.QuestRescueSeconds > 0.0
                    ? Math.Min(1.0, RescueSeconds / _config.QuestRescueSeconds)
                    : 0.0;
            }
        }

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

        /// <summary>
        /// 전사의 표시 상태 [소스 <c>setMode</c>].
        /// <para>⚠ <b>구조 «전»은 언제나 <see cref="EUndeadWarriorMode.Lay"/></b> — 원본도 그때는 따라오지 않는다.</para>
        /// </summary>
        public EUndeadWarriorMode WarriorMode { get; private set; }

        /// <summary>
        /// 전사가 왼쪽을 보나 [소스 — <c>h &lt; 0 ? scale.x = −s : h &gt; 0 &amp;&amp; (scale.x = s)</c>].
        /// <para>⚠ <b>0 이면 그대로 둔다</b> — 수직으로만 움직일 때 원본은 방향을 안 바꾼다.</para>
        /// </summary>
        public bool WarriorFacingLeft { get; private set; }

        /// <summary>
        /// 전사가 «놓였나». <b>퀘스트가 비는 동안에도 전사는 화면에 있다</b> —
        /// 과제와 과제 «사이»(예고 대기)에 <c>CurrentQuest</c> 가 <see cref="EUndeadQuest.None"/> 이 되기 때문이다.
        /// <para>⚠ [사고] 이걸 <c>QuestActive</c> 로 가르면 그 사이에 <b>전사가 통째로 사라진다</b>.</para>
        /// </summary>
        public bool WarriorSpawned { get; private set; }

        /// <summary>전사가 지금 하는 말 [소스 — 말풍선 하나를 갈아끼운다].</summary>
        public EUndeadWarriorSpeech WarriorSpeech { get; private set; }

        /// <summary>
        /// <b>예고 중인 다음 과제</b> — 아직 시작하지 않았다 [소스 <c>questIntro</c>].
        /// <para>말풍선 문구가 이 값으로 갈린다.</para>
        /// </summary>
        public EUndeadQuest PendingQuest { get; private set; }

        /// <summary>마법사 과제가 «시작됐나» — 마법사 NPC 는 그때 놓인다 [소스 — 과제 시작이 NPC 를 만든다].</summary>
        public bool MageQuestStarted { get; private set; }

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

        public IReadOnlyList<Kunai> Kunais { get { return _kunais; } }

        public int ActiveKunaiCount { get; private set; }

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

            Array.Clear(_kunais, 0, _kunais.Length);
            Array.Clear(_kunaiHits, 0, _kunaiHits.Length);
            ActiveKunaiCount = 0;
            _warriorAttackCooldown = 0.0;

            RescueSeconds = 0.0;
            QuestRescued = false;
            WarriorMode = EUndeadWarriorMode.Lay;
            WarriorFacingLeft = false;
            WarriorSpawned = false;
            WarriorSpeech = EUndeadWarriorSpeech.None;
            PendingQuest = EUndeadQuest.None;
            MageQuestStarted = false;
            _warriorThanksRemaining = 0.0;
            _questIntroDelay = 0.0;
            _questIntroBubble = 0.0;
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

            // ★★ 전사는 «지금 무슨 퀘스트인지»와 상관없이 돈다 [소스 — update() 는 mode 만 본다].
            //   [사고] 예전에는 퀘스트 갈래 «안»에서만 따라오게 해 두어, 퀘스트를 전부 끝내면
            //   (CurrentQuest = Done) 전사가 그 자리에 굳었다. 원본은 판이 끝날 때까지 따라온다.
            StepWarriorCompanion(dt);
            StepKunais(dt);

            switch (CurrentQuest)
            {
                case EUndeadQuest.None:
                    // ⚠ 시계는 «첫 과제»만 연다 [소스 — quest_warrior 만 type:"clock"].
                    //   나머지 셋은 전사가 말풍선으로 연다 — 그래서 과제 «사이»에도 여기로 온다.
                    //   전사가 이미 놓였으면 이 갈래는 아무 일도 하지 않는다.
                    if (WarriorSpawned)
                        break;

                    // 시계 퀘스트 — 전투 시작 2초 뒤 전사가 «그때 히어로 기준»으로 놓인다 [소스 spawnWarrior — hero.x−1500, hero.y+1500]
                    QuestIntroRemaining -= dt;

                    if (QuestIntroRemaining <= 0.0)
                    {
                        QuestIntroRemaining = 0.0;
                        WarriorPosition = HeroPosition + _config.WarriorOffset;
                        WarriorSpawned = true;
                        WarriorSpeech = EUndeadWarriorSpeech.Help;
                        CurrentQuest = EUndeadQuest.Warrior;
                    }

                    break;

                case EUndeadQuest.Warrior:
                    StepWarrior(dt);
                    break;

                case EUndeadQuest.Mage:
                    StepMage();
                    break;

                case EUndeadQuest.FirstBoss:
                    StepFirstBoss(dt);
                    break;

                case EUndeadQuest.FarmerSheep:
                    StepFarmer(dt);
                    break;
            }
        }

        /// <summary>
        /// 구조된 전사 — <b>따라오고 쿠나이를 던진다</b> [소스 <c>update()</c> — <c>updateFollow</c> + <c>updateAttack</c>].
        ///
        /// <para>
        /// ⚠ <b>구조 전(<see cref="EUndeadWarriorMode.Lay"/>)에는 둘 다 안 한다</b> — 원본도 누워 있는 동안은
        /// <c>updateLayState</c> 만 돈다. 그래서 조건은 「구조됐나」 하나다.
        /// </para>
        /// </summary>
        private void StepWarriorCompanion(double dt)
        {
            if (QuestRescued == false)
                return;

            FollowWarrior(dt);
            StepWarriorAttack(dt);

            // ⚠ 순서가 원본과 같아야 한다 [소스 update — 감사 인사 → 과제 예고].
            //   감사(2초)와 예고 대기(5·10초)는 겹치지 않지만, 겹치면 «예고»가 이긴다.
            StepWarriorThanks(dt);
            StepQuestIntro(dt);
        }

        /// <summary>구조 직후 2초 — 「고마워, 친구!」 [소스 <c>updateThanksBubble</c>].</summary>
        private void StepWarriorThanks(double dt)
        {
            if (_warriorThanksRemaining <= 0.0)
            {
                WarriorSpeech = EUndeadWarriorSpeech.None;
                return;
            }

            _warriorThanksRemaining = Math.Max(0.0, _warriorThanksRemaining - dt);
            WarriorSpeech = EUndeadWarriorSpeech.Thanks;
        }

        // ══════════════════════════════ ★★ 다음 과제를 «전사가» 연다 [소스 updateQuestIntro]

        /// <summary>
        /// 앞 과제가 끝나면 <b>곧장 다음이 켜지지 않는다</b> — 전사가 <b>기다렸다가 말풍선을 띄우고</b>
        /// 그 말풍선이 <b>사라질 때</b> 다음 과제가 시작된다 [소스 <c>el</c> — <c>introduction.type "warrior"</c>].
        ///
        /// <para>
        /// | 과제 | 기다림 | 말풍선 | 문구 |<br/>
        /// | 마법사 | 5초 | 4초 | <c>iHearSomebodyScreaming</c> |<br/>
        /// | 첫 보스 | 10초 | 3초 | <c>iFeelSomeDarkEnergy</c> |<br/>
        /// | 농부/양 | 10초 | 3초 | <c>whatTheAnimals</c> |
        /// </para>
        ///
        /// <para>
        /// ⚠ [사고] 우리는 <b>앞 과제가 끝나는 «그 프레임»에 다음을 켰다</b>. 그래서 전사가 하는 말 <b>세 마디</b>가
        /// 통째로 없었고, 문구 표의 세 키(<c>iHearSomebodyScreaming</c>·<c>iFeelSomeDarkEnergy</c>·<c>whatTheAnimals</c>)를
        /// <b>아무도 읽지 않았다</b> — 「표에 넣고 읽는 곳이 없다」의 재발이다 (재발방지 #131 · #159).
        /// </para>
        /// </summary>
        private void StepQuestIntro(double dt)
        {
            // 과제가 «도는 중»이면 예고를 접는다 [소스 — currentQuest 가 있으면 cancelQuestIntro]
            if (CurrentQuest != EUndeadQuest.None)
            {
                CancelQuestIntro();
                return;
            }

            if (PendingQuest == EUndeadQuest.None)
            {
                EUndeadQuest next = NextUncompletedQuest();

                if (next == EUndeadQuest.None)
                    return;

                PendingQuest = next;
                _questIntroDelay = IntroDelayOf(next);
                _questIntroBubble = 0.0;

                if (_questIntroDelay <= 0.0)
                    ShowQuestIntroBubble();

                return;
            }

            if (_questIntroDelay > 0.0)
            {
                _questIntroDelay = Math.Max(0.0, _questIntroDelay - dt);

                if (_questIntroDelay <= 0.0)
                    ShowQuestIntroBubble();

                return;
            }

            if (_questIntroBubble <= 0.0)
                return;

            _questIntroBubble = Math.Max(0.0, _questIntroBubble - dt);
            WarriorSpeech = EUndeadWarriorSpeech.QuestIntro;

            if (_questIntroBubble > 0.0)
                return;

            StartPreparedQuest();
        }

        /// <summary>아직 안 끝난 <b>첫</b> 과제 [소스 <c>il</c> — 정의 순서대로 찾는다].</summary>
        private EUndeadQuest NextUncompletedQuest()
        {
            if (QuestRescued == false)
                return EUndeadQuest.None;   // 전사 과제는 «시계»가 연다 — 예고 대상이 아니다

            if (MageCompleted == false)
                return EUndeadQuest.Mage;

            if (BossPhase != EBossPhase.Done)
                return EUndeadQuest.FirstBoss;

            if (FarmerPhase != EFarmerPhase.Done)
                return EUndeadQuest.FarmerSheep;

            return EUndeadQuest.None;
        }

        private double IntroDelayOf(EUndeadQuest quest)
        {
            switch (quest)
            {
                case EUndeadQuest.Mage: return _config.QuestIntroMageDelaySeconds;
                case EUndeadQuest.FirstBoss: return _config.QuestIntroFirstBossDelaySeconds;
                case EUndeadQuest.FarmerSheep: return _config.QuestIntroFarmerDelaySeconds;
                default: return 0.0;
            }
        }

        private double IntroBubbleOf(EUndeadQuest quest)
        {
            switch (quest)
            {
                case EUndeadQuest.Mage: return _config.QuestIntroMageBubbleSeconds;
                case EUndeadQuest.FirstBoss: return _config.QuestIntroFirstBossBubbleSeconds;
                case EUndeadQuest.FarmerSheep: return _config.QuestIntroFarmerBubbleSeconds;
                default: return 0.0;
            }
        }

        private void ShowQuestIntroBubble()
        {
            _questIntroBubble = IntroBubbleOf(PendingQuest);

            // 말풍선 시간이 0 이면 «말 없이» 바로 시작한다 [소스]
            if (_questIntroBubble <= 0.0)
            {
                StartPreparedQuest();
                return;
            }

            WarriorSpeech = EUndeadWarriorSpeech.QuestIntro;
        }

        private void StartPreparedQuest()
        {
            EUndeadQuest quest = PendingQuest;
            CancelQuestIntro();

            if (quest == EUndeadQuest.None)
                return;

            CurrentQuest = quest;

            if (quest == EUndeadQuest.Mage)
                MageQuestStarted = true;
        }

        private void CancelQuestIntro()
        {
            PendingQuest = EUndeadQuest.None;
            _questIntroDelay = 0.0;
            _questIntroBubble = 0.0;

            if (WarriorSpeech == EUndeadWarriorSpeech.QuestIntro)
                WarriorSpeech = EUndeadWarriorSpeech.None;
        }

        /// <summary>
        /// 전사가 히어로를 <b>「걸어서」</b> 따라온다 [소스 <c>updateFollow</c>].
        ///
        /// <para>
        /// ★ 속도는 <b>히어로 이동 속도 스탯의 0.95배</b>다 — 그래서 달리면 <b>조금씩 벌어지고</b>
        /// 멈추면 따라붙는다. 히어로가 이동 강화를 먹으면 전사도 같이 빨라진다 [소스는 스탯을 읽는다].
        /// </para>
        ///
        /// <para>
        /// ⚠ [사고] 예전 우리 코드는 <b>매 프레임 순간이동</b>이었다 — 거리가 늘 같으니 「전사가 움직이나」가
        /// 「히어로가 움직이나」와 같은 말이 되어 상태가 히어로 «입력»으로 갈렸다.
        /// 원본은 <b>남은 거리</b>로 가른다. 순간이동은 나무 통행 판정도 건너뛴다.
        /// </para>
        /// </summary>
        private void FollowWarrior(double dt)
        {
            double speed = _config.HeroMoveSpeed * MoveSpeedStat * _config.WarriorFollowSpeedMultiplier;
            double dx = HeroPosition.X + _config.WarriorFollowOffsetX - WarriorPosition.X;
            double dy = HeroPosition.Y - WarriorPosition.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance <= _config.WarriorFollowStopDistance)
            {
                WarriorMode = EUndeadWarriorMode.Idle;
                return;
            }

            double hx = dx / distance;
            double hy = dy / distance;

            // 나무 발자국은 전사도 못 지난다 [소스 — world.clampToPassablePosition(this, prevX, prevY)]
            var next = new UndeadVec2(WarriorPosition.X + hx * speed * dt, WarriorPosition.Y + hy * speed * dt);
            WarriorPosition = ClampToPassable(WarriorPosition, next);

            // ⚠ 0 이면 «그대로 둔다» — 수직으로만 갈 때 원본은 방향을 안 바꾼다 [소스]
            if (hx < 0.0)
                WarriorFacingLeft = true;
            else if (hx > 0.0)
                WarriorFacingLeft = false;

            WarriorMode = EUndeadWarriorMode.Run;
        }

        // ══════════════════════════════ 전사의 공격 — 쿠나이 [소스 throwKunais · class Pd]

        /// <summary>
        /// 구조된 전사는 <b>1초마다 대각 넷으로 쿠나이를 던진다</b> [소스 <c>updateAttack</c>].
        /// <para>★ 히어로가 어디를 보든·적이 어디 있든 상관없다 — <b>고정 대각 4방</b>이다.</para>
        /// </summary>
        private void StepWarriorAttack(double dt)
        {
            _warriorAttackCooldown -= dt;

            if (_warriorAttackCooldown > 0.0)
                return;

            _warriorAttackCooldown = _config.WarriorAttackIntervalSeconds;
            ThrowKunais();
        }

        /// <summary>√½ [소스 <c>Math.SQRT1_2</c>].</summary>
        private const double Sqrt1_2 = 0.7071067811865476;

        /// <summary>대각 넷 [소스 — <c>±√½</c> 조합].</summary>
        private static readonly UndeadVec2[] KunaiDirections =
        {
            new UndeadVec2(Sqrt1_2, Sqrt1_2),
            new UndeadVec2(-Sqrt1_2, Sqrt1_2),
            new UndeadVec2(Sqrt1_2, -Sqrt1_2),
            new UndeadVec2(-Sqrt1_2, -Sqrt1_2),
        };

        private void ThrowKunais()
        {
            for (int d = 0; d < KunaiDirections.Length; d++)
                SpawnKunai(KunaiDirections[d]);
        }

        private void SpawnKunai(UndeadVec2 direction)
        {
            for (int i = 0; i < _kunais.Length; i++)
            {
                if (_kunais[i].Active)
                    continue;

                _kunais[i] = new Kunai
                {
                    Active = true,
                    Position = new UndeadVec2(WarriorPosition.X, WarriorPosition.Y + _config.KunaiSpawnOffsetY),
                    Direction = direction,
                };

                // 새 쿠나이는 «아무도 안 때린» 상태로 시작한다 [소스 — hitEnemies = new Set]
                Array.Clear(_kunaiHits, i * MaxEnemies, MaxEnemies);
                return;
            }
        }

        /// <summary>
        /// 쿠나이 한 프레임 [소스 <c>Pd.update</c>].
        ///
        /// <para>
        /// ★ 피해는 <b>히어로의 총알 피해</b>를 그대로 쓴다 — 전사 고유 수치가 아니라
        /// <b>히어로 강화를 따라간다</b> [소스 <c>state.heroBulletDamage</c>].
        /// </para>
        ///
        /// <para>
        /// ★ <b>관통한다.</b> 총알은 맞으면 사라지지만 쿠나이는 계속 날아가고,
        /// <b>같은 적은 두 번 안 때린다</b> [소스 <c>hitEnemies</c>].
        /// </para>
        ///
        /// <para>⚠ 보스도 맞는다 — 원본에서 보스 클래스가 적 클래스를 <b>상속</b>해 같은 필터에 걸린다.</para>
        /// </summary>
        private void StepKunais(double dt)
        {
            int active = 0;

            for (int i = 0; i < _kunais.Length; i++)
            {
                if (_kunais[i].Active == false)
                    continue;

                double speed = _config.KunaiSpeedWorld;

                _kunais[i].Position = new UndeadVec2(
                    _kunais[i].Position.X + _kunais[i].Direction.X * speed * dt,
                    _kunais[i].Position.Y + _kunais[i].Direction.Y * speed * dt);

                StepKunaiHits(i);

                // 화면 «한 장»만큼 벗어나면 사라진다 [소스 — camera.worldX ± sceneRect.width]
                if (Math.Abs(_kunais[i].Position.X - CameraPivot.X) > _config.ViewportWidth
                    || Math.Abs(_kunais[i].Position.Y - CameraPivot.Y) > _config.ViewportHeight)
                {
                    _kunais[i].Active = false;
                    continue;
                }

                active++;
            }

            ActiveKunaiCount = active;
        }

        private void StepKunaiHits(int kunai)
        {
            int baseIndex = kunai * MaxEnemies;
            int damage = DamageStat;

            for (int e = 0; e < _enemies.Length; e++)
            {
                if (_enemies[e].Active == false || _kunaiHits[baseIndex + e])
                    continue;

                if (CircleHitsEnemy(_kunais[kunai].Position, _config.KunaiRadius, _enemies[e].Position) == false)
                    continue;

                _kunaiHits[baseIndex + e] = true;
                OnEnemyDamaged?.Invoke(_enemies[e].Position, damage, false, false);
                DamageEnemy(e, damage, _config.KunaiKnockback);
            }

            // 보스는 적 배열 «밖»이라 마지막 칸을 기록으로 쓴다 (궤도 _orbHits 와 같은 규약)
            if (BossActive == false || _kunaiHits[baseIndex + MaxEnemies - 1])
                return;

            if (CircleHitsBoss(_kunais[kunai].Position, _config.KunaiRadius) == false)
                return;

            _kunaiHits[baseIndex + MaxEnemies - 1] = true;
            OnEnemyDamaged?.Invoke(BossPosition, damage, false, true);
            DamageBoss(damage);
        }

        // ══════════════════════════════ 퀘스트 1 — 전사

        private void StepWarrior(double dt)
        {
            bool inside = (WarriorPosition - HeroPosition).Magnitude <= _config.QuestRescueRadius;
            RescueSeconds = inside ? Math.Min(_config.QuestRescueSeconds, RescueSeconds + dt) : 0.0;

            // ★ 링이 차는 «동안»에는 말풍선이 숨는다 [소스 — bubble.visible = rescueTimerMs <= 0].
            //   둘이 같이 뜨면 원본과 화면이 다르다.
            WarriorSpeech = RescueSeconds > 0.0 ? EUndeadWarriorSpeech.None : EUndeadWarriorSpeech.Help;

            if (inside == false || RescueSeconds < _config.QuestRescueSeconds)
                return;

            QuestRescued = true;

            // ★ 첫 발은 구조 «1초 뒤»다 [소스 — 구조 완료 시 attackCooldownMs = attackIntervalMs]
            _warriorAttackCooldown = _config.WarriorAttackIntervalSeconds;

            // ★ 구조되면 2초짜리 「고마워, 친구!」 [소스 thanksPal]
            _warriorThanksRemaining = _config.WarriorThanksBubbleSeconds;

            // ⚠ 다음 과제(마법사)는 «여기서» 켜지 않는다 — 전사가 5초 뒤 말풍선으로 연다 [소스].
            CurrentQuest = EUndeadQuest.None;
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
            // 다음 과제(첫 보스)는 전사가 «예고»한 뒤 열린다 [소스 introduction.type "warrior"]
            CurrentQuest = EUndeadQuest.None;
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
                    // 다음 과제(농부/양)는 전사가 «예고»한 뒤 열린다 [소스 introduction.type "warrior"]
                    CurrentQuest = EUndeadQuest.None;
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
