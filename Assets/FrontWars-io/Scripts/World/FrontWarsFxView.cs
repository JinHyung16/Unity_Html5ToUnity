using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 지도 위 연출 — 핵(폭발 · 파편) · SAM 요격 · 포탄 명중 · 군함 침몰 · 정복 · 떠오르는 금 · 철도 먼지 ·
    /// 내 수송선 목적지 표시 · 공격 병력 숫자.
    ///
    /// <para>
    /// 원본은 스프라이트 시트를 쓴다. 시트 그림은 옮기지 않고(R11) 코드로 그린 근사로 대신하되
    /// <b>시간 · 크기 · 색은 원본 값</b> 그대로다 (`render_specs.md` 2번 표 · `[소스] BMmXchgl.js:cr={[j.Fire]:Hn,…}`).
    /// </para>
    ///
    /// <para>모든 연출은 월드 단위라 확대하면 같이 커진다. 설정 「특수 효과」가 꺼지면 하나도 안 그린다.</para>
    /// <para>이모지는 이름표 아이콘 줄 자리에 뜬다 — 창구만 여기 두고 <see cref="FrontWarsNameView"/> 가 그린다 (`render_specs.md` 4번).</para>
    /// </summary>
    public sealed class FrontWarsFxView : MonoBehaviour
    {
        /// <summary>동시에 살아 있는 연출 한도 — 넘으면 오래된 것부터 버린다 `[소스] BMmXchgl.js:this.MAX_ACTIVE_EFFECTS=500`.</summary>
        private const int MaxActiveEffects = 500;

        // ── 핵 `[소스] BMmXchgl.js:this.lifespanMs=3100` · `maxRadius:r*1.55,lifespan:1550`

        private const float NukeSeconds = 3.1f;
        private const float NukeSize = 60f;
        private const float NukeFadeStart = 0.77f;
        private const float NukeRingRadiusScale = 1.55f;
        private const float NukeRingSeconds = 1.55f;

        // ── SAM 요격 `[소스] BMmXchgl.js:maxRadius:40,lifespan:800` · SAMExplosion 시트 675ms

        private const float SamSeconds = 0.675f;
        private const float SamSize = 48f;
        private const float SamRingRadius = 40f;
        private const float SamRingSeconds = 0.8f;

        /// <summary>고리 두께 (칸) `[소스] BMmXchgl.js:두께 0.5`.</summary>
        private const float RingWidth = 0.5f;

        private const int RingSegments = 64;

        // ── 포탄 명중 · 기차 파괴 (Explosion 시트 13px · 4프레임 × 80ms)

        private const float ShellSeconds = 0.32f;
        private const float ShellSize = 13f;

        // ── 군함 파괴 (UnitExplosion 19px 260ms 3발 + SinkingShip 16px 1330ms)

        private const float UnitExplosionSeconds = 0.26f;
        private const float UnitExplosionSize = 19f;
        private const float SinkingSeconds = 1.33f;
        private const float SinkingSize = 16f;

        /// <summary>군함 폭발 3발의 자리와 시각 `[소스] BMmXchgl.js:br=[{x:0,y:0,period:0},{x:4,y:-6,period:70},{x:-6,y:4,period:140}]`.</summary>
        private static readonly Vector3[] WarshipBurstOffsets =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 6f, 0f),
            new Vector3(-6f, -4f, 0f)
        };

        private static readonly float[] WarshipBurstDelays = { 0f, 0.07f, 0.14f };

        // ── 정복 `[소스] BMmXchgl.js:this.baseLabelOffsetY=10,this.lifespanMs=1600`

        private const float ConquestSeconds = 1.6f;
        private const float ConquestCircleRadius = 30f;
        private const float ConquestCircleGrow = 0.6f;
        private const float ConquestCircleAlpha = 0.18f;
        private const float ConquestMarkSize = 21f;
        private const float ConquestMarkBase = 0.6f;
        private const float ConquestMarkGrow = 0.55f;
        private const float ConquestWobbleRadians = 0.07f;
        private const float ConquestWobbleSpeed = 0.01f;
        private const float ConquestMarkRise = 6f;
        private const float ConquestLabelStartY = 10f;
        private const float ConquestLabelEndY = -6f;
        private const float ConquestFontWorld = 9f;
        private const float ConquestOutlineWorld = 1.5f;
        private const float ConquestFadeStart = 0.72f;

        // ── 떠오르는 금 `[소스] BMmXchgl.js:Cr.createLabel(e,t,n,1100,19)` · `700 12px sans-serif`

        private const float GoldSeconds = 1.1f;
        private const float GoldRise = 19f;
        private const float GoldFontWorld = 12f;

        // ── 철도 먼지 (Dust 시트 9px · 3프레임 × 110ms = 330ms) `[소스] render_specs.md 2번 「철도 먼지」`

        private const float DustSeconds = 0.33f;
        private const float DustSize = 9f;

        /// <summary>선로가 깔리거나 없어진 칸마다 먼지가 뜰 확률 `[소스] 1/3 확률`.</summary>
        private const float DustChance = 1f / 3f;

        /// <summary>먼지가 옅어지기 시작하는 지점 — 시트 흉내다 (원본 값 아님).</summary>
        private const float DustFadeStart = 0.35f;

        /// <summary>먼지 색 — 시트 대신 쓰는 우리 색이다 (마른 흙빛).</summary>
        private static readonly Color DustColor = new Color32(176, 158, 130, 255);

        // ── 핵 파편 `[소스] render_specs.md 2번 「핵 폭발」 ③` — 불 반경/88 · 연기 반경/98 · 큰연기·불연기 반경/240

        private const float DebrisSeconds = 2.05f;
        private const int DebrisMaxPieces = 50;
        private const float DebrisFireDivisor = 88f;
        private const float DebrisSmokeDivisor = 98f;
        private const float DebrisBigSmokeDivisor = 240f;

        /// <summary>큰연기 · 불연기가 흩어지는 범위 비 `[소스] 범위 0.9`.</summary>
        private const float DebrisWideSpread = 0.9f;

        /// <summary>파편이 떨어지는 범위 = 반경/2 `[소스] 반경/2 안의 땅 위 무작위`.</summary>
        private const float DebrisRadiusScale = 0.5f;

        /// <summary>땅 칸을 못 고르면 그만둔다 — 물뿐인 곳에서 헛도는 것을 막는다.</summary>
        private const int DebrisTileTries = 12;

        /// <summary>파편 그림 한 변 (칸) — 각 시트 한 칸 너비 `[소스] Fire 7 · Smoke 11 · BigSmoke 24 · FireSmoke 24`.</summary>
        private const float DebrisFireSize = 7f;
        private const float DebrisSmokeSize = 11f;
        private const float DebrisWideSize = 24f;

        /// <summary>파편이 사는 동안 올라가는 높이 (칸) — 시트 흉내다 (원본 값 아님).</summary>
        private const float DebrisRise = 4f;

        private const float DebrisFadeStart = 0.55f;

        /// <summary>연기 색 · 불연기 색 — 시트 대신 쓰는 우리 색이다.</summary>
        private static readonly Color DebrisSmokeColor = new Color32(125, 125, 125, 255);
        private static readonly Color DebrisFireSmokeColor = new Color32(160, 115, 80, 255);

        // ── 수송선 목적지 표시 `[소스] render_specs.md 2번 「수송선 목적지 표시」`

        /// <summary>빨강 두 가지 `[소스] #ff3b30` · `#e32a20`. 어느 쪽이 어디 쓰이는지는 규격에 없어 점 · 퍼지는 고리를 밝은 쪽으로 잡았다.</summary>
        private static readonly Color MarkerBright = new Color32(255, 59, 48, 255);
        private static readonly Color MarkerDark = new Color32(227, 42, 32, 255);

        /// <summary>가운데 점 — 반경 1.5 × (0.8~1.1) 로 깜빡인다. 깜빡이는 주기는 규격에 없어 1초로 잡았다.</summary>
        private const float MarkerDotRadius = 1.5f;
        private const float MarkerDotMin = 0.8f;
        private const float MarkerDotMax = 1.1f;
        private const float MarkerPulseSeconds = 1f;

        /// <summary>점선 고리 두 개 — 반경 3 · 6, 4조각, 한 조각이 제 몫의 61%. 도는 빠르기는 규격에 없어 45°/초로 잡았다.</summary>
        private const float MarkerInnerRadius = 3f;
        private const float MarkerOuterRadius = 6f;
        private const int MarkerDashes = 4;
        private const float MarkerDashFill = 0.61f;
        private const float MarkerSpinDegrees = 45f;

        /// <summary>한 조각을 그리는 점 수 — 많을수록 매끄럽다.</summary>
        private const int MarkerDashPoints = 8;

        /// <summary>퍼지는 고리 두 개 — 최대 반경 10. 한 바퀴 시간은 규격에 없어 1.4초로 잡았다.</summary>
        private const float MarkerWaveRadius = 10f;
        private const float MarkerWaveSeconds = 1.4f;
        private const int MarkerWaves = 2;

        /// <summary>도착 · 후퇴 · 파괴 뒤 사라지는 시간 `[소스] 210ms`.</summary>
        private const float MarkerFadeSeconds = 0.21f;

        /// <summary>고리 굵기 (칸) — 규격에 없어 우리가 잡았다.</summary>
        private const float MarkerLineWidth = 0.35f;

        // ── 공격 병력 숫자 `[소스] BMmXchgl.js:t.font=\`700 4.5px sans-serif\``

        private const float AttackFontWorld = 4.5f;
        private const float AttackOutlineWorld = 1f;

        /// <summary>나에게 오는 공격 `[소스] rgb(220,60,60)` · 내가 보낸 공격 `rgb(80,140,255)`.</summary>
        private static readonly Color32 AttackIncomingColor = new Color32(220, 60, 60, 255);
        private static readonly Color32 AttackOutgoingColor = new Color32(80, 140, 255, 255);

        /// <summary>공격 숫자 외곽선 `[소스] rgba(0,0,0,0.55)` (0.55 × 255 ≈ 140).</summary>
        private static readonly Color32 AttackOutline = new Color32(0, 0, 0, 140);

        /// <summary>TMP 3D 글자 크기 → 월드 높이 비 (fontSize 10 ≈ 1 단위) — 이름표 · 건물 레벨과 같은 약속이다.</summary>
        private const float TmpSizePerWorldUnit = 10f;

        private const int EffectOrder = 40;
        private const int LabelOrder = 50;
        private const int DiscTextureSize = 128;

        /// <summary>불 · 폭발 색 — 시작(흰 열) → 가운데(주황) → 끝(그을음).</summary>
        private static readonly Color BurstHot = new Color32(255, 236, 170, 255);
        private static readonly Color BurstMid = new Color32(255, 150, 40, 255);
        private static readonly Color BurstCold = new Color32(120, 40, 20, 255);

        /// <summary>SAM 요격은 불꽃이 아니라 흰 섬광이다.</summary>
        private static readonly Color SamFlash = new Color32(235, 240, 255, 255);

        /// <summary>정복 글자 외곽선 `[소스] BMmXchgl.js:rgba(0,0,0,0.35)`.</summary>
        private static readonly Color32 ConquestOutline = new Color32(0, 0, 0, 89);

        /// <summary>연출 종류 — 갱신 방법이 저마다 다르다.</summary>
        private enum EKind
        {
            /// <summary>퍼지는 불덩이 (핵 · 포탄 · 군함 폭발 · SAM 섬광).</summary>
            Burst,

            /// <summary>퍼지는 흰 고리 (핵 · SAM).</summary>
            Ring,

            /// <summary>가라앉는 배.</summary>
            Sinking,

            /// <summary>정복 — 원 + 표식 + 「+N gold」 글자.</summary>
            Conquest,

            /// <summary>떠오르는 금 글자.</summary>
            Gold,

            /// <summary>핵 파편 — 불 · 연기가 제자리에서 피어오르다 사라진다.</summary>
            Debris
        }

        private sealed class Effect
        {
            public EKind Kind;
            public SpriteRenderer Sprite;
            public SpriteRenderer Mark;
            public LineRenderer Ring;
            public TextMeshPro Text;

            public float Age;
            public float Delay;
            public float Life;
            public Vector3 Origin;

            /// <summary>그림 한 변 (칸) 또는 고리 최대 반지름.</summary>
            public float Size;

            public Color Color;

            /// <summary>이 지점까지 투명도 1, 그 뒤로 사라진다 (1 이면 처음부터 옅어진다).</summary>
            public float FadeStart = 1f;

            /// <summary>불꽃색으로 식어 가나 (SAM 섬광은 흰색 그대로다).</summary>
            public bool Fire;
        }

        /// <summary>
        /// 내 수송선 하나의 목적지 표시 — 점 하나 · 점선 고리 둘 · 퍼지는 고리 둘로 이루어진다.
        /// <para>배가 사는 동안 계속 떠 있고, 도착 · 후퇴 · 파괴 때 <see cref="MarkerFadeSeconds"/> 동안 사라진다.</para>
        /// </summary>
        private sealed class BoatMarker
        {
            public Vector3 At;
            public float Age;

            /// <summary>음수면 아직 살아 있다. 0 이상이면 사라지는 중이다.</summary>
            public float FadeAge = -1f;

            public SpriteRenderer Dot;
            public readonly LineRenderer[] Dashes = new LineRenderer[MarkerDashes * 2];
            public readonly LineRenderer[] Waves = new LineRenderer[MarkerWaves];
        }

        /// <summary>공격 하나에 붙는 병력 숫자 — 공격이 목록에서 빠지면 치운다.</summary>
        private sealed class AttackLabel
        {
            public TextMeshPro Text;
            public bool Seen;
        }

        private readonly List<Effect> _effects = new List<Effect>();
        private readonly Stack<SpriteRenderer> _spritePool = new Stack<SpriteRenderer>();
        private readonly Stack<LineRenderer> _ringPool = new Stack<LineRenderer>();
        private readonly Stack<TextMeshPro> _textPool = new Stack<TextMeshPro>();
        private readonly Vector3[] _ringPoints = new Vector3[RingSegments];
        private readonly Vector3[] _dashPoints = new Vector3[MarkerDashPoints];

        /// <summary>내 수송선 id → 목적지 표시.</summary>
        private readonly Dictionary<int, BoatMarker> _boatMarkers = new Dictionary<int, BoatMarker>();

        /// <summary>공격 id → 병력 숫자.</summary>
        private readonly Dictionary<string, AttackLabel> _attackLabels = new Dictionary<string, AttackLabel>();

        private readonly List<int> _staleMarkers = new List<int>();
        private readonly List<string> _staleAttacks = new List<string>();

        private FrontWarsGame _game;
        private FrontWarsMapRenderer _renderer;
        private FrontWarsNameView _names;
        private Func<FrontWarsPlayer> _me;
        private Sprite _disc;
        private Material _spriteMaterial;
        private bool _enabled = true;

        public int ActiveCount => _effects.Count;

        /// <summary>
        /// 설정 「특수 효과」 — 끄면 새 연출을 안 만들고 떠 있던 것도 치운다 `[소스] fxLayer(){return this.get(\`settings.specialEffects\`,!0)}` (기본 켬).
        /// </summary>
        public bool EffectsEnabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;

                if (value)
                    return;

                for (int i = _effects.Count - 1; i >= 0; i--)
                    Release(_effects[i]);

                _effects.Clear();
                ClearBoatMarkers();
                ClearAttackLabels();
            }
        }

        /// <summary>이름표 — 이모지를 그리는 쪽. 안 넣으면 같은 월드 묶음에서 찾는다.</summary>
        public FrontWarsNameView Names
        {
            get
            {
                if (_names == null)
                {
                    FrontWarsWorldView world = GetComponentInParent<FrontWarsWorldView>();
                    _names = world != null ? world.NameView : null;
                }

                return _names;
            }
            set { _names = value; }
        }

        /// <summary>설정 「게임 내 이모지 표시 여부」 — 이름표가 그리니 그쪽 값을 그대로 넘긴다.</summary>
        public bool EmojisEnabled
        {
            get { return Names == null || Names.EmojisEnabled; }
            set
            {
                if (Names != null)
                    Names.EmojisEnabled = value;
            }
        }

        public void Bind(FrontWarsGame game, FrontWarsMapRenderer renderer, Func<FrontWarsPlayer> me)
        {
            Unbind();
            _game = game;
            _renderer = renderer;
            _me = me;
            _game.OnNukeDetonated += HandleNukeDetonated;
            _game.OnGoldGain += HandleGoldGain;
            _game.OnConquerPlayer += HandleConquer;
            _game.OnUnitUpdate += HandleUnitUpdate;
            _game.OnRailAdded += HandleRailAdded;
            _game.OnRailRemoved += HandleRailRemoved;
        }

        public void Unbind()
        {
            if (_game != null)
            {
                _game.OnNukeDetonated -= HandleNukeDetonated;
                _game.OnGoldGain -= HandleGoldGain;
                _game.OnConquerPlayer -= HandleConquer;
                _game.OnUnitUpdate -= HandleUnitUpdate;
                _game.OnRailAdded -= HandleRailAdded;
                _game.OnRailRemoved -= HandleRailRemoved;
            }

            for (int i = _effects.Count - 1; i >= 0; i--)
                Release(_effects[i]);

            _effects.Clear();
            ClearBoatMarkers();
            ClearAttackLabels();
            _game = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        // ── 무엇이 언제 터지나

        /// <summary>핵이 목표에 닿았다 — 불덩이 3100ms + 흰 고리 반경×1.55 를 1550ms 동안.</summary>
        private void HandleNukeDetonated(int tile, EFrontWarsUnit type, int outerRadius)
        {
            if (_enabled == false)
                return;

            Vector3 at = Where(tile);
            AddBurst(at, NukeSize, NukeSeconds, 0f, BurstHot, NukeFadeStart);
            AddRing(at, outerRadius * NukeRingRadiusScale, NukeRingSeconds);
            AddNukeDebris(tile, outerRadius);
        }

        /// <summary>
        /// 핵 파편 — 반경으로 개수가 갈린다 `[소스] render_specs.md 2번 「핵 폭발」 ③`.
        /// <para>불 반경/88 · 연기 반경/98 · 큰연기 반경/240 · 불연기 반경/240, 모두 합쳐 최대 50개. 반경/2 안의 <b>땅</b> 칸에 무작위로 놓고 각 2050ms 산다.</para>
        /// <para>원자(75)는 한 조각도 안 뜨고 수소(175)는 불 1 · 연기 1 이다 — 식이 그렇게 나온다.</para>
        /// </summary>
        private void AddNukeDebris(int tile, int outerRadius)
        {
            if (_game == null || outerRadius <= 0)
                return;

            int fire = Mathf.FloorToInt(outerRadius / DebrisFireDivisor);
            int smoke = Mathf.FloorToInt(outerRadius / DebrisSmokeDivisor);
            int big = Mathf.FloorToInt(outerRadius / DebrisBigSmokeDivisor);
            int fireSmoke = big;
            int budget = DebrisMaxPieces;
            float range = outerRadius * DebrisRadiusScale;

            budget -= Scatter(tile, range, 1f, fire, DebrisFireSize, BurstHot, true, budget);
            budget -= Scatter(tile, range, 1f, smoke, DebrisSmokeSize, DebrisSmokeColor, false, budget);
            budget -= Scatter(tile, range, DebrisWideSpread, big, DebrisWideSize, DebrisSmokeColor, false, budget);
            Scatter(tile, range, DebrisWideSpread, fireSmoke, DebrisWideSize, DebrisFireSmokeColor, false, budget);
        }

        /// <summary>파편 한 종류를 뿌린다 — 놓은 개수를 돌려준다.</summary>
        private int Scatter(int center, float range, float spread, int count, float size, Color color, bool fire, int budget)
        {
            int placed = 0;
            float reach = range * spread;

            for (int i = 0; i < count && placed < budget; i++)
            {
                if (TryLandTileNear(center, reach, out int tile) == false)
                    break;

                _effects.Add(new Effect
                {
                    Kind = EKind.Debris,
                    Sprite = AcquireSprite(Disc()),
                    Origin = Where(tile),
                    Size = size,
                    Life = DebrisSeconds,
                    Color = color,
                    FadeStart = DebrisFadeStart,
                    Fire = fire
                });

                placed++;
            }

            Trim();
            return placed;
        }

        /// <summary>가운데에서 <paramref name="reach"/> 칸 안의 땅 칸 하나를 고른다 — 못 고르면 false.</summary>
        private bool TryLandTileNear(int center, float reach, out int tile)
        {
            tile = -1;
            FrontWarsTerrainMap map = _game.Map;
            int cx = map.X(center), cy = map.Y(center);

            for (int i = 0; i < DebrisTileTries; i++)
            {
                // 원 안에 고르게 뿌린다 (반지름에 제곱근을 씌워야 가운데로 몰리지 않는다). 시뮬 난수가 아니라 보기용 난수다
                float angle = UnityEngine.Random.value * 2f * Mathf.PI;
                float radius = Mathf.Sqrt(UnityEngine.Random.value) * reach;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(angle) * radius);

                if (map.IsValidCoord(x, y) == false)
                    continue;

                int candidate = map.Ref(x, y);

                if (map.IsLand(candidate) == false)
                    continue;

                tile = candidate;
                return true;
            }

            return false;
        }

        /// <summary>선로가 깔린 칸 — 칸마다 1/3 확률로 먼지.</summary>
        private void HandleRailAdded(int id, List<FrontWarsRailTile> tiles)
        {
            if (_enabled == false || tiles == null)
                return;

            for (int i = 0; i < tiles.Count; i++)
                AddDust(tiles[i].Tile);
        }

        /// <summary>선로가 없어진 칸 — 깔릴 때와 같은 확률로 먼지.</summary>
        private void HandleRailRemoved(int id, List<int> tiles)
        {
            if (_enabled == false || tiles == null)
                return;

            for (int i = 0; i < tiles.Count; i++)
                AddDust(tiles[i]);
        }

        /// <summary>먼지 한 뭉치 — 1/3 확률로 330ms `[소스] render_specs.md 2번 「철도 먼지」`. 선로 자체는 <see cref="FrontWarsRailView"/> 가 그린다.</summary>
        private void AddDust(int tile)
        {
            if (UnityEngine.Random.value >= DustChance)
                return;

            AddBurst(Where(tile), DustSize, DustSeconds, 0f, DustColor, DustFadeStart, false);
        }

        /// <summary>
        /// 유닛이 바뀌었다 — 사라진 것은 포탄 명중 · 기차 파괴 · 군함 침몰 · SAM 요격, 수송선은 목적지 표시.
        /// <para>요격은 핵이 목표에 닿기 전에 사라진 것이다 `[소스] render_specs.md 2번 「SAM 요격」`.</para>
        /// </summary>
        private void HandleUnitUpdate(FrontWarsUnit unit)
        {
            if (_enabled == false)
                return;

            // 수송선은 살아 있는 동안 목적지 표시를 들고 다닌다 (사라질 때가 아니라 바뀔 때마다 본다)
            if (unit.Type == EFrontWarsUnit.TransportShip)
            {
                SyncBoatMarker(unit);
                return;
            }

            if (unit.IsActive)
                return;

            Vector3 at = Where(unit.Tile);

            switch (unit.Type)
            {
                case EFrontWarsUnit.Shell:
                    if (unit.ReachedTarget)
                        AddBurst(at, ShellSize, ShellSeconds, 0f, BurstHot, 1f);

                    break;
                case EFrontWarsUnit.Train:
                    if (unit.ReachedTarget == false)
                        AddBurst(at, ShellSize, ShellSeconds, 0f, BurstHot, 1f);

                    break;
                case EFrontWarsUnit.Warship:
                    for (int i = 0; i < WarshipBurstOffsets.Length; i++)
                        AddBurst(at + WarshipBurstOffsets[i], UnitExplosionSize, UnitExplosionSeconds, WarshipBurstDelays[i], BurstHot, 1f);

                    AddSinking(at, unit.Owner);
                    break;
                case EFrontWarsUnit.AtomBomb:
                case EFrontWarsUnit.HydrogenBomb:
                case EFrontWarsUnit.MIRVWarhead:
                    // 목표에 닿기 전에 사라진 핵 = 요격당한 것이다 (닿았으면 폭발 알림이 따로 온다)
                    if (unit.ReachedTarget == false)
                    {
                        AddBurst(at, SamSize, SamSeconds, 0f, SamFlash, 1f, false);
                        AddRing(at, SamRingRadius, SamRingSeconds);
                    }

                    break;
            }
        }

        /// <summary>붙인 타일이 금을 줬다 — 받는 사람이 나일 때만 뜬다 `[소스] BMmXchgl.js:if(this._game.player(e.player)!==this._game.myPlayer())return`.</summary>
        private void HandleGoldGain(FrontWarsPlayer player, long gold, int tile)
        {
            if (_enabled == false || player != _me?.Invoke() || gold <= 0 || tile < 0)
                return;

            TextMeshPro text = AcquireText(GoldFontWorld, Color.white, 0f, ConquestOutline);
            text.text = "+ " + FrontWarsNumberFormat.Gold(gold);

            _effects.Add(new Effect
            {
                Kind = EKind.Gold,
                Text = text,
                Origin = Where(tile),
                Life = GoldSeconds,
                Color = Color.white
            });

            Trim();
        }

        /// <summary>정복 — 내가 정복자일 때만, 정복당한 쪽 이름 자리에서 1600ms.</summary>
        private void HandleConquer(FrontWarsPlayer conqueror, FrontWarsPlayer conquered, long gold)
        {
            if (_enabled == false || conqueror != _me?.Invoke())
                return;

            if (Names == null || Names.TryNamePosition(conquered, out Vector3 at) == false)
                return;

            Color32 color = _renderer != null && _renderer.Colors != null
                ? _renderer.Colors.TerritoryColor(conqueror)
                : new Color32(255, 255, 255, 255);

            SpriteRenderer circle = AcquireSprite(Disc());
            SpriteRenderer mark = AcquireSprite(FrontWarsIconFactory.GetMark(FrontWarsIconFactory.EEmojiMark.Flag));
            TextMeshPro text = AcquireText(ConquestFontWorld, color, ConquestOutlineWorld, ConquestOutline);
            text.text = $"+{FrontWarsNumberFormat.Gold(gold)} gold";

            _effects.Add(new Effect
            {
                Kind = EKind.Conquest,
                Sprite = circle,
                Mark = mark,
                Text = text,
                Origin = at,
                Life = ConquestSeconds,
                Size = ConquestMarkSize,
                Color = color
            });

            Trim();
        }

        // ── 수송선 목적지 표시

        /// <summary>
        /// 내 수송선 하나의 목적지 표시를 맞춘다 — 내 배가 아니면 안 띄우고, 도착 · 후퇴 · 파괴면 사라지게 한다.
        /// <para>후퇴한 배는 출발지로 돌아가지만 표시는 그대로 지운다 `[소스] render_specs.md 2번 「도착·후퇴·파괴 시 210ms 동안 사라짐」`.</para>
        /// </summary>
        private void SyncBoatMarker(FrontWarsUnit unit)
        {
            FrontWarsPlayer me = _me?.Invoke();
            bool mine = me != null && unit.Owner == me;

            if (mine == false || unit.IsActive == false || unit.Retreating || unit.TargetTile < 0)
            {
                if (_boatMarkers.TryGetValue(unit.Id, out BoatMarker dying) && dying.FadeAge < 0f)
                    dying.FadeAge = 0f;

                return;
            }

            if (_boatMarkers.TryGetValue(unit.Id, out BoatMarker marker) == false)
            {
                marker = CreateBoatMarker();
                _boatMarkers[unit.Id] = marker;
            }

            marker.At = Where(unit.TargetTile);
            marker.FadeAge = -1f;
        }

        private BoatMarker CreateBoatMarker()
        {
            var marker = new BoatMarker { Dot = AcquireSprite(Disc()) };

            for (int i = 0; i < marker.Dashes.Length; i++)
                marker.Dashes[i] = AcquireLine(MarkerDashPoints, false);

            for (int i = 0; i < marker.Waves.Length; i++)
                marker.Waves[i] = AcquireLine(RingSegments, true);

            return marker;
        }

        private void ReleaseBoatMarker(BoatMarker marker)
        {
            marker.Dot.gameObject.SetActive(false);
            _spritePool.Push(marker.Dot);

            for (int i = 0; i < marker.Dashes.Length; i++)
            {
                marker.Dashes[i].gameObject.SetActive(false);
                _ringPool.Push(marker.Dashes[i]);
            }

            for (int i = 0; i < marker.Waves.Length; i++)
            {
                marker.Waves[i].gameObject.SetActive(false);
                _ringPool.Push(marker.Waves[i]);
            }
        }

        private void ClearBoatMarkers()
        {
            foreach (BoatMarker marker in _boatMarkers.Values)
                ReleaseBoatMarker(marker);

            _boatMarkers.Clear();
        }

        /// <summary>표시 하나를 움직인다 — 깜빡이는 점 · 반대로 도는 점선 고리 둘 · 퍼지는 고리 둘.</summary>
        private void UpdateBoatMarkers(float dt)
        {
            if (_boatMarkers.Count == 0)
                return;

            _staleMarkers.Clear();

            foreach (KeyValuePair<int, BoatMarker> pair in _boatMarkers)
            {
                BoatMarker marker = pair.Value;
                marker.Age += dt;
                float alpha = 1f;

                if (marker.FadeAge >= 0f)
                {
                    marker.FadeAge += dt;
                    alpha = 1f - Mathf.Clamp01(marker.FadeAge / MarkerFadeSeconds);

                    if (alpha <= 0f)
                    {
                        _staleMarkers.Add(pair.Key);
                        continue;
                    }
                }

                // 가운데 점 — 반경 1.5 × (0.8~1.1)
                float pulse = Mathf.Lerp(MarkerDotMin, MarkerDotMax, 0.5f + 0.5f * Mathf.Sin(marker.Age * 2f * Mathf.PI / MarkerPulseSeconds));
                float diameter = MarkerDotRadius * pulse * 2f;
                marker.Dot.transform.localPosition = marker.At + new Vector3(0f, 0f, -0.24f);
                marker.Dot.transform.localScale = new Vector3(diameter, diameter, 1f);
                marker.Dot.color = new Color(MarkerBright.r, MarkerBright.g, MarkerBright.b, alpha);

                // 점선 고리 둘 — 서로 반대로 돈다
                float spin = marker.Age * MarkerSpinDegrees;
                DrawDashRing(marker, 0, MarkerInnerRadius, spin, alpha);
                DrawDashRing(marker, MarkerDashes, MarkerOuterRadius, -spin, alpha);

                // 퍼지는 고리 둘 — 반경 0 → 10, 서로 반 바퀴 어긋나게
                for (int i = 0; i < marker.Waves.Length; i++)
                {
                    float t = Mathf.Repeat(marker.Age / MarkerWaveSeconds + (float)i / marker.Waves.Length, 1f);
                    var color = new Color(MarkerBright.r, MarkerBright.g, MarkerBright.b, alpha * (1f - t));
                    SetCircle(marker.Waves[i], marker.At, MarkerWaveRadius * t, color);
                }
            }

            for (int i = 0; i < _staleMarkers.Count; i++)
            {
                ReleaseBoatMarker(_boatMarkers[_staleMarkers[i]]);
                _boatMarkers.Remove(_staleMarkers[i]);
            }
        }

        /// <summary>점선 고리 하나 — 4조각, 한 조각이 제 몫(90°)의 61%.</summary>
        private void DrawDashRing(BoatMarker marker, int offset, float radius, float spinDegrees, float alpha)
        {
            float slot = 360f / MarkerDashes;
            float arc = slot * MarkerDashFill;
            var color = new Color(MarkerDark.r, MarkerDark.g, MarkerDark.b, alpha);

            for (int i = 0; i < MarkerDashes; i++)
            {
                LineRenderer line = marker.Dashes[offset + i];
                float start = spinDegrees + slot * i;

                for (int p = 0; p < MarkerDashPoints; p++)
                {
                    float a = (start + arc * p / (MarkerDashPoints - 1)) * Mathf.Deg2Rad;
                    _dashPoints[p] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                }

                line.transform.localPosition = marker.At + new Vector3(0f, 0f, -0.24f);
                line.SetPositions(_dashPoints);
                line.widthMultiplier = MarkerLineWidth;
                line.startColor = color;
                line.endColor = color;
            }
        }

        /// <summary>동그란 고리 하나를 그 자리에 놓는다.</summary>
        private void SetCircle(LineRenderer line, Vector3 at, float radius, Color color)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                float a = 2f * Mathf.PI * i / RingSegments;
                _ringPoints[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }

            line.transform.localPosition = at + new Vector3(0f, 0f, -0.24f);
            line.SetPositions(_ringPoints);
            line.widthMultiplier = MarkerLineWidth;
            line.startColor = color;
            line.endColor = color;
        }

        // ── 공격 병력 숫자

        /// <summary>
        /// 나에게 오는 공격(봇 제외) · 내가 보낸 공격의 병력을 공격 평균 자리에 적는다 `[소스] render_specs.md 2번 「공격 병력 숫자」`.
        /// <para>공격은 틱마다 앞줄이 바뀌니 프레임마다 다시 잰다. 목록에서 빠진 공격의 글자는 치운다.</para>
        /// </summary>
        private void UpdateAttackLabels()
        {
            FrontWarsPlayer me = _me?.Invoke();

            if (_enabled == false || _game == null || me == null || me.IsAlive == false)
            {
                ClearAttackLabels();
                return;
            }

            foreach (AttackLabel label in _attackLabels.Values)
                label.Seen = false;

            List<FrontWarsAttack> incoming = me.IncomingAttacks;

            for (int i = 0; i < incoming.Count; i++)
            {
                if (incoming[i].Attacker != null && incoming[i].Attacker.Type != EFrontWarsPlayerType.Bot)
                    ShowAttackLabel(incoming[i], AttackIncomingColor);
            }

            List<FrontWarsAttack> outgoing = me.OutgoingAttacks;

            for (int i = 0; i < outgoing.Count; i++)
                ShowAttackLabel(outgoing[i], AttackOutgoingColor);

            _staleAttacks.Clear();

            foreach (KeyValuePair<string, AttackLabel> pair in _attackLabels)
            {
                if (pair.Value.Seen == false)
                    _staleAttacks.Add(pair.Key);
            }

            for (int i = 0; i < _staleAttacks.Count; i++)
            {
                AttackLabel label = _attackLabels[_staleAttacks[i]];
                label.Text.gameObject.SetActive(false);
                _textPool.Push(label.Text);
                _attackLabels.Remove(_staleAttacks[i]);
            }
        }

        private void ShowAttackLabel(FrontWarsAttack attack, Color32 color)
        {
            if (attack.AveragePosition(_game.Map, out double x, out double y) == false)
                return;

            if (_attackLabels.TryGetValue(attack.Id, out AttackLabel label) == false)
            {
                label = new AttackLabel { Text = AcquireText(AttackFontWorld, color, AttackOutlineWorld, AttackOutline) };
                _attackLabels[attack.Id] = label;
            }

            label.Seen = true;
            label.Text.color = color;
            label.Text.text = FrontWarsNumberFormat.Troops(attack.Troops);

            // 평균 자리는 칸 번호라 칸 가운데로 반 칸 민다 (다른 연출과 같은 기준)
            label.Text.transform.localPosition = FrontWarsWorld.FromTileCoords(x + 0.5, y + 0.5) + new Vector3(0f, 0f, -0.24f);
        }

        private void ClearAttackLabels()
        {
            foreach (AttackLabel label in _attackLabels.Values)
            {
                label.Text.gameObject.SetActive(false);
                _textPool.Push(label.Text);
            }

            _attackLabels.Clear();
        }

        private Vector3 Where(int tile)
        {
            if (_renderer != null)
                return _renderer.TileToWorld(tile);

            return _game != null ? FrontWarsWorld.TileCenter(_game.Map, tile) : Vector3.zero;
        }

        // ── 연출 만들기

        private void AddBurst(Vector3 at, float size, float life, float delay, Color color, float fadeStart, bool fire = true)
        {
            SpriteRenderer sprite = AcquireSprite(Disc());
            sprite.enabled = delay <= 0f;

            _effects.Add(new Effect
            {
                Kind = EKind.Burst,
                Sprite = sprite,
                Origin = at,
                Size = size,
                Life = life,
                Delay = delay,
                Color = color,
                FadeStart = fadeStart,
                Fire = fire
            });

            Trim();
        }

        private void AddRing(Vector3 at, float maxRadius, float life)
        {
            LineRenderer ring = AcquireLine(RingSegments, true);

            _effects.Add(new Effect
            {
                Kind = EKind.Ring,
                Ring = ring,
                Origin = at,
                Size = maxRadius,
                Life = life,
                Color = Color.white
            });

            Trim();
        }

        private void AddSinking(Vector3 at, FrontWarsPlayer owner)
        {
            Color32 color = _renderer != null && _renderer.Colors != null && owner != null
                ? _renderer.Colors.TerritoryColor(owner)
                : new Color32(255, 255, 255, 255);

            SpriteRenderer sprite = AcquireSprite(FrontWarsIconFactory.Get(FrontWarsIconFactory.EShape.Warship));

            _effects.Add(new Effect
            {
                Kind = EKind.Sinking,
                Sprite = sprite,
                Origin = at,
                Size = SinkingSize,
                Life = SinkingSeconds,
                Color = color
            });

            Trim();
        }

        /// <summary>한도를 넘으면 오래된 것부터 버린다.</summary>
        private void Trim()
        {
            while (_effects.Count > MaxActiveEffects)
            {
                Release(_effects[0]);
                _effects.RemoveAt(0);
            }
        }

        // ── 갱신

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                Effect e = _effects[i];
                e.Age += dt;

                if (e.Age < e.Delay)
                    continue;

                float p = Mathf.Clamp01((e.Age - e.Delay) / e.Life);

                switch (e.Kind)
                {
                    case EKind.Burst: UpdateBurst(e, p); break;
                    case EKind.Ring: UpdateRing(e, p); break;
                    case EKind.Sinking: UpdateSinking(e, p); break;
                    case EKind.Conquest: UpdateConquest(e, p); break;
                    case EKind.Gold: UpdateGold(e, p); break;
                    case EKind.Debris: UpdateDebris(e, p); break;
                }

                if (p < 1f)
                    continue;

                Release(e);
                _effects.RemoveAt(i);
            }

            UpdateBoatMarkers(dt);
            UpdateAttackLabels();
        }

        /// <summary>핵 파편 — 제자리에서 조금 커지며 위로 오르다 옅어진다 (원본은 반복 시트다).</summary>
        private void UpdateDebris(Effect e, float p)
        {
            float side = e.Size * (0.6f + 0.4f * p);
            e.Sprite.enabled = true;
            e.Sprite.transform.localPosition = e.Origin + new Vector3(0f, DebrisRise * p, -0.3f);
            e.Sprite.transform.localScale = new Vector3(side, side, 1f);

            float alpha = p <= e.FadeStart ? 1f : 1f - (p - e.FadeStart) / (1f - e.FadeStart);

            // 불 파편만 불꽃색으로 식는다. 연기는 제 색 그대로다
            Color tint = e.Fire ? Color.Lerp(new Color(e.Color.r, e.Color.g, e.Color.b, 1f), BurstCold, p) : e.Color;
            e.Sprite.color = new Color(tint.r, tint.g, tint.b, alpha);
        }

        /// <summary>불덩이 — 앞 30% 에 펴지고, 불투명 구간이 끝나면 1−t² 로 사라진다 `[소스] BMmXchgl.js:77% 지점까지 1, 이후 1−t²`.</summary>
        private void UpdateBurst(Effect e, float p)
        {
            float grow = Mathf.Clamp01(p / 0.3f);
            float side = e.Size * (0.45f + 0.55f * grow);
            e.Sprite.enabled = true;
            e.Sprite.transform.localPosition = e.Origin + new Vector3(0f, 0f, -0.3f);
            e.Sprite.transform.localScale = new Vector3(side, side, 1f);

            float alpha = p <= e.FadeStart
                ? 1f
                : 1f - Mathf.Pow((p - e.FadeStart) / Mathf.Max(1e-4f, 1f - e.FadeStart), 2f);
            Color hot = new Color(e.Color.r, e.Color.g, e.Color.b, 1f);
            Color tint = e.Fire == false ? hot
                : p < 0.5f ? Color.Lerp(hot, BurstMid, p * 2f)
                : Color.Lerp(BurstMid, BurstCold, (p - 0.5f) * 2f);
            e.Sprite.color = new Color(tint.r, tint.g, tint.b, alpha);
        }

        /// <summary>흰 고리 — 반지름 0 → 최대, 두께 0.5칸 고정, 투명도 1 → 0 직선.</summary>
        private void UpdateRing(Effect e, float p)
        {
            float radius = e.Size * p;

            for (int i = 0; i < RingSegments; i++)
            {
                float a = 2f * Mathf.PI * i / RingSegments;
                _ringPoints[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }

            e.Ring.transform.localPosition = e.Origin + new Vector3(0f, 0f, -0.31f);
            e.Ring.SetPositions(_ringPoints);
            e.Ring.widthMultiplier = RingWidth;
            var color = new Color(1f, 1f, 1f, 1f - p);
            e.Ring.startColor = color;
            e.Ring.endColor = color;
        }

        /// <summary>가라앉는 배 — 기울며 내려가고 사라진다 (원본은 14프레임 시트).</summary>
        private void UpdateSinking(Effect e, float p)
        {
            float side = e.Size * (1f - 0.25f * p);
            e.Sprite.transform.localPosition = e.Origin + new Vector3(0f, -e.Size * 0.3f * p, -0.29f);
            e.Sprite.transform.localScale = new Vector3(side, side, 1f);
            e.Sprite.transform.localRotation = Quaternion.Euler(0f, 0f, -35f * p);
            e.Sprite.color = new Color(e.Color.r, e.Color.g, e.Color.b, 1f - p * p);
        }

        /// <summary>정복 — 퍼지는 원 · 흔들리며 커지는 표식 · 위로 오르는 「+N gold」.</summary>
        private void UpdateConquest(Effect e, float p)
        {
            float diameter = ConquestCircleRadius * (1f + ConquestCircleGrow * p) * 2f;
            e.Sprite.transform.localPosition = e.Origin + new Vector3(0f, 0f, -0.28f);
            e.Sprite.transform.localScale = new Vector3(diameter, diameter, 1f);
            e.Sprite.color = new Color(e.Color.r, e.Color.g, e.Color.b, ConquestCircleAlpha * (1f - p));

            float alpha = p <= ConquestFadeStart ? 1f : 1f - (p - ConquestFadeStart) / (1f - ConquestFadeStart);
            float markScale = e.Size * (ConquestMarkBase + ConquestMarkGrow * EaseOutBack(p));
            float wobble = Mathf.Sin(e.Age * 1000f * ConquestWobbleSpeed) * ConquestWobbleRadians * Mathf.Rad2Deg;
            e.Mark.transform.localPosition = e.Origin + new Vector3(0f, ConquestMarkRise, -0.27f);
            e.Mark.transform.localScale = new Vector3(markScale, markScale, 1f);
            e.Mark.transform.localRotation = Quaternion.Euler(0f, 0f, wobble);
            e.Mark.color = new Color(e.Color.r, e.Color.g, e.Color.b, alpha);

            // 글자 자리 — 원본 캔버스는 y 가 아래로 +10 에서 −6 까지 간다 (월드에서는 위로 16칸)
            float offset = Mathf.Lerp(ConquestLabelStartY, ConquestLabelEndY, EaseOut(p));
            e.Text.transform.localPosition = e.Origin + new Vector3(0f, -offset, -0.26f);
            e.Text.color = new Color(e.Color.r, e.Color.g, e.Color.b, alpha);
        }

        /// <summary>떠오르는 금 — 1100ms 동안 19칸 위로 곧게, 투명도 1 → 0 직선.</summary>
        private void UpdateGold(Effect e, float p)
        {
            e.Text.transform.localPosition = e.Origin + new Vector3(0f, GoldRise * p, -0.25f);
            e.Text.color = new Color(1f, 1f, 1f, 1f - p);
        }

        private static float EaseOut(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        /// <summary>튕기며 커지는 보간 `[소스] BMmXchgl.js:easeOutBack`.</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        // ── 풀

        private SpriteRenderer AcquireSprite(Sprite sprite)
        {
            SpriteRenderer view;

            if (_spritePool.Count > 0)
            {
                view = _spritePool.Pop();
                view.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Fx");
                go.transform.SetParent(transform, false);
                view = go.AddComponent<SpriteRenderer>();
                view.sortingOrder = EffectOrder;
            }

            view.enabled = true;
            view.sprite = sprite;
            view.transform.localRotation = Quaternion.identity;
            return view;
        }

        /// <summary>선 하나를 빌린다 — 점 수와 이어 붙임은 쓰는 쪽이 정한다 (온 고리 · 점선 조각을 같은 풀에서 쓴다).</summary>
        private LineRenderer AcquireLine(int points, bool loop)
        {
            LineRenderer line;

            if (_ringPool.Count > 0)
            {
                line = _ringPool.Pop();
                line.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("FxLine");
                go.transform.SetParent(transform, false);
                line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.numCapVertices = 0;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.sortingOrder = EffectOrder;
                line.sharedMaterial = SpriteMaterial();
            }

            line.loop = loop;
            line.positionCount = points;
            return line;
        }

        private TextMeshPro AcquireText(float worldSize, Color color, float outlineWorld, Color32 outlineColor)
        {
            TextMeshPro text;

            if (_textPool.Count > 0)
            {
                text = _textPool.Pop();
                text.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("FxLabel");
                go.transform.SetParent(transform, false);
                text = go.AddComponent<TextMeshPro>();
                text.font = FrontWarsFonts.Korean;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.sortingOrder = LabelOrder;
            }

            text.fontSize = worldSize * TmpSizePerWorldUnit;
            text.color = color;
            text.outlineWidth = Mathf.Clamp01(outlineWorld / worldSize);
            text.outlineColor = outlineColor;
            return text;
        }

        private void Release(Effect effect)
        {
            if (effect.Sprite != null)
            {
                effect.Sprite.gameObject.SetActive(false);
                _spritePool.Push(effect.Sprite);
            }

            if (effect.Mark != null)
            {
                effect.Mark.gameObject.SetActive(false);
                _spritePool.Push(effect.Mark);
            }

            if (effect.Ring != null)
            {
                effect.Ring.gameObject.SetActive(false);
                _ringPool.Push(effect.Ring);
            }

            if (effect.Text != null)
            {
                effect.Text.gameObject.SetActive(false);
                _textPool.Push(effect.Text);
            }
        }

        /// <summary>고리에 쓸 재질 — 스프라이트 기본 재질을 빌려 온다 (셰이더 이름을 찾지 않는다).</summary>
        private Material SpriteMaterial()
        {
            if (_spriteMaterial != null)
                return _spriteMaterial;

            var probe = new GameObject("FxMaterialProbe") { hideFlags = HideFlags.HideAndDontSave };
            probe.transform.SetParent(transform, false);
            var sr = probe.AddComponent<SpriteRenderer>();
            _spriteMaterial = sr.sharedMaterial;
            Destroy(probe);
            return _spriteMaterial;
        }

        /// <summary>한 변 1칸짜리 둥근 빛 스프라이트 — 코드로 그린다 (가운데가 밝고 가장자리로 사라진다).</summary>
        private Sprite Disc()
        {
            if (_disc != null)
                return _disc;

            var tex = new Texture2D(DiscTextureSize, DiscTextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "FrontWarsFxDisc"
            };

            float half = DiscTextureSize * 0.5f;
            var pixels = new Color32[DiscTextureSize * DiscTextureSize];

            for (int y = 0; y < DiscTextureSize; y++)
            {
                for (int x = 0; x < DiscTextureSize; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);   // 가장자리를 부드럽게
                    pixels[y * DiscTextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _disc = Sprite.Create(tex, new Rect(0, 0, DiscTextureSize, DiscTextureSize), new Vector2(0.5f, 0.5f), DiscTextureSize);
            _disc.name = "FrontWarsFxDisc";
            return _disc;
        }
    }
}
