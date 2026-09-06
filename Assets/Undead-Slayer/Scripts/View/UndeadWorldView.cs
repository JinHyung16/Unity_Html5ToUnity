using System;
using System.Collections.Generic;
using JinHyung.Core;
using TMPro;
using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// ★★★ <b>중앙 틱 렌더러</b> — 시뮬의 개체 배열을 <b>풀링된 <c>SpriteRenderer</c></b> 에 그대로 붓는다.
    ///
    /// <para>
    /// ⚠ <b>개체당 컴포넌트를 붙이지 않는다</b> (확정표 G). 원본은 레벨 10 에서 적이 <b>485마리</b>
    /// 동시에 돈다 [실측] — 개체마다 <c>Animator</c> 나 «Enemy 스크립트»를 붙이면
    /// 업데이트와 재생 그래프가 개체 수에 비례해 붙는다.
    /// 여기 붙는 것은 <see cref="SpriteRenderer"/> <b>하나뿐</b>이고, 그마저 풀에서 돌려 쓴다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>프레임 인덱스는 «개체 데이터»에 있다</b> — 이 클래스는 그걸 «읽어» 스프라이트를 고를 뿐,
    /// 재생을 소유하지 않는다. 그래서 개체를 풀에 반환하면 프레임도 같이 초기화된다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>정렬은 y 기준이다</b> — 탑다운이라 아래에 있는 것이 앞이다.
    /// 원본의 «그리는 순서»는 씬 그래프 순서로 잡히지만 개체 간 정렬 규칙은 <b>미측정</b>이다.
    /// </para>
    /// </summary>
    public sealed class UndeadWorldView : MonoBehaviour
    {
        /// <summary>
        /// 정렬 오프셋. world y 를 정수 정렬 순서로 바꿀 때 쓴다 —
        /// y 가 클수록(아래일수록) 앞이므로 부호를 뒤집는다.
        /// </summary>
        private const int SortingBase = 10000;

        private readonly List<SpriteRenderer> _enemyPool = new List<SpriteRenderer>(512);
        private readonly List<SpriteRenderer> _projectilePool = new List<SpriteRenderer>(64);
        private readonly List<SpriteRenderer> _gemPool = new List<SpriteRenderer>(256);
        private readonly List<SpriteRenderer> _orbPool = new List<SpriteRenderer>(16);
        private readonly List<SpriteRenderer> _fireballPool = new List<SpriteRenderer>(64);

        /// <summary>말풍선이 목표 «위»로 뜨는 높이 (원본 px). ⚠ 원본 값은 <b>미측정</b>이라 눈으로 맞춘 값이다.</summary>
        private const double BubbleOffsetY = 48.0;

        private const double BubbleTextOffsetY = 10.0;
        private const float HelpTextSize = 3.5f;
        private const float HelpTextWidth = 6f;
        private const float HelpTextHeight = 1.6f;

        // ── 체력바 [소스 — heroLivesBar]
        private const double LifeBarY = -60.0;
        private const double LifeSegmentWidth = 12.0;
        private const double LifeSegmentGap = 1.0;
        private const double LifeFadeIn = 0.15;
        private const double LifeFadeOut = 0.4;
        private const double LifeFadeOutDelay = 1.2;

        private Transform _enemyRoot;
        private Transform _projectileRoot;
        private Transform _gemRoot;
        private SpriteRenderer _hero;
        /// <summary>체력바 — 히어로 «위»에 붙는 칸들 [소스 — y −60 · 12×6 · 여백 1].</summary>
        private SpriteRenderer[] _lifeSegments;

        private Transform _lifeRoot;
        private double _lifeFadeSeconds;

        private Transform _questRoot;
        private SpriteRenderer _questWarrior;
        private SpriteRenderer _questBubble;
        private SpriteRenderer _questMage;
        private SpriteRenderer[] _questFragments;
        private TMP_Text _questHelpText;

        private UndeadSimulation _sim;
        private UndeadSpriteSet _heroSet;
        /// <summary>
        /// <b>적 종류마다 한 벌</b> — 시뮬의 <c>TypeId</c> 가 이 배열의 인덱스다 [회차 9].
        /// <para>⚠ 한 벌만 두면 <b>웨이브 3 의 박쥐가 좀비 그림으로</b> 나온다.</para>
        /// </summary>
        private IReadOnlyList<UndeadSpriteSet> _enemySets;
        private UndeadSpriteSet _projectileSet;
        private UndeadSpriteSet _gemSet;
        private UndeadSpriteSet _orbSet;
        private Transform _orbRoot;
        private UndeadSpriteSet _fireballSet;
        private Transform _fireballRoot;

        // ── 퀘스트 3·4 의 개체들. 하나씩뿐이라 풀이 아니다.
        private SpriteRenderer _boss;
        private SpriteRenderer _family;
        private SpriteRenderer _farmer;
        // ── 월드 오브젝트 — 지형이 놓는 나무·모닥불, 나무가 뿜는 유성 (풀)
        private UndeadSpriteSet _treeSet;
        private UndeadSpriteSet _treeActivatedSet;
        private UndeadSpriteSet _treeInactiveSet;
        private UndeadSpriteSet _meteorSet;
        private UndeadSpriteSet _fireSet;
        private UndeadSpriteSet _fireInactiveSet;
        private UndeadSpriteSet _heartSet;
        private Transform _treeRoot;
        private Transform _meteorRoot;
        private Transform _fireplaceRoot;
        private Transform _heartRoot;
        private readonly List<SpriteRenderer> _treePool = new List<SpriteRenderer>(16);
        private readonly List<SpriteRenderer> _meteorPool = new List<SpriteRenderer>(16);
        private readonly List<SpriteRenderer> _fireplacePool = new List<SpriteRenderer>(16);
        private readonly List<SpriteRenderer> _heartPool = new List<SpriteRenderer>(32);

        /// <summary>
        /// 모닥불 위로 떠오르는 하트 [소스 <c>Gl</c> · 설정 <c>zl</c>] —
        /// <b>모닥불 하나에 최대 3개</b> · 초당 1.2개 · 1.1~1.6초 · 알파 .8 → 0 · 크기 .4~.5 가 8% 자란다.
        /// <para>⚠ 자리·크기는 <b>입자 컨테이너 배율(1.3, 1.8)</b>을 곱한 값이 화면 값이다 — 안 곱하면 궤적이 좁아진다.</para>
        /// </summary>
        private struct HeartParticle
        {
            public bool Active;
            public int Owner;
            public double X;
            public double Y;
            public double Vx;
            public double Vy;
            public double LifeMs;
            public double AgeMs;
            public double Size;
        }

        private const int HeartsPerFire = 3;              // [소스 zl.maxParticles]
        private const int MaxHearts = 96;
        private const double HeartSpawnPerSecond = 1.2;   // [소스 getParticleSpawnRate]
        private const double HeartEmitterY = -20.0 + -14.0;   // [소스 heartsParticles.y −20 · zl.containerY −14]
        private const double HeartContainerScaleX = 1.3;  // [소스 zl.containerScaleX]
        private const double HeartContainerScaleY = 1.8;  // [소스 zl.containerScaleY]
        private const double HeartAccumulatorCap = 3.0;   // [소스 zl.spawnAccumulatorCap]
        private readonly HeartParticle[] _hearts = new HeartParticle[MaxHearts];
        private double[] _heartSpawnAccum = new double[0];
        private SpriteRenderer[] _sheep;
        private UndeadSpriteSet _bossSet;
        private SpriteRenderer _encounterSheep;
        private SpriteRenderer _encounterFold;

        /// <summary>지금 화면에 켜져 있는 렌더러 수 — <b>검사가 이 값을 센다</b>.</summary>
        public int ActiveRendererCount { get; private set; }

        /// <summary>풀이 만든 렌더러 총수. <b>개체 수에 비례해 «컴포넌트»가 늘지 않는지</b>를 보는 값이다.</summary>
        public int PooledRendererCount
        {
            get { return _enemyPool.Count + _projectilePool.Count + _gemPool.Count + (_hero != null ? 1 : 0); }
        }

        public void Bind(UndeadSimulation sim,
                         UndeadSpriteSet hero,
                         IReadOnlyList<UndeadSpriteSet> enemies,
                         UndeadSpriteSet projectile,
                         UndeadSpriteSet gem)
        {
            _sim = sim;
            _heroSet = hero;
            _enemySets = enemies;

            // ⚠ 한 종류라도 못 읽으면 «그 종류가 조용히 안 그려진다» — 시끄럽게 알린다.
            //   [사고] 회차 9 에 첫 종류(박쥐)가 null 이라 <b>적이 통째로 안 보였다</b>.
            //   시뮬은 27마리라고 했고 화면엔 0마리였다.
            for (int i = 0; enemies != null && i < enemies.Count; i++)
            {
                if (enemies[i] == null)
                    Log.Error($"적 스프라이트 세트 {i} 번이 없다 — 그 종류는 화면에 안 나온다");
            }
            _projectileSet = projectile;
            _gemSet = gem;

            _enemyRoot = NewRoot("Enemies");
            _projectileRoot = NewRoot("Projectiles");
            _gemRoot = NewRoot("Gems");

            _hero = NewRenderer(NewRoot("Hero"), hero);
        }

        /// <summary>
        /// 지형이 놓는 <b>나무(평소·켜짐·터진 뒤) · 모닥불</b> 과 나무가 뿜는 <b>유성</b> [소스 class ld · Wl · rd].
        /// 전부 풀이다 — 카메라가 움직이면 청크가 바뀌고 개체가 갈린다.
        /// </summary>
        public void BindWorldObjects(UndeadSpriteSet tree, UndeadSpriteSet treeActivated, UndeadSpriteSet treeInactive,
                                     UndeadSpriteSet meteor, UndeadSpriteSet fire, UndeadSpriteSet fireInactive, UndeadSpriteSet heart)
        {
            _treeSet = tree;
            _treeActivatedSet = treeActivated;
            _treeInactiveSet = treeInactive;
            _meteorSet = meteor;
            _fireSet = fire;
            _fireInactiveSet = fireInactive;
            _heartSet = heart;
            _treeRoot = NewRoot("Trees");
            _meteorRoot = NewRoot("Meteors");
            _fireplaceRoot = NewRoot("Fireplaces");
            _heartRoot = NewRoot("FireHearts");

            if (tree == null || treeActivated == null || treeInactive == null || meteor == null || fire == null || fireInactive == null || heart == null)
                Log.Error("월드 오브젝트 스프라이트가 비었다 — 나무·유성·모닥불·하트 중 하나가 화면에 안 나온다");
        }

        /// <summary>번개 구슬 — 히어로 둘레를 도는 궤도 무기 [소스].</summary>
        public void BindLightning(UndeadSpriteSet orb)
        {
            _orbSet = orb;
            _orbRoot = NewRoot("Lightning");
        }

        /// <summary>
        /// 퀘스트 3·4 의 개체 — <b>보스 · 불덩이 · 가족 · 농부 · 양 · 모닥불</b> [소스].
        /// <para>보스와 NPC 는 판마다 하나씩이라 풀을 만들지 않는다. 불덩이만 풀이다.</para>
        /// </summary>
        public void BindBossQuest(UndeadSpriteSet boss, UndeadSpriteSet fireball, UndeadSpriteSet family,
                                  UndeadSpriteSet farmer, UndeadSpriteSet sheep)
        {
            Transform root = NewRoot("BossQuest");
            _bossSet = boss;
            _fireballSet = fireball;
            _fireballRoot = NewRoot("Fireballs");

            if (boss != null)
                _boss = NewRenderer(root, boss);

            if (family != null)
                _family = NewRenderer(root, family);

            if (farmer != null)
                _farmer = NewRenderer(root, farmer);

            if (sheep == null || _sim == null)
                return;

            _sheep = new SpriteRenderer[Math.Max(0, _sim.Sheep_.Count)];

            for (int i = 0; i < _sheep.Length; i++)
                _sheep[i] = NewRenderer(root, sheep);

            // 클리어 뒤 조우 — 양 하나 + 우리(농부 그림을 다시 쓴다)
            _encounterSheep = NewRenderer(root, sheep);

            if (farmer != null)
                _encounterFold = NewRenderer(root, farmer);
        }

        /// <summary>
        /// <b>체력바</b>를 세운다 [소스 — <c>heroLivesBar</c>].
        ///
        /// <para>
        /// ★ 원본은 <b>가득 차 있으면 사라진다</b> — 1.2초 기다렸다가 0.4초에 걸쳐 옅어지고,
        /// 맞으면 0.15초에 걸쳐 다시 나타난다. 그래서 <b>평소 캡처에는 «안 보이는» 것이 정상</b>이다
        /// (회차 6 에 「체력 UI 가 없다」로 적었던 것이 이것이다 — 없는 게 아니라 «꺼져» 있었다).
        /// </para>
        /// </summary>
        public void BindLifeBar(UndeadSpriteSet segment, int maxHp)
        {
            if (segment == null || maxHp <= 0)
                return;

            _lifeRoot = NewRoot("LifeBar");
            _lifeSegments = new SpriteRenderer[maxHp];

            for (int i = 0; i < maxHp; i++)
            {
                _lifeSegments[i] = NewRenderer(_lifeRoot, segment);
                _lifeSegments[i].gameObject.SetActive(true);
                _lifeSegments[i].sortingOrder = SortingBase * 2;
            }
        }

        /// <summary>
        /// <b>퀘스트 개체</b>를 세운다 — 쓰러진 전사 · 말풍선 · 마법사 · 아케인 조각 2개 [소스 · 회차 9].
        ///
        /// <para>
        /// ★ 자리는 <b>시뮬이 든다</b> — 전사는 구조되면 히어로를 따라오고 조각은 주우면 어깨에 붙으므로
        /// «한 번 놓고 끝»이 아니다. 여기서는 <b>매 프레임 시뮬 값을 옮겨 담기만</b> 한다.
        /// </para>
        /// </summary>
        public void BindQuestTarget(UndeadSpriteSet warrior, UndeadSpriteSet bubble,
                                    UndeadSpriteSet mage, UndeadSpriteSet fragment,
                                    string helpText, TMP_FontAsset font)
        {
            if (_sim == null)
                return;

            _questRoot = NewRoot("Quest");

            if (warrior != null)
                _questWarrior = NewRenderer(_questRoot, warrior);

            if (bubble != null)
                _questBubble = NewRenderer(_questRoot, bubble);

            if (mage != null)
                _questMage = NewRenderer(_questRoot, mage);

            if (fragment != null)
            {
                _questFragments = new SpriteRenderer[2];
                _questFragments[0] = NewRenderer(_questRoot, fragment);
                _questFragments[1] = NewRenderer(_questRoot, fragment);
            }

            if (string.IsNullOrEmpty(helpText))
                return;

            var textObject = new GameObject("HelpText");
            textObject.transform.SetParent(_questRoot, false);

            _questHelpText = textObject.AddComponent<TextMeshPro>();

            if (font != null)
                _questHelpText.font = font;

            _questHelpText.text = helpText;
            _questHelpText.fontSize = HelpTextSize;
            _questHelpText.color = Color.black;
            _questHelpText.alignment = TextAlignmentOptions.Center;
            _questHelpText.rectTransform.sizeDelta = new Vector2(HelpTextWidth, HelpTextHeight);
        }

        /// <summary>퀘스트 개체를 매 프레임 옮긴다 — 켜고 끄는 조건이 곧 «퀘스트 상태»다.</summary>
        private void RenderQuest()
        {
            if (_questRoot == null)
                return;

            // ── 전사. 구조 «전»에만 말풍선이 뜬다 [소스 — 구조되면 bubble.visible = false]
            if (_questWarrior != null)
            {
                Place(_questWarrior.transform, _sim.WarriorPosition, _questWarrior);
                _questWarrior.sortingOrder = SortOrder(_sim.WarriorPosition.Y);
                _questWarrior.enabled = _sim.QuestActive;   // 전사는 퀘스트가 «켜질 때» 놓인다 [소스 spawnWarrior]
            }

            bool showHelp = _sim.QuestActive && _sim.QuestRescued == false;

            if (_questBubble != null)
            {
                _questBubble.enabled = showHelp;

                if (showHelp)
                {
                    var at = new UndeadVec2(_sim.WarriorPosition.X, _sim.WarriorPosition.Y - BubbleOffsetY);
                    Place(_questBubble.transform, at, _questBubble);
                    _questBubble.sortingOrder = SortOrder(_sim.WarriorPosition.Y) + 1;
                }
            }

            if (_questHelpText != null)
            {
                _questHelpText.enabled = showHelp;

                if (showHelp)
                {
                    _questHelpText.transform.position = UndeadUnits.ToPosition(
                        _sim.WarriorPosition.X, _sim.WarriorPosition.Y - BubbleOffsetY - BubbleTextOffsetY);
                }
            }

            // ── 마법사. 전사를 구한 «뒤»에 나타난다 [소스 — 퀘스트가 순차다]
            if (_questMage != null)
            {
                _questMage.enabled = _sim.QuestRescued;

                if (_questMage.enabled)
                {
                    Place(_questMage.transform, _sim.MagePosition, _questMage);
                    _questMage.sortingOrder = SortOrder(_sim.MagePosition.Y);
                }
            }

            // ── 조각. 마법사를 «만난 뒤»에 자리가 생긴다
            if (_questFragments == null)
                return;

            bool showFragments = _sim.MageMet;
            SetFragment(_questFragments[0], showFragments, _sim.Fragment0Position);
            SetFragment(_questFragments[1], showFragments, _sim.Fragment1Position);
        }

        private void SetFragment(SpriteRenderer renderer, bool show, UndeadVec2 at)
        {
            if (renderer == null)
                return;

            renderer.enabled = show;

            if (show == false)
                return;

            Place(renderer.transform, at, renderer);
            renderer.sortingOrder = SortOrder(at.Y) + 2;
        }

        /// <summary>
        /// 한 프레임 그린다. <b>시뮬이 먼저 돈 뒤에 부른다</b> —
        /// 순서가 뒤바뀌면 화면이 한 프레임 늦는다.
        /// </summary>
        public void Render()
        {
            AnimateTimed();

            if (_sim == null)
                return;

            int active = 0;

            // ── 히어로. 0행 대기 · 1행 이동 [실측]
            if (_hero != null && _heroSet != null)
            {
                // ★ 죽으면 «장면에서 빠진다» [소스 — hp ≤ 0 이면 scene.remove(hero)]. 부활하면 다시 놓인다.
                _hero.enabled = _sim.HeroDead == false;

                _hero.sprite = _heroSet.Get(_sim.HeroMoving ? 1 : 0, _sim.HeroAnimFrame);
                Place(_hero.transform, _sim.HeroPosition, _heroSet);
                _hero.sortingOrder = SortOrder(_sim.HeroPosition.Y);

                // ★ 피격 연출 [소스] — 200ms 동안 «빨강»으로 물들고, 무적 동안 «깜빡인다».
                //   ⚠ 알파는 시뮬이 준다 — 뷰가 자기 시계로 깜빡이면 정지 중에도 깜빡인다.
                Color tint = _sim.HeroHitTintRemaining > 0.0 ? Color.red : Color.white;
                tint.a = (float)_sim.HeroAlpha;
                _hero.color = tint;

                active++;
            }

            // ⚠ 풀 «틀»로 쓸 대표 세트는 <b>null 이 아닌 첫 세트</b>다.
            //   0번을 그냥 쓰면 그 하나가 비었을 때 <c>Sync</c> 가 0 을 반환해 적이 전부 사라진다.
            active += Sync(_enemyPool, _enemyRoot, FirstEnemySet(),
                           EnemyCount(), (i, r) => WriteEnemy(i, r));
            active += Sync(_projectilePool, _projectileRoot, _projectileSet, ProjectileCount(), (i, r) => WriteProjectile(i, r));
            active += Sync(_gemPool, _gemRoot, _gemSet, GemCount(), (i, r) => WriteGem(i, r));

            active += Sync(_orbPool, _orbRoot, _orbSet, OrbCount(), (i, r) => WriteOrb(i, r));

            active += Sync(_fireballPool, _fireballRoot, _fireballSet, FireballCount(), (i, r) => WriteFireball(i, r));

            active += Sync(_treePool, _treeRoot, _treeSet, _sim.Trees.Count, (i, r) => WriteTree(i, r));
            active += Sync(_meteorPool, _meteorRoot, _meteorSet, _sim.Meteors.Count, (i, r) => WriteMeteor(i, r));
            active += Sync(_fireplacePool, _fireplaceRoot, _fireSet, _sim.Fireplaces.Count, (i, r) => WriteFireplace(i, r));
            StepHearts(Time.deltaTime);
            active += Sync(_heartPool, _heartRoot, _heartSet, MaxHearts, (i, r) => WriteHeart(i, r));

            RenderQuest();
            RenderBossQuest();
            RenderLifeBar();

            ActiveRendererCount = active;
        }

        // ══════════════════════════════ 개체별 쓰기

        /// <summary>체력바를 히어로 위에 그린다 — <b>가득 차면 서서히 사라진다</b> [소스].</summary>
        private void RenderLifeBar()
        {
            if (_lifeSegments == null || _lifeSegments.Length == 0)
                return;

            // ★ 죽으면 히어로가 «장면에서 빠진다» — 체력바도 같이 사라진다 [소스 scene.remove(hero)]
            if (_sim.HeroDead)
            {
                for (int i = 0; i < _lifeSegments.Length; i++)
                    _lifeSegments[i].enabled = false;

                return;
            }

            int hp = _sim.HeroHp;
            bool full = hp >= _lifeSegments.Length;

            // 페이드 — 가득 차면 «지연 뒤» 옅어지고, 아니면 곧바로 나타난다.
            double target = full ? 0.0 : 1.0;
            double speed = full ? 1.0 / (LifeFadeOutDelay + LifeFadeOut) : 1.0 / LifeFadeIn;
            _lifeFadeSeconds = Mathf.MoveTowards((float)_lifeFadeSeconds, (float)target,
                                                 (float)(speed * Time.deltaTime));

            float width = (float)(LifeSegmentWidth + LifeSegmentGap);
            float totalWidth = width * _lifeSegments.Length;

            for (int i = 0; i < _lifeSegments.Length; i++)
            {
                double x = _sim.HeroPosition.X - totalWidth * 0.5 + width * i + width * 0.5;
                _lifeSegments[i].transform.position = UndeadUnits.ToPosition(x, _sim.HeroPosition.Y + LifeBarY);

                Color color = _lifeSegments[i].color;
                color = i < hp ? new Color(0.85f, 0.26f, 0.24f) : new Color(0.15f, 0.13f, 0.19f);
                color.a = (float)_lifeFadeSeconds;
                _lifeSegments[i].color = color;
            }
        }

        private UndeadSpriteSet FirstEnemySet()
        {
            for (int i = 0; _enemySets != null && i < _enemySets.Count; i++)
            {
                if (_enemySets[i] != null)
                    return _enemySets[i];
            }

            return null;
        }

        private int EnemyCount()
        {
            return _sim.Enemies.Count;
        }

        private int ProjectileCount()
        {
            return _sim.Projectiles.Count;
        }

        private int FireballCount()
        {
            return _sim.Fireballs.Count;
        }

        private bool WriteFireball(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.Fireball f = _sim.Fireballs[index];

            if (f.Active == false || _fireballSet == null)
                return false;

            renderer.sprite = _fireballSet.Get(0, f.AnimFrame);
            Place(renderer.transform, f.Position, _fireballSet);
            renderer.transform.localRotation =
                Quaternion.Euler(0f, 0f, (float)(-Math.Atan2(f.Velocity.Y, f.Velocity.X) * Mathf.Rad2Deg));
            renderer.sortingOrder = SortOrder(f.Position.Y) + 2;
            return true;
        }

        /// <summary>
        /// 나무 — 상태에 따라 그림이 갈리고(평소 · 켜짐 · 터짐), 터지기 전엔 «숨쉰다» [소스 —
        /// <c>x: 1 + sin(φ)·.009·(1+1.8s)</c> · <c>y: 1 + sin(φ)·.04·(1+2.2s)</c>, s = 충전 진행(+.5)].
        /// </summary>
        private bool WriteTree(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.Tree t = _sim.Trees[index];

            if (t.Active == false)
                return false;

            UndeadSpriteSet set = t.Bursted ? _treeInactiveSet : (t.Activated ? _treeActivatedSet : _treeSet);

            if (set == null)
                return false;

            renderer.sprite = set.Get(0, 0.0);
            Place(renderer.transform, t.Position, set);
            renderer.sortingOrder = SortOrder(t.Position.Y);

            double s = t.ChargeSmoothed > 0.0 ? t.ChargeSmoothed + 0.5 : 0.0;
            double wobble = Math.Sin(0.03 * t.IdleMs) * s * 0.003;
            double sx = t.Bursted ? 1.0 : 1.0 + Math.Sin(t.PulsePhase) * (0.009 * (1.0 + 1.8 * s)) + wobble;
            double sy = t.Bursted ? 1.0 : 1.0 + Math.Sin(t.PulsePhase) * (0.04 * (1.0 + 2.2 * s)) - wobble;
            renderer.transform.localScale = new Vector3((float)(set.Data.DisplayScaleX * sx),
                                                        (float)(set.Data.DisplayScaleY * sy), 1f);
            return true;
        }

        private bool WriteMeteor(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.Meteor m = _sim.Meteors[index];

            if (m.Active == false || _meteorSet == null)
                return false;

            renderer.sprite = _meteorSet.Get(0, m.AnimFrame);
            Place(renderer.transform, m.Position, _meteorSet);
            renderer.transform.localRotation =
                Quaternion.Euler(0f, 0f, (float)(-Math.Atan2(m.Velocity.Y, m.Velocity.X) * Mathf.Rad2Deg));
            renderer.sortingOrder = SortOrder(m.Position.Y) + 3;
            return true;
        }

        /// <summary>모닥불 [소스 <c>Wl</c>] — 재고가 있으면 «fire» 3컷이 돌고, 0 이면 <c>fire_inactive</c> 한 장으로 멈춘다.</summary>
        private bool WriteFireplace(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.Fireplace f = _sim.Fireplaces[index];

            if (f.Active == false || _fireSet == null)
                return false;

            bool lit = f.Lives > 0;
            UndeadSpriteSet set = lit || _fireInactiveSet == null ? _fireSet : _fireInactiveSet;
            renderer.sprite = lit ? _fireSet.Get(0, Time.timeSinceLevelLoad * _fireSet.Data.Fps) : set.Get(0, 0.0);
            Place(renderer.transform, f.Position, set);
            renderer.sortingOrder = SortOrder(f.Position.Y);
            return true;
        }

        /// <summary>하트 입자 — 재고가 남은 모닥불마다 초당 1.2개 [소스 <c>Gl</c>]. 값은 전부 소스 식이다.</summary>
        private void StepHearts(float dt)
        {
            if (_heartSet == null)
                return;

            if (_heartSpawnAccum.Length < _sim.Fireplaces.Count)
                System.Array.Resize(ref _heartSpawnAccum, _sim.Fireplaces.Count);

            for (int i = 0; i < _sim.Fireplaces.Count; i++)
            {
                UndeadSimulation.Fireplace f = _sim.Fireplaces[i];

                if (f.Active == false || f.Lives <= 0)
                {
                    _heartSpawnAccum[i] = 0.0;
                    continue;
                }

                _heartSpawnAccum[i] = System.Math.Min(HeartAccumulatorCap, _heartSpawnAccum[i] + dt * HeartSpawnPerSecond);

                while (_heartSpawnAccum[i] >= 1.0)
                {
                    _heartSpawnAccum[i] -= 1.0;

                    if (LiveHearts(i) >= HeartsPerFire)
                        break;

                    SpawnHeart(i, f.Position);
                }
            }

            for (int i = 0; i < _hearts.Length; i++)
            {
                if (_hearts[i].Active == false)
                    continue;

                _hearts[i].AgeMs += dt * 1000.0;

                if (_hearts[i].AgeMs >= _hearts[i].LifeMs)
                {
                    _hearts[i].Active = false;
                    continue;
                }

                // [소스 updateActiveParticle] vx *= 1 − .22·dt · vy −= .45·dt · x += vx·dt · y += vy·dt
                _hearts[i].Vx *= 1.0 - 0.22 * dt;
                _hearts[i].Vy -= 0.45 * dt;
                _hearts[i].X += _hearts[i].Vx * dt;
                _hearts[i].Y += _hearts[i].Vy * dt;
            }
        }

        private int LiveHearts(int owner)
        {
            int count = 0;

            for (int i = 0; i < _hearts.Length; i++)
                if (_hearts[i].Active && _hearts[i].Owner == owner)
                    count++;

            return count;
        }

        private void SpawnHeart(int owner, UndeadVec2 origin)
        {
            for (int i = 0; i < _hearts.Length; i++)
            {
                if (_hearts[i].Active)
                    continue;

                // [소스 createParticleSpawn] e = 2r−1 · x = e·(3+6r) · y = 8r−10 · vx = e·(1.5+3.5r) · vy = −(5+8r) · life 1100+500r · size .4+.1r
                //   ⚠ 여기 x·y·속도는 «컨테이너 안» 값이다 — 화면 값은 컨테이너 배율을 곱한 것이다.
                double e = 2.0 * RandomUtil.Value() - 1.0;
                _hearts[i] = new HeartParticle
                {
                    Active = true,
                    Owner = owner,
                    X = e * (3.0 + 6.0 * RandomUtil.Value()),
                    Y = 8.0 * RandomUtil.Value() - 10.0,
                    Vx = e * (1.5 + 3.5 * RandomUtil.Value()),
                    Vy = -(5.0 + 8.0 * RandomUtil.Value()),
                    LifeMs = 1100.0 + 500.0 * RandomUtil.Value(),
                    AgeMs = 0.0,
                    Size = 0.4 + 0.1 * RandomUtil.Value(),
                };
                return;
            }
        }

        private bool WriteHeart(int index, SpriteRenderer renderer)
        {
            HeartParticle h = _hearts[index];

            if (h.Active == false)
                return false;

            if (h.Owner >= _sim.Fireplaces.Count)
                return false;

            UndeadSimulation.Fireplace owner = _sim.Fireplaces[h.Owner];

            if (owner.Active == false)
                return false;

            double n = h.AgeMs / h.LifeMs;
            float size = (float)(h.Size * (1.0 + 0.08 * n));
            double worldX = owner.Position.X + h.X * HeartContainerScaleX;
            double worldY = owner.Position.Y + HeartEmitterY + h.Y * HeartContainerScaleY;
            renderer.sprite = _heartSet.Get(0, 0.0);
            renderer.transform.localPosition = UndeadUnits.ToPosition(worldX, worldY);
            renderer.transform.localScale = new Vector3((float)(size * HeartContainerScaleX), (float)(size * HeartContainerScaleY), 1f);
            renderer.color = new Color(1f, 1f, 1f, (float)(0.8 * (1.0 - n)));
            renderer.sortingOrder = SortOrder(worldY) + 1;
            return true;
        }

        /// <summary>보스·NPC 를 «켜고 끄고 옮긴다» — 조건이 곧 퀘스트 상태다.</summary>
        private void RenderBossQuest()
        {
            if (_boss != null)
            {
                _boss.enabled = _sim.BossActive;

                if (_sim.BossActive)
                {
                    _boss.sprite = _bossSet != null ? _bossSet.Get(0, _sim.BossAnimFrame) : _boss.sprite;
                    Place(_boss.transform, _sim.BossPosition, _boss);
                    _boss.sortingOrder = SortOrder(_sim.BossPosition.Y);
                }
            }

            // 가족은 «보스 퀘스트 차례»에 나타나고, 끝나면 사라진다 [소스 — startFadeOut]
            if (_family != null)
            {
                _family.enabled = _sim.CurrentQuest == EUndeadQuest.FirstBoss;

                if (_family.enabled)
                {
                    Place(_family.transform, _sim.FamilyPosition, _family);
                    _family.sortingOrder = SortOrder(_sim.FamilyPosition.Y);
                }
            }

            if (_farmer != null)
            {
                _farmer.enabled = _sim.CurrentQuest == EUndeadQuest.FarmerSheep;

                if (_farmer.enabled)
                {
                    Place(_farmer.transform, _sim.FarmerPosition, _farmer);
                    _farmer.sortingOrder = SortOrder(_sim.FarmerPosition.Y);
                }
            }

            if (_encounterSheep != null)
            {
                _encounterSheep.enabled = _sim.EncounterPhase == 1;

                if (_encounterSheep.enabled)
                {
                    Place(_encounterSheep.transform, _sim.EncounterSheepPosition, _encounterSheep);
                    _encounterSheep.sortingOrder = SortOrder(_sim.EncounterSheepPosition.Y);
                }
            }

            if (_encounterFold != null)
            {
                _encounterFold.enabled = _sim.EncounterPhase != 0;

                if (_encounterFold.enabled)
                {
                    Place(_encounterFold.transform, _sim.EncounterFoldPosition, _encounterFold);
                    _encounterFold.sortingOrder = SortOrder(_sim.EncounterFoldPosition.Y);
                }
            }

            if (_sheep == null)
                return;

            bool showSheep = _sim.FarmerPhase != UndeadSimulation.EFarmerPhase.WaitForHero
                             && _sim.CurrentQuest == EUndeadQuest.FarmerSheep;

            for (int i = 0; i < _sheep.Length && i < _sim.Sheep_.Count; i++)
            {
                _sheep[i].enabled = showSheep;

                if (showSheep == false)
                    continue;

                UndeadVec2 at = _sim.Sheep_[i].Position;
                Place(_sheep[i].transform, at, _sheep[i]);
                _sheep[i].sortingOrder = SortOrder(at.Y);
            }
        }

        private int OrbCount()
        {
            return _sim.LightningOrbs.Count;
        }

        private bool WriteOrb(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.LightningOrb o = _sim.LightningOrbs[index];

            if (o.Active == false || _orbSet == null)
                return false;

            renderer.sprite = _orbSet.Get(0, o.AnimFrame);
            Place(renderer.transform, o.Position, _orbSet);

            // 구슬은 «도는 방향»을 향한다 [소스 — rotation = angle + π/2]
            float degrees = (float)(-(o.Angle * Mathf.Rad2Deg + 90.0));
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            renderer.sortingOrder = SortOrder(o.Position.Y) + 1;
            return true;
        }

        private int GemCount()
        {
            return _sim.Gems.Count;
        }

        private bool WriteEnemy(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.Enemy e = _sim.Enemies[index];

            if (e.Active == false || _enemySets == null || _enemySets.Count == 0)
                return false;

            // ★ 종류마다 그림이 다르다 — <c>TypeId</c> 가 곧 인덱스다.
            UndeadSpriteSet set = _enemySets[e.TypeId >= 0 && e.TypeId < _enemySets.Count ? e.TypeId : 0];

            if (set == null)
                return false;

            renderer.sprite = set.Get(0, e.AnimFrame);
            Place(renderer.transform, e.Position, set);
            renderer.sortingOrder = SortOrder(e.Position.Y);

            // ★ 표시 배율은 <b>개체가 들고 있다</b> — 박쥐는 스폰 당시 웨이브만큼 커진 채로 산다 [소스 Ml].
            //   아트 표의 DisplayScale 은 «기본값»이고, 웨이브가 실린 값은 시뮬이 정한다.
            float spriteScale = e.SpriteScale > 0.0 ? (float)e.SpriteScale : Mathf.Abs((float)set.Data.DisplayScaleX);
            Vector3 scale = renderer.transform.localScale;
            scale.x = spriteScale;
            scale.y = spriteScale;
            renderer.transform.localScale = scale;

            // ★ 좌우 반전으로 방향을 낸다 [실측 — 원본 scale.x 가 −3 이었다]. 왼쪽 그림을 따로 굽지 않는다.
            if (set.Data.FlipsHorizontally)
            {
                bool faceLeft = e.Position.X > _sim.HeroPosition.X;
                Vector3 s = renderer.transform.localScale;
                s.x = Mathf.Abs(s.x) * (faceLeft ? -1f : 1f);
                renderer.transform.localScale = s;
            }

            return true;
        }

        private bool WriteProjectile(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.Projectile p = _sim.Projectiles[index];

            if (p.Active == false || _projectileSet == null)
                return false;

            renderer.sprite = _projectileSet.Get(0, p.AnimFrame);
            Place(renderer.transform, p.Position, _projectileSet);

            // 투사체는 «나아가는 쪽»을 본다. 원본 world 는 y 가 아래가 + 라 각도 부호가 뒤집힌다.
            double angle = Mathf.Atan2((float)-p.Velocity.Y, (float)p.Velocity.X) * Mathf.Rad2Deg;
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, (float)angle);
            renderer.sortingOrder = SortOrder(p.Position.Y) + 1;
            return true;
        }

        private bool WriteGem(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.Gem g = _sim.Gems[index];

            if (g.Active == false || _gemSet == null)
                return false;

            renderer.sprite = _gemSet.Get(0, g.AnimFrame);
            Place(renderer.transform, g.Position, _gemSet);
            renderer.sortingOrder = SortOrder(g.Position.Y) - 1;
            return true;
        }

        // ══════════════════════════════ 풀

        /// <summary>
        /// 개체 배열 하나를 풀에 붓는다. <b>남는 렌더러는 끄기만 하고 «파괴하지 않는다»</b> —
        /// 매 프레임 생성/파괴하면 개체가 수백일 때 GC 가 화면을 끊는다.
        /// </summary>
        private int Sync(List<SpriteRenderer> pool, Transform root, UndeadSpriteSet set,
                         int slotCount, System.Func<int, SpriteRenderer, bool> write)
        {
            if (set == null)
                return 0;

            int used = 0;

            for (int i = 0; i < slotCount; i++)
            {
                SpriteRenderer renderer = used < pool.Count ? pool[used] : null;

                if (renderer == null)
                {
                    renderer = NewRenderer(root, set);
                    pool.Add(renderer);
                }

                if (write(i, renderer) == false)
                    continue;

                if (renderer.enabled == false)
                    renderer.enabled = true;

                used++;
            }

            for (int i = used; i < pool.Count; i++)
            {
                if (pool[i].enabled)
                    pool[i].enabled = false;
            }

            return used;
        }

        private Transform NewRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        /// <summary>
        /// 단발·소수 개체의 <b>컷 진행</b> — 시간으로 돈다 [소스 <c>animationSpeed × 60 = fps</c>].
        ///
        /// <para>
        /// ⚠ <b>[사고]</b> 퀘스트 개체(전사·마법사·조각·가족·농부·양)는 <see cref="NewRenderer"/> 가 넣은
        /// <b>첫 컷 그대로</b> 서 있었다. 표에 컷 수와 fps 를 넣어도 <b>돌리는 곳이 없으면</b> 정지 그림이다 —
        /// 어떤 데이터 검사에도 안 걸린다(재발방지 <c>#131</c> 과 같은 뿌리).
        /// </para>
        ///
        /// <para>
        /// ⚠ 동시에 수십~수백 개가 도는 개체(적·투사체·보석)는 <b>여기를 쓰지 않는다</b> —
        /// 그쪽은 프레임 인덱스가 «개체 데이터»에 있고 중앙 틱이 진행시킨다 (확정표 G).
        /// </para>
        /// </summary>
        /// <summary>컷이 여럿이고 시뮬 프레임이 «없는» 개체 — <see cref="Render"/> 첫머리에서 진행시킨다.</summary>
        private readonly List<SpriteRenderer> _timedRenderers = new List<SpriteRenderer>(16);

        private readonly List<UndeadSpriteSet> _timedSets = new List<UndeadSpriteSet>(16);

        private void AnimateTimed()
        {
            for (int i = 0; i < _timedRenderers.Count; i++)
            {
                SpriteRenderer renderer = _timedRenderers[i];

                if (renderer == null || renderer.enabled == false)
                    continue;

                Animate(renderer, _timedSets[i]);
            }
        }

        private static void Animate(SpriteRenderer renderer, UndeadSpriteSet set)
        {
            if (renderer == null || set == null || set.Data.Fps <= 0.0)
                return;

            renderer.sprite = set.Get(0, Time.timeSinceLevelLoad * set.Data.Fps);
        }

        private SpriteRenderer NewRenderer(Transform parent, UndeadSpriteSet set)
        {
            var go = new GameObject(set.Data.Code);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = set.Get(0, 0.0);

            // ★ 표시 배율은 «개체마다 다르다» [실측] — 표에서 온다. 코드에 박지 않는다.
            go.transform.localScale = new Vector3((float)set.Data.DisplayScaleX, (float)set.Data.DisplayScaleY, 1f);

            if (ColorUtility.TryParseHtmlString("#" + set.Data.TintHex, out Color tint))
                renderer.color = new Color(tint.r, tint.g, tint.b, (float)set.Data.Alpha);

            // 컷이 여럿인데 시뮬이 프레임을 안 들고 있는 개체는 «시간»으로 돈다.
            // ⚠ 시뮬 프레임이 있는 개체(적·투사체·보석)는 뒤에서 덮어쓰므로 여기 있어도 무해하다 —
            //   AnimateTimed 가 Render 의 «첫머리»에서 돌기 때문이다.
            if (set.Data.Fps > 0.0 && set.Data.UsedCols > 1)
            {
                _timedRenderers.Add(renderer);
                _timedSets.Add(set);
            }

            return renderer;
        }

        /// <summary>피벗이 이미 스프라이트에 구워져 있으므로 «자리»만 옮긴다.</summary>
        private static void Place(Transform target, UndeadVec2 world, SpriteRenderer renderer)
        {
            target.position = UndeadUnits.ToPosition(world.X, world.Y);
        }

        private static void Place(Transform target, UndeadVec2 world, UndeadSpriteSet set)
        {
            target.localPosition = UndeadUnits.ToPosition(world.X, world.Y);
        }

        /// <summary>탑다운이라 <b>아래(=world y 가 큰 쪽)가 앞</b>이다.</summary>
        private static int SortOrder(double worldY)
        {
            return SortingBase - (int)worldY;
        }
    }
}
