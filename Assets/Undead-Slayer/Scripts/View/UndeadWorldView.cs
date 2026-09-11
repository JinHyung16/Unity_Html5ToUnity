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
        private readonly List<SpriteRenderer> _kunaiPool = new List<SpriteRenderer>(32);

        /// <summary>화염 자취 조각 [소스 <c>blazing_trail</c> 의 <c>segments</c>].</summary>
        private readonly List<SpriteRenderer> _trailPool = new List<SpriteRenderer>(128);
        private readonly List<SpriteRenderer> _gemPool = new List<SpriteRenderer>(256);
        private readonly List<SpriteRenderer> _orbPool = new List<SpriteRenderer>(16);
        private readonly List<SpriteRenderer> _fireballPool = new List<SpriteRenderer>(64);

        /// <summary>
        /// 말풍선 <b>바닥</b>이 목표 위로 뜨는 높이 [소스 — <c>helpBubbleY = −78</c>].
        /// <para>⚠ 회차 16 까지 <b>48 (미측정 · 눈으로 맞춘 값)</b> 이었다 — 소스를 읽어 바꿨다.</para>
        /// <para>⚠ 원본 말풍선 앵커는 <c>(0.5, 1)</c> 이고 우리 표의 피벗은 <c>(0.5, 0)</c> 다 — <b>둘 다 «바닥 기준»</b>이라 값이 그대로 온다.</para>
        /// </summary>
        private const double BubbleOffsetY = 78.0;

        /// <summary>말풍선 «안»에서 글자가 놓이는 자리 — <b>풍선 높이의 절반</b> [소스 — <c>text.y = −0.5 × sprite.height</c>].</summary>
        private const double BubbleTextCenterRatio = 0.5;

        /// <summary>말풍선이 <b>떠 있는 폭</b> [소스 <c>dc.update</c> — <c>visual.y = 5 × sin(0.004 × 경과ms)</c>].</summary>
        private const double BubbleBobAmplitude = 5.0;

        /// <summary>말풍선이 뜨는 «빠르기» (rad/초) [소스 — ms 기준 0.004 → 초 기준 4].</summary>
        private const double BubbleBobSpeed = 4.0;

        /// <summary>「고마워, 친구!」 말풍선은 «조금 더 위»다 [소스 — <c>thanksBubbleY = helpBubbleY − 20</c>].</summary>
        private const double ThanksBubbleExtraY = 20.0;

        /// <summary>구조 진행 링의 높이 — 전사 «발» 기준 [소스 — <c>actionProgress.y = −96</c>].</summary>
        private const double ActionProgressY = 96.0;
        /// <summary>
        /// 말풍선 글자의 <b>원본 크기</b> — <c>16</c> px 에서 시작해 상자에 들 때까지 <b>한 단씩</b> 내린다
        /// [소스 <c>fitTextToBubble</c>].
        /// </summary>
        private const float BubbleFontMaxOrigin = 16f;

        /// <summary>더 못 내려가는 바닥 — 보통 <c>12</c>, <c>medium</c> 풍선은 <c>10</c> 이다 [소스].</summary>
        private const float BubbleFontMinOrigin = 12f;

        private const float BubbleFontMinOriginMedium = 10f;

        /// <summary>
        /// 월드 <see cref="TextMeshPro"/> 의 <c>fontSize</c> 는 <b>단위의 1/10</b> 을 em 으로 쓴다 —
        /// 그래서 원본 px 를 단위로 바꾼 뒤 10 을 곱해야 «원본과 같은 글자 크기»가 된다.
        /// </summary>
        private const float TmpWorldFontScale = 10f;
        private const float HelpTextWidth = 6f;
        private const float HelpTextHeight = 1.6f;

        /// <summary>글자가 쓰는 폭·높이 — 풍선 대비 [소스 <c>applyTextValue</c> — 기본 .78/.75 · 큰 것 .72/.62].</summary>
        private const float TextWidthRatio = 0.78f;

        private const float TextHeightRatio = 0.75f;

        private const float MediumTextWidthRatio = 0.72f;

        private const float MediumTextHeightRatio = 0.62f;

        /// <summary>글자 색 [소스 — <c>fill: 2824720</c> = <c>#2B1A10</c>]. ⚠ 검정이 아니다.</summary>
        private static readonly Color BubbleTextColor = new Color(0x2B / 255f, 0x1A / 255f, 0x10 / 255f, 1f);

        // ── 체력바 [소스 — heroLivesBar]
        private const double LifeBarY = -60.0;
        private const double LifeSegmentWidth = 12.0;
        private const double LifeSegmentHeight = 6.0;

        /// <summary>칸 사이 간격 [소스 — <c>segmentSpacing = 0</c>]. ⚠ 회차 18 까지 1 이었다(지어낸 값).</summary>
        private const double LifeSegmentGap = 0.0;

        /// <summary>바 전체가 «왼쪽으로 3» 밀린다 [소스 — <c>pivot.set(0.5×width − 3, 0.5×height)</c>].</summary>
        private const double LifeBarPivotShiftX = 3.0;

        private const double LifeFadeIn = 0.15;
        private const double LifeFadeOut = 0.4;
        private const double LifeFadeOutDelay = 1.2;

        /// <summary>칸이 <b>사라지는</b> 시간 (초) [소스 — <c>300ms</c>].</summary>
        private const double LifeSegmentVanish = 0.3;

        /// <summary>칸이 <b>돋아나는</b> 시간 (초) [소스 — <c>200ms</c>].</summary>
        private const double LifeSegmentAppear = 0.2;

        private Transform _enemyRoot;
        private Transform _projectileRoot;
        private Transform _kunaiRoot;
        private Transform _trailRoot;

        /// <summary>NPC 말풍선 — <b>한 벌을 돌려 쓴다</b>. 원본에서 마법사·가족·농부는 «다른 퀘스트»라 동시에 안 뜬다.</summary>
        private SpriteRenderer _npcBubble;

        private TextMeshPro _npcBubbleText;
        private UndeadSpriteSet _trailSet;
        private Transform _gemRoot;
        private SpriteRenderer _hero;
        /// <summary>체력바 — 히어로 «위»에 붙는 칸들 [소스 — y −60 · 12×6 · 여백 1].</summary>
        /// <summary>어두운 <b>바탕</b> — 목숨이 줄어도 남는다 [소스 — 칸마다 배경 + 채움 두 겹].</summary>
        private SpriteRenderer[] _lifeSegments;

        /// <summary>빨간 <b>채움</b> — 목숨 수만큼만 보이고, 줄 때 «떨어지며» 사라진다.</summary>
        private SpriteRenderer[] _lifeFills;

        /// <summary>칸마다의 연출 진행 (초). 양수면 돋아나는 중 · 음수면 사라지는 중 · 0 이면 정지.</summary>
        private double[] _lifeFillPhase;

        /// <summary>지난 프레임의 목숨 수 — 늘고 줆을 «전이»로 잡는다.</summary>
        private int _lifeLastHp = -1;

        private Transform _lifeRoot;
        private double _lifeFadeSeconds;

        private Transform _questRoot;
        private SpriteRenderer _questWarrior;

        /// <summary>전사는 <b>모드마다 시트가 다르다</b> [소스 <c>loadFrames</c>] — lay 4컷 · idle 4컷 · run 8컷.</summary>
        private UndeadSpriteSet _warriorLaySet;

        private UndeadSpriteSet _warriorIdleSet;

        private UndeadSpriteSet _warriorRunSet;
        private SpriteRenderer _questBubble;
        private SpriteRenderer _questMage;
        private SpriteRenderer[] _questFragments;

        /// <summary>
        /// 조각의 <b>후광</b>과 마법사 앞 <b>조각 자리</b> — 원본이 <c>Graphics</c> 로 그리는 도형이다
        /// [소스 <c>halo.circle(0,0,18)</c> · <c>createSlots</c>].
        /// <para>⚠ 채움과 테두리는 <b>색도 알파도 다르다</b> — 한 장으로 못 그린다.</para>
        /// </summary>
        private SpriteRenderer[] _fragmentHaloFill;
        private SpriteRenderer[] _fragmentHaloEdge;
        private SpriteRenderer[] _mageSlotFill;
        private SpriteRenderer[] _mageSlotEdge;

        // [소스 halo.fill({color: 7391743, alpha: .15}) · stroke({color: 14678015, alpha: .95})]
        private static readonly Color HaloFill = Hex(0x70C9FF);
        private static readonly Color HaloEdge = Hex(0xDFF7FF);
        private const float HaloFillAlpha = 0.15f;
        private const float HaloEdgeAlpha = 0.95f;

        /// <summary>후광이 숨 쉬는 폭·속도 [소스 <c>1 + .08·sin(.006t)</c>].</summary>
        private const double HaloPulse = 0.08;
        private const double HaloPulseSpeed = 0.006;

        // [소스 slot.fill({color: 1651274, alpha: .85}) · stroke({color: 14217471})]
        private static readonly Color SlotFill = Hex(0x19324A);
        private static readonly Color SlotEdge = Hex(0xD8F0FF);
        private const float SlotFillAlpha = 0.85f;

        /// <summary>조각 배율은 <b>상태마다 다르다</b> [소스 worldScale · heroScale · mageScale].</summary>
        private const double FragmentWorldScale = 2.8;
        private const double FragmentWorldScalePulse = 0.14;
        private const double FragmentWorldScaleSpeed = 0.007;
        private const double FragmentHeroScale = 2.55;
        private const double FragmentMageScale = 2.35;

        /// <summary>조각은 <b>계속 돈다</b> [소스 <c>sprite.rotation += .002·t</c>] — 라디안/ms 다.</summary>
        private const double FragmentSpinPerMs = 0.002;

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
        private TMP_Text _questHelpText;

        private UndeadSimulation _sim;
        private UndeadSpriteSet _heroSet;
        /// <summary>
        /// <b>적 종류마다 한 벌</b> — 시뮬의 <c>TypeId</c> 가 이 배열의 인덱스다 [회차 9].
        /// <para>⚠ 한 벌만 두면 <b>웨이브 3 의 박쥐가 좀비 그림으로</b> 나온다.</para>
        /// </summary>
        private IReadOnlyList<UndeadSpriteSet> _enemySets;
        private UndeadSpriteSet _projectileSet;
        private UndeadSpriteSet _kunaiSet;
        private UndeadSpriteSet _bubbleSet;
        private UndeadSpriteSet _bubbleMediumSet;
        private UndeadSpriteSet _questProgressSet;
        private SpriteRenderer _questProgress;

        /// <summary>
        /// 전사가 하는 말 — 순서는 <b>도와줘 · 고마워 · 마법사 예고 · 첫 보스 예고 · 농부 예고</b>다.
        /// <para>⚠ 한국어를 뷰에 박지 않는다 — <b>문구 표</b>에서 온다 (<c>UndeadGameInitialize</c>).</para>
        /// </summary>
        private string[] _warriorSpeechTexts;
        private UndeadSpriteSet _gemSet;
        private UndeadSpriteSet _orbSet;
        private Transform _orbRoot;
        private UndeadSpriteSet _fireballSet;
        private Transform _fireballRoot;

        // ── 퀘스트 3·4 의 개체들. 하나씩뿐이라 풀이 아니다.
        private SpriteRenderer _boss;
        /// <summary>
        /// 가족은 원본이 <b>«세 명»</b>이다 [소스 <c>createMembers</c>] —
        /// <c>family_npc_1/2/3</c> 이 각기 다른 그림으로 <c>(−30,0)·(30,0)·(0,26)</c> 에 선다.
        /// <para>⚠ 한 장을 한 자리에 두면 «가족»이 아니라 «사람 하나»가 된다.</para>
        /// </summary>
        private SpriteRenderer[] _family;

        private UndeadSpriteSet[] _familySets;

        /// <summary>[소스] 그룹 기준 상대 자리. 순서는 <c>family_npc_1 → 2 → 3</c> 이다.</summary>
        private static readonly UndeadVec2[] FamilyOffsets =
        {
            new UndeadVec2(-30.0, 0.0),
            new UndeadVec2(30.0, 0.0),
            new UndeadVec2(0.0, 26.0),
        };
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
        private readonly List<SpriteRenderer> _sparklePool = new List<SpriteRenderer>(64);
        private Transform _sparkleRoot;

        /// <summary>
        /// 모닥불 위로 떠오르는 하트 [소스 <c>Gl</c>] · 사악한 나무의 반짝임 [소스 <c>hd</c>] —
        /// <b>둘 다 <see cref="UndeadParticleEmitter"/> 한 틀</b>이고 «개체마다 하나»다.
        ///
        /// <para>
        /// ⚠ 슬롯은 <b>청크가 바뀌면 다른 나무·모닥불이 들어온다</b> — 그래서 «어느 칸의 것인가»를 같이 들고
        /// 칸이 바뀌면 이미터를 새로 만든다. 안 그러면 <b>남의 불티가 새 나무에서 계속 튄다</b>.
        /// </para>
        /// </summary>
        private UndeadHeartEmitter[] _heartEmitters = new UndeadHeartEmitter[0];
        private UndeadSparkleEmitter[] _treeSparkles = new UndeadSparkleEmitter[0];
        private long[] _heartEmitterTiles = new long[0];
        private long[] _treeSparkleTiles = new long[0];
        private bool[] _treeWasBursted = new bool[0];

        /// <summary>모닥불 이미터가 서는 높이 [소스 <c>heartsParticles.y = −20</c>].</summary>
        private const double HeartEmitterY = -20.0;

        /// <summary>나무 이미터가 서는 높이 [소스 <c>sparkles.y = −100</c>].</summary>
        private const double TreeSparkleEmitterY = -100.0;

        /// <summary>나무가 터질 때 한꺼번에 뿜는 알 [소스 <c>emitBurst(20, 1)</c>].</summary>
        private const int TreeBurstSparkles = 20;

        /// <summary>이번 프레임에 그릴 입자 — <b>(주인, 알)</b> 쌍을 평평하게 편 것이다.</summary>
        private readonly List<int> _heartDraw = new List<int>(96);
        private readonly List<int> _sparkleDraw = new List<int>(256);
        private SpriteRenderer[] _sheep;
        private UndeadSpriteSet _bossSet;
        private SpriteRenderer _encounterSheep;
        private SpriteRenderer _encounterFold;

        /// <summary>지금 화면에 켜져 있는 렌더러 수 — <b>검사가 이 값을 센다</b>.</summary>
        public int ActiveRendererCount { get; private set; }

        /// <summary>풀이 만든 렌더러 총수. <b>개체 수에 비례해 «컴포넌트»가 늘지 않는지</b>를 보는 값이다.</summary>
        public int PooledRendererCount
        {
            get { return _enemyPool.Count + _projectilePool.Count + _kunaiPool.Count + _gemPool.Count + (_hero != null ? 1 : 0); }
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
            _graveyardObject = new[] { tree, treeActivated, treeInactive };
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
            _sparkleRoot = NewRoot("Sparkles");

            if (tree == null || treeActivated == null || treeInactive == null || meteor == null || fire == null || fireInactive == null || heart == null)
                Log.Error("월드 오브젝트 스프라이트가 비었다 — 나무·유성·모닥불·하트 중 하나가 화면에 안 나온다");
        }

        /// <summary>
        /// 겨울 바이옴의 <b>중형 오브젝트(눈사람)</b> — 나무와 <b>같은 세 상태</b>다
        /// [소스 <c>2===biome ? "snowman..." : "graveyard_evil_tree..."</c>].
        /// </summary>
        public void BindWinterObjects(UndeadSpriteSet snowman, UndeadSpriteSet activated, UndeadSpriteSet inactive)
        {
            _winterObject = new[] { snowman, activated, inactive };

            if (snowman == null || activated == null || inactive == null)
                Log.Error("눈사람 시트가 비었다 — 바이옴 2 에서 중형 오브젝트가 안 보인다");
        }

        /// <summary>
        /// 바이옴을 바꾼다 — <b>중형 오브젝트 그림만</b> 갈린다 (규칙·자리는 같은 시뮬이 낸다).
        /// <para>⚠ 겨울 벌이 없으면 <b>묘지 그림을 그대로 쓴다</b> — 조용히 비는 것보다 낫다.</para>
        /// </summary>
        public void SetBiome(int biome)
        {
            Biome = biome == 2 ? 2 : 1;

            UndeadSpriteSet[] set = Biome == 2 && _winterObject != null && _winterObject[0] != null
                ? _winterObject
                : _graveyardObject;

            if (set == null)
                return;

            _treeSet = set[0];
            _treeActivatedSet = set[1];
            _treeInactiveSet = set[2];
        }

        /// <summary>지금 그리는 바이옴 — <b>검사가 이 값을 본다</b>.</summary>
        public int Biome { get; private set; } = 1;

        private UndeadSpriteSet[] _graveyardObject;
        private UndeadSpriteSet[] _winterObject;

        /// <summary>번개 구슬 — 히어로 둘레를 도는 궤도 무기 [소스].</summary>
        public void BindLightning(UndeadSpriteSet orb)
        {
            _orbSet = orb;
            _orbRoot = NewRoot("Lightning");
        }

        /// <summary>
        /// 화염 자취 [소스 <c>blazing_trail</c>].
        /// <para>⚠ 세트가 없으면 시뮬은 조각을 남기는데 <b>화면에는 아무것도 안 나온다</b> — 시끄럽게 알린다.</para>
        /// </summary>
        public void BindTrail(UndeadSpriteSet trail)
        {
            _trailSet = trail;
            _trailRoot = NewRoot("Trail");

            if (trail == null)
                Log.Error("화염 자취 스프라이트 세트가 없다 — 스킬이 도는데 화면에 안 보인다");
        }

        /// <summary>
        /// 퀘스트 3·4 의 개체 — <b>보스 · 불덩이 · 가족 · 농부 · 양 · 모닥불</b> [소스].
        /// <para>보스와 NPC 는 판마다 하나씩이라 풀을 만들지 않는다. 불덩이만 풀이다.</para>
        /// </summary>
        public void BindBossQuest(UndeadSpriteSet boss, UndeadSpriteSet fireball, UndeadSpriteSet[] family,
                                  UndeadSpriteSet farmer, UndeadSpriteSet sheep)
        {
            Transform root = NewRoot("BossQuest");
            _bossSet = boss;
            _fireballSet = fireball;
            _fireballRoot = NewRoot("Fireballs");

            if (boss != null)
                _boss = NewRenderer(root, boss);

            if (family != null)
            {
                _familySets = family;
                _family = new SpriteRenderer[family.Length];

                for (int i = 0; i < family.Length; i++)
                    _family[i] = family[i] == null ? null : NewRenderer(root, family[i]);
            }

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
        /// <summary>
        /// 체력바 — <b>칸마다 «두 겹»</b>이다 [소스 <c>rebuildSegments</c>].
        /// <para>어두운 바탕은 «늘 있고», 빨간 채움만 목숨 수를 따라 돋아나고 사라진다.</para>
        /// <para>⚠ [사고] 한 장으로 두고 «색만 갈아» 칠했더니, 목숨이 줄면 칸의 테두리까지 같이 어두워졌다.</para>
        /// </summary>
        public void BindLifeBar(UndeadSpriteSet background, UndeadSpriteSet fill, int maxHp)
        {
            if (background == null || maxHp <= 0)
                return;

            if (fill == null)
                Log.Error("체력 칸 «채움»(hp_segment_fill) 세트가 없다 — 남은 목숨이 화면에 안 보인다");

            _lifeRoot = NewRoot("LifeBar");
            _lifeSegments = new SpriteRenderer[maxHp];
            _lifeFills = new SpriteRenderer[maxHp];
            _lifeFillPhase = new double[maxHp];

            for (int i = 0; i < maxHp; i++)
            {
                _lifeSegments[i] = NewRenderer(_lifeRoot, background);
                _lifeSegments[i].gameObject.SetActive(true);
                _lifeSegments[i].sortingOrder = SortingBase * 2;

                if (fill == null)
                    continue;

                _lifeFills[i] = NewRenderer(_lifeRoot, fill);
                _lifeFills[i].gameObject.SetActive(true);
                _lifeFills[i].sortingOrder = SortingBase * 2 + 1;
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
        public void BindQuestTarget(UndeadSpriteSet warriorLay, UndeadSpriteSet warriorIdle,
                                    UndeadSpriteSet warriorRun, UndeadSpriteSet bubble,
                                    UndeadSpriteSet mage, UndeadSpriteSet fragment,
                                    UndeadSpriteSet kunai, UndeadSpriteSet bubbleMedium,
                                    UndeadSpriteSet questProgress,
                                    string[] speechTexts, string[] npcSpeechTexts, TMP_FontAsset font)
        {
            if (_sim == null)
                return;

            _questRoot = NewRoot("Quest");

            _warriorLaySet = warriorLay;
            _warriorIdleSet = warriorIdle;
            _warriorRunSet = warriorRun;

            // ★ 쿠나이는 «구조된 전사»가 던진다 [소스 throwKunais] — 풀이라 루트만 만들어 둔다.
            //   ⚠ 세트가 없으면 시뮬은 날리는데 화면에는 아무것도 안 나온다 — 시끄럽게 알린다.
            _kunaiSet = kunai;
            _kunaiRoot = NewRoot("Kunais");

            if (kunai == null)
                Log.Error("쿠나이 스프라이트 세트가 없다 — 전사가 던지는 것이 화면에 안 나온다");

            // ⚠ 모드마다 시트를 갈아끼우므로 «시간으로 도는 목록»에 넣지 않는다 — 아래에서 직접 진행시킨다
            if (warriorLay != null)
                _questWarrior = NewRenderer(_questRoot, warriorLay, timed: false);

            // ★ 말풍선은 «갈래마다 그림이 다르다» [소스 getTextureAlias] — 예고는 큰 것(bubble_medium)이다.
            //   ⚠ 개체는 «하나»다. 갈래마다 렌더러를 두면 둘이 겹쳐 뜬다.
            _bubbleSet = bubble;
            _bubbleMediumSet = bubbleMedium;
            _warriorSpeechTexts = speechTexts;

            if (bubbleMedium == null)
                Log.Error("큰 말풍선(bubble_medium) 세트가 없다 — 전사의 과제 예고가 작은 풍선으로 뜬다");

            // ★ 구조 진행 링 — «시간»이 아니라 «진행률»이 컷을 고른다 [소스 getTextureNameForProgress]
            _questProgressSet = questProgress;

            if (questProgress != null)
                _questProgress = NewRenderer(_questRoot, questProgress, timed: false);
            else
                Log.Error("구조 진행 링(quest_progress) 세트가 없다 — 4초를 눈금 없이 기다리게 된다");

            // 갈래마다 시트를 갈아끼우므로 «시간으로 도는 목록»에 넣지 않는다 — RenderWarriorSpeech 가 직접 준다
            if (bubble != null)
                _questBubble = NewRenderer(_questRoot, bubble, timed: false);

            if (mage != null)
                _questMage = NewRenderer(_questRoot, mage);

            if (fragment != null)
            {
                _questFragments = new SpriteRenderer[2];
                _questFragments[0] = NewRenderer(_questRoot, fragment);
                _questFragments[1] = NewRenderer(_questRoot, fragment);

                // ★ 후광은 조각 «뒤», 자리 원은 마법사 «뒤»에 깔린다 [소스 — addChild 순서]
                _fragmentHaloFill = new SpriteRenderer[2];
                _fragmentHaloEdge = new SpriteRenderer[2];
                _mageSlotFill = new SpriteRenderer[2];
                _mageSlotEdge = new SpriteRenderer[2];

                for (int i = 0; i < 2; i++)
                {
                    float halo = (float)UndeadSimulation.FragmentHaloRadius;
                    float slot = (float)UndeadSimulation.MageSlotRadius;

                    _fragmentHaloFill[i] = NewShape($"fragmentHalo{i}Fill", UndeadShapes.Disc(halo));
                    _fragmentHaloEdge[i] = NewShape($"fragmentHalo{i}Edge", UndeadShapes.Ring(halo));
                    _mageSlotFill[i] = NewShape($"mageSlot{i}Fill", UndeadShapes.Disc(slot));
                    _mageSlotEdge[i] = NewShape($"mageSlot{i}Edge", UndeadShapes.Ring(slot));
                }
            }

            if (speechTexts == null || speechTexts.Length == 0)
            {
                Log.Error("전사의 말 문구가 없다 — 말풍선이 빈 채로 뜬다");
                return;
            }

            // ── NPC 말풍선 한 벌 [소스 — 마법사·가족·농부. 셋은 «다른 퀘스트»라 동시에 안 뜬다]
            if (_bubbleMediumSet != null)
            {
                _npcBubble = NewRenderer(_questRoot, _bubbleMediumSet, timed: false);

                var npcTextObject = new GameObject("NpcSpeechText");
                npcTextObject.transform.SetParent(_questRoot, false);
                _npcBubbleText = npcTextObject.AddComponent<TextMeshPro>();

                if (font != null)
                    _npcBubbleText.font = font;

                _npcBubbleText.color = BubbleTextColor;
                _npcBubbleText.enableWordWrapping = true;
                _npcBubbleText.alignment = TextAlignmentOptions.Center;
                _npcBubbleText.fontSize = BubbleFontSize(BubbleFontMaxOrigin);
                _npcBubble.enabled = false;
                _npcBubbleText.enabled = false;
            }

            _npcSpeechTexts = npcSpeechTexts;

            var textObject = new GameObject("HelpText");
            textObject.transform.SetParent(_questRoot, false);

            _questHelpText = textObject.AddComponent<TextMeshPro>();

            if (font != null)
                _questHelpText.font = font;

            _questHelpText.text = speechTexts[0];
            _questHelpText.fontSize = BubbleFontSize(BubbleFontMaxOrigin);
            _questHelpText.color = BubbleTextColor;
            _questHelpText.enableWordWrapping = true;
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
                // ⚠ <b>QuestActive 로 가르지 않는다</b> — 과제와 과제 «사이»(예고 대기)에는
                //   CurrentQuest 가 비어 그때 전사가 통째로 사라진다. 놓였으면 계속 보인다 [소스 spawnWarrior].
                _questWarrior.enabled = _sim.WarriorSpawned;

                // ★ 모드마다 «다른 시트»다 [소스 setMode] — 예전에는 누운 그림 하나로 따라다녔다
                if (_questWarrior.enabled)
                {
                    ApplySet(_questWarrior, WarriorSet());

                    // ★ 가는 쪽을 본다 [소스 updateFollow — scale.x 부호를 뒤집는다]
                    ApplyFacing(_questWarrior.transform, _sim.WarriorFacingLeft);
                }
            }

            RenderWarriorSpeech();
            RenderNpcSpeech();
            RenderRescueRing();

            // ── 마법사. 전사를 구한 «뒤»에 나타난다 [소스 — 퀘스트가 순차다]
            if (_questMage != null)
            {
                // ⚠ 구조 «직후»가 아니다 — 전사가 5초 뒤 예고를 마쳐야 마법사 과제가 시작된다 [소스].
                _questMage.enabled = _sim.MageQuestStarted;

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
            SetFragment(0, _questFragments[0], showFragments, _sim.Fragment0Position, _sim.Fragment0State);
            SetFragment(1, _questFragments[1], showFragments, _sim.Fragment1Position, _sim.Fragment1State);
            RenderMageSlots();
        }

        /// <summary>
        /// 전사의 말풍선 — <b>갈래마다 그림·자리·문구가 갈린다</b> [소스 <c>dc.setText</c> · <c>getTextureAlias</c>].
        ///
        /// <para>
        /// | 갈래 | 그림 | 자리 | 언제 |<br/>
        /// | 도와줘 | <c>bubble</c> | <c>helpBubbleY</c> | 구조 «전» |<br/>
        /// | 고마워 | <c>bubble</c> | <c>helpBubbleY − 20</c> | 구조 직후 2초 |<br/>
        /// | 과제 예고 | <b><c>bubble_medium</c></b> | <c>helpBubbleY</c> | 다음 과제가 열리기 «전» |
        /// </para>
        /// </summary>
        private void RenderWarriorSpeech()
        {
            EUndeadWarriorSpeech speech = _sim.WarriorSpawned
                ? _sim.WarriorSpeech
                : EUndeadWarriorSpeech.None;

            bool show = speech != EUndeadWarriorSpeech.None;
            double offsetY = BubbleOffsetY
                             + (speech == EUndeadWarriorSpeech.Thanks ? ThanksBubbleExtraY : 0.0);

            // ★ 말풍선은 «위아래로 뜬다» [소스 dc.update]. 풍선과 글자가 «같이» 움직인다 — 한 컨테이너다.
            //   ⚠ 시계는 «시뮬»에서 받는다 — 뷰가 자기 시계로 흔들면 «정지 중에도» 흔들린다.
            if (speech == _lastSpeech)
                _bubbleElapsed = _sim.ElapsedSeconds - _bubbleStartedAt;
            else
            {
                _lastSpeech = speech;
                _bubbleStartedAt = _sim.ElapsedSeconds;
                _bubbleElapsed = 0.0;
            }

            offsetY -= BubbleBobAmplitude * Math.Sin(BubbleBobSpeed * _bubbleElapsed);

            bool medium = speech == EUndeadWarriorSpeech.QuestIntro && _bubbleMediumSet != null;
            UndeadSpriteSet set = medium ? _bubbleMediumSet : _bubbleSet;

            if (_questBubble != null)
            {
                _questBubble.enabled = show;

                if (show)
                {
                    ApplySet(_questBubble, set);

                    var at = new UndeadVec2(_sim.WarriorPosition.X, _sim.WarriorPosition.Y - offsetY);
                    Place(_questBubble.transform, at, _questBubble);
                    _questBubble.sortingOrder = SortOrder(_sim.WarriorPosition.Y) + 1;
                }
            }

            if (_questHelpText == null)
                return;

            _questHelpText.enabled = show;

            if (show == false)
                return;

            _questHelpText.text = SpeechTextOf(speech);

            // ★ 글자는 풍선 «가운데»다 — 풍선이 커지면 글자도 같이 올라간다 [소스 applyTextValue].
            //   ⚠ 고정 오프셋으로 두면 큰 풍선에서 글자가 바닥에 붙는다.
            double width = set != null ? set.Data.FrameWidth * set.Data.DisplayScaleX : 0.0;
            double height = set != null ? set.Data.FrameHeight * set.Data.DisplayScaleY : 0.0;

            float boxW = UndeadUnits.ToUnits(width * (medium ? MediumTextWidthRatio : TextWidthRatio));
            float boxH = UndeadUnits.ToUnits(height * (medium ? MediumTextHeightRatio : TextHeightRatio));
            _questHelpText.rectTransform.sizeDelta = new Vector2(boxW, boxH);

            FitBubbleText(_questHelpText, boxW, boxH, medium);

            _questHelpText.transform.position = UndeadUnits.ToPosition(
                _sim.WarriorPosition.X, _sim.WarriorPosition.Y - offsetY - height * BubbleTextCenterRatio);
        }

        /// <summary>
        /// 구조 진행 링 [소스 <c>class Ad</c>] — 쓰러진 전사 곁에 서 있는 <b>4초 동안</b> 차오른다.
        ///
        /// <para>
        /// ★ 컷은 <b>진행률</b>이 고른다 — <c>0</c> 이면 첫 컷, 그 밖에는 <c>ceil(t × 8)</c> 이다
        /// [소스 <c>getTextureNameForProgress</c>]. <b>시간으로 도는 애니메이션이 아니다.</b>
        /// </para>
        ///
        /// <para>⚠ 링이 뜨는 동안 말풍선은 숨는다 — 그건 시뮬이 <see cref="EUndeadWarriorSpeech"/> 로 정한다.</para>
        /// </summary>
        private void RenderRescueRing()
        {
            if (_questProgress == null)
                return;

            bool show = _sim.WarriorSpawned && _sim.QuestRescued == false && _sim.RescueSeconds > 0.0;
            _questProgress.enabled = show;

            if (show == false)
                return;

            int last = Mathf.Max(0, _questProgressSet.Data.Cols - 1);
            double progress = _sim.RescueProgress;
            int cut = progress <= 0.0
                ? 0
                : Mathf.Min(last, Mathf.CeilToInt((float)progress * last));

            // ⚠ Get 은 (행, 컷)이다 — 링은 «한 줄에 9컷»이라 행이 0 이다
            _questProgress.sprite = _questProgressSet.Get(0, cut);

            var at = new UndeadVec2(_sim.WarriorPosition.X, _sim.WarriorPosition.Y - ActionProgressY);
            Place(_questProgress.transform, at, _questProgress);
            _questProgress.sortingOrder = SortOrder(_sim.WarriorPosition.Y) + 2;
        }

        private EUndeadWarriorSpeech _lastSpeech = EUndeadWarriorSpeech.None;
        private double _bubbleStartedAt;
        private double _bubbleElapsed;

        /// <summary>지금 할 말 — <b>예고</b>는 «어느 과제를 예고 중인지»가 문구를 정한다 [소스 <c>bubbleTextKey</c>].</summary>
        /// <summary>
        /// 마법사·가족·농부의 말풍선 [소스 <c>showBubble</c> · <c>introductionText</c>].
        ///
        /// <para>
        /// 높이는 개체마다 다르다 — <b>마법사 −125 · 가족 −140 · 농부 −70</b> [소스].
        /// 셋 다 <b><c>medium</c> 풍선</b>이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>[사고 · #174]</b> 문구 표에 여섯 줄이 다 있었는데 <b>읽는 곳이 없어</b>
        /// 세 NPC 가 화면에서 <b>아무 말도 안 했다</b>. 「키가 다 있나」만 보는 감사로는 안 잡힌다.
        /// </para>
        /// </summary>
        private void RenderNpcSpeech()
        {
            if (_npcBubble == null || _npcBubbleText == null || _bubbleMediumSet == null)
                return;

            EUndeadNpcSpeech speech = EUndeadNpcSpeech.None;
            UndeadVec2 at = UndeadVec2.Zero;
            double offsetY = 0.0;

            if (_sim.MageSpeech != EUndeadNpcSpeech.None)
            {
                speech = _sim.MageSpeech;
                at = _sim.MagePosition;
                offsetY = MageBubbleY;
            }
            else if (_sim.FamilySpeech != EUndeadNpcSpeech.None)
            {
                speech = _sim.FamilySpeech;
                at = _sim.FamilyPosition;
                offsetY = FamilyBubbleY;
            }
            else if (_sim.FarmerSpeech != EUndeadNpcSpeech.None)
            {
                speech = _sim.FarmerSpeech;
                at = _sim.FarmerPosition;
                offsetY = FarmerBubbleY;
            }

            bool show = speech != EUndeadNpcSpeech.None;
            _npcBubble.enabled = show;
            _npcBubbleText.enabled = show;

            if (show == false)
                return;

            ApplySet(_npcBubble, _bubbleMediumSet);

            var bubbleAt = new UndeadVec2(at.X, at.Y + offsetY);
            Place(_npcBubble.transform, bubbleAt, _npcBubble);
            _npcBubble.sortingOrder = SortOrder(at.Y) + 1;

            _npcBubbleText.text = NpcSpeechTextOf(speech);

            double width = _bubbleMediumSet.Data.FrameWidth * _bubbleMediumSet.Data.DisplayScaleX;
            double height = _bubbleMediumSet.Data.FrameHeight * _bubbleMediumSet.Data.DisplayScaleY;
            float boxW = UndeadUnits.ToUnits(width * MediumTextWidthRatio);
            float boxH = UndeadUnits.ToUnits(height * MediumTextHeightRatio);

            _npcBubbleText.rectTransform.sizeDelta = new Vector2(boxW, boxH);
            FitBubbleText(_npcBubbleText, boxW, boxH, true);

            _npcBubbleText.transform.position = UndeadUnits.ToPosition(
                at.X, at.Y + offsetY - height * BubbleTextCenterRatio);

            _npcBubbleText.sortingOrder = _npcBubble.sortingOrder + 1;
        }

        /// <summary>말풍선 높이 [소스 — 개체마다 다르다].</summary>
        private const double MageBubbleY = -125.0;

        private const double FamilyBubbleY = -140.0;

        private const double FarmerBubbleY = -70.0;

        private string NpcSpeechTextOf(EUndeadNpcSpeech speech)
        {
            int index = (int)speech - 1;

            if (_npcSpeechTexts == null || index < 0 || index >= _npcSpeechTexts.Length)
            {
                Log.Error($"NPC 문구가 없다 — {speech}");
                return string.Empty;
            }

            return _npcSpeechTexts[index];
        }

        private string[] _npcSpeechTexts;

        private string SpeechTextOf(EUndeadWarriorSpeech speech)
        {
            if (_warriorSpeechTexts == null)
                return string.Empty;

            int index;

            switch (speech)
            {
                case EUndeadWarriorSpeech.Help:
                    index = 0;
                    break;

                case EUndeadWarriorSpeech.Thanks:
                    index = 1;
                    break;

                case EUndeadWarriorSpeech.QuestIntro:
                    index = _sim.PendingQuest == EUndeadQuest.Mage ? 2
                          : _sim.PendingQuest == EUndeadQuest.FirstBoss ? 3
                          : 4;
                    break;

                default:
                    return string.Empty;
            }

            return index < _warriorSpeechTexts.Length ? _warriorSpeechTexts[index] : string.Empty;
        }

        /// <summary>
        /// 조각 하나 — <b>배율이 상태마다 다르고</b>(세상 2.8 · 손 2.55 · 마법사 2.35) <b>계속 돈다</b> [소스].
        /// <para>⚠ 표의 표시 배율(2.8)은 «세상에 놓였을 때» 값이다 — 손에 들리면 여기서 덮어쓴다.</para>
        /// </summary>
        private void SetFragment(int index, SpriteRenderer renderer, bool show, UndeadVec2 at, int state)
        {
            if (renderer == null)
                return;

            renderer.enabled = show;
            SetShape(_fragmentHaloFill[index], show, at, HaloFill, HaloFillAlpha);
            SetShape(_fragmentHaloEdge[index], show, at, HaloEdge, HaloEdgeAlpha);

            if (show == false)
                return;

            double float01 = _sim.FragmentFloatMs;
            double scale = state == 1 ? FragmentHeroScale
                : state == 2 ? FragmentMageScale
                : FragmentWorldScale + FragmentWorldScalePulse * Math.Sin(FragmentWorldScaleSpeed * float01);

            Place(renderer.transform, at, renderer);
            renderer.transform.localScale = new Vector3((float)scale, (float)scale, 1f);
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, (float)(-FragmentSpinPerMs * float01 * Mathf.Rad2Deg));
            renderer.sortingOrder = SortOrder(at.Y) + 2;

            // 후광은 조각과 «따로» 숨 쉰다 [소스 halo.scale.set(1 + .08·sin(.006t))]
            float pulse = (float)(1.0 + HaloPulse * Math.Sin(HaloPulseSpeed * float01));
            _fragmentHaloFill[index].transform.localScale = new Vector3(pulse, pulse, 1f);
            _fragmentHaloEdge[index].transform.localScale = new Vector3(pulse, pulse, 1f);
            _fragmentHaloFill[index].sortingOrder = SortOrder(at.Y) + 1;
            _fragmentHaloEdge[index].sortingOrder = SortOrder(at.Y) + 1;
        }

        /// <summary>
        /// 마법사 앞 <b>조각 자리</b> 둘 — <b>만난 뒤부터</b> 보이고, 둘 다 꽂히면 <b>서서히 사라진다</b> [소스].
        /// </summary>
        private void RenderMageSlots()
        {
            if (_mageSlotFill == null)
                return;

            bool show = _sim.MageMet && _sim.MageCompleted == false && _sim.MageSlotAlpha > 0.0;
            float alpha = (float)_sim.MageSlotAlpha;

            for (int i = 0; i < 2; i++)
            {
                double sign = i == 0 ? -1.0 : 1.0;
                var at = new UndeadVec2(_sim.MagePosition.X + sign * UndeadSimulation.MageSlotOffsetX,
                                        _sim.MagePosition.Y + UndeadSimulation.MageSlotOffsetY);

                SetShape(_mageSlotFill[i], show, at, SlotFill, SlotFillAlpha * alpha);
                SetShape(_mageSlotEdge[i], show, at, SlotEdge, alpha);
            }
        }

        private void SetShape(SpriteRenderer renderer, bool show, UndeadVec2 at, Color color, float alpha)
        {
            if (renderer == null)
                return;

            renderer.enabled = show;

            if (show == false)
                return;

            renderer.transform.position = UndeadUnits.ToPosition(at.X, at.Y);
            renderer.color = new Color(color.r, color.g, color.b, alpha);
            renderer.sortingOrder = SortOrder(at.Y) + 1;
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

                // ★ 가는 쪽을 본다 [소스 Nl.update] — 가로 입력이 없으면 «마지막 방향»을 유지한다
                ApplyFacing(_hero.transform, _sim.HeroFacingLeft);

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
            active += Sync(_kunaiPool, _kunaiRoot, _kunaiSet, KunaiCount(), (i, r) => WriteKunai(i, r));
            active += Sync(_trailPool, _trailRoot, _trailSet, TrailCount(), (i, r) => WriteTrail(i, r));

            active += Sync(_orbPool, _orbRoot, _orbSet, OrbCount(), (i, r) => WriteOrb(i, r));

            active += Sync(_fireballPool, _fireballRoot, _fireballSet, FireballCount(), (i, r) => WriteFireball(i, r));

            active += Sync(_treePool, _treeRoot, _treeSet, _sim.Trees.Count, (i, r) => WriteTree(i, r));
            active += Sync(_meteorPool, _meteorRoot, _meteorSet, _sim.Meteors.Count, (i, r) => WriteMeteor(i, r));
            active += Sync(_fireplacePool, _fireplaceRoot, _fireSet, _sim.Fireplaces.Count, (i, r) => WriteFireplace(i, r));
            StepParticles(Time.deltaTime);
            active += Sync(_heartPool, _heartRoot, _heartSet, _heartDraw.Count, (i, r) => WriteHeart(i, r));
            active += SyncSprite(_sparklePool, _sparkleRoot, UndeadParticleView.White, _sparkleDraw.Count, (i, r) => WriteSparkle(i, r));

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
                {
                    _lifeSegments[i].enabled = false;

                    if (_lifeFills != null && _lifeFills[i] != null)
                        _lifeFills[i].enabled = false;
                }

                return;
            }

            int hp = _sim.HeroHp;
            bool full = hp >= _lifeSegments.Length;

            // 페이드 — 가득 차면 «지연 뒤» 옅어지고, 아니면 곧바로 나타난다.
            double target = full ? 0.0 : 1.0;
            double speed = full ? 1.0 / (LifeFadeOutDelay + LifeFadeOut) : 1.0 / LifeFadeIn;
            _lifeFadeSeconds = Mathf.MoveTowards((float)_lifeFadeSeconds, (float)target,
                                                 (float)(speed * Time.deltaTime));

            StepLifeSegments(hp);

            float width = (float)(LifeSegmentWidth + LifeSegmentGap);
            float totalWidth = width * _lifeSegments.Length;

            // ★ 바가 «왼쪽으로 3» 밀린다 [소스 pivot.set(0.5×width − 3, …)]
            double left = _sim.HeroPosition.X - totalWidth * 0.5 - LifeBarPivotShiftX;
            double y = _sim.HeroPosition.Y + LifeBarY;

            for (int i = 0; i < _lifeSegments.Length; i++)
            {
                double cx = left + width * i + LifeSegmentWidth * 0.5;

                _lifeSegments[i].transform.position = UndeadUnits.ToPosition(cx, y);
                _lifeSegments[i].color = new Color(1f, 1f, 1f, (float)_lifeFadeSeconds);

                if (_lifeFills == null || _lifeFills[i] == null)
                    continue;

                WriteLifeFill(i, cx, y);
            }
        }

        /// <summary>
        /// 목숨 수가 바뀐 «그 프레임»에 칸마다 연출을 건다 [소스 <c>setLives</c>].
        /// <para>돋아남 +시간 · 사라짐 −시간 으로 한 배열에 담는다.</para>
        /// </summary>
        private void StepLifeSegments(int hp)
        {
            if (_lifeFillPhase == null)
                return;

            if (_lifeLastHp != hp)
            {
                for (int i = 0; i < _lifeFillPhase.Length; i++)
                {
                    bool want = i < hp;
                    bool had = i < _lifeLastHp;

                    if (_lifeLastHp < 0)
                        _lifeFillPhase[i] = want ? 0.0 : double.NegativeInfinity;
                    else if (want && had == false)
                        _lifeFillPhase[i] = double.Epsilon;          // 돋아난다
                    else if (want == false && had)
                        _lifeFillPhase[i] = -double.Epsilon;         // 사라진다
                }

                _lifeLastHp = hp;
            }

            for (int i = 0; i < _lifeFillPhase.Length; i++)
            {
                if (double.IsInfinity(_lifeFillPhase[i]) || _lifeFillPhase[i] == 0.0)
                    continue;

                double moved = _lifeFillPhase[i] > 0.0
                    ? _lifeFillPhase[i] + Time.deltaTime
                    : _lifeFillPhase[i] - Time.deltaTime;

                if (moved >= LifeSegmentAppear)
                    moved = 0.0;                                     // 다 돋았다 — 그대로 보인다
                else if (moved <= -LifeSegmentVanish)
                    moved = double.NegativeInfinity;                 // 다 사라졌다

                _lifeFillPhase[i] = moved;
            }
        }

        /// <summary>
        /// 빨간 채움 한 칸 [소스 <c>update</c>] —
        /// 사라질 때는 <c>alpha 1−s</c> · <c>scale 1−0.8n</c> · <c>y +2n</c> (<c>n = 1−(1−s)²</c> · 300ms),
        /// 돋아날 때는 <c>alpha s</c> · <c>scale 0.2+0.8n</c> (<c>n = 1−(1−s)³</c> · 200ms).
        /// </summary>
        private void WriteLifeFill(int index, double cx, double y)
        {
            SpriteRenderer fill = _lifeFills[index];
            double phase = _lifeFillPhase[index];

            if (double.IsNegativeInfinity(phase))
            {
                fill.enabled = false;
                return;
            }

            fill.enabled = true;

            float alpha = 1f;
            float scale = 1f;
            double dropY = 0.0;

            if (phase < 0.0)
            {
                double s = Math.Min(-phase / LifeSegmentVanish, 1.0);
                double n = 1.0 - (1.0 - s) * (1.0 - s);
                alpha = (float)(1.0 - s);
                scale = (float)(1.0 - 0.8 * n);
                dropY = 2.0 * n;
            }
            else if (phase > 0.0)
            {
                double s = Math.Min(phase / LifeSegmentAppear, 1.0);
                double n = 1.0 - (1.0 - s) * (1.0 - s) * (1.0 - s);
                alpha = (float)s;
                scale = (float)(0.2 + 0.8 * n);
            }

            fill.transform.position = UndeadUnits.ToPosition(cx, y + dropY);
            fill.transform.localScale = new Vector3(scale, scale, 1f);
            fill.color = new Color(1f, 1f, 1f, alpha * (float)_lifeFadeSeconds);
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

        private int KunaiCount()
        {
            return _sim.Kunais.Count;
        }

        private int TrailCount()
        {
            return _trailSet != null ? _sim.TrailSegments.Count : 0;
        }

        /// <summary>
        /// 화염 자취 한 조각 [소스 <c>skill_effect_blazing_trail</c>].
        /// <para>★ 나이가 들수록 «옅어진다» — 원본은 조각을 페이드아웃시켜 지운다.</para>
        /// </summary>
        private bool WriteTrail(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.TrailSegment segment = _sim.TrailSegments[index];

            if (segment.Active == false || _trailSet == null)
                return false;

            renderer.sprite = _trailSet.Get(0, 0.0);
            Place(renderer.transform, segment.Position, _trailSet);

            // 자취는 «바닥»이다 — 개체보다 뒤로 보낸다
            renderer.sortingOrder = SortOrder(segment.Position.Y) - TrailSortingOffset;

            double life = _sim.TrailSegmentLifeSeconds;
            float fade = life > 0.0 ? 1f - (float)(segment.AgeSeconds / life) : 1f;
            Color color = BaseTint(_trailSet);
            color.a *= Mathf.Clamp01(fade);
            renderer.color = color;
            return true;
        }

        /// <summary>얼었을 때 덮어쓰는 색 [소스 <c>9427199</c> = <c>#8FD8FF</c>].</summary>
        private static readonly Color FrozenTint = new Color(0x8F / 255f, 0xD8 / 255f, 0xFF / 255f, 1f);

        /// <summary>자취가 개체 뒤로 가도록 빼는 값 — 같은 y 에서도 «항상 뒤»여야 한다.</summary>
        private const int TrailSortingOffset = 2;

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

        // ══════════════════════════════ 입자 — 모닥불 하트 · 나무 반짝임 [소스 Ll]

        /// <summary>
        /// 입자 이미터를 한 프레임 돌리고 <b>이번에 그릴 알을 «평평하게» 편다</b>.
        ///
        /// <para>
        /// ★ 풀은 <b>살아 있는 알 수만큼만</b> 렌더러를 만든다 — 슬롯 64칸 × 32알을 그대로 돌면
        /// 안 보이는 2048칸을 매 프레임 훑는다.
        /// </para>
        /// </summary>
        private void StepParticles(float dt)
        {
            double dtMs = dt * 1000.0;
            StepHeartEmitters(dtMs);
            StepTreeSparkles(dtMs);
        }

        private void StepHeartEmitters(double dtMs)
        {
            _heartDraw.Clear();

            if (_heartSet == null)
                return;

            int count = _sim.Fireplaces.Count;

            if (_heartEmitters.Length < count)
            {
                System.Array.Resize(ref _heartEmitters, count);
                System.Array.Resize(ref _heartEmitterTiles, count);
            }

            for (int i = 0; i < count; i++)
            {
                UndeadSimulation.Fireplace f = _sim.Fireplaces[i];

                if (f.Active == false)
                {
                    _heartEmitters[i] = null;
                    continue;
                }

                long tile = TileKey(f.TileX, f.TileY);

                // ⚠ 슬롯이 «다른 칸»으로 갈리면 이미터도 갈아야 한다 — 남의 하트가 이어 떠오른다
                if (_heartEmitters[i] == null || _heartEmitterTiles[i] != tile)
                {
                    _heartEmitters[i] = new UndeadHeartEmitter($"{f.TileX}_{f.TileY}_fire_sparks");
                    _heartEmitterTiles[i] = tile;
                }

                // [소스 isEmitterEnabled = !isInactive] — 재고가 없으면 새로 안 나온다
                _heartEmitters[i].Step(dtMs, 0.0, f.Lives > 0);
                Collect(_heartDraw, i, _heartEmitters[i]);
            }
        }

        private void StepTreeSparkles(double dtMs)
        {
            _sparkleDraw.Clear();

            int count = _sim.Trees.Count;

            if (_treeSparkles.Length < count)
            {
                System.Array.Resize(ref _treeSparkles, count);
                System.Array.Resize(ref _treeSparkleTiles, count);
                System.Array.Resize(ref _treeWasBursted, count);
            }

            for (int i = 0; i < count; i++)
            {
                UndeadSimulation.Tree t = _sim.Trees[i];

                if (t.Active == false)
                {
                    _treeSparkles[i] = null;
                    continue;
                }

                long tile = TileKey(t.TileX, t.TileY);

                if (_treeSparkles[i] == null || _treeSparkleTiles[i] != tile)
                {
                    _treeSparkles[i] = new UndeadSparkleEmitter($"{t.TileX}_{t.TileY}_sparks", UndeadSparklePalette.Fire);
                    _treeSparkleTiles[i] = tile;
                    _treeWasBursted[i] = t.Bursted;
                }

                // ★ 터지는 «순간»에 한꺼번에 뿜는다 [소스 burst() → emitBurst(20, 1)].
                //   시뮬은 「터졌다」는 상태만 들고 있으므로 여기서 «바뀐 순간»을 잡는다.
                if (_treeWasBursted[i] == false && t.Bursted)
                    _treeSparkles[i].EmitBurst(TreeBurstSparkles, 1.0);

                _treeWasBursted[i] = t.Bursted;

                // [소스] 터진 뒤에는 progress 0 으로 «있던 알만» 마저 돈다
                double progress = t.Bursted ? 0.0 : _sim.TreeChargeProgress(i);
                _treeSparkles[i].Step(dtMs, progress, t.Bursted == false);
                Collect(_sparkleDraw, i, _treeSparkles[i]);
            }
        }

        /// <summary>살아 있는 알을 <b>(주인 × 칸수 + 알)</b> 한 정수로 눌러 담는다.</summary>
        private static void Collect(List<int> into, int owner, UndeadParticleEmitter emitter)
        {
            for (int p = 0; p < emitter.Capacity; p++)
            {
                if (emitter[p].Active)
                    into.Add(owner * emitter.Capacity + p);
            }
        }

        /// <summary>칸 좌표 두 개를 <b>정수 하나</b>로 — 슬롯이 다른 칸으로 갈렸는지 보는 열쇠다.</summary>
        private static long TileKey(int tileX, int tileY)
        {
            return ((long)tileX << 32) ^ (uint)tileY;
        }

        private bool WriteHeart(int index, SpriteRenderer renderer)
        {
            if (index >= _heartDraw.Count)
                return false;

            int packed = _heartDraw[index];
            int owner = packed / UndeadHeartEmitter.HeartConfig.MaxParticles;
            int slot = packed % UndeadHeartEmitter.HeartConfig.MaxParticles;

            if (owner >= _sim.Fireplaces.Count || _heartEmitters[owner] == null)
                return false;

            UndeadSimulation.Fireplace f = _sim.Fireplaces[owner];
            renderer.sprite = _heartSet.Get(0, 0.0);

            double worldY = UndeadParticleView.Draw(renderer, _heartEmitters[owner][slot],
                                                    _heartEmitters[owner].Config, f.Position, HeartEmitterY);
            renderer.sortingOrder = SortOrder(worldY) + 1;
            return true;
        }

        private bool WriteSparkle(int index, SpriteRenderer renderer)
        {
            if (index >= _sparkleDraw.Count)
                return false;

            int packed = _sparkleDraw[index];
            int owner = packed / UndeadSparkleEmitter.SparkleConfig.MaxParticles;
            int slot = packed % UndeadSparkleEmitter.SparkleConfig.MaxParticles;

            if (owner >= _sim.Trees.Count || _treeSparkles[owner] == null)
                return false;

            UndeadSimulation.Tree t = _sim.Trees[owner];

            double worldY = UndeadParticleView.Draw(renderer, _treeSparkles[owner][slot],
                                                    _treeSparkles[owner].Config, t.Position, TreeSparkleEmitterY);
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
                    ApplyFacing(_boss.transform, _sim.BossFacingLeft);
                }
            }

            // 가족은 «보스 퀘스트 차례»에 나타나고, 끝나면 사라진다 [소스 — startFadeOut]
            if (_family != null)
            {
                bool showFamily = _sim.CurrentQuest == EUndeadQuest.FirstBoss;

                for (int i = 0; i < _family.Length; i++)
                {
                    if (_family[i] == null)
                        continue;

                    _family[i].enabled = showFamily;

                    if (showFamily == false)
                        continue;

                    // [소스 createMembers] 그룹 기준 상대 자리 — 셋이 서로 다른 자리에 선다
                    UndeadVec2 at = _sim.FamilyPosition + FamilyOffsets[i % FamilyOffsets.Length];
                    Place(_family[i].transform, at, _family[i]);
                    _family[i].sortingOrder = SortOrder(at.Y);
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
                ApplyFacing(_sheep[i].transform, _sim.Sheep_[i].FacingLeft);
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
            //   ⚠ 어느 쪽을 보는지는 «시뮬»이 든다 — 뷰가 매 프레임 다시 재면 붙었을 때 떤다.
            if (set.Data.FlipsHorizontally)
                ApplyFacing(renderer.transform, e.FacingLeft);

            // ★ 색은 «세 겹»이다 [소스 applyCurrentTint] — 피격 빨강이 «가장 위», 그다음 얼림, 없으면 평소 색.
            //   ⚠ 순서를 바꾸면 얼어 있는 적이 맞아도 안 붉어진다.
            renderer.color = e.HitTintRemaining > 0.0 ? Color.red
                           : _sim.EnemiesFrozen ? FrozenTint
                           : BaseTint(set);

            return true;
        }

        /// <summary>표가 정한 평소 색 — 틴트를 «되돌릴» 때 쓴다 (흰색이 아니다).</summary>
        private static Color BaseTint(UndeadSpriteSet set)
        {
            if (set == null || ColorUtility.TryParseHtmlString("#" + set.Data.TintHex, out Color tint) == false)
                return Color.white;

            return new Color(tint.r, tint.g, tint.b, (float)set.Data.Alpha);
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

        /// <summary>
        /// 전사가 던진 쿠나이 [소스 <c>Pd.instantiate</c>] — <b>날아가는 쪽을 향해 돈다</b>
        /// (<c>rotation = atan2(dir.y, dir.x)</c>).
        /// <para>⚠ 원본 월드는 y 가 «아래»가 +라 각도 부호가 뒤집힌다 — 총알과 같은 규약이다.</para>
        /// <para>★ 애니메이션이 없다 [실측 — 컷 1장]. 프레임을 돌리지 않는다.</para>
        /// </summary>
        private bool WriteKunai(int index, SpriteRenderer renderer)
        {
            UndeadSimulation.Kunai k = _sim.Kunais[index];

            if (k.Active == false || _kunaiSet == null)
                return false;

            renderer.sprite = _kunaiSet.Get(0, 0.0);
            Place(renderer.transform, k.Position, _kunaiSet);

            double angle = Mathf.Atan2((float)-k.Direction.Y, (float)k.Direction.X) * Mathf.Rad2Deg;
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, (float)angle);
            renderer.sortingOrder = SortOrder(k.Position.Y) + 1;
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

        /// <summary>
        /// 시트가 <b>없는</b> 풀 — 입자는 <b>흰 1×1 텍스처</b>라 <see cref="UndeadSpriteSet"/> 이 없다 [소스 <c>K.WHITE</c>].
        /// <para>⚠ <see cref="Sync"/> 는 시트가 <c>null</c> 이면 통째로 건너뛴다 — 그래서 갈래를 하나 둔다.</para>
        /// </summary>
        private int SyncSprite(List<SpriteRenderer> pool, Transform root, Sprite sprite,
                               int slotCount, System.Func<int, SpriteRenderer, bool> write)
        {
            if (sprite == null || root == null)
                return 0;

            int used = 0;

            for (int i = 0; i < slotCount; i++)
            {
                SpriteRenderer renderer = used < pool.Count ? pool[used] : null;

                if (renderer == null)
                {
                    var go = new GameObject("particle");
                    go.transform.SetParent(root, false);
                    renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
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

        /// <summary>도형 스프라이트 하나 — <b>아트 표를 타지 않는다</b>(원본에도 그런 «컷»이 없다).</summary>
        private SpriteRenderer NewShape(string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_questRoot, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.enabled = false;
            return renderer;
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

        /// <summary>
        /// 렌더러의 <b>세트를 갈아끼운다</b> — 시트마다 컷 수·fps·표시 배율이 다르다.
        /// <para>⚠ 배율까지 같이 바꾼다. 안 바꾸면 이전 시트의 배율로 새 그림이 나온다.</para>
        /// </summary>
        private static void ApplySet(SpriteRenderer renderer, UndeadSpriteSet set)
        {
            if (renderer == null || set == null)
                return;

            double fps = set.Data.Fps;
            renderer.sprite = set.Get(0, fps > 0.0 ? Time.timeSinceLevelLoad * fps : 0.0);
            renderer.transform.localScale =
                new Vector3((float)set.Data.DisplayScaleX, (float)set.Data.DisplayScaleY, 1f);
        }

        /// <summary>전사의 지금 시트 [소스 <c>setMode</c>].</summary>
        private UndeadSpriteSet WarriorSet()
        {
            switch (_sim.WarriorMode)
            {
                case EUndeadWarriorMode.Run: return _warriorRunSet ?? _warriorIdleSet ?? _warriorLaySet;
                case EUndeadWarriorMode.Idle: return _warriorIdleSet ?? _warriorLaySet;
                default: return _warriorLaySet;
            }
        }

        private SpriteRenderer NewRenderer(Transform parent, UndeadSpriteSet set, bool timed = true)
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
            if (timed && set.Data.Fps > 0.0 && set.Data.UsedCols > 1)
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

        /// <summary>
        /// 좌우 반전 — <b>배율 x 의 «부호»만</b> 준다 [소스 — <c>scale.x = ±Math.abs(scale.x)</c>].
        ///
        /// <para>
        /// ⚠ <b>어느 쪽을 보는지는 여기서 «정하지 않는다».</b> 그 판정은 개체마다 규칙이 다르고
        /// (게이트가 있는 것 · 「0 이면 유지」인 것 · 매 프레임 다시 재는 것) <b>«상태»여야 한다</b> —
        /// 그래서 시뮬이 들고 이 함수는 <b>그리기만</b> 한다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>[사고]</b> 히어로·보스·양은 이 처리가 <b>통째로 없었다</b>. 실측 2243 프레임에서
        /// 히어로 배율 x 가 <c>3.000</c> 으로 고정이었다 — 왼쪽으로 걸어도 오른쪽을 보고 있었다.
        /// 원본은 <b>다섯 자리</b>에서 반전한다 (히어로 · 일반 적 · 보스 · 양 · 동료 전사).
        /// </para>
        /// </summary>
        /// <summary>원본 px 글자 크기를 월드 <see cref="TextMeshPro"/> 의 <c>fontSize</c> 로 바꾼다.</summary>
        private static float BubbleFontSize(float originPx)
        {
            return UndeadUnits.ToUnits(originPx) * TmpWorldFontScale;
        }

        /// <summary>
        /// 말풍선 글자를 상자에 맞춘다 [소스 <c>fitTextToBubble</c>].
        ///
        /// <para>
        /// ★ <b>«배율로 줄이는» 것이 아니라 «크기를 한 단씩 내리는» 것이다.</b>
        /// <c>16</c> 에서 시작해 <c>12</c>(<c>medium</c> 은 <c>10</c>)까지 1 씩 내리며 <b>처음 드는 크기</b>를 쓴다.
        /// 짧은 문구는 <b>16 그대로</b> 뜬다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>[사고]</b> 크기를 <c>3.5</c> 로 «고정»해 두었었다 — 원본 <c>16</c>px 의 <b>절반</b>이고,
        /// 문구가 길든 짧든 늘 같은 크기였다. 실측에서 <c>도와줘!</c> 가 <c>3.5</c> 로 찍혔다.
        /// 「상자에 맞추기」(<c>#164</c>)는 <b>배율만</b>이 아니다 — 원본이 «크기를 고르면» 그 규칙을 옮긴다.
        /// </para>
        /// </summary>
        private static void FitBubbleText(TMP_Text text, float boxW, float boxH, bool medium)
        {
            if (text == null || boxW <= 0f || boxH <= 0f)
                return;

            float min = medium ? BubbleFontMinOriginMedium : BubbleFontMinOrigin;
            text.transform.localScale = Vector3.one;

            for (float origin = BubbleFontMaxOrigin; origin >= min; origin -= 1f)
            {
                text.fontSize = BubbleFontSize(origin);
                Vector2 preferred = text.GetPreferredValues(text.text, boxW, 0f);

                if (preferred.x <= boxW && preferred.y <= boxH)
                    return;
            }

            // 바닥 크기로도 안 들면 «그때만» 배율로 줄인다 [소스 — Mh(bubbleText, …)]
            text.fontSize = BubbleFontSize(min);
            Vector2 last = text.GetPreferredValues(text.text, boxW, 0f);
            float scale = Mathf.Min(1f, Mathf.Min(boxW / Mathf.Max(0.0001f, last.x),
                                                  boxH / Mathf.Max(0.0001f, last.y)));
            text.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private static void ApplyFacing(Transform target, bool facingLeft)
        {
            if (target == null)
                return;

            Vector3 scale = target.localScale;
            float magnitude = Mathf.Abs(scale.x);
            scale.x = facingLeft ? -magnitude : magnitude;
            target.localScale = scale;
        }
    }
}
